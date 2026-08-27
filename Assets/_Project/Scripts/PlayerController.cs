using UnityEngine;

/// <summary>
/// Keeps TimeRush readable: the player switches between three fixed lanes while
/// using a short, clamped depth range to dodge fair forward/back obstacle layouts.
/// </summary>
public class PlayerController : MonoBehaviour
{
    private static readonly float[] DefaultLanePositions = { -2.5f, 0f, 2.5f };

    [Header("Three-Lane Movement")]
    [SerializeField] private TrackLayoutConfig trackLayoutConfig;
    [SerializeField] private float[] lanePositions = { -2.5f, 0f, 2.5f };
    [SerializeField] private float laneMoveSpeed = 18f;
    [SerializeField, Range(0.05f, 0.3f)] private float laneChangeDuration = 0.12f;
    [SerializeField] private int startingLane = 1;

    [Header("Safe Forward / Back Movement")]
    [SerializeField] private float forwardBackSpeed = 7.5f;
    [SerializeField, Range(0.05f, 0.3f)] private float depthChangeDuration = 0.14f;
    [SerializeField, Range(1.25f, 3f)] private float safeDepthRange = 2f;

    [Header("Feedback")]
    [SerializeField] private bool enableNearMissFeedback = true;
    [SerializeField, Range(3.5f, 5.2f)] private float nearMissWidth = 4.8f;
    [SerializeField, Range(1.1f, 2.2f)] private float nearMissDepth = 1.7f;

    private int currentLane;
    private float laneVelocity;
    private float depthVelocity;
    private float trackCenterZ;
    private float targetDepthZ;
    private Transform visual;
    private Vector3 baseVisualScale = Vector3.one;
    private float movementPulse;

    public int CurrentLane => currentLane;
    public float CurrentTrackDepth => transform.position.z;
    public float MinimumSafeDepth => trackCenterZ - safeDepthRange;
    public float MaximumSafeDepth => trackCenterZ + safeDepthRange;
    public float LaneChangeProgress { get; private set; }
    public bool IsLaneTransitioning => Mathf.Abs(lanePositions[currentLane] - transform.position.x) > 0.03f;

    public FairnessPlayerState GetFairnessState()
    {
        return new FairnessPlayerState(
            transform.position.x,
            transform.position.z,
            transform.position.y,
            MinimumSafeDepth,
            MaximumSafeDepth,
            laneMoveSpeed,
            laneChangeDuration,
            forwardBackSpeed,
            depthChangeDuration);
    }

    private void Awake()
    {
        EnsureLaneConfiguration();
        currentLane = Mathf.Clamp(startingLane, 0, lanePositions.Length - 1);
        trackCenterZ = transform.position.z;
        targetDepthZ = trackCenterZ;
        visual = transform.Find("Visual");

        if (visual)
        {
            baseVisualScale = visual.localScale;
        }

        EnsureNearMissDetector();

        var position = transform.position;
        position.x = lanePositions[currentLane];
        position.z = Mathf.Clamp(position.z, MinimumSafeDepth, MaximumSafeDepth);
        transform.position = position;
    }

    private void Update()
    {
        MoveTowardsTargets();
        UpdateVisualMotion();
    }

    public bool SubmitIntent(PlayerIntent intent)
    {
        bool laneAccepted = !intent.HasLaneStep || TryChangeLane(intent.LaneStep);

        if (intent.HasDepthAxis)
        {
            AdjustDepthTarget(intent.DepthAxis * forwardBackSpeed * Time.deltaTime);
        }

        if (intent.HasDepthStep)
        {
            ShiftDepth(intent.DepthStep);
        }

        return laneAccepted;
    }

    private bool TryChangeLane(int direction)
    {
        if (IsLaneTransitioning)
        {
            return false;
        }

        int requestedLane = PlayerIntent.ClampLaneIndex(currentLane, direction, lanePositions.Length);

        if (requestedLane == currentLane)
        {
            return false;
        }

        currentLane = requestedLane;
        LaneChangeProgress = 0f;
        movementPulse = 1f;
        return true;
    }

    private void ShiftDepth(float direction)
    {
        float step = Mathf.Min(1.5f, safeDepthRange);
        targetDepthZ = Mathf.Clamp(targetDepthZ + direction * step, MinimumSafeDepth, MaximumSafeDepth);
        movementPulse = Mathf.Max(movementPulse, 0.65f);
    }

    private void AdjustDepthTarget(float amount)
    {
        targetDepthZ = Mathf.Clamp(targetDepthZ + amount, MinimumSafeDepth, MaximumSafeDepth);

        if (Mathf.Abs(amount) > 0.01f)
        {
            movementPulse = Mathf.Max(movementPulse, 0.35f);
        }
    }

    private void MoveTowardsTargets()
    {
        Vector3 position = transform.position;
        float targetX = lanePositions[currentLane];
        float laneSmoothTime = Mathf.Max(0.05f, laneChangeDuration);
        float depthSmoothTime = Mathf.Max(0.05f, depthChangeDuration);

        position.x = Mathf.SmoothDamp(position.x, targetX, ref laneVelocity, laneSmoothTime, laneMoveSpeed * 2f, Time.deltaTime);
        position.x = Mathf.Clamp(position.x, lanePositions[0], lanePositions[lanePositions.Length - 1]);

        targetDepthZ = Mathf.Clamp(targetDepthZ, MinimumSafeDepth, MaximumSafeDepth);
        position.z = Mathf.SmoothDamp(position.z, targetDepthZ, ref depthVelocity, depthSmoothTime, forwardBackSpeed, Time.deltaTime);
        position.z = Mathf.Clamp(position.z, MinimumSafeDepth, MaximumSafeDepth);
        transform.position = position;

        float laneDistance = Mathf.Abs(targetX - position.x);
        LaneChangeProgress = Mathf.Clamp01(1f - laneDistance / 2.5f);
    }

    private void UpdateVisualMotion()
    {
        if (!visual)
        {
            return;
        }

        float laneTilt = Mathf.Clamp(-laneVelocity * 1.35f, -14f, 14f);
        float depthTilt = Mathf.Clamp(depthVelocity * 0.9f, -8f, 8f);
        Quaternion targetRotation = Quaternion.Euler(depthTilt, 0f, laneTilt);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, 14f * Time.deltaTime);

        movementPulse = Mathf.MoveTowards(movementPulse, 0f, 6f * Time.deltaTime);
        float scale = 1f + movementPulse * 0.075f;
        visual.localScale = Vector3.Lerp(visual.localScale, baseVisualScale * scale, 16f * Time.deltaTime);
    }

    private void EnsureNearMissDetector()
    {
        if (!enableNearMissFeedback || GetComponentInChildren<NearMissDetector>())
        {
            return;
        }

        var detectorObject = new GameObject("NearMissZone");
        detectorObject.transform.SetParent(transform, false);

        var trigger = detectorObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(nearMissWidth, 1f, nearMissDepth);

        detectorObject.AddComponent<NearMissDetector>();
    }

    private void EnsureLaneConfiguration()
    {
        if (trackLayoutConfig)
        {
            if (!trackLayoutConfig.IsValid(out string message))
            {
                Debug.LogError($"PlayerController: {message}", this);
            }
            else
            {
                lanePositions = trackLayoutConfig.CopyLanePositions();
                safeDepthRange = trackLayoutConfig.SafeDepthRange;
            }
        }

        if (lanePositions == null || lanePositions.Length != 3)
        {
            lanePositions = (float[])DefaultLanePositions.Clone();
            Debug.LogWarning("PlayerController: lanePositions reset to the required three lanes.", this);
            return;
        }

        lanePositions[0] = -2.5f;
        lanePositions[1] = 0f;
        lanePositions[2] = 2.5f;
    }
}
