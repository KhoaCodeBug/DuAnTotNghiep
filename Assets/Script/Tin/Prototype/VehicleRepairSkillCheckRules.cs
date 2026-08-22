using UnityEngine;

public enum VehicleRepairSkillCheckResult
{
    Miss,
    Success,
    Perfect
}

/// <summary>Pure repair-minigame rules shared by the Fusion runtime and edit-mode tests.</summary>
public static class VehicleRepairSkillCheckRules
{
    public const float MaxProgress = 100f;

    public static float AdvanceBaseProgress(float progress, float deltaSeconds, float repairDurationSeconds)
    {
        if (deltaSeconds <= 0f || repairDurationSeconds <= 0f)
            return Mathf.Clamp(progress, 0f, MaxProgress);
        return Mathf.Clamp(progress + deltaSeconds / repairDurationSeconds * MaxProgress, 0f, MaxProgress);
    }

    public static VehicleRepairSkillCheckResult Evaluate(
        float needleAngle, float targetAngle, float successArcDegrees, float perfectArcDegrees)
    {
        float distance = Mathf.Abs(Mathf.DeltaAngle(needleAngle, targetAngle));
        if (distance <= Mathf.Max(0f, perfectArcDegrees) * 0.5f)
            return VehicleRepairSkillCheckResult.Perfect;
        if (distance <= Mathf.Max(0f, successArcDegrees) * 0.5f)
            return VehicleRepairSkillCheckResult.Success;
        return VehicleRepairSkillCheckResult.Miss;
    }

    public static float ApplyResult(float progress, VehicleRepairSkillCheckResult result,
        float successBonus, float perfectBonus, float missPenalty)
    {
        float delta = result switch
        {
            VehicleRepairSkillCheckResult.Perfect => Mathf.Max(0f, perfectBonus),
            VehicleRepairSkillCheckResult.Success => Mathf.Max(0f, successBonus),
            _ => -Mathf.Max(0f, missPenalty)
        };
        return Mathf.Clamp(progress + delta, 0f, MaxProgress);
    }

    public static float GetMinimumTargetCenterAngle(float minimumTravelFraction, float successArcDegrees)
    {
        float firstSafeAngle = Mathf.Clamp01(minimumTravelFraction) * 360f;
        return Mathf.Clamp(firstSafeAngle + Mathf.Max(0f, successArcDegrees) * 0.5f, 0f, 360f);
    }
}
