namespace MUServer.Core.Models;

public sealed class ItemDefinition
{
    public byte ItemType { get; init; }

    public string Name { get; init; } = string.Empty;

    public byte Width { get; init; } = 1;

    public byte Height { get; init; } = 1;

    public bool IsCurrency { get; init; }

    public bool CanEquip { get; init; }

    public byte[] AllowedGearSlots { get; init; } = [];
}

public static class ItemDefinitions
{
    public static readonly IReadOnlyDictionary<byte, ItemDefinition> All =
        new Dictionary<byte, ItemDefinition>
        {
            [0] = new()
            {
                ItemType = 0,
                Name = "Potion",
                Width = 1,
                Height = 1
            },

            [1] = new()
            {
                ItemType = 1,
                Name = "Sword",
                Width = 2,
                Height = 4,
                CanEquip = true,
                AllowedGearSlots = [7, 8]
            },

            [2] = new()
            {
                ItemType = 2,
                Name = "Armor",
                Width = 2,
                Height = 3,
                CanEquip = true,
                AllowedGearSlots = [3]
            },

            [3] = new()
            {
                ItemType = 3,
                Name = "Helm",
                Width = 2,
                Height = 2,
                CanEquip = true,
                AllowedGearSlots = [2]
            },

            [4] = new()
            {
                ItemType = 4,
                Name = "Pants",
                Width = 2,
                Height = 2,
                CanEquip = true,
                AllowedGearSlots = [4]
            },

            [5] = new()
            {
                ItemType = 5,
                Name = "Gloves",
                Width = 2,
                Height = 2,
                CanEquip = true,
                AllowedGearSlots = [5]
            },

            [6] = new()
            {
                ItemType = 6,
                Name = "Boots",
                Width = 2,
                Height = 2,
                CanEquip = true,
                AllowedGearSlots = [6]
            },

            [7] = new()
            {
                ItemType = 7,
                Name = "Pendant",
                Width = 1,
                Height = 1,
                CanEquip = true,
                AllowedGearSlots = [9]
            },

            [8] = new()
            {
                ItemType = 8,
                Name = "Ring",
                Width = 1,
                Height = 1,
                CanEquip = true,
                AllowedGearSlots = [10, 11]
            },

            [9] = new()
            {
                ItemType = 9,
                Name = "Wings",
                Width = 4,
                Height = 3,
                CanEquip = true,
                AllowedGearSlots = [0]
            },

            [10] = new()
            {
                ItemType = 10,
                Name = "Pet",
                Width = 2,
                Height = 2,
                CanEquip = true,
                AllowedGearSlots = [1]
            },

            [20] = new()
            {
                ItemType = 20,
                Name = "VIP Emblem",
                Width = 1,
                Height = 1
            }
        };

    public static ItemDefinition Get(byte itemType)
    {
        return All.TryGetValue(itemType, out var definition)
            ? definition
            : All[0];
    }
}