# Antigravity Implementation Prompt — Backpack Loot and Radio Level-5 Reward Sequence

Repository: `E:/Unity/GameObject/Game3D/ProJectZomboiNhai`

Implement the approved plan in `docs/plans/2026-08-31-backpack-quest-rewards.md`.

## User-approved behavior

1. Ordinary loot may contain only backpack levels 1, 2 and 3, with a low drop rate.
2. Remove the fixed level-2 backpack reward from Office Safe.
3. Remove the fixed level-3 backpack reward from Armory.
4. Backpack level 5 is a personal reward for every valid player, not a shared inventory item.
5. Move the level-5 trigger from military-area entry to completion of all three Radio restoration segments.
6. The exact local presentation order must be:

   `Radio 3/3 → Map Fragment 2 reward → map reveal → map overlay fully closes → backpack level-5 effect → effect finishes → upgrade notification`

7. The backpack effect is Option 1: visually related to the existing map reward/reveal, but rendered by a separate backpack presenter and never using map art as the backpack reward.

## Multiplayer rules

- Fusion State Authority owns all loot rolls, pickup validation, inventory mutation and quest claims.
- Use the existing shared-container model: if a player is rejected because their current backpack is equal or higher, do not remove the item. An eligible teammate must still be able to loot it.
- Apply the equal-or-higher rejection both to local UI affordance and authoritative server validation for `LootContainer`, `ZombieCorpseLoot` and dropped `ItemPickup` requests.
- A backpack loot level must be strictly greater than the requester's current equipped level. Never downgrade.
- The Radio milestone is shared quest state, but level-5 inventory and presentation are per-player.
- Create a durable pending level-5 claim per valid active `PlayerRef` when Radio 3/3 completes. A slow-loading or late-joining player must not lose the reward because a one-shot RPC arrived before its UI/network object was ready.
- Each player may claim only its own pending reward. Duplicate callbacks, reconnects and respawns must not duplicate the claim or presentation.
- The final military-zone entry scan must no longer grant level 5.

## Loot configuration

- Use the approved low-rate baseline unless the existing project balance system requires an equivalent centralized value: 5% overall container backpack chance, with tier weights level 1 = 70%, level 2 = 25%, level 3 = 5%.
- Difficulty multipliers may modify the overall chance only within the existing clamp; they must never add levels 4 or 5 to ordinary loot.
- Keep catalog level-4/level-5 definitions available for quest rewards and presentation resolution, but exclude them from ordinary loot rolls.

## Exact reward/effect sequencing

- Do not call the level-5 quest upgrade/presentation immediately when `AuthorityCompleteHospitalRadio` sets the Radio to recovered.
- Preserve the existing Map Fragment 2 and military map reveal behavior.
- Introduce an explicit completion boundary after the map overlay is actually closed and its canvas/input suppression is restored.
- Only then request/authorize the individual player's level-5 claim and start `BackpackQuestRewardPresentation`.
- After the backpack animation finishes, show the localized “upgraded to backpack level 5” notification. The notification must not appear at Radio 3/3, while the map is visible, or at backpack animation start.
- Do not block teammates' movement, interaction or loot while one player's local presentation is running.

## Scope files

Inspect and modify only the files needed for this feature, primarily:

- `Assets/Script/Tin/ZombieCorpseLoot.cs`
- `Assets/Khoa/Code/LootContainer.cs`
- `Assets/Script/Tin/InventorySystem.cs`
- `Assets/Script/Tin/BackpackQuestRewardRules.cs`
- `Assets/Script/Tin/BackpackQuestRewardPresentation.cs`
- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs`
- `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs`
- `Assets/Script/Tin/GameLocalization.cs`
- relevant tests under `Assets/Script/Tin/Prototype/Tests/`

Do not modify unrelated scenes, prefabs, packages, project settings or source systems. Do not delete user data. Do not commit, push, create a PR or merge.

## Test requirements

Add/update regression tests before or alongside implementation for:

- only levels 1–3 roll from ordinary loot;
- low-rate/weight configuration;
- equal-or-higher backpack cannot loot and rejected item remains available;
- a lower-level teammate can loot the same item;
- dropped item and corpse paths apply the same rule;
- Office Safe/Armory no longer grant backpacks;
- Radio 3/3 maps to personal level-5 pending claims;
- military-area entry no longer grants level 5;
- map closes before backpack effect;
- final upgrade notification occurs only after the backpack effect callback;
- duplicate, reconnect and late-join claims are idempotent;
- no player can claim another player's reward.

Run focused EditMode tests, full EditMode tests, focused Route B PlayMode/multiplayer tests and full PlayMode tests if available. Check Unity compile state and Console output. Capture screenshots proving: map visible → map closed → backpack effect → final upgrade notification.

## Required report

When finished, report:

1. exact files changed;
2. the final loot chance and tier weights;
3. the authoritative per-player reward state flow;
4. the final map/backpack/notification callback order;
5. EditMode and PlayMode results, including failures or unavailable tests;
6. Console/compile status;
7. screenshot paths;
8. any remaining risks.
