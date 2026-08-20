using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 6f;

    private void OnEnable()
    {
        Destroy(gameObject, lifetime);
    }

    public void SetLifetime(float value)
    {
        lifetime = Mathf.Max(0.1f, value);
    }
}