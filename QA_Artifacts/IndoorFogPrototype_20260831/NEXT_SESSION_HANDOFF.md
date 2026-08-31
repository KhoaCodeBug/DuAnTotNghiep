# Indoor Fog Surface Prototype — V1 user review and V2 handoff

Date: 2026-08-31

## Latest user decision (supersedes the V2 waiting status below)

User manually passed flicker, decor and indoor scope; minor black wall rim is tolerated/backlog. V2 is an authorized save point to commit/push on the current feature branch before the next change. The flashlight light-to-dark intensity transition is NOT accepted yet: it must fade strong → medium → dim → dark, not merely soften wall geometry. The next iteration is authorized for FLASHLIGHT ONLY; normal vision/ambient remains unchanged. Extending the gradient to unaided vision is a future-plan item, not current scope. Preserve real blocker visibility. Latest images: `V2_UserReview_20260831/`. User authorization covers pushing the current snapshot, not untested subsequent changes or a main merge.

## Status labels

- **Confirmed by user/manual observation:** V1 is broadly acceptable as a first prototype, and the user wants the feature continued.
- **Observed defects:** light-to-dark boundaries are still too hard compared with the approved concept; approaching some walls produces black streaks; moving close to walls can make those streaks flicker continuously.
- **Not yet diagnosed:** the exact source of the streak/flicker. The still images prove the artifact exists but cannot identify its temporal trigger.
- **Not accepted as stable:** V1 has automated coverage and a runtime screenshot set, but the user has explicitly found visual regressions. Do not promote it beyond the sample house yet.

User evidence is preserved in `V1_UserReview_20260831/`:

- `user-hard-transition.png`
- `user-near-wall-black-streak.png`

The approved visual target and user-marked concept remain in `../IndoorFogConcept_20260831/`.

## Required behavior for V2

1. Keep the surface reveal on visible wall, picture, shelf and cabinet faces.
2. Make the light-to-dark transition visibly smoother and closer to the approved concept.
3. Remove black streaks near walls and stop continuous flicker while the Player approaches or moves parallel to a wall.
4. Preserve occlusion: smoothing must not reveal floor, actors or objects behind a closed wall.
5. Continue with one sample house only until the result is visually accepted and measured.
6. After each isolated implementation step, capture a real Unity Game View image at fixed poses and compare it against both the approved concept and the V1 defect images. Record the mismatch honestly.

## Investigation order

Do not begin by adding a broad blur. First stabilize the visibility signal; otherwise blur can hide the shape while preserving temporal flicker.

1. Reproduce near each reported wall at fixed camera/zoom and record the position, facing, flashlight state and time of day. Capture a short sequence or multiple consecutive frames, not only one screenshot.
2. Determine whether the instability comes from the original 15 Hz ray fan/collider hit distance, discontinuous surface-atlas foot projection, point-sampled atlas lookup, disagreement between original and projected visibility combined with `max`, camera sub-pixel movement, or more than one factor. These are hypotheses, not confirmed causes.
3. Fix the unstable source with a bounded rule such as stable sampling, epsilon/hysteresis or temporal interpolation only after identifying which value flips. Do not patch individual wall coordinates.
4. Add spatial feathering that stays on the visible side of occluders. Compare the feather width and early darkening on the right wall to the approved concept.
5. Repeat day/night, flashlight on/off, approach/parallel movement, left/right facing, exit/return, prototype disable and the existing performance A/B/A measurement.

## Quality gates

- Do not change scenes or prefabs for the sample experiment unless inspection proves it is necessary and the user approves the scope.
- Do not extend to other houses before this house passes the user review.
- Keep Fog presentation local; do not add Fusion replicated state/RPC for the visual mask.
- Treat screenshot comparison as evidence, not pixel-perfect equivalence. Capture before/after with the same pose and Game View size.
- If two or three focused iterations still require house-specific exceptions, a renderer rewrite, or introduce visibility leaks, stop and discuss whether to abandon this enhancement rather than expanding complexity.
- Re-run compile/Console, the existing 44 Editor tests, the 5 relevant PlayMode tests, and focused runtime movement checks after material changes. Automated tests do not replace user acceptance.
- Re-measure CPU/GPU. V1 measured roughly +0.08 to +0.23 ms median GPU in the existing A/B/A run, with noisy CPU values; do not promise 60 FPS.

## V2 milestone stop gate

- V2 ends after the two approved defects are addressed in the sample house: stable near-wall motion without black streak/flicker, and a softer indoor light/dark boundary without wall leaks or clear regressions.
- Once compile/Console, focused tests, real movement evidence, fixed-pose comparison images and performance evidence meet the internal gate, stop implementation. Leave Unity in a playable sample-house state, return manual control and wait for user acceptance.
- Do not begin V3 automatically. Non-essential aesthetic or architectural improvements belong in a `Proposed V3` list.
- If two or three evidence-backed V2 iterations still leave a material defect or require house-specific exceptions/a renderer rewrite, stop at a testable checkpoint and ask the user to choose whether to continue.
- The agent cannot claim an exact context percentage without a supplied system value. Handoff proactively at a safe phase boundary before a large verification phase if the task context has become long; do not wait solely for the user to report the UI percentage.

## Safety and Git

- Stable pre-prototype checkpoint: `codex/checkpoint-vision-menu-stable-20260831` at `ddf440424`.
- Working branch at handoff: `codex/restore-indoor-vision-20260831`, HEAD `ddf440424`, dirty local prototype and QA artifacts, not committed or pushed.
- Do not reset, clean, restore or overwrite teammate/user changes. No push without explicit permission or successful user acceptance under repository rules.
- Canonical history: `CODEX_PROJECT_WORK_LOG.md`. Verify repository and Unity state before trusting this handoff.

## Next-session startup

1. Read `AGENTS.md`, the three Unity project skills, the latest entries in `CODEX_PROJECT_WORK_LOG.md`, this file, and `README.md` in this directory.
2. Inspect Git status/diff and verify Unity Editor/Console state.
3. Preserve the V1 implementation and evidence before editing.
4. Reproduce and instrument the wall flicker first. Do not claim a root cause from the screenshots alone.
5. Work in isolated steps. After every step, capture real in-game evidence and append the comparison and remaining defects to this handoff/work log.

## V2 completion candidate — awaiting manual acceptance

- Root cause evidence: abrupt near/far changes between adjacent rays created triangular surface shadows; using current Player position with cached scan distances added temporal disagreement. Atlas continuity was inspected and ruled out as the primary stripe source.
- Final bounded implementation pairs distances with `_IndoorOcclusionOrigin` and applies four-ray non-negative cubic B-spline reconstruction only to configured static surface pixels. World/floor/actors retain strict legacy distance occlusion.
- Visual evidence: `V2_Diagnostic_FinalBurst/`, `V2_Diagnostic_Step1/2/3/`, and four `v2-final-*.png` fixed poses. Final burst no longer shows the V1 black ray stripes in the reproduced path; real occluder edges remain deliberately visible.
- Verification: integration 1/1 job `75cc0c4b5a0a421b932d50822efda2c0`; EditMode 44/44 job `6c0247d57ee948328ad7372d81e14a50`; PlayMode 5/5 job `8d10a50c13f74cb7b47e9ccd55a0d14b`; final Console 0 error.
- Performance A/B/A median GPU: 3.244032 / 3.488768 / 3.227648 ms, V2 cost about +0.245 to +0.261 ms in this Editor sample; no extra draws/batches. Do not promise 60 FPS or extrapolate to build.
- Current gate: stop implementation and leave a playable sample-house runtime for user acceptance. Do not start V3. If user still reproduces flicker, collect exact position/facing/light/time and compare to the preserved burst before changing code.
