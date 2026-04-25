using System.Net;
using System.Net.Sockets;

namespace MUServer.Core.Server;

public class GameServerHost
{
    private readonly TcpListener _listener;

    public GameServerHost(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        Console.WriteLine($"[SERVER] Listening on port {_listener.LocalEndpoint}");

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        Console.WriteLine($"[CLIENT] Connected: {client.Client.RemoteEndPoint}");

        using var stream = client.GetStream();

        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, ct);

            if (read <= 0)
                break;

            Console.WriteLine($"[RECV] {read} bytes");
        }

        Console.WriteLine($"[CLIENT] Disconnected");
    }
}