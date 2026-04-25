using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MUServer.Core.Models;
using MUServer.Core.Network.Handlers;
using MUServer.Core.Services;
using MUServer.Core.World;

namespace MUServer.Core.Network;

public sealed class MUPacketHandler
{
    private const string TestAccount = "test_account";

    private const int InventoryWidth = 8;
    private const int InventoryHeight = 20;
    private const int InventorySlots = InventoryWidth * InventoryHeight;

    private static readonly Dictionary<byte, ItemDefinition> ItemDefinitions = new()
    {
        [0] = new ItemDefinition { ItemType = 0, Name = "Potion", Width = 1, Height = 1 },
        [1] = new ItemDefinition { ItemType = 1, Name = "Sword", Width = 2, Height = 4 },
        [2] = new ItemDefinition { ItemType = 2, Name = "Armor", Width = 2, Height = 3 }
    };

    private readonly ILogger _logger;
    private readonly CharacterService _characterService;
    private readonly WorldManager _worldManager;
    private readonly MonsterManager _monsterManager;
    private readonly BroadcastService _broadcastService;

    private readonly MovementPacketHandler _movementHandler;
    private readonly CombatPacketHandler _combatHandler;

    public MUPacketHandler(
        ILogger logger,
        CharacterService characterService,
        BroadcastService broadcastService,
        MonsterManager monsterManager,
        WorldManager worldManager,
        MovementService movementService,
        AutoCombatService autoCombatService)
    {
        _logger = logger;
        _characterService = characterService;
        _broadcastService = broadcastService;
        _monsterManager = monsterManager;
        _worldManager = worldManager;

        _movementHandler = new MovementPacketHandler(
            movementService,
            worldManager,
            NullLogger<MovementPacketHandler>.Instance);

        _combatHandler = new CombatPacketHandler(
            characterService,
            broadcastService,
            monsterManager,
            worldManager,
            autoCombatService,
            NullLogger<CombatPacketHandler>.Instance);
    }

    public async Task ProcessClientAsync(TcpClient client)
    {
        string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        ClientSession session = new();

        _logger.LogInformation("Cliente MU conectado: {Endpoint}", endpoint);

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                Pipe pipe = new();

                Task fillTask = FillPipeAsync(stream, pipe.Writer);
                Task readTask = ReadPipeAsync(pipe.Reader, stream, session);

                await Task.WhenAll(fillTask, readTask);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando cliente {Endpoint}", endpoint);
        }
        finally
        {
            DisconnectSession(session);
            _logger.LogInformation("Cliente MU desconectado: {Endpoint}", endpoint);
        }
    }

    private void DisconnectSession(ClientSession session)
    {
        if (!session.PlayerId.HasValue)
            return;

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);

        if (player is not null)
        {
            var receivers = _worldManager.GetVisiblePlayers(player);
            _broadcastService.BroadcastPlayerDespawn(player, receivers);
        }

        _worldManager.DisconnectPlayer(session.PlayerId.Value);
    }

    private static async Task FillPipeAsync(NetworkStream stream, PipeWriter writer)
    {
        try
        {
            while (true)
            {
                Memory<byte> memory = writer.GetMemory(1024);

                int bytesRead = await stream.ReadAsync(memory);
                if (bytesRead == 0)
                    break;

                writer.Advance(bytesRead);

                FlushResult result = await writer.FlushAsync();
                if (result.IsCompleted)
                    break;
            }
        }
        catch
        {
            // Cliente desconectado o socket cerrado.
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private async Task ReadPipeAsync(PipeReader reader, NetworkStream stream, ClientSession session)
    {
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryParsePacket(ref buffer, out byte[] packet))
                {
                    HandlePacket(packet, stream, session);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out byte[] packet)
    {
        packet = Array.Empty<byte>();

        if (buffer.Length < 2)
            return false;

        SequenceReader<byte> reader = new(buffer);

        if (!reader.TryRead(out byte header))
            return false;

        if (header != 0xC1 && header != 0xC2)
        {
            buffer = buffer.Slice(1);
            return false;
        }

        if (!reader.TryRead(out byte length))
            return false;

        if (length < 3 || buffer.Length < length)
            return false;

        packet = buffer.Slice(0, length).ToArray();
        buffer = buffer.Slice(length);

        return true;
    }

    private void HandlePacket(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (packet.Length < 3)
            return;

        byte code = packet[2];
        byte subCode = packet.Length > 3 ? packet[3] : (byte)0;

        _logger.LogInformation(
            "Packet recibido Header={Header:X2} Length={Length} Code={Code:X2} SubCode={SubCode:X2}",
            packet[0],
            packet.Length,
            code,
            subCode);

        switch (code)
        {
            case 0x00:
                HandleLogin(stream, session);
                break;

            case 0xF3:
                HandleCharacterPacket(subCode, packet, stream, session);
                break;

            case 0xF4:
                HandleWorldItemPacket(subCode, packet, stream, session);
                break;

            case 0xD4:
                _movementHandler.Handle(subCode, packet, stream, session);
                break;

            case 0xD7:
                _combatHandler.Handle(subCode, packet, stream, session);
                break;

            default:
                _logger.LogDebug("Código no manejado: {Code:X2}", code);
                break;
        }
    }

    private void HandleLogin(NetworkStream stream, ClientSession session)
    {
        _logger.LogInformation("LOGIN REQUEST recibido");

        session.AccountName = TestAccount;
        session.IsAuthenticated = true;

        _characterService.EnsureSeedData(session.AccountName);

        SendLoginResponse(stream);

        _logger.LogInformation("Login aceptado para cuenta {Account}", session.AccountName);
    }

    private void HandleCharacterPacket(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.IsAuthenticated)
        {
            _logger.LogWarning("Intento de acceso a personajes sin login.");
            return;
        }

        switch (subCode)
        {
            case 0x00:
                HandleCharacterList(stream, session);
                break;

            case 0x01:
                HandleCharacterCreate(packet, stream, session);
                break;

            case 0x02:
                SendCharacterDeleteResult(stream, false);
                break;

            case 0x03:
                HandleEnterWorld(packet, stream, session);
                break;

            case 0x31:
                HandleMoveInventoryItem(packet, stream, session);
                break;

            default:
                _logger.LogDebug("SubCode F3 no manejado: {SubCode:X2}", subCode);
                break;
        }
    }

    private void HandleCharacterList(NetworkStream stream, ClientSession session)
    {
        IReadOnlyList<Character> characters = _characterService.GetCharacters(session.AccountName);
        SendCharacterList(stream, characters);
    }

    private void HandleCharacterCreate(byte[] packet, NetworkStream stream, ClientSession session)
    {
        byte classId = packet.Length > 4 ? packet[4] : (byte)0;

        IReadOnlyList<Character> existing = _characterService.GetCharacters(session.AccountName);
        string generatedName = $"PJ{existing.Count + 1}";

        Character? character = _characterService.CreateCharacter(
            session.AccountName,
            generatedName,
            classId);

        SendCharacterCreateResult(stream, character is not null);
    }

    private void HandleEnterWorld(byte[] packet, NetworkStream stream, ClientSession session)
    {
        byte slot = packet.Length > 4 ? packet[4] : (byte)0;

        Character? character = _characterService.GetCharacterBySlot(session.AccountName, slot);
        if (character is null)
        {
            _logger.LogWarning(
                "No existe personaje en slot {Slot} para cuenta {Account}",
                slot,
                session.AccountName);
            return;
        }

        session.SelectedCharacter = character;

        Player player = _worldManager.EnterWorld(session.AccountName, character);
        player.Stream = stream;
        session.PlayerId = player.PlayerId;

        SendEnterWorldResponse(stream, player);
        SendCharacterStatsPacket(stream, character);
        SendVisibleMonsters(stream, player);
        SendVisiblePlayers(stream, player);
    }

    private void SendVisibleMonsters(NetworkStream stream, Player player)
    {
        var monsters = _monsterManager.GetMonstersInMap(player.CurrentMapId);
        var visibleMonsters = _worldManager.GetVisibleMonsters(player, monsters);

        foreach (Monster monster in visibleMonsters)
        {
            SendMonsterSpawnPacket(stream, monster);
        }
    }

    private void SendVisiblePlayers(NetworkStream stream, Player player)
    {
        var visiblePlayers = _worldManager.GetVisiblePlayers(player);

        foreach (Player other in visiblePlayers)
        {
            if (other.Stream is null)
                continue;

            SendSpawnPacket(stream, other);
            SendSpawnPacket(other.Stream, player);
        }
    }

    private void HandleWorldItemPacket(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        switch (subCode)
        {
            case 0x06:
                HandlePickItem(packet, stream, session);
                break;

            default:
                _logger.LogDebug("SubCode F4 no manejado: {SubCode:X2}", subCode);
                break;
        }
    }

    private void HandlePickItem(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
        {
            SendPickItemResult(stream, 0, false, 0x06);
            return;
        }

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            SendPickItemResult(stream, 0, false, 0x06);
            return;
        }

        if (player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendPickItemResult(stream, 0, false, 0x03);
            return;
        }

        if (packet.Length < 5)
        {
            SendPickItemResult(stream, 0, false, 0x04);
            return;
        }

        int itemId = packet[4];

        WorldItem? item = _worldManager.GetItem(itemId);
        if (item is null)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x01);
            return;
        }

        if (item.MapId != player.CurrentMapId)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x02);
            return;
        }

        int dx = Math.Abs(player.X - item.X);
        int dy = Math.Abs(player.Y - item.Y);

        if (dx > 2 || dy > 2)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x05);
            return;
        }

        int slot = AddItemToInventory(player.Character, item.ItemType);
        if (slot < 0)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x07);
            return;
        }

        if (!_worldManager.RemoveItem(itemId))
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x01);
            return;
        }

        SendPickItemResult(stream, (byte)itemId, true, 0x00);
        SendInventoryUpdatePacket(stream, player.Character, (byte)slot);

        _logger.LogInformation(
            "Item picked Player:{PlayerId} ItemId:{ItemId} Type:{ItemType} Slot:{Slot}",
            player.PlayerId,
            item.ItemId,
            item.ItemType,
            slot);
    }

    private void HandleMoveInventoryItem(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue || session.SelectedCharacter is null)
        {
            SendInventoryMoveResult(stream, 0, 0, false, 0x06);
            return;
        }

        if (packet.Length < 6)
        {
            SendInventoryMoveResult(stream, 0, 0, false, 0x04);
            return;
        }

        byte fromSlot = packet[4];
        byte toSlot = packet[5];

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null || player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x03);
            return;
        }

        Character character = session.SelectedCharacter;

        EnsureInventorySize(character);

        if (fromSlot >= InventorySlots || toSlot >= InventorySlots)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x01);
            return;
        }

        byte itemValue = character.Inventory[fromSlot];

        if (itemValue == 0)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x02);
            return;
        }

        byte itemType = (byte)(itemValue - 1);
        ItemDefinition definition = GetItemDefinition(itemType);

        ClearItemFromInventory(character, fromSlot, definition.Width, definition.Height);

        if (!CanPlaceItem(character, toSlot, definition.Width, definition.Height))
        {
            PlaceItem(character, fromSlot, itemType, definition.Width, definition.Height);
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x05);
            return;
        }

        PlaceItem(character, toSlot, itemType, definition.Width, definition.Height);

        SendInventoryMoveResult(stream, fromSlot, toSlot, true, 0x00);
        SendInventoryUpdatePacket(stream, character, toSlot);

        _logger.LogInformation(
            "Inventory move From:{FromSlot} To:{ToSlot} ItemType:{ItemType} Size:{Width}x{Height}",
            fromSlot,
            toSlot,
            itemType,
            definition.Width,
            definition.Height);
    }

    private void SendLoginResponse(NetworkStream stream)
    {
        byte[] response =
        {
            0xC1, 0x05,
            0xF1, 0x01, 0x01
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendCharacterList(NetworkStream stream, IReadOnlyList<Character> characters)
    {
        List<byte> response = new()
        {
            0xC1,
            0x00,
            0xF3,
            0x00,
            (byte)characters.Count
        };

        byte slot = 0;

        foreach (Character character in characters)
        {
            response.Add(slot);

            byte[] nameBytes = new byte[10];
            byte[] rawName = System.Text.Encoding.ASCII.GetBytes(character.Name);
            Array.Copy(rawName, nameBytes, Math.Min(nameBytes.Length, rawName.Length));

            response.AddRange(nameBytes);
            response.Add((byte)character.Class);
            response.Add((byte)character.Level);

            slot++;
        }

        response[1] = (byte)response.Count;

        byte[] data = response.ToArray();
        stream.Write(data, 0, data.Length);
    }

    private void SendCharacterCreateResult(NetworkStream stream, bool success)
    {
        byte[] response =
        {
            0xC1, 0x06,
            0xF3, 0x01,
            (byte)(success ? 0x00 : 0x01),
            0x00
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendCharacterDeleteResult(NetworkStream stream, bool success)
    {
        byte[] response =
        {
            0xC1, 0x05,
            0xF3, 0x02,
            (byte)(success ? 0x00 : 0x01)
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendEnterWorldResponse(NetworkStream stream, Player player)
    {
        byte[] response =
        {
            0xC1, 0x08,
            0xF3, 0x03,
            player.CurrentMapId,
            player.X,
            player.Y,
            player.Direction
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendCharacterStatsPacket(NetworkStream stream, Character character)
    {
        byte[] response =
        {
            0xC1, 0x12,
            0xF3, 0x20,
            character.Class,
            (byte)character.Strength,
            (byte)character.Agility,
            (byte)character.Vitality,
            (byte)character.Energy,
            (byte)character.Leadership,
            (byte)character.Life,
            (byte)character.Mana,
            (byte)character.Level,
            (byte)character.MapId,
            character.X,
            character.Y,
            0x00,
            0x00
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendSpawnPacket(NetworkStream stream, Player player)
    {
        byte[] packet =
        {
            0xC1, 0x08,
            0xF3, 0x10,
            player.CurrentMapId,
            player.X,
            player.Y,
            (byte)player.PlayerId
        };

        stream.Write(packet, 0, packet.Length);
    }

    private void SendMonsterSpawnPacket(NetworkStream stream, Monster monster)
    {
        if (!stream.CanWrite)
            return;

        byte[] response =
        {
            0xC1, 0x09,
            0xF4, 0x01,
            (byte)monster.MonsterId,
            monster.MonsterClass,
            monster.MapId,
            monster.X,
            monster.Y
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendPickItemResult(NetworkStream stream, byte itemId, bool success, byte reasonCode)
    {
        byte[] packet =
        {
            0xC1, 0x07,
            0xF4, 0x06,
            itemId,
            (byte)(success ? 1 : 0),
            reasonCode
        };

        stream.Write(packet, 0, packet.Length);
    }

    private void SendInventoryUpdatePacket(NetworkStream stream, Character character, byte slot)
    {
        byte usedSlots = (byte)character.Inventory.Count(item => item != 0);
        byte itemValue = character.Inventory[slot];

        ItemDefinition definition = itemValue == 0
            ? GetItemDefinition(0)
            : GetItemDefinition((byte)(itemValue - 1));

        byte[] packet =
        {
            0xC1,
            0x0B,
            0xF3,
            0x30,
            usedSlots,
            slot,
            itemValue,
            InventoryWidth,
            InventoryHeight,
            definition.Width,
            definition.Height
        };

        stream.Write(packet, 0, packet.Length);
    }

    private void SendInventoryMoveResult(NetworkStream stream, byte fromSlot, byte toSlot, bool success, byte reasonCode)
    {
        byte[] packet =
        {
            0xC1,
            0x08,
            0xF3,
            0x31,
            fromSlot,
            toSlot,
            (byte)(success ? 1 : 0),
            reasonCode
        };

        stream.Write(packet, 0, packet.Length);
    }

    private static int AddItemToInventory(Character character, byte itemType)
    {
        EnsureInventorySize(character);

        ItemDefinition definition = GetItemDefinition(itemType);

        for (int slot = 0; slot < InventorySlots; slot++)
        {
            if (CanPlaceItem(character, slot, definition.Width, definition.Height))
            {
                PlaceItem(character, slot, itemType, definition.Width, definition.Height);
                return slot;
            }
        }

        return -1;
    }

    private static ItemDefinition GetItemDefinition(byte itemType)
    {
        return ItemDefinitions.TryGetValue(itemType, out ItemDefinition? definition)
            ? definition
            : ItemDefinitions[0];
    }

    private static void EnsureInventorySize(Character character)
    {
        if (character.Inventory.Length == InventorySlots)
            return;

        byte[] newInventory = new byte[InventorySlots];

        Array.Copy(
            character.Inventory,
            newInventory,
            Math.Min(character.Inventory.Length, newInventory.Length));

        character.Inventory = newInventory;
    }

    private static bool CanPlaceItem(Character character, int startSlot, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        if (startColumn + width > InventoryWidth)
            return false;

        if (startRow + height > InventoryHeight)
            return false;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;

                if (slot < 0 || slot >= character.Inventory.Length)
                    return false;

                if (character.Inventory[slot] != 0)
                    return false;
            }
        }

        return true;
    }

    private static void PlaceItem(Character character, int startSlot, byte itemType, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        byte storedValue = (byte)(itemType + 1);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;
                character.Inventory[slot] = storedValue;
            }
        }
    }

    private static void ClearItemFromInventory(Character character, int startSlot, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;

                if (slot >= 0 && slot < character.Inventory.Length)
                {
                    character.Inventory[slot] = 0;
                }
            }
        }
    }
}