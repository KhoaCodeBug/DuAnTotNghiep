using UnityEngine;
using Fusion;

/// <summary>
/// Diagnostic instrument for player movement, physics and interpolation tracking.
/// Guarded with UNITY_EDITOR and a static toggle to guarantee zero log spam in release builds.
/// </summary>
public class PlayerMovementDiagnostic : NetworkBehaviour
{
    public static bool EnableDiagnostics = false;
    public static float LastRecordedMaxRootRbDelta = 0f;
    public static float LastRecordedMaxVisualRootDelta = 0f;
    public static int TotalCorrectionDeltasObserved = 0;

    #if UNITY_EDITOR
    private Rigidbody2D _rb;
    private Fusion.Addons.Physics.NetworkRigidbody2D _netRb;
    private Transform _visualTarget;
    private Vector3 _lastRootPos;
    private float _logInterval = 0.5f;
    private float _lastLogTime;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody2D>();
        _netRb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
        if (_netRb != null && _netRb.InterpolationTarget != null)
        {
            _visualTarget = _netRb.InterpolationTarget;
        }
        else
        {
            _visualTarget = transform.Find("Visual");
        }
        _lastRootPos = transform.position;
    }

    public override void Render()
    {
        if (!EnableDiagnostics || Runner == null || _rb == null) return;

        Vector2 rootPos = transform.position;
        Vector2 rbPos = _rb.position;
        Vector2 visualPos = _visualTarget != null ? (Vector2)_visualTarget.position : rootPos;

        float rootRbDelta = Vector2.Distance(rootPos, rbPos);
        float visualRootDelta = Vector2.Distance(rootPos, visualPos);

        if (rootRbDelta > LastRecordedMaxRootRbDelta) LastRecordedMaxRootRbDelta = rootRbDelta;
        if (visualRootDelta > LastRecordedMaxVisualRootDelta) LastRecordedMaxVisualRootDelta = visualRootDelta;

        if (Time.time - _lastLogTime >= _logInterval)
        {
            _lastLogTime = Time.time;
            Debug.Log($"[MovementDiagnostic] Obj={gameObject.name}, Tick={Runner.Tick}, Frame={Time.frameCount}, " +
                      $"StateAuth={HasStateAuthority}, InputAuth={HasInputAuthority}, IsForward={Runner.IsForward}, " +
                      $"RootPos={rootPos:F3}, RbPos={rbPos:F3}, Vel={_rb.linearVelocity:F3}, " +
                      $"VisualPos={visualPos:F3}, RootRbDelta={rootRbDelta:F4}, VisualRootDelta={visualRootDelta:F4}");
        }
    }
    #endif
}
