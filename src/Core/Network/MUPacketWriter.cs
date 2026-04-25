namespace MUServer.Core.Network;

public static class MUPacketWriter
{
    public static byte[] C1(byte code, params byte[] payload)
    {
        var length = 3 + payload.Length;
        if (length > byte.MaxValue)
            throw new InvalidOperationException("C1 packet demasiado grande.");

        var packet = new byte[length];
        packet[0] = 0xC1;
        packet[1] = (byte)length;
        packet[2] = code;
        Buffer.BlockCopy(payload, 0, packet, 3, payload.Length);
        return packet;
    }

    public static byte[] C1(byte code, byte subCode, params byte[] payload)
    {
        var fullPayload = new byte[1 + payload.Length];
        fullPayload[0] = subCode;
        Buffer.BlockCopy(payload, 0, fullPayload, 1, payload.Length);
        return C1(code, fullPayload);
    }
}
