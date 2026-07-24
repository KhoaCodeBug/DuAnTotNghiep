using UnityEngine;

/// <summary>
/// Keeps the intro camera locked to a cinematic target without touching gameplay-camera code.
/// </summary>
public sealed class IntroCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.01f)] private float smoothTime = 0.18f;
    [SerializeField] private bool snapToTargetOnStart = true;

    private Vector3 followVelocity;

    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        followVelocity = Vector3.zero;
        if (snapImmediately && target != null)
            transform.position = target.position + offset;
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("IntroCameraFollow needs a target.", this);
            enabled = false;
            return;
        }

        if (snapToTargetOnStart)
            transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime);
    }
}
