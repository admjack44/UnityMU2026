using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class MUClient : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 55901;

    [Header("World")]
    [SerializeField] private PlayerController player;
    [SerializeField] private MonsterManagerClient monsterManager;

    [Header("Movement")]
    [SerializeField] private byte currentMapId = 0;
    [SerializeField] private float moveRequestCooldownSeconds = 0.15f;

    private TcpClient client;
    private NetworkStream stream;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected;
    private bool isAutoCombatEnabled;

    private byte lastRequestedX;
    private byte lastRequestedY;
    private bool hasMoveTarget;
    private float lastMoveRequestTime;

    private readonly ConcurrentQueue<Action> mainThreadActions = new();

    private async void Start()
    {
        await ConnectAsync();
        await SendLoginAsync();
        await SendCharacterListRequestAsync();
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    public async void EnterWorldSlot0()
    {
        await SendEnterWorldAsync(0);
    }

    /// <summary>
    /// Professional movement request: the client asks for a destination once.
    /// The server owns the real position and sends D4/00 authoritative updates.
    /// </summary>
    public async void SendMoveTarget(byte x, byte y)
    {
        if (!CanSendMoveTarget(x, y))
            return;

        hasMoveTarget = true;
        lastRequestedX = x;
        lastRequestedY = y;
        lastMoveRequestTime = Time.time;

        byte[] packet =
        {
            0xC1, 0x07,
            0xD4, 0x10,
            currentMapId,
            x,
            y
        };

        await SendPacketAsync(packet);
        Debug.Log($"MoveTarget enviado -> Map:{currentMapId} X:{x} Y:{y}");
    }

    /// <summary>
    /// Compatibility wrapper for older PlayerController versions.
    /// </summary>
    public void SendMove(byte x, byte y)
    {
        SendMoveTarget(x, y);
    }

    public async void ToggleAutoCombat()
    {
        await SetAutoCombatAsync(!isAutoCombatEnabled);
    }

    public async void ToggleAutoCombatOn()
    {
        await SetAutoCombatAsync(true);
    }

    public async void ToggleAutoCombatOff()
    {
        await SetAutoCombatAsync(false);
    }

    private async Task ConnectAsync()
    {
        if (isConnected)
            return;

        cancellationTokenSource = new CancellationTokenSource();
        client = new TcpClient();

        await client.ConnectAsync(host, port);
        stream = client.GetStream();
        isConnected = true;

        Debug.Log($"Conectado al GameServer {host}:{port}");

        _ = Task.Run(() => ReadLoopAsync(cancellationTokenSource.Token));
    }

    private async Task SendLoginAsync()
    {
        byte[] packet =
        {
            0xC1, 0x05,
            0x00,
            0x01, 0x02
        };

        await SendPacketAsync(packet);
        Debug.Log("Login enviado");
    }

    private async Task SendCharacterListRequestAsync()
    {
        await Task.Delay(300);

        byte[] packet =
        {
            0xC1, 0x05,
            0xF3, 0x00,
            0x00
        };

        await SendPacketAsync(packet);
        Debug.Log("CharacterList request enviado");
    }

    private async Task SendEnterWorldAsync(byte slot)
    {
        byte[] packet =
        {
            0xC1, 0x05,
            0xF3, 0x03,
            slot
        };

        await SendPacketAsync(packet);
        Debug.Log($"EnterWorld slot {slot} enviado");
    }

    private async Task SetAutoCombatAsync(bool enabled)
    {
        byte[] packet =
        {
            0xC1, 0x05,
            0xD7, 0x10,
            (byte)(enabled ? 1 : 0)
        };

        await SendPacketAsync(packet);
        Debug.Log(enabled ? "AutoCombat ON enviado" : "AutoCombat OFF enviado");
    }

    private async Task SendPacketAsync(byte[] packet)
    {
        if (!isConnected || stream == null || !stream.CanWrite)
        {
            Debug.LogWarning("No hay conexión activa con el servidor.");
            return;
        }

        try
        {
            await stream.WriteAsync(packet, 0, packet.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error enviando packet: {ex.Message}");
            QueueDisconnect();
        }
    }

    private bool CanSendMoveTarget(byte x, byte y)
    {
        if (!isConnected)
            return false;

        if (hasMoveTarget && lastRequestedX == x && lastRequestedY == y)
            return false;

        if (Time.time - lastMoveRequestTime < moveRequestCooldownSeconds)
            return false;

        return true;
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested && isConnected && client != null && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead <= 0)
                    break;

                byte[] received = new byte[bytesRead];
                Array.Copy(buffer, received, bytesRead);

                foreach (byte[] packet in SplitPackets(received))
                {
                    mainThreadActions.Enqueue(() => HandlePacket(packet));
                }
            }
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() => Debug.LogWarning($"ReadLoop cerrado: {ex.Message}"));
        }

        QueueDisconnect();
    }

    private static byte[][] SplitPackets(byte[] buffer)
    {
        var packets = new System.Collections.Generic.List<byte[]>();
        int offset = 0;

        while (offset + 2 <= buffer.Length)
        {
            byte header = buffer[offset];

            if (header != 0xC1 && header != 0xC2)
            {
                offset++;
                continue;
            }

            int length = buffer[offset + 1];
            if (length < 3 || offset + length > buffer.Length)
                break;

            byte[] packet = new byte[length];
            Array.Copy(buffer, offset, packet, 0, length);
            packets.Add(packet);

            offset += length;
        }

        return packets.ToArray();
    }

    private void HandlePacket(byte[] packet)
    {
        if (packet.Length < 3)
            return;

        byte code = packet[2];
        byte subCode = packet.Length > 3 ? packet[3] : (byte)0;

        Debug.Log("Packet recibido: " + BitConverter.ToString(packet));

        switch (code)
        {
            case 0xF1 when subCode == 0x01:
                HandleLoginResponse(packet);
                break;

            case 0xF3 when subCode == 0x00:
                HandleCharacterList(packet);
                break;

            case 0xF3 when subCode == 0x03:
                HandleEnterWorld(packet);
                break;

            case 0xF3 when subCode == 0x10:
                Debug.Log("F3/10 recibido: spawn/info adicional de personaje.");
                break;

            case 0xF4 when subCode == 0x01:
                HandleMonsterSpawn(packet);
                break;

            case 0xF4 when subCode == 0x02:
                HandleMonsterDeath(packet);
                break;

            case 0xD4:
                HandleMoveResponse(packet);
                break;

            case 0xD7 when subCode == 0x10:
                HandleAutoCombatResponse(packet);
                break;

            default:
                Debug.Log($"Packet no manejado Code:{code:X2} SubCode:{subCode:X2}");
                break;
        }
    }

    private void HandleLoginResponse(byte[] packet)
    {
        bool success = packet.Length >= 5 && packet[4] == 0x01;
        Debug.Log(success ? "LOGIN OK" : "LOGIN FAILED");
    }

    private void HandleCharacterList(byte[] packet)
    {
        if (packet.Length < 5)
        {
            Debug.LogWarning("CharacterList packet inválido.");
            return;
        }

        int count = packet[4];
        int offset = 5;

        Debug.Log($"Personajes recibidos: {count}");

        for (int i = 0; i < count; i++)
        {
            if (packet.Length < offset + 13)
            {
                Debug.LogWarning("CharacterList incompleto.");
                break;
            }

            byte slot = packet[offset];
            string name = Encoding.ASCII.GetString(packet, offset + 1, 10).TrimEnd('\0');
            byte classId = packet[offset + 11];
            byte level = packet[offset + 12];

            Debug.Log($"Slot:{slot} Name:{name} Class:{classId} Level:{level}");

            offset += 13;
        }
    }

    private void HandleEnterWorld(byte[] packet)
    {
        if (packet.Length < 8)
        {
            Debug.LogWarning("EnterWorld packet inválido.");
            return;
        }

        currentMapId = packet[4];
        byte x = packet[5];
        byte y = packet[6];
        byte direction = packet[7];

        hasMoveTarget = false;

        Debug.Log($"ENTER WORLD OK -> Map:{currentMapId} Pos:{x},{y} Dir:{direction}");

        if (player != null)
            player.SetPosition(x, y);
    }

    private void HandleMoveResponse(byte[] packet)
    {
        if (packet.Length < 6)
            return;

        bool success = packet[3] == 0x00;
        byte x = packet[4];
        byte y = packet[5];

        Debug.Log($"MOVE RESPONSE -> Success:{success} Pos:{x},{y}");

        if (!success)
            return;

        if (x == lastRequestedX && y == lastRequestedY)
            hasMoveTarget = false;

        if (player != null)
            player.ConfirmMove(x, y);
    }

    private void HandleMonsterSpawn(byte[] packet)
    {
        if (packet.Length < 9)
        {
            Debug.LogWarning("MonsterSpawn packet inválido.");
            return;
        }

        byte monsterId = packet[4];
        byte monsterClass = packet[5];
        byte mapId = packet[6];
        byte x = packet[7];
        byte y = packet[8];

        if (monsterManager != null)
            monsterManager.SpawnMonster(monsterId, monsterClass, mapId, x, y);
    }

    private void HandleMonsterDeath(byte[] packet)
    {
        if (packet.Length < 5)
            return;

        byte monsterId = packet[4];

        if (monsterManager != null)
            monsterManager.DespawnMonster(monsterId);
    }

    private void HandleAutoCombatResponse(byte[] packet)
    {
        if (packet.Length < 5)
            return;

        isAutoCombatEnabled = packet[4] == 1;
        Debug.Log(isAutoCombatEnabled ? "AUTO COMBAT ACTIVADO" : "AUTO COMBAT DESACTIVADO");
    }

    private void QueueDisconnect()
    {
        mainThreadActions.Enqueue(Disconnect);
    }

    private void Disconnect()
    {
        if (!isConnected)
            return;

        isConnected = false;
        cancellationTokenSource?.Cancel();

        stream?.Close();
        client?.Close();

        stream = null;
        client = null;
        cancellationTokenSource = null;

        Debug.Log("Desconectado del GameServer.");
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
