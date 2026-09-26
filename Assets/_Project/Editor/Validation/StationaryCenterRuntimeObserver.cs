#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic-only tool (Phase K Step 5B, lifecycle-fixed in Step 5C). Observes REAL
/// obstacles spawned by the production ObstacleSpawner during an active Play Mode session -- it
/// never instantiates, simulates, or recreates any spawn logic. Reads live Collider/Rigidbody/
/// Transform state from the actual GameObjects to determine, with real collider bounds, whether
/// center-lane obstacles ever geometrically reach a stationary center player and whether the
/// production collision signal (GameFeedbackSignals.ObstacleCollision) and GameOver signal
/// (GameFeedbackSignals.GameOver) fire. Subscribes to both signals (and taps Debug.Log to detect
/// a KillOnHit-attributed GameOver) BEFORE observation starts, and takes an immutable player
/// snapshot at start so losing the live PlayerController reference after GameOver never prevents
/// recording the kill. Not part of the shipped game; delete after use; do not commit.
/// </summary>
public static class StationaryCenterRuntimeObserver
{
    private const float ObserveSeconds = 90f;
    private const int MaxCenterLogEntries = 20;

    private sealed class CenterLogEntry
    {
        public float Time;
        public float Z;
        public float PlayerZ;
        public float Distance;
        public float VelocityZ;
        public bool DangerWindow;
        public bool ColliderEnabled;
        public bool IsTrigger;
        public string Outcome = "TRACKING";
    }

    private sealed class TrackedObstacle
    {
        public string Name;
        public int LaneIndex;
        public bool IsCenter;
        public bool DangerWindowSeen;
        public bool FullOverlapSeen;
        public bool CollidedThisRun;
        public CenterLogEntry LogEntry;
    }

    private sealed class CollisionRecord
    {
        public float Time;
        public Vector3 ObstaclePosition;
        public Vector3 PlayerPosition;
        public int Lane;
        public string ObstacleName;
    }

    // Immutable snapshot captured once at observer start -- the report must remain fully
    // answerable even after the live PlayerController is destroyed (e.g. non-additive Results
    // scene load right after GameOver).
    private readonly struct PlayerSnapshot
    {
        public readonly int InstanceId;
        public readonly int Lane;
        public readonly Vector3 Position;
        public readonly Bounds ColliderBounds;

        public PlayerSnapshot(int instanceId, int lane, Vector3 position, Bounds colliderBounds)
        {
            InstanceId = instanceId;
            Lane = lane;
            Position = position;
            ColliderBounds = colliderBounds;
        }
    }

    private sealed class KillRecord
    {
        public float Time;
        public string ObstacleInstance = "<unknown>";
        public int Lane;
        public Vector3? ObstaclePosition;
        public bool CollisionEventReceived;
    }

    private static bool active;
    private static bool subscribedToFeedback;
    private static bool subscribedToLog;
    private static float startTime;
    private static int centerLaneIndex;
    private static PlayerController player;
    private static Collider playerCollider;
    private static PlayerSnapshot playerSnapshot;

    private static readonly Dictionary<int, TrackedObstacle> tracked = new Dictionary<int, TrackedObstacle>();
    private static readonly List<CenterLogEntry> centerLog = new List<CenterLogEntry>();
    private static readonly List<CollisionRecord> collisionEvents = new List<CollisionRecord>();

    private static int leftSpawned, centerSpawned, rightSpawned;
    private static int centerEnteredDangerWindowCount;
    private static int centerFullOverlapCount;
    private static int centerDestroyedBeforeDanger;
    private static int centerPassedPlayer;
    private static int centerActualCollisions;
    private static int otherLaneCollisions;
    private static bool gameOverObserved;
    private static float gameOverTime;
    private static bool collisionEventReceived;
    private static bool killOnHitGameOver;
    private static bool playerLostAfterGameOver;
    private static KillRecord killRecord;
    private static float finishedElapsed;

    [MenuItem("TimeRush/Validation/Run Stationary Center Runtime Observer")]
    public static void Run()
    {
        if (active)
        {
            Debug.LogWarning("[StationaryCenterRuntimeObserver] Already running.");
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[StationaryCenterRuntimeObserver] Enter Play Mode first (player stationary in the center lane), then run this menu command.");
            return;
        }

        player = Object.FindObjectOfType<PlayerController>();
        if (!player)
        {
            Debug.LogError("[StationaryCenterRuntimeObserver] No PlayerController found in the running scene.");
            return;
        }

        playerCollider = player.GetComponent<Collider>();
        if (!playerCollider)
        {
            Debug.LogError("[StationaryCenterRuntimeObserver] Player has no Collider component; cannot measure real bounds.");
            return;
        }

        ResetState();
        centerLaneIndex = player.CurrentLane;
        // Immutable snapshot taken up front (lane, position, collider bounds, instance id) so the
        // final report never depends on the PlayerController still existing.
        playerSnapshot = new PlayerSnapshot(player.GetInstanceID(), centerLaneIndex, player.transform.position, playerCollider.bounds);
        startTime = Time.time;
        active = true;

        // Subscribe to the production signals BEFORE the observation loop starts ticking.
        SubscribeToFeedback();
        Application.logMessageReceived += OnLogMessageReceived;
        subscribedToLog = true;

        EditorApplication.update += Tick;
        Debug.Log($"[StationaryCenterRuntimeObserver] Started. centerLaneIndex={centerLaneIndex} playerInstanceId={playerSnapshot.InstanceId} playerPosition={playerSnapshot.Position} observeSeconds={ObserveSeconds}");
    }

    private static void SubscribeToFeedback()
    {
        if (subscribedToFeedback || !GameFeedbackSignals.HasInstance)
        {
            return;
        }

        GameFeedbackSignals.Instance.Events.ObstacleCollision += OnObstacleCollision;
        GameFeedbackSignals.Instance.Events.GameOver += OnGameOver;
        subscribedToFeedback = true;
    }

    private static void ResetState()
    {
        subscribedToFeedback = false;
        subscribedToLog = false;
        tracked.Clear();
        centerLog.Clear();
        collisionEvents.Clear();
        leftSpawned = centerSpawned = rightSpawned = 0;
        centerEnteredDangerWindowCount = 0;
        centerFullOverlapCount = 0;
        centerDestroyedBeforeDanger = 0;
        centerPassedPlayer = 0;
        centerActualCollisions = 0;
        otherLaneCollisions = 0;
        gameOverObserved = false;
        gameOverTime = -1f;
        collisionEventReceived = false;
        killOnHitGameOver = false;
        playerLostAfterGameOver = false;
        killRecord = null;
        finishedElapsed = 0f;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("Play Mode exited before the observation window elapsed");
            return;
        }

        // Retry subscription in case the feedback hub instance wasn't ready yet at Run() time.
        SubscribeToFeedback();

        // The PlayerController is allowed to disappear (e.g. Results scene load right after
        // GameOver) -- never terminate the diagnostic or reset any timer because of it. Only
        // note it, for reporting, once GameOver has actually been observed.
        if (!player && gameOverObserved)
        {
            playerLostAfterGameOver = true;
        }

        float elapsed = Time.time - startTime;
        ScanObstacles(elapsed);

        if (gameOverObserved)
        {
            Finish("Game over signal observed");
            return;
        }

        if (elapsed >= ObserveSeconds)
        {
            Finish(null);
        }
    }

    private static void ScanObstacles(float elapsed)
    {
        ObstacleLaneMarker[] markers = Object.FindObjectsOfType<ObstacleLaneMarker>();
        var seen = new HashSet<int>();
        // Fall back to the immutable start-of-run snapshot once the live player is gone (the
        // player was stationary for this diagnostic, so the snapshot bounds/position stay valid).
        Bounds playerBounds = player ? playerCollider.bounds : playerSnapshot.ColliderBounds;
        float playerZ = player ? player.transform.position.z : playerSnapshot.Position.z;

        foreach (ObstacleLaneMarker marker in markers)
        {
            if (!marker)
            {
                continue;
            }

            int id = marker.GetInstanceID();
            seen.Add(id);

            if (!tracked.TryGetValue(id, out TrackedObstacle rec))
            {
                rec = new TrackedObstacle
                {
                    Name = marker.gameObject.name,
                    LaneIndex = marker.LaneIndex,
                    IsCenter = marker.LaneIndex == centerLaneIndex
                };
                tracked[id] = rec;

                if (rec.IsCenter)
                {
                    centerSpawned++;
                    if (centerLog.Count < MaxCenterLogEntries)
                    {
                        rec.LogEntry = new CenterLogEntry();
                        centerLog.Add(rec.LogEntry);
                    }
                }
                else if (marker.LaneIndex < centerLaneIndex)
                {
                    leftSpawned++;
                }
                else
                {
                    rightSpawned++;
                }
            }

            Collider col = marker.GetComponent<Collider>();
            Rigidbody rb = marker.GetComponent<Rigidbody>();
            float velocityZ = rb ? rb.velocity.z : 0f;
            bool colliderEnabled = col && col.enabled;
            bool isTrigger = col && col.isTrigger;
            bool dangerWindow = false;
            bool fullOverlap = false;

            if (col)
            {
                Bounds ob = col.bounds;
                bool xOverlap = playerBounds.min.x <= ob.max.x && ob.min.x <= playerBounds.max.x;
                bool zOverlap = playerBounds.min.z <= ob.max.z && ob.min.z <= playerBounds.max.z;
                bool yOverlap = playerBounds.min.y <= ob.max.y && ob.min.y <= playerBounds.max.y;
                dangerWindow = xOverlap && zOverlap;
                fullOverlap = dangerWindow && yOverlap;
            }

            if (dangerWindow && !rec.DangerWindowSeen)
            {
                rec.DangerWindowSeen = true;
                if (rec.IsCenter)
                {
                    centerEnteredDangerWindowCount++;
                }
            }

            if (fullOverlap && !rec.FullOverlapSeen)
            {
                rec.FullOverlapSeen = true;
                if (rec.IsCenter)
                {
                    centerFullOverlapCount++;
                }
            }

            if (rec.IsCenter && rec.LogEntry != null && rec.LogEntry.Outcome == "TRACKING")
            {
                rec.LogEntry.Time = elapsed;
                rec.LogEntry.Z = marker.transform.position.z;
                rec.LogEntry.PlayerZ = playerZ;
                rec.LogEntry.Distance = Mathf.Abs(marker.transform.position.z - playerZ);
                rec.LogEntry.VelocityZ = velocityZ;
                rec.LogEntry.DangerWindow = rec.DangerWindowSeen;
                rec.LogEntry.ColliderEnabled = colliderEnabled;
                rec.LogEntry.IsTrigger = isTrigger;
            }
        }

        List<int> gone = null;

        foreach (KeyValuePair<int, TrackedObstacle> kvp in tracked)
        {
            if (!seen.Contains(kvp.Key))
            {
                (gone ??= new List<int>()).Add(kvp.Key);
            }
        }

        if (gone == null)
        {
            return;
        }

        foreach (int id in gone)
        {
            TrackedObstacle rec = tracked[id];
            tracked.Remove(id);

            if (!rec.IsCenter || rec.CollidedThisRun)
            {
                continue;
            }

            if (!rec.DangerWindowSeen)
            {
                centerDestroyedBeforeDanger++;
                if (rec.LogEntry != null)
                {
                    rec.LogEntry.Outcome = "DESTROYED_BEFORE_DANGER";
                }
            }
            else
            {
                centerPassedPlayer++;
                if (rec.LogEntry != null)
                {
                    rec.LogEntry.Outcome = "PASSED_WITHOUT_COLLISION";
                }
            }
        }
    }

    private static void OnObstacleCollision(ObstacleCollisionFeedback payload)
    {
        // NOTE: KillOnHit raises this signal BEFORE calling GameController.TriggerGameOver, so
        // this handler always runs while the player/obstacle GameObjects are still alive.
        collisionEventReceived = true;
        float t = Time.time - startTime;
        int hitLane = player ? player.CurrentLane : playerSnapshot.Lane;
        bool isCenterLane = hitLane == centerLaneIndex;

        TrackedObstacle nearest = FindNearestTracked(payload.Position);

        if (nearest != null)
        {
            nearest.CollidedThisRun = true;
            if (nearest.LogEntry != null)
            {
                nearest.LogEntry.Outcome = "COLLIDED";
            }
        }

        if (isCenterLane)
        {
            centerActualCollisions++;
        }
        else
        {
            otherLaneCollisions++;
        }

        collisionEvents.Add(new CollisionRecord
        {
            Time = t,
            ObstaclePosition = payload.Position,
            PlayerPosition = player ? player.transform.position : playerSnapshot.Position,
            Lane = hitLane,
            ObstacleName = nearest != null ? nearest.Name : "<unmatched>"
        });

        // Record the kill immediately -- do not wait for the GameOver signal, since the ensuing
        // non-additive Results scene load can destroy the obstacle/player before another chance.
        killRecord = new KillRecord
        {
            Time = t,
            ObstacleInstance = nearest != null ? nearest.Name : "<unmatched>",
            Lane = hitLane,
            ObstaclePosition = payload.Position,
            CollisionEventReceived = true
        };

        Debug.Log($"[StationaryCenterRuntimeObserver] COLLISION t={t:F2}s lane={hitLane} obstacle={(nearest != null ? nearest.Name : "<unmatched>")} obstaclePos={payload.Position} playerPos={(player ? player.transform.position : playerSnapshot.Position)}");
    }

    private static void OnGameOver()
    {
        if (gameOverObserved)
        {
            return;
        }

        gameOverObserved = true;
        gameOverTime = Time.time - startTime;

        if (killRecord == null)
        {
            // GameOver fired but ObstacleCollision never did -- report this explicitly rather
            // than incorrectly concluding that no collision occurred.
            killRecord = new KillRecord
            {
                Time = gameOverTime,
                ObstacleInstance = "COLLISION_EVENT_NOT_EMITTED",
                Lane = player ? player.CurrentLane : playerSnapshot.Lane,
                ObstaclePosition = null,
                CollisionEventReceived = false
            };
        }
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (killOnHitGameOver || string.IsNullOrEmpty(condition))
        {
            return;
        }

        // GameController.TriggerGameOverInternal logs "GameOver triggered by {source}" where
        // DescribeSource(source) formats a KillOnHit source as "KillOnHit (ObstacleName)". This is
        // a read-only tap of an existing production log line -- no production script is touched.
        if (condition.StartsWith("GameOver triggered by", System.StringComparison.Ordinal)
            && condition.Contains("KillOnHit"))
        {
            killOnHitGameOver = true;
        }
    }

    private static TrackedObstacle FindNearestTracked(Vector3 position)
    {
        TrackedObstacle best = null;
        float bestDist = float.MaxValue;

        foreach (TrackedObstacle rec in tracked.Values)
        {
            if (rec.CollidedThisRun)
            {
                continue;
            }

            float dist = rec.LogEntry != null
                ? Mathf.Abs(rec.LogEntry.Z - position.z)
                : 0f;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = rec;
            }
        }

        return best;
    }

    private static void Finish(string reason)
    {
        if (!active)
        {
            return;
        }

        active = false;
        EditorApplication.update -= Tick;

        if (subscribedToFeedback && GameFeedbackSignals.HasInstance)
        {
            GameFeedbackSignals.Instance.Events.ObstacleCollision -= OnObstacleCollision;
            GameFeedbackSignals.Instance.Events.GameOver -= OnGameOver;
        }

        if (subscribedToLog)
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            subscribedToLog = false;
        }

        // Always measured from the wall-clock start time -- never conditioned on the player still
        // being alive, and never reset to 0 just because it disappeared.
        finishedElapsed = Time.time - startTime;

        if (gameOverObserved && !player)
        {
            playerLostAfterGameOver = true;
        }

        foreach (TrackedObstacle rec in tracked.Values)
        {
            if (!rec.IsCenter || rec.CollidedThisRun || rec.LogEntry == null)
            {
                continue;
            }

            if (rec.LogEntry.Outcome == "TRACKING")
            {
                rec.LogEntry.Outcome = "STILL_ACTIVE_AT_END";
            }
        }

        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[StationaryCenterRuntimeObserver] Finished: {reason}");
        }

        PrintReport();
    }

    private static void PrintReport()
    {
        int totalObstacles = leftSpawned + centerSpawned + rightSpawned;
        string gameOverTimeText = gameOverObserved ? gameOverTime.ToString("F2") : "N/A";

        Debug.Log("[StationaryCenterRuntimeObserver:SUMMARY]\n"
            + $"observationSeconds={finishedElapsed:F1}\n"
            + $"gameOverTime={gameOverTimeText}\n"
            + $"totalObstacles={totalObstacles}\n"
            + $"left={leftSpawned}\n"
            + $"center={centerSpawned}\n"
            + $"right={rightSpawned}\n"
            + $"centerDangerCandidates={centerEnteredDangerWindowCount}\n"
            + $"centerActualCollisionEvents={centerActualCollisions}\n"
            + $"killOnHitGameOver={killOnHitGameOver}\n"
            + $"collisionEventReceived={collisionEventReceived}\n"
            + $"playerLostAfterGameOver={playerLostAfterGameOver}");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[StationaryCenterRuntimeObserver:CENTER]");

        for (int i = 0; i < centerLog.Count; i++)
        {
            CenterLogEntry e = centerLog[i];
            sb.AppendLine($"time={e.Time:F2} z={e.Z:F2} playerZ={e.PlayerZ:F2} distance={e.Distance:F2} velocityZ={e.VelocityZ:F2} dangerWindow={e.DangerWindow} colliderEnabled={e.ColliderEnabled} isTrigger={e.IsTrigger} outcome={e.Outcome}");
        }

        Debug.Log(sb.ToString());

        PrintKillRecord();

        string conclusion = DetermineConclusion();
        Debug.Log($"[StationaryCenterRuntimeObserver:CONCLUSION] {conclusion}");
    }

    private static void PrintKillRecord()
    {
        if (killRecord == null)
        {
            Debug.Log("[StationaryCenterRuntimeObserver:KILL]\n"
                + "time=N/A\n"
                + "obstacleInstance=N/A\n"
                + "lane=N/A\n"
                + "obstaclePosition=N/A\n"
                + $"playerSnapshotPosition={playerSnapshot.Position}\n"
                + "distance=N/A\n"
                + "collisionEventReceived=False\n"
                + "gameOverTriggeredByKillOnHit=False");
            return;
        }

        string obstaclePositionText = killRecord.ObstaclePosition.HasValue ? killRecord.ObstaclePosition.Value.ToString() : "N/A";
        string distanceText = killRecord.ObstaclePosition.HasValue
            ? Vector3.Distance(playerSnapshot.Position, killRecord.ObstaclePosition.Value).ToString("F2")
            : "N/A";

        Debug.Log("[StationaryCenterRuntimeObserver:KILL]\n"
            + $"time={killRecord.Time:F2}\n"
            + $"obstacleInstance={killRecord.ObstacleInstance}\n"
            + $"lane={killRecord.Lane}\n"
            + $"obstaclePosition={obstaclePositionText}\n"
            + $"playerSnapshotPosition={playerSnapshot.Position}\n"
            + $"distance={distanceText}\n"
            + $"collisionEventReceived={killRecord.CollisionEventReceived}\n"
            + $"gameOverTriggeredByKillOnHit={killOnHitGameOver}");
    }

    private static string DetermineConclusion()
    {
        if (!gameOverObserved)
        {
            return "4. NO GAMEOVER DURING OBSERVATION WINDOW";
        }

        if (killOnHitGameOver && !collisionEventReceived)
        {
            return "2. KILLONHIT CONFIRMED BUT COLLISION EVENT NOT EMITTED";
        }

        bool killLaneIsCenter = killRecord != null && killRecord.Lane == centerLaneIndex;

        if (collisionEventReceived && killLaneIsCenter)
        {
            return "1. REAL CENTER COLLISION CONFIRMED";
        }

        return "3. NO CENTER COLLISION OBSERVED BEFORE GAMEOVER";
    }
}
#endif
