# Antigravity Correction Prompt — Backpack Radio Reward

## Context

The first implementation compiles and its reported EditMode/PlayMode totals are green, but independent review found acceptance gaps. Apply this correction in the existing workspace only.

## Required corrections

1. **Durable per-player level-5 reward**
   - `AuthorityCompleteHospitalRadio` must create/mark a persistent pending level-5 milestone for every valid active player in the session, not only players within `hospitalRadioHearingDistance` and not only the finisher.
   - The shared `IsHospitalRadioRecovered` state may be the prerequisite, but each player's pending/claimed state must be recoverable from replicated state after slow load, late join, reconnect, respawn snapshot, or a scene transition.
   - A player must be able to request only its own claim. Keep the claim and inventory upgrade authoritative on State Authority.
   - Do not restore the old military-zone-entry reward. `MilitaryBaseEntry` must not grant level 5; military entry remains travel/progression only.
   - Make the request validation explicit: reject `requester` when it is not the inventory object's `Object.InputAuthority`, in addition to the existing `RpcInfo.Source` validation.

2. **Idempotent presentation and notification**
   - `RequestClaimLevelFiveBackpackReward` must not invoke the presentation callback merely because the claim bit is already set. Duplicate/reconnect/late-join requests must not replay the backpack animation or final upgrade notification.
   - Guard the local flow against duplicate callbacks while the map/reward sequence or backpack presenter is already running.
   - If the player already has level 5 from legacy data, preserve level 5 and mark the milestone safely, but do not replay the visual reward endlessly.
   - Ensure the final “upgraded to backpack level 5” notification is emitted exactly once per player and only after the backpack animation has finished.

3. **Exact order, including late clients**
   - For a fresh eligible client, preserve this exact order:
     `Radio 3/3 -> Map Fragment 2 card -> map reveal -> map overlay fully closed -> backpack effect -> effect fully ends -> level-5 notification`.
   - The map-reveal completion callback must be after `SetOpen(false)` and after all map reveal/input suppression is cleared.
   - For a slow/late client that already has the shared radio/map state, run only the missing local stages; never show level 5 before the map is closed, and never replay completed stages unnecessarily.
   - The backpack presentation must remain a separate non-blocking local canvas and must not mutate map reward/state.

4. **Regression tests**
   Add or strengthen tests so they actually cover the above contracts, not only string/reflection presence:
   - two players with different backpack levels contest the same ordinary loot item; rejection leaves it available and the lower-level player can take it;
   - every active player gets an independent pending level-5 claim after Radio 3/3;
   - a player cannot claim another player's reward;
   - duplicate claim/reconnect/late-join requests do not invoke the presentation callback twice;
   - non-finisher/far player and late joiner can receive their own claim after the shared radio state is recovered;
   - map is closed before the backpack presenter is visible, and the notification occurs only after the presenter callback;
   - military entry has no level-5 reward path.

5. **Visual QA evidence**
   - Replace the current invalid blank-blue screenshot at `Assets/Screenshots/backpack_radio_reward_verification.png` with real Unity captures showing the relevant UI. Do not generate a placeholder or blank image.
   - Capture a small evidence set (separate PNGs or a clearly labeled contact sheet) proving: map fragment/reveal visible, map fully closed, backpack Option 1 effect visible, and final level-5 notification after the effect.
   - If a real gameplay capture cannot be staged, report that limitation explicitly and do not claim visual verification.

6. **Cleanup and scope**
   - Remove dead no-op reward scan fields/calls left solely from the old military-entry reward if they are no longer used.
   - Do not alter unrelated gameplay, scenes, prefabs, assets, or tests outside the approved backpack/Radio/map scope.
   - Do not delete existing QA artifacts, do not commit, do not push, do not create a PR, and do not merge.

## Verification required before reporting back

- Confirm Unity is not compiling and has no new compile errors.
- Run the full EditMode suite and full PlayMode suite through Unity MCP.
- Read the Unity Console and list any warnings/errors, distinguishing pre-existing warnings from new ones.
- Reinspect the changed-file list and provide exact paths.
- Provide the test job IDs/results and the actual screenshot paths.
- Report any remaining blocker instead of claiming completion.
