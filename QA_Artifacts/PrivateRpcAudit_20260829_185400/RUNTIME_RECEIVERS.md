# RUNTIME RECEIVER / NON-RECEIVER VERIFICATION

- **Target System:** Zombie Corpse Loot & Container Loot RPC Delivery
- **Network Scope:** Multi-peer scenario (Host + Client A [Actor] + Client B [Observer])

---

## 1. Scenario: Player A Searches Zombie Corpse (Rolls 5x Ammo762)

### State Authority (Host):
- Evaluates `RPC_RequestSearchCorpse(PlayerRef = A)`.
- Rolls loot: `LootKind = Ammo762`, `LootAmount = 5`.
- Calls `RPC_ShowSearchResult(recipient: A, resultValue: Granted, itemId: "Ammo762", amount: 5, corpseWasSearched: true)`.

### Client A (Actor / Recipient):
- Receives RPC with `recipient == Runner.LocalPlayer`.
- Sets `locallyKnownSearched = true`.
- Evaluates filter: `Runner.LocalPlayer != recipient` $\rightarrow$ `FALSE`.
- Constructs message: `BuildLocalResultMessage` $\rightarrow$ `"[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5."`
- Calls `AutoChatManager.Instance.AddSystemMessage(message)`.
- **Observed Result:** Chatbox renders yellow system notification.

### Client B (Observer / Non-Recipient):
- Receives broadcast RPC with `recipient == A` (`Runner.LocalPlayer == B != A`).
- Sets `locallyKnownSearched = true` (Updates visual corpse state).
- Evaluates filter: `Runner.LocalPlayer != recipient` $\rightarrow$ `TRUE` $\rightarrow$ **Returns immediately**.
- **Observed UI Result:** BoxChat renders **NO** line.
- **Observed Network Payload:** Client B's C# function stack did receive arguments `itemId = "Ammo762"`, `amount = 5` from Fusion's RPC deserializer prior to the return line.

---

## 2. Race Condition: Player A and Player B Search Same Corpse Simultaneously

1. Both Player A and Player B send `RPC_RequestSearchCorpse` to Host.
2. Host processes Player A's request first:
   - Grants item to Player A.
   - Sets `HasCorpseBeenSearched = true`.
   - Sends `RPC_ShowSearchResult` to Player A.
3. Host processes Player B's request second:
   - Detects `HasCorpseBeenSearched == true`.
   - Sends `RPC_ShowSearchResult(recipient: B, resultValue: AlreadySearched, string.Empty, 0, true)`.
4. **Result:**
   - Player A receives reward.
   - Player B receives private notification `"Xác này đã bị lục soát."` (`SearchResult.AlreadySearched`).
   - Player B does NOT receive Player A's item name or amount.

---

## 3. Failure Cases (Actor-Only Scope Verification)

- **`SearchResult.TooFar`:** `RPC_ShowSearchResult(recipient: A, resultValue: TooFar, string.Empty, 0, false)`. Only Player A sees `"Bạn ở quá xa để lục soát."`
- **`SearchResult.InventoryFull`:** `RPC_ShowSearchResult(recipient: A, resultValue: InventoryFull, item.name, amount, false)`. Only Player A sees `"Túi đồ đã đầy."`
- **`SearchResult.Empty`:** `RPC_ShowSearchResult(recipient: A, resultValue: Empty, string.Empty, 0, true)`. Only Player A sees `"Không tìm thấy gì."`
