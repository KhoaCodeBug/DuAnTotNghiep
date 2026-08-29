# STATIC RPC CODEGEN & ATTRIBUTE VERIFICATION

- **Audited Target:** `Assets/Script/Tin/ZombieCorpseLoot.cs` (`RPC_ShowSearchResult`)
- **Framework:** Photon Fusion 2 (Host Mode / IL Weaver)

---

## 1. Updated Method Signature

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSearchResult(
    [RpcTarget] PlayerRef recipient,
    int resultValue,
    string itemId,
    int amount)
{
    if (Runner == null || Runner.LocalPlayer != recipient) return;

    isAwaitingSearchResult = false;

    string message = BuildLocalResultMessage((SearchResult)resultValue, itemId, amount);
    if (!string.IsNullOrEmpty(message))
    {
        AutoChatManager.Instance?.AddSystemMessage(message);
    }
}
```

---

## 2. Verification Points

1. **`[RpcTarget]` Attribute Presence:**
   - Parameter `recipient` is decorated with `[RpcTarget]`.
   - Photon Fusion 2's IL Weaver compiles the RPC as a unicast message routed exclusively to `recipient`'s network connection.
2. **Elimination of `corpseWasSearched` Parameter:**
   - Visual state is decoupled from private item notification.
   - Global visual state is driven by the `[Networked] private NetworkBool HasCorpseBeenSearched { get; set; }` variable.
3. **Consistency with `LootContainer.cs`:**
   - Exact parity with `RPC_NotifyLootGranted([RpcTarget] PlayerRef targetPlayer, string itemId, int amount)` in `Assets/Khoa/Code/LootContainer.cs:622`.
4. **Automated Reflection Test:**
   - Tested by `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature` in `ReadinessAndChatEditorTests.cs` (PASS).
