using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Network.Handlers;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class WorldItemPacketHandler
{
    private readonly WorldManager _worldManager;
    private readonly ILogger<WorldItemPacketHandler> _logger;

    public WorldItemPacketHandler(
        WorldManager worldManager,
        ILogger<WorldItemPacketHandler> logger)
    {
        _worldManager = worldManager;
        _logger = logger;
    }

    public void Handle(byte subCode, byte[] packet, NetworkStream stream, ClientSession session)
    {
        switch (subCode)
        {
            case 0x06:
                HandlePickItem(packet, stream, session);
                break;

            default:
                _logger.LogDebug("SubCode F4 no manejado: {SubCode:X2}", subCode);
                break;
        }
    }

    private void HandlePickItem(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue)
        {
            SendPickItemResult(stream, 0, false, 0x06);
            return;
        }

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null)
        {
            SendPickItemResult(stream, 0, false, 0x06);
            return;
        }

        if (player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendPickItemResult(stream, 0, false, 0x03);
            return;
        }

        if (packet.Length < 5)
        {
            SendPickItemResult(stream, 0, false, 0x04);
            return;
        }

        int itemId = packet[4];

        WorldItem? item = _worldManager.GetItem(itemId);
        if (item is null)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x01);
            return;
        }

        if (item.MapId != player.CurrentMapId)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x02);
            return;
        }

        int dx = Math.Abs(player.X - item.X);
        int dy = Math.Abs(player.Y - item.Y);

        if (dx > 2 || dy > 2)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x05);
            return;
        }

        int slot = InventoryPacketHandler.AddItemToInventory(player.Character, item.ItemType);
        if (slot < 0)
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x07);
            return;
        }

        if (!_worldManager.RemoveItem(itemId))
        {
            SendPickItemResult(stream, (byte)itemId, false, 0x01);
            return;
        }

        SendPickItemResult(stream, (byte)itemId, true, 0x00);
        InventoryPacketHandler.SendInventoryUpdatePacket(stream, player.Character, (byte)slot);

        _logger.LogInformation(
            "Item picked Player:{PlayerId} ItemId:{ItemId} Type:{ItemType} Slot:{Slot}",
            player.PlayerId,
            item.ItemId,
            item.ItemType,
            slot);
    }

    private static void SendPickItemResult(NetworkStream stream, byte itemId, bool success, byte reasonCode)
    {
        byte[] packet =
        {
            0xC1,
            0x07,
            0xF4,
            0x06,
            itemId,
            (byte)(success ? 1 : 0),
            reasonCode
        };

        stream.Write(packet, 0, packet.Length);
    }
}