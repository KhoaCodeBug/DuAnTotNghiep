# RUNTIME ROUTING EVIDENCE & MULTI-PEER MATRIX

- **Execution Context:** Unity 6000.0.69f1 | Photon Fusion 2 Host Mode
- **Test Assemblies:** `ReadinessAndChatEditorTests`, `InventoryAndLootCapacityTests`, `NetworkAuthorityRegressionTests`

---

## 1. Multi-Peer Receiver Matrix

| Scenario | State Authority (Host) | Client A (Actor / Recipient) | Client B (Observer / Non-Recipient) | Client C (Late Joiner) |
| :--- | :--- | :--- | :--- | :--- |
| **A searches corpse with loot (5x Ammo762)** | Evaluates search, rolls 5x Ammo762, marks `HasCorpseBeenSearched = true`, sends unicast RPC to A. | Receives `RPC_ShowSearchResult`. Local chat logs: `[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5`. | **Does NOT receive RPC packet over transport**. Visual corpse shows searched via `HasCorpseBeenSearched`. | Connects later, receives snapshot with `HasCorpseBeenSearched = true`. No chat replay. |
| **A searches empty corpse** | Consumes corpse, sets `HasCorpseBeenSearched = true`, sends unicast RPC to A. | Receives `RPC_ShowSearchResult(Empty)`. Local chat logs: `Không tìm thấy gì`. | **Does NOT receive RPC packet**. Visual corpse shows searched. | Sees searched corpse. No chat replay. |
| **A searches corpse with full inventory** | Does NOT consume corpse (`HasCorpseBeenSearched = false`), sends unicast RPC to A. | Receives `RPC_ShowSearchResult(InventoryFull)`. Local chat logs: `Túi đồ đã đầy`. `isAwaitingSearchResult` cleared. | **Does NOT receive RPC packet**. Corpse remains available. | Sees unsearched corpse. |
| **Simultaneous search race between A and B** | Grants item to A (first arrival), consumes corpse. Rejects B with `AlreadySearched`. | Receives item reward. | Receives unicast `AlreadySearched` message. Does NOT receive A's item name/amount. | Sees searched corpse. |

---

## 2. Test Execution Breakdown

1. **EditMode Test Run (`1e9aa64923ca48d2b7fd26cce72d1ee1`):**
   - Total: **145 passed (100%)**, duration: `3.41s`.
   - Verified `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature`.
2. **PlayMode Test Run (`2d7520805be94cb3b57d7b08f13fa51e`):**
   - Total: **10 passed (100%)**, duration: `117.72s`.
   - Verified `NetworkAuthorityRegressionTests.CorpseLootRaceBetweenTwoPeersGrantsOnlyOnce`.
   - Verified `NetworkAuthorityRegressionTests.LateJoinerReceivesFullSnapshotWithoutRollback`.
