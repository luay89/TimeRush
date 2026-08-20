using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleLaneMarker : MonoBehaviour
{
    public int LaneIndex { get; private set; } = -1;

    private ObstacleSpawner owner;
    private bool registered;
    private bool initialized;

    public float CurrentHeight => transform.position.y;

    public void Initialize(ObstacleSpawner spawner, int laneIndex)
    {
        owner = spawner;
        LaneIndex = laneIndex;
        if (LaneIndex < 0)
        {
            return;
        }
        initialized = true;

        if (!isActiveAndEnabled)
        {
            return;
        }

        Register();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            Register();
        }
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void Register()
    {
        if (registered || owner == null || LaneIndex < 0)
        {
            return;
        }

        owner.RegisterLaneMarker(this);
        registered = true;
    }

    private void OnDestroy()
    {
        Unregister();
        owner = null;
        initialized = false;
    }

    private void Unregister()
    {
        if (!registered || owner == null)
        {
            return;
        }

        owner.UnregisterLaneMarker(this);
        registered = false;
    }
}
