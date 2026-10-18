using System.Net;
using System.Net.Sockets;

namespace Njord.IntegrationTests.Broker;

// A forwarding TCP endpoint between the host and the broker. Sever() resets every open connection without a
// protocol-level disconnect, which is what a network failure looks like to the broker (it publishes the last will).
public sealed class TcpProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<TcpClient> _connections = [];
    private readonly Lock _gate = new();
    private readonly Func<int> _targetPort;
    private readonly Task _accepting;

    public TcpProxy(Func<int> targetPort)
    {
        _targetPort = targetPort;
        _listener.Start();
        _accepting = AcceptAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = ForwardAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ForwardAsync(TcpClient client)
    {
        var upstream = new TcpClient();
        try
        {
            await upstream.ConnectAsync(IPAddress.Loopback, _targetPort(), _stop.Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            client.Dispose();
            upstream.Dispose();
            return;
        }

        lock (_gate)
        {
            _connections.Add(client);
            _connections.Add(upstream);
        }

        var toBroker = client.GetStream().CopyToAsync(upstream.GetStream(), _stop.Token);
        var toClient = upstream.GetStream().CopyToAsync(client.GetStream(), _stop.Token);
        try
        {
            await Task.WhenAny(toBroker, toClient);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
        }
        finally
        {
            client.Dispose();
            upstream.Dispose();
        }
    }

    public void Sever()
    {
        TcpClient[] open;
        lock (_gate)
        {
            open = [.. _connections];
            _connections.Clear();
        }

        foreach (var connection in open)
        {
            try
            {
                if (connection.Client is { } socket)
                {
                    socket.LingerState = new LingerOption(true, 0);
                }
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
            }

            connection.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        Sever();
        await _accepting;
        _stop.Dispose();
    }
}
