using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root component for an enemy ship in the new combat loop.
/// Provides a lightweight registry for targeting/homing.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatEnemy : MonoBehaviour
{
    private static readonly List<CombatEnemy> ActiveEnemies = new List<CombatEnemy>(64);

    [SerializeField] private Transform aimPoint;
    private CombatShipHealth health;

    public bool IsAlive => health ? health.IsAlive : gameObject.activeInHierarchy;
    public Vector3 AimPoint => aimPoint ? aimPoint.position : transform.position + transform.forward * 0.2f;

    private void Awake()
    {
        health = GetComponent<CombatShipHealth>();
        if (!aimPoint)
        {
            var go = new GameObject("AimPoint");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0.1f);
            aimPoint = go.transform;
        }
    }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveEnemies.Remove(this);
    }

    public static CombatEnemy FindNearest(Vector3 position, float range)
    {
        float bestScore = range * range;
        CombatEnemy best = null;

        for (int i = ActiveEnemies.Count - 1; i >= 0; i--)
        {
            CombatEnemy enemy = ActiveEnemies[i];
            if (!enemy || !enemy.IsAlive)
            {
                ActiveEnemies.RemoveAt(i);
                continue;
            }

            float score = (enemy.transform.position - position).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }
}

