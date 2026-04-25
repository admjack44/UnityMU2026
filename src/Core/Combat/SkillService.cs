using MUServer.Core.World;

namespace MUServer.Core.Combat;

public sealed class SkillService
{
    private readonly Dictionary<byte, SkillDefinition> _skills = new()
    {
        [1] = new SkillDefinition
        {
            SkillId = 1,
            Name = "Fireball",
            ManaCost = 10,
            Damage = 25,
            CooldownMs = 2000
        }
    };

    private readonly Dictionary<(int playerId, byte skillId), DateTime> _cooldowns = new();

    public bool TryUseSkill(Player player, byte skillId, out SkillDefinition? skill)
    {
        skill = null;

        if (!_skills.TryGetValue(skillId, out var def))
            return false;

        if (player.Character.Mana < def.ManaCost)
            return false;

        var key = (player.PlayerId, skillId);

        if (_cooldowns.TryGetValue(key, out var last))
        {
            if ((DateTime.UtcNow - last).TotalMilliseconds < def.CooldownMs)
                return false;
        }

        _cooldowns[key] = DateTime.UtcNow;

        player.Character.Mana -= (ushort)def.ManaCost;

        skill = def;
        return true;
    }
}