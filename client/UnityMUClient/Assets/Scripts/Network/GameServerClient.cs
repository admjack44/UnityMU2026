using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GameServerClient : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 55901;

    private TcpClient client;
    private NetworkStream stream;

    private enum OpCode : ushort
    {
        LoginRequest = 1000,
        LoginResponse = 1001
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(20, 100, 240, 60), "LOGIN TEST"))
        {
            _ = Login("test", "1234");
        }
    }

    async Task Login(string user, string pass)
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(ip, port);

            stream = client.GetStream();

            Debug.Log("Conectado al GameServer");

            var body = Encoding.UTF8.GetBytes($"{user}:{pass}");

            var packet = BuildPacket(OpCode.LoginRequest, body);

            await stream.WriteAsync(packet, 0, packet.Length);

            Debug.Log("Login enviado");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    byte[] BuildPacket(OpCode opCode, byte[] body)
    {
        byte[] packet = new byte[5 + body.Length];

        packet[0] = 0xA1;

        WriteUShort(packet, 1, (ushort)packet.Length);
        WriteUShort(packet, 3, (ushort)opCode);

        Buffer.BlockCopy(body, 0, packet, 5, body.Length);

        return packet;
    }

    void WriteUShort(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
}