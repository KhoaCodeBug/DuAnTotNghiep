# STATIC RPC CHECK: SOURCE DECLARATION & FUSION CODEGEN

- **File:** `Assets/Script/Tin/ZombieCorpseLoot.cs`
- **Method:** `RPC_ShowSearchResult`
- **Photon Fusion Version:** Fusion 2.0 (Host Mode)

---

## 1. Source Declaration

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

## 2. Static Analysis Points

1. **Exact 4 Parameters:**
   - Parameter 0: `[RpcTarget] PlayerRef recipient`
   - Parameter 1: `int resultValue`
   - Parameter 2: `string itemId`
   - Parameter 3: `int amount`
2. **Call Sites inside `ZombieCorpseLoot.cs`:**
   - Line 260: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.AlreadySearched, string.Empty, 0);`
   - Line 267: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.PlayerMissing, string.Empty, 0);`
   - Line 273: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.TooFar, string.Empty, 0);`
   - Line 281: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Empty, string.Empty, 0);`
   - Line 287: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryMissing, string.Empty, 0);`
   - Line 294: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InvalidLoot, string.Empty, 0);`
   - Line 301: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryFull, item.name, amount);`
   - Line 306: `RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Granted, item.name, amount);`
3. **Photon Fusion IL Weaver Compilation:**
   - Assembly `Assembly-CSharp.dll` passed IL Weaving with 0 weave errors.
   - Unit test `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature` in `ReadinessAndChatEditorTests.cs` passed.
