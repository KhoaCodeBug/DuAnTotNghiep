# Phục hồi PlayerVision / Fog — 2026-08-31

## Nguồn phục hồi và bản sao an toàn

- Task đúng: `codex://threads/01a05047-5995-79b1-984a-f38ca91e1466`, **QA Fog, starter loadout và zombie indoor**.
- Trước sửa: `main` và `origin/main` tại `a23c33247323ed2d7670869c11298bc67aa0832e`; working tree sạch.
- Nhánh giữ toàn bộ trạng thái trước sửa: `codex/backup-vision-before-restore-20260831` tại cùng commit.
- Nhánh đang làm: `codex/restore-indoor-vision-20260831`; không reset main, không commit/push/merge.
- `vision-before-restore-a23c33247.zip` lưu các nguồn vision, shader, prefab, tests và tài liệu trước sửa.
  SHA256: `06C5A8F2D1633B401803DF70846A04F47CF2688A675D5F134DFAC6DC830CC2DD`.
- `prior-vision-7987af306.zip` và `historical-school-visibility.patch` giữ nguồn cũ cùng patch chưa commit được khôi phục từ lịch sử task.
- `restored-vision-snapshot.zip` lưu ba file vision sau phục hồi (SHA256 `B7E359D5ECADE72F2257DA826F9E995F578A8E7C914E4C99FE2B7F1C3FAA1AFA`).

## Thay đổi có chủ đích

- `FogVisionController.cs` và `FogVisionOverlay.shader` khớp byte nội dung Git của checkpoint `7987af306` (checkpoint gameplay `01455503e`).
- `PlayerVision.cs` gồm checkpoint đó và đúng patch mái trường của phiên cũ. Git blob SHA1 sau phục hồi:
  `0f9415ea35d81e5498b4f2f82e54555613f2ad0d`.
- Bỏ fan occlusion ngoài trời mới; khôi phục Fog/thời tiết, cone, fade và đèn pin trước đó. Fan che tường chỉ kích hoạt khi Player có indoor trigger.
- Giữ sửa hình học bệnh viện/trường có tường nằm ngoài hierarchy mái; giữ fallback mái trường nhiều polygon khi cả pivot Player bị polygon từ chối.
- Loại `VisionLineOfSight.cs`, `FowLosArtifactRunner.cs` và hai meta vì chỉ thuộc cơ chế mới đã bỏ. Không còn code reference hoặc serialized GUID reference trong scene/prefab/asset.
- Khôi phục tests cho indoor scope và mái trường; bỏ tests riêng cho thiết kế FOW ngoài trời bị user yêu cầu hoàn tác.
- Giữ test AK47/S12K placement của main; thêm regression trên map thật và teardown để không để collider map nhiễu test dựng riêng.
- Không đổi scene, prefab, Inventory/DifficultyRules, loading/readiness, chat, combat, AI, quest, multiplayer hay authority/RPC.
  Hai prefab Player hiện tại cũng khớp checkpoint nguồn.

Không rollback toàn repository hoặc cherry-pick cả nhánh cũ vì sẽ cuốn theo loadout/loading và các sửa khác ngoài yêu cầu.

## Bằng chứng runtime

Đã chạy Solo qua UI Button events thực trong MainMenu, sau đó dùng đường teleport Fusion của production để đặt Player chính xác tại các điểm kiểm tra. Đây là runtime automation, không phải hoàn thành tuyến quest bằng đi bộ.

| Mẫu | Tọa độ | Indoor mask |
| --- | --- | --- |
| Ngoài trời | -62.29, 30.77 | 0 |
| Bệnh viện lớn | -46.646, 16.413 | 1 |
| Ra khỏi bệnh viện | -62.29, 30.77 | 0 |
| Bệnh viện nhỏ | -49.584, 37.427 | 1 |
| Trường học | 11.36, 49.93 | 1 |
| Ra khỏi trường | 11.36, 37.50 | 0 |

- Có lượt Easy và lượt Medium; Medium trang bị đèn pin starter bằng đường equip của Inventory.
- Tại cả sáu điểm: đèn off → on → off truyền đúng tới shader, không thay đổi phạm vi indoor mask.
- Shader supported, vị trí thực khớp vị trí yêu cầu; ảnh cho thấy cover trong nhà và không còn fan đen ngoài trời tại các điểm đã lấy mẫu.
- 12 ảnh on/off cùng `RUNTIME.txt` đã xác minh: `verified-runtime/` (snapshot cố định). `../VisionRollback_Runtime/` là đầu ra có thể bị ghi đè khi chạy lại test.
- Ca mái trường zombie và tia bỏ qua hàng rào ngoài công trình có assertions riêng; chưa tương đương soak horde toàn map.

## Lỗi setup QA đã phân biệt với lỗi production

- Lúc đầu quest giữ Player trong trường do chưa có ba manh mối. Fixture nay hoàn tất clue mask runtime trước khi đo việc rời trường; không sửa quest production.
- Reflection tới `CompleteClueMask` phải đọc property ở assembly QuestUI, không phải field/Assembly-CSharp.
- Lượt full PlayMode đầu sau thêm ca map thật fail hai test dựng riêng do map/collider chưa được unload. Bổ sung UnityTearDown; chạy lại riêng nhóm vision đạt **5/5** (`b3d6109c050744b3804c1228f5048610`).

## Giới hạn và quan sát cần giữ lại

- Đã thao tác desktop MainMenu → Solo → Medium; gặp nền menu che map sau tải. Console có `ShowLoadingScreen ignored: Scene is already loaded or gameplay is released`. Lượt thao tác sau reload cũng chưa vào được gameplay qua click. Không báo luồng desktop này pass.
- Project tắt Domain Reload (`m_EnterPlayModeOptions: 1`); trạng thái tĩnh sót giữa lượt chạy là giả thuyết, chưa xác nhận nguyên nhân. Không sửa loading trong task này.
- Console trước thay đổi đã có loading timeout 36.4s; sau các lượt test có hai thông báo EventSystem trùng và log runner `Saving results to...` bị phân loại Exception. Không tuyên bố Console hoàn toàn sạch hoặc các lỗi này đã được sửa.
- Chưa chạy Host+Client, build player, horde soak, nhiều góc cửa/giờ ngày đêm hay nghiệm thu thị giác của người dùng. Các ảnh FPS là ảnh chụp tức thời, không phải phép đo hiệu năng.

## Cách test thực tế

1. MainMenu → Solo → Medium, mở túi và trang bị đèn pin lên hotbar; chọn slot, chuột trái để bật/tắt.
2. Đi ngoài đường, sát hàng rào và quanh nhà, xoay hướng nhìn: không xuất hiện fan đen lớn của cơ chế mới; Fog/thời tiết cũ vẫn được giữ.
3. Vào bệnh viện lớn/nhỏ và trường: tường kín che phần phía sau; cửa mở còn cho nhìn theo hình học; zombie gần trong mái trường không biến mất chỉ vì polygon bị chia.
4. Bật/tắt đèn và đi ra khỏi công trình (trường cần hoàn tất quest): indoor mask phải tắt, không kéo vùng che ra cả map.
5. Cần QA Host+Client riêng để xác nhận mỗi peer nhìn theo Player local; không suy ra từ Solo.

## Kết quả cuối

- Unity 6000.0.69f1 compile thành công; không có C#/shader compile error được ghi nhận.
- Full EditMode: **173/173 passed**, 0 failed, 0 skipped, 2.8449938s; job `10a89167a5254d1fba4ce7ed46e8dd8f`.
- Full PlayMode: **15/15 passed**, 0 failed, 0 skipped, 106.6487751s; job `daef476b9fb9434d804963f92c56e6eb`.
- Ảnh full suite bị VictorySummaryUI từ test quest trước che; lượt standalone ngay sau đó còn nền menu do trạng thái giữ lại. Không dùng những ảnh này làm bằng chứng thị giác. Sau force compile/domain reload đã được xác nhận, chạy riêng ca vision: **1/1 passed**, 22.5454773s, job `98b547a20c5a4c5a8d56104f426a7ff7`. Đã mở xem ảnh gameplay mới trong/ngoài bệnh viện và trường; snapshot `verified-runtime/` lấy từ đúng lượt này.
- Full suite xác minh state/physics bằng assertions; không có pixel assertion cho overlay của menu/quest. Rủi ro UI giữ trạng thái được ghi riêng, không gộp vào kết luận Fog.
- XML đầy đủ: `EditMode-final.xml`, `PlayMode-final.xml`; kết quả tool và Console riêng trong `verification-results.json`.
- `git diff --check` pass. Ngoài ba nguồn vision, hai script bị loại và tests/tài liệu/QA artifact nêu trên, không thay đổi production khác.
- Unity đã thoát Play, trở lại MainMenu. Các lưu ý Console/menu và giới hạn QA phía trên vẫn còn; test pass không phải người dùng đã nghiệm thu.
