# Kế hoạch tiếp nối — Collider vô hình, FogOfWar và súng

> Cập nhật: 2026-08-30. Đây là checklist tập trung cho nhóm lỗi visibility/combat.
> Trạng thái tổng và lịch sử quyết định vẫn lấy từ `CODEX_PROJECT_WORK_LOG.md`.

## Cập nhật 2026-08-31 — Khôi phục vision trước bản FOW toàn map

- Người dùng yêu cầu bỏ cơ chế FOW/LOS mới của Tín vì mất cảm giác Fog cũ và tạo mảng đen ngoài trời.
- Đã khôi phục Fog controller/shader từ checkpoint local `7987af306` (gameplay `01455503e`),
  cùng đúng patch PlayerVision sửa mái trường chưa commit của phiên `01a05047-5995-79b1-984a-f38ca91e1466`.
- Ngoài trời dùng Fog/thời tiết, góc nhìn và đèn pin cũ; fan che theo tường chỉ chạy khi có indoor trigger.
  Giữ hình học nhận tường công trình lớn ngoài hierarchy và fallback mái trường bị chia nhiều polygon.
- Không khôi phục inventory/readiness của checkpoint cũ trong lượt này: loading/chat/loot/network hiện tại giữ nguyên.
- Backup toàn trạng thái Git: `codex/backup-vision-before-restore-20260831` tại `a23c33247`.
  ZIP vision và patch lịch sử: `QA_Artifacts/VisionRollback_20260831_091549/`.
- Kết quả kiểm tra mới và giới hạn nghiệm thu nằm ở entry 2026-08-31 trong work log.
- Test tay ưu tiên: MainMenu → Solo → Medium (có đèn pin trong túi); trang bị đèn lên hotbar,
  quan sát Fog/góc nhìn ngoài trời, bật/tắt đèn pin,
  vào/ra nhà rồi bệnh viện/trường; ngoài trời không được giữ mảng đen từ indoor, tường kín trong nhà vẫn chặn.
- Chưa nghiệm thu thay người dùng; không tự push/merge.

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
- Starter loadout: `Assets/Script/Tin/InventorySystem.cs`,
  `Assets/Script/Tin/Prototype/DifficultyRules.cs`, `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`.
- Zombie/A*: `Assets/Khoa/Code/ZombieAI_Khoa.cs`, `Assets/Khoa/Code/ZombieAIKhoaRebuilt.cs`,
  `Assets/Khoa/Code/ZombieSpawnZone.cs` và ba prefab Zombie trong `Assets/Khoa/`.
- Regression: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`,
  `Assets/Script/Tin/Prototype/Tests/PlayMode/VisibilityAndZombieRegressionPlayModeTests.cs`.
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

Trạng thái: **đã triển khai bản 1,5m và test tự động; cần QA cảm giác runtime**.

- Chuẩn hóa cả Player/Player2 ở bán kính 1,5m.
- Hoạt động cả trong lẫn ngoài nhà và sau lưng; tường kín vẫn chặn cảm nhận.
- Fade local-only 0.25 giây, alpha khởi đầu 0.18; chỉ điều khiển alpha, giữ RGB để không phá hit flash.

Acceptance:

- Zombie bước vào 1,5m từ mọi hướng chuyển mờ → rõ, không bật hình đột ngột.
- Ra khỏi vùng và không còn LOS thì fade out rồi disable renderer.
- TutorialForceVisible vẫn ưu tiên hiển thị.
- Hai client nhìn cùng zombie có thể thấy khác nhau theo camera/Player của chính client.

## P1C — Đèn pin bị tường chặn nhưng không bị hàng rào thấp chặn

Trạng thái: **đã triển khai occlusion cục bộ theo căn nhà; automated runtime pass, cần QA hình ảnh**.

Nguyên nhân:

- `Light2D.shadowsEnabled=true` nhưng hiện không có `ShadowCaster2D` trong scene/prefab, nên ánh
  sáng xuyên tường là hành vi tất yếu.
- `Obstacle` đang gộp tường, hàng rào và vật cản di chuyển. Dùng toàn bộ layer này làm vision blocker
  sẽ khiến hàng rào ngoài trời chặn sáng vô lý.

Giải pháp đã triển khai:

1. Khi Player ở trong nhà, `FogVisionController` dựng fan 180 tia local ở 15Hz.
2. Tia physics vẫn query `Obstacle`, nhưng chỉ nhận hit là con của đúng `RoofVisibility`/
   `IndoorVisionArea` đang chứa Player. Vì vậy tường và đồ cản thuộc nhà chặn; hàng rào ngoài map bị bỏ qua.
3. Shader dùng khoảng cách tia để giữ Fog gần như kín (alpha 0,98) sau tường, che cả phần Light2D
   có thể rò; khe cửa không có collider vẫn cho ánh sáng/tầm nhìn đi qua.
4. Không thêm collider, không đổi layer map, không phụ thuộc `ShadowCaster2D`, không đổi authority Fusion.
5. PlayMode regression đã xác nhận một hàng rào gần hơn bị bỏ qua và tia dừng ở tường của căn nhà.

Acceptance vertical slice:

- Đèn pin không rọi xuyên tường kín; rọi qua cửa mở.
- Hàng rào thấp ngoài map không cắt cone sáng.
- Zombie sau tường không được LOS reveal; vùng cảm nhận 1,5m cũng không xuyên tường.
- Không tạo collider vật lý mới hoặc thay đổi A* chỉ để giải quyết shadow.

## P1E — Starter weapon Solo đôi lúc bị hụt

Trạng thái: **đã sửa retry/idempotence; giữ nguyên luật độ khó**.

- Easy vẫn nhận AK47 + 30 Ammo762 + Meat; Normal chỉ Flashlight + Bandage; Hard không có starter gear.
- Trước đây code đặt `HasStartingWeapon/hasAppliedStartingWeaponLocally` dù thao tác hotbar có thể thất bại,
  nên lỗi transient không còn đường retry.
- Nay mỗi thao tác trả kết quả; chỉ đặt `StartingLoadoutResolved` khi toàn bộ loadout đã có đủ.
- State Authority retry 0,5 giây/lần; retry kiểm tra số lượng hiện có nên không nhân đôi item.
- Military respawn snapshot được đánh dấu resolved trước khi restore để không cấp starter gear chồng lên save.

Acceptance:

- Easy Solo luôn có đúng một AK47 trong hotbar và đủ ammo/meat sau spawn.
- Retry không tạo AK47 hoặc item phụ trùng.
- Normal/Hard không tự nhận súng.
- Respawn military giữ nguyên snapshot, không tái cấp starter loadout.

## P1F — Zombie chìm/tàng hình/xuyên tường trong nhà

Trạng thái: **đã sửa lớp phòng thủ movement; A* scan pass, cần soak trong nhiều căn nhà**.

- Layer prefab Zombie là `Enemy` và `obstacleMask` là `Obstacle`; collision matrix Enemy–Obstacle đang bật.
- A* Main scan thành công 485.805 node / 1 graph; spawn zone chỉ spawn trên node Walkable.
- Nguyên nhân xuyên tường còn lại là Rigidbody2D Kinematic di chuyển bằng `MovePosition`: steering/flocking có
  thể cắt góc giữa waypoint và Kinematic không tự bị static wall đẩy lùi.
- Cả brain cũ và rebuilt nay dùng `Rigidbody2D.Cast` theo toàn bộ thân trước mỗi bước; quãng đường được cắt
  tại collider Obstacle, `NetSpeed` phản ánh quãng đường thực và logic stuck sẽ repath.
- Không đổi sang Dynamic vì phương án đó có rủi ro đẩy Player, rung va chạm và lệch host/client.

Acceptance:

- Zombie không xuyên tường ngoài/internal wall khi chase, flock hoặc repath ở góc hẹp.
- Zombie bị tường giữ lại phải repath, không chạy animation tại chỗ vô hạn.
- Không bị ẩn do Fog khi đang cùng phía tường và trong LOS/vùng cảm nhận.
- Soak trong các nhà nhỏ, school, hospital; ghi marker nếu còn vị trí spawn/sorting cụ thể.

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
