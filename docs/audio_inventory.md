# Audio Asset Inventory & Categorization

This document provides a comprehensive inventory of audio assets in the project, categorized into Music/BGM, SFX (UI, Footsteps, Combat, Vehicle, BodyState), Story/Voice, and unused/raw assets.

## Summary of Volume Sources

- **Master Volume**: `GameMasterVolume` (PlayerPrefs & `GameAudioSettings.MasterVolume`, Default: `1.0`). Controls `AudioListener.volume`.
- **Music Volume**: `GameMusicVolume` (PlayerPrefs & `GameAudioSettings.MusicVolume`, Default: `0.5`). Multiplied by Master for effective music volume.
- **SFX Volume**: `GameSFXVolume` (PlayerPrefs & `GameAudioSettings.SFXVolume`, Default: `0.8`). Multiplied by Master for effective SFX volume.
- **Centralized Service**: `GameAudioSettings` provides real-time bidirectional synchronization between Main Menu and Pause Menu, live preview without requiring save, and safe persistence to PlayerPrefs.

---

## 1. Music / BGM

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Music/ThemeGamePlay.mp3` | Music / BGM | `GameplayMusicController.cs` via `GameplayMusicSettings` (Intro & Main scenes) | 2D Non-spatial, Looping | `GameAudioSettings.MusicVolume * RelativeVolume` | Active |
| `Assets/Resources/Sound/Project Zomboid Main Theme.mp3` | Music / BGM | `MainMenuManager.cs` (`menuBGM` in Main Menu) | 2D Non-spatial, Looping | `GameAudioSettings.EffectiveMusicVolume` | Active |

---

## 2. Story / Voice

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/Story/RouteB/radio_outro_1_clean.mp3` | Story / Voice | `RouteBRadioBroadcastUI.cs` (Military evacuation broadcast step 1) | 2D Radio Presentation | `GameAudioSettings.SFXVolume * masterVolume` | Active |
| `Assets/Resources/Sound/Story/RouteB/radio_outro_2_clean.mp3` | Story / Voice | `RouteBRadioBroadcastUI.cs` (Military evacuation broadcast step 2) | 2D Radio Presentation | `GameAudioSettings.SFXVolume * masterVolume` | Active |
| `Assets/Resources/Sound/Story/RouteB/radio_outro_3_clean.mp3` | Story / Voice | `RouteBRadioBroadcastUI.cs` (Military evacuation broadcast step 3) | 2D Radio Presentation | `GameAudioSettings.SFXVolume * masterVolume` | Active |
| `Assets/Resources/Sound/Story/RouteB/radio_outro_4_clean.mp3` | Story / Voice | `RouteBRadioBroadcastUI.cs` (Military evacuation broadcast step 4) | 2D Radio Presentation | `GameAudioSettings.SFXVolume * masterVolume` | Active |

> **Note on Story/Voice**: Currently played via SFX audio channels as narration/radio audio per existing game design. It respects `GameSFXVolume` multiplied by `GameMasterVolume`.

---

## 3. Sound Effects (SFX)

### 3a. Melee & Weapons Combat SFX

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/Melee/melee_swing.mp3` | Combat SFX | `PlayerCombat.cs` (`RPC_PlayMeleeSwingSFX`) | `GameplayAudioSpatializer` (Melee profile, local/remote cue policy) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Melee/melee_hit_flesh.mp3` | Combat SFX | `PlayerCombat.cs` (`RPC_PlayMeleeHitFleshSFX`) | `GameplayAudioSpatializer` (Melee profile) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Weapons/AK47/ak47_single.mp3` | Weapon SFX | `PlayerCombat.cs` (`PlayAK47ShootSFX`) | `GameplayAudioSpatializer` (Gunshot profile, 3D spatial) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Weapons/AK47/ak47_reload.mp3` | Weapon SFX | `PlayerCombat.cs` (Reload action) | 3D Spatialized | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Weapons/AK47/ak47_empty.mp3` | Weapon SFX | `PlayerCombat.cs` (Dry fire cue) | 2D Local Cue | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Weapons/S12K/s12k_single.mp3` | Weapon SFX | `ItemData` custom shoot clip | `GameplayAudioSpatializer` (Gunshot profile) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Weapons/S12K/s12k_reload.mp3` | Weapon SFX | `ItemData` custom reload clip | 3D Spatialized | `GameAudioSettings.SFXVolume` | Active |

### 3b. Movement & Footsteps SFX

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/Footsteps/concrete1.mp3` to `concrete4.mp3` | Footstep SFX | `PlayerMovement.cs` (`PlaySpecificFootstep`) | Spatialized (Local owner & allowed remote cues) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Footsteps/grass1.mp3`, `grass2.mp3` | Footstep SFX | `PlayerMovement.cs` | Spatialized | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Footsteps/wood1.mp3`, `wood2.mp3` | Footstep SFX | `PlayerMovement.cs` | Spatialized | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Footsteps/snow1.mp3`, `snow2.mp3` | Footstep SFX | `PlayerMovement.cs` | Spatialized | `GameAudioSettings.SFXVolume` | Active |

### 3c. Body State & Health SFX

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/BodyState/male_hurt_grunt.mp3` | Body SFX | `PlayerHealth.cs` (`PlayHurtGruntSFX`) | Spatialized | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/BodyState/player_death.mp3` | Body SFX | `PlayerHealth.cs` (`PlayDeathSFX`) | 3D Spatialized (Allowed on remote avatars) | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/BodyState/heavy_breathing.mp3` | Body SFX | `PlayerStamina.cs` (Exhaustion loop) | 2D Local Cue | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/BodyState/heartbeat_fast.mp3` | Body SFX | `PlayerHealth.cs` (Low HP heartbeat) | 2D Local Cue | `GameAudioSettings.SFXVolume` | Active |

### 3d. Vehicles & Interaction SFX

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/Vehicles/Repair/vehicle_repair_wrench.mp3` | Vehicle SFX | `ArrivalCarInspectionUI.cs` | 2D Local UI/Action | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Vehicles/Repair/vehicle_engine_start.mp3` | Vehicle SFX | `CivilianRoutePresentationController.cs` | 3D Spatialized | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/Vehicles/Repair/vehicle_engine_loop.mp3` | Vehicle SFX | `CivilianRoutePresentationController.cs` | 3D Spatialized | `GameAudioSettings.SFXVolume` | Active |

### 3e. UI Audio

| Asset Path | Category | Usage / Caller | Spatial Policy | Volume Source | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Assets/Resources/Sound/UI/click.mp3` | UI SFX | `MainMenuManager.cs`, `AutoUIManager.cs` | 2D Non-spatial | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/UI/hover.mp3` | UI SFX | `MainMenuManager.cs`, `AutoUIManager.cs` | 2D Non-spatial | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/UI/inv_open.mp3`, `inv_close.mp3` | UI SFX | `AutoUIManager.cs` | 2D Non-spatial | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/UI/quest_complete.mp3`, `quest_accept.mp3` | UI SFX | `AutoUIManager.cs` | 2D Non-spatial | `GameAudioSettings.SFXVolume` | Active |
| `Assets/Resources/Sound/UI/skill_success.mp3`, `skill_fail.mp3` | UI SFX | `AutoUIManager.cs` | 2D Non-spatial | `GameAudioSettings.SFXVolume` | Active |

---

## 4. Unused / Raw / Third-Party Package Audio

| Asset Path | Category | Reason / Status |
| :--- | :--- | :--- |
| `Assets/SmallScaleInt/...` | Raw / Demo SFX | Packaged assets bundled with 2D tile pack; preserved intact without modification. |
| Duplicate / backup clips in subfolders | Backup Clips | Retained on disk per safety policy; not referenced in active gameplay scripts. |
