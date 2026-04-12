namespace Tav.Models;

public readonly record struct AttackResolution
{
    public bool Hit { get; init; }
    public int D20 { get; init; }
    public int HitTotal { get; init; }
    public int Damage { get; init; }
}
