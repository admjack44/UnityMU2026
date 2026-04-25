using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class MovementPacketHandler
{
    private readonly MovementService _movementService;
    private readonly WorldManager _worldManager;
    private readonly ILogger<MovementPacketHandler> _logger;

    public MovementPacketHandler(
        MovementService movementService,
        WorldManager worldManager,
        ILogger<MovementPacketHandler> logger)
    {
        _movementService = movementService;
        _worldManager = worldManager;
        _logger = logger;
    }

    public void Handle(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
        {
            _logger.LogWarning("MoveRequest ignorado: no hay player activo.");
            return;
        }

        if (packet.Length < 6)
        {
            _logger.LogWarning("MoveRequest inválido: paquete demasiado corto.");
            return;
        }

        if (subCode != 0x01 && subCode != 0x10)
        {
            _logger.LogDebug("SubCode D4 no manejado: {SubCode:X2}", subCode);
            return;
        }

        var player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            _logger.LogWarning("MoveRequest ignorado: player no existe en WorldManager.");
            return;
        }

        byte mapId = player.CurrentMapId;
        byte targetX = packet[4];
        byte targetY = packet[5];

        _movementService.SetMoveTarget(
            session.PlayerId.Value,
            mapId,
            targetX,
            targetY);

        _logger.LogInformation(
            "MoveRequest aceptado Player:{PlayerId} Map:{Map} Target:{X},{Y}",
            session.PlayerId.Value,
            mapId,
            targetX,
            targetY);
    }
}