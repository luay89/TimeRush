using UnityEngine;

/// <summary>
/// Data ownership for the fixed TimeRush track. It intentionally validates exactly three lanes.
/// </summary>
[CreateAssetMenu(fileName = "TrackLayoutConfig", menuName = "TimeRush/Track Layout Config")]
public sealed class TrackLayoutConfig : ScriptableObject
{
    [SerializeField] private float[] lanePositions = { -2.5f, 0f, 2.5f };
    [SerializeField] private float[] depthOffsets = { -1.5f, 0f, 1.5f };
    [SerializeField, Min(1.25f)] private float safeDepthRange = 2f;

    public float SafeDepthRange => safeDepthRange;

    public float[] CopyLanePositions()
    {
        return lanePositions == null ? null : (float[])lanePositions.Clone();
    }

    public float[] CopyDepthOffsets()
    {
        return depthOffsets == null ? null : (float[])depthOffsets.Clone();
    }

    public bool IsValid(out string message)
    {
        if (lanePositions == null || lanePositions.Length != 3)
        {
            message = "TimeRush requires exactly three lanes.";
            return false;
        }

        if (!Mathf.Approximately(lanePositions[0], -2.5f) || !Mathf.Approximately(lanePositions[1], 0f) || !Mathf.Approximately(lanePositions[2], 2.5f))
        {
            message = "Track lane positions must remain -2.5, 0, and 2.5.";
            return false;
        }

        if (depthOffsets == null || depthOffsets.Length < 2 || safeDepthRange < 1.25f)
        {
            message = "Track depth configuration is invalid.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
