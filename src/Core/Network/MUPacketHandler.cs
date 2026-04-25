using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
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
    private readonly MovementService _movementService;
    private readonly AutoCombatService _autoCombatService;

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
        _movementService = movementService;
        _autoCombatService = autoCombatService;
    }

    public async Task ProcessClientAsync(TcpClient client)
    {
        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var session = new ClientSession();

        _logger.LogInformation("Cliente MU conectado: {Endpoint}", endpoint);

        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var pipe = new Pipe();

                var fillTask = FillPipeAsync(stream, pipe.Writer);
                var readTask = ReadPipeAsync(pipe.Reader, stream, session);

                await Task.WhenAll(fillTask, readTask);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando cliente {Endpoint}", endpoint);
        }
        finally
        {
            if (session.PlayerId.HasValue)
            {
                var player = _worldManager.GetPlayer(session.PlayerId.Value);

                if (player is not null)
                {
                    var receivers = _worldManager.GetVisiblePlayers(player);
                    _broadcastService.BroadcastPlayerDespawn(player, receivers);
                }

                _worldManager.DisconnectPlayer(session.PlayerId.Value);
            }

            _logger.LogInformation("Cliente MU desconectado: {Endpoint}", endpoint);
        }
    }

    private static async Task FillPipeAsync(NetworkStream stream, PipeWriter writer)
    {
        while (true)
        {
            var memory = writer.GetMemory(1024);
            int bytesRead;

            try
            {
                bytesRead = await stream.ReadAsync(memory);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
            {
                break;
            }

            writer.Advance(bytesRead);

            var result = await writer.FlushAsync();
            if (result.IsCompleted)
            {
                break;
            }
        }

        await writer.CompleteAsync();
    }

    private async Task ReadPipeAsync(PipeReader reader, NetworkStream stream, ClientSession session)
    {
        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            while (TryParsePacket(ref buffer, out var packet))
            {
                HandlePacket(packet, stream, session);
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
    }

    private static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out byte[] packet)
    {
        packet = Array.Empty<byte>();

        if (buffer.Length < 2)
        {
            return false;
        }

        var seqReader = new SequenceReader<byte>(buffer);

        if (!seqReader.TryRead(out var header))
        {
            return false;
        }

        if (header != 0xC1 && header != 0xC2)
        {
            buffer = buffer.Slice(1);
            return false;
        }

        if (!seqReader.TryRead(out var length))
        {
            return false;
        }

        if (length < 3 || buffer.Length < length)
        {
            return false;
        }

        packet = buffer.Slice(0, length).ToArray();
        buffer = buffer.Slice(length);
        return true;
    }

    private void HandlePacket(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (packet.Length < 3)
        {
            return;
        }

        var code = packet[2];
        var subCode = packet.Length > 3 ? packet[3] : (byte)0;

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
                HandleMovementPacket(subCode, packet, stream, session);
                break;

            case 0xD7:
                HandleAttackPacket(subCode, stream, session, packet);
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

        _logger.LogInformation("Login aceptado para cuenta {Account}", session.AccountName);

        SendLoginResponse(stream);

        _logger.LogInformation("LOGIN RESPONSE enviado");
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

        var player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null || player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x03);
            return;
        }

        var character = session.SelectedCharacter;

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
        var definition = GetItemDefinition(itemType);

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
            "Inventory move => From:{FromSlot} To:{ToSlot} ItemType:{ItemType} Size:{Width}x{Height}",
            fromSlot,
            toSlot,
            itemType,
            definition.Width,
            definition.Height);
    }

    private static void ClearItemFromInventory(Character character, int startSlot, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + (startColumn + column);

                if (slot >= 0 && slot < character.Inventory.Length)
                {
                    character.Inventory[slot] = 0;
                }
            }
        }
    }

    private void HandleCharacterList(NetworkStream stream, ClientSession session)
    {
        var characters = _characterService.GetCharacters(session.AccountName);
        SendCharacterList(stream, characters);
    }

    private void HandleCharacterCreate(byte[] packet, NetworkStream stream, ClientSession session)
    {
        var classId = packet.Length > 4 ? packet[4] : (byte)0;

        var existing = _characterService.GetCharacters(session.AccountName);
        var generatedName = $"PJ{existing.Count + 1}";

        var character = _characterService.CreateCharacter(session.AccountName, generatedName, classId);
        SendCharacterCreateResult(stream, character is not null);
    }

    private void HandleEnterWorld(byte[] packet, NetworkStream stream, ClientSession session)
    {
        var slot = packet.Length > 4 ? packet[4] : (byte)0;

        var character = _characterService.GetCharacterBySlot(session.AccountName, slot);
        if (character is null)
        {
            _logger.LogWarning(
                "No existe personaje en slot {Slot} para cuenta {Account}",
                slot,
                session.AccountName);
            return;
        }

        session.SelectedCharacter = character;

        var player = _worldManager.EnterWorld(session.AccountName, character);
        player.Stream = stream;
        session.PlayerId = player.PlayerId;

        SendEnterWorldResponse(stream, player);
        SendCharacterStatsPacket(stream, character);

        var monsters = _monsterManager.GetMonstersInMap(player.CurrentMapId);
        var visibleMonsters = _worldManager.GetVisibleMonsters(player, monsters);

        foreach (var monster in visibleMonsters)
        {
            SendMonsterSpawnPacket(stream, monster);
        }

        var others = _worldManager.GetVisiblePlayers(player);
        foreach (var other in others)
        {
            if (other.Stream is null)
            {
                continue;
            }

            SendSpawnPacket(stream, other);
            SendSpawnPacket(other.Stream, player);
        }
    }

    private void HandleMovementPacket(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
        {
            _logger.LogWarning("MoveRequest ignorado: no hay player activo.");
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        if (session.SelectedCharacter is null)
        {
            _logger.LogWarning("MoveRequest ignorado: no hay personaje activo.");
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        if (packet.Length < 7)
        {
            _logger.LogWarning("MoveRequest inválido: paquete demasiado corto.");
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        byte mapId = packet[4];
        byte targetX = packet[5];
        byte targetY = packet[6];

        if (subCode != 0x10)
        {
            _logger.LogDebug("SubCode D4 no manejado: {SubCode:X2}", subCode);
            return;
        }

        _movementService.SetMoveTarget(
            session.PlayerId.Value,
            mapId,
            targetX,
            targetY);

        _logger.LogInformation(
            "MoveRequest aceptado Player:{PlayerId} Map:{Map} Target:{X},{Y}",
            session.PlayerId.Value,
            mapId,
            targetX,
            targetY);
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

        var player = _worldManager.GetPlayer(session.PlayerId.Value);
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

        var item = _worldManager.GetItem(itemId);
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
            "Item picked => Player:{PlayerId} ItemId:{ItemId} Type:{ItemType} Slot:{Slot}",
            player.PlayerId,
            item.ItemId,
            item.ItemType,
            slot);
    }

    private void HandleAttackPacket(byte subCode, NetworkStream stream, ClientSession session, byte[] packet)
    {
        if (subCode == 0x10)
        {
            HandleAutoCombatPacket(packet, stream, session);
            return;
        }

        if (subCode != 0x01)
        {
            _logger.LogDebug("SubCode D7 no manejado: {SubCode:X2}", subCode);
            return;
        }

        if (session.SelectedCharacter is null)
        {
            _logger.LogWarning("Ataque ignorado: no hay personaje seleccionado.");
            return;
        }

        if (session.SelectedCharacter.CurrentLife == 0)
        {
            _logger.LogWarning("Ataque ignorado: el jugador está muerto.");
            SendAttackFailedPacket(stream, 0, 0x03);
            return;
        }

        if (!session.PlayerId.HasValue)
        {
            _logger.LogWarning("Ataque ignorado: no hay player activo.");
            SendAttackFailedPacket(stream, 0, 0x06);
            return;
        }

        if (packet.Length < 5)
        {
            _logger.LogWarning("Ataque ignorado: falta MonsterId.");
            SendAttackFailedPacket(stream, 0, 0x04);
            return;
        }

        int monsterId = packet[4];

        var monster = _monsterManager.GetMonster(monsterId);
        if (monster is null)
        {
            _logger.LogWarning("Ataque ignorado: monster {MonsterId} no existe.", monsterId);
            SendAttackFailedPacket(stream, (byte)monsterId, 0x04);
            return;
        }

        if (!monster.IsAlive)
        {
            _logger.LogWarning("Ataque ignorado: monster {MonsterId} está muerto.", monsterId);
            SendAttackFailedPacket(stream, (byte)monsterId, 0x05);
            return;
        }

        var player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            _logger.LogWarning("Ataque ignorado: no se encontró el player activo.");
            return;
        }

        if (player.IsDead)
        {
            _logger.LogWarning("Ataque ignorado: el jugador está muerto.");
            SendAttackFailedPacket(stream, (byte)monsterId, 0x03);
            return;
        }

        if ((DateTime.UtcNow - player.LastAttackTimeUtc).TotalMilliseconds < 800)
        {
            _logger.LogWarning("Ataque ignorado: cooldown activo.");
            SendAttackFailedPacket(stream, (byte)monsterId, 0x01);
            return;
        }

        int dx = Math.Abs(player.X - monster.X);
        int dy = Math.Abs(player.Y - monster.Y);

        if (dx > 3 || dy > 3)
        {
            _logger.LogWarning(
                "Ataque ignorado: fuera de rango. PlayerPos={PlayerX},{PlayerY} MonsterPos={MonsterX},{MonsterY}",
                player.X,
                player.Y,
                monster.X,
                monster.Y);

            SendAttackFailedPacket(stream, (byte)monsterId, 0x02);
            return;
        }

        player.LastAttackTimeUtc = DateTime.UtcNow;

        var (damage, remainingHp, killed) =
            _monsterManager.AttackMonster(monsterId, session.SelectedCharacter);

        SendMonsterAttackResponse(stream, (byte)monsterId, damage, remainingHp, killed);

        if (!killed)
        {
            var (monsterDamage, playerRemainingHp, playerDead) =
                _monsterManager.CounterAttackPlayer(monsterId, session.SelectedCharacter);

            SendMonsterHitPacket(stream, (byte)monsterId, monsterDamage, playerRemainingHp, playerDead);

            if (playerDead)
            {
                SendPlayerDeathPacket(stream);

                var deadPlayer = _worldManager.GetPlayer(session.PlayerId!.Value);
                if (deadPlayer is not null)
                {
                    var receivers = _worldManager.GetVisiblePlayers(deadPlayer);
                    _broadcastService.BroadcastPlayerDeath(deadPlayer, receivers);
                }

                _ = SchedulePlayerRespawnAsync(stream, session, session.SelectedCharacter);

                return;
            }
        }

        if (killed)
        {
            uint gainedExp = _monsterManager.GetMonsterExperience(monsterId);

            session.SelectedCharacter.Experience += gainedExp;
            _characterService.UpdateCharacterExperience(
                session.SelectedCharacter.Id,
                session.SelectedCharacter.Experience);

            SendExperiencePacket(stream, session.SelectedCharacter, gainedExp);

            if (_characterService.TryLevelUp(session.SelectedCharacter))
            {
                SendLevelUpPacket(stream, session.SelectedCharacter);
            }

            SendMonsterDeathPacket(stream, (byte)monsterId);

            var deadMonster = _monsterManager.GetMonster(monsterId);
            if (deadMonster is not null)
            {
                var item = _worldManager.SpawnItem(deadMonster.MapId, deadMonster.X, deadMonster.Y);
                var receivers = _worldManager.GetPlayersNearItem(item);

                foreach (var receiver in receivers)
                {
                    if (receiver.Stream is null)
                    {
                        continue;
                    }

                    SendItemDropPacket(receiver.Stream, item);
                }
            }

            _ = ScheduleMonsterRespawnAsync((byte)monsterId, stream);
        }
    }

    private void HandleAutoCombatPacket(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
        {
            _logger.LogWarning("AutoCombat ignorado: no hay player activo.");
            return;
        }

        if (packet.Length < 5)
        {
            _logger.LogWarning("AutoCombat packet inválido.");
            return;
        }

        bool enabled = packet[4] == 0x01;

        if (enabled)
        {
            _autoCombatService.EnableAutoCombat(session.PlayerId.Value);
        }
        else
        {
            _autoCombatService.DisableAutoCombat(session.PlayerId.Value);
        }

        byte[] response =
        {
        0xC1, 0x05,
        0xD7, 0x10,
        (byte)(enabled ? 1 : 0)
    };

        stream.Write(response, 0, response.Length);

        _logger.LogInformation(
            "AutoCombat {State} enviado a Player:{PlayerId}",
            enabled ? "ON" : "OFF",
            session.PlayerId.Value
        );
    }

    private async Task ScheduleMonsterRespawnAsync(byte monsterId, NetworkStream stream)
    {
        var monster = await _monsterManager.RespawnMonsterAsync(monsterId);

        if (monster is null)
        {
            return;
        }

        if (!monster.IsAlive)
        {
            _logger.LogWarning("Ataque ignorado: monster {MonsterId} está muerto.", monsterId);
            SendAttackFailedPacket(stream, (byte)monsterId, 0x05);
            return;
        }

        SendMonsterSpawnPacket(stream, monster);
    }

    private async Task SchedulePlayerRespawnAsync(NetworkStream stream, ClientSession session, Character character)
    {
        await Task.Delay(5000);

        if (character.CurrentLife > 0)
        {
            return;
        }

        character.MapId = 0;
        character.X = 125;
        character.Y = 125;
        character.CurrentLife = character.MaxLife;

        SendPlayerRespawnPacket(stream, character);

        if (session.PlayerId.HasValue)
        {
            var player = _worldManager.GetPlayer(session.PlayerId.Value);
            if (player is not null)
            {
                var receivers = _worldManager.GetVisiblePlayers(player);
                _broadcastService.BroadcastPlayerRespawn(player, receivers);
            }
        }
    }

    private void SendLoginResponse(NetworkStream stream)
    {
        var response = new byte[]
        {
            0xC1, 0x05,
            0xF1, 0x01, 0x01
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Login response: SUCCESS");
    }

    private void SendCharacterList(NetworkStream stream, IReadOnlyList<Character> characters)
    {
        _logger.LogInformation("Enviando {Count} personajes", characters.Count);

        var response = new List<byte>();

        response.Add(0xC1); // header
        response.Add(0x00); // length placeholder
        response.Add(0xF3); // code
        response.Add(0x00); // subcode

        response.Add((byte)characters.Count); // cantidad

        byte slot = 0;

        foreach (var character in characters)
        {
            response.Add(slot); // slot

            // nombre (10 bytes)
            var nameBytes = new byte[10];
            var rawName = System.Text.Encoding.ASCII.GetBytes(character.Name);
            Array.Copy(rawName, nameBytes, Math.Min(10, rawName.Length));
            response.AddRange(nameBytes);

            response.Add((byte)character.Class); // clase
            response.Add((byte)character.Level); // nivel

            slot++;
        }

        response[1] = (byte)response.Count; // set length

        var data = response.ToArray();

        stream.Write(data, 0, data.Length);

        _logger.LogInformation("<-- CharacterList enviada correctamente");
    }

    private void SendCharacterCreateResult(NetworkStream stream, bool success)
    {
        var response = new byte[]
        {
            0xC1, 0x06,
            0xF3, 0x01,
            (byte)(success ? 0x00 : 0x01),
            0x00
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Character create: {Result}", success ? "SUCCESS" : "FAILED");
    }

    private void SendCharacterDeleteResult(NetworkStream stream, bool success)
    {
        var response = new byte[]
        {
            0xC1, 0x05,
            0xF3, 0x02,
            (byte)(success ? 0x00 : 0x01)
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Character delete: {Result}", success ? "SUCCESS" : "FAILED");
    }

    private void SendEnterWorldResponse(NetworkStream stream, Player player)
    {
        var response = new byte[]
        {
            0xC1, 0x08,
            0xF3, 0x03,
            player.CurrentMapId,
            player.X,
            player.Y,
            player.Direction
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Enter world: Map={Map}, Pos={X},{Y}", player.CurrentMapId, player.X, player.Y);
    }

    private void SendMoveResponse(NetworkStream stream, byte x, byte y, bool success)
    {
        var response = new byte[]
        {
            0xC1, 0x06,
            0xD4,
            (byte)(success ? 0x00 : 0x01),
            x, y
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Move response: {Result} -> {X},{Y}", success ? "OK" : "FAIL", x, y);
    }

    private void SendCharacterStatsPacket(NetworkStream stream, Character character)
    {
        var response = new byte[]
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
        _logger.LogInformation(
            "<- Character stats: Class={Class}, STR={STR}, AGI={AGI}, VIT={VIT}, ENE={ENE}, LIFE={LIFE}, MANA={MANA}",
            character.Class,
            character.Strength,
            character.Agility,
            character.Vitality,
            character.Energy,
            character.Life,
            character.Mana);
    }

    private void SendSpawnPacket(NetworkStream stream, Player player)
    {
        var packet = new byte[]
        {
            0xC1, 0x08,
            0xF3, 0x10,
            player.CurrentMapId,
            player.X,
            player.Y,
            (byte)player.PlayerId
        };

        stream.Write(packet, 0, packet.Length);
        _logger.LogInformation("<- Spawn packet: Map={Map}, Pos={X},{Y}", player.CurrentMapId, player.X, player.Y);
    }

    private void SendMoveBroadcastPacket(NetworkStream stream, Player player)
    {
        var response = new byte[]
        {
            0xC1, 0x06,
            0xD4, 0x00,
            player.X,
            player.Y
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Move broadcast: Pos={X},{Y}", player.X, player.Y);
    }

    private void SendMonsterSpawnPacket(NetworkStream stream, Monster monster)
    {
        try
        {
            if (!stream.CanWrite)
            {
                return;
            }

            var response = new byte[]
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

            _logger.LogInformation(
                "<- Monster spawn: Id={MonsterId}, Class={Class}, Map={Map}, Pos={X},{Y}",
                monster.MonsterId,
                monster.MonsterClass,
                monster.MapId,
                monster.X,
                monster.Y);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar MonsterSpawn: cliente desconectado.");
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar MonsterSpawn: socket cerrado.");
        }
    }

    private void SendMonsterDeathPacket(NetworkStream stream, byte monsterId)
    {
        var response = new byte[]
        {
            0xC1, 0x05,
            0xF4, 0x02,
            monsterId
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Monster death: MonsterId={MonsterId}", monsterId);
    }

    private void SendMonsterAttackResponse(NetworkStream stream, byte monsterId, byte damage, byte remainingHp, bool killed)
    {
        var response = new byte[]
        {
            0xC1, 0x08,
            0xD7, 0x00,
            monsterId,
            damage,
            remainingHp,
            (byte)(killed ? 1 : 0)
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation(
            "<- Monster attack response: MonsterId={MonsterId}, Damage={Damage}, RemainingHp={RemainingHp}, Killed={Killed}",
            monsterId,
            damage,
            remainingHp,
            killed);
    }

    private void SendAttackFailedPacket(NetworkStream stream, byte monsterId, byte reasonCode)
    {
        var response = new byte[]
        {
        0xC1, 0x06,
        0xD7, 0x02,
        reasonCode,
        monsterId
        };

        stream.Write(response, 0, response.Length);

        _logger.LogInformation(
            "<- Attack failed: MonsterId={MonsterId}, Reason={ReasonCode}",
            monsterId,
            reasonCode);
    }

    private void SendMonsterHitPacket(NetworkStream stream, byte monsterId, byte damage, byte remainingHp, bool dead)
    {
        var response = new byte[]
        {
            0xC1, 0x08,
            0xF4, 0x03,
            monsterId,
            damage,
            remainingHp,
            (byte)(dead ? 1 : 0)
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation(
            "<- Monster hit player: MonsterId={MonsterId}, Damage={Damage}, PlayerHP={RemainingHp}, Dead={Dead}",
            monsterId,
            damage,
            remainingHp,
            dead);
    }

    private void SendExperiencePacket(NetworkStream stream, Character character, uint gainedExp)
    {
        ushort totalExp = (ushort)Math.Min(character.Experience, ushort.MaxValue);

        var response = new byte[]
        {
            0xC1, 0x09,
            0xF3, 0x21,
            (byte)character.Level,
            (byte)gainedExp,
            (byte)(totalExp & 0xFF),
            (byte)((totalExp >> 8) & 0xFF),
            0x00,
            0x00
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation(
            "<- EXP packet: Gained={GainedExp}, Total={TotalExp}, Level={Level}",
            gainedExp,
            character.Experience,
            character.Level);
    }

    private void SendLevelUpPacket(NetworkStream stream, Character character)
    {
        var response = new byte[]
        {
            0xC1, 0x07,
            0xF3, 0x22,
            (byte)character.Level,
            0x00,
            0x00
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Level Up: Level={Level}", character.Level);
    }

    private void SendPlayerDeathPacket(NetworkStream stream)
    {
        var response = new byte[]
        {
            0xC1, 0x05,
            0xF3, 0x23,
            0x01
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation("<- Player death packet enviado");
    }

    private void SendPlayerRespawnPacket(NetworkStream stream, Character character)
    {
        var response = new byte[]
        {
            0xC1, 0x08,
            0xF3, 0x24,
            character.MapId,
            character.X,
            character.Y,
            (byte)Math.Min((int)character.CurrentLife, 255)
        };

        stream.Write(response, 0, response.Length);
        _logger.LogInformation(
            "<- Player respawn packet: Map={Map}, Pos={X},{Y}, HP={HP}",
            character.MapId,
            character.X,
            character.Y,
            character.CurrentLife);
    }

    private void SendItemDropPacket(NetworkStream stream, WorldItem item)
    {
        var packet = new byte[]
        {
        0xC1,
        0x09,
        0xF4,
        0x05,
        (byte)item.ItemId,
        item.ItemType,
        item.MapId,
        item.X,
        item.Y
        };

        stream.Write(packet, 0, packet.Length);

        _logger.LogInformation(
            "<- Item drop: ItemId={ItemId} Type={ItemType} Map={Map} Pos={X},{Y}",
            item.ItemId,
            item.ItemType,
            item.MapId,
            item.X,
            item.Y);
    }

    private void SendPickItemResult(NetworkStream stream, byte itemId, bool success, byte reasonCode)
    {
        var packet = new byte[]
        {
        0xC1,
        0x07,
        0xF4,
        0x06,
        itemId,
        (byte)(success ? 1 : 0),
        reasonCode
        };

        stream.Write(packet, 0, packet.Length);

        _logger.LogInformation(
            "<- Pick item result: ItemId={ItemId} Success={Success} Reason={Reason}",
            itemId,
            success,
            reasonCode);
    }

    private static int AddItemToInventory(Character character, byte itemType)
    {
        EnsureInventorySize(character);

        var definition = GetItemDefinition(itemType);

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
        return ItemDefinitions.TryGetValue(itemType, out var definition)
            ? definition
            : ItemDefinitions[0];
    }

    private static void EnsureInventorySize(Character character)
    {
        if (character.Inventory.Length == InventorySlots)
        {
            return;
        }

        var newInventory = new byte[InventorySlots];

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
        {
            return false;
        }

        if (startRow + height > InventoryHeight)
        {
            return false;
        }

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + (startColumn + column);

                if (character.Inventory[slot] != 0)
                {
                    return false;
                }
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
                int slot = (startRow + row) * InventoryWidth + (startColumn + column);
                character.Inventory[slot] = storedValue;
            }
        }
    }

    private void SendInventoryUpdatePacket(NetworkStream stream, Character character, byte slot)
    {
        byte usedSlots = (byte)character.Inventory.Count(item => item != 0);
        byte itemValue = character.Inventory[slot];

        var definition = GetItemDefinition((byte)(itemValue - 1));

        var packet = new byte[]

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

        _logger.LogInformation(
            "<- Inventory update: UsedSlots={UsedSlots} Slot={Slot} Item={Item} Size={Width}x{Height}",
            usedSlots,
            slot,
            itemValue,
            InventoryWidth,
            InventoryHeight);
    }

    private void SendInventoryMoveResult(NetworkStream stream, byte fromSlot, byte toSlot, bool success, byte reasonCode)
    {
        var packet = new byte[]
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

        _logger.LogInformation(
            "<- Inventory move result: From={FromSlot} To={ToSlot} Success={Success} Reason={Reason}",
            fromSlot,
            toSlot,
            success,
            reasonCode);
    }
}