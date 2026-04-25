using Microsoft.Extensions.Logging;

namespace MUServer.Core.World;

public sealed class MonsterAIService
{
    private const int TickMilliseconds = 700;
    private const int DetectionRange = 8;
    private const int AttackRange = 1;
    private const int AttackDamage = 5;

    private readonly MonsterManager _monsterManager;
    private readonly WorldManager _worldManager;
    private readonly Dictionary<int, DateTime> _lastAttackUtcByMonster = new();
    private readonly ILogger<MonsterAIService> _logger;
    
    private CancellationTokenSource? _cts;

    public MonsterAIService(
        MonsterManager monsterManager,
        WorldManager worldManager,
        ILogger<MonsterAIService> logger)
    {
        _monsterManager = monsterManager;
        _worldManager = worldManager;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => TickLoopAsync(_cts.Token));
        _logger.LogInformation("MonsterAIService iniciado.");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _logger.LogInformation("MonsterAIService detenido.");
    }

    private async Task TickLoopAsync(CancellationToken token)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(TickMilliseconds));

        while (await timer.WaitForNextTickAsync(token))
        {
            Tick();
        }
    }

    private void Tick()
    {
        var players = _worldManager.GetAllPlayers();
        var monsters = _monsterManager.GetAllMonsters();

        foreach (var monster in monsters)
        {
            if (!monster.IsAlive)
                continue;

            var target = players
                .Where(p => !p.IsDead)
                .Where(p => p.CurrentMapId == monster.MapId)
                .OrderBy(p => Distance(monster.X, monster.Y, p.X, p.Y))
                .FirstOrDefault();

            if (target is null)
                continue;

            int distance = Distance(monster.X, monster.Y, target.X, target.Y);

            if (distance <= AttackRange)
            {
                AttackPlayer(monster, target);
                continue;
            }

            if (distance <= DetectionRange)
            {
                MoveTowards(monster, target);
            }
        }
    }

    private void MoveTowards(Monster monster, Player player)
    {
        if (monster.X < player.X)
            monster.X++;
        else if (monster.X > player.X)
            monster.X--;

        if (monster.Y < player.Y)
            monster.Y++;
        else if (monster.Y > player.Y)
            monster.Y--;

        SendMonsterMove(player, monster);

        _logger.LogInformation(
            "MonsterAI Move Monster:{MonsterId} -> {X},{Y}",
            monster.MonsterId,
            monster.X,
            monster.Y);
    }

    private void AttackPlayer(Monster monster, Player player)
    {
        if (player.Stream is null || !player.Stream.CanWrite)
            return;

        if (player.Character.CurrentLife <= 0)
            return;

        if (_lastAttackUtcByMonster.TryGetValue(monster.MonsterId, out DateTime lastAttack))
        {
            if ((DateTime.UtcNow - lastAttack).TotalMilliseconds < 1500)
                return;
        }

        _lastAttackUtcByMonster[monster.MonsterId] = DateTime.UtcNow;

        const int damage = AttackDamage;

        int remainingLife = Math.Max(0, player.Character.CurrentLife - damage);
        player.Character.CurrentLife = (ushort)remainingLife;

        bool dead = player.Character.CurrentLife <= 0;

        byte[] hitPacket =
        {
        0xC1, 0x08,
        0xF4, 0x03,
        (byte)monster.MonsterId,
        damage,
        (byte)Math.Min((int)player.Character.CurrentLife, 255),
        (byte)(dead ? 1 : 0)
    };

        player.Stream.Write(hitPacket, 0, hitPacket.Length);

        if (dead)
        {
            byte[] deathPacket =
            {
            0xC1, 0x05,
            0xF3, 0x23,
            0x01
        };

            player.Stream.Write(deathPacket, 0, deathPacket.Length);
        }

        _logger.LogInformation(
            "MonsterAI Attack Monster:{MonsterId} Player:{PlayerId} Damage:{Damage} HP:{HP} Dead:{Dead}",
            monster.MonsterId,
            player.PlayerId,
            damage,
            player.Character.CurrentLife,
            dead);
    }

    private static void SendMonsterMove(Player receiver, Monster monster)
    {
        if (receiver.Stream is null || !receiver.Stream.CanWrite)
            return;

        byte[] packet =
        {
            0xC1, 0x07,
            0xF4, 0x04,
            (byte)monster.MonsterId,
            monster.X,
            monster.Y
        };

        receiver.Stream.Write(packet, 0, packet.Length);
    }

    private static int Distance(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }
}