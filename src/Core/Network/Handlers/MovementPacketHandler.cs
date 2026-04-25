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
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        if (packet.Length < 6)
        {
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        if (subCode != 0x01 && subCode != 0x10)
        {
            _logger.LogWarning("Move subCode no soportado: {SubCode:X2}", subCode);
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            SendMoveResponse(stream, 0, 0, false);
            return;
        }

        byte targetX = packet[4];
        byte targetY = packet[5];
        byte mapId = player.CurrentMapId;

        if (player.X == targetX && player.Y == targetY)
        {
            SendMoveResponse(stream, targetX, targetY, true);
            return;
        }

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

    private static void SendMoveResponse(NetworkStream stream, byte x, byte y, bool success)
    {
        byte[] response =
        {
            0xC1, 0x06,
            0xD4,
            (byte)(success ? 0x01 : 0x00),
            x,
            y
        };

        stream.Write(response, 0, response.Length);
    }
}