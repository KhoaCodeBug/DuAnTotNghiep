using UnityEngine;

public readonly struct SurvivalBalanceProfile
{
    public readonly float HungerDrainRate;
    public readonly float ThirstDrainRate;
    public readonly float DamagePerSecond;
    public readonly float ZeroNeedGraceSeconds;

    public SurvivalBalanceProfile(float hungerDrainRate, float thirstDrainRate,
        float damagePerSecond, float zeroNeedGraceSeconds)
    {
        HungerDrainRate = hungerDrainRate;
        ThirstDrainRate = thirstDrainRate;
        DamagePerSecond = damagePerSecond;
        ZeroNeedGraceSeconds = zeroNeedGraceSeconds;
    }
}

/// <summary>Single source of truth for hunger/thirst tuning across both player prefabs.</summary>
public static class SurvivalBalanceRules
{
    public const float NormalHungerDrainRate = 0.20f;
    public const float NormalThirstDrainRate = 0.25f;
    public const float NormalDamagePerSecond = 0.30f;

    public const float EasyMultiplier = 0.75f;
    public const float HardMultiplier = 1.25f;
    public const float EasyGraceSeconds = 35f;
    public const float NormalGraceSeconds = 25f;
    public const float HardGraceSeconds = 15f;

    public static SurvivalBalanceProfile GetProfile(int difficulty)
    {
        return BuildProfile(difficulty, NormalHungerDrainRate, NormalThirstDrainRate,
            NormalDamagePerSecond, EasyMultiplier, HardMultiplier, EasyGraceSeconds,
            NormalGraceSeconds, HardGraceSeconds);
    }

    public static SurvivalBalanceProfile BuildProfile(int difficulty, float normalHungerDrainRate,
        float normalThirstDrainRate, float normalDamagePerSecond, float easyMultiplier,
        float hardMultiplier, float easyGraceSeconds, float normalGraceSeconds,
        float hardGraceSeconds)
    {
        int clampedDifficulty = Mathf.Clamp(difficulty, 0, 2);
        float multiplier = clampedDifficulty switch
        {
            0 => Mathf.Max(0f, easyMultiplier),
            2 => Mathf.Max(0f, hardMultiplier),
            _ => 1f
        };
        float graceSeconds = clampedDifficulty switch
        {
            0 => Mathf.Max(0f, easyGraceSeconds),
            2 => Mathf.Max(0f, hardGraceSeconds),
            _ => Mathf.Max(0f, normalGraceSeconds)
        };
        return new SurvivalBalanceProfile(
            Mathf.Max(0f, normalHungerDrainRate) * multiplier,
            Mathf.Max(0f, normalThirstDrainRate) * multiplier,
            Mathf.Max(0f, normalDamagePerSecond) * multiplier,
            graceSeconds);
    }

    public static float RestoreNeed(float current, float maximum, float amount)
    {
        float safeMaximum = Mathf.Max(0f, maximum);
        return Mathf.Clamp(current + Mathf.Max(0f, amount), 0f, safeMaximum);
    }

    public static int GetWellFedTier(float hungerRatio, float thirstRatio)
    {
        hungerRatio = Mathf.Clamp01(hungerRatio);
        thirstRatio = Mathf.Clamp01(thirstRatio);
        if (hungerRatio < 0.8f || thirstRatio < 0.8f) return 0;
        if (hungerRatio < 0.85f) return 1;
        if (hungerRatio < 0.90f) return 2;
        if (hungerRatio < 0.95f) return 3;
        return 4;
    }

    public static int GetBadNeedTier(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (ratio > 0.25f) return 1;
        if (ratio > 0.10f) return 2;
        if (ratio > 0f) return 3;
        return 4;
    }
}
