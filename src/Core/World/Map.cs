namespace MUServer.Core.World;

public sealed class Map
{
    public byte Id { get; }
    public string Name { get; }
    public byte Width { get; } = 255;
    public byte Height { get; } = 255;

    public bool[,] WalkableGrid { get; }

    public Map(byte id, string name)
    {
        Id = id;
        Name = name;
        WalkableGrid = new bool[Width, Height];

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                WalkableGrid[x, y] = true;
            }
        }
    }

    public bool CanWalk(byte x, byte y)
    {
        if (x >= Width || y >= Height)
        {
            return false;
        }

        return WalkableGrid[x, y];
    }
}
