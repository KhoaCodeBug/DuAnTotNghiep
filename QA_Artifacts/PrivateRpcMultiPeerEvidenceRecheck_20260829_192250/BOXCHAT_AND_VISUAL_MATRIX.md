# BOXCHAT & VISUAL PRESENTATION MATRIX

---

## 1. Matrix by Event and Peer

| Event / Outcome | Peer Role | Expected BoxChat Output | Observed BoxChat Status | Corpse Visual State | Evidence Type |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **A searches corpse with loot (5x Ammo762)** | **Client A (Actor)** | `[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5.` | *Expected by source logic* | Sprite updates to Searched | `STATIC_EXPECTED / LOCAL PRESENTATION` |
| **A searches corpse with loot (5x Ammo762)** | **Client B (Observer)** | *No text rendered* | Filtered by `Runner.LocalPlayer != recipient` | Sprite updates via `HasCorpseBeenSearched` | `PRESENTATION_ONLY (UI Filter)` |
| **A searches empty corpse** | **Client A (Actor)** | `[HỆ THỐNG] Không tìm thấy gì.` | *Expected by source logic* | Sprite updates to Searched | `STATIC_EXPECTED` |
| **A searches empty corpse** | **Client B (Observer)** | *No text rendered* | Filtered by UI | Sprite updates to Searched | `PRESENTATION_ONLY` |
| **A searches with full inventory** | **Client A (Actor)** | `[HỆ THỐNG] Túi đồ đã đầy.` | *Expected by source logic* | Corpse remains unsearched | `STATIC_EXPECTED` |
| **A searches with full inventory** | **Client B (Observer)** | *No text rendered* | Filtered by UI | Corpse remains unsearched | `PRESENTATION_ONLY` |
| **Simultaneous Race (A wins, B loses)** | **Client A (Winner)** | Item reward notification | *Expected by source logic* | Sprite updates to Searched | `UNVERIFIED (No standalone test)` |
| **Simultaneous Race (A wins, B loses)** | **Client B (Loser)** | `[HỆ THỐNG] Xác này đã bị lục soát.` | *Expected by source logic* | Sprite updates to Searched | `UNVERIFIED (No standalone test)` |
| **Late Joiner joins after search** | **Client C (Late Joiner)** | *No chat replay* | *Expected by source logic* | Sprite shows Searched from snapshot | `UNVERIFIED (Dual-peer pending)` |
