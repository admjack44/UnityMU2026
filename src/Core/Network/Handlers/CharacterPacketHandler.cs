using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
using MUServer.Core.Services;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class CharacterPacketHandler
{
    private readonly CharacterService _characterService;
    private readonly WorldManager _worldManager;
    private readonly MonsterManager _monsterManager;
    private readonly BroadcastService _broadcastService;
    private readonly ILogger<CharacterPacketHandler> _logger;

    private const string TestAccount = "test_account";

    public CharacterPacketHandler(
        CharacterService characterService,
        WorldManager worldManager,
        MonsterManager monsterManager,
        BroadcastService broadcastService,
        ILogger<CharacterPacketHandler> logger)
    {
        _characterService = characterService;
        _worldManager = worldManager;
        _monsterManager = monsterManager;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public void HandleLogin(NetworkStream stream, ClientSession session)
    {
        session.AccountName = TestAccount;
        session.IsAuthenticated = true;

        _characterService.EnsureSeedData(session.AccountName);

        byte[] response =
        {
            0xC1, 0x05,
            0xF1, 0x01, 0x01
        };

        stream.Write(response, 0, response.Length);

        _logger.LogInformation("Login OK Account:{Account}", session.AccountName);
    }

    public void Handle(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.IsAuthenticated)
            return;

        switch (subCode)
        {
            case 0x00:
                HandleCharacterList(stream, session);
                break;

            case 0x01:
                HandleCharacterCreate(packet, stream, session);
                break;

            case 0x03:
                HandleEnterWorld(packet, stream, session);
                break;

            default:
                _logger.LogDebug("F3 no manejado: {SubCode:X2}", subCode);
                break;
        }
    }

    private void HandleCharacterList(NetworkStream stream, ClientSession session)
    {
        var characters = _characterService.GetCharacters(session.AccountName);

        List<byte> response = new()
        {
            0xC1,
            0x00,
            0xF3,
            0x00,
            (byte)characters.Count
        };

        byte slot = 0;

        foreach (var character in characters)
        {
            response.Add(slot);

            byte[] nameBytes = new byte[10];
            byte[] raw = System.Text.Encoding.ASCII.GetBytes(character.Name);
            Array.Copy(raw, nameBytes, Math.Min(raw.Length, 10));

            response.AddRange(nameBytes);
            response.Add((byte)character.Class);
            response.Add((byte)character.Level);

            slot++;
        }

        response[1] = (byte)response.Count;

        stream.Write(response.ToArray());
    }

    private void HandleCharacterCreate(byte[] packet, NetworkStream stream, ClientSession session)
    {
        byte classId = packet.Length > 4 ? packet[4] : (byte)0;

        var list = _characterService.GetCharacters(session.AccountName);
        string name = $"PJ{list.Count + 1}";

        var character = _characterService.CreateCharacter(
            session.AccountName,
            name,
            classId);

        byte[] response =
        {
            0xC1, 0x06,
            0xF3, 0x01,
            (byte)(character != null ? 0 : 1),
            0x00
        };

        stream.Write(response);
    }

    private void HandleEnterWorld(byte[] packet, NetworkStream stream, ClientSession session)
    {
        byte slot = packet.Length > 4 ? packet[4] : (byte)0;

        var character = _characterService.GetCharacterBySlot(session.AccountName, slot);
        if (character == null)
            return;

        session.SelectedCharacter = character;

        var player = _worldManager.EnterWorld(session.AccountName, character);
        player.Stream = stream;
        session.PlayerId = player.PlayerId;

        SendEnterWorld(stream, player);
        SendStats(stream, character);
        SendWorld(stream, player);
    }

    private void SendEnterWorld(NetworkStream stream, Player player)
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

        stream.Write(response);
    }

    private void SendStats(NetworkStream stream, Character c)
    {
        byte[] response =
        {
            0xC1, 0x12,
            0xF3, 0x20,
            c.Class,
            (byte)c.Strength,
            (byte)c.Agility,
            (byte)c.Vitality,
            (byte)c.Energy,
            (byte)c.Leadership,
            (byte)c.Life,
            (byte)c.Mana,
            (byte)c.Level,
            (byte)c.MapId,
            c.X,
            c.Y,
            0,0
        };

        stream.Write(response);
    }

    private void SendWorld(NetworkStream stream, Player player)
    {
        var monsters = _monsterManager.GetMonstersInMap(player.CurrentMapId);
        var visible = _worldManager.GetVisibleMonsters(player, monsters);

        foreach (var m in visible)
        {
            byte[] packet =
            {
                0xC1,0x09,0xF4,0x01,
                (byte)m.MonsterId,
                m.MonsterClass,
                m.MapId,
                m.X,
                m.Y
            };

            stream.Write(packet);
        }
    }
}