using System.Net.Sockets;
using System.Text;

internal static class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 55901;

    private static async Task Main()
    {
        Console.WriteLine("=== CLIENTE DE PRUEBA MU PEGASO ===");

        using var client = new TcpClient();

        await client.ConnectAsync(Host, Port);
        Console.WriteLine($"Conectado al GameServer {Host}:{Port}");

        using var stream = client.GetStream();

        _ = Task.Run(() => ReadLoopAsync(stream));

        await Task.Delay(200);
        await RunScenarioAsync(stream);

        Console.WriteLine();
        Console.WriteLine("Presiona ENTER para salir...");
        Console.ReadLine();
    }

    private static async Task RunScenarioAsync(NetworkStream stream)
    {
        Console.WriteLine("\n--- LOGIN ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0x00, 0x01, 0x02 });

        await Task.Delay(500);

        Console.WriteLine("\n--- LISTA DE PERSONAJES ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF3, 0x00, 0x00 });

        await Task.Delay(500);

        Console.WriteLine("\n--- ENTRAR AL MUNDO ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF3, 0x03, 0x00 });

        await Task.Delay(500);

        Console.WriteLine("\n--- MOVER PERSONAJE CERCA DEL GOBLIN ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x06, 0xD4, 0x01, 141, 138 });

        await Task.Delay(300);

        Console.WriteLine("\n--- ATACAR MONSTER ID 1 ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(900);

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(900);

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(200);

        Console.WriteLine("\n--- PICK ITEM ID 1 ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF4, 0x06, 0x01 });
        await Task.Delay(500);

        Console.WriteLine("\n--- ESPERAR RESPAWN MONSTER ---");
        await Task.Delay(6000);

        Console.WriteLine("\n--- ATACAR MONSTER ID 1 SEGUNDA VEZ ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(900);

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(900);

        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xD7, 0x01, 0x01 });
        await Task.Delay(500);

        Console.WriteLine("\n--- PICK ITEM ID 2 ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x05, 0xF4, 0x06, 0x02 });
        await Task.Delay(500);

        Console.WriteLine("\n--- MOVE INVENTORY ITEM SLOT 0 -> SLOT 8 ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x06, 0xF3, 0x31, 0x00, 0x08 });
        await Task.Delay(500);

        Console.WriteLine("\n--- MOVER MIENTRAS ESTÁ MUERTO ---");
        await SendPacketAsync(stream, new byte[] { 0xC1, 0x06, 0xD4, 0x01, 140, 130 });

        await Task.Delay(9000);
    }

    private static async Task ReadLoopAsync(NetworkStream stream)
    {
        var buffer = new byte[1024];

        while (true)
        {
            int bytesRead;

            try
            {
                bytesRead = await stream.ReadAsync(buffer);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
            {
                break;
            }

            int offset = 0;

            while (offset < bytesRead)
            {
                if (bytesRead - offset < 2)
                {
                    break;
                }

                byte packetLength = buffer[offset + 1];

                if (packetLength <= 0 || offset + packetLength > bytesRead)
                {
                    break;
                }

                var packet = new byte[packetLength];
                Array.Copy(buffer, offset, packet, 0, packetLength);

                ParsePacket(packet);

                offset += packetLength;
            }
        }
    }

    private static void ParsePacket(byte[] packet)
    {
        if (packet.Length < 3)
        {
            return;
        }

        byte code = packet[2];
        byte subCode = packet.Length > 3 ? packet[3] : (byte)0;

        Console.WriteLine();
        Console.WriteLine($"📥 Packet recibido: {BitConverter.ToString(packet)}");

        switch (code)
        {
            case 0xF1:
                HandleF1(packet, subCode);
                break;

            case 0xF3:
                HandleF3(packet, subCode);
                break;

            case 0xF4:
                HandleF4(packet, subCode);
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

    private static void HandleF1(byte[] packet, byte subCode)
    {
        if (subCode == 0x01)
        {
            Console.WriteLine("✔ Login OK");
            return;
        }

        Console.WriteLine($"⚠ F1 desconocido: {subCode:X2}");
    }

    private static void HandleF3(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x00:
                Console.WriteLine($"✔ Lista personajes: {packet[4]}");
                break;

            case 0x01:
                Console.WriteLine("✔ Crear personaje");
                break;

            case 0x03:
                Console.WriteLine($"✔ Enter World -> Map:{packet[4]} Pos:{packet[5]},{packet[6]}");
                break;

            case 0x10:
                ParsePlayerSpawn(packet);
                break;

            case 0x11:
                ParsePlayerMove(packet);
                break;

            case 0x12:
                Console.WriteLine($"👋 Player Despawn -> Id:{packet[4]}");
                break;

            case 0x20:
                Console.WriteLine(
                    $"📊 Stats -> Class:{packet[4]} STR:{packet[5]} AGI:{packet[6]} VIT:{packet[7]} ENE:{packet[8]} LEA:{packet[9]} LIFE:{packet[10]} MANA:{packet[11]} LVL:{packet[12]} MAP:{packet[13]} POS:{packet[14]},{packet[15]}");
                break;

            case 0x21:
                {
                    ushort totalExp = (ushort)(packet[6] | (packet[7] << 8));
                    Console.WriteLine($"⭐ EXP gained -> +{packet[5]} TotalEXP:{totalExp} Level:{packet[4]}");
                    break;
                }

            case 0x22:
                Console.WriteLine($"🎉 LEVEL UP! Nuevo nivel: {packet[4]}");
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

                    Console.WriteLine(
                        $"📦 Inventory Move -> From:{fromSlot} To:{toSlot} Success:{success} Reason:{reason}");

                    break;
                }

            default:
                Console.WriteLine($"⚠ F3 desconocido: {subCode:X2}");
                break;
        }
    }

    private static void ParsePlayerSpawn(byte[] packet)
    {
        if (packet.Length < 8)
        {
            Console.WriteLine("⚠ Player Spawn inválido: paquete demasiado corto");
            return;
        }

        Console.WriteLine($"👤 Spawn jugador -> Id:{packet[7]} Map:{packet[4]} Pos:{packet[5]},{packet[6]}");
    }

    private static void ParsePlayerMove(byte[] packet)
    {
        if (packet.Length < 8)
        {
            Console.WriteLine("⚠ Player Move inválido: paquete demasiado corto");
            return;
        }

        byte playerId = packet[4];
        byte mapId = packet[5];
        byte x = packet[6];
        byte y = packet[7];

        Console.WriteLine($"🏃 Player Move -> Id:{playerId} Map:{mapId} Pos:{x},{y}");
    }

    private static void HandleF4(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x01:
                Console.WriteLine(
                    $"👹 Monster Spawn -> Id:{packet[4]} Class:{packet[5]} Map:{packet[6]} Pos:{packet[7]},{packet[8]}");
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

                    Console.WriteLine(
                        $"💰 Item Drop -> ItemId:{itemId} Type:{itemType} Map:{map} Pos:{x},{y}");

                    break;
                }

            case 0x06:
                {
                    byte itemId = packet[4];
                    bool success = packet[5] == 1;
                    byte reasonCode = packet[6];

                    Console.WriteLine(
                        $"🎒 Pick Item -> ItemId:{itemId} Success:{success} Reason:{reasonCode}");

                    break;
                }

            default:
                Console.WriteLine($"⚠ F4 desconocido: {subCode:X2}");
                break;
        }
    }

    private static void HandleMove(byte[] packet)
    {
        byte result = packet[3];

        if (result == 0x01)
        {
            Console.WriteLine($"✔ Move OK -> {packet[4]},{packet[5]}");
        }
        else
        {
            Console.WriteLine($"❌ Move FAIL -> {packet[4]},{packet[5]}");
        }
    }

    private static void HandleAttack(byte[] packet, byte subCode)
    {
        switch (subCode)
        {
            case 0x00:
                Console.WriteLine(
                    $"⚔ Attack Monster -> Id:{packet[4]} Damage:{packet[5]} RemainingHp:{packet[6]} Killed:{(packet[7] == 1 ? "YES" : "NO")}");
                break;

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

                    Console.WriteLine($"❌ ATTACK FAIL -> Monster:{monsterId} Reason:{reason}");
                    break;
                }

            default:
                Console.WriteLine($"⚠ Attack desconocido: {subCode:X2}");
                break;
        }
    }

    private static async Task SendPacketAsync(NetworkStream stream, byte[] packet)
    {
        Console.WriteLine($"📤 Enviado: {BitConverter.ToString(packet)}");
        await stream.WriteAsync(packet);
    }
}