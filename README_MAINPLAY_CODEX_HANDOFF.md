# MainPlay — Gameplay và Tuyến B Handoff

Đây là file bàn giao chính dành cho người pull nhánh về tiếp tục phát triển. Tài liệu tổng kết gameplay sửa xe, flow Tuyến B, các quyết định thiết kế đã chốt, phần đang tạm hoãn và cách kiểm tra trạng thái hiện tại.

## Addendum finale quân sự — 2026-08-25

- Trường quân sự không tự phát event khi bước vào. Người chơi kiểm tra đúng `3` manh mối dạng quest-state, rồi rời `__SchoolRoofTrigger_FIXED` để mở waypoint trên `Car`.
- Giữ `E` tại `Car` mở vote nhất trí toàn phòng. Bất kỳ Player nào cũng có thể khởi tạo; một phiếu từ chối hủy vote và có thể tương tác xe lại. Player disconnect được loại khỏi snapshot, late join không chen vào vote đang chạy.
- Chỉ khi đủ phiếu mới khóa Ending B và chạy cinematic Host: dọn zombie trong Polygon `KhuVucQuanSu`, đặt visual Host ở điểm diễn xuất bên trong gần `Car`, ẩn toàn đội, khóa input, đi/chạy bằng Animator và tốc độ Player thật, thử xe thất bại, còi báo động, chạy tới `ViTriDongCong`, hiện hàng rào authored `CongRao` rồi gom toàn đội vào phía trong.
- `Car` hoàn thiện có sẵn trong scene được tái sử dụng tại đúng vị trí đã author; runtime không chuyển xe tới marker khác. Sau cinematic, chính xe này dùng minigame sửa năm hạng mục.
- Horde dùng bốn `ViTriSpawnZombie*`, kiểm tra mỗi `5 giây`: Solo sinh `8`/batch và dừng ở `24+`; từ hai Player sinh `16`/batch và dừng ở `50+`. Hard cap tương ứng `36/72`.
- Không có Generator, không tăng cổng lên `150% HP` và không có điện giật/làm choáng zombie. Đây là ý tưởng prototype cũ đã bị loại khỏi flow canonical.
- Cổng luôn dùng tối thiểu `5.000 HP`, collider runtime `Obstacle` cập nhật A* nhưng không chặn Fog/LOS, và thanh HP lớn phía trên hotbar. Zombie siege sinh cách cổng tối thiểu 18m, zombie cũ sát cổng được dọn, dùng chase speed thật và phân tán trên 13 lane. Damage theo animation beat `12 HP/hit`, tối đa `4 hit/giây` toàn cổng.
- QA mới nhất: `51/51` EditMode liên quan và `2/2` PlayMode trọng tâm finale đạt; các script finale được Unity validation xác nhận `0` lỗi. Full nhóm PlayMode rộng hơn đạt `3/4`; bài còn lỗi là assertion cũ về trả Player từ ranh giới khu dân cư, không nằm trong finale quân sự.

## Addendum bệnh viện H1–H5 — 2026-08-25

- Flow canonical hiện là `ShiftLog → ShiftLog2 → KeyLoot ngẫu nhiên/shared key → cửa Radio → khôi phục tín hiệu 14 giây/3 chặng → bản ghi bệnh viện → Mảnh bản đồ 2 → căn cứ phía Bắc`; không còn Tủ hồ sơ.
- Scene có 6 `KeyLoot` candidate. Host chọn một stable ID, replicate cho Client/late join; chỉ điểm được chọn tương tác được. Có thể duplicate bất kỳ KeyLoot hiện tại để mở rộng danh sách.
- `ShiftLog`, `ShiftLog2`, sáu KeyLoot, Door và Radio có tổng cộng 10 child `InteractionZone`, mỗi child chứa Polygon riêng để người thiết kế Edit Collider bằng tay. Prompt local và xác thực Host dùng chung Polygon.
- Tiến độ Radio do State Authority giữ, chỉ một operator; thả E/rời vùng giữ tiến độ và người khác tiếp tục được. Người ở xa không bị ép UI/audio.
- Chặng 1 và 2 tự dừng, phát nhiễu `2,7 giây`; mỗi chặng sinh tại mỗi anchor lần lượt Dễ `3`, Thường `4`, Hardcore `5`, cách nhau `0,25 giây`. Không có kill gate và chặng 3 không sinh thêm.
- Bốn xác zombie tĩnh chỉ làm breadcrumb môi trường, không collider, AI, networking, interaction hoặc loot.
- QA tự động chốt H5: toàn bộ EditMode `96/96`; hai PlayMode trọng tâm scene/regression `2/2`. Regression Easy ghi nhận selected KeyLoot authoritative, shared key chỉ có sau bước loot, spawn counter `6 → 12`, rồi chặng 3 mới hoàn tất. Test xe cảnh sát cũ sau đó đã được thay bằng assertion finale mới dùng `Car` tại chỗ; xem addendum quân sự để biết trạng thái chạy gần nhất.
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
- `Car` cảnh sát thật giữ nguyên vị trí đã đặt trong scene; runtime tuyệt đối không chuyển nó tới `ViTriXeTest` hay marker khác.
- Nếu scene không có vùng kiểm tra xe riêng, `RoadsideVehicleRepairStation` tạo polygon `[AUTO]` quanh chính `Car` ở runtime.
- Xe được khóa hướng index `0` trong thời gian hỏng; đủ năm hạng mục mới mở lại khả năng lái có sẵn.
- `ViTriDongCong`, `__SchoolRoofTrigger_FIXED` và bốn `ViTriSpawnZombie*` là các marker canonical của finale.

## Các thành phần chính

| File | Thay đổi |
| --- | --- |
| `Assets/Hau/Script/VehicleController.cs` | Khóa/mở xe hỏng tại chỗ và cung cấp hiệu ứng cửa, đề máy, còi cho cinematic. |
| `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs` | State authoritative cho manh mối, roof-exit, vote, cinematic, siege và sửa xe 5 hạng mục. |
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
| `Assets/Script/Tin/MainQuest/MilitaryRouteCinematicController.cs` | Trình diễn Host/Car, fade, letterbox, khóa input và gom đội sau khi đóng cổng. |
| `Assets/Script/Tin/MainQuest/SiegeHordeDirector.cs` | Wave co giãn Solo/Multiplayer từ bốn marker và giới hạn zombie gần cổng. |
| `Assets/Scenes/Main.unity` | Chứa `Car` và các marker canonical do chủ dự án author; không được tự động lưu đè khi QA. |

## Kiểm thử

- Có EditMode test cho luật tiến độ, giới hạn góc xuất hiện, Perfect, Success, Miss và clamp `0–100%`.
- Có PlayMode flow test từ Main Menu tới scene Main, kiểm tra xe cảnh sát bị khóa nhưng giữ nguyên vị trí author, ba manh mối, cổng, bốn spawn marker và flow Ending B.
- Kết quả finale mới nhất: `51/51` EditMode liên quan và `2/2` PlayMode trọng tâm đạt. Full nhóm rộng hơn còn một regression ranh giới khu dân cư như ghi ở addendum.

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
