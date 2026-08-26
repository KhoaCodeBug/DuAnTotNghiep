using NUnit.Framework;
using UnityEngine;

public sealed class MilitaryRespawnRulesTests
{
    [Test]
    public void SoloGatePoolReachesZeroAtThreeMinutesUnderFullAssault()
    {
        float soloPool = MilitaryQuestRules.ComputeSiegeGateMaxHealth(1);
        float fullAssaultDamagePerSecond = MilitaryQuestRules.GateDamagePerHit *
            MilitaryQuestRules.GateMaxHitsPerSecond;
        Assert.That(soloPool, Is.EqualTo(fullAssaultDamagePerSecond * MilitaryQuestRules.SoloGateHoldSeconds)
            .Within(0.001f));
        Assert.That(soloPool / fullAssaultDamagePerSecond,
            Is.EqualTo(MilitaryQuestRules.SoloGateHoldSeconds).Within(0.001f));
    }

    [Test]
    public void SoloGateDpsDoesNotStartDepletedAndReachesZeroExactlyAtDeadline()
    {
        float pool = MilitaryQuestRules.ComputeSiegeGateMaxHealth(1);
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsed(pool, 0f), Is.EqualTo(pool).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsed(pool, 90f), Is.EqualTo(pool * 0.5f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsed(pool,
            MilitaryQuestRules.SoloGateHoldSeconds), Is.EqualTo(0f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsed(pool,
            MilitaryQuestRules.SoloGateHoldSeconds + 30f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void MultiplayerGateKeepsCanonicalBaseHealth()
    {
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(2),
            Is.EqualTo(MilitaryQuestRules.BaseGateHealth));
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(4),
            Is.EqualTo(MilitaryQuestRules.BaseGateHealth));
    }

    [Test]
    public void TeamRespawnPoolIsSharedThreeChargesAndClampsAtZero()
    {
        int charges = MilitaryQuestRules.TeamRespawnChargeTotal;
        Assert.That(charges, Is.EqualTo(3));
        charges = MilitaryQuestRules.ConsumeTeamRespawnCharge(charges);
        Assert.That(charges, Is.EqualTo(2));
        charges = MilitaryQuestRules.ConsumeTeamRespawnCharge(charges);
        charges = MilitaryQuestRules.ConsumeTeamRespawnCharge(charges);
        Assert.That(charges, Is.EqualTo(0));
        // An exhausted pool can never go negative.
        Assert.That(MilitaryQuestRules.ConsumeTeamRespawnCharge(charges), Is.EqualTo(0));
    }

    [Test]
    public void TeamRespawnsAreMultiplayerOnly()
    {
        Assert.That(MilitaryQuestRules.CanUseTeamRespawn(true,
            MilitaryQuestRules.TeamRespawnChargeTotal), Is.False);
        Assert.That(MilitaryQuestRules.CanUseTeamRespawn(false, 3), Is.True);
        Assert.That(MilitaryQuestRules.CanUseTeamRespawn(false, 1), Is.True);
        Assert.That(MilitaryQuestRules.CanUseTeamRespawn(false, 0), Is.False);
    }

    [Test]
    public void RespawnWaitsTenSecondsAfterDeath()
    {
        Assert.That(MilitaryQuestRules.IsRespawnDelayElapsed(0f), Is.False);
        Assert.That(MilitaryQuestRules.IsRespawnDelayElapsed(9.99f), Is.False);
        Assert.That(MilitaryQuestRules.IsRespawnDelayElapsed(
            MilitaryQuestRules.RespawnDelaySeconds), Is.True);
        Assert.That(MilitaryQuestRules.RespawnDelaySeconds, Is.EqualTo(10f));
    }

    [Test]
    public void RemainingRespawnSecondsCountDownAndClampAtZero()
    {
        Assert.That(MilitaryQuestRules.GetRemainingRespawnSeconds(0f), Is.EqualTo(10f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetRemainingRespawnSeconds(4f), Is.EqualTo(6f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetRemainingRespawnSeconds(12f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GateDamageRuleStillClampsAtZeroForAnyInput()
    {
        Assert.That(MilitaryQuestRules.ApplyGateDamage(100f, MilitaryQuestRules.GateDamagePerHit),
            Is.EqualTo(88f));
        Assert.That(MilitaryQuestRules.ApplyGateDamage(5f, 50f), Is.EqualTo(0f));
        // Negative damage never heals the gate.
        Assert.That(MilitaryQuestRules.ApplyGateDamage(5f, -20f), Is.EqualTo(5f));
    }
}
