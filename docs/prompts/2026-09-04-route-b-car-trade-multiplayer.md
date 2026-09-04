# Prompt Antigravity — Route B Car exit and multiplayer trade

Repository: `E:/Unity/GameObject/Game3D/ProJectZomboiNhai`

Bạn là bên trực tiếp triển khai task này trong Unity project. Hãy điều tra và sửa hai lỗi sau, theo thứ tự đo đạc → test đỏ → sửa tối thiểu → test xanh:

1. Trong tuyến B, khi player vào chiếc xe `Car`, player bị hiển thị đứng trên/ở ngoài xe và bị kẹt, không di chuyển/thoát xe được.
2. Trong multiplayer, các player đứng gần nhau không thể bấm `T` để gửi/nhận Trade; tính năng Trade hiện không hoạt động.

## Phạm vi cần kiểm tra

- Vehicle code: `Assets/Hau/Script/VehicleController.cs`, `Assets/Hau/Script/PlayerInteraction.cs`.
- Route B: `Assets/Scenes/Main.unity`, prefab `Assets/Hau/NewPrefab/Car/Car.prefab`, và instance `Car` gần `SpawnXeCanhSat`.
- Player: `Assets/Prefab/Player.prefab`, `Assets/Prefab/Player2.prefab`.
- Trade: `Assets/Script/Tin/PlayerTrade.cs`, `Assets/Script/Tin/AutoUIManager.cs`, cùng test harness multiplayer hiện có.
- Chỉ mở rộng test ở thư mục test hiện có dưới `Assets/Script/Tin/Prototype/Tests` hoặc vị trí test phù hợp nhất đã tồn tại.

## Bắt buộc chẩn đoán trước khi sửa

Đừng đoán nguyên nhân và đừng giải quyết bằng offset cố định. Hãy tái hiện bằng authored Route B `Car` và báo rõ object/path nếu object thực tế khác với giả định.

### Vehicle

Ghi nhận tại thời điểm enter, sau ít nhất một network tick, và lúc nhấn phím exit:

- State authority/input authority của vehicle và player.
- `CurrentVehicle`, `NetworkIsInVehicle`, seat number/driver state.
- Player root `Transform`, `Rigidbody2D.position`, `simulated`, velocity.
- Seat anchor world position và exit anchor world position.
- Thành phần nào đang ghi player transform: state-authority seat sync, local presentation, `NetworkRigidbody2D`/interpolation, hay input/physics.
- Vì sao exit request có hoặc không đến được authority, và vì sao movement/physics có hoặc không được khôi phục.

Dùng diagnostic có giới hạn theo enter/tick/exit; không thêm log mỗi frame. Xác định root cause trước khi chọn fix.

### Trade

Tái hiện với host + client, và thêm client thứ hai nếu harness hỗ trợ. Trace có giới hạn:

- `T` có được nhận ở đúng object có `HasInputAuthority` không.
- Số `PlayerTrade` objects, vị trí hai player, distance, radius hiệu dụng.
- Target `PlayerRef`/`InputAuthority` được chọn có hợp lệ không.
- RPC source/target, `Runner.LocalPlayer`, và mọi lý do `CanStartTrade` từ chối.
- Popup có được gửi và bật ở target không; Accept có mở cửa sổ ở cả hai peer không.
- UI/story/health/death blockers có vô tình chặn input hay không.

Giữ nguyên state authority cho toàn bộ trade validation và giao dịch inventory.

## TDD bắt buộc

Trước khi sửa production code:

1. Viết regression test nhỏ nhất cho vehicle: seat assignment/presentation hội tụ về seat anchor; exit clear network vehicle state, restore physics/movement, và đặt player ở exit position hợp lệ.
2. Viết regression test nhỏ nhất cho trade: chọn target gần hợp lệ, giữ đúng `InputAuthority`, không chọn self/stale object; thêm integration test request/popup nếu harness có thể chạy.
3. Chạy focused tests và lưu output đang fail.
4. Sửa tối thiểu để test xanh, rồi chạy lại toàn bộ kiểm chứng liên quan.

## Tiêu chí nghiệm thu

- Route B authored Car không còn để player đứng trên xe/ngoài seat sau enter; local và remote view nhất quán.
- Khi seated, input không thể kéo player rời seat.
- Phím exit hiện có đặt player tại exit anchor an toàn, clear đúng network state, bật lại physics/movement, và remote peer thấy cùng trạng thái.
- Hai peer đứng trong trade radius có thể bấm `T`, target thấy request, Accept mở trade window ở cả hai; Decline/Cancel hoạt động đúng.
- Offer/ready/confirm/exchange vẫn state-authority controlled, validate inventory và atomic; không tăng radius hoặc bỏ validation chỉ để che lỗi.
- EditMode và PlayMode/multiplayer tests có output mới; Unity compile xong; Console không có lỗi mới; có screenshot vehicle enter/exit và trade request/window nếu harness cho phép.

## Ràng buộc thay đổi

- Không sửa ngoài phạm vi khi chưa có bằng chứng; không đổi API công khai/serialized references nếu không cần.
- Không sửa package Photon/Fusion, không xóa dữ liệu/file/test artifact, không sửa scene/prefab không liên quan.
- Không dùng client authority cho state vehicle/trade.
- Không commit/push/merge/PR.
- Trả về: root cause đã xác nhận, danh sách file đã đổi, test commands + output, trạng thái compile/Console, screenshot paths, và limitation còn lại.
