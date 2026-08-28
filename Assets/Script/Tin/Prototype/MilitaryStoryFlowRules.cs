using UnityEngine;

/// <summary>
/// Pure rules for the school-to-siege military story milestone. Keeping the
/// scaling here makes the authoritative runtime and EditMode tests agree.
/// </summary>
public static class MilitaryStoryFlowRules
{
    public const int RequiredSchoolClues = 3;
    public const float HordeCheckIntervalSeconds = 5f;
    public const int SmallMultiplayerNearbyTarget = 50;
    public const int LargeMultiplayerNearbyTarget = 80;
    public const int SoloNearbyTarget = 24;
    public const int SmallMultiplayerSpawnPerPoint = 4;
    public const int LargeMultiplayerSpawnPerPoint = 6;
    public const int SoloSpawnPerPoint = 2;
    public const int SoloHardSafetyCap = 36;
    public const int SmallMultiplayerHardSafetyCap = 72;
    public const int LargeMultiplayerHardSafetyCap = 112;
    public const int SpawnPointCount = 4;
    public const float EscapeGatherRadius = 6f;
    public const float EndingMapCameraTravelSeconds = 6f;
    public const float EndingMapCameraHoldSeconds = 2f;

    public static int CompleteClueMask => (1 << RequiredSchoolClues) - 1;

    public static bool HasAllSchoolClues(int clueMask) =>
        (clueMask & CompleteClueMask) == CompleteClueMask;

    public static int GetNearbyTarget(int activePlayerCount) => activePlayerCount switch
    {
        <= 1 => SoloNearbyTarget,
        <= 4 => SmallMultiplayerNearbyTarget,
        _ => LargeMultiplayerNearbyTarget
    };

    public static int GetSpawnPerPoint(int activePlayerCount) => activePlayerCount switch
    {
        <= 1 => SoloSpawnPerPoint,
        <= 4 => SmallMultiplayerSpawnPerPoint,
        _ => LargeMultiplayerSpawnPerPoint
    };

    public static int GetHardSafetyCap(int activePlayerCount) => activePlayerCount switch
    {
        <= 1 => SoloHardSafetyCap,
        <= 4 => SmallMultiplayerHardSafetyCap,
        _ => LargeMultiplayerHardSafetyCap
    };

    /// <summary>
    /// A living survivor is extraction-ready when occupying the canonical
    /// police car, or when standing on foot inside the authority-validated
    /// gather radius. Occupants of another vehicle are not treated as standing.
    /// </summary>
    public static bool IsPlayerReadyForMilitaryEscape(bool isOnPoliceVehicle,
        bool isInAnyVehicle, float distanceToPoliceVehicle) =>
        isOnPoliceVehicle || (!isInAnyVehicle &&
            Mathf.Max(0f, distanceToPoliceVehicle) <= EscapeGatherRadius);

    public static int GetBatchSize(int activePlayerCount, int spawnPointCount = SpawnPointCount) =>
        Mathf.Max(0, spawnPointCount) * GetSpawnPerPoint(activePlayerCount);

    public static bool ShouldSpawnBatch(int activePlayerCount, int nearbySiegeZombieCount) =>
        Mathf.Max(0, nearbySiegeZombieCount) < GetNearbyTarget(activePlayerCount);

    public static bool ShouldInterruptVehicleRepair(bool isDirectZombieAttack) => isDirectZombieAttack;

    public static float GetEscapeGateDamagePerSecond(float gateMaxHealth, float maximumDrainSeconds) =>
        Mathf.Max(0f, gateMaxHealth) / Mathf.Max(1f, maximumDrainSeconds);

    public static float GetSoloGateElapsedRate(bool escapeEngineStarted, float normalHoldSeconds,
        float maximumDrainSeconds) => escapeEngineStarted
        ? 1f + Mathf.Max(1f, normalHoldSeconds) / Mathf.Max(1f, maximumDrainSeconds)
        : 1f;

    /// <summary>
    /// Integrated t^3(1-t)^4 velocity profile. Compared with the former shot,
    /// the camera spends longer easing away from rest, peaks around 43%, then
    /// settles gently into the authored map-reveal target.
    /// </summary>
    public static float EvaluateEndingMapCameraTravel(float t)
    {
        double x = Mathf.Clamp01(t);
        double x2 = x * x;
        double x4 = x2 * x2;
        double x5 = x4 * x;
        double x6 = x5 * x;
        double x7 = x6 * x;
        double x8 = x7 * x;
        return Mathf.Clamp01((float)(70.0 * x4 - 224.0 * x5 + 280.0 * x6 - 160.0 * x7 + 35.0 * x8));
    }
}
