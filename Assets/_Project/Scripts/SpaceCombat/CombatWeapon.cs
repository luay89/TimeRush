using UnityEngine;

/// <summary>Base class for all combat weapons (player and enemy).</summary>
public abstract class CombatWeapon : MonoBehaviour
{
    protected CombatPlayerController player;
    protected CombatTeamMember team;
    protected Transform muzzle;

    public abstract string DisplayName { get; }
    public virtual bool SupportsHoldFire => true;

    public void Bind(CombatPlayerController playerController)
    {
        player = playerController;
        team = GetComponent<CombatTeamMember>();
        if (!muzzle)
        {
            // A stable point slightly ahead of the ship hull.
            var go = new GameObject("Muzzle");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 1.2f);
            muzzle = go.transform;
        }
    }

    public abstract void Tick(bool fireHeld);

    public virtual void OnSelected(bool selected) { }

    protected CombatTeam ResolveTeam() => team ? team.Team : CombatTeam.Enemy;
}

