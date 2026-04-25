using System;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
using MUServer.Core.World;

namespace MUServer.Core.Network.Handlers;

public sealed class InventoryPacketHandler
{
    private const int InventoryWidth = 8;
    private const int InventoryHeight = 20;
    private const int InventorySlots = InventoryWidth * InventoryHeight;

    private static readonly Dictionary<byte, ItemDefinition> ItemDefinitions = new()
    {
        [0] = new ItemDefinition { ItemType = 0, Name = "Potion", Width = 1, Height = 1 },
        [1] = new ItemDefinition { ItemType = 1, Name = "Sword", Width = 2, Height = 4 },
        [2] = new ItemDefinition { ItemType = 2, Name = "Armor", Width = 2, Height = 3 }
    };

    private readonly WorldManager _worldManager;
    private readonly ILogger<InventoryPacketHandler> _logger;

    public InventoryPacketHandler(
        WorldManager worldManager,
        ILogger<InventoryPacketHandler> logger)
    {
        _worldManager = worldManager;
        _logger = logger;
    }

    public void HandleMoveItem(byte[] packet, NetworkStream stream, ClientSession session)
    {
        if (!session.PlayerId.HasValue || session.SelectedCharacter is null)
        {
            SendInventoryMoveResult(stream, 0, 0, false, 0x06);
            return;
        }

        if (packet.Length < 6)
        {
            SendInventoryMoveResult(stream, 0, 0, false, 0x04);
            return;
        }

        byte fromSlot = packet[4];
        byte toSlot = packet[5];

        Player? player = _worldManager.GetPlayer(session.PlayerId.Value);
        if (player is null || player.IsDead || player.Character.CurrentLife <= 0)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x03);
            return;
        }

        Character character = session.SelectedCharacter;

        EnsureInventorySize(character);

        if (fromSlot >= InventorySlots || toSlot >= InventorySlots)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x01);
            return;
        }

        byte itemValue = character.Inventory[fromSlot];
        if (itemValue == 0)
        {
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x02);
            return;
        }

        byte itemType = (byte)(itemValue - 1);
        ItemDefinition definition = GetItemDefinition(itemType);

        ClearItemFromInventory(character, fromSlot, definition.Width, definition.Height);

        if (!CanPlaceItem(character, toSlot, definition.Width, definition.Height))
        {
            PlaceItem(character, fromSlot, itemType, definition.Width, definition.Height);
            SendInventoryMoveResult(stream, fromSlot, toSlot, false, 0x05);
            return;
        }

        PlaceItem(character, toSlot, itemType, definition.Width, definition.Height);

        SendInventoryMoveResult(stream, fromSlot, toSlot, true, 0x00);
        SendInventoryUpdatePacket(stream, character, toSlot);

        _logger.LogInformation(
            "Inventory move From:{FromSlot} To:{ToSlot} ItemType:{ItemType} Size:{Width}x{Height}",
            fromSlot,
            toSlot,
            itemType,
            definition.Width,
            definition.Height);
    }

    public static int AddItemToInventory(Character character, byte itemType)
    {
        EnsureInventorySize(character);

        ItemDefinition definition = GetItemDefinition(itemType);

        for (int slot = 0; slot < InventorySlots; slot++)
        {
            if (CanPlaceItem(character, slot, definition.Width, definition.Height))
            {
                PlaceItem(character, slot, itemType, definition.Width, definition.Height);
                return slot;
            }
        }

        return -1;
    }

    public static void SendInventoryUpdatePacket(NetworkStream stream, Character character, byte slot)
    {
        byte usedSlots = (byte)character.Inventory.Count(item => item != 0);
        byte itemValue = character.Inventory[slot];

        ItemDefinition definition = itemValue == 0
            ? GetItemDefinition(0)
            : GetItemDefinition((byte)(itemValue - 1));

        byte[] packet =
        {
            0xC1,
            0x0B,
            0xF3,
            0x30,
            usedSlots,
            slot,
            itemValue,
            InventoryWidth,
            InventoryHeight,
            definition.Width,
            definition.Height
        };

        stream.Write(packet, 0, packet.Length);
    }

    private static void SendInventoryMoveResult(NetworkStream stream, byte fromSlot, byte toSlot, bool success, byte reasonCode)
    {
        byte[] packet =
        {
            0xC1,
            0x08,
            0xF3,
            0x31,
            fromSlot,
            toSlot,
            (byte)(success ? 1 : 0),
            reasonCode
        };

        stream.Write(packet, 0, packet.Length);
    }

    private static ItemDefinition GetItemDefinition(byte itemType)
    {
        return ItemDefinitions.TryGetValue(itemType, out ItemDefinition? definition)
            ? definition
            : ItemDefinitions[0];
    }

    private static void EnsureInventorySize(Character character)
    {
        if (character.Inventory.Length == InventorySlots)
            return;

        byte[] newInventory = new byte[InventorySlots];

        Array.Copy(
            character.Inventory,
            newInventory,
            Math.Min(character.Inventory.Length, newInventory.Length));

        character.Inventory = newInventory;
    }

    private static bool CanPlaceItem(Character character, int startSlot, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        if (startColumn + width > InventoryWidth)
            return false;

        if (startRow + height > InventoryHeight)
            return false;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;

                if (slot < 0 || slot >= character.Inventory.Length)
                    return false;

                if (character.Inventory[slot] != 0)
                    return false;
            }
        }

        return true;
    }

    private static void PlaceItem(Character character, int startSlot, byte itemType, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        byte storedValue = (byte)(itemType + 1);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;
                character.Inventory[slot] = storedValue;
            }
        }
    }

    private static void ClearItemFromInventory(Character character, int startSlot, int width, int height)
    {
        int startRow = startSlot / InventoryWidth;
        int startColumn = startSlot % InventoryWidth;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int slot = (startRow + row) * InventoryWidth + startColumn + column;

                if (slot >= 0 && slot < character.Inventory.Length)
                    character.Inventory[slot] = 0;
            }
        }
    }
}