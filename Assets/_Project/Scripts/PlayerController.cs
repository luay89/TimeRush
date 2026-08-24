using UnityEngine;

/// <summary>
/// Handles direct, readable lane changes for the three-lane TimeRush track.
/// Movement remains transform-based to preserve the existing scene Rigidbody and collision setup.
/// </summary>
public class PlayerController : MonoBehaviour
{
    private static readonly float[] DefaultLanePositions = { -2.5f, 0f, 2.5f };

    [Header("Three-Lane Movement")]
    [SerializeField] private float[] lanePositions = { -2.5f, 0f, 2.5f };
    [SerializeField] private float moveSpeed = 18f;
    [SerializeField, Range(0.05f, 0.3f)] private float laneChangeDuration = 0.12f;
    [SerializeField] private int startingLane = 1;
    [SerializeField] private bool allowTouchSwipe = true;
    [SerializeField] private bool enableNearMissFeedback = true;
    [SerializeField, Range(3.5f, 5.2f)] private float nearMissWidth = 4.8f;
    [SerializeField, Range(1.1f, 2.2f)] private float nearMissDepth = 1.7f;

    private int currentLane;
    private float laneVelocity;
    private Transform visual;
    private Vector2 pointerDownPosition;
    private bool trackingPointer;

    public int CurrentLane => currentLane;
    public float LaneChangeProgress { get; private set; }

    private void Awake()
    {
        EnsureLaneConfiguration();
        currentLane = Mathf.Clamp(startingLane, 0, lanePositions.Length - 1);
        visual = transform.Find("Visual");
        EnsureNearMissDetector();

        var position = transform.position;
        position.x = lanePositions[currentLane];
        transform.position = position;
    }

    private void Update()
    {
        ReadKeyboardInput();
        ReadTouchInput();
        MoveTowardsTargetLane();
        UpdateVisualMotion();
    }

    private void ReadKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            ChangeLane(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            ChangeLane(1);
        }
    }

    private void ReadTouchInput()
    {
        if (!allowTouchSwipe || Input.touchCount == 0)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            pointerDownPosition = touch.position;
            trackingPointer = true;
            return;
        }

        if (!trackingPointer || touch.phase != TouchPhase.Ended)
        {
            return;
        }

        trackingPointer = false;
        Vector2 delta = touch.position - pointerDownPosition;
        float threshold = Mathf.Max(32f, Screen.width * 0.08f);

        if (Mathf.Abs(delta.x) >= threshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            ChangeLane(delta.x > 0f ? 1 : -1);
        }
    }

    private void ChangeLane(int direction)
    {
        int requestedLane = Mathf.Clamp(currentLane + direction, 0, lanePositions.Length - 1);

        if (requestedLane == currentLane)
        {
            return;
        }

        currentLane = requestedLane;
        LaneChangeProgress = 0f;
    }

    private void MoveTowardsTargetLane()
    {
        float targetX = lanePositions[currentLane];
        Vector3 position = transform.position;
        float previousDistance = Mathf.Abs(targetX - position.x);
        float smoothTime = Mathf.Max(0.05f, laneChangeDuration);

        position.x = Mathf.SmoothDamp(position.x, targetX, ref laneVelocity, smoothTime, moveSpeed * 2f, Time.deltaTime);
        position.x = Mathf.Clamp(position.x, lanePositions[0], lanePositions[lanePositions.Length - 1]);
        transform.position = position;

        float currentDistance = Mathf.Abs(targetX - position.x);
        LaneChangeProgress = previousDistance <= 0.001f
            ? 1f
            : Mathf.Clamp01(1f - (currentDistance / previousDistance));
    }

    private void UpdateVisualMotion()
    {
        if (!visual)
        {
            return;
        }

        float tilt = Mathf.Clamp(-laneVelocity * 1.35f, -14f, 14f);
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, tilt);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, 14f * Time.deltaTime);
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
