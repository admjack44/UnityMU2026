namespace MUServer.Core.Network;

public sealed class MobilePacket
{
    public const byte Header = 0xA1;
    public const int HeaderSize = 5;

    public MobileOpCode OpCode { get; }
    public byte[] Body { get; }

    public MobilePacket(MobileOpCode opCode, byte[] body)
    {
        OpCode = opCode;
        Body = body;
    }
}