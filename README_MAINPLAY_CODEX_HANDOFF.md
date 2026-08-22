# MainPlay — Vehicle Repair Gameplay Update

Tài liệu này chỉ tổng kết các chức năng, cải tiến và thay đổi được triển khai trong phiên cập nhật gameplay sửa xe cảnh sát.

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
