using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DevCheatReleaseContractEditorTests
{
    private static string ReadSource(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, relativePath);
        Assert.That(File.Exists(path), Is.True, path);
        return File.ReadAllText(path);
    }

    [Test]
    public void ReleaseBuild_KeepsAutoInitAndAllSupportedHotkeys()
    {
        string source = ReadSource("Script/Tin/DevCheatManager.cs");

        Assert.That(source, Does.Contain("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)"));
        Assert.That(source, Does.Contain("var _ = Instance;"));
        Assert.That(source, Does.Not.Contain("DEVELOPMENT_BUILD"),
            "The manager must compile and process input in a non-development Player build.");

        string[] supportedKeys = { "P", "F1", "F6", "F7", "F10", "F11", "F12" };
        foreach (string key in supportedKeys)
            Assert.That(source, Does.Contain("KeyCode." + key), key + " must remain wired in the runtime manager.");
    }

    [Test]
    public void ReleaseBuild_KeepsCheatOnlyQuestHandlersCompiled()
    {
        AssertMethodsHaveNoBuildGuard(
            ReadSource("Script/Tin/MainQuest/MainQuestManager.cs"),
            "DebugTeleportToCurrentObjective", "DebugAdvanceRouteB",
            "AuthorityDebugCollectHospitalRadioKey", "AuthorityDebugAdvanceHospitalRadioStage",
            "DebugUnlockHospitalAndMilitaryMapRegions");
        AssertMethodsHaveNoBuildGuard(
            ReadSource("Script/Tin/MainQuest/MilitaryBaseQuestManager.cs"),
            "DebugAdvanceMilitaryRoute", "DebugTeleportToCurrentObjective",
            "DebugConfirmMilitaryFinale", "DebugHealGate");
        AssertMethodsHaveNoBuildGuard(
            ReadSource("Script/Tin/Prototype/QuestFlowUIPrototype.cs"),
            "DebugUnlockHospitalAndMilitaryMapRegions");
        AssertMethodsHaveNoBuildGuard(
            ReadSource("Script/Tin/Prototype/QuestMapUIPrototype.cs"),
            "DebugRevealHospitalAndMilitaryImmediately");
    }

    [TestCase(false, false, true, TestName = "AuthorityPolicy_OfflineSolo_IsAllowed")]
    [TestCase(true, true, true, TestName = "AuthorityPolicy_HostOrServer_IsAllowed")]
    [TestCase(true, false, false, TestName = "AuthorityPolicy_RegularClient_IsDenied")]
    public void AuthorityPolicy_MatchesHostAuthoritativeContract(
        bool hasRunningRunner, bool isServer, bool expected)
    {
        Type managerType = Type.GetType("DevCheatManager, Assembly-CSharp");
        Assert.That(managerType, Is.Not.Null);
        MethodInfo policy = managerType.GetMethod("IsCheatAuthorityAllowed",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(policy, Is.Not.Null);

        Assert.That((bool)policy.Invoke(null, new object[] { hasRunningRunner, isServer }), Is.EqualTo(expected));

        string source = ReadSource("Script/Tin/DevCheatManager.cs");
        Assert.That(source, Does.Match(@"isGodMode\)[\s\S]{0,300}cachedHealth\.HasStateAuthority"),
            "The continuous God Mode mutation must retain a direct State Authority guard.");
    }

    [Test]
    public void F7_HasNoClientToAuthorityCheatRpc()
    {
        string source = ReadSource("Script/Tin/MainQuest/MainQuestManager.cs");

        Assert.That(source, Does.Not.Contain("RPC_RequestDebugCompleteClueSearch"));
        Assert.That(source, Does.Match(
                @"DebugCompleteClueSearch\(\)[\s\S]{0,500}if\s*\(!HasStateAuthority\)[\s\S]{0,240}return;[\s\S]{0,160}ServerDebugCompleteClueSearch"),
            "F7 must reject a regular client locally and execute only on State Authority.");
    }

    private static void AssertMethodsHaveNoBuildGuard(string source, params string[] methodNames)
    {
        foreach (string methodName in methodNames)
        {
            Assert.That(source, Does.Contain(methodName + "("), methodName + " is missing.");
            Assert.That(source, Does.Not.Match(
                    methodName + @"\([^)]*\)\s*\{\s*#if[^\r\n]*DEVELOPMENT_BUILD"),
                methodName + " must not compile out of a non-development Player build.");
        }
    }
}
