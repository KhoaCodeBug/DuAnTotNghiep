# Kế hoạch tiếp nối — Collider vô hình, FogOfWar và súng

> Cập nhật: 2026-08-29. Đây là checklist tập trung cho nhóm lỗi visibility/combat.
> Trạng thái tổng và lịch sử quyết định vẫn lấy từ `CODEX_PROJECT_WORK_LOG.md`.

## Tài liệu bắt buộc phải đọc khi mở phiên mới

1. `AGENTS.md` ở project root.
2. `.codex/skills/graduation-gameplay-workflow/SKILL.md`.
3. `.codex/skills/unity-project-workflow/SKILL.md`.
4. `CODEX_PROJECT_WORK_LOG.md` — nguồn trạng thái canonical.
5. File kế hoạch này.

Tài liệu/code đọc theo task:

- Collider/A*: `Assets/Scenes/Main.unity`, `Assets/Editor/EnvironmentColliderFixerWindow.cs`.
- Vision/Fog: `Assets/Khoa/Code/PlayerVision.cs`, `Assets/Khoa/Code/FogVisionController.cs`,
  `Assets/Khoa/Code/FlashlightController.cs`, `Assets/Khoa/Code/RoofDetector.cs`, hai prefab
  `Assets/Prefab/Player.prefab` và `Assets/Prefab/Player2.prefab`.
- Combat: `Assets/Script/Tin/PlayerCombat.cs`, `Assets/Resources/Items/AK47.asset`,
  `Assets/Resources/Items/S12K.asset` và các class health zombie.
- Regression: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`.
- Multiplayer/QA tồn đọng: `QA_Artifacts/P1P2Continuation_20260829/`.

## P0 — Collider vô hình tại marker `Collider_A*TangHinh`

Trạng thái: **đã sửa, đã scan A*, đang chờ nghiệm thu gameplay cuối**.

- Nguyên nhân đã xác nhận: `==========Wall===========/HangRao (3)` là một
  `PolygonCollider2D` solid trên layer `Obstacle`, không có renderer, chồng đúng marker
  `(-0.604, -2.844)`. A* vì thế bake node tại marker thành unwalkable.
- Đã xóa riêng `HangRao (3)`, không xóa ba collider `HangRao` còn lại vì Scene view cho thấy
  chúng khớp hàng rào/biên map có hình thật.
- Giữ marker để làm tọa độ hồi quy. A* scan sau sửa hoàn tất 485.805 node.
- Regression bắt buộc: scene còn marker và không được tái xuất hiện `HangRao (3)`.

Acceptance:

- Player đi xuyên qua marker không bị chặn.
- Zombie/A* chọn được đường qua điểm này khi mục tiêu ở phía đối diện.
- Không làm hở ba hàng rào thật lân cận.
- Nếu phát hiện điểm khác, đặt marker riêng rồi audit collider tại đúng điểm; không xóa hàng loạt
  các GameObject collider-only vì nhiều object là biên map có chủ đích.

## P1A — X-Ray local Player

Trạng thái: **đã triển khai vertical slice; cần QA hình ảnh trong gameplay**.

- `PlayerVision` tạo silhouette runtime local-only bằng SpriteRenderer unlit ở sorting layer trước nhất.
- Chỉ Player đang được camera local/spectator theo dõi có silhouette; không sync qua Fusion.
- Silhouette bám sprite animation/flip/size của body; mặc định xanh nhạt alpha 0.32.

Acceptance:

- Player vẫn nhận diện được khi bị tường, mái, cây hoặc foreground sprite che.
- Remote player không tự có X-Ray trên máy người khác nếu không phải camera target.
- Không có bản sao silhouette còn sót sau despawn/respawn/spectator switch.
- Nếu silhouette luôn-hiện gây gắt, task polish kế tiếp là occlusion-only bằng material/stencil;
  không đổi renderer pipeline trước khi có mockup/tiêu chí hình ảnh được duyệt.

## P1B — Vùng cảm nhận zombie 360° có fade

Trạng thái: **đã triển khai; cần QA runtime và profiling nhỏ**.

- Chuẩn hóa cả Player/Player2 ở bán kính 2m.
- Hoạt động cả trong lẫn ngoài nhà và sau lưng.
- Fade local-only 0.25 giây, alpha khởi đầu 0.18; chỉ điều khiển alpha, giữ RGB để không phá hit flash.

Acceptance:

- Zombie bước vào 2m từ mọi hướng chuyển mờ → rõ, không bật hình đột ngột.
- Ra khỏi vùng và không còn LOS thì fade out rồi disable renderer.
- TutorialForceVisible vẫn ưu tiên hiển thị.
- Hai client nhìn cùng zombie có thể thấy khác nhau theo camera/Player của chính client.

## P1C — Đèn pin bị tường chặn nhưng không bị hàng rào thấp chặn

Trạng thái: **đã xác nhận nguyên nhân và thiết kế; chưa mutate toàn map**.

Nguyên nhân:

- `Light2D.shadowsEnabled=true` nhưng hiện không có `ShadowCaster2D` trong scene/prefab, nên ánh
  sáng xuyên tường là hành vi tất yếu.
- `Obstacle` đang gộp tường, hàng rào và vật cản di chuyển. Dùng toàn bộ layer này làm vision blocker
  sẽ khiến hàng rào ngoài trời chặn sáng vô lý.

Thiết kế cần làm:

1. Tạo semantic riêng `VisionBlocker2D`/layer ánh sáng cho tường kết cấu, không tái sử dụng toàn bộ
   `Obstacle`.
2. Tạo/author `ShadowCaster2D` proxy cho tường nhà và vật thể cao; loại hàng rào thấp, bụi cây,
   mép pavement.
3. Cửa đóng phải cast/block LOS; cửa mở phải tắt hoặc đổi geometry shadow tương ứng.
4. Cho `PlayerVision` raycast LOS dùng cùng semantic blocker để Fog và ánh sáng không mâu thuẫn.
5. Làm vertical slice trên **một căn nhà + một hàng rào kế bên**, đo hình ảnh và performance trước
   khi nhân rộng toàn map.

Acceptance vertical slice:

- Đèn pin không rọi xuyên tường kín; rọi qua cửa mở.
- Hàng rào thấp ngoài map không cắt cone sáng.
- Zombie sau tường không được LOS reveal nhưng vẫn hiện nếu bước vào vùng cảm nhận 2m.
- Không tạo collider vật lý mới hoặc thay đổi A* chỉ để giải quyết shadow.

## P1D — AK47/S12K damage, accuracy và bắn sát người

Trạng thái: **đã triển khai; cần runtime combat QA**.

- AK47: damage 34 → 42, spread authored 2° → 0.75° (single-pellet hiện vốn không random spread).
- S12K: 15 → 24 damage mỗi pellet, spread 12° → 8°, giữ 9 pellet và range 12m.
- Host/State Authority chọn zombie còn sống trong 1.2m nếu nằm trong cone aim 70° nửa góc.
- Đường đạn dùng CircleCast radius 0.08m để tránh lọt khe collider ở point blank.

Acceptance:

- Zombie 100 HP ở gần chết khi nhận tối thiểu 5 pellet S12K (5 × 24 = 120).
- AK47 không hụt zombie đang chồng sát Player khi ngắm về phía zombie.
- Aim assist không khóa zombie sau lưng ngoài cone và không gây damage từ client authority.
- Corpse bị bỏ qua như trước; infected-player friendly-fire giữ nguyên hành vi.

## P2 — QA multiplayer/performance sau khi P1 ổn định

Trạng thái: **còn lại**.

- Chạy Host + Client thật: spawn, local X-Ray độc lập, zombie fade độc lập, AK/S12K damage chỉ Host
  áp dụng một lần; late join/respawn không tạo silhouette thừa.
- Chạy 3 peer để xác minh corpse-loot targeted RPC privacy/race.
- Readiness/disconnect/death boundary với số peer cao hơn nếu máy cho phép.
- Profiler horde 80–112 zombie và soak 60 phút; theo dõi allocation từ dictionary visibility và
  physics query.
- Không coi automated pass là nghiệm thu gameplay; ghi rõ mục nào assistant tự QA và mục nào cần
  người dùng xem cảm giác/hình ảnh sau cùng.

## Lệnh/check bắt buộc trước khi push task kế tiếp

- Unity compile/Console: 0 error mới.
- Full EditMode + PlayMode.
- `git diff --check`.
- `git status --short` và review từng scene/prefab/YAML diff.
- Nếu có network behavior: ít nhất Host + Client live; ghi room, số peer và bằng chứng authority.
- Chỉ push branch `codex/*`; không push `main` nếu chưa có yêu cầu rõ ràng.
