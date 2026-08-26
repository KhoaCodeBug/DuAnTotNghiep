# Ox Alpha — nhiệm vụ lớn: loot sửa xe cảnh sát (Route B)

## 0. Cách làm việc bắt buộc

Đây là task Unity/Fusion nhiều người chơi. Đọc toàn bộ file này, sau đó **chỉ hỏi ngay ở đầu** nếu một yêu cầu dưới đây không thể xác minh bằng code/Scene. Không tự hỏi giữa chừng và không tự đặt thêm gameplay. Nếu không bị chặn, hãy thực hiện toàn bộ nhiệm vụ, tự compile/test, rồi báo cáo rõ phần nào chưa thể xác nhận bằng Play Mode.

Không push, không commit, không tạo/xóa branch, không reset/revert/stash. Không thêm các ảnh `opencode-screen*.png` vào Git.

## 1. Context đã chốt

- Project Unity 6 + Fusion Host Mode. Scene chính: `Assets/Scenes/Main.unity`.
- Đây là Route B / quân sự. Flow đã duyệt:
  1. Nhặt đúng 3 manh mối trong trường; rời `__SchoolRoofTrigger_FIXED`.
  2. Kiểm tra xe cảnh sát `Car`; vote đồng thuận; cinematic đóng `CongRao`.
  3. Siege bắt đầu. Sau cinematic, người chết respawn gần xe, giữ đồ. Solo chết là fail; Multi có pool 3 lượt hồi sinh chung.
  4. Trong siege, sửa `Car` qua 5 hạng mục và minigame; xong đủ thì xe mới mở khoá để extraction.
- Không có Generator, điện giật zombie, hay luật gameplay mới nào trong scope này.
- Không di chuyển, thay sprite, hay thay thế `Car`. `Car` là xe hoàn chỉnh có sẵn trong Scene; chỉ đang bị khoá lúc hỏng.
- Không thay đổi logic cinematic, gate/horde, respawn hoặc balance trong task này, trừ khi lỗi compile trực tiếp do task mới gây ra.

## 2. Cơ chế sửa xe đã có — phải tái sử dụng

Các file trọng tâm:

- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- `Assets/Script/Tin/MainQuest/RoadsideVehicleRepairStation.cs`
- `Assets/Script/Tin/MainQuest/ArrivalCarInspectionUI.cs`
- `Assets/Script/Tin/MainQuest/VehicleRepairSkillCheckUI.cs`
- `Assets/Script/Tin/Prototype/PoliceCarRepairRules.cs`
- `Assets/Script/Tin/MainQuest/PoliceCarItemCatalog.cs`
- `Assets/Hau/Script/VehicleController.cs`

Năm hạng mục và đúng năm item riêng cho xe cảnh sát:

| Hạng mục | `partId` | Item bắt buộc |
|---|---|---|
| Động cơ | `engine` | `Toolbox` / `Bộ dụng cụ xe tuần tra` |
| Nắp capo | `hood` | `Hammer` / `Búa cứu hộ cảnh sát` |
| Nhiên liệu | `fuel` | `FuelCan` / `Can nhiên liệu dự phòng cảnh sát` |
| Ắc quy | `battery` | `Battery` / `Ắc quy xe tuần tra` |
| Lốp trước trái | `front_left` | `Tire` / `Lốp xe tuần tra` |

Tuyệt đối dùng `PoliceCarItemCatalog` và `PoliceCarRepairRules`; không dùng/tiêu hao item Route A (`ArrivalCarItemCatalog`) và không đổi số hạng mục. Minigame hiện có: 45 giây cơ bản/hạng mục, skill-check 4–7 giây, vòng 1.25 giây, perfect/success/miss đã có rule/test. Giữ nguyên.

F9 hiện chỉ là tiện ích Editor cấp 5 đồ để test; sau task này game thật phải chơi được **không cần F9**. Có thể giữ F9 như debug-only, nhưng không dùng nó làm đường gameplay.

## 3. Mục tiêu cần làm

Tạo một lớp/flow loot sửa xe cảnh sát mới, nhưng hành vi mở loot phải giống container loot hiện có của project:

1. Các thùng loot thuộc Route B chỉ sẵn sàng trong `SiegeAndRepair` (không hiện/không mở trước cinematic); loot container phải tương tác, mở UI và chuyển item vào inventory theo **cơ chế loot hiện có**, không làm UI loot thứ hai.
2. Người chơi đến gần phải thấy highlight **đỏ rõ trên toàn bộ hình/container**, theo đúng hệ thống highlight/proximity của loot hiện có. Khi rời xa hoặc container đã hết thì xử lý giống loot chuẩn.
3. Tất cả 5 item sửa xe cảnh sát phải luôn có trong tổng loot của match, mỗi item đúng ít nhất một lần. Việc phân phối giữa các thùng được random trên State Authority. Không được để RNG tạo match không thể hoàn thành xe.
4. Mỗi thùng cũng random thêm súng và đạn từ các ID/tables đã tồn tại trong project. Chỉ dùng những `ItemData` đã load/được dùng thật; không bịa tên ID hay tạo vũ khí mới. Loot phụ có thể ngẫu nhiên nhưng không được thay thế 5 item bắt buộc.
5. Multiplayer: nội dung container, trạng thái đã loot, và việc nhận item phải authoritative/replicated. Hai người mở cùng thùng không được nhân đôi item. Late joiner thấy đúng trạng thái còn lại.
6. Container phải có tính tái sử dụng/authorable rõ ràng, không viết hard-code rải rác 5 món vào `MilitaryBaseQuestManager`.
7. Dùng các container/asset/visual có sẵn nếu tìm thấy. Ưu tiên đặt một nhóm container ở các vị trí an toàn, nhìn thấy được trong khu quân sự, không chặn lối/collider/cổng và không spawn chồng player/zombie/Car. Không tự tạo bitmap/sprite placeholder.
8. Nếu Scene đã có marker/container phù hợp, dùng chúng. Nếu không có marker phù hợp, tạo runtime placement nhỏ, có tên rõ ràng dưới presentation root, **không save/dirty Main.unity**. Nếu không thể suy ra vị trí an toàn bằng scene/code, dừng trước khi code và hỏi một câu ngắn ngay đầu task.
9. Nhặt item không được tự đánh dấu hạng mục sửa xong: người chơi vẫn phải quay về `Car`, chọn đúng hạng mục và hoàn thành minigame. Progress hạng mục phải giữ lại khi bị damage/chết/rời vị trí như code hiện có.
10. Không sửa rộng các legacy Route A repair/cabinet systems nếu không thật cần; tránh regress Route A.

## 4. Quy tắc kỹ thuật / kiểm tra trước khi code

- Trước hết tìm và đọc toàn bộ implementation của loot hiện tại (`LootContainer`, interaction/highlight/UI/inventory), các prefab/cabinet liên quan, và các test hiện có. Không đoán API.
- Xem `Main.unity` hierarchy để biết asset/marker/collider thực tế.
- Tìm nơi `MilitaryBaseQuestManager` tạo presentation và nơi phase chuyển sang `SiegeAndRepair` để activate loot đúng lúc.
- Nếu cần network state, dùng Fusion state authority + Networked/RPC theo pattern hiện tại. Không tin client về nội dung loot hay trạng thái đã nhặt.
- Không đưa logic ngẫu nhiên vào client; server/state authority chọn seed/distribution.
- Bảo toàn tất cả sửa đổi hiện có trong working tree; đây là branch checkpoint, không phải repo sạch.
- Chỉ dùng `apply_patch` để sửa file local.

## 5. Test bắt buộc

Sau khi code:

1. `dotnet build Assembly-CSharp.csproj --no-restore -v:q` phải 0 errors.
2. Unity Console không có error mới do task.
3. Thêm/chỉnh EditMode tests cho ít nhất:
   - mapping đủ 5 required police parts;
   - random distribution luôn chứa đủ 5 món;
   - không trùng/không cấp đôi required loot;
   - trạng thái depleted/claim không spawn lại đồ.
4. Chạy test repair rule hiện có và test mới. Nếu chạy được, kiểm tra bằng Unity test runner.
5. Không tuyên bố multiplayer/visual đã xác nhận nếu không chạy 2 client + Play Mode; ghi rõ chúng cần test tay.

## 6. Báo cáo cuối cho người giao task

Trả về ngắn gọn bằng tiếng Việt:

- Các file sửa/tạo và flow thực tế sau thay đổi.
- Kết quả build/tests thật.
- Hướng dẫn test Play Mode: vào Route B → cinematic → tìm/mở loot → lấy đồ → repair 5 mục → vào xe.
- Kết quả mong đợi, edge cases (2 player mở cùng thùng, inventory đầy, chết/respawn, late joiner).
- Mọi rủi ro/chưa xác nhận.
- Không push/commit.
