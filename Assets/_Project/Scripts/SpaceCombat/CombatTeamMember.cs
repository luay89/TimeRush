using UnityEngine;

public enum CombatTeam
{
    Player = 0,
    Enemy = 1,
}

/// <summary>Lightweight team marker so projectiles and AI can avoid friendly fire when desired.</summary>
[DisallowMultipleComponent]
public sealed class CombatTeamMember : MonoBehaviour
{
    [SerializeField] private CombatTeam team = CombatTeam.Enemy;
    public CombatTeam Team
    {
        get => team;
        set => team = value;
    }
}

