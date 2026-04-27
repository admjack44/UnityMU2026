using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace MUServer.Core.Network;

public sealed class MobilePacketReader
{
    private readonly byte[] _data;
    private int _offset;

    public MobilePacketReader(byte[] data, bool bodyOnly = false)
    {
        _data = data;
        _offset = bodyOnly ? 0 : MobilePacket.HeaderSize;
    }

    public ushort ReadUShort()
    {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(_offset, 2));
        _offset += 2;
        return value;
    }

    public string ReadString()
    {
        ushort length = ReadUShort();

        string value = Encoding.UTF8.GetString(_data, _offset, length);
        _offset += length;

        return value;
    }

    public T? ReadJson<T>()
    {
        string json = ReadString();

        return JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
    }
}