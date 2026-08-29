---
name: unity-project-workflow
description: "Use for any work in this Unity 6 zombie-survival project: gameplay, C# code, scenes, prefabs, UI, audio, assets, performance, localization, tests, Git, or Fusion multiplayer. Enforces context discovery, bounded planning, independent verification, and honest handoff."
---

# Unity Project Workflow

This is the project-level workflow guardrail. It routes each task to the
appropriate specialist skill while protecting existing Unity content and
requiring evidence before completion claims.

## Project context

- Project root: E:\Unity\GameObject\Game3D\ProJectZomboiNhai
- Engine: Unity 6.0.69f1; verify ProjectSettings/ProjectVersion.txt before relying on this.
- Game: top-down zombie-survival game with Solo and Fusion Host Mode multiplayer.
- Main gameplay scene: Assets/Scenes/Main.unity; verify before editing.
- The repository's current files and Unity serialization are the source of truth.
- CODEX_PROJECT_WORK_LOG.md is the historical handoff; read it before gameplay,
  scene, multiplayer, QA, or Git work, then verify its claims against the
  current repository.

## Non-negotiable operating rules

1. Do not guess file paths, scene objects, prefab references, package versions,
   network authority, or expected behavior. Inspect the repository first.
2. Do not claim a bug is fixed, a build passes, or a feature is complete without
   fresh verification evidence from the relevant command or runtime test.
3. Separate these states in every report: diagnosed, planned, implemented,
   automatically tested, manually tested, and not verified.
4. Preserve user and teammate changes. Never use destructive reset, clean, or
   restore operations or overwrite unrelated files to make the tree look clean.
5. Keep the requested scope bounded. Do not perform a broad refactor, asset
   migration, scene rewrite, or architecture change without explicit approval.
6. If an important choice is ambiguous and can change gameplay, UX, save data,
   scene wiring, network state, or compatibility, stop and ask before editing.

## Phase 1 — Preflight and context

Before implementation:

1. Read the relevant project handoff and work-log sections.
2. Run git status --short --branch and inspect the relevant diff.
3. Inspect ProjectVersion.txt, Packages/manifest.json, the relevant C# files,
   scenes, prefabs, ScriptableObjects, and existing tests.
4. Identify the smallest set of files and serialized objects that can satisfy
   the request.
5. Write a short plan containing:
   - observed current behavior;
   - root-cause hypothesis or design decision;
   - files, assets, and scenes that may change;
   - risks and preserved behavior;
   - exact verification matrix.

## Phase 2 — Route to the right specialist

Use the installed specialist skill when its scope applies:

- Unity code, editor, rendering, physics, asset, input, or general engine work:
  unity-developer
- General game architecture or 3D/gameplay principles:
  game-development, 3d-games, game-design, or game-audio
- Any bug, regression, timing issue, or unexpected behavior:
  systematic-debugging before proposing a fix
- A multi-step feature:
  writing-plans, then executing-plans
- Tests failing or a regression:
  test-fixing and suitable Unity EditMode/PlayMode tests
- UI, layout, overlay, or visual regression:
  ui-visual-validator, with screenshots or exact runtime observations
- Text, locale, or language synchronization:
  i18n-localization
- Performance or loading:
  use Unity Profiler, Frame Debugger, or Memory Profiler evidence; do not infer
  performance from code inspection alone
- Fusion, networking, late join, or reconnect:
  multiplayer plus the project's network-authority rules
- Commit, branch, merge, or push:
  git-pushing and finishing-a-development-branch, while following the explicit
  Git permission rules below
- Before any success or completion statement:
  verification-before-completion

Do not invoke unrelated web, backend, mobile, or cloud skills for a Unity task
merely because they are installed.

## Phase 3 — Implementation discipline

- Make one logically isolated change group at a time.
- For C# changes, preserve Unity lifecycle, assembly boundaries, null safety,
  serialization, and existing public behavior unless the plan says otherwise.
- For scenes and prefabs, locate the authoring object and verify every
  component, GUID/reference, NetworkObject registration, layer, tag, and
  transform.
- For UI, verify Canvas sorting/order, anchors, safe area, resolution scaling,
  and overlap with hotbar, chat, and modal overlays.
- For loading or scene transitions, instrument actual milestones instead of
  using arbitrary delays or moving the UI to hide a race.
- For localization, verify the source locale, runtime locale, fallback strings,
  and every visible loading and gameplay message.

## Network gate when applicable

For any network-affecting change, explicitly identify State Authority,
request/RPC flow, replicated state, client presentation, late-join snapshot,
spawn/despawn, reconnect, and failure behavior. Test Solo separately; never
assume a Solo pass proves Host/Client correctness.

## Phase 4 — Verification gate

Run the narrowest relevant checks, then broaden them:

1. Refresh and compile Unity; read the complete Console result.
2. Run relevant EditMode tests.
3. Run relevant PlayMode tests covering the real flow, not only helpers.
4. If scenes or prefabs changed, verify references and runtime instantiation.
5. For multiplayer, run Host/Client or ParrelSync checks when the environment
   permits; record limitations rather than inferring success.
6. Reproduce the original symptom and test important edge cases.
7. Re-check git diff --check and the final status.

Use this result format:

- Command or test and exact pass/fail count.
- Runtime steps and expected observation.
- Evidence path or log/screenshot identifier.
- Unverified items and remaining risks.

verification-before-completion is mandatory: no done, fixed, stable, or
equivalent claim based only on confidence, a previous run, or another agent's
report.

## Git safety gate

- Do not stage all files blindly; stage only reviewed intended paths.
- Do not commit or push merely because compilation or tests pass.
- Push and merge require explicit user authorization for that operation in the
  current task.
- Prefer a codex/... feature branch and never push directly to main unless the
  user explicitly requests it after the risk is explained.
- Before merge, compare both sides and resolve Unity YAML conflicts by object,
  component, and serialized reference—not by accepting an entire side.

## Handoff

Every meaningful task report must state:

1. What changed and what was intentionally preserved.
2. Files, scenes, and prefabs changed.
3. Network and authority impact when relevant.
4. Tests and runtime checks with real results.
5. Manual test steps with expected observations.
6. Unverified gaps and remaining risks.
7. Git branch, commit, dirty files, and push status.

If the request is analysis-only, do not edit files; provide evidence and the
bounded implementation plan for later approval.
