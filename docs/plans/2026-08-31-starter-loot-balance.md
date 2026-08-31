# Starter Loadout and Loot Balance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Cập nhật trang bị đầu trận và cân bằng loot container/corpse theo ba độ khó, với đạn khởi đầu đúng một băng của vũ khí được chọn.

**Architecture:** DifficultyRules tiếp tục là nguồn luật loadout; State Authority chọn một vũ khí trong pool hiện có (AK47/S12K), ghi `StartingWeaponId` networked và cấp ammo theo `ammoTypeRequired`/`magazineCapacity` của asset. Loot container dùng bảng independent-roll đã giảm lạm phát và difficulty multiplier hiện hữu; corpse giữ một lần roll authoritative, thêm 12 Gauge và ưu tiên bandage. Route B military loot, tutorial authored loot và inventory/RPC validation được giữ nguyên.

**Tech Stack:** Unity 6.0.69f1, C#, Fusion Host Mode, ScriptableObject loot tables, Unity Test Framework EditMode/PlayMode.

---

### Task 1: Lock the failing contracts first

**Files:**
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/InventoryAndLootCapacityTests.cs`
- Modify: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`

**Step 1: Write failing tests**

Add assertions for:

- Easy: one random starter weapon, its matching one-magazine ammo, Water 3, Meat 3, Flashlight 1, Bandage 5, PainKiller 1.
- Normal: one random starter weapon, matching one-magazine ammo, Flashlight 1, Bandage 3.
- Hard: Flashlight 1 only.
- Starter weapon pool is exactly the existing `AK47`/`S12K` assets, and their matching ammo/capacity is derived from the assets.
- Ordinary loot uses Ammo762 15–30 and Ammo12Gauge fixed 5; authored tutorial Ammo12Gauge 12 remains respected.
- Corpse conditional weights are Water 25, Bandage 45, PainKiller 15, Ammo762 10, Ammo12Gauge 5 and total 100.
- Default container base chances are Ammo762 20, Bandage 35, EnergyWater 20, Meat 40, PainKiller 15, Water 45, Ammo12Gauge 15; backpack remains 10% and weapon bonus is the approved 15% base scaled by difficulty.

**Step 2: Run the targeted tests to prove RED**

Run the Unity EditMode tests covering `DifficultyRules`, `InventoryAndLootCapacityTests`, and `ReadinessAndChatEditorTests`. Expected result: the new assertions fail against the old loadout/loot values, with no compile error caused by the test itself.

### Task 2: Implement authoritative starter loadout

**Files:**
- Modify: `Assets/Script/Tin/Prototype/DifficultyRules.cs`
- Modify: `Assets/Script/Tin/InventorySystem.cs`
- Modify: `Assets/Script/Tin/GameLocalization.cs`

**Step 1: Add the minimal contract implementation**

Represent the random starter weapon explicitly in `DifficultyRules`, expose the current starter weapon pool, and make the loadout amounts match the approved table. In `InventorySystem`, resolve the random weapon once on State Authority, reuse `StartingWeaponId` on retries, place it in the hotbar, and add exactly `weapon.ammoTypeRequired` at `weapon.magazineCapacity`. Do not let clients roll their own weapon or ammo.

**Step 2: Run the targeted tests**

Expected result: starter loadout contract tests pass, including retry/idempotency and asset-derived ammo checks.

### Task 3: Implement ordinary container and corpse loot balance

**Files:**
- Modify: `Assets/Khoa/Code/LootContainer.cs`
- Modify: `Assets/Script/Tin/ZombieCorpseLoot.cs`
- Modify: `Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset`
- Modify: `Assets/Khoa/Zombie2Khoa.prefab`
- Modify: `Assets/Khoa/ZombieKhoaRebuilt.prefab`
- Modify: `Assets/Thai/prefab/Zombie.prefab`
- Modify: ordinary serialized `LootContainer` instances that override the old 25% weapon chance, after confirming their paths and excluding backups/military behavior where the field is unused.

**Step 1: Add the minimal implementation**

- Roll Ammo762 with `Random.Range(15, 31)` and Ammo12Gauge with fixed 5 for normal random loot; retain explicit authored fixed quantities such as tutorial Ammo12Gauge 12.
- Change the default lobby table to the approved base chances and quantities.
- Add `ammo12GaugeWeight`, map corpse kind 5 to `Ammo12Gauge`, set weights to 25/45/15/10/5, and keep the existing Easy/Normal/Hard corpse gate 45/30/12.
- Keep corpse roll, search consumption, loot amount, and result RPC on State Authority so late joiners see the same canonical state.
- Apply the approved 15% base weapon bonus with existing difficulty multiplier; preserve backpack tier weights and Route B military bypass.

**Step 2: Run targeted loot tests**

Expected result: quantity, weight, independent-probability, tutorial-preservation, and authority-contract tests pass.

### Task 4: Green and regression verification

**Files:**
- No new production files unless compilation identifies an existing assembly-boundary issue.

**Step 1: Run Unity refresh/compile**

Expected result: zero new compile errors and no broken serialized references.

**Step 2: Run the full EditMode suite**

Record exact pass/fail/skip counts.

**Step 3: Run relevant PlayMode flows**

At minimum cover Solo menu → Main spawn/loadout, ordinary container generation, corpse search, and the existing Route B/military flow. Record anything unavailable in the current environment rather than inferring it.

**Step 4: Inspect final diff**

Run `git diff --check`, review every changed path, verify military/tutorial assets were not unintentionally changed, and confirm no user-owned unrelated changes were staged or overwritten.

### Task 5: Handoff

Append a dated entry to `CODEX_PROJECT_WORK_LOG.md` with `Đề xuất`, `Đã duyệt`, `Đã triển khai`, `Đã test tự động`, `Đã test tay` and remaining risks. Keep the feature branch local unless the user separately authorizes push.
