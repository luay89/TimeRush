using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -10f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 2.5f, 3.5f);
    [SerializeField] private float followSpeed = 10f;
    private Vector3 feedbackOffset;
    private Vector3 appliedFeedbackOffset;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position + offset;
        Vector3 basePosition = transform.position - appliedFeedbackOffset;
        transform.position = Vector3.Lerp(basePosition, desired, followSpeed * Time.deltaTime) + feedbackOffset;
        appliedFeedbackOffset = feedbackOffset;
        transform.LookAt(target.position + lookAtOffset);
    }

    /// <summary>
    /// Lets the feedback layer add a short visual offset without altering the follow target or gameplay transforms.
    /// </summary>
    public void SetFeedbackOffset(Vector3 offset)
    {
        feedbackOffset = offset;
    }
}
