using NUnit.Framework;

public sealed class VehicleRepairSkillCheckRulesTests
{
    [Test]
    public void BaseRepairTakesFortyFiveSecondsWithoutSkillChecks()
    {
        float halfway = VehicleRepairSkillCheckRules.AdvanceBaseProgress(0f, 22.5f, 45f);
        Assert.That(halfway, Is.EqualTo(50f).Within(0.001f));
        Assert.That(VehicleRepairSkillCheckRules.AdvanceBaseProgress(halfway, 22.5f, 45f),
            Is.EqualTo(100f).Within(0.001f));
    }

    [Test]
    public void SkillCheckUsesNestedPerfectAndSuccessArcsAcrossZeroDegrees()
    {
        Assert.That(VehicleRepairSkillCheckRules.Evaluate(359f, 1f, 25f, 8f),
            Is.EqualTo(VehicleRepairSkillCheckResult.Perfect));
        Assert.That(VehicleRepairSkillCheckRules.Evaluate(12f, 1f, 25f, 8f),
            Is.EqualTo(VehicleRepairSkillCheckResult.Success));
        Assert.That(VehicleRepairSkillCheckRules.Evaluate(30f, 1f, 25f, 8f),
            Is.EqualTo(VehicleRepairSkillCheckResult.Miss));
    }

    [Test]
    public void SkillCheckBonusesAndMissPenaltyArePercentagePointsAndClamped()
    {
        Assert.That(VehicleRepairSkillCheckRules.ApplyResult(40f,
            VehicleRepairSkillCheckResult.Perfect, 3.5f, 7f, 2f), Is.EqualTo(47f));
        Assert.That(VehicleRepairSkillCheckRules.ApplyResult(40f,
            VehicleRepairSkillCheckResult.Success, 3.5f, 7f, 2f), Is.EqualTo(43.5f));
        Assert.That(VehicleRepairSkillCheckRules.ApplyResult(1f,
            VehicleRepairSkillCheckResult.Miss, 3.5f, 7f, 2f), Is.EqualTo(0f));
        Assert.That(VehicleRepairSkillCheckRules.ApplyResult(98f,
            VehicleRepairSkillCheckResult.Perfect, 3.5f, 7f, 2f), Is.EqualTo(100f));
    }

    [Test]
    public void SuccessZoneStartsOnlyAfterThirtyPercentOfTheCircle()
    {
        float center = VehicleRepairSkillCheckRules.GetMinimumTargetCenterAngle(0.30f, 25f);
        Assert.That(center, Is.EqualTo(120.5f).Within(0.001f));
        Assert.That(center - 12.5f, Is.EqualTo(108f).Within(0.001f));
    }

    [Test]
    public void PoliceCarUsesFiveIndependentRepairActions()
    {
        Assert.That(PoliceCarRepairRules.RequiredActionCount, Is.EqualTo(5));
        Assert.That(PoliceCarRepairRules.TryGetAction("engine", out PoliceCarRepairAction engine), Is.True);
        Assert.That(PoliceCarRepairRules.TryGetAction("hood", out PoliceCarRepairAction hood), Is.True);
        Assert.That(engine, Is.Not.EqualTo(hood));

        int mask = (int)PoliceCarRepairRules.GetStateBit(engine);
        Assert.That(PoliceCarRepairRules.CountApplied(mask), Is.EqualTo(1));
        Assert.That(PoliceCarRepairRules.IsComplete(mask), Is.False);

        foreach (PoliceCarRepairAction action in System.Enum.GetValues(typeof(PoliceCarRepairAction)))
            mask |= (int)PoliceCarRepairRules.GetStateBit(action);
        Assert.That(PoliceCarRepairRules.CountApplied(mask), Is.EqualTo(5));
        Assert.That(PoliceCarRepairRules.IsComplete(mask), Is.True);
    }
}
