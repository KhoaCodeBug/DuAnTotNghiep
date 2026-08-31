# Per-Player Multiplayer Readiness Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow every authenticated multiplayer player to enter gameplay immediately after their own local player and HUD are ready, without waiting for slower peers.

**Architecture:** Keep Fusion host authority and the authoritative ready-player set. Replace the room-wide readiness barrier with a targeted release RPC per ready player, and make the local menu retain early release signals until its readiness state is safe.

**Tech Stack:** Unity 6, C#, Photon Fusion, Unity Test Framework (NUnit EditMode/PlayMode).

---

### Task 1: Add per-player readiness regression tests

**Files:**
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`

**Step 1:** Add a test that requires `HostModeSpawner` to target the authenticated ready player and rejects the room-count barrier/watchdog source pattern.

**Step 2:** Add a test that sends `ForceCloseLoadingScreen` during `Connecting`, verifies no early release, advances to `HUDAndSystemsReady`, invokes the pending-release condition check, and verifies release.

**Step 3:** Run only the new EditMode tests and confirm they fail because the new behavior is absent.

### Task 2: Make host readiness per-player

**Files:**
- Modify: `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`

**Step 1:** Replace initial host room-barrier handling with authoritative registration plus targeted player release.

**Step 2:** Change `RPC_PlayerFinishedLoadingMap` to release each newly authenticated ready player immediately.

**Step 3:** Replace the all-target release RPC with a targeted `RpcTarget` release RPC shared by initial and later players.

**Step 4:** Remove the ten-second room readiness watchdog and disconnect re-check that are no longer part of startup.

**Step 5:** Preserve idempotency, anti-spoof validation, `IsMatchStarted`, and late-join announcements.

### Task 3: Retain early release signals locally

**Files:**
- Modify: `Assets/Script/Tin/MainMenuManager.cs`

**Step 1:** Make `ForceCloseLoadingScreen` record the host signal before checking the local readiness stage.

**Step 2:** Add a condition-based helper that releases only when local readiness is safe and never from `Failed`.

**Step 3:** Poll that helper in `SmoothLoadingLogic`; remove the late-stage arbitrary safety release because valid host signals are now durable.

**Step 4:** Keep the global 35-second explicit failure path and all menu reset paths.

### Task 4: Harden terminal startup failures

**Files:**
- Modify: `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`

**Step 1:** Convert runner-loss and last-moment authoritative-difficulty aborts into explicit readiness failures.

**Step 2:** Run the targeted EditMode tests and confirm green.

### Task 5: Verify Unity behavior

**Files:**
- No production file changes expected.

**Step 1:** Refresh Unity and wait for compilation.

**Step 2:** Validate changed scripts and inspect Console errors/warnings.

**Step 3:** Run full EditMode and full PlayMode suites.

**Step 4:** Run a host/client loading scenario where one client is delayed; capture evidence that the ready player enters first and the delayed player enters later.

### Task 6: Review and integrate

**Files:**
- Review all changed files and temporary QA artifacts.

**Step 1:** Review diff for unrelated changes and serialized asset modifications.

**Step 2:** Create a working branch from the tested working tree, commit, push to `origin`, and merge to `main` according to repository workflow.

**Step 3:** Return to `main`, run `git pull --ff-only origin main`, and verify HEAD and worktree state.
