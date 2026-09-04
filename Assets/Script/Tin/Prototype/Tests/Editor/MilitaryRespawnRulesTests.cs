using NUnit.Framework;
using UnityEngine;

public sealed class MilitaryRespawnRulesTests
{
    [Test]
    public void SoloGatePoolUsesSelectedDifficultyDeadline()
    {
        float fullAssaultDamagePerSecond = MilitaryQuestRules.GateDamagePerHit *
            MilitaryQuestRules.GateMaxHitsPerSecond;
        Assert.That(MilitaryQuestRules.GetSoloGateHoldSeconds(0), Is.EqualTo(360f));
        Assert.That(MilitaryQuestRules.GetSoloGateHoldSeconds(1), Is.EqualTo(300f));
        Assert.That(MilitaryQuestRules.GetSoloGateHoldSeconds(2), Is.EqualTo(240f));
        for (int difficulty = 0; difficulty <= 2; difficulty++)
        {
            float deadline = MilitaryQuestRules.GetSoloGateHoldSeconds(difficulty);
            float soloPool = MilitaryQuestRules.ComputeSiegeGateMaxHealthForDifficulty(1, difficulty);
            Assert.That(soloPool, Is.EqualTo(fullAssaultDamagePerSecond * deadline).Within(0.001f));
        }
    }

    [Test]
    public void SoloGateDpsDoesNotStartDepletedAndReachesZeroExactlyAtDeadline()
    {
        float pool = MilitaryQuestRules.ComputeSiegeGateMaxHealthForDifficulty(1, 1);
        float deadline = MilitaryQuestRules.SoloGateHoldSecondsNormal;
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsedForDifficulty(pool, 0f, 1), Is.EqualTo(pool).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsedForDifficulty(pool, deadline * 0.5f, 1),
            Is.EqualTo(pool * 0.5f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsedForDifficulty(pool, deadline, 1), Is.EqualTo(0f).Within(0.001f));
        Assert.That(MilitaryQuestRules.GetSoloGateHealthAtElapsedForDifficulty(pool, deadline + 30f, 1),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void MultiplayerGateUsesApprovedTeamTierHealth()
    {
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(2),
            Is.EqualTo(7500f));
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(4),
            Is.EqualTo(7500f));
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(5),
            Is.EqualTo(9000f));
        Assert.That(MilitaryQuestRules.ComputeSiegeGateMaxHealth(10),
            Is.EqualTo(9000f));
    }

    [Test]
    public void TeamRespawnPoolScalesForOneToTenPlayersAndClampsAtZero()
    {
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(1), Is.Zero);
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(2), Is.EqualTo(3));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(4), Is.EqualTo(3));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(5), Is.EqualTo(5));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(6), Is.EqualTo(5));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(7), Is.EqualTo(6));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(8), Is.EqualTo(6));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(9), Is.EqualTo(8));
        Assert.That(MilitaryQuestRules.ComputeTeamRespawnCharges(10), Is.EqualTo(8));

        int charges = MilitaryQuestRules.ComputeTeamRespawnCharges(10);
        charges = MilitaryQuestRules.ConsumeTeamRespawnCharge(charges);
        Assert.That(charges, Is.EqualTo(7));
        for (int i = 0; i < 7; i++)
            charges = MilitaryQuestRules.ConsumeTeamRespawnCharge(charges);
        Assert.That(charges, Is.EqualTo(0));
        // An exhausted pool can never go negative.
        Assert.That(MilitaryQuestRules.ConsumeTeamRespawnCharge(charges), Is.EqualTo(0));
    }

    [Test]
    public void TeamRespawnsAreMultiplayerOnly()
    {
        Assert.That(MilitaryQuestRules.CanUseTeamRespawn(true,
            MilitaryQuestRules.ComputeTeamRespawnCharges(4)), Is.False);
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
