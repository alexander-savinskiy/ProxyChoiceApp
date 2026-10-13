using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Tests;

internal sealed class MockProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentBag<Task> _clients = new();
    private readonly Task _accept;
    private readonly ProxyProtocol _protocol;
    private readonly X509Certificate2? _certificate;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int AuthAccepted, AuthRejected, Connections;
    public string Username = "test-user", Password = "test-secret";
    public string? LastTarget;
    public bool Anonymous;
    public bool StallResponse;

    public MockProxy(ProxyProtocol protocol)
    {
        _protocol = protocol;
        if (protocol == ProxyProtocol.Https)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }
        _listener.Start();
        _accept = AcceptLoopAsync();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _clients.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            Interlocked.Increment(ref Connections);
            try
            {
                Stream stream = client.GetStream();
                if (_protocol == ProxyProtocol.Https)
                {
                    var tls = new SslStream(stream, false);
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate }, _stop.Token);
                    stream = tls;
                }
                if (_protocol == ProxyProtocol.Socks5)
                {
                    var greeting = await Bytes(stream, 2);
                    if (greeting[0] != 5) throw new IOException("Bad greeting");
                    await Bytes(stream, greeting[1]);
                    await stream.WriteAsync(new byte[] { 5, Anonymous ? (byte)0 : (byte)2 }, _stop.Token);
                    if (!Anonymous)
                    {
                        var auth = await Bytes(stream, 2);
                        var user = Encoding.UTF8.GetString(await Bytes(stream, auth[1]));
                        var length = (await Bytes(stream, 1))[0];
                        var password = Encoding.UTF8.GetString(await Bytes(stream, length));
                        var accepted = user == Username && password == Password;
                        await stream.WriteAsync(new byte[] { 1, accepted ? (byte)0 : (byte)1 }, _stop.Token);
                        if (!accepted) { Interlocked.Increment(ref AuthRejected); return; }
                    }
                    Interlocked.Increment(ref AuthAccepted);
                    var connect = await Bytes(stream, 4);
                    if (connect[1] != 1) throw new IOException("Expected TCP CONNECT");
                    LastTarget = connect[3] switch {
                        1 => new IPAddress(await Bytes(stream, 4)).ToString(),
                        4 => new IPAddress(await Bytes(stream, 16)).ToString(),
                        3 => Encoding.ASCII.GetString(await Bytes(stream, (await Bytes(stream, 1))[0])),
                        _ => throw new IOException("Bad address type")
                    };
                    await Bytes(stream, 2);
                    await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 80 }, _stop.Token);
                }
                else
                {
                    var header = await Header(stream, _stop.Token);
                    if (!header.StartsWith("CONNECT ", StringComparison.Ordinal)) throw new IOException("Expected CONNECT");
                    var expected = "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(Username + ":" + Password));
                    if (!Anonymous && !header.Contains(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref AuthRejected);
                        await stream.WriteAsync("HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), _stop.Token);
                        return;
                    }
                    Interlocked.Increment(ref AuthAccepted);
                    LastTarget = header.Split(' ')[1];
                    await stream.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), _stop.Token);
                }
                var requestHeader = await Header(stream, _stop.Token);
                if (!requestHeader.StartsWith("GET ", StringComparison.Ordinal)) throw new IOException("Expected GET");
                if (StallResponse) { await Task.Delay(Timeout.Infinite, _stop.Token); return; }
                var body = Encoding.ASCII.GetBytes("proxied");
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n"), _stop.Token);
                await stream.WriteAsync(body, _stop.Token);
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or AuthenticationException) { }
        }
    }

    private async Task<byte[]> Bytes(Stream stream, int count)
    { var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, _stop.Token); return bytes; }

    public static async Task<string> Header(Stream stream, CancellationToken token)
    {
        var value = new StringBuilder(); var one = new byte[1];
        while (value.Length < 8192)
        {
            await stream.ReadExactlyAsync(one, token); value.Append((char)one[0]);
            if (value.Length >= 4 && value.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) return value.ToString();
        }
        throw new IOException("Header too long");
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _listener.Stop();
        await _accept; await Task.WhenAll(_clients);
        _certificate?.Dispose(); _stop.Dispose();
    }
}

internal sealed class DirectOrigin : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accept;
    public int Requests;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public DirectOrigin() { _listener.Start(); _accept = LoopAsync(); }
    private async Task LoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var stream = client.GetStream();
                await MockProxy.Header(stream, _stop.Token);
                Interlocked.Increment(ref Requests);
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 6\r\n\r\ndirect"u8.ToArray(), _stop.Token);
            }
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync() { _stop.Cancel(); _listener.Stop(); await _accept; _stop.Dispose(); }
}
