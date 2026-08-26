using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    private static readonly float[] RequiredLanePositions = { -2.5f, 0f, 2.5f };
    private static readonly float[] DefaultDepthOffsets = { -1.5f, 0f, 1.5f };
    private const int MaxLaneSelectionAge = 3;

    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private float fallbackInterval = 1.5f;
    [SerializeField] private float fallbackSpeed = 6f;
    [SerializeField] private float spawnHeight = 13.5f;
    [SerializeField] private bool ensureKillOnHit = true;
    [SerializeField] private float autoDestroyLifetime = 10f;
    [Header("Lane Spawning")]
    [SerializeField] private float[] lanePositions = new[] { -2.5f, 0f, 2.5f };
    [SerializeField] private float minLaneGap = 7f;
    [SerializeField] private bool preventSameLaneTwice = true;
    [Header("Depth Variety")]
    [SerializeField, Tooltip("Local road-depth offsets used to make individual hazards readable and varied.")]
    private float[] depthOffsets = { -1.5f, 0f, 1.5f };
    [SerializeField, Range(0.75f, 2f)] private float minDepthSeparation = 1.25f;
    [SerializeField] private bool preventSameDepthTwice = true;
    [Header("Fairness")]
    [SerializeField] private bool enableFairnessRules = true;
    [SerializeField, Tooltip("Maximum time window where L+R spawns are treated as lock candidates.")]
    private float lockWindowSeconds = 1.8f;
    [SerializeField, Tooltip("Minimum reaction time between spawns that block different lanes.")]
    private float reactionTimeSeconds = 1f;
    [SerializeField, Tooltip("Log lane picks and fairness rejections.")]
    private bool debugLaneDecisions;
    [Header("Occupancy")]
    [SerializeField, Tooltip("Distance above the player within which a lane counts as blocked.")]
    private float dangerRange = 4f;
    [SerializeField, Tooltip("Optional reference to the player transform; defaults to world Y=0 when null.")]
    private Transform playerReference;

    private float timer;
    private bool prefabMissingLogged;
    private float[] lastLaneY;
    private float[] lastLaneSpeed;
    private float[] lastLaneTime;
    private int[] laneSelectionAge;
    private int lastLaneIndex = -1;
    private int lastDepthIndex = -1;
    private int previousLaneIndex = -1;
    private int secondPreviousLaneIndex = -1;
    private float previousSpawnTime = float.NegativeInfinity;
    private float secondPreviousSpawnTime = float.NegativeInfinity;
    private float lastSpawnGlobalTime = float.NegativeInfinity;
    private List<ObstacleLaneMarker>[] laneOccupants;
    private bool configInvalid;

    private void Awake()
    {
        NormalizeLanePositions();
        NormalizeDepthOffsets();
        minLaneGap = Mathf.Max(2f, minLaneGap);
        minDepthSeparation = Mathf.Max(0.5f, minDepthSeparation);
        lockWindowSeconds = Mathf.Max(0.2f, lockWindowSeconds);
        reactionTimeSeconds = Mathf.Max(0.2f, reactionTimeSeconds);
        dangerRange = Mathf.Max(0.5f, dangerRange);

        if (!ValidateLanes())
        {
            configInvalid = true;
            enabled = false;
            return;
        }

        if (!playerReference)
        {
            var playerController = FindObjectOfType<PlayerController>();
            playerReference = playerController ? playerController.transform : null;
        }

        EnsureLaneArrays();
    }

    private void Update()
    {
        if (configInvalid)
        {
            return;
        }

        if (!obstaclePrefab)
        {
            if (!prefabMissingLogged)
            {
                Debug.LogError("ObstacleSpawner: Missing obstacle prefab reference.", this);
                prefabMissingLogged = true;
            }
            enabled = false;
            return;
        }

        prefabMissingLogged = false;

        timer += Time.deltaTime;

        var gc = GameController.Instance;

        if (gc && gc.IsGameOver)
        {
            return;
        }

        bool controllerAvailable = gc != null;

        float targetInterval = controllerAvailable ? gc.GetSpawnInterval() : fallbackInterval;
        float interval = Mathf.Max(0.2f, targetInterval);
        timer = Mathf.Min(timer, interval);

        if (timer >= interval)
        {
            timer -= interval;
            float speed = controllerAvailable ? gc.GetObstacleSpeed() : fallbackSpeed;
            TrySpawnObstacle(speed);
        }
    }

    private void EnsureKillOnHit(GameObject obstacleInstance)
    {
        if (!ensureKillOnHit || !obstacleInstance)
        {
            return;
        }

        if (!obstacleInstance.TryGetComponent<KillOnHit>(out _))
        {
            obstacleInstance.AddComponent<KillOnHit>();
        }
    }

    private bool TrySpawnObstacle(float speed)
    {
        if (!TrySelectLane(speed, out int laneIndex))
        {
            return false;
        }

        if (!TrySelectDepth(laneIndex, out int depthIndex, out float spawnZ))
        {
            return false;
        }

        float laneX = lanePositions[laneIndex];
        Vector3 spawnPos = new Vector3(laneX, spawnHeight, spawnZ);
        var instance = Instantiate(obstaclePrefab, spawnPos, Quaternion.identity);
        EnsureKillOnHit(instance);
        EnsureAutoDestroy(instance);
        AttachLaneMarker(instance, laneIndex);
        ApplySpeed(instance, speed);
        CommitLaneSpawn(laneIndex, depthIndex, speed);
        DebugLane($"spawn lane {laneIndex}, depth {depthIndex} | {FormatLaneStates()}" );
        return true;
    }

    private bool TrySelectDepth(int laneIndex, out int depthIndex, out float spawnZ)
    {
        var validDepths = new List<int>(depthOffsets.Length);
        float variation = GameController.Instance ? GameController.Instance.GetDepthVariation() : 1f;

        for (int i = 0; i < depthOffsets.Length; i++)
        {
            if (preventSameDepthTwice && depthOffsets.Length > 1 && i == lastDepthIndex)
            {
                continue;
            }

            float candidateZ = transform.position.z + depthOffsets[i] * variation;

            if (IsDepthOccupied(laneIndex, candidateZ))
            {
                continue;
            }

            validDepths.Add(i);
        }

        if (validDepths.Count == 0)
        {
            depthIndex = -1;
            spawnZ = transform.position.z;
            DebugLane("no valid depth; skipping spawn");
            return false;
        }

        depthIndex = validDepths[Random.Range(0, validDepths.Count)];
        spawnZ = transform.position.z + depthOffsets[depthIndex] * variation;
        return true;
    }

    private bool IsDepthOccupied(int laneIndex, float candidateZ)
    {
        if (laneOccupants == null || laneIndex < 0 || laneIndex >= laneOccupants.Length)
        {
            return false;
        }

        var list = laneOccupants[laneIndex];
        CleanupLaneList(list);

        if (list == null)
        {
            return false;
        }

        float playerY = playerReference ? playerReference.position.y : 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var marker = list[i];

            if (!marker || marker.CurrentHeight < playerY - 0.5f)
            {
                continue;
            }

            if (Mathf.Abs(marker.CurrentDepth - candidateZ) < minDepthSeparation)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplySpeed(GameObject obstacleInstance, float speed)
    {
        if (!obstacleInstance)
        {
            return;
        }

        if (obstacleInstance.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = Vector3.down * speed;
            return;
        }

        var mover = obstacleInstance.GetComponent<ObstacleMover>();

        if (!mover)
        {
            mover = obstacleInstance.AddComponent<ObstacleMover>();
        }

        mover.SetSpeed(speed);
    }

    private bool TrySelectLane(float spawnSpeed, out int laneIndex)
    {
        EnsureLaneArrays();

        int laneCount = lanePositions.Length;
        float now = Time.time;
        var validLanes = new List<int>(laneCount);

        for (int i = 0; i < laneCount; i++)
        {
            int candidate = i;

            if (preventSameLaneTwice && candidate == lastLaneIndex)
            {
                DebugLane($"reject lane {candidate} (repeat)");
                continue;
            }

            if (IsLaneTooClose(candidate))
            {
                DebugLane($"reject lane {candidate} (min gap)");
                continue;
            }

            if (IsLaneBlocked(candidate))
            {
                DebugLane($"reject lane {candidate} (blocked)");
                continue;
            }

            if (enableFairnessRules && laneCount >= 3)
            {
                if (ViolatesReactionTime(candidate, now, spawnSpeed))
                {
                    DebugLane($"reject lane {candidate} (reaction)");
                    continue;
                }

                if (WouldExhaustSafeLanes(candidate, spawnSpeed))
                {
                    DebugLane($"reject lane {candidate} (all lanes would block)");
                    continue;
                }

                if (ViolatesLockRule(candidate, now))
                {
                    DebugLane($"reject lane {candidate} (lock rule)");
                    continue;
                }
            }

            validLanes.Add(candidate);
        }

        if (validLanes.Count == 0)
        {
            laneIndex = -1;
            DebugLane("no valid lane; skipping spawn");
            return false;
        }

        // Keep lane selection random, but prevent one lane from remaining the
        // only consistently safe option when other valid lanes are available.
        var overdueLanes = new List<int>(validLanes.Count);
        foreach (int candidate in validLanes)
        {
            if (laneSelectionAge != null && laneSelectionAge[candidate] >= MaxLaneSelectionAge)
            {
                overdueLanes.Add(candidate);
            }
        }

        var selectionPool = overdueLanes.Count > 0 ? overdueLanes : validLanes;
        laneIndex = selectionPool[Random.Range(0, selectionPool.Count)];
        DebugLane($"accept lane {laneIndex} | overdue {overdueLanes.Count}/{validLanes.Count}");
        return true;
    }

    private bool IsLaneTooClose(int laneIndex)
    {
        if (lastLaneY == null || laneIndex < 0 || laneIndex >= lastLaneY.Length)
        {
            return false;
        }

        if (!float.IsFinite(lastLaneY[laneIndex]))
        {
            return false;
        }

        float elapsed = Time.time - lastLaneTime[laneIndex];
        float estimatedY = lastLaneY[laneIndex] - lastLaneSpeed[laneIndex] * elapsed;
        float allowedY = spawnHeight - minLaneGap;
        return estimatedY > allowedY;
    }

    private void CommitLaneSpawn(int laneIndex, int depthIndex, float speed)
    {
        if (lastLaneY == null || laneIndex < 0 || laneIndex >= lastLaneY.Length)
        {
            return;
        }

        secondPreviousLaneIndex = previousLaneIndex;
        secondPreviousSpawnTime = previousSpawnTime;
        previousLaneIndex = laneIndex;
        previousSpawnTime = Time.time;
        lastSpawnGlobalTime = previousSpawnTime;
        lastLaneIndex = laneIndex;
        lastDepthIndex = depthIndex;

        if (laneSelectionAge != null)
        {
            for (int i = 0; i < laneSelectionAge.Length; i++)
            {
                laneSelectionAge[i] = Mathf.Min(MaxLaneSelectionAge, laneSelectionAge[i] + 1);
            }

            laneSelectionAge[laneIndex] = 0;
        }

        lastLaneY[laneIndex] = spawnHeight;
        lastLaneSpeed[laneIndex] = speed;
        lastLaneTime[laneIndex] = Time.time;
    }

    private void EnsureLaneArrays()
    {
        int count = lanePositions.Length;

        if (lastLaneY != null && lastLaneY.Length == count)
        {
            EnsureLaneOccupantLists(count);
            return;
        }

        lastLaneY = new float[count];
        lastLaneSpeed = new float[count];
        lastLaneTime = new float[count];
        laneSelectionAge = new int[count];
        laneOccupants = new List<ObstacleLaneMarker>[count];

        for (int i = 0; i < count; i++)
        {
            lastLaneY[i] = float.NegativeInfinity;
            lastLaneSpeed[i] = 0f;
            lastLaneTime[i] = 0f;
            laneOccupants[i] = new List<ObstacleLaneMarker>();
        }

        EnsureLaneOccupantLists(count);
    }

    private void EnsureLaneOccupantLists(int count)
    {
        if (laneOccupants == null)
        {
            laneOccupants = new List<ObstacleLaneMarker>[count];
            for (int i = 0; i < count; i++)
            {
                laneOccupants[i] = new List<ObstacleLaneMarker>();
            }
            return;
        }

        if (laneOccupants.Length != count)
        {
            var newArrays = new List<ObstacleLaneMarker>[count];
            for (int i = 0; i < count; i++)
            {
                newArrays[i] = i < laneOccupants.Length && laneOccupants[i] != null
                    ? laneOccupants[i]
                    : new List<ObstacleLaneMarker>();
                CleanupLaneList(newArrays[i]);
            }

            laneOccupants = newArrays;
            return;
        }

        for (int i = 0; i < laneOccupants.Length; i++)
        {
            if (laneOccupants[i] == null)
            {
                laneOccupants[i] = new List<ObstacleLaneMarker>();
            }
            else
            {
                CleanupLaneList(laneOccupants[i]);
            }
        }
    }

    private void CleanupLaneList(List<ObstacleLaneMarker> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i])
            {
                list.RemoveAt(i);
            }
        }
    }

    private void EnsureAutoDestroy(GameObject instance)
    {
        if (!instance.TryGetComponent<AutoDestroy>(out var autoDestroy))
        {
            autoDestroy = instance.AddComponent<AutoDestroy>();
        }

        autoDestroy.SetLifetime(autoDestroyLifetime);
    }

    private bool ViolatesReactionTime(int candidate, float now, float spawnSpeed)
    {
        if (reactionTimeSeconds <= 0f || lastLaneIndex < 0 || candidate == lastLaneIndex)
        {
            return false;
        }

        if ((now - lastSpawnGlobalTime) >= reactionTimeSeconds)
        {
            return false;
        }

        // Do not suppress lane switches when previous hazards are still far from the player.
        if (!IsLaneBlocked(lastLaneIndex))
        {
            return false;
        }

        // Only gate lane switches when the new spawn will become dangerous soon.
        if (!WouldSpawnThreatenSoon(spawnSpeed))
        {
            return false;
        }

        // Keep trap protection in the short reaction window.
        return WouldExhaustSafeLanes(candidate, spawnSpeed);
    }

    private bool WouldExhaustSafeLanes(int candidate, float spawnSpeed)
    {
        int count = lanePositions.Length;
        int blocked = 0;
        bool candidateThreatensSoon = WouldSpawnThreatenSoon(spawnSpeed);

        for (int i = 0; i < count; i++)
        {
            bool laneBlocked = IsLaneBlocked(i);

            // Candidate lane should only count as blocked if the newly spawned obstacle
            // will enter the player's reaction zone soon.
            if (i == candidate && candidateThreatensSoon)
            {
                laneBlocked = true;
            }

            if (laneBlocked)
            {
                blocked++;
            }
        }

        return blocked >= count;
    }

    private bool WouldSpawnThreatenSoon(float spawnSpeed)
    {
        float speed = Mathf.Max(0.1f, spawnSpeed);
        float playerY = playerReference ? playerReference.position.y : 0f;
        float dangerTop = playerY + dangerRange;

        if (spawnHeight <= dangerTop)
        {
            return true;
        }

        float timeToDanger = (spawnHeight - dangerTop) / speed;
        return timeToDanger <= reactionTimeSeconds;
    }

    private bool ViolatesLockRule(int candidate, float now)
    {
        if (lockWindowSeconds <= 0f || lanePositions.Length < 3 || lanePositions.Length % 2 == 0)
        {
            return false;
        }

        if (previousLaneIndex < 0 || secondPreviousLaneIndex < 0)
        {
            return false;
        }

        float timeSincePrev = now - previousSpawnTime;
        float timeSinceSecond = now - secondPreviousSpawnTime;

        if (timeSincePrev > lockWindowSeconds || timeSinceSecond > lockWindowSeconds)
        {
            return false;
        }

        int leftIndex = 0;
        int rightIndex = lanePositions.Length - 1;

        bool prevSide = previousLaneIndex == leftIndex || previousLaneIndex == rightIndex;
        bool secondSide = secondPreviousLaneIndex == leftIndex || secondPreviousLaneIndex == rightIndex;

        if (!prevSide || !secondSide)
        {
            return false;
        }

        bool oppositeSides =
            (previousLaneIndex == leftIndex && secondPreviousLaneIndex == rightIndex) ||
            (previousLaneIndex == rightIndex && secondPreviousLaneIndex == leftIndex);

        if (!oppositeSides)
        {
            return false;
        }

        int centerIndex = lanePositions.Length / 2;
        return candidate != centerIndex;
    }

    private void DebugLane(string message)
    {
        if (!debugLaneDecisions)
        {
            return;
        }

        Debug.Log($"[ObstacleSpawner] {message}", this);
    }

    private bool IsLaneBlocked(int laneIndex)
    {
        if (laneOccupants == null || laneIndex < 0 || laneIndex >= laneOccupants.Length)
        {
            return false;
        }

        var list = laneOccupants[laneIndex];

        if (list == null)
        {
            return false;
        }

        CleanupLaneList(list);

        float playerY = playerReference ? playerReference.position.y : 0f;
        float dangerTop = playerY + dangerRange;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var marker = list[i];

            float y = marker.CurrentHeight;

            if (y < playerY - 0.5f)
            {
                continue;
            }

            if (y <= dangerTop)
            {
                return true;
            }
        }

        return false;
    }

    private string FormatLaneStates()
    {
        if (laneOccupants == null)
        {
            return "n/a";
        }

        var states = new StringBuilder();
        states.Append('[');

        for (int i = 0; i < lanePositions.Length; i++)
        {
            if (i > 0)
            {
                states.Append(", ");
            }

            states.Append(i);
            states.Append(':');
            states.Append(IsLaneBlocked(i) ? 'X' : 'O');
        }

        states.Append(']');
        return states.ToString();
    }

    internal void RegisterLaneMarker(ObstacleLaneMarker marker)
    {
        if (!marker || laneOccupants == null)
        {
            return;
        }

        int lane = marker.LaneIndex;

        if (lane < 0 || lane >= laneOccupants.Length)
        {
            return;
        }

        var list = laneOccupants[lane];

        if (list == null)
        {
            list = laneOccupants[lane] = new List<ObstacleLaneMarker>();
        }

        CleanupLaneList(list);

        if (!list.Contains(marker))
        {
            list.Add(marker);
        }
    }

    internal void UnregisterLaneMarker(ObstacleLaneMarker marker)
    {
        if (!marker || laneOccupants == null)
        {
            return;
        }

        int lane = marker.LaneIndex;

        if (lane < 0 || lane >= laneOccupants.Length)
        {
            return;
        }

        var list = laneOccupants[lane];

        if (list == null)
        {
            return;
        }

        list.Remove(marker);
        CleanupLaneList(list);
    }

    private void AttachLaneMarker(GameObject instance, int laneIndex)
    {
        if (!instance)
        {
            return;
        }

        if (!instance.TryGetComponent<ObstacleLaneMarker>(out var marker))
        {
            marker = instance.AddComponent<ObstacleLaneMarker>();
        }

        marker.Initialize(this, laneIndex, spawnHeight);
    }

    private void NormalizeLanePositions()
    {
        if (lanePositions == null || lanePositions.Length != RequiredLanePositions.Length)
        {
            lanePositions = (float[])RequiredLanePositions.Clone();
            Debug.LogWarning("ObstacleSpawner: lanePositions reset to the required three lanes.", this);
            return;
        }

        for (int i = 0; i < RequiredLanePositions.Length; i++)
        {
            lanePositions[i] = RequiredLanePositions[i];
        }
    }

    private void NormalizeDepthOffsets()
    {
        if (depthOffsets == null || depthOffsets.Length < 2)
        {
            depthOffsets = (float[])DefaultDepthOffsets.Clone();
            Debug.LogWarning("ObstacleSpawner: depthOffsets reset to the default road-depth offsets.", this);
            return;
        }

        for (int i = 0; i < depthOffsets.Length; i++)
        {
            depthOffsets[i] = Mathf.Clamp(depthOffsets[i], -2f, 2f);
        }
    }

    private bool ValidateLanes()
    {
        if (lanePositions == null || lanePositions.Length != RequiredLanePositions.Length)
        {
            Debug.LogError("ObstacleSpawner: Exactly three lane positions are required.", this);
            return false;
        }

        return true;
    }
}
