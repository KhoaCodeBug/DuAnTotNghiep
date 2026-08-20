# Route B — Audio Source Script

Nguồn nội dung canonical nằm trong `Assets/Script/Tin/Prototype/RouteBAudioContent.cs`. Có 15 cue từ lúc kiểm tra xe đến bảng tổng kết Tuyến B.

## Quy ước thu âm

- Tên file phải trùng phần cuối `AudioResourcePath`, ví dụ `01_OpeningEmergencyBroadcast.wav`.
- Đặt file tại `Assets/Resources/Story/RouteB/`.
- Radio/hệ thống: giọng trung tính, ngắt câu rõ, không diễn quá kịch; hậu kỳ mono, band-pass kiểu máy vô tuyến.
- Người sống sót: giọng gần, khô, bình tĩnh nhưng cảnh giác; không áp hiệu ứng radio.
- Subtitle trong catalog là nguồn thật. Nếu sửa lời thoại phải sửa catalog trước rồi mới thu lại clip.
- Không ghép nhạc vào voice clip; runtime tự trộn cùng gameplay music và radio static fallback.

## Thứ tự cue

1. `01_OpeningEmergencyBroadcast` — giới thiệu hồ sơ tiếp tế và Văn phòng Điều phối.
2. `02_PlayerRouteReaction` — xác nhận có hai hướng thoát, chưa khóa ending.
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

Nếu clip chưa tồn tại, runtime vẫn hiển thị subtitle và dùng radio static được tạo trong code; flow không bị chặn.
