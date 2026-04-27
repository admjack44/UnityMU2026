using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public sealed class GameServerLoginClient : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 55901;
    public string username = "test";
    public string password = "1234";
    public string clientVersion = "0.1.0";
    public string deviceId = "editor-windows";

    private bool isBusy;

    [Serializable]
    private sealed class LoginRequestDto
    {
        public string Username;
        public string Password;
        public string ClientVersion;
        public string DeviceId;
    }

    [Serializable]
    private sealed class LoginResponseDto
    {
        public bool Success;
        public string Message;
        public int AccountId;
        public string SessionToken;
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(20, 90, 240, 60), "LOGIN GAME SERVER"))
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
            Task connectTask = client.ConnectAsync(ip, port);
            Task timeoutTask = Task.Delay(5000);

            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                Debug.LogError("Timeout conectando al GameServer.");
                return;
            }

            NetworkStream stream = client.GetStream();

            LoginRequestDto login = new LoginRequestDto
            {
                Username = username,
                Password = password,
                ClientVersion = clientVersion,
                DeviceId = deviceId
            };

            string json = JsonUtility.ToJson(login);
            byte[] request = MobilePacket.Build(MobileOpCode.LoginRequest, json);
            await stream.WriteAsync(request, 0, request.Length);

            Debug.Log("LoginRequest enviado.");

            byte[] header = new byte[5];
            await ReadExactAsync(stream, header, header.Length);

            if (header[0] != 0xA1)
            {
                Debug.LogError($"Header inválido: {header[0]:X2}");
                return;
            }

            ushort length = MobilePacket.ReadUShort(header, 1);
            ushort opCodeValue = MobilePacket.ReadUShort(header, 3);

            byte[] body = new byte[length - 5];
            await ReadExactAsync(stream, body, body.Length);

            byte[] fullPacket = new byte[length];
            Buffer.BlockCopy(header, 0, fullPacket, 0, 5);
            Buffer.BlockCopy(body, 0, fullPacket, 5, body.Length);

            string responseJson = MobilePacket.ReadString(fullPacket);
            Debug.Log($"OpCode recibido: {opCodeValue}");
            Debug.Log($"LoginResponse JSON: {responseJson}");

            LoginResponseDto response = JsonUtility.FromJson<LoginResponseDto>(responseJson);
            Debug.Log(response.Success
                ? $"LOGIN OK AccountId={response.AccountId} Token={response.SessionToken}"
                : $"LOGIN FAIL: {response.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error Login: " + ex);
        }
        finally
        {
            isBusy = false;
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int length)
    {
        int offset = 0;
        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer, offset, length - offset);
            if (read <= 0)
                throw new Exception("Conexión cerrada antes de terminar la lectura.");
            offset += read;
        }
    }
}
