using System.Net.Sockets;

internal static class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 55901;

    private const byte GoblinId = 1;
    private const byte TargetX = 141;
    private const byte TargetY = 138;

    private static readonly GameState State = new();

    private static async Task Main()
    {
        Console.WriteLine("=== MU TEST CLIENT PROFESSIONAL ===");

        using TcpClient client = new();
        await client.ConnectAsync(Host, Port);

        Console.WriteLine($"Conectado al GameServer {Host}:{Port}");

        using NetworkStream stream = client.GetStream();

        using CancellationTokenSource cts = new();

        Task readTask = Task.Run(() => ReadLoopAsync(stream, cts.Token));

        try
        {
            await RunScenarioAsync(stream, cts.Token);
        }
        finally
        {
            cts.Cancel();
        }

        Console.WriteLine();
        Console.WriteLine("Prueba finalizada. Presiona ENTER para salir...");
        Console.ReadLine();
    }

    private static async Task RunScenarioAsync(NetworkStream stream, CancellationToken token)
    {
        await LoginAsync(stream, token);
        await RequestCharacterListAsync(stream, token);
        await EnterWorldAsync(stream, token);

        await MoveToAsync(stream, TargetX, TargetY, token);

        byte droppedItemId = await KillMonsterAndWaitDropAsync(stream, GoblinId, token);

        await PickItemAsync(stream, droppedItemId, token);

        await MoveInventoryItemAsync(stream, fromSlot: 0, toSlot: 8, token);

        Console.WriteLine();
        Console.WriteLine("✅ SCENARIO OK: Login → World → Move → Kill → Drop → Pick → Inventory Move");
    }

    private static async Task LoginAsync(NetworkStream stream, CancellationToken token)
    {
        Console.WriteLine("\n--- LOGIN ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0x00, 0x01, 0x02 }, token);

        await WaitUntilAsync(() => State.LoginOk, "login", token);
    }

    private static async Task RequestCharacterListAsync(NetworkStream stream, CancellationToken token)
    {
        Console.WriteLine("\n--- CHARACTER LIST ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF3, 0x00, 0x00 }, token);

        await WaitUntilAsync(() => State.CharacterListReceived, "character list", token);
    }

    private static async Task EnterWorldAsync(NetworkStream stream, CancellationToken token)
    {
        Console.WriteLine("\n--- ENTER WORLD ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF3, 0x03, 0x00 }, token);

        await WaitUntilAsync(() => State.InWorld, "enter world", token);
    }

    private static async Task MoveToAsync(NetworkStream stream, byte x, byte y, CancellationToken token)
    {
        Console.WriteLine($"\n--- MOVE TO {x},{y} ---");

        State.TargetX = x;
        State.TargetY = y;
        State.ReachedTarget = false;

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x06, 0xD4, 0x01, x, y }, token);

        await WaitUntilAsync(() => State.ReachedTarget, $"movement to {x},{y}", token);
    }

    private static async Task<byte> KillMonsterAndWaitDropAsync(
        NetworkStream stream,
        byte monsterId,
        CancellationToken token)
    {
        Console.WriteLine($"\n--- KILL MONSTER {monsterId} ---");

        State.MonsterKilled = false;
        State.LastDroppedItemId = null;

        while (!State.MonsterKilled)
        {
            await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, monsterId }, token);

            await Task.Delay(900, token);
        }

        Console.WriteLine("\n--- WAITING DROP ---");

        await WaitUntilAsync(() => State.LastDroppedItemId.HasValue, "item drop", token);

        return State.LastDroppedItemId!.Value;
    }

    private static async Task PickItemAsync(NetworkStream stream, byte itemId, CancellationToken token)
    {
        Console.WriteLine($"\n--- PICK ITEM {itemId} ---");

        State.LastPickedItemOk = false;

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF4, 0x06, itemId }, token);

        await WaitUntilAsync(() => State.LastPickedItemOk, $"pick item {itemId}", token);
    }

    private static async Task MoveInventoryItemAsync(
        NetworkStream stream,
        byte fromSlot,
        byte toSlot,
        CancellationToken token)
    {
        Console.WriteLine($"\n--- MOVE INVENTORY SLOT {fromSlot} -> {toSlot} ---");

        State.InventoryMoveOk = false;

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x06, 0xF3, 0x31, fromSlot, toSlot }, token);

        await WaitUntilAsync(() => State.InventoryMoveOk, "inventory move", token);
    }

    private static async Task ReadLoopAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] buffer = new byte[4096];

        while (!token.IsCancellationRequested)
        {
            int bytesRead;

            try
            {
                bytesRead = await stream.ReadAsync(buffer, token);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
                break;

            int offset = 0;

            while (offset < bytesRead)
            {
                if (bytesRead - offset < 2)
                    break;

                byte packetLength = buffer[offset + 1];

                if (packetLength <= 0 || offset + packetLength > bytesRead)
                    break;

                byte[] packet = new byte[packetLength];
                Array.Copy(buffer, offset, packet, 0, packetLength);

                ParsePacket(packet);

                offset += packetLength;
            }
        }
    }

    private static void ParsePacket(byte[] packet)
    {
        if (packet.Length < 3)
            return;

        byte code = packet[2];
        byte subCode = packet.Length > 3 ? packet[3] : (byte)0;

        Console.WriteLine();
        Console.WriteLine($"📥 {BitConverter.ToString(packet)}");

        switch (code)
        {
            case 0xF1:
                HandleLogin(packet, subCode);
                break;

            case 0xF3:
                HandleCharacterAndInventory(packet, subCode);
                break;

            case 0xF4:
                HandleWorldPacket(packet, subCode);
                break;

            case 0xD4:
                HandleMove(packet);
                break;

            case 0xD7:
                HandleAttack(packet, subCode);
                break;

            default:
                Console.WriteLine($"⚠ Código desconocido: {code:X2}");
                break;
        }
    }

    private static void HandleLogin(byte[] packet, byte subCode)
    {
        if (subCode == 0x01)
        {
            State.LoginOk = true;
            Console.WriteLine("✔ Login OK");
            return;
        }

        Console.WriteLine($"⚠ Login desconocido: {subCode:X2}");
    }

    private static void HandleCharacterAndInventory(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x00:
                State.CharacterListReceived = true;
                Console.WriteLine($"✔ Character List -> Count:{packet[4]}");
                break;

            case 0x03:
                State.InWorld = true;
                State.PlayerMap = packet[4];
                State.PlayerX = packet[5];
                State.PlayerY = packet[6];

                Console.WriteLine($"✔ Enter World -> Map:{State.PlayerMap} Pos:{State.PlayerX},{State.PlayerY}");
                break;

            case 0x20:
                Console.WriteLine(
                    $"📊 Stats -> Class:{packet[4]} STR:{packet[5]} AGI:{packet[6]} VIT:{packet[7]} ENE:{packet[8]} LEA:{packet[9]} LIFE:{packet[10]} MANA:{packet[11]} LVL:{packet[12]} MAP:{packet[13]} POS:{packet[14]},{packet[15]}");
                break;

            case 0x21:
                {
                    ushort totalExp = (ushort)(packet[6] | (packet[7] << 8));
                    Console.WriteLine($"⭐ EXP -> +{packet[5]} Total:{totalExp} Level:{packet[4]}");
                    break;
                }

            case 0x22:
                Console.WriteLine($"🎉 LEVEL UP -> Level:{packet[4]}");
                break;

            case 0x23:
                Console.WriteLine("☠ PLAYER DEAD");
                break;

            case 0x24:
                Console.WriteLine($"✨ PLAYER RESPAWN -> Map:{packet[4]} Pos:{packet[5]},{packet[6]} HP:{packet[7]}");
                break;

            case 0x30:
                {
                    byte usedSlots = packet[4];
                    byte slot = packet[5];
                    byte item = packet[6];
                    byte width = packet[7];
                    byte height = packet[8];
                    byte itemWidth = packet[9];
                    byte itemHeight = packet[10];

                    int row = slot / width;
                    int column = slot % width;

                    Console.WriteLine(
                        $"🎒 Inventory Update -> UsedSlots:{usedSlots} Slot:{slot} Row:{row} Column:{column} Item:{item} Inventory:{width}x{height} ItemSize:{itemWidth}x{itemHeight}");
                    break;
                }

            case 0x31:
                {
                    byte fromSlot = packet[4];
                    byte toSlot = packet[5];
                    bool success = packet[6] == 1;
                    byte reason = packet[7];

                    State.InventoryMoveOk = success;

                    Console.WriteLine($"📦 Inventory Move -> From:{fromSlot} To:{toSlot} Success:{success} Reason:{reason}");
                    break;
                }

            default:
                Console.WriteLine($"⚠ F3 desconocido: {subCode:X2}");
                break;
        }
    }

    private static void HandleWorldPacket(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x01:
                Console.WriteLine($"👹 Monster Spawn -> Id:{packet[4]} Class:{packet[5]} Map:{packet[6]} Pos:{packet[7]},{packet[8]}");
                break;

            case 0x02:
                Console.WriteLine($"💀 Monster Death -> Id:{packet[4]}");
                break;

            case 0x03:
                Console.WriteLine(
                    $"💥 Monster Hit -> MonsterId:{packet[4]} Damage:{packet[5]} PlayerHP:{packet[6]} Dead:{(packet[7] == 1 ? "YES" : "NO")}");
                break;

            case 0x04:
                Console.WriteLine($"🧟 Monster Move -> Id:{packet[4]} Pos:{packet[5]},{packet[6]}");
                break;

            case 0x05:
                {
                    byte itemId = packet[4];
                    byte itemType = packet[5];
                    byte map = packet[6];
                    byte x = packet[7];
                    byte y = packet[8];

                    State.LastDroppedItemId = itemId;

                    Console.WriteLine($"💰 Item Drop -> ItemId:{itemId} Type:{itemType} Map:{map} Pos:{x},{y}");
                    break;
                }

            case 0x06:
                {
                    byte itemId = packet[4];
                    bool success = packet[5] == 1;
                    byte reason = packet[6];

                    State.LastPickedItemOk = success;

                    Console.WriteLine($"🎒 Pick Item -> ItemId:{itemId} Success:{success} Reason:{reason}");
                    break;
                }

            default:
                Console.WriteLine($"⚠ F4 desconocido: {subCode:X2}");
                break;
        }
    }

    private static void HandleMove(byte[] packet)
    {
        if (packet.Length < 6)
        {
            Console.WriteLine("⚠ Move inválido");
            return;
        }

        bool success = packet[3] == 0x01;
        byte x = packet[4];
        byte y = packet[5];

        if (success)
        {
            State.PlayerX = x;
            State.PlayerY = y;

            if (State.TargetX == x && State.TargetY == y)
                State.ReachedTarget = true;

            Console.WriteLine($"✔ Move OK -> {x},{y}");
        }
        else
        {
            Console.WriteLine($"❌ Move FAIL -> {x},{y}");
        }
    }

    private static void HandleAttack(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x00:
                {
                    byte monsterId = packet[4];
                    byte damage = packet[5];
                    byte remainingHp = packet[6];
                    bool killed = packet[7] == 1;

                    if (killed)
                        State.MonsterKilled = true;

                    Console.WriteLine(
                        $"⚔ Attack Monster -> Id:{monsterId} Damage:{damage} RemainingHp:{remainingHp} Killed:{(killed ? "YES" : "NO")}");
                    break;
                }

            case 0x02:
                {
                    byte reasonCode = packet[4];
                    byte monsterId = packet[5];

                    string reason = reasonCode switch
                    {
                        0x01 => "COOLDOWN",
                        0x02 => "OUT_OF_RANGE",
                        0x03 => "PLAYER_DEAD",
                        0x04 => "MONSTER_NOT_FOUND",
                        0x05 => "MONSTER_DEAD",
                        0x06 => "NO_PLAYER",
                        _ => $"UNKNOWN_{reasonCode:X2}"
                    };

                    Console.WriteLine($"❌ Attack Fail -> Monster:{monsterId} Reason:{reason}");
                    break;
                }

            default:
                Console.WriteLine($"⚠ Attack desconocido: {subCode:X2}");
                break;
        }
    }

    private static async Task SendPacketAsync(NetworkStream stream, byte[] packet, CancellationToken token)
    {
        Console.WriteLine($"📤 {BitConverter.ToString(packet)}");
        await stream.WriteAsync(packet, token);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        string operationName,
        CancellationToken token,
        int timeoutMs = 10000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Timeout esperando: {operationName}");

            await Task.Delay(100, token);
        }
    }

    private sealed class GameState
    {
        public bool LoginOk { get; set; }
        public bool CharacterListReceived { get; set; }
        public bool InWorld { get; set; }

        public byte PlayerMap { get; set; }
        public byte PlayerX { get; set; }
        public byte PlayerY { get; set; }

        public byte TargetX { get; set; }
        public byte TargetY { get; set; }
        public bool ReachedTarget { get; set; }

        public bool MonsterKilled { get; set; }
        public byte? LastDroppedItemId { get; set; }

        public bool LastPickedItemOk { get; set; }
        public bool InventoryMoveOk { get; set; }
    }
}