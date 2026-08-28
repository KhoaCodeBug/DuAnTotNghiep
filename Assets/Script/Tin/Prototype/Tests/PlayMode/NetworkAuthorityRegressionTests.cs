using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NetworkAuthorityRegressionTests
{
    private static Type ResolveGameType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Could not resolve runtime type '{name}'.");
        return type;
    }

    [Test]
    public void HostModeSpawner_Rejects_Spoofed_Spawn_And_Loading_Ack()
    {
        Type spawnerType = ResolveGameType("HostModeSpawner");
        MethodInfo authenticate = spawnerType.GetMethod("TryResolveAuthenticatedPlayer",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(authenticate, Is.Not.Null);

        Type playerRefType = ResolveGameType("Fusion.PlayerRef");
        MethodInfo fromIndex = playerRefType.GetMethod("FromIndex", BindingFlags.Public | BindingFlags.Static);
        object attacker = fromIndex.Invoke(null, new object[] { 0 });
        object victim = fromIndex.Invoke(null, new object[] { 1 });
        object[] spoofedArguments = { victim, attacker, null };

        Assert.That((bool)authenticate.Invoke(null, spoofedArguments), Is.False,
            "A remote source must not claim another PlayerRef.");

        foreach (string rpcName in new[] { "RPC_RequestSpawn", "RPC_PlayerFinishedLoadingMap" })
        {
            MethodInfo rpc = spawnerType.GetMethod(rpcName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(rpc, Is.Not.Null);
            Assert.That(rpc.GetParameters().Last().ParameterType.FullName, Is.EqualTo("Fusion.RpcInfo"),
                $"{rpcName} must receive RpcInfo so it can authenticate info.Source.");
        }

        object[] honestArguments = { attacker, attacker, null };
        Assert.That((bool)authenticate.Invoke(null, honestArguments), Is.True);
        Assert.That(honestArguments[2], Is.EqualTo(attacker));
    }

    [Test]
    public void HostModeSpawner_Readiness_Deduplicates_Acks_Correctly()
    {
        Type spawnerType = ResolveGameType("HostModeSpawner");
        MethodInfo register = spawnerType.GetMethod("RegisterReadyPlayer",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(register, Is.Not.Null);

        Type playerRefType = ResolveGameType("Fusion.PlayerRef");
        object readyPlayers = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(playerRefType));
        object client = playerRefType.GetMethod("FromIndex", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { 0 });
        int accepted = 0;
        for (int i = 0; i < 5; i++)
            if ((bool)register.Invoke(null, new object[] { readyPlayers, client })) accepted++;

        Assert.That(accepted, Is.EqualTo(1));
        Assert.That((int)readyPlayers.GetType().GetProperty("Count").GetValue(readyPlayers), Is.EqualTo(1));
    }

    [Test]
    public void ArrivalStoryBootstrap_Provides_Ten_Unique_Spawn_Positions()
    {
        Type bootstrapType = ResolveGameType("MainArrivalStoryBootstrap");
        MethodInfo getOffset = bootstrapType.GetMethod("GetInitialSpawnOffset",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(getOffset, Is.Not.Null);

        Vector2[] positions = Enumerable.Range(0, 10)
            .Select(slot => (Vector2)getOffset.Invoke(null, new object[] { slot }))
            .ToArray();

        for (int i = 0; i < positions.Length; i++)
        for (int j = i + 1; j < positions.Length; j++)
            Assert.That(Vector2.Distance(positions[i], positions[j]), Is.GreaterThan(0.8f),
                $"Spawn slots {i} and {j} overlap.");
    }

    [Test]
    public void Bandage_Request_Identifies_Wound_And_Consumes_Single_Item()
    {
        Type playerHealthType = ResolveGameType("PlayerHealth");
        Type woundType = playerHealthType.GetNestedType("NetworkWoundState", BindingFlags.Public);
        MethodInfo applyBandage = playerHealthType.GetMethod("TryApplyBandage",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(woundType, Is.Not.Null);
        Assert.That(applyBandage, Is.Not.Null);

        object wound = Activator.CreateInstance(woundType);
        woundType.GetField("InjuryMask").SetValue(wound, 1);
        int consumed = 0;
        Func<bool> consumeOne = () => { consumed++; return true; };
        object[] arguments = { wound, consumeOne };

        Assert.That((bool)applyBandage.Invoke(null, arguments), Is.True);
        Assert.That(consumed, Is.EqualTo(1));
        object updatedWound = arguments[0];
        object networkBool = woundType.GetField("IsBandaged").GetValue(updatedWound);
        int encodedBool = (int)networkBool.GetType()
            .GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(networkBool);
        Assert.That(encodedBool, Is.Not.EqualTo(0));

        object[] duplicateArguments = { updatedWound, consumeOne };
        Assert.That((bool)applyBandage.Invoke(null, duplicateArguments), Is.False);
        Assert.That(consumed, Is.EqualTo(1), "A duplicate request must not consume another item.");
    }
}
