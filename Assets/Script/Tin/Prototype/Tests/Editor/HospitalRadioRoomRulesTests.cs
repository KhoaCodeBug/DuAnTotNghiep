using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HospitalRadioRoomRulesTests
{
    private Type rulesType;
    private Type stageType;
    private Type hospitalStageType;
    private MethodInfo canUseRadio;
    private MethodInfo canOperateRadio;
    private MethodInfo advanceRestore;
    private MethodInfo getSegmentEndSeconds;
    private MethodInfo getThreatSpawnHorizontalOffset;
    private MethodInfo getThreatZombiesPerEntry;
    private MethodInfo createRadioBurstClip;
    private Type investigationRulesType;
    private MethodInfo canOpenDoor;
    private MethodInfo isDoorDiscoverable;
    private MethodInfo isClueAvailable;
    private Type clueRoleType;

    [SetUp]
    public void SetUp()
    {
        rulesType = Type.GetType("HospitalRadioRoomRules, Assembly-CSharp");
        stageType = Type.GetType("MainQuestManager+QuestStage, Assembly-CSharp");
        hospitalStageType = Type.GetType("MainQuestManager+HospitalInvestigationStage, Assembly-CSharp");
        investigationRulesType = Type.GetType("HospitalInvestigationRules, Assembly-CSharp");
        clueRoleType = Type.GetType("HospitalQuestClueRole, Assembly-CSharp");
        Assert.That(rulesType, Is.Not.Null, "HospitalRadioRoomRules must exist in Assembly-CSharp.");
        Assert.That(stageType, Is.Not.Null, "MainQuestManager.QuestStage must exist in Assembly-CSharp.");
        Assert.That(hospitalStageType, Is.Not.Null);
        Assert.That(investigationRulesType, Is.Not.Null);
        Assert.That(clueRoleType, Is.Not.Null);
        canOpenDoor = investigationRulesType.GetMethod("CanOpenDoor", BindingFlags.Public | BindingFlags.Static);
        isDoorDiscoverable = investigationRulesType.GetMethod("IsDoorDiscoverable", BindingFlags.Public | BindingFlags.Static);
        isClueAvailable = investigationRulesType.GetMethod("IsClueAvailable", BindingFlags.Public | BindingFlags.Static);
        canUseRadio = rulesType.GetMethod("CanUseRadio", BindingFlags.Public | BindingFlags.Static);
        canOperateRadio = rulesType.GetMethod("CanOperateRadio", BindingFlags.Public | BindingFlags.Static);
        advanceRestore = rulesType.GetMethod("AdvanceRestore", BindingFlags.Public | BindingFlags.Static);
        getSegmentEndSeconds = rulesType.GetMethod("GetSegmentEndSeconds", BindingFlags.Public | BindingFlags.Static);
        getThreatSpawnHorizontalOffset = rulesType.GetMethod("GetThreatSpawnHorizontalOffset",
            BindingFlags.Public | BindingFlags.Static);
        getThreatZombiesPerEntry = rulesType.GetMethod("GetThreatZombiesPerEntry",
            BindingFlags.Public | BindingFlags.Static);
        Type presentationType = Type.GetType("HospitalRadioMilestonePresentation, Assembly-CSharp");
        createRadioBurstClip = presentationType?.GetMethod("CreateBurstClip",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(canOpenDoor, Is.Not.Null);
        Assert.That(isDoorDiscoverable, Is.Not.Null);
        Assert.That(isClueAvailable, Is.Not.Null);
        Assert.That(canUseRadio, Is.Not.Null);
        Assert.That(canOperateRadio, Is.Not.Null);
        Assert.That(advanceRestore, Is.Not.Null);
        Assert.That(getSegmentEndSeconds, Is.Not.Null);
        Assert.That(getThreatSpawnHorizontalOffset, Is.Not.Null);
        Assert.That(getThreatZombiesPerEntry, Is.Not.Null);
        Assert.That(createRadioBurstClip, Is.Not.Null);
    }

    [Test]
    public void ShiftLogsAreMandatoryAndAvailableOnlyInCanonicalOrder()
    {
        object questStage = Enum.Parse(stageType, "FindCityMap");
        object shiftLogStage = Enum.Parse(hospitalStageType, "FindShiftLog");
        object shiftLog2Stage = Enum.Parse(hospitalStageType, "FindShiftLog2");
        object shiftLogRole = Enum.Parse(clueRoleType, "ShiftLog");
        object shiftLog2Role = Enum.Parse(clueRoleType, "ShiftLog2");

        Assert.That((bool)isClueAvailable.Invoke(null,
            new[] { (object)true, questStage, shiftLogStage, shiftLogRole }), Is.True);
        Assert.That((bool)isClueAvailable.Invoke(null,
            new[] { (object)true, questStage, shiftLogStage, shiftLog2Role }), Is.False);
        Assert.That((bool)isClueAvailable.Invoke(null,
            new[] { (object)true, questStage, shiftLog2Stage, shiftLogRole }), Is.False);
        Assert.That((bool)isClueAvailable.Invoke(null,
            new[] { (object)true, questStage, shiftLog2Stage, shiftLog2Role }), Is.True);
    }

    [Test]
    public void DoorCanBeDiscoveredEarlyButOnlyOpensWithSharedKeyAtUnlockStage()
    {
        object locateOffice = Enum.Parse(stageType, "LocateOffice");
        object findCityMap = Enum.Parse(stageType, "FindCityMap");
        object notStarted = Enum.Parse(hospitalStageType, "NotStarted");
        object findShiftLog = Enum.Parse(hospitalStageType, "FindShiftLog");
        object unlock = Enum.Parse(hospitalStageType, "UnlockRadioRoom");

        Assert.That((bool)isDoorDiscoverable.Invoke(null,
            new[] { (object)true, locateOffice, notStarted, false }), Is.True);
        Assert.That((bool)isDoorDiscoverable.Invoke(null,
            new[] { (object)true, findCityMap, findShiftLog, false }), Is.True);
        Assert.That((bool)canOpenDoor.Invoke(null,
            new[] { (object)true, findCityMap, unlock, false, false }), Is.False);
        Assert.That((bool)canOpenDoor.Invoke(null,
            new[] { (object)true, findCityMap, unlock, false, true }), Is.True);
    }

    [Test]
    public void DoorRejectsUnreadyOpenOrReleaseAccessStates()
    {
        object stage = Enum.Parse(stageType, "FindCityMap");
        object unlock = Enum.Parse(hospitalStageType, "UnlockRadioRoom");
        object findShiftLog2 = Enum.Parse(hospitalStageType, "FindShiftLog2");
        Assert.That((bool)canOpenDoor.Invoke(null, new[] { (object)false, stage, unlock, false, true }), Is.False);
        Assert.That((bool)canOpenDoor.Invoke(null, new[] { (object)true, stage, unlock, true, true }), Is.False);
        Assert.That((bool)canOpenDoor.Invoke(null, new[] { (object)true, stage, findShiftLog2, false, true }), Is.False);
    }

    [Test]
    public void RadioIsImpossibleUntilReplicatedDoorStateIsOpen()
    {
        object questStage = Enum.Parse(stageType, "FindCityMap");
        object unlock = Enum.Parse(hospitalStageType, "UnlockRadioRoom");
        object ready = Enum.Parse(hospitalStageType, "RadioReady");
        Assert.That((bool)canUseRadio.Invoke(null, new[] { (object)true, questStage, unlock, true }), Is.False);
        Assert.That((bool)canUseRadio.Invoke(null, new[] { (object)true, questStage, ready, false }), Is.False);
        Assert.That((bool)canUseRadio.Invoke(null, new[] { (object)true, questStage, ready, true }), Is.True);
    }

    [Test]
    public void RecoveredRadioCannotBeOperatedAgain()
    {
        object questStage = Enum.Parse(stageType, "FindCityMap");
        object ready = Enum.Parse(hospitalStageType, "RadioReady");
        Assert.That((bool)canOperateRadio.Invoke(null,
            new[] { (object)true, questStage, ready, true, false }), Is.True);
        Assert.That((bool)canOperateRadio.Invoke(null,
            new[] { (object)true, questStage, ready, true, true }), Is.False);
    }

    [Test]
    public void RadioProgressPausesAndContinuesAcrossOperatorHandoff()
    {
        float firstOperatorProgress = (float)advanceRestore.Invoke(null, new object[] { 0f, 5f, 14f });
        float pausedProgress = (float)advanceRestore.Invoke(null, new object[] { firstOperatorProgress, 0f, 14f });
        float secondOperatorProgress = (float)advanceRestore.Invoke(null, new object[] { pausedProgress, 9f, 14f });

        Assert.That(firstOperatorProgress, Is.EqualTo(5f));
        Assert.That(pausedProgress, Is.EqualTo(5f), "Releasing E must preserve team progress.");
        Assert.That(secondOperatorProgress, Is.EqualTo(14f), "Another player must be able to finish it.");
    }

    [Test]
    public void FourteenSecondRepairUsesThreeCheckpointsAndDifficultyScaledNaturalSpawns()
    {
        Assert.That((float)getSegmentEndSeconds.Invoke(null, new object[] { 0, 14f }),
            Is.EqualTo(14f / 3f).Within(0.001f));
        Assert.That((float)getSegmentEndSeconds.Invoke(null, new object[] { 1, 14f }),
            Is.EqualTo(28f / 3f).Within(0.001f));
        Assert.That((float)getSegmentEndSeconds.Invoke(null, new object[] { 2, 14f }),
            Is.EqualTo(14f).Within(0.001f));
        Assert.That((int)getThreatZombiesPerEntry.Invoke(null, new object[] { 0 }), Is.EqualTo(3));
        Assert.That((int)getThreatZombiesPerEntry.Invoke(null, new object[] { 1 }), Is.EqualTo(4));
        Assert.That((int)getThreatZombiesPerEntry.Invoke(null, new object[] { 2 }), Is.EqualTo(5));
        Assert.That((float)getThreatSpawnHorizontalOffset.Invoke(null, new object[] { 0, 3, 0.8f }),
            Is.EqualTo(-0.8f).Within(0.001f));
        Assert.That((float)getThreatSpawnHorizontalOffset.Invoke(null, new object[] { 1, 3, 0.8f }),
            Is.EqualTo(0f).Within(0.001f));
        Assert.That((float)getThreatSpawnHorizontalOffset.Invoke(null, new object[] { 2, 3, 0.8f }),
            Is.EqualTo(0.8f).Within(0.001f));
    }

    [Test]
    public void MilestoneStaticRepeatsForTwoFullCycles()
    {
        UnityEngine.AudioClip clip = createRadioBurstClip.Invoke(null, null) as UnityEngine.AudioClip;
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(2.7f).Within(0.01f));
        UnityEngine.Object.DestroyImmediate(clip);
    }

    [Test]
    public void H5KeyChoiceAndThreatStateUseFusionReplicationContracts()
    {
        Type managerType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Assert.That(managerType, Is.Not.Null);
        string[] replicatedProperties =
        {
            "NetworkHospitalInvestigationStage", "HasHospitalRadioKey",
            "SelectedHospitalRadioKeyLootId", "HospitalRadioCheckpointCount",
            "HospitalRadioThreatSpawnCount"
        };
        for (int i = 0; i < replicatedProperties.Length; i++)
        {
            PropertyInfo property = managerType.GetProperty(replicatedProperties[i]);
            Assert.That(property, Is.Not.Null);
            object[] attributes = property.GetCustomAttributes(false);
            bool networked = false;
            for (int attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
                if (attributes[attributeIndex].GetType().FullName == "Fusion.NetworkedAttribute")
                    networked = true;
            Assert.That(networked, Is.True, replicatedProperties[i] + " must replicate to clients/late joiners.");
        }

        MethodInfo keyRequest = managerType.GetMethod("RPC_RequestHospitalRadioKeyLoot",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(keyRequest, Is.Not.Null);
        object[] rpcAttributes = keyRequest.GetCustomAttributes(false);
        bool hasRpcContract = false;
        for (int i = 0; i < rpcAttributes.Length; i++)
            if (rpcAttributes[i].GetType().FullName == "Fusion.RpcAttribute") hasRpcContract = true;
        Assert.That(hasRpcContract, Is.True,
            "Clients must request key pickup through a Fusion RPC validated by State Authority.");
    }

    [Test]
    public void RadioThreeOfThreeMapsToLevelFiveQuestMilestoneReward()
    {
        Type rulesType = Type.GetType("BackpackQuestRewardRules, Assembly-CSharp");
        Assert.That(rulesType, Is.Not.Null);
        Type milestoneType = Type.GetType("BackpackQuestRewardMilestone, Assembly-CSharp");
        Assert.That(milestoneType, Is.Not.Null);

        object radioMilestone = Enum.Parse(milestoneType, "RadioRestoration");
        MethodInfo getRewardLevel = rulesType.GetMethod("GetRewardLevel", BindingFlags.Public | BindingFlags.Static);
        Assert.That((int)getRewardLevel.Invoke(null, new[] { radioMilestone }), Is.EqualTo(5));

        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);
        MethodInfo claimMethod = inventoryType.GetMethod("RequestClaimLevelFiveBackpackReward",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(claimMethod, Is.Not.Null,
            "InventorySystem must provide a durable per-player request method for the level 5 reward.");
    }

    [Test]
    public void EveryActivePlayerGetsPendingLevelFiveClaim_AndForeignPlayerClaimIsRejected()
    {
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);

        GameObject p1Host = new GameObject("Player 1");
        GameObject p2Host = new GameObject("Player 2");
        try
        {
            Component inv1 = p1Host.AddComponent(inventoryType);
            Component inv2 = p2Host.AddComponent(inventoryType);

            MethodInfo tryGrant = inventoryType.GetMethod("TryGrantQuestBackpackReward",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo hasClaimed = inventoryType.GetMethod("HasClaimedQuestBackpackReward",
                BindingFlags.Public | BindingFlags.Instance);

            // Initially neither player has claimed level 5
            Assert.That((bool)hasClaimed.Invoke(inv1, new object[] { 5 }), Is.False);
            Assert.That((bool)hasClaimed.Invoke(inv2, new object[] { 5 }), Is.False);

            // Player 1 claims their reward
            Assert.That((bool)tryGrant.Invoke(inv1, new object[] { 5 }), Is.True);
            Assert.That((bool)hasClaimed.Invoke(inv1, new object[] { 5 }), Is.True);
            Assert.That((bool)hasClaimed.Invoke(inv2, new object[] { 5 }), Is.False,
                "Player 1's claim must not prematurely mark Player 2's milestone as claimed.");

            // Player 2 claims their independent reward
            Assert.That((bool)tryGrant.Invoke(inv2, new object[] { 5 }), Is.True);
            Assert.That((bool)hasClaimed.Invoke(inv2, new object[] { 5 }), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(p1Host);
            UnityEngine.Object.DestroyImmediate(p2Host);
        }
    }

    [Test]
    public void LateJoinOrReconnectingPlayer_ReceivesPendingClaimHandoff_AndClaimsAuthoritativelyOnce()
    {
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);

        GameObject lateJoinerHost = new GameObject("Late Joiner");
        try
        {
            Component inv = lateJoinerHost.AddComponent(inventoryType);
            MethodInfo triggerHandoff = inventoryType.GetMethod("TriggerLateOrPendingRadioBackpackRewardHandoff",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo tryGrant = inventoryType.GetMethod("TryGrantQuestBackpackReward",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo hasClaimed = inventoryType.GetMethod("HasClaimedQuestBackpackReward",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo requestClaim = inventoryType.GetMethod("RequestClaimLevelFiveBackpackReward",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(triggerHandoff, Is.Not.Null, "InventorySystem must expose a durable late-join/reconnect handoff.");

            // Late joiner starts unclaimed
            Assert.That((bool)hasClaimed.Invoke(inv, new object[] { 5 }), Is.False);

            // First claim execution
            int presentationCount = 0;
            Action callback = () => { presentationCount++; };
            requestClaim.Invoke(inv, new object[] { callback });
            Assert.That(presentationCount, Is.EqualTo(1));
            Assert.That((bool)hasClaimed.Invoke(inv, new object[] { 5 }), Is.True);

            // Reconnecting/replaying after claim is already recorded
            requestClaim.Invoke(inv, new object[] { callback });
            Assert.That(presentationCount, Is.EqualTo(1), "Recorded claim must never re-invoke presentation callback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lateJoinerHost);
        }
    }
}
