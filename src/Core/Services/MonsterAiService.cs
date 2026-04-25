using Microsoft.Extensions.Logging;
using MUServer.Core.World;

namespace MUServer.Core.Services;

public sealed class MonsterAiService
{
    private const int AiTickMilliseconds = 1000;
    private const int AttackRange = 1;
    private const int DetectionRange = 6;
    private const int MonsterDamage = 10;
    private const int MonsterAttackCooldownMilliseconds = 1500;

    private readonly MonsterManager _monsterManager;
    private readonly WorldManager _worldManager;
    private readonly BroadcastService _broadcastService;
    private readonly ILogger _logger;

    public MonsterAiService(
        MonsterManager monsterManager,
        WorldManager worldManager,
        BroadcastService broadcastService,
        ILogger logger)
    {
        _monsterManager = monsterManager;
        _worldManager = worldManager;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monster AI iniciada.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(AiTickMilliseconds, cancellationToken);
            Tick();
        }
    }

    private void Tick()
    {
        var monsters = _monsterManager.GetAllMonsters();

        foreach (var monster in monsters)
        {
            if (!monster.IsAlive)
            {
                continue;
            }

            var target = _worldManager.GetNearestPlayer(
                monster.MapId,
                monster.X,
                monster.Y,
                range: DetectionRange);

            if (target is null)
            {
                continue;
            }

            _logger.LogInformation(
                "Monster AI => Monster:{MonsterId} Target:{PlayerId}",
                monster.MonsterId,
                target.PlayerId);

            _monsterManager.MoveMonsterTowards(monster, target);

            var monsterReceivers = _worldManager.GetVisiblePlayersFromMonster(monster);
            _broadcastService.BroadcastMonsterMove(monster, monsterReceivers);

            var distance = Math.Abs(monster.X - target.X) + Math.Abs(monster.Y - target.Y);

            if (distance > AttackRange)
            {
                continue;
            }

            if (!CanMonsterAttack(monster))
            {
                continue;
            }

            monster.LastAttackTimeUtc = DateTime.UtcNow;

            ApplyMonsterDamage(monster, target, MonsterDamage);
        }
    }

    private static bool CanMonsterAttack(Monster monster)
    {
        var elapsed = DateTime.UtcNow - monster.LastAttackTimeUtc;
        return elapsed.TotalMilliseconds >= MonsterAttackCooldownMilliseconds;
    }

    private void ApplyMonsterDamage(Monster monster, Player target, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        var currentLife = target.Character.CurrentLife;
        var newLife = Math.Max(0, (int)currentLife - damage);

        target.Character.CurrentLife = (ushort)newLife;
        target.Character.Life = (ushort)newLife;

        var dead = target.Character.CurrentLife <= 0;

        SendMonsterHit(monster, target, damage, dead);

        _logger.LogInformation(
            "Monster AI attack => Monster:{MonsterId} Player:{PlayerId} Damage:{Damage} HP:{HP}",
            monster.MonsterId,
            target.PlayerId,
            damage,
            target.Character.CurrentLife);

        if (!dead)
        {
            return;
        }

        target.Character.CurrentLife = 0;
        target.Character.Life = 0;

        _logger.LogInformation(
            "Player muerto por monster => Player:{PlayerId}",
            target.PlayerId);

        var receivers = _worldManager.GetOtherPlayersInMap(target);
        _broadcastService.BroadcastPlayerDeath(target, receivers);
    }

    private void SendMonsterHit(Monster monster, Player target, int damage, bool dead)
    {
        if (target.Stream is null)
        {
            _logger.LogWarning(
                "No se pudo enviar MonsterHit: Player:{PlayerId} no tiene Stream activo.",
                target.PlayerId);

            return;
        }

        var monsterId = (byte)Math.Clamp(monster.MonsterId, 0, byte.MaxValue);
        var damageValue = (byte)Math.Clamp(damage, 0, byte.MaxValue);
        var playerHp = (byte)Math.Clamp((int)target.Character.CurrentLife, 0, byte.MaxValue);

        var packet = new byte[]
        {
            0xC1,
            0x08,
            0xF4,
            0x03,
            monsterId,
            damageValue,
            playerHp,
            (byte)(dead ? 1 : 0)
        };

        target.Stream.Write(packet, 0, packet.Length);

        _logger.LogInformation(
            "<- Monster hit player: MonsterId:{MonsterId} Damage:{Damage} PlayerHP:{PlayerHP} Dead:{Dead}",
            monsterId,
            damageValue,
            playerHp,
            dead);
    }
}