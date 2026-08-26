using UnityEngine;

/// <summary>
/// Pure rules for the school-to-siege military story milestone. Keeping the
/// scaling here makes the authoritative runtime and EditMode tests agree.
/// </summary>
public static class MilitaryStoryFlowRules
{
    public const int RequiredSchoolClues = 3;
    public const float HordeCheckIntervalSeconds = 5f;
    public const int MultiplayerNearbyTarget = 50;
    public const int SoloNearbyTarget = 24;
    public const int MultiplayerSpawnPerPoint = 4;
    public const int SoloSpawnPerPoint = 2;
    public const int SpawnPointCount = 4;

    public static int CompleteClueMask => (1 << RequiredSchoolClues) - 1;

    public static bool HasAllSchoolClues(int clueMask) =>
        (clueMask & CompleteClueMask) == CompleteClueMask;

    public static int GetNearbyTarget(int activePlayerCount) =>
        activePlayerCount <= 1 ? SoloNearbyTarget : MultiplayerNearbyTarget;

    public static int GetSpawnPerPoint(int activePlayerCount) =>
        activePlayerCount <= 1 ? SoloSpawnPerPoint : MultiplayerSpawnPerPoint;

    public static int GetBatchSize(int activePlayerCount, int spawnPointCount = SpawnPointCount) =>
        Mathf.Max(0, spawnPointCount) * GetSpawnPerPoint(activePlayerCount);

    public static bool ShouldSpawnBatch(int activePlayerCount, int nearbySiegeZombieCount) =>
        Mathf.Max(0, nearbySiegeZombieCount) < GetNearbyTarget(activePlayerCount);

    public static bool ShouldInterruptVehicleRepair(bool isDirectZombieAttack) => isDirectZombieAttack;
}
