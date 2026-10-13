using UnityEngine;

/// <summary>
/// A collectible dropped by PowerUpSpawner. On contact with the player it grants temporary
/// invulnerability (a "shield") by reusing GameController's existing invulnerability timer --
/// the same one KillOnHit already checks -- so no other system needs to change.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private float shieldDurationSeconds = 3f;

    private bool consumed;

    public void Configure(float durationSeconds)
    {
        shieldDurationSeconds = Mathf.Max(0.5f, durationSeconds);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other ? other.gameObject : null);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCollect(collision != null ? collision.gameObject : null);
    }

    private static PlayerController cachedPlayer;

    // The trigger alone missed pickups that visibly touched the ship: the pickup falls at a
    // single depth while the player may sit one depth step in front or behind, and from the chase
    // camera those look like the same spot. Collect generously whenever the shield passes the ship
    // in the same lane, within one depth step.
    private void Update()
    {
        if (consumed)
        {
            return;
        }

        if (!cachedPlayer)
        {
            cachedPlayer = FindObjectOfType<PlayerController>();
            if (!cachedPlayer)
            {
                return;
            }
        }

        Vector3 delta = transform.position - cachedPlayer.transform.position;
        if (Mathf.Abs(delta.x) < 1.4f && delta.y > -0.9f && delta.y < 1.6f && Mathf.Abs(delta.z) < 2f)
        {
            Collect();
        }
    }

    private void TryCollect(GameObject other)
    {
        if (consumed || !other || !(other.CompareTag(targetTag) || other.GetComponentInParent<PlayerController>()))
        {
            return;
        }

        Collect();
    }

    private void Collect()
    {
        if (consumed)
        {
            return;
        }

        GameController controller = GameController.Instance;
        if (!controller)
        {
            controller = FindObjectOfType<GameController>();
        }

        if (!controller)
        {
            return;
        }

        consumed = true;
        controller.GrantInvulnerability(shieldDurationSeconds);
        FloatingScorePopup.Spawn(transform.position, "SHIELD", new Color(0.4f, 0.8f, 1f, 1f));
        Destroy(gameObject);
    }
}
