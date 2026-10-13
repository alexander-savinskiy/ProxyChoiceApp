using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyChoiceApp.Tests;

internal sealed class MockDns : IAsyncDisposable
{
    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    public int Requests;
    public int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;
    public MockDns() => _loop = ServeAsync();

    private async Task ServeAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var packet = await _server.ReceiveAsync(_stop.Token);
                var request = packet.Buffer;
                if (request.Length < 17) continue;
                var position = 12;
                while (position < request.Length && request[position] != 0) position += request[position] + 1;
                if (position + 5 > request.Length) continue;
                var type = (request[position + 1] << 8) | request[position + 2];
                var reply = new List<byte>(request.Take(position + 5));
                reply[2] = 0x81; reply[3] = 0x80;
                reply[6] = 0; reply[7] = (byte)(type == 1 ? 1 : 0);
                reply[8] = reply[9] = reply[10] = reply[11] = 0;
                if (type == 1) reply.AddRange(new byte[] { 0xC0, 12, 0, 1, 0, 1, 0, 0, 0, 30, 0, 4, 127, 0, 0, 1 });
                Interlocked.Increment(ref Requests);
                await _server.SendAsync(reply.ToArray(), packet.RemoteEndPoint, _stop.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    { _stop.Cancel(); await _loop; _server.Dispose(); _stop.Dispose(); }
}
