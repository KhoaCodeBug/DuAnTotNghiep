using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameAudioSettingsTests
{
    private static Type SettingsType =>
        Type.GetType("GameAudioSettings, Assembly-CSharp") ??
        throw new InvalidOperationException("GameAudioSettings was not found in Assembly-CSharp.");

    private static float GetProperty<floatType>(string propName)
    {
        PropertyInfo prop = SettingsType.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prop, $"Property {propName} must exist.");
        return (float)prop.GetValue(null);
    }

    private static void InvokeMethod(string methodName, params object[] args)
    {
        MethodInfo method = SettingsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, $"Method {methodName} must exist.");
        method.Invoke(null, args);
    }

    [SetUp]
    public void Setup()
    {
        InvokeMethod("ResetToDefaults");
    }

    [TearDown]
    public void TearDown()
    {
        InvokeMethod("Revert");
    }

    [Test]
    public void DefaultVolumes_MatchExpectedValues()
    {
        InvokeMethod("ResetToDefaults");
        Assert.AreEqual(1.0f, GetProperty<float>("MasterVolume"), 0.001f);
        Assert.AreEqual(0.5f, GetProperty<float>("MusicVolume"), 0.001f);
        Assert.AreEqual(0.8f, GetProperty<float>("SFXVolume"), 0.001f);

        Assert.AreEqual(0.5f, GetProperty<float>("EffectiveMusicVolume"), 0.001f);
        Assert.AreEqual(0.8f, GetProperty<float>("EffectiveSFXVolume"), 0.001f);
    }

    [Test]
    public void MasterVolume_ScalesEffectiveVolumes()
    {
        InvokeMethod("SetMasterVolume", 0.5f);
        InvokeMethod("SetMusicVolume", 0.8f);
        InvokeMethod("SetSFXVolume", 0.6f);

        Assert.AreEqual(0.5f, GetProperty<float>("MasterVolume"), 0.001f);
        Assert.AreEqual(0.8f, GetProperty<float>("MusicVolume"), 0.001f);
        Assert.AreEqual(0.6f, GetProperty<float>("SFXVolume"), 0.001f);

        // Effective = Master * Category
        Assert.AreEqual(0.4f, GetProperty<float>("EffectiveMusicVolume"), 0.001f);
        Assert.AreEqual(0.3f, GetProperty<float>("EffectiveSFXVolume"), 0.001f);
    }

    [Test]
    public void MusicAndSFX_AreIndependent()
    {
        InvokeMethod("SetMasterVolume", 1.0f);
        InvokeMethod("SetMusicVolume", 0.2f);
        InvokeMethod("SetSFXVolume", 0.9f);

        Assert.AreEqual(0.2f, GetProperty<float>("MusicVolume"), 0.001f);
        Assert.AreEqual(0.9f, GetProperty<float>("SFXVolume"), 0.001f);

        InvokeMethod("SetMusicVolume", 0.7f);
        Assert.AreEqual(0.7f, GetProperty<float>("MusicVolume"), 0.001f);
        Assert.AreEqual(0.9f, GetProperty<float>("SFXVolume"), 0.001f);
    }

    [Test]
    public void SaveAndRevert_PreserveSavedPlayerPrefs()
    {
        InvokeMethod("SetPreview", 0.75f, 0.45f, 0.65f);
        InvokeMethod("Save");

        Assert.AreEqual(0.75f, PlayerPrefs.GetFloat("GameMasterVolume", 1.0f), 0.001f);
        Assert.AreEqual(0.45f, PlayerPrefs.GetFloat("GameMusicVolume", 0.5f), 0.001f);
        Assert.AreEqual(0.65f, PlayerPrefs.GetFloat("GameSFXVolume", 0.8f), 0.001f);

        // Modify in preview
        InvokeMethod("SetPreview", 0.2f, 0.2f, 0.2f);
        Assert.AreEqual(0.2f, GetProperty<float>("MasterVolume"), 0.001f);

        // Revert restores saved PlayerPrefs
        InvokeMethod("Revert");
        Assert.AreEqual(0.75f, GetProperty<float>("MasterVolume"), 0.001f);
        Assert.AreEqual(0.45f, GetProperty<float>("MusicVolume"), 0.001f);
        Assert.AreEqual(0.65f, GetProperty<float>("SFXVolume"), 0.001f);
    }
}
