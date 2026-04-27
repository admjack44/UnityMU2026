using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ConnectServerClient : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 44405;

    private bool isRequesting;

    private enum OpCode : ushort
    {
        ServerListRequest = 100,
        ServerListResponse = 101
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(20, 20, 240, 60), "GET SERVER LIST"))
        {
            if (!isRequesting)
                _ = RequestServerList();
        }
    }

    private async Task RequestServerList()
    {
        isRequesting = true;

        try
        {
            Debug.Log($"Conectando al ConnectServer {ip}:{port}...");

            using TcpClient client = new TcpClient();

            Task connectTask = client.ConnectAsync(ip, port);
            Task timeoutTask = Task.Delay(5000);

            Task finished = await Task.WhenAny(connectTask, timeoutTask);

            if (finished == timeoutTask)
            {
                Debug.LogError("Timeout conectando al ConnectServer. Revisa si el servidor está abierto.");
                return;
            }

            Debug.Log("Conectado correctamente.");

            NetworkStream stream = client.GetStream();

            byte[] request = BuildPacket(OpCode.ServerListRequest, Array.Empty<byte>());
            await stream.WriteAsync(request, 0, request.Length);

            Debug.Log("Request enviada al ConnectServer.");

            byte[] header = new byte[5];
            await ReadExact(stream, header, 5);

            Debug.Log($"Header recibido: {BitConverter.ToString(header)}");

            if (header[0] != 0xA1)
            {
                Debug.LogError($"Header inválido: {header[0]:X2}");
                return;
            }

            ushort length = ReadUShort(header, 1);
            ushort opcode = ReadUShort(header, 3);

            Debug.Log($"Length: {length}");
            Debug.Log($"OpCode recibido: {opcode}");

            byte[] body = new byte[length - 5];
            await ReadExact(stream, body, body.Length);

            string json = Encoding.UTF8.GetString(body);

            Debug.Log("Server list JSON:");
            Debug.Log(json);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error ConnectServerClient: " + ex);
        }
        finally
        {
            isRequesting = false;
        }
    }

    private static byte[] BuildPacket(OpCode opCode, byte[] body)
    {
        byte[] packet = new byte[5 + body.Length];

        packet[0] = 0xA1;

        WriteUShort(packet, 1, (ushort)packet.Length);
        WriteUShort(packet, 3, (ushort)opCode);

        if (body.Length > 0)
            Buffer.BlockCopy(body, 0, packet, 5, body.Length);

        return packet;
    }

    private static async Task ReadExact(NetworkStream stream, byte[] buffer, int length)
    {
        int offset = 0;

        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer, offset, length - offset);

            if (read <= 0)
                throw new Exception("El servidor cerró la conexión antes de completar la lectura.");

            offset += read;
        }
    }

    private static void WriteUShort(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static ushort ReadUShort(byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }
}