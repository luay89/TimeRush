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

    private void TryCollect(GameObject other)
    {
        if (consumed || !other || !other.CompareTag(targetTag))
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
        Destroy(gameObject);
    }
}
