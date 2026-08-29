# STATIC DATA-FLOW AUDIT: CORPSE LOOT & TARGETED RPCS

- **Audited Target:** `Assets/Script/Tin/ZombieCorpseLoot.cs` vs `Assets/Khoa/Code/LootContainer.cs` and other Fusion RPCs.
- **Engine / Network Framework:** Unity 6000.0.69f1 / Photon Fusion 2.

---

## 1. Comparison Matrix: RPC Declarations

| RPC Name | File & Line | Target Attribute | Network Packet Destination | Sensitive Data Exposed Over Wire? |
| :--- | :--- | :--- | :--- | :--- |
| `RPC_NotifyLootGranted` | `LootContainer.cs:621` | `[RpcTarget] PlayerRef targetPlayer` | **Unicast** (Only `targetPlayer` socket receives the packet) | **NO** (Secure) |
| `RPC_NotifyLootDenied` | `LootContainer.cs:612` | `[RpcTarget] PlayerRef targetPlayer` | **Unicast** (Only `targetPlayer` socket receives the packet) | **NO** (Secure) |
| `RPC_NotifyQuestClueLooted`| `LootContainer.cs:568` | `[RpcTarget] PlayerRef targetPlayer` | **Unicast** (Only `targetPlayer` socket receives the packet) | **NO** (Secure) |
| `RPC_ShowSchoolClueDialogue`| `MilitaryBaseQuestManager.cs:728` | `[RpcTarget] PlayerRef targetPlayer` | **Unicast** (Only `targetPlayer` socket receives the packet) | **NO** (Secure) |
| `RPC_OpenEyesForLateJoiner` | `HostModeSpawner.cs:632` | `[RpcTarget] PlayerRef targetPlayer` | **Unicast** (Only `targetPlayer` socket receives the packet) | **NO** (Secure) |
| **`RPC_ShowSearchResult`** | **`ZombieCorpseLoot.cs:304`** | **NONE** (`PlayerRef recipient` without `[RpcTarget]`) | **Multicast / Broadcast** (Every peer connected receives the packet) | **YES (Over-broadcast defect)** |

---

## 2. Deep Dive: `ZombieCorpseLoot.RPC_ShowSearchResult`

### 2.1. Declaration & Implementation
```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSearchResult(
    PlayerRef recipient,
    int resultValue,
    string itemId,
    int amount,
    NetworkBool corpseWasSearched)
{
    if (corpseWasSearched) locallyKnownSearched = true;
    if (Runner == null || Runner.LocalPlayer != recipient) return;

    isAwaitingSearchResult = false;
    if (!corpseWasSearched) locallyKnownSearched = false;

    string message = BuildLocalResultMessage((SearchResult)resultValue, itemId, amount);
    AutoChatManager.Instance?.AddSystemMessage(message);
}
```

### 2.2. Data Serialization & Delivery Analysis
1. **At State Authority (Host):**
   - Host generates a packet containing:
     - `recipient`: target `PlayerRef`
     - `resultValue`: integer enum (`SearchResult.Granted = 0`)
     - `itemId`: string (e.g. `"Ammo762"`)
     - `amount`: integer (e.g. `5`)
     - `corpseWasSearched`: boolean (`true`)
   - Because `[RpcTarget]` is absent, Fusion's generated code treats `RpcTargets.All` as a general broadcast.
2. **At Network Transport Layer:**
   - The packet is transmitted over UDP sockets to **ALL connected clients** (Peer A, Peer B, Peer C, ...).
3. **At Peer B (Non-Recipient Client):**
   - Fusion receives the packet, deserializes `itemId = "Ammo762"` and `amount = 5`, and invokes `RPC_ShowSearchResult(...)` on Peer B.
   - Line 312 executes: `if (corpseWasSearched) locallyKnownSearched = true;`.
   - Line 313 executes: `if (Runner.LocalPlayer != recipient) return;` -> Peer B returns here.
   - Line 319 (`AddSystemMessage`) is skipped on Peer B.

### 2.3. Conclusion
- **Presentation Layer:** Working as intended (Peer B's chatbox does not display the message).
- **Network Protocol Layer:** **DEFECT**. Sensitive reward data (`itemId`, `amount`) is transmitted in cleartext over the network to non-recipients before being discarded client-side.
