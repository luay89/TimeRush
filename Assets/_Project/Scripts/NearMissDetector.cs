using UnityEngine;

/// <summary>
/// Awards a near-miss bonus when obstacles pass through an expanded trigger without colliding.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NearMissDetector : MonoBehaviour
{
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private int bonusPoints = 5;

    private Collider cachedTrigger;
    private Collider playerCollisionCollider;
    private PlayerController playerController;
    private GameController cachedController;

    private void Awake()
    {
        cachedTrigger = GetComponent<Collider>();
        playerCollisionCollider = transform.parent ? transform.parent.GetComponent<Collider>() : null;
        playerController = GetComponentInParent<PlayerController>();

        if (cachedTrigger && !cachedTrigger.isTrigger)
        {
            cachedTrigger.isTrigger = true;
        }
    }

    private void Start()
    {
        CacheController();
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = EnsureController();

        if (!enabled || controller == null || controller.IsGameOver)
        {
            return;
        }

        if (!IsEligibleCollider(other) || IsDirectCollision(other) || IsSameLane(other))
        {
            return;
        }

        if (!other.TryGetComponent<KillOnHit>(out _))
        {
            return;
        }

        var state = other.GetComponent<NearMissState>();

        if (!state)
        {
            state = other.gameObject.AddComponent<NearMissState>();
        }

        if (!state.TryAward())
        {
            return;
        }

        controller.AddScore(bonusPoints, "NearMiss");
    }

    private bool IsDirectCollision(Collider other)
    {
        if (!playerCollisionCollider || !other)
        {
            return false;
        }

        return Physics.ComputePenetration(
            playerCollisionCollider,
            playerCollisionCollider.transform.position,
            playerCollisionCollider.transform.rotation,
            other,
            other.transform.position,
            other.transform.rotation,
            out _,
            out _);
    }

    private bool IsSameLane(Collider other)
    {
        if (!playerController || !other.TryGetComponent<ObstacleLaneMarker>(out var marker))
        {
            return false;
        }

        return marker.LaneIndex == playerController.CurrentLane;
    }

    private bool IsEligibleCollider(Collider other)
    {
        if (!other)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(groundTag) && other.CompareTag(groundTag))
        {
            return false;
        }

        if (!other.CompareTag(obstacleTag))
        {
            return false;
        }

        if (obstacleLayers.value != ~0 && obstacleLayers.value != 0)
        {
            int otherLayerMask = 1 << other.gameObject.layer;

            if ((obstacleLayers.value & otherLayerMask) == 0)
            {
                return false;
            }
        }

        return true;
    }

    private void CacheController()
    {
        cachedController = GameController.Instance;

        if (!cachedController)
        {
            cachedController = FindObjectOfType<GameController>();
        }

        if (!cachedController)
        {
            Debug.LogError("NearMissDetector: GameController not found. Near-miss bonuses disabled.", this);
        }
    }

    private GameController EnsureController()
    {
        if (cachedController)
        {
            return cachedController;
        }

        CacheController();
        return cachedController;
    }
}
