using UnityEngine;

public sealed class IntroCarDriveSetup : MonoBehaviour
{
    [SerializeField] private Transform carStart;
    [SerializeField] private Transform carStop;
    [SerializeField, Min(0f)] private float startDelay = 0.75f;
    [SerializeField, Min(0.1f)] private float travelDuration = 5f;
    [SerializeField, Min(0.05f)] private float preStopShakeDuration = 0.45f;
    [SerializeField, Min(0f)] private float preStopShakeDistance = 0.035f;
    [SerializeField, Min(0.1f)] private float brakingDuration = 2.5f;
    [SerializeField, Min(0f)] private float stopShakeDuration = 0.35f;
    [SerializeField, Min(0f)] private float stopShakeDistance = 0.06f;

    private float elapsed;
    private Vector3 stopPosition;

    public bool IsComplete { get; private set; }

    private void Awake()
    {
        carStart ??= GameObject.Find("CarStart")?.transform;
        carStop ??= GameObject.Find("CarStop")?.transform;
        if (carStart == null || carStop == null)
        {
            Debug.LogError("IntroCarDriveSetup needs scene objects named CarStart and CarStop.", this);
            enabled = false;
            return;
        }

        transform.position = WithOwnZ(carStart.position);
        stopPosition = WithOwnZ(carStop.position);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed <= startDelay) return;

        float driveTime = elapsed - startDelay;
        float finalShakeDuration = Mathf.Max(0.05f, stopShakeDuration);
        float totalMotionDuration = Mathf.Max(0.1f, travelDuration);
        float specialDuration = preStopShakeDuration + brakingDuration + finalShakeDuration;
        float cruiseDuration = Mathf.Max(0.1f, totalMotionDuration - specialDuration);
        float actualTotalDuration = cruiseDuration + specialDuration;

        const float cruiseEnd = 0.73f;
        const float brakeEnd = 0.96f;
        Vector3 startPosition = WithOwnZ(carStart.position);

        if (driveTime < cruiseDuration)
        {
            float t = driveTime / cruiseDuration;
            transform.position = Vector3.Lerp(startPosition, stopPosition, cruiseEnd * t);
            return;
        }

        if (driveTime < cruiseDuration + preStopShakeDuration)
        {
            float t = (driveTime - cruiseDuration) / preStopShakeDuration;
            float progress = Mathf.Lerp(cruiseEnd, 0.79f, t);
            float shake = Mathf.Sin(t * Mathf.PI * 7f) * preStopShakeDistance;
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress) + transform.right * shake;
            return;
        }

        if (driveTime < cruiseDuration + preStopShakeDuration + brakingDuration)
        {
            float t = (driveTime - cruiseDuration - preStopShakeDuration) / brakingDuration;
            float brakingProgress = 1f - Mathf.Pow(1f - t, 3f);
            float progress = Mathf.Lerp(0.79f, brakeEnd, brakingProgress);
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress);
            return;
        }

        if (driveTime < actualTotalDuration)
        {
            float t = (driveTime - cruiseDuration - preStopShakeDuration - brakingDuration) / finalShakeDuration;
            float progress = Mathf.Lerp(brakeEnd, 1f, t);
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * stopShakeDistance * (1f - t);
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress) + transform.right * shake;
            return;
        }

        transform.position = stopPosition;
        IsComplete = true;
        enabled = false;
    }

    private Vector3 WithOwnZ(Vector3 position)
    {
        position.z = transform.position.z;
        return position;
    }
}
