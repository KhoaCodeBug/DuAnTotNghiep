# MainPlay — Gameplay và Tuyến B Handoff

Đây là file bàn giao chính dành cho người pull nhánh về tiếp tục phát triển. Tài liệu tổng kết gameplay sửa xe, flow Tuyến B, các quyết định thiết kế đã chốt, phần đang tạm hoãn và cách kiểm tra trạng thái hiện tại.

## Addendum bệnh viện H1–H5 — 2026-08-25

- Flow canonical hiện là `ShiftLog → ShiftLog2 → KeyLoot ngẫu nhiên/shared key → cửa Radio → khôi phục tín hiệu 14 giây/3 chặng → bản ghi bệnh viện → Mảnh bản đồ 2 → căn cứ phía Bắc`; không còn Tủ hồ sơ.
- Scene có 6 `KeyLoot` candidate. Host chọn một stable ID, replicate cho Client/late join; chỉ điểm được chọn tương tác được. Có thể duplicate bất kỳ KeyLoot hiện tại để mở rộng danh sách.
- `ShiftLog`, `ShiftLog2`, sáu KeyLoot, Door và Radio có tổng cộng 10 child `InteractionZone`, mỗi child chứa Polygon riêng để người thiết kế Edit Collider bằng tay. Prompt local và xác thực Host dùng chung Polygon.
- Tiến độ Radio do State Authority giữ, chỉ một operator; thả E/rời vùng giữ tiến độ và người khác tiếp tục được. Người ở xa không bị ép UI/audio.
- Chặng 1 và 2 tự dừng, phát nhiễu `2,7 giây`; mỗi chặng sinh tại mỗi anchor lần lượt Dễ `3`, Thường `4`, Hardcore `5`, cách nhau `0,25 giây`. Không có kill gate và chặng 3 không sinh thêm.
- Bốn xác zombie tĩnh chỉ làm breadcrumb môi trường, không collider, AI, networking, interaction hoặc loot.
- QA tự động chốt H5: toàn bộ EditMode `96/96`; hai PlayMode trọng tâm scene/regression `2/2`. Regression Easy ghi nhận selected KeyLoot authoritative, shared key chỉ có sau bước loot, spawn counter `6 → 12`, rồi chặng 3 mới hoàn tất. Full PlayMode toàn project đạt `4/5`; test xe cảnh sát cũ còn fail vì scene hiện không lưu fixture `ViTriXeTest`/`VungKiemTraXeCanhSat`, ngoài phạm vi H5.
- Transcript đầy đủ được lưu trong Nhật ký; click thẻ phần thưởng ở phase chưa tới căn cứ để đọc lại. Bản ghi không khẳng định căn cứ còn người sống.
- Cue05–08 dùng nội dung canonical mới trong khi chờ thu bốn voice thay thế (Cue05 subtitle; Cue06–08 subtitle + static). Cue09 đã có bản cắt sạch `09_MilitaryRouteRevealed_Clean.mp3` và phát sau cinematic, trước bảng chọn theo dõi lần hai.
- QA tự động H3: compile sạch; `49/49` EditMode liên quan, PlayMode scene `1/1`, regression MainMenu → Ending B `1/1`.
- Kế hoạch tiếp theo nằm trong `NEXT_SESSION_MILITARY_FINALE_PLAN.md`: thảo luận/chốt thiết kế finale căn cứ quân sự trước khi code.

## Đọc nhanh sau khi pull — cập nhật 2026-08-24

1. Mở project bằng Unity `6000.0.69f1`.
2. Khi kiểm thử flow thật, luôn bắt đầu từ scene `MainMenu`, chọn chơi đơn rồi để hệ thống chuyển sang `Main`; không chạy thẳng `Main` vì player và trạng thái network có thể không được khởi tạo đúng.
3. Tuyến B hiện đã có flow test từ Main Menu tới Ending B mà không cần LootContainer. Các phím/CheatMenu phát triển chỉ mô phỏng state nhiệm vụ và không tạo hoặc sửa loot thật.
4. Không triển khai phần nhiệm vụ phụ thuộc LootContainer tại bệnh viện/căn cứ cho tới khi chủ dự án xác nhận container đã setup xong.
5. Hai lần chọn Tuyến A/B chỉ đổi tuyến đang theo dõi. Ending B chỉ bị khóa authoritative tại bước xác nhận kích hoạt báo động ở căn cứ.
6. Prompt `Giữ E` trong Khu Điều phối đã chốt phương án C: nằm dưới vùng objective phía trên, hiển thị như thẻ tương tác riêng và không dùng bố cục mission toast.

### Thay đổi UI mới nhất

- Thẻ tương tác có ô `[E]` màu cam, nhãn loại `TƯƠNG TÁC • GIỮ PHÍM`, hành động ở dòng thứ hai, nền xanh-đen và sọc cam bên trái.
- Nội dung tự đổi Việt/Anh cho từng client: bàn điều phối, radio, tủ hồ sơ và trạng thái đang kiểm tra.
- Prompt tự ẩn khi đang có hội thoại, bảng chọn tuyến, Nhật ký, bản đồ hoặc modal UI khác; khi đóng UI, prompt chỉ hiện lại nếu local player vẫn đứng đúng vùng tương tác.
- Ảnh kiểm tra thật ở độ phân giải Game View 1920×1080: `Captures/hold-e-option-c-improved-interaction-card.png`.
- Code chính: `Assets/Script/Tin/MainQuest/MainQuestSearchCabinet.cs`, `Assets/Script/Tin/MainQuest/MainQuestManager.cs` và `Assets/Script/Tin/GameLocalization.cs`.

### Trạng thái kiểm thử hiện tại

- EditMode toàn project: `82/82` test đạt.
- PlayMode: `MainMenuToMilitaryQuestFlowTests.RouteBDebugFlowRunsFromMainMenuThroughMilitaryExtractionWithoutLootContainers` đạt `1/1`.
- Unity Console sau kiểm thử: `0` error.
- Cảnh báo glyph `✦` của `LiberationSans SDF` và cảnh báo Photon Voice `TransmitEnabled=false` vẫn có thể xuất hiện trong log test; đây không phải compile error và không làm test thất bại.

## Gameplay sửa xe cảnh sát

- Xe cảnh sát cũ trong scene `Main` được dùng làm trạm thử gameplay và bị khóa thao tác lên xe/lái xe.
- Người chơi phải đứng trong polygon trước mũi xe, giữ `E` để mở giao diện kiểm tra cơ khí dùng chung với xe đầu game.
- Giao diện xe cảnh sát có tiêu đề, tên xe, trạng thái và tiến độ riêng; không thay đổi dữ liệu sửa chữa của xe đầu game.
- Có 5 hạng mục bắt buộc: động cơ, nắp capo, nhiên liệu, ắc quy và lốp xe.
- Mỗi hạng mục có tiến độ độc lập và thời gian cơ bản 45 giây.
- Khi chọn hạng mục và bấm sửa, giao diện kiểm tra tạm đóng để mở minigame skill-check và thanh tiến độ.
- `Esc` hủy phiên sửa hiện tại nhưng giữ lại tiến độ đã đạt của hạng mục.
- Nhận sát thương, chết, biến đổi, rời vị trí sửa hoặc mất kết nối sẽ ngắt phiên sửa và giữ tiến độ.

## Skill-check

- Event skill-check xuất hiện ngẫu nhiên sau mỗi 4–7 giây.
- Kim hoàn thành một vòng trong 1,25 giây.
- Vùng Success rộng 25°, vùng Perfect rộng 8°.
- Vùng Success chỉ bắt đầu sau khi kim đi qua tối thiểu 30% vòng tròn, tránh xuất hiện ngay tại điểm khởi đầu.
- Kết quả Perfect cộng 7% tiến độ; Success cộng 3,5%.
- Trượt hoặc không bấm trừ 2% và dừng tăng tiến độ trong 1 giây.
- Trong giai đoạn test ven đường, trượt skill-check không phát tiếng động, không gọi `MakeNoise()` và không thu hút zombie.

## Multiplayer và khóa thao tác

- State Authority quản lý người đang sửa, hạng mục, tiến độ, event skill-check và kết quả.
- Chỉ một người được sửa xe tại một thời điểm.
- Người chơi khác thử sửa sẽ nhận thông báo `XE ĐANG ĐƯỢC SỬA BỞI: <TÊN>`.
- Client chỉ gửi yêu cầu bắt đầu, hủy và kết quả bấm Space; không tự cộng tiến độ hoặc tự hoàn thành hạng mục.
- Trong minigame, movement, tương tác, aim, attack và bash bị khóa; phím Space chỉ phục vụ skill-check.

## Vật phẩm thử nghiệm

- Xe cảnh sát có 5 item sửa chữa với ID và tên riêng, không trùng item xe đầu game.
- Các item sử dụng lại icon gốc nhưng có nền xanh biển để phân biệt.
- Item xe cảnh sát không được thêm vào loot tủ thông thường.
- Trong Unity Editor, Solo/Host có thể nhấn `F9` để nhận các item xe cảnh sát còn thiếu; `F8` vẫn dành cho bộ item xe đầu game.
- Item chỉ bị tiêu hao khi hạng mục đạt 100% và được State Authority xác nhận.

## Scene authoring và polygon

- Xe đầu game `Broken Arrival Car (from Intro)` hiện sẵn trong EditMode cùng polygon `VungKiemTraXeDauGame`.
- `Car` cảnh sát thật giữ vị trí gốc trong scene và chỉ được chuyển tới `ViTriXeTest` khi chạy game.
- `Police Car Preview [EDIT MODE]` hiển thị đúng sprite xe tại `ViTriXeTest` để chỉnh `VungKiemTraXeCanhSat` trực tiếp trong Scene View.
- Khi vào PlayMode, chỉ SpriteRenderer của preview bị ẩn; polygon vẫn hoạt động và được trạm sửa xe sử dụng.
- Xe cảnh sát runtime được khóa tại direction index `0`, trùng sprite `download_0` của preview, nên thân xe và polygon giữ đúng cùng một hướng.
- Nếu polygon scene tồn tại, runtime ưu tiên dùng polygon đã căn tay và không sinh vùng `[AUTO]` thay thế.

## Các thành phần chính

| File | Thay đổi |
| --- | --- |
| `Assets/Hau/Script/VehicleController.cs` | Khóa xe test, đưa xe tới marker và cố định hướng runtime khớp preview. |
| `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs` | State authoritative cho phiên sửa, tiến độ 5 hạng mục, skill-check, ngắt phiên và phím test `F9`. |
| `Assets/Script/Tin/MainQuest/ArrivalCarInspectionUI.cs` | Tái sử dụng giao diện cơ khí cho chế độ xe cảnh sát. |
| `Assets/Script/Tin/MainQuest/RoadsideVehicleRepairStation.cs` | Tương tác giữ `E`, polygon trước mũi xe và thông báo trạng thái sửa. |
| `Assets/Script/Tin/MainQuest/VehicleRepairSkillCheckUI.cs` | Giao diện vòng skill-check, thanh tiến độ và điều phối input local. |
| `Assets/Script/Tin/Prototype/VehicleRepairSkillCheckRules.cs` | Luật tính thời gian, góc bấm, bonus và penalty. |
| `Assets/Script/Tin/Prototype/PoliceCarRepairRules.cs` | Ánh xạ 5 hạng mục, state bit và item yêu cầu. |
| `Assets/Script/Tin/MainQuest/PoliceCarItemCatalog.cs` | Catalog item riêng cho xe cảnh sát. |
| `Assets/Script/Tin/MainQuest/EditModeVehiclePreview.cs` | Hiện sprite khi EditMode và ẩn sprite khi PlayMode. |
| `Assets/Script/Tin/MainQuest/VehicleInspectionZoneAuthoring.cs` | Hỗ trợ nhìn và chỉnh polygon trực tiếp trong Scene View. |
| `Assets/Script/Tin/Multiplayer/PlayerInputHandler2D.cs` | Chặn network input khi minigame đang mở. |
| `Assets/Hau/Script/PlayerInteraction.cs` | Chặn tương tác gameplay trong minigame. |
| `Assets/Scenes/Main.unity` | Bổ sung hai polygon authoring và preview xe cảnh sát tại marker test. |

## Kiểm thử

- Có EditMode test cho luật tiến độ, giới hạn góc xuất hiện, Perfect, Success, Miss và clamp `0–100%`.
- Có PlayMode flow test từ Main Menu tới scene Main, kiểm tra xe cảnh sát bị khóa, được đặt đúng marker, dùng polygon scene, hướng runtime khớp preview, quyền sửa authoritative và giữ tiến độ khi hủy.
- Kết quả chốt phiên: test luật EditMode đạt `5/5`; flow PlayMode xe cảnh sát đạt `1/1`; Unity Console không có compile error.

## Cập nhật Tuyến B — audio, lựa chọn tuyến và input

- Toàn bộ 15 file MP3 tuyến B được sắp tại `Assets/Resources/Sound/Story/RouteB/` và nối với catalog `RouteBAudioContent`; Unity xác nhận load đủ `15/15` clip.
- Cue 01–02 phát sau lần kiểm tra xe đầu tiên; cue 03–05 theo ba tài liệu; cue 06–09 theo chuỗi Văn phòng; cue 10–15 theo tiến trình căn cứ và Ending B.
- Sau cue 09, bảng hai tuyến xuất hiện lần thứ hai để người chơi xác nhận hướng ưu tiên trước khi tiến gần căn cứ. Đây là lựa chọn theo dõi; khóa ending authoritative vẫn chỉ xảy ra ở hành động cuối của từng tuyến.
- Dòng `CHƯA KHÓA ENDING` đã được xóa khỏi bảng chọn. Phần xác nhận cuối vẫn cảnh báo rõ hậu quả khóa tuyến cho toàn đội.
- Hotbar không còn dùng `Q/E`; người chơi đổi ô bằng hàng số `1–5`. Map không còn dùng `Q/E` để xoay và không còn hiển thị hướng dẫn này.
- Header, nội dung tuyến trong Nhật ký, bảng chọn, radio subtitle và thông báo cốt truyện lấy ngôn ngữ local của từng client và cập nhật khi đổi Việt/Anh.
- Voice Tuyến B duck game audio của client kích hoạt xuống 18%, ẩn tạm các Canvas khác và đặt bảng thoại ngay phía trên hotbar.
- Khóa hội thoại là local-only: client kích hoạt gửi network input rỗng và không mở được UI/voice chat; đồng đội không nhận bảng thoại nên vẫn di chuyển, chiến đấu và dùng UI bình thường.
- Header hội thoại chỉ hiện tên người nói. Prompt/viền tương tác xe chỉ hiện khi local player còn trong collider và không có modal UI; nó ẩn khi mở UI, hiện lại nếu đóng UI mà vẫn ở trong vùng, và tắt khi bước ra.

## Phạm vi tạm hoãn

- Hạng mục plan số 4 cũ đã được loại khỏi danh sách triển khai tiếp theo.
- Chưa bổ sung spawn loot dùng chung cho Tuyến A/B. Phần này được giữ lại cho tới khi khu Văn phòng hoàn thiện và có xác nhận mới từ chủ dự án.

## Cập nhật Tuyến B — Mảnh bản đồ quân sự

- Phần thưởng sau Cue 09 là `Mảnh bản đồ 2 — Tuyến quân sự`, dùng cùng art giấy rách với Mảnh 1; không còn lấy nguyên raster map làm ảnh phần thưởng.
- Sau thẻ thưởng, map tự mở, camera map tập trung vào căn cứ và vùng quân sự được mở sáng bằng hiệu ứng scan. Các khu chưa khám phá vẫn đen hoàn toàn; không mở full map.
- Khi Mảnh 2 đã được nhận, marker bệnh viện/Khu Điều phối được gỡ khỏi map và chỉ còn marker căn cứ quân sự.
- Sau khi map reveal kết thúc, map đóng rồi mới chạy cinematic ngoài thế giới và bảng chọn tuyến lần hai.
- Prompt tương tác bệnh viện đã dùng policy UI chung nên không còn hiện đè sau hội thoại, bảng chọn tuyến, Journal hoặc map.
- Chủ dự án đã chọn phương án C. Prompt production nằm cố định dưới vùng objective phía trên và được đổi thành thẻ tương tác riêng: ô phím `[E]` màu cam, nhãn nhỏ `TƯƠNG TÁC • GIỮ PHÍM`, nội dung hành động ở dòng hai, nền xanh-đen và sọc cam bên trái. Thiết kế này tránh nhầm với mission toast. Ảnh Game View thực tế: `Captures/hold-e-option-c-improved-interaction-card.png`.
