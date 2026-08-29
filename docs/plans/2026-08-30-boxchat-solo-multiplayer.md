# BoxChat Solo and Multiplayer Reliability Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make BoxChat accept Enter and render player/system messages reliably in Solo and in a real Fusion Multiplayer session started from MainMenu.

**Architecture:** Trace the complete input → chat UI → message routing path first, then add a failing regression contract for the observed Enter/message symptom. Keep authority rules intact: player chat may be sent through the existing network path, while system announcements remain authoritative and must render on the correct peer(s). Fix only the root cause and preserve loading suppression, localization, sanitization, bounded history, and existing gameplay HUD behavior.

**Tech Stack:** Unity 6.0.69f1, C#, Fusion Host Mode, Unity Test Runner EditMode/PlayMode, ParrelSync for the real Multiplayer Host/Client check.

---

### Task 1: Investigate and reproduce the BoxChat failure

**Files:**
- Inspect: `Assets/Script/Tin/AutoChatManager.cs`
- Inspect: `Assets/Script/Tin/PlayerInputHandler2D.cs`
- Inspect: `Assets/Script/Tin/Multiplayer/GameplayReadinessCoordinator.cs`
- Inspect: `Assets/Script/Tin/MainMenuManager.cs`
- Inspect: `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`
- Inspect: current chat/readiness tests and `Main.unity` chat wiring

**Steps:**

1. Reproduce in Solo from MainMenu: enter Main, press Enter, type a normal message, press Enter again, and record focus, input field, visibility, and console logs.
2. Reproduce in Multiplayer exactly from MainMenu → Multiplayer: create Host in the original Editor first, open the ParrelSync clone, join the Host room, then repeat the chat flow on Host and Client.
3. Trace whether the break is input focus/Enter handling, UI visibility/suppression, message dispatch, network authority/RPC, or message rendering.
4. Compare with any working input/submit path already present in the project. Do not modify production code before the failing contract is identified.

### Task 2: Add the failing regression contract first

**Files:**
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`
- Add/modify PlayMode test only if the real scene/UI path cannot be covered by the existing suite

**Steps:**

1. Add focused tests for Enter submit/message visibility and system-message rendering without depending on a fake Solo-only shortcut.
2. Cover: normal text, empty/whitespace input, repeated messages, system announcement, bounded history, sanitization/localization, loading suppression, and reopening chat.
3. Run the focused test before the fix and confirm it fails for the observed reason.

### Task 3: Implement the minimal root-cause fix

**Files:**
- Modify only the smallest set discovered in Tasks 1–2; likely chat/input/readiness files, but do not assume paths until verified.

**Requirements:**

1. Enter must open/focus BoxChat when closed and submit the message when the input is focused, without requiring a mouse click.
2. Submitted text must clear the input, appear exactly once locally, and follow the existing authority/network route in Multiplayer.
3. System announcements (join, loading/readiness, death, loot, errors) must render through the same visible BoxChat path and not be silently blocked by stale CanvasGroup/suppression state.
4. Preserve rich-text sanitization, Vietnamese/English localization, bounded history, no duplicate RPC delivery, and loading/pause/modal input locks.
5. Do not solve the symptom with arbitrary delays, Solo-only code, or a second parallel chat system.

### Task 4: Verify the fix broadly

1. Run focused EditMode regression and confirm RED → GREEN.
2. Run full EditMode and full PlayMode.
3. Solo manual matrix without ParrelSync: Easy/Normal/Hard; open chat with Enter; submit normal, empty, whitespace, Vietnamese, repeated, long, and rich-text-like input; close/reopen; verify system join/loading/death/loot/error announcements.
4. Multiplayer manual matrix from MainMenu → Multiplayer only: original Editor creates Host first; ParrelSync clone opens afterward and joins the visible room. Test Host and Client input independently, Enter open/submit, system announcements, duplicate prevention, localization per peer, late join, disconnect/reconnect if supported, loading suppression/release, and chat reopen after gameplay actions.
5. Read Unity Console completely after each compile/test group and record any bridge/environment limitation separately from gameplay failures.

### Task 5: Independent review and Git handoff

1. Review Antigravity's diff file by file; verify no unrelated scene/prefab changes and no merge markers.
2. Repeat the highest-risk Solo and real Host/ParrelSync Client cases independently after Antigravity reports completion.
3. Run `git diff --check`, full tests, and final status.
4. Create a `codex/...` branch, commit only reviewed files, push it, merge to `main`, pull `main`, verify the final SHA and clean tree.
5. Append a truthful work-log/QA note only if needed; distinguish implemented, automatically tested, manually tested, and unverified cases.
