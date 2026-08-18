using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SurvivalBalanceTests
{
    [Test]
    public void ProductionSettingsAssetIsAvailable()
    {
        Type settingsType = RequireType("SurvivalBalanceSettings");
        UnityEngine.Object settings = Resources.Load("SurvivalBalanceSettings", settingsType);
        Assert.That(settings, Is.Not.Null,
            "The shared Resources balance asset must be available to spawned players.");
    }

    [TestCase(0, 0.15f, 0.1875f, 0.225f, 35f, 666.667f, 533.333f)]
    [TestCase(1, 0.20f, 0.25f, 0.30f, 25f, 500f, 400f)]
    [TestCase(2, 0.25f, 0.3125f, 0.375f, 15f, 400f, 320f)]
    public void ProfilesProduceExpectedRatesAndFullBarDurations(int difficulty,
        float expectedHungerRate, float expectedThirstRate, float expectedDamage,
        float expectedGrace, float expectedHungerSeconds, float expectedThirstSeconds)
    {
        Type settingsType = RequireType("SurvivalBalanceSettings");
        object profile = settingsType.GetMethod("GetActiveProfile",
            BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { difficulty });
        Assert.That(profile, Is.Not.Null);

        float hungerRate = ReadProfileField(profile, "HungerDrainRate");
        float thirstRate = ReadProfileField(profile, "ThirstDrainRate");
        Assert.That(hungerRate, Is.EqualTo(expectedHungerRate).Within(0.0001f));
        Assert.That(thirstRate, Is.EqualTo(expectedThirstRate).Within(0.0001f));
        Assert.That(ReadProfileField(profile, "DamagePerSecond"),
            Is.EqualTo(expectedDamage).Within(0.0001f));
        Assert.That(ReadProfileField(profile, "ZeroNeedGraceSeconds"),
            Is.EqualTo(expectedGrace).Within(0.001f));
        Assert.That(100f / hungerRate,
            Is.EqualTo(expectedHungerSeconds).Within(0.01f));
        Assert.That(100f / thirstRate,
            Is.EqualTo(expectedThirstSeconds).Within(0.01f));
    }

    [Test]
    public void ConsumableRestoreNeverOverfillsOrReducesNeed()
    {
        Assert.That(InvokeRule<float>("RestoreNeed", 20f, 100f, 35f), Is.EqualTo(55f));
        Assert.That(InvokeRule<float>("RestoreNeed", 90f, 100f, 35f), Is.EqualTo(100f));
        Assert.That(InvokeRule<float>("RestoreNeed", 20f, 100f, -10f), Is.EqualTo(20f));
    }

    [Test]
    public void WarningTiersUseTwentyFiveTenAndZeroPercentBoundaries()
    {
        Assert.That(InvokeRule<int>("GetBadNeedTier", 0.40f), Is.EqualTo(1));
        Assert.That(InvokeRule<int>("GetBadNeedTier", 0.25f), Is.EqualTo(2));
        Assert.That(InvokeRule<int>("GetBadNeedTier", 0.10f), Is.EqualTo(3));
        Assert.That(InvokeRule<int>("GetBadNeedTier", 0f), Is.EqualTo(4));
    }

    [Test]
    public void WellFedRequiresBothNeedsAndPreservesFourTiers()
    {
        Assert.That(InvokeRule<int>("GetWellFedTier", 1f, 0.79f), Is.Zero);
        Assert.That(InvokeRule<int>("GetWellFedTier", 0.82f, 1f), Is.EqualTo(1));
        Assert.That(InvokeRule<int>("GetWellFedTier", 0.87f, 1f), Is.EqualTo(2));
        Assert.That(InvokeRule<int>("GetWellFedTier", 0.92f, 1f), Is.EqualTo(3));
        Assert.That(InvokeRule<int>("GetWellFedTier", 1f, 1f), Is.EqualTo(4));
    }

    private static Type RequireType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name + " runtime type was not compiled.");
        return type;
    }

    private static float ReadProfileField(object profile, string fieldName)
    {
        FieldInfo field = profile.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Missing balance profile field: " + fieldName);
        return (float)field.GetValue(profile);
    }

    private static T InvokeRule<T>(string methodName, params object[] arguments)
    {
        Type rulesType = RequireType("SurvivalBalanceRules");
        MethodInfo method = rulesType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Missing survival rule: " + methodName);
        return (T)method.Invoke(null, arguments);
    }
}
