# STATIC RPC CODEGEN & METHOD SIGNATURE VERIFICATION

- **Audited Target:** `Assets/Script/Tin/ZombieCorpseLoot.cs` (`RPC_ShowSearchResult`)
- **Framework:** Photon Fusion 2 (Host Mode / IL Weaver)

---

## 1. Static Verification Checklist

1. **Single Declaration:**
   - There is exactly ONE declaration of `RPC_ShowSearchResult` in the repository (in `ZombieCorpseLoot.cs:317-333`).
2. **First Parameter Decoration:**
   - Parameter `recipient` is explicitly decorated with `[RpcTarget]`:
     ```csharp
     [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
     private void RPC_ShowSearchResult(
         [RpcTarget] PlayerRef recipient,
         int resultValue,
         string itemId,
         int amount)
     ```
3. **Removal of `corpseWasSearched` Parameter:**
   - The parameter `corpseWasSearched` has been completely removed from the declaration and all 8 call sites.
   - Global corpse visual state is decoupled and driven exclusively by `[Networked] private NetworkBool HasCorpseBeenSearched { get; set; }`.
4. **Call Site Consistency:**
   - Exactly 8 call sites inside `ZombieCorpseLoot.cs` (lines 260, 267, 273, 281, 287, 294, 301, 306), all passing exactly 4 arguments (`requestingPlayer, resultValue, itemId, amount`).
5. **Pattern Parity with `LootContainer.cs`:**
   - Matches the standard pattern established in `Assets/Khoa/Code/LootContainer.cs:621`:
     `RPC_NotifyLootGranted([RpcTarget] PlayerRef targetPlayer, string itemId, int amount)`
6. **Compilation & Weaving:**
   - Photon Fusion IL Weaver processed the assembly successfully with 0 errors.
   - Reflection unit test `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature` in `ReadinessAndChatEditorTests.cs` passed.
