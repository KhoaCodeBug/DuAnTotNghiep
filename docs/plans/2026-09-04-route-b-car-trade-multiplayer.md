# Route B Car Exit and Multiplayer Trade Implementation Plan

> **Implementation owner:** Antigravity. Codex must review and verify independently after Antigravity reports the change.

**Goal:** Fix the Route B authored `Car` so a player who enters a seat is rendered and synchronized at the seat, can exit through a valid exit point, and regains movement. Restore multiplayer proximity trade so any eligible player can press `T`, the target receives the request, and both peers can complete an authoritative trade.

**Architecture:** Keep Photon Fusion state authority as the source of truth. Diagnose transform/physics ownership and RPC/UI routing before changing behavior. Add the smallest regression tests and implementation changes needed for vehicle seat/exit state and trade target selection/request flow. Preserve the existing inventory transaction validation and atomicity.

**Tech Stack:** Unity `6000.0.69f1`, Photon Fusion, Unity Input System plus existing legacy key checks, Unity Test Framework, 2D physics.

---

## Scope and observed entry points

- Vehicle: `Assets/Hau/Script/VehicleController.cs` and `Assets/Hau/Script/PlayerInteraction.cs`.
- Route B authored prefab/scene: `Assets/Hau/NewPrefab/Car/Car.prefab`, `Assets/Scenes/Main.unity`, and the `Car` instance near `SpawnXeCanhSat`.
- Player prefabs: `Assets/Prefab/Player.prefab`, `Assets/Prefab/Player2.prefab`.
- Trade: `Assets/Script/Tin/PlayerTrade.cs` and the existing trade UI methods in `Assets/Script/Tin/AutoUIManager.cs`.
- Tests: extend the closest existing vehicle/route-B and multiplayer test locations under `Assets/Script/Tin/Prototype/Tests` or add narrowly scoped tests beside the owning scripts. Do not create duplicate test infrastructure.

Important current facts to verify at runtime rather than assume:

- The Route B Car prefab has a `VehicleControllerFusion`, multiple seat/entry/exit anchors, a body collider, an interaction collider, and an authored `enterDistance` override of `2.5`.
- Both player prefabs serialize `PlayerTrade.tradeRadius` as `2`.
- `PlayerInteraction` requests vehicle enter/exit through state-authority RPCs and `VehicleControllerFusion` moves occupants to seat anchors on authority/network ticks.
- `PlayerTrade` discovers nearby objects locally, sends `RPC_SendRequest` to state authority, then the authority validates distance and trading state before showing the request popup.

## Acceptance criteria

Vehicle:

1. In `Main.unity`, using the authored Route B `Car`, a player entering any available seat is at that seat anchor on the local and remote views; the player is not visually left standing on the roof or above the vehicle.
2. While seated, player movement input cannot independently pull the player away from the seat or leave a physics body fighting the authoritative seat synchronization.
3. Pressing the existing exit key while the player is seated clears the network vehicle state, places the player at the selected safe exit anchor, and restores movement/physics on the same peer and on remote peers.
4. The fix does not hardcode a scene-only offset or break other vehicle prefabs/seats.

Trade:

1. In a host/client multiplayer session, any eligible player within the configured trade radius can press `T` while gameplay is unblocked; the nearest valid target is selected using valid Fusion player authority.
2. The target receives the request popup, accepting opens the trade window for both peers, and declining/canceling closes the correct UI.
3. Offers, ready state, confirmation, item validation, and the final exchange remain state-authority controlled and atomic; no client can trade with itself or a stale/nonexistent object.
4. Trade input is rejected only when the existing UI/story/health/death blockers intentionally block gameplay, and those blockers do not silently prevent a valid request in ordinary gameplay.

Verification:

- A failing regression test is observed before production changes (Red), then the minimal fix makes it pass (Green), followed by a focused refactor only if needed.
- EditMode tests run and their result is recorded.
- PlayMode/multiplayer verification runs with at least host + one client; include a second client if the existing harness supports it.
- Unity compilation is complete, Console has no new errors/warnings attributable to this change, and screenshots capture the Route B seated/exit state plus the trade request/window flow where the harness permits.

## Implementation sequence for Antigravity

### 1. Baseline and reproduction

- Confirm the repository is on the requested working state and inspect the current diff before editing.
- Reproduce the Route B vehicle issue with the authored Car. Record state authority, input authority, player root `Transform`/`Rigidbody2D`, seat anchor world position, exit anchor world position, `CurrentVehicle`, `NetworkIsInVehicle`, physics simulation, and any network interpolation/presentation writer at enter, one network tick later, and exit.
- Reproduce trade with host + client. Record `T` input receipt, `FindObjectsByType<PlayerTrade>` count, local/remote positions and distance, target `InputAuthority`, RPC source/target, every `CanStartTrade` rejection reason, popup activation, and UI singleton/panel state. Use bounded diagnostics; do not add per-frame log spam.
- Identify and state the root cause before choosing the fix. If the observed object is not the authored Route B Car, stop and report the exact object/path.

### 2. Red tests

- Add the smallest test for the vehicle invariant: authoritative seat assignment and presentation must converge to the seat anchor; exit must clear vehicle state, restore physics, and use a valid exit position. Prefer a pure/helper seam for deterministic assertions and a PlayMode test for the actual Fusion/physics integration.
- Add the smallest trade test for selecting a valid nearby target and preserving the target's `InputAuthority`; add an integration test for request delivery/popup if the existing multiplayer test harness supports it.
- Run the focused tests and preserve the failing output as evidence before modifying production code.

### 3. Minimal fix

- Fix the actual ownership/order/authority or UI/input problem discovered in baseline. Do not solve the symptom with arbitrary transform offsets, unconditional teleport loops, broad collider disabling, client-authoritative trade, or a global radius increase without measured justification.
- Keep server/state-authority validation for vehicle state and trade transactions.
- Preserve public APIs, serialized references, unrelated scenes/prefabs, inventory semantics, and existing UI behavior.

### 4. Verification and report

- Run the focused EditMode tests, then PlayMode/multiplayer tests, then inspect compile state and Console.
- Capture fresh screenshots for vehicle enter/seat/exit and trade request/open/complete states.
- Report root cause, every changed file, test commands/results, Console status, screenshots/artifact paths, and any remaining limitation. Do not report success without fresh evidence.

## Out-of-scope constraints

- Do not modify Photon/Fusion packages, unrelated scenes, unrelated prefabs, save data, or inventory rules.
- Do not delete files or test artifacts.
- Do not commit, push, merge, or create a pull request as part of this task.
- Keep diagnostic/test artifacts until the branch/pull request has been accepted and merged, per repository workflow.
