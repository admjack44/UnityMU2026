namespace MUServer.Core.World;

public sealed class Monster
{
    public int MonsterId { get; set; }

    public byte MonsterClass { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte MapId { get; set; }

    public byte X { get; set; }

    public byte Y { get; set; }

    public byte SpawnX { get; set; }

    public byte SpawnY { get; set; }

    public ushort MaxHp { get; set; }

    public ushort CurrentHp { get; set; }

    public bool IsAlive { get; set; } = true;

    public DateTime LastAttackTimeUtc { get; set; } = DateTime.MinValue;

    public void Respawn()
    {
        CurrentHp = MaxHp;
        IsAlive = true;

        X = SpawnX;
        Y = SpawnY;
    }
}