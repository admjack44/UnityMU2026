using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
using System.Threading.Tasks;

namespace MUServer.Core.World;

public sealed class MonsterManager
{
    private readonly ILogger _logger;
    private readonly Dictionary<int, Monster> _monsters = new();
    private int _nextMonsterId = 1;

    public MonsterManager(ILogger logger)
    {
        _logger = logger;
        InitializeMonsters();
    }

    public void MoveMonsterTowards(Monster monster, Player target)
    {
        if (monster.MapId != target.CurrentMapId)
        {
            return;
        }

        if (monster.X < target.X) monster.X++;
        else if (monster.X > target.X) monster.X--;

        if (monster.Y < target.Y) monster.Y++;
        else if (monster.Y > target.Y) monster.Y--;

        _logger.LogInformation(
            "Monster move AI => Monster:{MonsterId} Target:{PlayerId} Pos:{X},{Y}",
            monster.MonsterId,
            target.PlayerId,
            monster.X,
            monster.Y);
    }

    private void InitializeMonsters()
    {
        SpawnMonster(new Monster
        {
            MonsterClass = 0,
            Name = "Goblin",
            MapId = 0,
            X = 141,
            Y = 138,
            SpawnX = 141,
            SpawnY = 138,
            MaxHp = 40,
            CurrentHp = 40,
            IsAlive = true
        });

        SpawnMonster(new Monster
        {
            MonsterClass = 1,
            Name = "Spider",
            MapId = 0,
            X = 145,
            Y = 122,
            SpawnX = 145,
            SpawnY = 122,
            MaxHp = 60,
            CurrentHp = 60,
            IsAlive = true
        });

        _logger.LogInformation("Monstruos inicializados: {Count}", _monsters.Count);
    }

    private void SpawnMonster(Monster monster)
    {
        monster.MonsterId = _nextMonsterId++;
        _monsters[monster.MonsterId] = monster;
    }

    public IReadOnlyList<Monster> GetMonstersInMap(byte mapId)
    {
        return _monsters.Values
            .Where(m => m.MapId == mapId && m.IsAlive)
            .ToList();
    }

    public Monster? GetMonster(int monsterId)
    {
        return _monsters.TryGetValue(monsterId, out var monster)
            ? monster
            : null;
    }

    public IEnumerable<Monster> GetAllMonsters()
    {
        return _monsters.Values;
    }

    public (byte Damage, byte RemainingHp, bool Killed) AttackMonster(int monsterId, Character attacker)
    {
        if (!_monsters.TryGetValue(monsterId, out var monster))
        {
            return (0, 0, false);
        }

        if (!monster.IsAlive)
        {
            return (0, 0, false);
        }

        int baseDamage = attacker.Strength + (attacker.Agility / 4);

        int classBonus = monster.MonsterClass switch
        {
            0 => 0,   // Goblin
            1 => 5,   // Spider
            2 => 10,
            _ => 2
        };

        int damage = baseDamage + classBonus;

        if (damage < 1)
        {
            damage = 1;
        }

        int remainingHp = Math.Max(0, monster.CurrentHp - damage);
        monster.CurrentHp = (ushort)remainingHp;

        bool killed = monster.CurrentHp == 0;
        if (killed)
        {
            monster.IsAlive = false;

            _ = RespawnMonsterAfterDelayAsync(monster, TimeSpan.FromSeconds(5));
        }

        _logger.LogInformation(
            "Combat => Player:{Player} Monster:{Monster}(ID:{MonsterId}) Damage:{Damage} HP:{RemainingHp}",
            attacker.Name,
            monster.Name,
            monster.MonsterId,
            damage,
            monster.CurrentHp);

        byte finalDamage = (byte)Math.Clamp(damage, 0, 255);
        byte finalHp = (byte)Math.Clamp((int)monster.CurrentHp, 0, 255);

        return (finalDamage, finalHp, killed);
    }

    public (byte Damage, byte RemainingHp, bool Dead) CounterAttackPlayer(int monsterId, Character target)
    {
        if (!_monsters.TryGetValue(monsterId, out var monster))
        {
            return (0, 0, false);
        }

        if (!monster.IsAlive)
        {
            return (0, 0, false);
        }

        int damage = 25 + monster.MonsterClass * 5;

        int remainingHp = Math.Max(0, target.CurrentLife - damage);
        target.CurrentLife = (ushort)remainingHp;

        bool dead = target.CurrentLife == 0;

        _logger.LogInformation(
            "Monster counter => Monster:{Monster}(ID:{MonsterId}) Target:{Target} Damage:{Damage} HP:{RemainingHp}",
            monster.Name,
            monster.MonsterId,
            target.Name,
            damage,
            target.CurrentLife);

        byte finalDamage = (byte)Math.Clamp(damage, 0, 255);
        byte finalHp = (byte)Math.Clamp((int)target.CurrentLife, 0, 255);

        return (finalDamage, finalHp, dead);
    }

    public async Task<Monster?> RespawnMonsterAsync(int monsterId, int delayMilliseconds = 5000)
    {
        if (!_monsters.TryGetValue(monsterId, out var monster))
        {
            return null;
        }

        await Task.Delay(delayMilliseconds);

        monster.CurrentHp = monster.MaxHp;
        monster.X = monster.SpawnX;
        monster.Y = monster.SpawnY;
        monster.IsAlive = true;

        _logger.LogInformation(
            "Monster respawn => Monster:{Monster}(ID:{MonsterId}) Map:{Map} Pos:{X},{Y} HP:{Hp}",
            monster.Name,
            monster.MonsterId,
            monster.MapId,
            monster.X,
            monster.Y,
            monster.CurrentHp);

        return monster;
    }

    public uint GetMonsterExperience(int monsterId)
    {
        if (!_monsters.TryGetValue(monsterId, out var monster))
        {
            return 0;
        }

        return monster.MonsterClass switch
        {
            0 => 20, // Goblin
            1 => 25, // Spider
            _ => 10
        };
    }

    private async Task RespawnMonsterAfterDelayAsync(Monster monster, TimeSpan delay)
    {
        await Task.Delay(delay);

        monster.Respawn();

        _logger.LogInformation(
            "Monster respawn => Monster:{Name}(ID:{Id}) Map:{Map} Pos:{X},{Y} HP:{HP}",
            monster.Name,
            monster.MonsterId,
            monster.MapId,
            monster.X,
            monster.Y,
            monster.CurrentHp);
    }
}