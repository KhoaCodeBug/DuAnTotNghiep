using UnityEngine;

public sealed class IntroCarDriveSetup : MonoBehaviour
{
    public enum DrivePhase
    {
        Idle,
        Cruising,
        Malfunctioning,
        Braking,
        FinalShake,
        Stopped
    }

    [SerializeField] private Transform carStart;
    [SerializeField] private Transform carStop;
    [SerializeField, Min(0f)] private float startDelay = 0.75f;
    [SerializeField, Min(0.1f)] private float travelDuration = 5f;
    [SerializeField, Min(0.5f)] private float preStopShakeDuration = 2.4f;
    [SerializeField, Min(0f)] private float preStopShakeDistance = 0.035f;
    [SerializeField, Range(2, 3)] private int malfunctionPulseCount = 3;
    [SerializeField, Min(0.1f)] private float brakingDuration = 1.35f;
    [SerializeField, Min(0f)] private float stopShakeDuration = 0.45f;
    [SerializeField, Min(0f)] private float stopShakeDistance = 0.06f;
    [SerializeField, Range(0.5f, 0.95f)] private float malfunctionStartProgress = 0.86f;
    [SerializeField, Range(0.5f, 0.98f)] private float malfunctionEndProgress = 0.91f;
    [SerializeField, Range(0.8f, 0.999f)] private float brakeEndProgress = 0.985f;
    private float elapsed;
    private Vector3 stopPosition;
    private Vector3 startPosition;

    public bool IsComplete { get; private set; }
    public DrivePhase CurrentPhase { get; private set; } = DrivePhase.Idle;
    public int CurrentMalfunctionPulse { get; private set; } = -1;

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

        startPosition = WithOwnZ(carStart.position);
        transform.position = startPosition;
        stopPosition = WithOwnZ(carStop.position);

        // The director starts the car only after the opening-eye sequence.
        enabled = false;
    }

    public void BeginDrive()
    {
        if (IsComplete) return;
        // Begin immediately when the eyelids start moving; startDelay is only
        // retained for backwards-compatible scene data.
        elapsed = startDelay;
        CurrentPhase = DrivePhase.Cruising;
        CurrentMalfunctionPulse = -1;
        enabled = true;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float driveTime = elapsed - startDelay;
        float finalShakeDuration = Mathf.Max(0.05f, stopShakeDuration);
        float totalMotionDuration = Mathf.Max(0.1f, travelDuration);
        float specialDuration = preStopShakeDuration + brakingDuration + finalShakeDuration;
        float cruiseDuration = Mathf.Max(0.1f, totalMotionDuration - specialDuration);
        float actualTotalDuration = cruiseDuration + specialDuration;

        float cruiseEnd = Mathf.Clamp01(malfunctionStartProgress);
        float malfunctionEnd = Mathf.Clamp(malfunctionEndProgress, cruiseEnd, 0.98f);
        float brakeEnd = Mathf.Clamp(brakeEndProgress, malfunctionEnd, 0.999f);
        if (driveTime < cruiseDuration)
        {
            CurrentPhase = DrivePhase.Cruising;
            float t = driveTime / cruiseDuration;
            transform.position = Vector3.Lerp(startPosition, stopPosition, cruiseEnd * t);
            return;
        }

        if (driveTime < cruiseDuration + preStopShakeDuration)
        {
            CurrentPhase = DrivePhase.Malfunctioning;
            float t = (driveTime - cruiseDuration) / preStopShakeDuration;
            CurrentMalfunctionPulse = Mathf.Min(Mathf.FloorToInt(t * malfunctionPulseCount), malfunctionPulseCount - 1);
            float progress = Mathf.Lerp(cruiseEnd, malfunctionEnd, t);
            // One full left/right sway per malfunction sound pulse.
            float shake = Mathf.Sin(t * Mathf.PI * 2f * malfunctionPulseCount) * preStopShakeDistance;
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress) + transform.right * shake;
            return;
        }

        if (driveTime < cruiseDuration + preStopShakeDuration + brakingDuration)
        {
            CurrentPhase = DrivePhase.Braking;
            float t = (driveTime - cruiseDuration - preStopShakeDuration) / brakingDuration;
            float brakingProgress = 1f - Mathf.Pow(1f - t, 3f);
            // The broken car now coasts only the short remaining distance to CarStop.
            float progress = Mathf.Lerp(malfunctionEnd, brakeEnd, brakingProgress);
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress);
            return;
        }

        if (driveTime < actualTotalDuration)
        {
            CurrentPhase = DrivePhase.FinalShake;
            float t = (driveTime - cruiseDuration - preStopShakeDuration - brakingDuration) / finalShakeDuration;
            float progress = Mathf.Lerp(brakeEnd, 1f, t);
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * stopShakeDistance * (1f - t);
            transform.position = Vector3.Lerp(startPosition, stopPosition, progress) + transform.right * shake;
            return;
        }

        transform.position = stopPosition;
        IsComplete = true;
        CurrentPhase = DrivePhase.Stopped;
        enabled = false;
    }

    private Vector3 WithOwnZ(Vector3 position)
    {
        position.z = transform.position.z;
        return position;
    }
}
