using System;
using System.Reflection;
using NUnit.Framework;

public class GameplayAudioPolicyTests
{
    private static Type SpatializerType =>
        Type.GetType("GameplayAudioSpatializer, Assembly-CSharp") ??
        throw new InvalidOperationException("GameplayAudioSpatializer was not found in Assembly-CSharp.");

    private static Type PlayerCueType =>
        SpatializerType.GetNestedType("PlayerCue", BindingFlags.Public) ??
        throw new InvalidOperationException("GameplayAudioSpatializer.PlayerCue was not found.");

    private static Type ProfileType =>
        SpatializerType.GetNestedType("Profile", BindingFlags.Public) ??
        throw new InvalidOperationException("GameplayAudioSpatializer.Profile was not found.");

    [Test]
    public void RemoteAvatar_AllowsOnlyDeathAndGunshot()
    {
        foreach (string cueName in Enum.GetNames(PlayerCueType))
        {
            bool actual = InvokeShouldPlay(cueName, false);
            bool expected = cueName == "Death" || cueName == "Gunshot";
            Assert.AreEqual(expected, actual, $"Unexpected remote policy for {cueName}.");
        }
    }

    [Test]
    public void OwnedAvatar_KeepsEveryCue()
    {
        foreach (string cueName in Enum.GetNames(PlayerCueType))
            Assert.IsTrue(InvokeShouldPlay(cueName, true), $"Owner lost {cueName} feedback.");
    }

    [Test]
    public void AuthoritativeGunshot_DoesNotEchoAfterClientPrediction()
    {
        MethodInfo method = SpatializerType.GetMethod("ShouldPlayAuthoritativeCue", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        object gunshot = Enum.Parse(PlayerCueType, "Gunshot");

        Assert.IsFalse((bool)method.Invoke(null, new[] { gunshot, (object)true, false }),
            "A client owner must suppress the authority echo after local prediction.");
        Assert.IsTrue((bool)method.Invoke(null, new[] { gunshot, (object)false, false }),
            "A teammate must hear the authoritative gunshot.");
        Assert.IsTrue((bool)method.Invoke(null, new[] { gunshot, (object)true, true }),
            "A Host owner has no separate client prediction and must hear the authoritative gunshot.");
    }

    [TestCase(3f, 1.00f)]
    [TestCase(10f, 0.72f)]
    [TestCase(20f, 0.42f)]
    [TestCase(32f, 0.20f)]
    [TestCase(48f, 0.00f)]
    public void GunshotAttenuation_MatchesLockedAnchors(float distance, float expected)
    {
        Assert.AreEqual(expected, InvokeGunshotAttenuation(distance), 0.0001f);
    }

    [Test]
    public void GunshotAttenuation_DecreasesMonotonicallyAcrossAudibleRange()
    {
        float previous = InvokeGunshotAttenuation(0f);
        for (int distance = 1; distance <= 48; distance++)
        {
            float current = InvokeGunshotAttenuation(distance);
            Assert.LessOrEqual(current, previous + 0.0001f, $"Volume increased at {distance}m.");
            previous = current;
        }
    }

    private static bool InvokeShouldPlay(string cueName, bool isOwnedAvatar)
    {
        MethodInfo method = SpatializerType.GetMethod("ShouldPlayPlayerCue", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        object cue = Enum.Parse(PlayerCueType, cueName);
        return (bool)method.Invoke(null, new[] { cue, (object)isOwnedAvatar });
    }

    private static float InvokeGunshotAttenuation(float distance)
    {
        MethodInfo method = SpatializerType.GetMethod("GetAttenuation", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        object gunshot = Enum.Parse(ProfileType, "Gunshot");
        return (float)method.Invoke(null, new[] { gunshot, (object)distance });
    }
}
