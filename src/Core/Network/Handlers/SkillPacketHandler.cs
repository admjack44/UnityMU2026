using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Combat;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class SkillPacketHandler
{
    private readonly WorldManager _world;
    private readonly MonsterManager _monsters;
    private readonly SkillService _skills;
    private readonly ILogger<SkillPacketHandler> _logger;

    public SkillPacketHandler(
        WorldManager world,
        MonsterManager monsters,
        SkillService skills,
        ILogger<SkillPacketHandler> logger)
    {
        _world = world;
        _monsters = monsters;
        _skills = skills;
        _logger = logger;
    }

    public void Handle(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
            return;

        if (packet.Length < 6)
            return;

        byte skillId = packet[4];
        int targetId = packet[5];

        var player = _world.GetPlayer(session.PlayerId.Value);
        if (player == null)
            return;

        if (!_skills.TryUseSkill(player, skillId, out var skill))
            return;

        var monster = _monsters.GetMonster(targetId);
        if (monster == null || !monster.IsAlive)
            return;

        int newHp = Math.Max(0, monster.CurrentHp - skill!.Damage);
        monster.CurrentHp = (ushort)newHp;

        if (monster.CurrentHp == 0)
        {
            monster.IsAlive = false;
        }

        byte[] packetOut =
        {
            0xC1, 0x08,
            0xD7, 0x10,
            (byte)targetId,
            (byte)skill.Damage,
            (byte)monster.CurrentHp,
            (byte)(monster.CurrentHp == 0 ? 1 : 0)
        };

        stream.Write(packetOut, 0, packetOut.Length);

        _logger.LogInformation(
            "Skill used Player:{Player} Skill:{Skill} Damage:{Damage}",
            player.PlayerId,
            skill.Name,
            skill.Damage);
    }
}