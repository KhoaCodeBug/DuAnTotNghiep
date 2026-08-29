# MESSAGE & NOTIFICATION ROUTING CONTRACT AUDIT

- **Audited Files:** `Assets/Script/Tin/ZombieCorpseLoot.cs`, `Assets/Khoa/Code/LootContainer.cs`, `Assets/Script/Tin/PlayerHealth.cs`, `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`, `Assets/Script/Tin/MainQuest/MainQuestManager.cs`, `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`, `Assets/Script/Tin/AutoChatManager.cs`

---

## 1. Classification Contract

| Scope | Definition | BoxChat Behavior | Network Replication Rule |
| :--- | :--- | :--- | :--- |
| **`GLOBAL`** | Broadcast to all peers in the session | Rendered in yellow `#FFD54A` on all peer chatboxes | Sent from State Authority to `RpcTargets.All`, localized on each client |
| **`PRIVATE_SELF`** | Only visible to the interacting actor | Rendered only on actor's local UI/BoxChat | Target `PlayerRef` filtered (`Runner.LocalPlayer != recipient` drops packet) |
| **`TARGETED`** | Visible only to specified related players (e.g. revive partner) | Rendered only on targeted recipient's UI | Target `PlayerRef` filtered |
| **`LOCAL_PRESENTATION`** | Local client visual/audio feedback | **Not** posted to BoxChat | Local execution only (audio, animation, floating numbers, prompts) |

---

## 2. Call-Site Audit Ledger

### 2.1. Corpse Looting (`ZombieCorpseLoot.cs`)
- **Call-site:** Line 305: `RPC_ShowSearchResult(PlayerRef recipient, int resultIndex, string itemId, int amount, bool updateVisualState)`
- **Authority:** State Authority (Host) rolls and executes RPC.
- **Client Filter:** Line 316: `if (Runner == null || Runner.LocalPlayer != recipient) return;`
- **Scope Analysis:**
  - `updateVisualState`: Replicated to ALL peers (`GLOBAL` visual state / `LOCAL_PRESENTATION`).
  - `BuildLocalResultMessage` & `AddSystemMessage`: Filtered to `recipient == Runner.LocalPlayer`.
  - **Verdict:** Strictly **`PRIVATE_SELF`**. Other players (Peer B, C) never see what item or amount Player A looted.

### 2.2. Loot Containers (`LootContainer.cs`)
- **Call-site:** Line 709: `RPC_NotifyLootDenied(PlayerRef recipient, string message)`
- **Client Filter:** Line 711: `if (Runner != null && Runner.LocalPlayer != recipient) return;`
- **Scope Analysis:** Strictly **`PRIVATE_SELF`** to recipient only.
- **Backpack Drop in Container:** Notification via `AddSystemMessage` rendered locally for the player who opened/took the container.

### 2.3. Player Death Announcements (`PlayerHealth.cs`)
- **Call-site:** Line 546: `AutoChatManager.Instance?.AddSystemMessage(deathMessage)`
- **Scope Analysis:** **`GLOBAL`**. Broadcast to all peers with victim name, killer name, and death cause in yellow `#FFD54A`.

### 2.4. Player Join Announcements (`HostModeSpawner.cs`)
- **Call-site:** Line 652: `AutoChatManager.Instance?.AddSystemMessage(joinMsg)`
- **Scope Analysis:** **`GLOBAL`**. Broadcast to all peers when player finishes loading.

### 2.5. Story & Clue Progress (`MainQuestManager.cs` & `MilitaryBaseQuestManager.cs`)
- **Call-site:** `AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", ...)`
- **Scope Analysis:** **`GLOBAL`**. Team progression is broadcast to everyone; individual item acquisition remains private.
