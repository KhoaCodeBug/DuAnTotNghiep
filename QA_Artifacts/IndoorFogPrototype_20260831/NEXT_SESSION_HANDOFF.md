# Indoor Fog Surface — flashlight gradient handoff

## Quy tắc bàn giao model — user chốt 2026-08-31

Mọi task tiếp tục mặc định dùng **Sol - High**. Khi tạo task, truyền rõ `model="gpt-5.6-sol"`, `thinking="high"`; không để cấu hình mặc định của app chọn Luna. Khi tiếp tục một task có sẵn, giữ/chọn Sol - High và xác minh metadata. Chỉ đổi khi user yêu cầu khác. Lượt sửa sau phản hồi ảnh 16:05 đã xác minh `gpt-5.6-sol/high` trong turn_context.

**Kết quả sửa Sol-High mới nhất:** radial fade (`boundary-candidate-edge.png`) làm tối cả mặt tường đã bị loại và giữ tại `RejectedRadialFade_20260831/`. Candidate mới chỉ grade silhouette near→far ở góc/cửa; ảnh `shadow-verified-*` giữ lõi tường/decor sáng như V2 và tối mép phải. Xem `SHADOW_BOUNDARY_REVIEW.md`: EditMode 4/4, PlayMode 5/5, A/B/A perf và giới hạn. Dải vẫn hẹp hơn concept ở vài góc; user chưa test tay, chưa push.

Task tiếp tục: `01a056fe-f433-72a1-88a1-28d920414269` (tạo trực tiếp cùng project Unity). Task nguồn `01a0561f-527b-7ea3-8cdb-5910cf3849ae` dừng triển khai sau bàn giao. Bản sao trước chuyển: `E:/Lap_trinh/HocTap/DuAnTotNghiepFIx/CodexBackups/IndoorFogBoundaryHandoff_20260831_154446/`, gồm patch tracked và ZIP133file chưa tracked, đã xác minh số entry; base ee0554d14. Không tự áp patch lên cây hiện tại đang có đủ thay đổi.

## PHÊ DUYỆT MỚI NHẤT — bắt đầu vòng fade ranh có giới hạn

User đã chấp nhận tối sớm ở phía sáng và cho làm tiếp; yêu cầu khắt khe với hình ảnh và logic ở nhiều vị trí/hướng/nhà khác. Đọc BOUNDARY_FADE_NEXT_ITERATION.md trước mọi mục cũ. Agent chọn chuyển sang task mới trước vòng triển khai; tiếp tục đúng project/local changes, không push. Các dòng dưới nói chỉ thảo luận/chưa cho implementation đã hết hiệu lực. Vẫn giữ đánh giá bản cone-only CHƯA ĐẠT và V2 là savepoint. Không nhân rộng chưa kiểm chứng.

## Current state — supersedes ALL historical sections below

UPDATE after manual review: flashlight gradient is REJECTED / not good enough. User marked the hard right-hand light/dark boundary in `Gradient_UserReview_20260831/`; cone-angle feather + Light2D core changes did not address the desired final boundary. Read the newest user-review section in FLASHLIGHT_GRADIENT_REVIEW.md. Discussion only at this point: assess inward darkening near final visible-region boundary; no new implementation approved yet. Preserve V2/save point and local experiment. Unity stopped after user finished testing. Future test handoff must enable existing God Mode, switch torch OFF, and restore movement before saying ready; God Mode was not successfully enabled in the last setup.

Read `FLASHLIGHT_GRADIENT_REVIEW.md` first. V2 was manually accepted except the intensity transition; tiny wall rim tolerated. Save point `ee0554d14` was pushed and subsequently merged externally through PR #326 into main `5aa09d71c` (same tree); the remote feature branch is now deleted. Local branch remains `codex/restore-indoor-vision-20260831` with uncommitted flashlight gradient + QA/docs. Do not recreate the remote branch or push unaccepted work automatically.

Implemented: wider inward intensity ramp (`flashlightConeFeather=.5`) plus matching Light2D inner core ~61.21 degrees ONLY with flashlight in opt-in sample house. Outer/gameplay cone145 unchanged; OFF100/140 and other-house torch105/145 unchanged. No occlusion/atlas/scene/prefab/Fusion changes. 44/44 EditMode + 5/5 PlayMode pass, 180-step motion and screenshots, same-binary A/B/A profile recorded in review. User has NOT manually accepted this gradient yet. Do not restart V1 flicker investigation. Await/handle latest user feedback, preserve new local code/evidence.

Runtime fixture: sample house `nhachinhxaydautien (12)`, player(-39.2,44.3), up, 13:30, flashlight ON, pose.json. Apply Runtime Pose after a new Solo session, then Return Manual Control. The component is runtime-only. Final screenshot/state `gradient-manual-ready`. Practical tests and limitations are in review. Backlog: ordinary-vision gradient after flashlight acceptance, minor wall rim, dynamic atlas/build/multiplayer before expansion.

## Historical V1/V2 handoff (retained for evidence)

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
