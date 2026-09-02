# Runtime Warning Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement the plan task-by-task.

**Goal:** Loại bỏ các warning runtime đã xác định mà không thay đổi gameplay, thứ tự nhận thưởng balo, hành vi UI B+A, hoặc cơ chế push-to-talk.

**Architecture:** Giữ một EventSystem hợp lệ cho mỗi runtime scene/lifecycle, ưu tiên input module đã được scene cấu hình và không tạo thêm module legacy khi hệ thống đã tồn tại. Khôi phục marker story `ViTriXeChetMay` tại đúng tọa độ fallback hiện tại để đường đi story dùng scene reference thật; giữ fallback an toàn. Voice vẫn tắt transmit khi chưa bấm V; chỉ điều chỉnh log/guard nếu xác minh warning là do code dự án gây ra, không sửa package và không ép microphone truyền.

**Tech Stack:** Unity 6.0.0.69f1, C#, Unity Test Framework, Unity MCP, Antigravity IDE, PowerShell.

---

### 1. Establish fresh baseline

**Files:** `Assets/Script/Tin/AutoChatManager.cs`, `Assets/Script/Tin/AutoUIManager.cs`, `Assets/Script/Tin/MainQuest/MainMenuManager.cs`, `Assets/Script/Tin/MainQuest/MainArrivalStoryBootstrap.cs`, `Assets/Scenes/Main.unity`, `Assets/Scenes/MainMenu.unity`.

**Actions:**

- Confirm the current branch, working tree, and existing user-owned screenshot/runtime artifacts.
- Re-read the warning call sites and inspect the current Unity hierarchy/console.
- Reproduce or capture fresh evidence for duplicate EventSystem, missing arrival marker, and Voice transmit warning, including object/module names and stack traces where available.

**Expected result:** A bounded root-cause record; no source or scene changes during baseline.

### 2. Add regression tests before production changes

**Files:** `Assets/Script/Tin/Prototype/Tests/Editor/RuntimeWarningRegressionTests.cs`, `Assets/Script/Tin/Prototype/Tests/PlayMode/RuntimeWarningRegressionPlayModeTests.cs` (or the smallest existing test files Antigravity determines are appropriate).

**Actions:**

- Add an EditMode/scene regression test that requires exactly one authored `ViTriXeChetMay` at the preserved story-arrival position.
- Add a focused PlayMode regression test for the MainMenu-to-Main lifecycle that asserts one active EventSystem and one effective input module after initialization.
- Add only a safe Voice regression assertion for the intentional push-to-talk initial state; do not require transmit to be enabled.

**Expected result:** New tests fail for the current project state (RED), with failures demonstrating the missing marker and/or duplicate lifecycle behavior before any fix is applied.

### 3. Implement the minimal root-cause fix through Antigravity

**Scope:** Only the EventSystem ownership/creation call sites, `Main.unity` marker, focused regression tests, and Voice warning guard/log path if the call-stack investigation proves it is project-owned.

**Constraints:**

- Do not modify Photon Voice package files, force microphone transmission, suppress all warnings globally, or destroy a valid EventSystem blindly.
- Preserve existing B+A backpack reward flow and all unrelated scenes/assets.
- Keep `ViTriXeChetMay` at `(35.73, -13.73, -0.025169304)` unless fresh scene evidence proves the serialized world position differs; keep the existing fallback for resilience.
- Prefer the authored `InputSystemUIInputModule`; only create an input module when no valid one exists.
- Report every changed file, exact design decision, and RED/GREEN test output.

**Expected result:** The duplicate creator race is removed or made idempotent, the arrival bootstrap resolves the scene marker without fallback warning, and intentional push-to-talk semantics remain unchanged.

### 4. Independent verification

**Actions:**

- Review the Antigravity diff and scene serialization for unintended changes.
- Refresh Unity and verify compilation.
- Run focused EditMode tests, relevant existing EditMode tests, focused PlayMode tests, and the existing backpack B+A PlayMode flow.
- Clear the Unity console, run the relevant runtime path, and inspect fresh warnings/errors. Confirm no duplicate EventSystem warning and no missing-marker fallback warning. If the Voice warning is third-party and unavoidable, record its exact source rather than masking it.
- Capture fresh runtime screenshots/video showing the game still reaches the existing map → map reveal → backpack effect → compact upgrade notification sequence.

**Expected result:** New evidence supports the warning cleanup while demonstrating no regression in the existing gameplay/UI flow.

### 5. Commit only after verification

**Actions:**

- Keep the existing uncommitted user-owned runtime artifacts intact.
- Create/use a `codex/` working branch as required by repository workflow, commit only the warning-cleanup implementation, tests, plan, and intentional scene/script changes.
- Stop before push/PR/merge and report branch, commit, diff summary, tests, console results, and evidence paths; request explicit permission for any remote Git operation.

**Expected result:** A locally verified commit with no remote mutation.
