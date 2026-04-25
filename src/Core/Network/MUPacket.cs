namespace MUServer.Core.Network;

public readonly record struct MUPacket(byte Header, byte Length, byte Code, byte SubCode, byte[] Payload)
{
    public bool HasSubCode => Length > 3;
}
