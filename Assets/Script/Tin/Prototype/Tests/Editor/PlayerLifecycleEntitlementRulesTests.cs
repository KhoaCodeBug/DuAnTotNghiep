using NUnit.Framework;

public sealed class PlayerLifecycleEntitlementRulesTests
{
    [Test]
    public void FlashlightGrantUsesFirstEmptyHotbarBeforeBackpack()
    {
        bool[] occupied = { true, true, false, false, false, false, false };
        Assert.That(SpawnFlashlightGrantRules.FindGrantSlot(occupied, 7, 5), Is.EqualTo(2));
    }

    [Test]
    public void FlashlightGrantFallsBackToFirstUsableBackpackSlot()
    {
        bool[] occupied = { true, true, true, true, true, true, false, false };
        Assert.That(SpawnFlashlightGrantRules.FindGrantSlot(occupied, 8, 5), Is.EqualTo(6));
    }

    [Test]
    public void FlashlightGrantRemainsPendingWhenInventoryIsFull()
    {
        bool[] occupied = { true, true, true, true, true, true };
        Assert.That(SpawnFlashlightGrantRules.FindGrantSlot(occupied, 6, 5),
            Is.EqualTo(SpawnFlashlightGrantRules.PendingSlot));
    }

    [Test]
    public void BackpackEntitlementMergesMonotonicallyAndKeepsReceiptSeparate()
    {
        var record = new PlayerLifecycleEntitlementRecord();
        record.MergeQuestBackpackState(4, 0b01, 0b01);
        record.MergeQuestBackpackState(3, 0, 0);
        record.MergeQuestBackpackState(5, 0b10, 0);

        Assert.That(record.QuestBackpackLevel, Is.EqualTo(5));
        Assert.That(record.QuestBackpackClaimMask, Is.EqualTo(0b11));
        Assert.That(record.BackpackPresentationReceiptMask, Is.EqualTo(0b01));
    }
}
