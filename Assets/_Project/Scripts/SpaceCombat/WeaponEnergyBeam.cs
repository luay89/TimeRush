using UnityEngine;

/// <summary>Continuous energy beam: hold to deal DPS to the nearest target.</summary>
public sealed class WeaponEnergyBeam : CombatWeapon
{
    [SerializeField] private float dps = 55f;
    [SerializeField] private float range = 45f;
    [SerializeField] private float beamWidth = 0.22f;

    private LineRenderer beam;
    private CombatEnemy cachedTarget;

    public override string DisplayName => "BEAM";

    public override void OnSelected(bool selected)
    {
        EnsureBeam();
        if (beam)
        {
            beam.enabled = selected && false;
        }

        cachedTarget = null;
    }

    public override void Tick(bool fireHeld)
    {
        EnsureBeam();
        if (!beam)
        {
            return;
        }

        if (!fireHeld)
        {
            beam.enabled = false;
            cachedTarget = null;
            return;
        }

        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.2f;
        cachedTarget = cachedTarget && cachedTarget.IsAlive ? cachedTarget : CombatEnemy.FindNearest(origin + Vector3.forward * 3f, range);

        Vector3 end = origin + Vector3.forward * range;
        if (cachedTarget)
        {
            end = cachedTarget.AimPoint;
            var health = cachedTarget.GetComponent<CombatShipHealth>();
            if (health)
            {
                float amount = Mathf.Max(0f, dps) * Time.deltaTime;
                health.TryApplyDamage(new CombatDamage(amount, CombatDamageKind.Energy), gameObject);
            }
        }

        beam.enabled = true;
        beam.SetPosition(0, origin);
        beam.SetPosition(1, end);
    }

    private void EnsureBeam()
    {
        if (beam)
        {
            return;
        }

        beam = gameObject.AddComponent<LineRenderer>();
        beam.positionCount = 2;
        beam.material = CombatVfxLibrary.BeamMaterial;
        beam.startWidth = beamWidth;
        beam.endWidth = beamWidth * 0.6f;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.numCapVertices = 6;
        beam.enabled = false;
    }
}

