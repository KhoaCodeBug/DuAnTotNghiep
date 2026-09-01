# Backpack Loot and Radio Level-5 Reward Sequence Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use the `executing-plans` skill to implement this plan task-by-task with review checkpoints.

**Goal:** Chuyển balo cấp 1–2–3 thành loot hiếm có luật multiplayer rõ ràng, bỏ reward balo cấp 2–3 cố định, và trao balo cấp 5 riêng cho từng người chơi theo chuỗi Radio 3/3 → Map → đóng Map → hiệu ứng balo → thông báo nâng cấp.

**Architecture:** State Authority vẫn là nguồn sự thật cho loot, inventory và quest milestone. Loot balo cấp 1–3 dùng một pool riêng với tỉ lệ thấp; mỗi request pickup được kiểm tra theo cấp balo hiện tại của người chơi, vì vậy một người bị từ chối không làm mất item của đồng đội. Mốc Radio 3/3 tạo trạng thái reward chờ theo từng PlayerRef; client chỉ được trình bày balo sau khi chuỗi Map Fragment 2 và map reveal của chính client kết thúc, còn State Authority xác nhận claim cuối cùng.

**Tech Stack:** Unity 6000.0.69f1, Fusion `NetworkBehaviour`/RPC, uGUI/TextMeshPro, NUnit EditMode và PlayMode tests, Unity MCP.

---

## Decisions locked by user

- Loot thường chỉ có backpack level 1, 2 và 3.
- Loot backpack có tỉ lệ thấp; không loot level 4 hoặc level 5.
- Không giữ balo level 2 trong Office Safe.
- Không giữ balo level 3 trong Armory.
- Backpack level 5 là phần thưởng cá nhân, mỗi người chơi nhận riêng.
- Khi Radio hoàn tất đủ 3/3, thứ tự bắt buộc là:

  ```text
  Radio 3/3
      → Map Fragment 2
      → mở/reveal map
      → đóng map hoàn toàn
      → hiệu ứng nhận balo cấp 5
      → hiệu ứng kết thúc
      → thông báo đã nâng cấp balo
  ```

- Hiệu ứng balo chọn Option 1: dùng cùng ngôn ngữ hình ảnh với map reward nhưng là presenter/reward riêng.
- Không tự commit, push, tạo PR hoặc merge nếu người dùng chưa cho phép riêng.

## Current baseline and risks

- `LootContainer` hiện roll backpack với `backpackDropChance = 10%` và `BackpackLootRules` hiện còn pool level 1–5 với trọng số `50/30/15/4/1`.
- Loot container là nguồn loot dùng chung trên State Authority; khi một player lấy thành công, item bị xóa khỏi container cho cả phòng.
- `CanLocalPlayerLootItem` hiện chỉ khóa weapon; backpack thấp vẫn đi qua `AddItem`, sau đó chỉ bị từ chối khi người chơi cố equip.
- `ZombieCorpseLoot` cũng cần kiểm tra backpack trước khi consume corpse; nếu không, corpse có thể bị consume sai khi người chơi đã có balo cao.
- Radio 3/3 hiện gọi callback recording/map; reward level 5 hiện còn được scan khi player đi vào vùng quân sự. Trigger cũ phải bị loại bỏ hoặc trở thành no-op đối với level 5.
- Map flow và backpack flow là hai presentation độc lập. Backpack không được sửa map state, map reward hoặc quyền điều khiển của teammate.
- RPC presentation có thể đến trước UI của client load chậm. Reward state phải bền vững theo PlayerRef, không chỉ dựa vào một RPC tức thời.

## Multiplayer contract

1. State Authority roll item và giữ item trong container/corpse cho đến khi một request hợp lệ được chấp nhận.
2. Với backpack loot, điều kiện nhận là `lootLevel > currentBackpackLevel`.
3. Nếu `lootLevel <= currentBackpackLevel`, State Authority gửi denial riêng cho requester và không remove item.
4. Một player bị từ chối không ảnh hưởng tới quyền loot của player khác có balo thấp hơn.
5. Balo quest level 5 được claim theo từng `PlayerRef`; không dùng chung một claim bit cho cả team.
6. Completion của Radio là shared quest state, nhưng map presentation, inventory upgrade và backpack presentation là per-player.
7. Player load chậm/late join không được mất reward: State Authority giữ pending milestone và client tiếp tục chuỗi khi đã có UI/network object.
8. Nếu người chơi đã có level 5 do dữ liệu cũ, không downgrade; claim vẫn idempotent và presentation không được lặp vô hạn.

## Implementation tasks

### Task 1: Add failing regression tests first

**Files:**

- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/InventoryAndLootCapacityTests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/HospitalRadioRoomRulesTests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/QuestFlowUIPrototypeTests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/PlayMode/MainMenuToMilitaryQuestFlowTests.cs`

**Steps:**

1. Add an EditMode test that asserts ordinary loot can roll only levels 1–3 and exposes the three configured weights/percentages.
2. Add an EditMode test for the backpack pickup predicate: level 0 can accept level 1; level 2 cannot accept level 1 or 2; level 2 can accept level 3; no downgrade is allowed.
3. Add an EditMode test that Office Safe and Armory rewards no longer contain a backpack item while retaining their non-backpack rewards.
4. Add tests that the level-5 quest milestone is mapped to Radio completion, not MilitaryBaseEntry.
5. Add tests for the per-player pending claim state and duplicate/reconnect/late-join idempotence.
6. Add a UI test that map overlay is closed before the backpack presenter becomes visible, and the upgrade notification is emitted only after the backpack animation callback.
7. Run focused EditMode tests before production changes. Expected result: new assertions fail against the current implementation.

### Task 2: Restrict ordinary backpack loot to levels 1–3

**Files:**

- Modify: `Assets/Script/Tin/ZombieCorpseLoot.cs:131-156`
- Modify: `Assets/Khoa/Code/LootContainer.cs:44-47,153-160`

**Steps:**

1. Change the ordinary backpack tier pool from levels 1–5 to levels 1–3 only.
2. Use an explicit low-rate configuration. Initial proposal: 5% chance for a container to roll a backpack, with tier weights 70%/25%/5% for levels 1/2/3. Keep the values centralized and testable.
3. Ensure difficulty multipliers clamp the final chance and never reintroduce level 4 or 5.
4. Keep quest level 4/5 catalog entries available for direct quest rewards; only ordinary loot must exclude them.
5. Keep the generated item IDs and icon resolution stable for all existing catalog levels.

### Task 3: Enforce per-player backpack loot eligibility on the server

**Files:**

- Modify: `Assets/Script/Tin/InventorySystem.cs:104-120,736-848,1087-1119`
- Modify: `Assets/Khoa/Code/LootContainer.cs:601-611,485-567`
- Modify: `Assets/Script/Tin/ZombieCorpseLoot.cs:270-329`

**Steps:**

1. Add one shared, non-UI predicate in `InventorySystem` or a small rules class that reads the item backpack level and compares it with `CurrentBackpackLevel`.
2. Make the local loot UI disable only the backpack item when the player already owns an equal-or-higher level.
3. Revalidate the same rule in `LootContainer.RPC_RequestTakeItem` before `AddItem`.
4. Revalidate it in `InventorySystem.RPC_RequestPickupItem` for dropped world items.
5. Revalidate it in `ZombieCorpseLoot` before `AddItem` and before consuming the corpse.
6. Send a targeted denial message such as “Bạn đang có balo cấp cao hơn; balo này để đồng đội khác loot.”
7. Do not remove or mutate the rejected container/corpse/world item.
8. Do not auto-equip or downgrade from a lower-level loot backpack. Higher-level loot continues to upgrade through the existing authoritative path.

### Task 4: Remove fixed level-2 and level-3 mission backpack rewards

**Files:**

- Modify: `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs:1126-1166`
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/InventoryAndLootCapacityTests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/PlayMode/MainMenuToMilitaryQuestFlowTests.cs`

**Steps:**

1. Remove `BackpackItemCatalog.GetOrCreate(2)` from `ServerClaimOfficeSafe`.
2. Remove `MilitaryQuestItemCatalog.LevelThreeBackpack` from `ServerUnlockArmory`.
3. Preserve the Office Safe weapon/ammunition/key rewards.
4. Preserve the Armory weapon/ammunition rewards.
5. Update quest messages and journal reward text so neither location promises a level-2 or level-3 backpack.
6. Verify ordinary loot remains the only source of levels 1–3 after this change.

### Task 5: Move level-5 reward from military entry to Radio 3/3

**Files:**

- Modify: `Assets/Script/Tin/BackpackQuestRewardRules.cs:5-30`
- Modify: `Assets/Script/Tin/InventorySystem.cs:123-195`
- Modify: `Assets/Script/Tin/MainQuest/MainQuestManager.cs:1405-1466,1725-1753,2452-2462`
- Modify: `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs:305-364`

**Steps:**

1. Replace the level-5 milestone meaning from physical military-zone entry with the Radio 3/3 completion milestone while keeping the level value at 5.
2. At the authoritative `AuthorityCompleteHospitalRadio` transition, create a per-player pending level-5 reward for every valid active player in the session.
3. Keep the Radio recording/map completion shared for progression, but do not immediately fire the backpack presentation before the map sequence has completed.
4. Add an authoritative claim request/acknowledgement for the pending level-5 reward. The client may request completion only for its own `PlayerRef` after its map sequence callback.
5. Validate that the Radio is recovered, the player has a pending claim, and the claim has not already been consumed.
6. Make the claim idempotent across duplicate callbacks, reconnects, respawn snapshots and late joins.
7. Remove the level-5 grant from the military-area scan. Military entry remains the travel/progression event only.
8. Preserve personal inventory ownership and target presentation only to the claiming player.

### Task 6: Implement the exact Option 1 presentation sequence

**Files:**

- Modify: `Assets/Script/Tin/BackpackQuestRewardPresentation.cs:79-183,317-390`
- Modify: `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs:58-64`
- Modify: `Assets/Script/Tin/MainQuest/MainQuestManager.cs:2452-2462,2762-2792`
- Modify: `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs:612-636,1148-1220`

**Steps:**

1. Keep the existing Map Fragment 2 card and map reveal as the first presentation stage.
2. Ensure the map overlay is explicitly closed and its overlay/input suppression is restored before starting the backpack presentation.
3. Add a completion callback boundary named clearly enough to distinguish `MapClosed` from `MapRevealStarted`.
4. Start the backpack presenter only from the `MapClosed` callback for that local player.
5. Use the current Option 1 visual language: dim/scan layer, backpack icon, level-5 badge, capacity increase and a short military accent; do not show map art inside the backpack card.
6. Keep the backpack presenter on a separate canvas and do not modify map state or map reward data.
7. Keep the presentation non-blocking for teammates. Local UI may animate independently while other players continue moving/looting.
8. Invoke the authoritative “upgrade complete” notification only after the backpack animation finishes. The notification must not appear at Radio 3/3, during map reveal, or at backpack animation start.
9. If a client receives the pending reward after the map is already known/closed, run only the missing stage and still show the final upgrade notification once.
10. Update Vietnamese and English strings for level 5 and remove stale wording that suggests level 5 is awarded at military entry.

### Task 7: Update quest/UI text and durable design documentation

**Files:**

- Modify: `Assets/Script/Tin/GameLocalization.cs`
- Modify: `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs`
- Modify: `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- Modify: `docs/plans/2026-08-31-backpack-quest-rewards.md`

**Steps:**

1. Change the Route B reward description to state: Map Fragment 2 first, then personal level-5 backpack after the map presentation closes.
2. Remove/replace legacy hospital text referring to the old dispatch-desk/records-cabinet flow if it is still visible in the canonical Route B UI.
3. Make the journal distinguish progression rewards (map fragments/waypoints) from inventory rewards (backpack levels).
4. Keep the Route A reward text unchanged except where it incorrectly implies a backpack reward.
5. Record final constants, per-player state flow, presentation order and known non-goals in this plan after implementation review.

### Task 8: Independent verification and visual QA

**Files/artifacts:**

- Test: `Assets/Script/Tin/Prototype/Tests/Editor/InventoryAndLootCapacityTests.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/HospitalRadioRoomRulesTests.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/QuestFlowUIPrototypeTests.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/PlayMode/MainMenuToMilitaryQuestFlowTests.cs`
- Artifact directory: `QA_Artifacts/BackpackRadioRewardSequence_20260831/`

**Steps:**

1. After Antigravity reports changes, inspect `git diff`, file contents, and the list of changed files independently.
2. Confirm no source, scene, prefab or asset outside the approved scope was deleted or overwritten.
3. Check Unity compile state and read the Unity Console for errors/warnings related to Fusion RPCs, UI callbacks or missing assets.
4. Run focused EditMode tests through Unity MCP and capture the new result.
5. Run the full EditMode suite and record the result.
6. Run focused PlayMode Route B tests with at least two player objects or the project's multiplayer harness.
7. Run the full PlayMode suite and record the result.
8. Verify these multiplayer cases:
   - Player A has level 3 and cannot consume a level-1/2 loot item.
   - Player B has level 0/1 and can consume that same item if it remains.
   - One player cannot claim another player's level-5 reward.
   - Radio finisher and non-finisher each receive their own level-5 upgrade after their local map closes.
   - A slow-loading/late-joining player receives the pending reward without a duplicate.
9. Capture screenshots proving the order: map visible → map fully closed → backpack effect visible → final upgrade notification visible.
10. If any test or screenshot fails, create a correction prompt for Antigravity and repeat review/test; do not silently patch around the workflow.

## Acceptance criteria

- Ordinary loot can generate only backpack levels 1–3, with a low, centralized and test-covered chance.
- Office Safe and Armory no longer grant backpack level 2 or level 3.
- A player with an equal-or-higher backpack cannot loot a lower/equal backpack; the item remains available to eligible teammates.
- State Authority validates every backpack loot and quest claim; clients cannot spoof another player's claim or create inventory.
- Radio 3/3 gives each valid player a personal level-5 claim, with durable handling for slow load, late join, reconnect and duplicate callbacks.
- The level-5 inventory upgrade occurs in the sequence Map Fragment 2 → map reveal → map closes → backpack effect → final upgrade notification.
- The backpack effect is visually related to map reveal but remains separate from map reward/state and does not block teammates.
- Military-zone entry no longer grants level 5.
- EditMode, PlayMode, compile/console checks and fresh screenshots are recorded before any completion claim.

## Repository workflow gates

- Plan is reviewed by the user before implementation.
- After approval, prepare a clear Antigravity prompt with this scope and require a changed-file list plus test report.
- Confirm Antigravity is open in the correct workspace before sending the prompt.
- Codex independently reviews the result and runs verification through Unity MCP.
- Keep prompt/test artifacts until review and merge are approved.
- Do not commit, push, create PR or merge without the user's separate approval. If approved later, follow the repository's required branch-from-main → commit → push → merge → return-to-main sequence.
