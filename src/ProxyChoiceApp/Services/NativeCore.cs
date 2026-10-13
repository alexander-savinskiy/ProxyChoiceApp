using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ProxyChoiceApp.Services;

// A hidden console permits a graceful Ctrl+C shutdown. A job terminates the core if the GUI crashes.
public sealed class NativeCore : IAsyncDisposable
{
    private delegate bool ConsoleHandler(uint signal);
    private static readonly ConsoleHandler IgnoreOwnSignal = signal => signal is 0 or 1;
    private static readonly IntPtr IgnoreOwnSignalPointer = Marshal.GetFunctionPointerForDelegate(IgnoreOwnSignal);
    private readonly SafeFileHandle _job;
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);
    private readonly object _lifecycle = new();
    private Task? _stopTask, _disposeTask;
    private bool _disposed;
    private int? _exitCode;
    public Process Process { get; }
    public int ProcessId { get; }
    public bool HasExited => _disposed || Process.HasExited;
    public int? ExitCode => _exitCode ?? (!_disposed && Process.HasExited ? Process.ExitCode : null);
    public bool ForcedStop { get; private set; }
    public string? ShutdownDiagnostic { get; private set; }

    public NativeCore(string executable, string config) : this(executable, new[] { "run", "-c", config }) { }

    internal NativeCore(string executable, string[] arguments)
    {
        _job = CreateLifetimeJob();
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = 1, ShowWindow = 0 };
        var command = new StringBuilder(Quote(executable));
        foreach (var argument in arguments) command.Append(' ').Append(Quote(argument));
        if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false, 0x00000010 | 0x00000004,
            IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out var created))
        { _job.Dispose(); throw new Win32Exception(); }
        try
        {
            if (!AssignProcessToJobObject(_job, created.Process))
            { TerminateProcess(created.Process, 1); throw new Win32Exception(); }
            Process = System.Diagnostics.Process.GetProcessById((int)created.ProcessId);
            ProcessId = Process.Id;
            Process.EnableRaisingEvents = true;
            if (ResumeThread(created.Thread) == uint.MaxValue) throw new Win32Exception();
        }
        catch { _job.Dispose(); throw; }
        finally { CloseHandle(created.Thread); CloseHandle(created.Process); }
    }

    private static SafeFileHandle CreateLifetimeJob()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(); }
        var info = new JobExtendedInfo { Basic = new JobBasicInfo { LimitFlags = 0x2000 } };
        if (!SetInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf<JobExtendedInfo>()))
        { job.Dispose(); throw new Win32Exception(); }
        return job;
    }

    internal static SafeFileHandle Guard(Process process)
    {
        var job = CreateLifetimeJob();
        if (!AssignProcessToJobObject(job, process.Handle)) { job.Dispose(); throw new Win32Exception(); }
        return job;
    }

    public Task StopAsync()
    {
        lock (_lifecycle) return _stopTask ??= StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        if (Process.HasExited) { _exitCode = Process.ExitCode; return; }
        // Console attachment belongs to the whole GUI process, so concurrent shutdowns must serialize.
        await ConsoleGate.WaitAsync();
        // No terminal is opened or shown to the user.
        var hadConsole = false;
        var attached = false;
        try
        {
            if (Process.HasExited) { _exitCode = Process.ExitCode; return; }
            hadConsole = FreeConsole();
            attached = AttachConsole((uint)ProcessId);
            if (attached)
            {
                // The handler stays registered until the asynchronous console event has been delivered.
                SetConsoleCtrlHandler(IgnoreOwnSignalPointer, true);
                if (!GenerateConsoleCtrlEvent(0, 0)) ShutdownDiagnostic = "GenerateConsoleCtrlEvent: " + Marshal.GetLastWin32Error();
            }
            else ShutdownDiagnostic = "AttachConsole: " + Marshal.GetLastWin32Error();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { await Process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                ForcedStop = true;
                // Closing our job kills this core and its children, never unrelated sing-box processes.
                _job.Dispose();
                try { await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { throw new IOException("Windows did not confirm network core termination in time."); }
            }
            _exitCode = Process.ExitCode;
        }
        finally
        {
            if (attached) FreeConsole();
            if (hadConsole) AttachConsole(uint.MaxValue);
            ConsoleGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycle) return new(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try { await StopAsync(); }
        finally { _job.Dispose(); _disposed = true; Process.Dispose(); }
    }

    public static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public string? Reserved; public string? Desktop; public string? Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, Reserved2; public IntPtr ReservedPointer, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicInfo
    {
        public long PerProcessTime, PerJobTime; public uint LimitFlags;
        public UIntPtr MinWorkingSet, MaxWorkingSet; public uint ActiveProcessLimit;
        public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedInfo
    {
        public JobBasicInfo Basic; public IoCounters Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes,
        bool inheritHandles, uint flags, IntPtr environment, string? directory, ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref JobExtendedInfo info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GenerateConsoleCtrlEvent(uint kind, uint processGroup);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
}
