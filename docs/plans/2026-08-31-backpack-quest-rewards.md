# Backpack Quest Milestone Rewards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use the `executing-plans` skill to implement this plan task-by-task with review checkpoints.

## Goal

Trao tự động Balo cấp 4 khi người chơi đạt bệnh viện và Balo cấp 5 khi người chơi đi vào khu quân sự sau khi nhiệm vụ bệnh viện hoàn tất. Mỗi người chơi chỉ nhận mỗi mốc một lần trong multiplayer; phần thưởng balo là state cá nhân, được State Authority xác thực và đồng bộ tới đúng owner. Hiệu ứng nhận balo dùng ngôn ngữ hình ảnh kiểu map reveal nhưng là presenter riêng, không đụng vào map fragment/map unlock. Tạo icon runtime dùng ảnh PNG riêng cho cả 5 cấp balo.

## Architecture

- Thêm `BackpackQuestRewardRules` thuần (không phụ thuộc UI/network) để định nghĩa hai mốc: HospitalArrival → cấp 4 và MilitaryBaseEntry → cấp 5, cùng bit claim idempotent.
- Mở rộng `InventorySystem` với claim mask networked và API authority-only để auto-equip balo mốc nhiệm vụ; persist claim mask trong `MilitaryRespawnSnapshot`; không gắn logic này vào việc người chơi nhặt/equip balo loot thông thường.
- `MainQuestManager` gọi claim cấp 4 sau khi server xác nhận vào trigger bệnh viện và cho phép retry hợp lệ cho người chơi multiplayer vào sau.
- `MilitaryBaseQuestManager` kiểm tra collider `KhuVucQuanSu` trên State Authority sau `IsCityMapUnlocked`, rồi claim cấp 5 cho từng player có vị trí authoritative nằm trong vùng.
- RPC presentation chỉ target Input Authority của player vừa nhận; UI overlay riêng có scan pulse/core/card theo nhịp map reveal, nhưng không gọi `QuestMapUIPrototype`, không sửa `IsCityMapUnlocked` và không tạo map reward.
- `BackpackItemCatalog` ưu tiên load `Resources/Backpacks/BackpackLevel1..5.png`, fallback icon procedural nếu asset chưa import được.

## Tech Stack

- Unity 6000.0.69f1, Fusion `NetworkBehaviour`/RPC, uGUI + TextMeshPro.
- NUnit EditMode reflection tests hiện có; PlayMode route-B smoke và network authority regression.
- Generated transparent PNG inventory art, imported under `Assets/Resources/Backpacks/`.

---

## Implementation steps

1. Add failing EditMode assertions for milestone mapping, exact levels 4/5, no map-fragment reward, claim idempotency, and icon resource lookup.
2. Run the focused EditMode tests and record the expected RED failure before production implementation.
3. Implement pure reward rules and inventory claim/snapshot/network synchronization.
4. Wire the hospital trigger and military-area authoritative entry checks; target reward presentation only to the qualifying player.
5. Add the independent backpack reveal presenter, localization strings, gameplay-input suppression, and PNG icon lookup/fallback.
6. Run focused and full EditMode tests, then the relevant PlayMode tests; clear/read Unity Console and verify compile state.
7. Exercise the developer Route B path to capture fresh screenshots for level-4 hospital reward and level-5 military-entry reward, or document any environment limitation.
8. Review diff and serialized/API compatibility, fix any failures, then follow the repository branch → commit → push → merge → return-to-main workflow.

## Acceptance criteria

- Starting at level 0–3 does not receive a backpack quest reward; hospital milestone grants exactly level 4; military entry after hospital completion grants exactly level 5.
- Direct exploit/repeat requests, duplicate trigger callbacks, respawn, reconnect, and late join do not duplicate or downgrade the personal backpack reward.
- In multiplayer, host/state authority owns the claim and capacity mutation; each client sees only its own reward reveal and receives replicated inventory/capacity state.
- Map fragment rewards, map unlock flags, map reveal callbacks, and shared quest state remain unchanged except for the intended trigger points.
- All five catalog levels resolve a project PNG icon; fallback remains available without breaking tests.
- EditMode, PlayMode, console/compile checks, and screenshot evidence are recorded in the final report.
