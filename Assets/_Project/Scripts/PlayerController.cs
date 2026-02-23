using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float xLimit = 4.5f;

    void Update()
    {
        // إدخال بسيط: A/D أو الأسهم أو سحب بإصبع لاحقاً
        float x = Input.GetAxisRaw("Horizontal"); // -1..0..1

        Vector3 pos = transform.position;
        pos.x += x * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -xLimit, xLimit);

        transform.position = pos;
    }
}
