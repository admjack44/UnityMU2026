using System.Buffers.Binary;
using System.Net.Sockets;
using MUServer.Core.Network;

namespace MUServer.GameServer.Network;

public sealed class MobileClientSession
{
    private readonly TcpClient _client;
    private readonly MobileGamePacketDispatcher _dispatcher;

    public MobileClientSession(TcpClient client, MobileGamePacketDispatcher dispatcher)
    {
        _client = client;
        _dispatcher = dispatcher;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using NetworkStream stream = _client.GetStream();

        while (!cancellationToken.IsCancellationRequested && _client.Connected)
        {
            byte[] header = await ReadExactAsync(stream, MobilePacket.HeaderSize, cancellationToken);

            if (header[0] != MobilePacket.Header)
                return;

            ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1, 2));
            ushort opCodeValue = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3, 2));

            int bodyLength = length - MobilePacket.HeaderSize;

            if (bodyLength < 0)
                return;

            byte[] body = bodyLength > 0
                ? await ReadExactAsync(stream, bodyLength, cancellationToken)
                : Array.Empty<byte>();

            var packet = new MobilePacket(
                (MobileOpCode)opCodeValue,
                body
            );

            byte[] response = await _dispatcher.DispatchAsync(packet, cancellationToken);

            await stream.WriteAsync(response, cancellationToken);
        }
    }

    private static async Task<byte[]> ReadExactAsync(
        NetworkStream stream,
        int length,
        CancellationToken cancellationToken
    )
    {
        byte[] buffer = new byte[length];
        int offset = 0;

        while (offset < length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(offset, length - offset),
                cancellationToken
            );

            if (read <= 0)
                throw new IOException("Conexión cerrada por el cliente.");

            offset += read;
        }

        return buffer;
    }
}