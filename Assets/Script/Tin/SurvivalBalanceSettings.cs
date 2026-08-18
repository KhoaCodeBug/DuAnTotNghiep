using UnityEngine;

/// <summary>
/// Editable shared survival tuning. Player prefabs keep fallback values, while
/// this Resources asset is the production source of truth for all difficulties.
/// </summary>
[CreateAssetMenu(fileName = "SurvivalBalanceSettings", menuName = "Game/Balance/Survival")]
public sealed class SurvivalBalanceSettings : ScriptableObject
{
    private const string ResourcePath = "SurvivalBalanceSettings";

    [Header("Normal baseline (per real-time second)")]
    [SerializeField, Min(0f)] private float normalHungerDrainRate = SurvivalBalanceRules.NormalHungerDrainRate;
    [SerializeField, Min(0f)] private float normalThirstDrainRate = SurvivalBalanceRules.NormalThirstDrainRate;
    [SerializeField, Min(0f)] private float normalDamagePerSecond = SurvivalBalanceRules.NormalDamagePerSecond;

    [Header("Difficulty multipliers")]
    [SerializeField, Min(0f)] private float easyMultiplier = SurvivalBalanceRules.EasyMultiplier;
    [SerializeField, Min(0f)] private float hardMultiplier = SurvivalBalanceRules.HardMultiplier;

    [Header("Grace after hunger or thirst reaches zero")]
    [SerializeField, Min(0f)] private float easyGraceSeconds = SurvivalBalanceRules.EasyGraceSeconds;
    [SerializeField, Min(0f)] private float normalGraceSeconds = SurvivalBalanceRules.NormalGraceSeconds;
    [SerializeField, Min(0f)] private float hardGraceSeconds = SurvivalBalanceRules.HardGraceSeconds;

    public SurvivalBalanceProfile GetProfile(int difficulty)
    {
        return SurvivalBalanceRules.BuildProfile(difficulty, normalHungerDrainRate,
            normalThirstDrainRate, normalDamagePerSecond, easyMultiplier, hardMultiplier,
            easyGraceSeconds, normalGraceSeconds, hardGraceSeconds);
    }

    public static SurvivalBalanceProfile GetActiveProfile(int difficulty)
    {
        SurvivalBalanceSettings settings = Resources.Load<SurvivalBalanceSettings>(ResourcePath);
        return settings != null ? settings.GetProfile(difficulty) : SurvivalBalanceRules.GetProfile(difficulty);
    }
}
