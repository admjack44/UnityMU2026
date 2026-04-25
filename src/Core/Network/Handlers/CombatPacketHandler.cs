using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class CombatPacketHandler
{
    private readonly MonsterManager _monsterManager;
    private readonly WorldManager _worldManager;
    private readonly ILogger _logger;

    public CombatPacketHandler(
        MonsterManager monsterManager,
        WorldManager worldManager,
        ILogger logger)
    {
        _monsterManager = monsterManager;
        _worldManager = worldManager;
        _logger = logger;
    }

    public void Handle(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        var handler = new MUPacketHandler(
            _logger,
            null!,
            null!,
            _monsterManager,
            _worldManager,
            null!,
            null!
        );

        var method = typeof(MUPacketHandler).GetMethod(
            "HandleAttackPacket",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        method!.Invoke(handler, new object[] { subCode, stream, session, packet });
    }
}