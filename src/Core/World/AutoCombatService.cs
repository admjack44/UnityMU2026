using Microsoft.Extensions.Logging;
using MUServer.Core.Services;

namespace MUServer.Core.World;

public sealed class AutoCombatService
{
    private const int TickMilliseconds = 500;
    private const int AttackRange = 3;
    private const int AttackCooldownMs = 1000;

    private readonly WorldManager _worldManager;
    private readonly MonsterManager _monsterManager;
    private readonly MovementService _movementService;
    private readonly CharacterService _characterService;
    private readonly ILogger<AutoCombatService> _logger;

    private readonly Dictionary<int, AutoCombatState> _states = new();
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;

    public AutoCombatService(
        WorldManager worldManager,
        MonsterManager monsterManager,
        MovementService movementService,
        CharacterService characterService,
        ILogger<AutoCombatService> logger)
    {
        _worldManager = worldManager;
        _monsterManager = monsterManager;
        _movementService = movementService;
        _characterService = characterService;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => TickLoopAsync(_cts.Token));

        _logger.LogInformation("AutoCombatService iniciado.");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _logger.LogInformation("AutoCombatService detenido.");
    }

    public void EnableAutoCombat(int playerId)
    {
        lock (_lock)
        {
            _states[playerId] = new AutoCombatState
            {
                PlayerId = playerId,
                IsEnabled = true
            };
        }

        _logger.LogInformation("AutoCombat ON -> Player:{PlayerId}", playerId);
    }

    public void DisableAutoCombat(int playerId)
    {
        lock (_lock)
        {
            _states.Remove(playerId);
        }

        _logger.LogInformation("AutoCombat OFF -> Player:{PlayerId}", playerId);
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
        lock (_lock)
        {
            foreach (AutoCombatState state in _states.Values.ToList())
            {
                ProcessPlayer(state);
            }
        }
    }

    private void ProcessPlayer(AutoCombatState state)
    {
        var player = _worldManager.GetPlayer(state.PlayerId);
        if (player is null)
        {
            _states.Remove(state.PlayerId);
            return;
        }

        if (player.IsDead || player.Character.CurrentLife <= 0)
        {
            return;
        }

        var monster = FindNearestMonster(player);
        if (monster is null)
        {
            return;
        }

        int dx = Math.Abs(player.X - monster.X);
        int dy = Math.Abs(player.Y - monster.Y);

        if (dx > AttackRange || dy > AttackRange)
        {
            _movementService.SetMoveTarget(
                player.PlayerId,
                player.CurrentMapId,
                monster.X,
                monster.Y
            );

            return;
        }

        if ((DateTime.UtcNow - state.LastAttackUtc).TotalMilliseconds < AttackCooldownMs)
        {
            return;
        }

        state.LastAttackUtc = DateTime.UtcNow;

        var (damage, remainingHp, killed) =
            _monsterManager.AttackMonster(monster.MonsterId, player.Character);

        _logger.LogInformation(
            "AutoAttack -> Player:{PlayerId} Monster:{MonsterId} Damage:{Damage} HP:{HP} Killed:{Killed}",
            player.PlayerId,
            monster.MonsterId,
            damage,
            remainingHp,
            killed
        );

        if (killed)
        {
            uint exp = _monsterManager.GetMonsterExperience(monster.MonsterId);

            player.Character.Experience += exp;

            _characterService.UpdateCharacterExperience(
                player.Character.Id,
                player.Character.Experience
            );

            _characterService.TryLevelUp(player.Character);
        }
    }

    private Monster? FindNearestMonster(Player player)
    {
        var monsters = _monsterManager.GetMonstersInMap(player.CurrentMapId);

        return monsters
            .Where(m => m.IsAlive)
            .OrderBy(m =>
                Math.Abs(player.X - m.X) +
                Math.Abs(player.Y - m.Y))
            .FirstOrDefault();
    }

    private sealed class AutoCombatState
    {
        public int PlayerId { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime LastAttackUtc { get; set; } = DateTime.MinValue;
    }
}