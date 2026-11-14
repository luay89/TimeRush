public enum CombatDamageKind
{
    Energy = 0,
    Kinetic = 1,
    Explosive = 2,
}

public readonly struct CombatDamage
{
    public readonly float Amount;
    public readonly CombatDamageKind Kind;

    public CombatDamage(float amount, CombatDamageKind kind)
    {
        Amount = amount;
        Kind = kind;
    }
}

