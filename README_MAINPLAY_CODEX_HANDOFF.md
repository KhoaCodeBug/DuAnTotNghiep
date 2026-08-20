# Main Play & Vehicle Quest — Codex Handoff

Tài liệu này là nguồn bàn giao nhanh cho thành viên mở project bằng Codex trên máy khác. Hãy đọc file này trước khi sửa flow nhiệm vụ, sau đó kiểm tra `git status`, diff hiện tại và các test liên quan vì trạng thái code có thể mới hơn tài liệu.

## Nguồn hội thoại

- Task thiết kế và xây lại Main Play: `codex://threads/01a0151d-4eac-7bf2-9acb-d9048e867c20`
- Task hoàn thiện xe đầu game và UI kiểm tra xe: `codex://threads/01a018f6-ef2a-72e1-b67c-1929ae8e6f8b`

Hai task trên thống nhất một flow liên tục từ Intro sang Main; không được xem chiếc xe ở Main là một phương tiện hoặc đoạn mở đầu tách biệt.

## Quyết định thiết kế đã chốt

### Mạch truyện mở đầu

1. `Intro_Cinematic` đã có cảnh lái xe và radio chính phủ. Không phát thêm một đoạn radio mở đầu trùng lặp khi vào `Main`.
2. Xe chết máy trong Intro được tiếp nối tại `Main`, đặt theo GameObject `ViTriXeChetMay`.
3. Lần đầu vào Main, người chơi xuất hiện gần chiếc xe. Sau khi chết, hệ thống vẫn dùng các điểm hồi sinh nhà cũ; không hồi sinh cạnh xe.
4. Xe đầu game chỉ là phương tiện hướng dẫn/ngắn hạn. Xe thoát hiểm tại căn cứ quân sự dùng flow sửa chữa nâng cao riêng.

### Tương tác xe đầu game

- Chỉ có thể bắt đầu kiểm tra khi đứng trong vùng polygon trước mũi xe và giữ `E` đủ thời gian.
- Khi người chơi hợp lệ đi vào vùng, viền polygon màu xanh và dòng `KIỂM TRA TÌNH TRẠNG XE — GIỮ [E]` mới hiện; rời vùng thì cả hai ẩn.
- UI kiểm tra khóa di chuyển và tấn công thông qua trạng thái modal của `AutoUIManager`.
- Đóng bằng `E` lần nữa, `Esc`, hoặc nút `X`. Giữ `E` để mở không được làm UI đóng ngay; code chờ người chơi nhả phím trước.
- UI có thể mở lại sau lần kiểm tra đầu.
- Chỉ sau khi đóng UI lần đầu, server mới ghi nhận đã kiểm tra xe và mở thông báo nhiệm vụ phụ tùy chọn sửa xe.
- Hư hỏng cốt truyện: động cơ quá nhiệt/bộ đề kẹt, nhiên liệu gần cạn. Bộ dụng cụ, búa và can nhiên liệu là vật phẩm bắt buộc; ắc quy và lốp là nâng cấp tùy chọn.

### UI cơ khí phương tiện

- Giao diện theo phong cách Project Zomboid: sơ đồ xe ở trái, bảng tình trạng và chi tiết bộ phận ở phải.
- Có thể bấm trực tiếp hotspot trên sơ đồ hoặc từng hàng để chọn 11 bộ phận: động cơ, ắc quy, ống xả, bình xăng, bốn lốp, nắp capo, kính chắn gió và cửa trước.
- Nắp capo hư được tô bằng polygon đỏ trong suốt theo đúng hình nắp xe, không dùng khối chữ nhật đỏ kèm chữ trắng.
- Bộ phận đang chọn có viền/pulse nhẹ, có hàng được chọn và bảng chẩn đoán riêng.
- Nút hành động ngữ cảnh ở góc dưới phải của bảng chi tiết được giữ đúng bản thiết kế cuối:

  - `Sửa chữa`: động cơ, nắp capo.
  - `Thay linh kiện`: ắc quy và các lốp được đánh dấu cần thay.
  - `Đổ nhiên liệu`: bình xăng.
  - `Kiểm tra`: ống xả, các bộ phận chưa cần thay và thân xe.

- Bấm `Kiểm tra` hiển thị kết quả chẩn đoán. Các nút sửa/thay/đổ hiện hướng người chơi sang nhật ký `J` để xem vật phẩm.
- Danh sách vật phẩm không còn nằm trong modal kiểm tra xe. Nhật ký `J` là nguồn hiển thị người chơi đang có hoặc còn thiếu gì.
- Hiện tại nút hành động của xe đầu game mới hoàn thiện phần UI/phản hồi. Giao dịch authoritative để tiêu hao vật phẩm, sửa xe thật và cho phép lái rời khu vực vẫn là hạng mục kế tiếp; không được giả định nút đã thực hiện việc đó.

### Manh mối và bản đồ

- Manh mối tuyến đường là vật phẩm thật trong loot, không tăng tiến độ ngay lúc mở container. Tiến độ chỉ tăng khi vật phẩm thực sự vào inventory.
- Có ba tài liệu tuyến đường riêng biệt. State Authority quản lý mask đã nhặt, mask đã “bảo hiểm” trong container và số lần mở hụt.
- Tỉ lệ cơ bản hiện là 70%; sau một lần hụt, lần hợp lệ kế tiếp được bảo đảm. Nếu vật phẩm được bảo hiểm bị mất khỏi container, hệ thống có thể phục hồi để tránh soft-lock.
- Đủ `3/3` manh mối mới chuyển sang tìm văn phòng cổng tím.
- Chuỗi văn phòng: kiểm tra bàn/tài liệu, nghe radio, sau đó mở tủ/két đúng mục tiêu để lấy mảnh bản đồ tiếp theo. Không cho phép tương tác sai thứ tự làm nhảy quest.
- `J` mở nhật ký nhiệm vụ; `M` mở bản đồ. Vùng tìm kiếm khu dân cư và vùng văn phòng được reveal độc lập, không làm sáng toàn bản đồ.
- Tuning map hiện tại:

  - Khu dân cư: Center `(0.503, 0.339)`, Size `(0.288, 0.176)`.
  - Khu văn phòng: Center `(0.505, 0.485)`, Size `(0.284, 0.138)`.

## Flow Main Play hiện tại

```text
Main Menu / Solo
  -> Intro_Cinematic: lái xe + radio chính phủ
  -> Main: spawn lần đầu cạnh xe tại ViTriXeChetMay
  -> Đứng trước mũi xe, giữ E, kiểm tra và đóng UI
  -> Mở nhiệm vụ phụ tùy chọn sửa xe trong J
  -> Tìm kiếm các nhà được State Authority chọn
  -> Nhặt đủ 3 tài liệu tuyến đường thật trong inventory
  -> Suy luận và tìm văn phòng cổng tím
  -> Bàn/tài liệu -> radio -> tủ/két -> mảnh bản đồ
  -> Reveal tuyến tới căn cứ quân sự và tập hợp đội
  -> Tới căn cứ, kiểm tra xe thoát hiểm
  -> Kích hoạt báo động; bắt đầu SiegeAndRepair
  -> Bật máy phát, mở kho vũ khí/két văn phòng
  -> Thu thập và lắp ắc quy + nhiên liệu + bộ sửa chữa
  -> Giữ tương tác sửa xe theo tiến độ trong lúc phòng thủ cổng
  -> ReadyToEscape -> tập hợp người còn sống -> thoát
  -> Victory cutscene / bảng tổng kết
```

Các phase authoritative của căn cứ quân sự là `NotReached`, `Investigating`, `SiegeAndRepair`, `ReadyToEscape`, `Escaped`, `Failed`. Nếu toàn đội chết trong giai đoạn vây hãm/chuẩn bị thoát, nhiệm vụ chuyển sang `Failed`.

## Điều khiển liên quan

- `E` giữ: kiểm tra xe khi đứng đúng vùng; `E` lần nữa đóng modal.
- `Esc` hoặc `X`: đóng modal.
- `J`: nhật ký và checklist nhiệm vụ; tại đây mới kiểm tra vật phẩm đã có/đang thiếu.
- `M`: bản đồ nhiệm vụ.
- Trong nhật ký: `W/S` đổi nhiệm vụ, `Q/E` đổi tab, `V` bật/tắt theo dõi (khi UI cho phép).

## Các file quan trọng

| File | Trách nhiệm |
| --- | --- |
| `Assets/Script/Tin/MainQuest/MainQuestManager.cs` | State Authority của nửa đầu Main, car-inspection state, nhà tìm kiếm, loot clue, stage và snapshot mạng. |
| `Assets/Script/Tin/MainQuest/MainArrivalStoryBootstrap.cs` | Nối Intro với Main, tìm `ViTriXeChetMay`, tạo/đặt xe và cấu hình spawn lần đầu. |
| `Assets/Script/Tin/MainQuest/BrokenArrivalCar.cs` | Vùng polygon trước mũi xe, prompt, giữ `E`, mở lại UI và báo lần đóng đầu. |
| `Assets/Script/Tin/MainQuest/ArrivalCarInspectionUI.cs` | Modal cơ khí, chọn bộ phận, nắp capo đỏ và nút hành động ngữ cảnh. |
| `Assets/Script/Tin/MainQuest/UIPolygonGraphic.cs` | Graphic polygon UI tái sử dụng cho vùng/hình không chữ nhật. |
| `Assets/Script/Tin/MainQuest/VehiclePartPulseHighlight.cs` | Viền/pulse thẩm mỹ cho bộ phận đang chọn. |
| `Assets/Script/Tin/MainQuest/PreMilitaryQuestRuntimeBridge.cs` | Nối state mạng với UI, inventory, map reveal, boundary và cinematic trước căn cứ. |
| `Assets/Script/Tin/MainQuest/QuestRouteClueItemCatalog.cs` | Định nghĩa ba item manh mối, tên, nội dung đọc và icon runtime. |
| `Assets/Script/Tin/MainQuest/QuestMapRevealTuningTool.cs` | Thông số vùng reveal trước/sau quest và công cụ chỉnh trong Play Mode. |
| `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs` | Nhật ký `J`, checklist sửa xe và các overlay quest. |
| `Assets/Script/Tin/Prototype/QuestMapUIPrototype.cs` | Bản đồ `M` và các lớp reveal. |
| `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs` | State Authority của flow căn cứ, vật phẩm, lắp đặt, sửa xe, siege và thoát. |
| `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs` | Spawn player và gọi bootstrap story Main. |
| `Assets/Resources/Story/BrokenArrivalCar.prefab` | Prefab xe đầu game và polygon tương tác đã cấu hình. |

Asset ảnh UI xe nằm trong `Assets/Art/Generated/Car/`. Ảnh nguồn nhỏ có thể hơi mềm khi phóng lớn; code dùng cách hiển thị giữ tỉ lệ và overlay vector để hạn chế lộ vỡ ảnh. Nếu thay asset độ phân giải cao, giữ cùng silhouette hoặc chỉnh lại hotspot/polygon.

## Nguyên tắc multiplayer và tránh regression

- `MainQuestManager` và `MilitaryBaseQuestManager` là nguồn sự thật. Client chỉ gửi request; server kiểm tra stage, vị trí và quyền rồi mới đổi state.
- Late joiner phải nhận snapshot canonical, gồm stage, house/clue masks và trạng thái kiểm tra xe.
- Không tăng tiến độ clue từ UI preview hoặc từ việc container vừa mở.
- Không khởi động tìm kiếm khu dân cư trước khi xe đã được kiểm tra và đóng UI lần đầu.
- Không chuyển checklist vật phẩm trở lại modal xe; checklist thuộc `J`.
- Không thay polygon nắp capo bằng hình chữ nhật hoặc nhãn đỏ che kín asset.
- Không phá flow hồi sinh cũ khi chỉnh spawn lần đầu cạnh xe.

## Kiểm thử và việc còn lại

- Mốc xác nhận trước thay đổi nút hành động: EditMode `38/38` và PlayMode `1/1` đã pass.
- Test PlayMode hiện đã có thêm assertion cho hotspot, polygon capo, vị trí checklist và nhãn nút hành động. Cần chạy lại Unity Test Runner sau khi pull/checkout vì môi trường tạo tài liệu này không được phép điều khiển Unity Editor.
- Cần QA tay ở độ phân giải mục tiêu: vị trí nút, text dài tiếng Việt, mở/đóng liên tục bằng `E`/`Esc`/`X`, vùng đứng trước mũi xe và khóa di chuyển/tấn công.
- Hạng mục chưa triển khai: transaction sửa xe đầu game (kiểm tra inventory authoritative, tiêu hao vật phẩm, đổi trạng thái bộ phận, hoàn tất nhiệm vụ phụ và cho xe rời khu vực).
- Cần QA multiplayer thật với host + client, đặc biệt late join, hai người mở cùng container, mất/nhặt lại clue và hai người tương tác xe cùng lúc.

## Checklist cho Codex ở máy khác

1. Đọc file này và hai task ID ở trên nếu máy còn quyền truy cập task.
2. Chạy `git status --short --branch` và đọc toàn bộ diff trước khi sửa.
3. Mở scene `Main`, xác nhận có `ViTriXeChetMay` và prefab xe xuất hiện đúng hướng.
4. Chạy EditMode và PlayMode tests trước/sau thay đổi.
5. Khi làm transaction sửa xe, giữ State Authority làm nguồn thật và tái sử dụng checklist inventory trong `J`.
6. Không commit `.codex-artifacts`, `.codex-remote-attachments` hay thư mục tạm; chỉ commit source/assets/project cần để Unity chạy trên máy khác.
