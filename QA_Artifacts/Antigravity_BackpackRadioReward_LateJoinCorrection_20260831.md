# Antigravity Correction Prompt — Durable Radio Level-5 Reward for Slow Load/Late Join

## Context

The previous implementation and UI verification are on disk. Independent verification currently shows:

- Unity 6000.0.69f1 is idle and compile-clean.
- Independent EditMode: 192/192 passed.
- Independent PlayMode: 15/15 passed.
- The screenshots for the five Option 1 presentation stages are valid separate Unity renders.

However, independent code review found a multiplayer durability gap: `MainQuestManager.AuthorityCompleteHospitalRadio` currently sends `RPC_PlayHospitalRadioRecording` only to `PlayerMovement` objects present at the instant Radio 3/3 completes. `InventorySystem.Spawned` and `Render` do not show a durable per-player pending level-5 reward handoff for a player whose object loads later or reconnects. The shared `IsHospitalRadioRecovered` flag alone is not sufficient to prove that the late player's local Map-closed -> Backpack presentation -> final notification sequence will run exactly once.

## Required outcome

Fix only the durable slow-load/late-join/reconnect behavior for the existing backpack reward design. Do not change the locked user-facing rules:

```text
Radio 3/3
  -> Map Fragment 2 reward
  -> map reveal
  -> map fully closed
  -> Option 1 backpack level-5 effect
  -> effect ends
  -> final "Đã nâng cấp lên balo cấp 5" notification
```

Level 5 remains a personal reward for every valid living player, not a team-shared inventory grant. Military-zone entry remains a travel/progression event and must not grant level 5. Ordinary loot remains restricted to levels 1/2/3 and must not be touched by this correction.

## Scope to inspect

- `Assets/Script/Tin/InventorySystem.cs`
- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/BackpackQuestRewardRules.cs`
- `Assets/Script/Tin/Prototype/Tests/Editor/HospitalRadioRoomRulesTests.cs`
- `Assets/Script/Tin/Prototype/Tests/Editor/BackpackRewardVisualQATests.cs`
- Related existing Route B PlayMode test file(s), only if the current harness supports the needed deterministic case.

## Implementation requirements

1. Add an explicit durable per-player pending/claim state for the Radio level-5 milestone, or an equivalent authoritative state keyed to the player's network identity. Do not rely only on a one-shot presentation RPC or a local callback.
2. When State Authority completes Radio 3/3, mark the level-5 milestone pending for every currently valid living player that should receive it. Keep the state distinct per player; one player's claim bit must not consume another player's reward.
3. When a valid player object spawns after `IsHospitalRadioRecovered` is already true, reconcile that player's pending level-5 state on State Authority and expose a targeted handoff to that player's Input Authority. This must cover slow loading, late join and reconnect/respawn without replaying the reward for a player whose claim is already recorded.
4. The late player's local handoff must continue from the first missing stage. If the map reward/reveal has already been completed or is not available to replay safely, it may proceed to the map-closed boundary and then present the backpack; do not replay a stale map overlay indefinitely. If a map presentation is currently active, wait for its explicit close callback before showing the backpack effect.
5. Preserve the existing Option 1 separation: the backpack presenter is a separate UI/effect and must not mutate map reward data or block teammates. Final level-5 notification must remain after the backpack effect completion callback.
6. Keep all server validation: requester must match `info.Source` and the player's `InputAuthority`; Radio must be recovered; duplicate claims/callbacks must be idempotent.
7. Remove or resolve the now-unused `hospitalRadioHearingDistance` field if it remains unused after the implementation, so this change does not introduce a new warning.
8. Add deterministic regression coverage for:
   - finisher and a non-finisher each get separate pending claims;
   - a player joining/reconnecting after Radio 3/3 can claim once;
   - a player with an already recorded claim cannot trigger a duplicate claim or notification;
   - a foreign player cannot claim another player's pending reward;
   - the map-close boundary still precedes the backpack effect and final notification.
   Prefer the existing Fusion test harness. If a full network setup is unavailable, extract and test small authoritative state-transition helpers and clearly report what remains integration-only; do not write a synthetic test that merely calls `TryGrantQuestBackpackReward(5)` and label it as late-join coverage.
9. Keep the existing screenshots with their actual filenames:
   - `Assets/Screenshots/backpack_radio_01_map_fragment_reward.png`
   - `Assets/Screenshots/backpack_radio_02_map_reveal.png`
   - `Assets/Screenshots/backpack_radio_03_map_closed.png`
   - `Assets/Screenshots/backpack_radio_04_backpack_effect.png`
   - `Assets/Screenshots/backpack_radio_05_upgrade_notification.png`
   Report them as five separate stage images; do not describe `backpack_radio_reward_verification.png` as a contact sheet unless it is actually regenerated as one.

## Constraints

- Do not edit unrelated gameplay, scenes, prefabs or assets.
- Do not delete user data or existing official tests.
- Do not commit, push, create a PR or merge.
- Return the exact changed-file list, the authoritative state-flow explanation, test commands/results, compile/Console status and any limitation.

## Acceptance criteria for this correction

- The source contains an explicit durable late-join/reconnect handoff, not only the shared Radio flag and one-shot recording RPC.
- Each valid player's level-5 reward is personal, server-authoritative and idempotent.
- A late/slow player can receive the reward after loading without requiring the Radio completion event to fire again.
- The exact local order remains map closed -> backpack effect -> final notification.
- No new compile errors; full EditMode and full PlayMode are rerun from an idle Unity Editor.
