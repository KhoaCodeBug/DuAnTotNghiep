using UnityEngine;

/// <summary>Pure transition rules shared by the Fusion runtime and edit-mode tests.</summary>
public static class MilitaryQuestRules
{
    public const float BaseGateHealth = 5000f;
    public const float MaxRepairProgress = 100f;

    // Canonical siege damage beat: 12 HP per hit, at most 4 hits per second.
    public const float GateDamagePerHit = 12f;
    public const int GateMaxHitsPerSecond = 4;

    // Team respawn rules for the military finale (multiplayer only).
    public const float RespawnDelaySeconds = 10f;

    // Once the first zombie reaches the gate, Solo uses a deterministic
    // difficulty-scaled countdown so the player can divide attention between
    // defense and repairs. Difficulty IDs follow MainMenuManager/PlayerPrefs:
    // Easy = 0, Normal = 1, Hard = 2.
    public const float SoloGateHoldSecondsEasy = 300f;
    public const float SoloGateHoldSecondsNormal = 240f;
    public const float SoloGateHoldSecondsHard = 180f;
    public const float SoloGateHoldSeconds = SoloGateHoldSecondsNormal;

    public static bool HasAllParts(bool hasBattery, bool hasFuel, bool hasRepairKit) =>
        hasBattery && hasFuel && hasRepairKit;

    public static float ApplyGateDamage(float currentHealth, float damage) =>
        Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));

    public static float GetSoloGateHoldSeconds(int difficulty) => difficulty switch
    {
        0 => SoloGateHoldSecondsEasy,
        2 => SoloGateHoldSecondsHard,
        _ => SoloGateHoldSecondsNormal
    };

    /// <summary>Solo receives a pool that the selected difficulty timer drains.</summary>
    public static float ComputeSiegeGateMaxHealth(int activePlayerCount) =>
        ComputeSiegeGateMaxHealthForDifficulty(activePlayerCount, 1);

    public static float ComputeSiegeGateMaxHealthForDifficulty(int activePlayerCount, int difficulty) =>
        activePlayerCount > 1 ? BaseGateHealth :
        Mathf.Max(BaseGateHealth, GateDamagePerHit * GateMaxHitsPerSecond * GetSoloGateHoldSeconds(difficulty));

    public static float GetSoloGateHealthAtElapsed(float maxHealth, float elapsedSeconds) =>
        GetSoloGateHealthAtElapsedForDifficulty(maxHealth, elapsedSeconds, 1);

    public static float GetSoloGateHealthAtElapsedForDifficulty(float maxHealth, float elapsedSeconds, int difficulty)
    {
        if (maxHealth <= 0f) return 0f;
        float normalizedElapsed = Mathf.Clamp01(elapsedSeconds / GetSoloGateHoldSeconds(difficulty));
        return Mathf.Max(0f, maxHealth * (1f - normalizedElapsed));
    }

    public static bool CanUseTeamRespawn(bool soloMode, int remainingCharges) =>
        !soloMode && remainingCharges > 0;

    /// <summary>
    /// Shared military respawn pool sized for the team that committed to the
    /// finale. The explicit tiers keep the intended 5/6 and 9/10 player values
    /// stable instead of allowing rounding to under-allocate those teams.
    /// </summary>
    public static int ComputeTeamRespawnCharges(int activePlayerCount)
    {
        if (activePlayerCount <= 1) return 0;
        if (activePlayerCount <= 4) return 3;
        if (activePlayerCount <= 6) return 5;
        if (activePlayerCount <= 8) return 6;
        return 8;
    }

    public static int ConsumeTeamRespawnCharge(int remainingCharges) =>
        Mathf.Max(0, remainingCharges - 1);

    public static bool IsRespawnDelayElapsed(float secondsSinceDeath) =>
        secondsSinceDeath >= RespawnDelaySeconds;

    public static float GetRemainingRespawnSeconds(float secondsSinceDeath) =>
        Mathf.Max(0f, RespawnDelaySeconds - secondsSinceDeath);

    public static float ApplyRepairProgress(float currentProgress, float deltaSeconds, float repairSeconds)
    {
        if (deltaSeconds <= 0f || repairSeconds <= 0f)
            return Mathf.Clamp(currentProgress, 0f, MaxRepairProgress);
        return Mathf.Clamp(currentProgress + deltaSeconds / repairSeconds * MaxRepairProgress,
            0f, MaxRepairProgress);
    }
}
