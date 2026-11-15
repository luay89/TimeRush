using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hosts the player's weapon set (six weapons) and handles switching + upgrade levels.
///
/// Note: This is intentionally runtime-configured (no prefab work needed) so combat can evolve
/// quickly while preserving the original project's assets and scene flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatWeaponController : MonoBehaviour
{
    public event Action<int, CombatWeapon> WeaponChanged;

    private readonly List<CombatWeapon> weapons = new List<CombatWeapon>(8);
    private CombatPlayerController player;
    // -1 = nothing selected yet, so Awake's SelectIndex(0) really selects (and binds) the first weapon.
    private int index = -1;
    private bool fireHeld;

    public int ActiveIndex => index;
    public int WeaponCount => weapons.Count;
    public CombatWeapon ActiveWeapon => index >= 0 && index < weapons.Count ? weapons[index] : null;

    public void SetPlayer(CombatPlayerController controller)
    {
        player = controller;
    }

    public void SetFireHeld(bool held)
    {
        fireHeld = held;
    }

    private void Awake()
    {
        if (!player)
        {
            player = GetComponent<CombatPlayerController>();
        }

        EnsureWeaponSet();
        SelectIndex(0);
    }

    private void Update()
    {
        if (!GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
        {
            fireHeld = false;
        }

        var weapon = ActiveWeapon;
        if (weapon)
        {
            weapon.Tick(fireHeld);
        }
    }

    public void SelectNext()
    {
        if (weapons.Count == 0)
        {
            return;
        }

        SelectIndex((index + 1) % weapons.Count);
    }

    public void SelectPrevious()
    {
        if (weapons.Count == 0)
        {
            return;
        }

        SelectIndex((index - 1 + weapons.Count) % weapons.Count);
    }

    private void SelectIndex(int next)
    {
        next = Mathf.Clamp(next, 0, Mathf.Max(0, weapons.Count - 1));
        if (next == index && ActiveWeapon)
        {
            return;
        }

        if (ActiveWeapon)
        {
            ActiveWeapon.OnSelected(false);
        }

        index = next;
        var weapon = ActiveWeapon;
        if (weapon)
        {
            weapon.Bind(player);
            weapon.OnSelected(true);
        }

        WeaponChanged?.Invoke(index, weapon);
    }

    private void EnsureWeaponSet()
    {
        weapons.Clear();

        AddWeapon<WeaponLaserCannon>();
        AddWeapon<WeaponPlasmaCannon>();
        AddWeapon<WeaponHomingMissiles>();
        AddWeapon<WeaponHeavyRailgun>();
        AddWeapon<WeaponSpreadShot>();
        AddWeapon<WeaponEnergyBeam>();
    }

    private void AddWeapon<T>() where T : CombatWeapon
    {
        var weapon = GetComponent<T>();
        if (!weapon)
        {
            weapon = gameObject.AddComponent<T>();
        }

        weapons.Add(weapon);
    }
}
