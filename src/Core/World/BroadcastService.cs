using Microsoft.Extensions.Logging;

namespace MUServer.Core.World;

public sealed class BroadcastService
{
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(ILogger<BroadcastService> logger)
    {
        _logger = logger;
    }

    public void BroadcastPlayerMove(Player player, IEnumerable<Player> receivers)
    {
        var packet = new byte[]
        {
            0xC1, 0x08,
            0xF3, 0x11,
            (byte)player.PlayerId,
            player.CurrentMapId,
            player.X,
            player.Y
        };

        BroadcastToPlayers(receivers, packet);

        _logger.LogInformation(
            "Move broadcast => Player:{PlayerId} Pos:{X},{Y}",
            player.PlayerId,
            player.X,
            player.Y);
    }

    public void BroadcastPlayerRespawn(Player player, IEnumerable<Player> receivers)
    {
        var packet = new byte[]
        {
            0xC1, 0x08,
            0xF3, 0x10,
            player.CurrentMapId,
            player.X,
            player.Y,
            (byte)player.PlayerId
        };

        BroadcastToPlayers(receivers, packet);

        _logger.LogInformation(
            "Respawn broadcast => Player:{PlayerId} Pos:{X},{Y}",
            player.PlayerId,
            player.X,
            player.Y);
    }

    public void BroadcastPlayerDespawn(Player player, IEnumerable<Player> receivers)
    {
        var packet = new byte[]
        {
            0xC1, 0x05,
            0xF3, 0x12,
            (byte)player.PlayerId
        };

        BroadcastToPlayers(receivers, packet);

        _logger.LogInformation(
            "Despawn broadcast => Player:{PlayerId}",
            player.PlayerId);
    }

    public void BroadcastPlayerDeath(Player player, IEnumerable<Player> receivers)
    {
        var packet = new byte[]
        {
            0xC1, 0x05,
            0xF3, 0x23,
            (byte)player.PlayerId
        };

        BroadcastToPlayers(receivers, packet);

        _logger.LogInformation(
            "Player death broadcast => Player:{PlayerId}",
            player.PlayerId);
    }

    public void BroadcastMonsterMove(Monster monster, IEnumerable<Player> receivers)
    {
        var packet = new byte[]
        {
            0xC1, 0x07,
            0xF4, 0x04,
            (byte)monster.MonsterId,
            monster.X,
            monster.Y
        };

        BroadcastToPlayers(receivers, packet);

        _logger.LogInformation(
            "Monster move broadcast => Monster:{MonsterId} Pos:{X},{Y}",
            monster.MonsterId,
            monster.X,
            monster.Y);
    }

    private void BroadcastToPlayers(IEnumerable<Player> receivers, byte[] packet)
    {
        foreach (var receiver in receivers)
        {
            if (receiver.Stream is null)
            {
                continue;
            }

            try
            {
                receiver.Stream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo enviar broadcast a Player:{PlayerId}",
                    receiver.PlayerId);
            }
        }
    }
}