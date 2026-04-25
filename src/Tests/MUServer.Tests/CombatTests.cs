using Microsoft.Extensions.Logging.Abstractions;
using MUServer.Core.Models;
using MUServer.Core.World;

namespace MUServer.Tests;

public sealed class CombatTests
{
    [Fact]
    public void AttackMonster_ShouldReduceHp()
    {
        var monsterManager = new MonsterManager(
            NullLogger<MonsterManager>.Instance);

        var monster = monsterManager.GetMonster(1);
        Assert.NotNull(monster);

        var character = new Character
        {
            Strength = 50
        };

        var (_, remainingHp, _) =
            monsterManager.AttackMonster(1, character);

        Assert.True(remainingHp < monster!.MaxHp);
    }

    [Fact]
    public void MonsterShouldDie_WhenDamageExceedsHp()
    {
        var monsterManager = new MonsterManager(
            NullLogger<MonsterManager>.Instance);

        var character = new Character
        {
            Strength = 999
        };

        var (_, remainingHp, killed) =
            monsterManager.AttackMonster(1, character);

        Assert.True(killed);
        Assert.Equal(0, remainingHp);
    }
}