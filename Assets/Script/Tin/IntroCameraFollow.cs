using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps the intro camera locked to a cinematic target without touching gameplay-camera code.
/// </summary>
public sealed class IntroCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.01f)] private float smoothTime = 0.18f;
    [SerializeField] private bool snapToTargetOnStart = true;
    [Header("Tutorial camera zoom")]
    [SerializeField, Min(0.1f)] private float zoomSpeed = 1f;
    [SerializeField, Min(0.1f)] private float minZoomSize = 1f;
    [SerializeField, Min(0.1f)] private float maxZoomSize = 3f;
    [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.15f;

    // These are deliberately the same values configured on Main Camera.  The
    // Intro scene originally serialized a much wider 5-14 range, so tutorial
    // mode uses this canonical scale even if an old scene instance still has
    // those stale Inspector values saved on it.
    private const float TutorialZoomSpeed = 1f;
    private const float TutorialMinZoomSize = 1f;
    private const float TutorialMaxZoomSize = 3f;
    private const float TutorialZoomSmoothTime = 0.15f;

    private Vector3 followVelocity;
    private Camera sceneCamera;
    private float targetZoom;
    private float zoomVelocity;

    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        followVelocity = Vector3.zero;
        if (snapImmediately && target != null)
            transform.position = target.position + offset;
    }

    private void Start()
    {
        sceneCamera = GetComponent<Camera>();
        if (sceneCamera != null)
        {
            sceneCamera.orthographic = true;
            targetZoom = sceneCamera.orthographicSize;
        }

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

    private void Update()
    {
        if (sceneCamera == null || !TutorialSession.IsActive || TutorialInputGate.CameraZoomLocked) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return;

        float scroll = Mathf.Clamp(Input.GetAxis("Mouse ScrollWheel"), -0.2f, 0.2f);
        float activeZoomSpeed = TutorialSession.IsActive ? TutorialZoomSpeed : zoomSpeed;
        float activeMinZoom = TutorialSession.IsActive ? TutorialMinZoomSize : minZoomSize;
        float activeMaxZoom = TutorialSession.IsActive ? TutorialMaxZoomSize : maxZoomSize;
        float activeSmoothTime = TutorialSession.IsActive ? TutorialZoomSmoothTime : zoomSmoothTime;
        if (Mathf.Abs(scroll) > 0.001f)
            targetZoom = Mathf.Clamp(targetZoom - scroll * activeZoomSpeed, activeMinZoom, activeMaxZoom);

        sceneCamera.orthographicSize = Mathf.SmoothDamp(sceneCamera.orthographicSize, targetZoom,
            ref zoomVelocity, activeSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    public void SetZoomInAmount(float zoomInAmount)
    {
        if (sceneCamera == null) sceneCamera = GetComponent<Camera>();
        if (sceneCamera == null) return;

        zoomInAmount = Mathf.Clamp01(zoomInAmount);
        float activeMinZoom = TutorialSession.IsActive ? TutorialMinZoomSize : minZoomSize;
        float activeMaxZoom = TutorialSession.IsActive ? TutorialMaxZoomSize : maxZoomSize;
        targetZoom = Mathf.Lerp(activeMaxZoom, activeMinZoom, zoomInAmount);
        zoomVelocity = 0f;
        sceneCamera.orthographicSize = targetZoom;
    }
}
