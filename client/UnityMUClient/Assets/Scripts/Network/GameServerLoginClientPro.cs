using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public sealed class GameServerLoginClientPro : MonoBehaviour
{
    [Header("GameServer")]
    public string ip = "127.0.0.1";
    public int port = 55901;

    [Header("Login Test")]
    public string username = "test";
    public string password = "1234";
    public string version = "0.1.0";

    private bool isBusy;

    private enum OpCode : ushort
    {
        LoginRequest = 1000,
        LoginResponse = 1001,
        Error = 9000
    }

    [Serializable]
    private sealed class LoginRequestDto
    {
        public string Username;
        public string Password;
        public string Version;
        public string DeviceId;
        public string Platform;
    }

    [Serializable]
    private sealed class LoginResponseDto
    {
        public bool Success;
        public string Message;
        public string SessionToken;
        public int AccountId;
        public string Username;
        public string ServerTimeUtc;
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(20, 100, 260, 60), isBusy ? "LOGGING..." : "LOGIN GAME SERVER"))
        {
            if (!isBusy)
                _ = LoginAsync();
        }
    }

    private async Task LoginAsync()
    {
        isBusy = true;

        try
        {
            Debug.Log($"Conectando al GameServer {ip}:{port}...");

            using TcpClient client = new TcpClient();
            await client.ConnectAsync(ip, port);

            Debug.Log("Conectado al GameServer.");

            NetworkStream stream = client.GetStream();

            var login = new LoginRequestDto
            {
                Username = username,
                Password = password,
                Version = version,
                DeviceId = SystemInfo.deviceUniqueIdentifier,
                Platform = Application.platform.ToString()
            };

            string json = JsonUtility.ToJson(login);
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] packet = BuildPacket(OpCode.LoginRequest, body);

            await stream.WriteAsync(packet, 0, packet.Length);
            Debug.Log("LoginRequest enviado: " + json);

            byte[] header = new byte[5];
            await ReadExactAsync(stream, header, header.Length);

            if (header[0] != 0xA1)
                throw new Exception($"Header inválido: 0x{header[0]:X2}");

            ushort length = ReadUShort(header, 1);
            ushort opCode = ReadUShort(header, 3);

            byte[] responseBody = new byte[length - 5];
            await ReadExactAsync(stream, responseBody, responseBody.Length);

            string responseJson = Encoding.UTF8.GetString(responseBody);
            Debug.Log($"LoginResponse OpCode={opCode} JSON={responseJson}");

            LoginResponseDto response = JsonUtility.FromJson<LoginResponseDto>(responseJson);

            if (response.Success)
            {
                Debug.Log($"LOGIN OK AccountId={response.AccountId} User={response.Username} Token={response.SessionToken}");
            }
            else
            {
                Debug.LogError("LOGIN FAIL: " + response.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("GameServerLoginClientPro error: " + ex);
        }
        finally
        {
            isBusy = false;
        }
    }

    private static byte[] BuildPacket(OpCode opCode, byte[] body)
    {
        byte[] packet = new byte[5 + body.Length];
        packet[0] = 0xA1;
        WriteUShort(packet, 1, (ushort)packet.Length);
        WriteUShort(packet, 3, (ushort)opCode);
        Buffer.BlockCopy(body, 0, packet, 5, body.Length);
        return packet;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int length)
    {
        int offset = 0;
        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer, offset, length - offset);
            if (read <= 0)
                throw new Exception("Conexión cerrada antes de completar lectura.");

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
