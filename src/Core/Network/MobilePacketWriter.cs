using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace MUServer.Core.Network;

public sealed class MobilePacketWriter
{
    private byte[] _body = Array.Empty<byte>();

    public MobilePacketWriter WriteJson<T>(T value)
    {
        string json = JsonSerializer.Serialize(value);
        _body = Encoding.UTF8.GetBytes(json);
        return this;
    }

    public byte[] Build(MobileOpCode opCode)
    {
        byte[] packet = new byte[MobilePacket.HeaderSize + _body.Length];

        packet[0] = MobilePacket.Header;

        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1, 2), (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(3, 2), (ushort)opCode);

        Buffer.BlockCopy(_body, 0, packet, MobilePacket.HeaderSize, _body.Length);

        return packet;
    }
}