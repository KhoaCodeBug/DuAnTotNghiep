# BOXCHAT & VISUAL PRESENTATION MATRIX

---

## 1. Message & Visual Presentation by Peer

| Event / Outcome | Peer Role | Expected BoxChat Output | Actual BoxChat Observed | Corpse Visual State | Scope Classification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **A searches corpse with loot (5x Ammo762)** | **Client A (Actor)** | `[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5.` | `[HỆ THỐNG] Đã nhận được: Đạn 7.62mm x5.` | Sprite updates to Searched | `PRIVATE_SELF` |
| **A searches corpse with loot (5x Ammo762)** | **Client B (Observer)** | *No text rendered* | *No text rendered* (UI filter pass) | Sprite updates to Searched (`HasCorpseBeenSearched == true`) | `PRESENTATION ONLY / GLOBAL STATE` |
| **A searches empty corpse** | **Client A (Actor)** | `[HỆ THỐNG] Không tìm thấy gì.` | `[HỆ THỐNG] Không tìm thấy gì.` | Sprite updates to Searched | `PRIVATE_SELF` |
| **A searches empty corpse** | **Client B (Observer)** | *No text rendered* | *No text rendered* | Sprite updates to Searched | `PRESENTATION ONLY` |
| **A searches with full inventory** | **Client A (Actor)** | `[HỆ THỐNG] Túi đồ đã đầy.` | `[HỆ THỐNG] Túi đồ đã đầy.` | Corpse remains unsearched | `PRIVATE_SELF` |
| **A searches with full inventory** | **Client B (Observer)** | *No text rendered* | *No text rendered* | Corpse remains unsearched | `PRESENTATION ONLY` |
| **Simultaneous Race (A wins, B loses)** | **Client A (Winner)** | Item reward notification | Item reward notification | Sprite updates to Searched | `PRIVATE_SELF` |
| **Simultaneous Race (A wins, B loses)** | **Client B (Loser)** | `[HỆ THỐNG] Xác này đã bị lục soát.` | `[HỆ THỐNG] Xác này đã bị lục soát.` | Sprite updates to Searched | `PRIVATE_SELF` |
| **Late Joiner joins after search** | **Client C (Late Joiner)** | *No chat replay* | *No chat replay* | Sprite shows Searched from snapshot | `GLOBAL STATE` |
