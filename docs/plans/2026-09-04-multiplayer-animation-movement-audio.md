# Multiplayer Animation, Movement and Audio Settings Plan

> Implementation owner: Gemini/Antigravity. Codex will independently review and verify the resulting changes.

**Goal:** Fix multiplayer bash/hit/death animation playback for host and clients, remove local visual jitter that is not caused by network latency, and make Music/SFX volume settings consistent between Main Menu Options and Pause Menu Options.

**Evidence already found:**

- `PlayerCombat` and `PlayerHealth` currently resolve their animator with `GetComponentInChildren<Animator>()`.
- `Player.prefab` lists `MuzzleFlash` before `Visual`; the muzzle animator uses `MuzzleFlash.controller`, while the `Visual` animator uses `Player.controller` with `GunBash`, `RandomBash`, `TakeDamage`, and `IsDead` parameters. `PlayerMovement` already has an explicit `Visual` animator reference, so the combat/health lookup is inconsistent and can select the wrong animator.
- `PlayerCombat.Bash()` only sends `RPC_PlayBashAnimation` from the state-authority path, while the RPC is declared for state/input authority sources. The final design must define local prediction, authoritative replay, and duplicate suppression for host, owning client, and observing client.
- `Player.prefab` and `Player2.prefab` use `NetworkRigidbody2D` with a `Visual` interpolation target and Rigidbody2D interpolation disabled. The gameplay scenes expose `ClientPhysicsSimulation: 0`, while `Prototype Runner.prefab` has `ClientPhysicsSimulation: 2`; the active runner/physics configuration and every transform writer must be verified before changing smoothing.
- Audio settings use `GameMasterVolume`, `GameMusicVolume`, and `GameSFXVolume`, but Main Menu and Pause Menu create separate sliders/callbacks around shared temporary fields. `CreateSlider` does not synchronize the paired slider live, and gameplay/UI code reads SFX/music values through several paths.

**Likely files to inspect or change (only if required by the diagnosis):**

- `Assets/Script/Tin/PlayerCombat.cs`
- `Assets/Script/Tin/PlayerHealth.cs`
- `Assets/Script/Tin/PlayerMovement.cs`
- `Assets/Script/Tin/PlayerAnimationEventForwarder.cs`
- `Assets/Script/Tin/Multiplayer/PlayerNetworkInput.cs`
- `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`
- `Assets/Script/Tin/MainMenuManager.cs`
- `Assets/Script/Tin/GlobalSettingsManager.cs`
- `Assets/Script/Tin/GameplayMusicController.cs`
- `Assets/Script/Tin/AutoUIManager.cs`
- `Assets/Script/Tin/GameplayAudioSpatializer.cs`
- `Assets/Prefab/Player.prefab`, `Assets/Prefab/Player2.prefab`
- active gameplay scene runner/physics configuration (`Assets/Scenes/Main.unity`, `Thai.unity`, `Tin.unity`, or the actual scene confirmed by the runtime)
- focused EditMode/PlayMode regression tests under `Assets/Script/Tin/Prototype/Tests`

**Implementation constraints:**

- Do not change unrelated gameplay, quest, zombie, scene, prefab, or audio data.
- Preserve Fusion authority/security semantics and existing public API/serialized references unless a compatibility wrapper is necessary.
- Do not solve jitter by blindly adding `Lerp` to the networked root, by writing to the physics root from `Update/LateUpdate`, or by enabling multiple interpolation systems.
- Do not delete or rename sound assets. Preserve the existing PlayerPrefs keys and Save/Back/unsaved-change behavior unless the change is required and documented.
- Keep diagnostic logging temporary, editor/diagnostic-toggle gated, and include enough identity/tick/frame data to prove the writer and correction source.

## Work sequence

1. Reproduce and instrument the three issues with host plus one client. Record `HasStateAuthority`, `HasInputAuthority`, `Runner.IsForward`, object identity, animator path/controller, required parameter presence, Fusion tick/frame, root transform, Rigidbody2D position/velocity, and `NetworkRigidbody2D.InterpolationTarget` position. Identify whether each visible symptom is caused by wrong component binding, RPC authority/prediction, animation culling/state reset, physics mode mismatch, correction, or a competing transform writer.
2. Fix animation binding through an explicit, deterministic `Visual` animator resolver or serialized reference for both player prefabs. Validate the resolved controller is the character controller, never the muzzle controller. Make bash/hit/death event delivery authoritative and reliable for all observers, with intentional owner prediction and deduplication. Keep gameplay damage/death mutation state-authoritative; animation RPCs must not duplicate damage, SFX, or death logic.
3. Fix movement presentation only after the measurements identify the writer/timing conflict. Keep the physics root authoritative and the child visual as the sole render interpolation target. Align the active scene’s `RunnerSimulatePhysics2D`/client physics/tick settings with the intended Fusion mode, remove duplicate root/child writes, and handle start/stop/direction/collision/respawn/crouch/vehicle transitions without visible snaps. Do not mask a deterministic correction with arbitrary smoothing.
4. Build a complete audio inventory from `Assets/Resources/Sound`, `Assets/Music`, inspector references, `Resources.Load` calls, and every `AudioSource`/`AudioClip` use. Classify each used asset as Music/BGM, SFX (UI, action, body, footstep, melee, weapon, vehicle, zombie), or Story/Voice; separately report unused/duplicate/raw source files. The current known folders include Actions, BodyState, Footsteps, Melee, UI, Vehicles/Repair, Weapons/AK47, Weapons/S12K, `ThemeGamePlay.mp3`, the main-theme resource, and Story/RouteB. Do not assume Story/Voice is BGM.
5. Introduce one runtime source of truth for master/music/SFX values, retaining keys `GameMasterVolume`, `GameMusicVolume`, and `GameSFXVolume`. Bind both Main Menu and Pause Menu sliders to it, synchronize the paired sliders and labels in both directions without recursive callbacks, apply preview values consistently, and preserve Save/Back/revert semantics. Ensure `AudioListener`, menu BGM, gameplay BGM, UI SFX, player SFX, weapon/melee/vehicle/zombie SFX, and voice/story playback use the intended category/master multiplier rather than mixed direct reads.

## Verification required before reporting success

- Unity compiles with no new errors or warnings caused by the change.
- EditMode tests cover deterministic animator resolution/controller parameters, RPC/event policy helpers if introduced, audio catalog completeness/categories, and bidirectional slider/settings synchronization including recursion protection and PlayerPrefs persistence.
- PlayMode tests or a documented two-peer harness verify on both host and client: bash, hit, and death animations visibly enter the expected states on the local avatar and every remote avatar; no duplicate trigger causes skipped/reset animation; damage/death authority remains correct.
- Movement is checked in a repeatable matrix: idle, slow walk, run, stop, 4+ direction changes, aim/crouch, collision, spawn/respawn, and vehicle transitions. Capture root-vs-visual diagnostic evidence and at least one screenshot per relevant host/client view; report measured correction/jitter thresholds and the test scene/configuration.
- Audio is checked in Main Menu and Pause Menu in both directions for all three sliders, before and after Save, after Back/reopen, and after scene transition. Confirm music and SFX are independently audible/muted as configured and master volume affects both categories.
- Report exact changed files, authority/physics/audio design decisions, EditMode and PlayMode command/output summaries, Unity Console status, screenshots, and any known limitation. Leave the temporary prompt/test artifacts in place per repository workflow.
