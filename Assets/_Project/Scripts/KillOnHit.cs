using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class KillOnHit : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Log a warning if the obstacle does not have a Rigidbody.")]
    [SerializeField] private bool requireRigidbody = true;
    [Tooltip("Toggle verbose collision diagnostics in the Console.")]
    [SerializeField] private bool debugLogs = false;
    private Collider cachedCollider;
    private Rigidbody cachedRigidbody;
    private GameController cachedController;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        cachedRigidbody = GetComponent<Rigidbody>();

        if (!cachedCollider)
        {
            Debug.LogError($"KillOnHit on {name} is missing a Collider component. Collisions will not be detected.");
        }

        if (requireRigidbody && !cachedRigidbody)
        {
            Debug.LogWarning($"KillOnHit on {name} has no Rigidbody; ensure the obstacle is moved by physics or disable 'Require Rigidbody'.");
        }

        CacheController();
    }

    private void CacheController()
    {
        cachedController = GameController.Instance;

        if (!cachedController)
        {
            cachedController = FindObjectOfType<GameController>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKill(collision?.gameObject, "collision");
    }

    private void OnTriggerEnter(Collider other)
    {
        TryKill(other?.gameObject, "trigger");
    }

    private void TryKill(GameObject other, string channel)
    {
        if (!other)
        {
            DebugMessage($"{channel}: ignored because other is null");
            return;
        }

        DebugMessage($"{channel}: contact with {other.name} (tag: {other.tag})");

        if (!other.CompareTag(targetTag))
        {
            DebugMessage($"{channel}: tag mismatch, expected '{targetTag}'");
            return;
        }

        DebugMessage($"{channel}: tag matched '{targetTag}'");

        var controller = cachedController;

        if (!controller)
        {
            CacheController();
            controller = cachedController;
        }

        if (!controller)
        {
            Debug.LogError($"KillOnHit: GameController missing when processing {channel} on {name}. Cannot trigger GameOver.");
            return;
        }

        if (controller.IsPlayerInvulnerable)
        {
            DebugMessage($"{channel}: player invulnerable, ignoring hit");
            return;
        }

        var nearMissState = GetComponent<NearMissState>();

        if (!nearMissState)
        {
            nearMissState = gameObject.AddComponent<NearMissState>();
        }

        nearMissState.MarkCollision();

        DebugMessage($"{channel}: using controller {controller.name}");
        GameFeedbackSignals.RaiseObstacleCollision(new ObstacleCollisionFeedback(transform.position));
        DebugMessage($"{channel}: invoking TriggerGameOver");
        controller.TriggerGameOver(this);
    }

    private void DebugMessage(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[KillOnHit:{name}] {message}", this);
    }
}
