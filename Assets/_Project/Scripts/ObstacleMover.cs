using UnityEngine;

/// <summary>
/// Simple transform-based fall-back mover for obstacles that lack a Rigidbody.
/// </summary>
public class ObstacleMover : MonoBehaviour
{
    [SerializeField] private Vector3 direction = Vector3.down;
    private float speed;

    public void SetSpeed(float value)
    {
        speed = Mathf.Max(0f, value);
    }

    private void Update()
    {
        if (speed <= 0f)
        {
            return;
        }

        transform.position += direction * (speed * Time.deltaTime);
    }
}
