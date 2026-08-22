# Route B — Audio Source Script

Nguồn nội dung canonical nằm trong `Assets/Script/Tin/Prototype/RouteBAudioContent.cs`. Có 15 cue từ lúc kiểm tra xe đến bảng tổng kết Tuyến B.

## Quy ước thu âm

- Tên file phải trùng phần cuối `AudioResourcePath`, ví dụ `01_OpeningEmergencyBroadcast.mp3`.
- 15 clip hiện được đặt tại `Assets/Resources/Sound/Story/RouteB/` và runtime load bằng đường dẫn `Sound/Story/RouteB/<tên file>`.
- Radio/hệ thống: giọng trung tính, ngắt câu rõ, không diễn quá kịch; hậu kỳ mono, band-pass kiểu máy vô tuyến.
- Người sống sót: giọng gần, khô, bình tĩnh nhưng cảnh giác; không áp hiệu ứng radio.
- Subtitle trong catalog là nguồn thật. Nếu sửa lời thoại phải sửa catalog trước rồi mới thu lại clip.
- Không ghép nhạc vào voice clip; runtime tự trộn cùng gameplay music và radio static fallback.

## Trình bày và multiplayer

- Khi voice phát, game audio trên đúng client đó giảm còn 18%; voice giữ mức riêng và vẫn tôn trọng Master/SFX volume.
- Chỉ client của người kích hoạt cue bị ẩn HUD, khóa shortcut và gửi network input rỗng. Không dùng `Time.timeScale`, nên đồng đội vẫn di chuyển và chiến đấu bình thường.
- Bảng thoại nằm ở đáy giữa màn hình, ngay phía trên vùng hotbar. Sau cue, âm lượng, Canvas và input cục bộ được khôi phục đúng trạng thái trước đó.

## Thứ tự cue

1. `01_OpeningEmergencyBroadcast` — giới thiệu hồ sơ tiếp tế và Văn phòng Điều phối.
2. `02_PlayerRouteReaction` — xác nhận người chơi có thể chuẩn bị song song hai hướng thoát.
3. `03_FirstSupplyDocument` — tài liệu tiếp tế đầu tiên.
4. `04_SecondEvacuationDocument` — lịch trình sơ tán.
5. `05_ThirdCoordinationDocument` — địa chỉ cổng tím và tần số dự phòng.
6. `06_OfficeLocated` — tới Văn phòng Điều phối.
7. `07_DispatchDeskLog` — bàn trực chỉ tới radio và chìa khóa.
8. `08_OfficeRadioRecording` — cảnh báo báo động sẽ thu hút nguy hiểm.
9. `09_MilitaryRouteRevealed` — mở đường tới căn cứ, nhắc điểm không thể quay lại.
10. `10_MilitaryBaseApproach` — tình trạng căn cứ và xe sơ tán.
11. `11_AlarmPointOfNoReturn` — cảnh báo khóa ending cho toàn đội.
12. `12_SiegeStarted` — bắt đầu phòng thủ.
13. `13_GeneratorOnline` — mở kho và gia cố cổng.
14. `14_EscapeVehicleReady` — tập hợp đội để rời căn cứ.
15. `15_MilitaryEvacuationComplete` — kết thúc Tuyến B.

Unity đã nhận đủ `15/15` clip. Nếu một clip bị xóa hoặc đổi tên, runtime vẫn hiển thị subtitle và dùng radio static được tạo trong code; flow không bị chặn.
