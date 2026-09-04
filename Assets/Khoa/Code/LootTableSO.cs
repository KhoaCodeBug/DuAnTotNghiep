using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Loot Table", menuName = "Survival Game/Loot Table")]
public class LootTableSO : ScriptableObject
{
    [Header("Danh sách đồ rớt")]
    public List<LootContainer.LootSpawnData> lootRules = new List<LootContainer.LootSpawnData>();
}

/// <summary>
/// Canonical probability contract shared by authoritative runtime rolls and
/// deterministic audit simulations. The difficulty multiplier is applied here
/// exactly once; callers must always provide the authored/base chance.
/// </summary>
public static class LootDropRules
{
    public const float FlashlightBaseChancePercent = 25f;

    public static float GetEffectiveChancePercent(float baseChancePercent, float difficultyMultiplier)
    {
        return Mathf.Clamp(Mathf.Max(0f, baseChancePercent) * Mathf.Max(0f, difficultyMultiplier), 0f, 100f);
    }

    public static bool PassesRoll(float roll01, float baseChancePercent, float difficultyMultiplier)
    {
        float normalizedRoll = Mathf.Clamp01(roll01);
        return normalizedRoll < GetEffectiveChancePercent(baseChancePercent, difficultyMultiplier) / 100f;
    }

    public static int SimulateHits(float baseChancePercent, float difficultyMultiplier, int seed, int attempts)
    {
        if (attempts <= 0) return 0;

        System.Random random = new System.Random(seed);
        int hits = 0;
        for (int i = 0; i < attempts; i++)
        {
            if (PassesRoll((float)random.NextDouble(), baseChancePercent, difficultyMultiplier)) hits++;
        }

        return hits;
    }

    public static bool CanBeginAuthorityGeneration(bool hasStateAuthority, bool rollResolved)
    {
        return hasStateAuthority && !rollResolved;
    }

    public static int GetRandomLootSlotLimit(int maxSlots, int reservedQuestSlots)
    {
        return Mathf.Max(0, Mathf.Max(1, maxSlots) - Mathf.Max(0, reservedQuestSlots));
    }
}
