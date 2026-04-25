namespace MUServer.Core.Models;

public sealed class Character
{
    public const byte DefaultInventoryWidth = 8;
    public const byte DefaultInventoryHeight = 20;
    public const int DefaultInventorySlots = DefaultInventoryWidth * DefaultInventoryHeight;
    public const int DefaultGearSlots = 12;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte Class { get; set; }

    public ushort Level { get; set; } = 1;

    public uint Experience { get; set; }

    public byte MapId { get; set; } = 0;

    public byte X { get; set; } = 125;

    public byte Y { get; set; } = 125;

    public ushort Strength { get; set; }

    public ushort Agility { get; set; }

    public ushort Vitality { get; set; }

    public ushort Energy { get; set; }

    public ushort Leadership { get; set; }

    public ushort Life { get; set; }

    public ushort Mana { get; set; }

    public ushort MaxLife { get; set; }

    public ushort CurrentLife { get; set; }

    public byte InventoryWidth { get; set; } = DefaultInventoryWidth;

    public byte InventoryHeight { get; set; } = DefaultInventoryHeight;

    public byte[] Inventory { get; set; } = new byte[DefaultInventorySlots];

    public byte[] Gear { get; set; } = new byte[DefaultGearSlots];

    public uint Zen { get; set; }

    public uint GoblinPoints { get; set; }

    public uint Ruud { get; set; }

    public uint WCoins { get; set; }

    public bool IsDead => CurrentLife == 0;

    public int InventorySlots => InventoryWidth * InventoryHeight;

    public void EnsureInventorySize()
    {
        var expectedSlots = InventorySlots;

        if (Inventory.Length == expectedSlots)
        {
            return;
        }

        var resizedInventory = new byte[expectedSlots];

        Array.Copy(
            Inventory,
            resizedInventory,
            Math.Min(Inventory.Length, resizedInventory.Length));

        Inventory = resizedInventory;
    }

    public void EnsureGearSize()
    {
        if (Gear.Length == DefaultGearSlots)
        {
            return;
        }

        var resizedGear = new byte[DefaultGearSlots];

        Array.Copy(
            Gear,
            resizedGear,
            Math.Min(Gear.Length, resizedGear.Length));

        Gear = resizedGear;
    }

    public void RestoreLife()
    {
        CurrentLife = MaxLife;
        Life = MaxLife;
    }

    public void SetLife(ushort value)
    {
        var finalValue = Math.Min(value, MaxLife);

        CurrentLife = finalValue;
        Life = finalValue;
    }

    public void Kill()
    {
        CurrentLife = 0;
        Life = 0;
    }
}