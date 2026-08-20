using UnityEngine;

/// <summary>
/// Tracks whether an obstacle has already awarded a near-miss bonus or collided with the player.
/// </summary>
public class NearMissState : MonoBehaviour
{
    public bool Awarded { get; private set; }
    public bool Collided { get; private set; }

    /// <summary>
    /// Attempts to award the near miss bonus. Returns true only once, and never after a collision.
    /// </summary>
    public bool TryAward()
    {
        if (Awarded || Collided)
        {
            return false;
        }

        Awarded = true;
        return true;
    }

    public void MarkCollision()
    {
        Collided = true;
    }
}
