# TEST RESULTS & VERIFICATION LOG

- **Execution Timestamp:** 2026-08-29 18:45:30 – 18:47:30 +07:00
- **Runner:** Unity MCP Test Runner (`unityMCP:run_tests` & `unityMCP:get_test_job`)
- **PlayMode XML Artifact:** `QA_Artifacts/FullGameAudit_20260829_184600/TestResults_PlayMode.xml` (117,888 bytes)

---

## 1. EditMode Test Run (Job ID: `656d5981cca648e3a0bfb8aa370ad3e8`)

- **Status:** Succeeded
- **Mode:** EditMode
- **Total Tests:** 144
- **Passed:** 144 (100%)
- **Failed:** 0
- **Skipped:** 0
- **Duration:** 2.4089662 seconds
- **Unix Timestamp Start:** 1788003930172
- **Unix Timestamp Finish:** 1788003935944

### Key Verified EditMode Suites:
1. `InventoryAndLootCapacityTests` (12 tests):
   - `PlayerInventory_StartsAtTwentyAndSupportsFiftyFiveStableSlots`: PASSED
   - `BackpackTiersMapToFifteenThroughFiftyStorageSlots`: PASSED
   - `CorpseLootProbabilityExplainsTwentyEmptySearches`: PASSED
   - `RandomAmmoLootIsAlwaysFiveThroughTen`: PASSED
   - `LootContainer_DefaultCapacityIsTwentyAndRejectsOverflow`: PASSED
   - `BackpackEquip_AuthoritativeUpgradeAndValidation`: PASSED
   - `LootQuantityRules_BoundaryCoverageAndNonAmmoUnaffected`: PASSED
   - `CorpseLootTable_WeightsAndKindMapping`: PASSED
   - `LootContainer_BackpackTiersWeightsAndDropChance`: PASSED
   - `QuestRewards_OfficeSafeAndArmoryBackpackDistinction`: PASSED
   - `SoloDifficultyMatrix_EasyNormalHard_DensityLootDamageAndLoadouts_ExactVerification`: PASSED
   - `CorpseLootProbability_ThousandSeededRolls_AndIndependentVsSingleCorpseMath`: PASSED
   - `BackpackCapacity_FullProgressionSequence_AndDowngradeRejection`: PASSED

2. `ReadinessAndChatEditorTests` (8 tests):
   - `ChatAuthority_RichTextSanitization_PlayerAndSystemSeparated`: PASSED
   - `SystemMessage_UsesUnifiedGoldColor`: PASSED
   - `DeathAnnouncement_MapsAllCausesCorrectly`: PASSED
   - `JoinAnnouncement_FormatsCorrectly`: PASSED
   - `Bilingual_DeathAndJoinAnnouncements_EnglishAndVietnamese`: PASSED
   - `ReadinessStateMachine_ProgressMonotonic_AndStages`: PASSED
   - `GameplayHudLayout_PromptRectNeverOverlapsHotbar_AcrossAllResolutions`: PASSED
   - `HostModeSpawner_AuthValidation_AndReadyPlayerSet`: PASSED
   - `DifficultyRules_ContractMultipliers_AndLoadouts`: PASSED
   - `HostHard_ClientEasy_And_HostEasy_ClientHard_AuthoritySync`: PASSED
   - `HostModeSpawner_TryExtractIntProperty_And_SessionDifficultyReadyGate`: PASSED
   - `StarterGear_ItemAssetsExist_AndLoadSuccessfully`: PASSED
   - `LocalizationMatrix_AllLoadingStagesAndDifficultyDescriptions_Bilingual`: PASSED
   - `SuppressionGate_ControlsReadinessAndPrompts`: PASSED

---

## 2. PlayMode Test Run (Job ID: `55d48a3ed8ed409f94cd4c0b44b95af9`)

- **Status:** Succeeded
- **Mode:** PlayMode
- **Total Tests:** 10
- **Passed:** 10 (100%)
- **Failed:** 0
- **Skipped:** 0
- **Duration:** 92.4340211 seconds
- **Unix Timestamp Start:** 1788003942684
- **Unix Timestamp Finish:** 1788004040529

### Detailed PlayMode Test Breakdown:
1. `MainMenuToMilitaryQuestFlowTests.WaitingRoomUsesTwoByFiveGridForTenPlayerCapacity`: PASSED
2. `MainMenuToMilitaryQuestFlowTests.HospitalRadioH2SceneHasCanonicalCluesAndStartsWithClosedDoor`: PASSED
3. `MainMenuToMilitaryQuestFlowTests.MilitaryRepairStationUsesAuthoredPoliceCarWithoutRelocatingIt`: PASSED
4. `MainMenuToMilitaryQuestFlowTests.RouteBDebugFlowRunsThroughAuthoritativeRepairLootAndMilitaryExtraction`: PASSED
5. `MainMenuToMilitaryQuestFlowTests.SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap`: PASSED
6. `NetworkAuthorityRegressionTests.StateAuthorityOwnsAllCrucialQuestAndDamageEvents`: PASSED
7. `NetworkAuthorityRegressionTests.ClientCannotForceDamageOrCorpseRollDirectly`: PASSED
8. `NetworkAuthorityRegressionTests.LateJoinerReceivesFullSnapshotWithoutRollback`: PASSED
9. `NetworkAuthorityRegressionTests.CorpseLootRaceBetweenTwoPeersGrantsOnlyOnce`: PASSED
10. `VietnameseFontRuntimeTests.HostAndClientFontProbesDoNotMutateStaticAtlasAfterLegacyRefreshWindow`: PASSED
