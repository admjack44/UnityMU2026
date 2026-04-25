using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
using MUServer.Core.Services;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class CombatPacketHandler
{
    private readonly CharacterService _characterService;
    private readonly BroadcastService _broadcastService;
    private readonly MonsterManager _monsterManager;
    private readonly WorldManager _worldManager;
    private readonly AutoCombatService _autoCombatService;
    private readonly ILogger<CombatPacketHandler> _logger;

    public CombatPacketHandler(
        CharacterService characterService,
        BroadcastService broadcastService,
        MonsterManager monsterManager,
        WorldManager worldManager,
        AutoCombatService autoCombatService,
        ILogger<CombatPacketHandler> logger)
    {
        _characterService = characterService;
        _broadcastService = broadcastService;
        _monsterManager = monsterManager;
        _worldManager = worldManager;
        _autoCombatService = autoCombatService;
        _logger = logger;
    }

    public void Handle(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
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

        HandleManualAttack(packet, stream, session);
    }

    private void HandleManualAttack(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (session.SelectedCharacter is null)
        {
            _logger.LogWarning("Ataque ignorado: no hay personaje seleccionado.");
            return;
        }

        if (!session.PlayerId.HasValue)
        {
            SendAttackFailedPacket(stream, 0, 0x06);
            return;
        }

        if (session.SelectedCharacter.CurrentLife == 0)
        {
            SendAttackFailedPacket(stream, 0, 0x03);
            return;
        }

        if (packet.Length < 5)
        {
            SendAttackFailedPacket(stream, 0, 0x04);
            return;
        }

        int monsterId = packet[4];

        Monster? monster = _monsterManager.GetMonster(monsterId);
        if (monster is null)
        {
            SendAttackFailedPacket(stream, (byte)monsterId, 0x04);
            return;
        }

        if (!monster.IsAlive)
        {
            SendAttackFailedPacket(stream, (byte)monsterId, 0x05);
            return;
        }

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            SendAttackFailedPacket(stream, (byte)monsterId, 0x06);
            return;
        }

        if (player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendAttackFailedPacket(stream, (byte)monsterId, 0x03);
            return;
        }

        if ((DateTime.UtcNow - player.LastAttackTimeUtc).TotalMilliseconds < 800)
        {
            SendAttackFailedPacket(stream, (byte)monsterId, 0x01);
            return;
        }

        int dx = Math.Abs(player.X - monster.X);
        int dy = Math.Abs(player.Y - monster.Y);

        if (dx > 3 || dy > 3)
        {
            _logger.LogWarning(
                "Ataque fuera de rango. Player={PlayerX},{PlayerY} Monster={MonsterX},{MonsterY}",
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
            HandleMonsterCounterAttack(stream, session, monsterId);
            return;
        }

        HandleMonsterKilled(stream, session, monsterId);
    }

    private void HandleMonsterCounterAttack(NetworkStream stream, ClientSession session, int monsterId)
    {
        if (session.SelectedCharacter is null)
            return;

        var (monsterDamage, playerRemainingHp, playerDead) =
            _monsterManager.CounterAttackPlayer(monsterId, session.SelectedCharacter);

        SendMonsterHitPacket(stream, (byte)monsterId, monsterDamage, playerRemainingHp, playerDead);

        if (!playerDead)
            return;

        SendPlayerDeathPacket(stream);

        if (session.PlayerId.HasValue)
        {
            Player? deadPlayer = _worldManager.GetPlayer(session.PlayerId.Value);
            if (deadPlayer is not null)
            {
                var receivers = _worldManager.GetVisiblePlayers(deadPlayer);
                _broadcastService.BroadcastPlayerDeath(deadPlayer, receivers);
            }
        }

        _ = SchedulePlayerRespawnAsync(stream, session, session.SelectedCharacter);
    }

    private void HandleMonsterKilled(NetworkStream stream, ClientSession session, int monsterId)
    {
        if (session.SelectedCharacter is null)
            return;

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

        Monster? deadMonster = _monsterManager.GetMonster(monsterId);
        if (deadMonster is not null)
        {
            WorldItem item = _worldManager.SpawnItem(
                deadMonster.MapId,
                deadMonster.X,
                deadMonster.Y);

            var receivers = _worldManager.GetPlayersNearItem(item);

            foreach (Player receiver in receivers)
            {
                if (receiver.Stream is not null)
                {
                    SendItemDropPacket(receiver.Stream, item);
                }
            }
        }

        _ = ScheduleMonsterRespawnAsync((byte)monsterId, stream);
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
            _autoCombatService.EnableAutoCombat(session.PlayerId.Value);
        else
            _autoCombatService.DisableAutoCombat(session.PlayerId.Value);

        byte[] response =
        {
            0xC1, 0x05,
            0xD7, 0x10,
            (byte)(enabled ? 1 : 0)
        };

        stream.Write(response, 0, response.Length);

        _logger.LogInformation(
            "AutoCombat {State} Player:{PlayerId}",
            enabled ? "ON" : "OFF",
            session.PlayerId.Value);
    }

    private async Task ScheduleMonsterRespawnAsync(byte monsterId, NetworkStream stream)
    {
        Monster? monster = await _monsterManager.RespawnMonsterAsync(monsterId);

        if (monster is null || !monster.IsAlive)
            return;

        SendMonsterSpawnPacket(stream, monster);
    }

    private async Task SchedulePlayerRespawnAsync(NetworkStream stream, ClientSession session, Character character)
    {
        await Task.Delay(5000);

        if (character.CurrentLife > 0)
            return;

        character.MapId = 0;
        character.X = 125;
        character.Y = 125;
        character.CurrentLife = character.MaxLife;

        SendPlayerRespawnPacket(stream, character);

        if (!session.PlayerId.HasValue)
            return;

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
            return;

        var receivers = _worldManager.GetVisiblePlayers(player);
        _broadcastService.BroadcastPlayerRespawn(player, receivers);
    }

    private void SendMonsterAttackResponse(NetworkStream stream, byte monsterId, byte damage, byte remainingHp, bool killed)
    {
        byte[] response =
        {
            0xC1, 0x08,
            0xD7, 0x00,
            monsterId,
            damage,
            remainingHp,
            (byte)(killed ? 1 : 0)
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendAttackFailedPacket(NetworkStream stream, byte monsterId, byte reasonCode)
    {
        byte[] response =
        {
            0xC1, 0x06,
            0xD7, 0x02,
            reasonCode,
            monsterId
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendMonsterHitPacket(NetworkStream stream, byte monsterId, byte damage, byte remainingHp, bool dead)
    {
        byte[] response =
        {
            0xC1, 0x08,
            0xF4, 0x03,
            monsterId,
            damage,
            remainingHp,
            (byte)(dead ? 1 : 0)
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendExperiencePacket(NetworkStream stream, Character character, uint gainedExp)
    {
        ushort totalExp = (ushort)Math.Min(character.Experience, ushort.MaxValue);

        byte[] response =
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
    }

    private void SendLevelUpPacket(NetworkStream stream, Character character)
    {
        byte[] response =
        {
            0xC1, 0x07,
            0xF3, 0x22,
            (byte)character.Level,
            0x00,
            0x00
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendMonsterDeathPacket(NetworkStream stream, byte monsterId)
    {
        byte[] response =
        {
            0xC1, 0x05,
            0xF4, 0x02,
            monsterId
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendItemDropPacket(NetworkStream stream, WorldItem item)
    {
        byte[] response =
        {
            0xC1, 0x09,
            0xF4, 0x05,
            (byte)item.ItemId,
            item.ItemType,
            item.MapId,
            item.X,
            item.Y
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendPlayerDeathPacket(NetworkStream stream)
    {
        byte[] response =
        {
            0xC1, 0x05,
            0xF3, 0x23,
            0x01
        };

        stream.Write(response, 0, response.Length);
    }

    private void SendPlayerRespawnPacket(NetworkStream stream, Character character)
    {
        byte[] response =
        {
            0xC1, 0x08,
            0xF3, 0x24,
            character.MapId,
            character.X,
            character.Y,
            (byte)Math.Min((int)character.CurrentLife, 255)
        };

        stream.Write(response, 0, response.Length);
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
}