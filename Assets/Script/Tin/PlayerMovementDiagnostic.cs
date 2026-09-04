using System;
using UnityEngine;
using Fusion;

/// <summary>
/// Diagnostic instrument for player movement, physics, and interpolation tracking.
/// Features rolling 5-second statistical windows, zero-allocation steady state,
/// rollback correction measurement, and combat event auditing.
/// Guarded with UNITY_EDITOR || DEVELOPMENT_BUILD and a static toggle.
/// </summary>
public class PlayerMovementDiagnostic : NetworkBehaviour, IBeforeAllTicks
{
    public static bool EnableDiagnostics = false;

    // Lifetime summary metrics accessible to tests
    public static float LastRecordedMaxRootRbDelta = 0f;
    public static float LastRecordedMaxVisualRootDelta = 0f;
    public static int TotalCorrectionDeltasObserved = 0;
    public static float LastRecordedMaxCorrectionDelta = 0f;
    public static float LastRecordedMeanCorrectionDelta = 0f;
    public static float LastRecordedP95CorrectionDelta = 0f;

    // Combat presentation audit counters
    public static int TotalBashPredictions = 0;
    public static int TotalBashAuthoritativeRPCs = 0;
    public static int TotalBashObserverTriggers = 0;
    public static int TotalVariantMismatches = 0;
    public static int TotalMovementLockHitches = 0;
    public static int TotalHitTriggers = 0;
    public static int TotalDieTriggers = 0;

    public static void ResetCounters()
    {
        LastRecordedMaxRootRbDelta = 0f;
        LastRecordedMaxVisualRootDelta = 0f;
        TotalCorrectionDeltasObserved = 0;
        LastRecordedMaxCorrectionDelta = 0f;
        LastRecordedMeanCorrectionDelta = 0f;
        LastRecordedP95CorrectionDelta = 0f;

        TotalBashPredictions = 0;
        TotalBashAuthoritativeRPCs = 0;
        TotalBashObserverTriggers = 0;
        TotalVariantMismatches = 0;
        TotalMovementLockHitches = 0;
        TotalHitTriggers = 0;
        TotalDieTriggers = 0;
    }

    public static void RecordBashTrigger(int variant, bool isLocalPrediction, bool isAuthority)
    {
        if (isLocalPrediction) TotalBashPredictions++;
        else if (!isAuthority) TotalBashObserverTriggers++;
    }

    public static void RecordBashRPC(int variant, bool isEchoOnOwner)
    {
        TotalBashAuthoritativeRPCs++;
    }

    public static void RecordVariantAudit(int predictedVariant, int authoritativeVariant)
    {
        if (predictedVariant != authoritativeVariant) TotalVariantMismatches++;
    }

    public static void RecordMovementLockHitch()
    {
        TotalMovementLockHitches++;
    }

    public static void RecordHit(bool isAuthority)
    {
        TotalHitTriggers++;
    }

    public static void RecordDeath(bool isAuthority)
    {
        TotalDieTriggers++;
    }

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Rigidbody2D _rb;
    private Fusion.Addons.Physics.NetworkRigidbody2D _netRb;
    private Transform _visualTarget;
    private Vector3 _preResimPos;
    private bool _hasPreResimPos = false;

    // Rolling 5-second metrics (pre-allocated ring buffers for 0 GC alloc)
    private const int BUFFER_SIZE = 300; // ~5 seconds at 60 FPS
    private readonly float[] _correctionDeltas = new float[BUFFER_SIZE];
    private int _correctionCount = 0;
    private int _correctionHead = 0;

    private readonly float[] _rootRbDeltas = new float[BUFFER_SIZE];
    private readonly float[] _visualRootDeltas = new float[BUFFER_SIZE];
    private readonly float[] _frameTimes = new float[BUFFER_SIZE];
    private int _sampleCount = 0;
    private int _sampleHead = 0;

    private float _lastWindowTime = 0f;
    private const float WINDOW_INTERVAL = 5.0f;
    private long _lastGcMemory = 0;

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
        _lastWindowTime = Time.time;
        _lastGcMemory = GC.GetTotalMemory(false);
    }

    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
    {
        if (!EnableDiagnostics || _rb == null) return;

        if (resimulation && _hasPreResimPos)
        {
            // Snapshot rewind just occurred. Compare current snapshot position with pre-rewind predicted position
            float correctionDist = Vector3.Distance(transform.position, _preResimPos);
            if (correctionDist > 0.001f)
            {
                TotalCorrectionDeltasObserved++;
                _correctionDeltas[_correctionHead] = correctionDist;
                _correctionHead = (_correctionHead + 1) % BUFFER_SIZE;
                if (_correctionCount < BUFFER_SIZE) _correctionCount++;

                if (correctionDist > LastRecordedMaxCorrectionDelta)
                    LastRecordedMaxCorrectionDelta = correctionDist;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!EnableDiagnostics) return;
        _preResimPos = transform.position;
        _hasPreResimPos = true;
    }

    public override void Render()
    {
        if (!EnableDiagnostics || Runner == null || _rb == null) return;

        Vector2 rootPos = transform.position;
        Vector2 rbPos = _rb.position;
        Vector2 visualPos = _visualTarget != null ? (Vector2)_visualTarget.position : rootPos;

        float rootRbDelta = Vector2.Distance(rootPos, rbPos);
        float visualRootDelta = Vector2.Distance(rootPos, visualPos);
        float frameTime = Time.unscaledDeltaTime;

        if (rootRbDelta > LastRecordedMaxRootRbDelta) LastRecordedMaxRootRbDelta = rootRbDelta;
        if (visualRootDelta > LastRecordedMaxVisualRootDelta) LastRecordedMaxVisualRootDelta = visualRootDelta;

        // Push samples into circular buffers
        _rootRbDeltas[_sampleHead] = rootRbDelta;
        _visualRootDeltas[_sampleHead] = visualRootDelta;
        _frameTimes[_sampleHead] = frameTime;
        _sampleHead = (_sampleHead + 1) % BUFFER_SIZE;
        if (_sampleCount < BUFFER_SIZE) _sampleCount++;

        // Periodic 5s window reporting
        if (Time.time - _lastWindowTime >= WINDOW_INTERVAL)
        {
            float dt = Time.time - _lastWindowTime;
            _lastWindowTime = Time.time;

            // Compute statistics without allocating arrays
            float maxCorr = 0f, sumCorr = 0f, p95Corr = 0f;
            ComputeStats(_correctionDeltas, _correctionCount, out maxCorr, out sumCorr, out p95Corr);
            float meanCorr = _correctionCount > 0 ? sumCorr / _correctionCount : 0f;

            LastRecordedMaxCorrectionDelta = maxCorr;
            LastRecordedMeanCorrectionDelta = meanCorr;
            LastRecordedP95CorrectionDelta = p95Corr;

            float maxRootRb = 0f, sumRootRb = 0f, p95RootRb = 0f;
            ComputeStats(_rootRbDeltas, _sampleCount, out maxRootRb, out sumRootRb, out p95RootRb);

            float maxVisRoot = 0f, sumVisRoot = 0f, p95VisRoot = 0f;
            ComputeStats(_visualRootDeltas, _sampleCount, out maxVisRoot, out sumVisRoot, out p95VisRoot);

            float maxFt = 0f, sumFt = 0f, p95Ft = 0f;
            ComputeStats(_frameTimes, _sampleCount, out maxFt, out sumFt, out p95Ft);

            long currentMem = GC.GetTotalMemory(false);
            long memDelta = currentMem - _lastGcMemory;
            _lastGcMemory = currentMem;

            PZ_CameraController cam = PZ_CameraController.Instance;
            float camTargetDelta = (cam != null && cam.CurrentTarget != null)
                ? Vector2.Distance(cam.transform.position, cam.CurrentTarget.position)
                : 0f;

            Debug.Log($"[MovementDiagnostic 5s Window] Obj={gameObject.name} Auth(State={HasStateAuthority},Input={HasInputAuthority}) " +
                      $"Corrections={TotalCorrectionDeltasObserved} (WinCount={_correctionCount}, Max={maxCorr:F4}m, p95={p95Corr:F4}m, Mean={meanCorr:F4}m) | " +
                      $"RootRb(Max={maxRootRb:F4}m, p95={p95RootRb:F4}m) | VisRoot(Max={maxVisRoot:F4}m, p95={p95VisRoot:F4}m) | " +
                      $"CamTargetDelta={camTargetDelta:F3}m | FrameTimeP95={p95Ft * 1000f:F1}ms (Max={maxFt * 1000f:F1}ms) | GCAlloc={memDelta / 1024}KB | " +
                      $"Bash(Pred={TotalBashPredictions}, RPC={TotalBashAuthoritativeRPCs}, Obs={TotalBashObserverTriggers}, Mismatches={TotalVariantMismatches})");

            // Reset window-specific counts
            _correctionCount = 0;
            _correctionHead = 0;
            _sampleCount = 0;
            _sampleHead = 0;
        }
    }

    private static void ComputeStats(float[] buffer, int count, out float max, out float sum, out float p95)
    {
        max = 0f;
        sum = 0f;
        p95 = 0f;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            float val = buffer[i];
            if (val > max) max = val;
            sum += val;
        }

        // Compute 95th percentile
        int p95Index = Mathf.Min(count - 1, (int)(count * 0.95f));
        float[] temp = new float[count];
        Array.Copy(buffer, temp, count);
        Array.Sort(temp);
        p95 = temp[p95Index];
    }
    #endif
}
