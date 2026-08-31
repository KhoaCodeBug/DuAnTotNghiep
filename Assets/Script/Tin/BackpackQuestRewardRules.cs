/// <summary>
/// Quest milestones that award the two late-game backpack upgrades. These
/// rules are intentionally separate from loot/equip rules and map rewards.
/// </summary>
public enum BackpackQuestRewardMilestone
{
    HospitalArrival = 0,
    MilitaryBaseEntry = 1
}

public static class BackpackQuestRewardRules
{
    public const int HospitalBackpackLevel = 4;
    public const int MilitaryBackpackLevel = 5;

    public static int GetRewardLevel(BackpackQuestRewardMilestone milestone)
    {
        switch (milestone)
        {
            case BackpackQuestRewardMilestone.HospitalArrival:
                return HospitalBackpackLevel;
            case BackpackQuestRewardMilestone.MilitaryBaseEntry:
                return MilitaryBackpackLevel;
            default:
                return 0;
        }
    }

    public static bool IsRewardLevel(int level) => level == HospitalBackpackLevel ||
                                                   level == MilitaryBackpackLevel;

    /// <summary>
    /// Returns a compact per-player claim bit. Zero means that the requested
    /// level is not a quest milestone and cannot be claimed.
    /// </summary>
    public static int GetClaimBit(int level)
    {
        return level == HospitalBackpackLevel ? 1 :
            level == MilitaryBackpackLevel ? 2 : 0;
    }

    public static bool IsClaimed(int claimMask, int level)
    {
        int bit = GetClaimBit(level);
        return bit != 0 && (claimMask & bit) != 0;
    }

    public static int MarkClaimed(int claimMask, int level)
    {
        int bit = GetClaimBit(level);
        return bit == 0 ? claimMask : claimMask | bit;
    }
}
