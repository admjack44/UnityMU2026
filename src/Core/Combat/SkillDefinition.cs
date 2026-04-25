namespace MUServer.Core.Combat;

public sealed class SkillDefinition
{
    public byte SkillId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ManaCost { get; init; }
    public int Damage { get; init; }
    public int CooldownMs { get; init; }
}