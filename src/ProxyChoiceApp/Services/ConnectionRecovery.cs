using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyChoiceApp.Services;

internal static class ConnectionRecovery
{
    internal static async Task RunAsync(Func<Task> prepare, Func<CancellationToken, Task> connect,
        Func<bool> online, Action<int> waiting, Action<Exception> failed, CancellationToken token,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        var attempt = 0;
        while (true) {
            token.ThrowIfCancellationRequested();
            await prepare();
            var seconds = Math.Min(60, 5 * (1 << Math.Min(attempt, 4))); attempt++;
            waiting(seconds);
            await delay(TimeSpan.FromSeconds(seconds), token);
            token.ThrowIfCancellationRequested();
            if (!online()) continue;
            try { await connect(token); token.ThrowIfCancellationRequested(); return; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { failed(error); }
        }
    }
}
