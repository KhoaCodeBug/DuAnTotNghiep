# RUNTIME TEST RESULTS: PRIVATE RPC VERIFICATION

- **Context:** Unity 6000.0.69f1 | Photon Fusion 2 Host Mode
- **Test Runs:** 3 iterations for local actor flow; dual-process transport marked UNVERIFIED.

---

## 1. Test Runs Summary

| Case ID | Run # | Action & Target Item | State Authority Result | Client A (Actor) UI | Client B (Observer) UI | Transport Proof Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Case A (Private Loot)** | Run 1 | Corpse Loot (5x Ammo762) | Granted (5x Ammo762) | BoxChat: `[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case A (Private Loot)** | Run 2 | Corpse Loot (1x Bandage) | Granted (1x Bandage) | BoxChat: `[HỆ THỐNG] Đã nhận được: Băng gạc x1.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case A (Private Loot)** | Run 3 | Corpse Loot (1x Water) | Granted (1x Water) | BoxChat: `[HỆ THỐNG] Đã nhận được: Nước uống x1.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case B (Empty Corpse)** | Run 1 | Empty Corpse (LootKind=0) | Consumed, Empty | BoxChat: `[HỆ THỐNG] Không tìm thấy gì.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case B (Full Inventory)** | Run 1 | 55 slots full, try loot | Rejected, InventoryFull | BoxChat: `[HỆ THỐNG] Túi đồ đã đầy.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case B (Too Far)** | Run 1 | Range > 0.5m | Rejected, TooFar | BoxChat: `[HỆ THỐNG] Bạn ở quá xa để lục soát.` | No chat text | **PASS (STATIC/CODEGEN & LOCAL UI)**; Transport on B: **UNVERIFIED** |
| **Case C (Race Condition)** | Run 1 | A and B search same corpse | A granted, B rejected (AlreadySearched) | A receives item message | B receives AlreadySearched | **UNVERIFIED (STANDALONE TEST PENDING)** |
| **Case D (Late Join)** | Run 1 | Join after corpse searched | N/A | N/A | Late joiner sees searched corpse, no chat replay | **UNVERIFIED (DUAL-PEER CAPTURE PENDING)** |
