namespace MUServer.Core.Network;

public static class MobilePacketJson
{
    public static byte[] Create<T>(MobileOpCode opCode, T data)
    {
        return new MobilePacketWriter()
            .WriteJson(data)
            .Build(opCode);
    }
}