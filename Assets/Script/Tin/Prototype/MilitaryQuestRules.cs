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
    public const int TeamRespawnChargeTotal = 3;
    public const float RespawnDelaySeconds = 10f;

    // Once the first zombie reaches the gate, Solo uses this fixed-duration
    // countdown so the player can divide attention between defense and repairs.
    public const float SoloGateHoldSeconds = 180f;

    public static bool HasAllParts(bool hasBattery, bool hasFuel, bool hasRepairKit) =>
        hasBattery && hasFuel && hasRepairKit;

    public static float ApplyGateDamage(float currentHealth, float damage) =>
        Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));

    /// <summary>Solo receives a pool that a fixed three-minute DPS timer drains.</summary>
    public static float ComputeSiegeGateMaxHealth(int activePlayerCount) =>
        activePlayerCount > 1 ? BaseGateHealth :
        Mathf.Max(BaseGateHealth, GateDamagePerHit * GateMaxHitsPerSecond * SoloGateHoldSeconds);

    public static float GetSoloGateHealthAtElapsed(float maxHealth, float elapsedSeconds)
    {
        if (maxHealth <= 0f) return 0f;
        float normalizedElapsed = Mathf.Clamp01(elapsedSeconds / SoloGateHoldSeconds);
        return Mathf.Max(0f, maxHealth * (1f - normalizedElapsed));
    }

    public static bool CanUseTeamRespawn(bool soloMode, int remainingCharges) =>
        !soloMode && remainingCharges > 0;

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
