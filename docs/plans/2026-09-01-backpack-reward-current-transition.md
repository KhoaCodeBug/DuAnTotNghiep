# Backpack Reward Current-to-Reward Transition Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make Notification A identify the actual equipped backpack level/capacity before the quest reward and the received reward level/capacity, instead of using fixed `30 → 40` or `40 → 50` values.

**Architecture:** Capture the player's backpack state before the authoritative quest upgrade, carry that state through the local and RPC presentation paths, and format the transition in the existing compact Notification A. Preserve Effect B as a text-free visual-only phase and keep the Level 5 map sequence unchanged.

**Tech Stack:** Unity 6, C#, Fusion RPC, Unity Test Framework EditMode/PlayMode, TextMeshPro/uGUI.

---

### Task 1: Add the failing regression coverage

**Files:**
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/BackpackRewardCombinationBATests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/PlayMode/PlayModeBackpackVisualCaptureTests.cs`

**Step 1: Write the failing test**

- Add a presenter-level case that simulates a player at backpack level 3 receiving level 4 and asserts Notification A contains `LEVEL 3 → LEVEL 4` and `30 → 40`.
- Add a non-sequential/low-state case (current level 0 or 2 receiving level 4) so a fixed `level - 1` implementation cannot satisfy the test; assert the actual current-to-reward transition.
- Keep existing assertions that Effect B has no text and Notification A is blocked while Effect B is active.
- Extend the PlayMode assertions to verify the runtime notification receives the current level/capacity captured before the authoritative upgrade.

**Step 2: Run the focused EditMode test before production changes**

Run the focused `BackpackRewardCombinationBATests` group through Unity MCP.

Expected: the new transition assertion fails because the current presenter only formats fixed per-reward capacity strings and does not receive the pre-upgrade inventory state.

### Task 2: Carry the authoritative pre-upgrade state

**Files:**
- Modify: `Assets/Script/Tin/InventorySystem.cs`
- Modify: `Assets/Script/Tin/BackpackQuestRewardPresentation.cs`
- Modify: `Assets/Script/Tin/MainQuest/MainQuestManager.cs`

**Step 1: Capture before applying the reward**

- In `TryGrantQuestBackpackReward`, capture `CurrentBackpackLevel` and `CurrentBackpackSlots` before `ApplyBackpackUpgradeLocal`.
- Preserve the captured state for both local-host and client-confirmation paths.

**Step 2: Preserve the state across RPC/presentation handoff**

- Extend the existing quest reward notification/confirmation data path without removing existing public APIs used by tests.
- Pass the captured previous level (and derive its storage capacity through `BackpackCapacityRules`) to Notification A for level 4.
- Ensure the deferred Level 5 callback uses the same captured previous state after map reveal closes, rather than reading the already-upgraded inventory.

**Step 3: Format the dynamic transition**

- Add localized format keys for the dynamic level transition title/body.
- Notification A should show the actual received item name and milestone reason, plus `current level → reward level` and `current storage → reward storage`.
- Keep a safe fallback for legacy/direct calls, but never replace a valid captured previous state with a fixed hard-coded `30 → 40`/`40 → 50` value.
- Keep Effect B unchanged: no storage, capacity, level, item name, or story text while the reward visual is active.

### Task 3: Verify the complete Unity flow

**Files:**
- No additional production files.
- Runtime evidence: `Assets/Screenshots/RuntimeBackpack/`.

**Step 1: Compile and run focused tests**

Run the focused EditMode group and confirm all transition, guard, layout, and sequence tests succeed.

**Step 2: Run full EditMode**

Run the complete EditMode suite and confirm no unrelated inventory, networking, or warning-regression test breaks.

**Step 3: Run PlayMode and capture evidence**

Run the deterministic solo flow. Confirm the actual order remains map reward → map reveal → map close → Effect B → Notification A, and capture L4/L5 screenshots showing the dynamic level/capacity transition with no overflow.

**Step 4: Check Console and final state**

Clear Console before the run, then verify 0 game Error and 0 game Warning, no compile errors, and Unity returns to idle MainMenu.

### Task 4: Review and local commit

**Files:**
- Only the scoped implementation, tests, and this plan document.

**Step 1: Review diff**

Confirm no scene, prefab, unrelated quest story, or user-owned screenshot/config changes were altered.

**Step 2: Create a dedicated `codex/` branch and commit**

Commit only after the new tests and runtime evidence are green. Do not push, create a PR, or merge without explicit user approval.
