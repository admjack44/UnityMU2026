namespace MUServer.Core.Combat;

public sealed class DummyTarget
{
    public int Id { get; set; } = 1;

    public string Name { get; set; } = "Training Dummy";

    public int MaxHp { get; set; } = 200;

    public int CurrentHp { get; set; } = 200;

    public bool IsAlive => CurrentHp > 0;
}