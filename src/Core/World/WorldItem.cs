namespace MUServer.Core.World;

public sealed class WorldItem
{
    public int ItemId { get; set; }

    public byte ItemType { get; set; }

    public byte MapId { get; set; }

    public byte X { get; set; }

    public byte Y { get; set; }
}