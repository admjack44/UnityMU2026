using System;
using System.Text;
using UnityEngine;

public enum MobileOpCode : ushort
{
    ServerListRequest = 100,
    ServerListResponse = 101,
    LoginRequest = 1000,
    LoginResponse = 1001,
    Error = 9000
}

public static class MobilePacket
{
    public static byte[] Build(MobileOpCode opCode, string jsonBody)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);
        byte[] body = new byte[2 + jsonBytes.Length];
        WriteUShort(body, 0, (ushort)jsonBytes.Length);
        Buffer.BlockCopy(jsonBytes, 0, body, 2, jsonBytes.Length);

        byte[] packet = new byte[5 + body.Length];
        packet[0] = 0xA1;
        WriteUShort(packet, 1, (ushort)packet.Length);
        WriteUShort(packet, 3, (ushort)opCode);
        Buffer.BlockCopy(body, 0, packet, 5, body.Length);
        return packet;
    }

    public static ushort ReadUShort(byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }

    public static string ReadString(byte[] packet, int offset = 5)
    {
        ushort size = ReadUShort(packet, offset);
        return Encoding.UTF8.GetString(packet, offset + 2, size);
    }

    private static void WriteUShort(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
}
