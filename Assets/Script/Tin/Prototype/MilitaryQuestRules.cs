using UnityEngine;

/// <summary>Pure transition rules shared by the Fusion runtime and edit-mode tests.</summary>
public static class MilitaryQuestRules
{
    public const float BaseGateHealth = 5000f;
    public const float MaxRepairProgress = 100f;

    public static bool HasAllParts(bool hasBattery, bool hasFuel, bool hasRepairKit) =>
        hasBattery && hasFuel && hasRepairKit;

    public static float ApplyGateDamage(float currentHealth, float damage) =>
        Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));

    public static float ApplyRepairProgress(float currentProgress, float deltaSeconds, float repairSeconds)
    {
        if (deltaSeconds <= 0f || repairSeconds <= 0f)
            return Mathf.Clamp(currentProgress, 0f, MaxRepairProgress);
        return Mathf.Clamp(currentProgress + deltaSeconds / repairSeconds * MaxRepairProgress,
            0f, MaxRepairProgress);
    }
}
