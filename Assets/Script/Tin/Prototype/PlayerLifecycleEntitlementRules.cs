using System;

public static class SpawnFlashlightGrantRules
{
    public const int PendingSlot = -1;

    /// <summary>Hotbar first, then usable backpack storage; pending when every usable slot is occupied.</summary>
    public static int FindGrantSlot(bool[] occupiedSlots, int usableSlotCount, int hotbarSlotCount)
    {
        if (occupiedSlots == null || usableSlotCount <= 0) return PendingSlot;
        int usable = Math.Min(usableSlotCount, occupiedSlots.Length);
        int hotbar = Math.Min(Math.Max(0, hotbarSlotCount), usable);

        for (int i = 0; i < hotbar; i++)
            if (!occupiedSlots[i]) return i;
        for (int i = hotbar; i < usable; i++)
            if (!occupiedSlots[i]) return i;
        return PendingSlot;
    }
}

/// <summary>
/// Durable session entitlement owned outside a replaceable avatar. Claim and
/// presentation masks are deliberately separate so reconciliation can restore
/// capacity without replaying a reward reveal.
/// </summary>
public sealed class PlayerLifecycleEntitlementRecord
{
    public int QuestBackpackLevel { get; private set; }
    public int QuestBackpackClaimMask { get; private set; }
    public int BackpackPresentationReceiptMask { get; private set; }

    public void MergeQuestBackpackState(int level, int claimMask, int presentationReceiptMask)
    {
        QuestBackpackLevel = Math.Max(QuestBackpackLevel, Math.Max(0, level));
        QuestBackpackClaimMask |= claimMask;
        BackpackPresentationReceiptMask |= presentationReceiptMask;
    }
}
