using MUServer.Core.Models;
using MUServer.Core.Network.Handlers;

namespace MUServer.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void AddItem_ShouldPlaceItemInInventory()
    {
        var character = new Character
        {
            Inventory = new byte[160]
        };

        int slot = InventoryPacketHandler.AddItemToInventory(character, 1); // sword 2x4

        Assert.True(slot >= 0);
        Assert.True(character.Inventory[slot] != 0);
    }

    [Fact]
    public void AddItem_ShouldFail_WhenNoSpace()
    {
        var character = new Character
        {
            Inventory = new byte[160]
        };

        // llenar inventario
        for (int i = 0; i < 160; i++)
            character.Inventory[i] = 1;

        int slot = InventoryPacketHandler.AddItemToInventory(character, 1);

        Assert.Equal(-1, slot);
    }

    [Fact]
    public void Items_ShouldNotOverlap()
    {
        var character = new Character
        {
            Inventory = new byte[160]
        };

        int slot1 = InventoryPacketHandler.AddItemToInventory(character, 1);
        int slot2 = InventoryPacketHandler.AddItemToInventory(character, 1);

        Assert.NotEqual(slot1, slot2);
    }
}