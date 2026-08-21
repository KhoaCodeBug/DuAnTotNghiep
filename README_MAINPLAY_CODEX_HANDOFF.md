# Main Play & Vehicle Quest — Technical Summary

Tài liệu này tóm tắt thiết kế, trạng thái triển khai và các điểm còn cần xác nhận để thành viên khác tiếp tục phát triển Main Play. Chiếc xe chết máy trong Intro và sedan ở đầu scene Main là cùng một phương tiện, thuộc cùng một flow liên tục.

## Quyết định thiết kế đã chốt

### Mạch truyện mở đầu

1. `Intro_Cinematic` đã có cảnh lái xe và radio chính phủ. Không phát thêm một đoạn radio mở đầu trùng lặp khi vào `Main`.
2. Xe chết máy trong Intro được tiếp nối tại `Main`, đặt theo GameObject `ViTriXeChetMay`.
3. Lần đầu vào Main, người chơi xuất hiện gần chiếc xe. Sau khi chết, hệ thống vẫn dùng các điểm hồi sinh nhà cũ; không hồi sinh cạnh xe.
4. Xe đầu game chỉ là phương tiện hướng dẫn/ngắn hạn. Xe thoát hiểm tại căn cứ quân sự dùng flow sửa chữa nâng cao riêng.

### Thuật ngữ xe đã chốt

- **Xe cảnh sát**: chiếc xe cũ đã có code lái và khoảng 25 ảnh nhưng hình ảnh xấu/mờ. Không tái sử dụng sprite của xe này; chỉ tham khảo cấu trúc controller, độ cao camera và footprint tổng quát. Góc nhìn của bộ ảnh cảnh sát cũng không khớp hoàn toàn với trục đường nên không phải chuẩn hướng cho asset sedan.
- **Xe đầu game**: sedan xanh cũ/bẩn, mui chở hành lý, xuất hiện liên tục ở scene hướng dẫn và đầu scene Main. Ảnh nguồn canonical là `Assets/Art/Generated/IntroCar_UpperLeft.png`. Mốc hiện tại dùng tám hướng; NE và SE đã có bản map-test v3 căn theo trục đường isometric, sáu hướng còn lại vẫn là asset thử nghiệm cần QA/sửa tiếp.
- Không được thay xe đầu game bằng xe cảnh sát hoặc dùng bộ sprite xe cảnh sát làm bản cuối.
- Mở rộng lên 16 hướng là hướng phát triển sau mốc tám hướng. Quy ước index/controller hiện tại phải được mở rộng tương thích, không loại bỏ nền tám hướng đã có.

### Tương tác xe đầu game

- Chỉ có thể bắt đầu kiểm tra khi đứng trong vùng polygon trước mũi xe và giữ `E` đủ thời gian.
- Khi người chơi hợp lệ đi vào vùng, viền polygon màu xanh và dòng `KIỂM TRA TÌNH TRẠNG XE — GIỮ [E]` mới hiện; rời vùng thì cả hai ẩn.
- UI kiểm tra khóa di chuyển và tấn công thông qua trạng thái modal của `AutoUIManager`.
- Đóng bằng `E` lần nữa, `Esc`, hoặc nút `X`. Giữ `E` để mở không được làm UI đóng ngay; code chờ người chơi nhả phím trước.
- UI có thể mở lại sau lần kiểm tra đầu.
- Chỉ sau khi đóng UI lần đầu, server mới ghi nhận đã kiểm tra xe và mở màn giới thiệu hai tuyến thoát hiểm A/B.
- Condition hiện hành: các bộ phận thật sự hỏng đặt `0%`; bộ phận tạm còn dùng được đặt từ `60%` trở lên. Các bước bắt buộc là sửa cụm động cơ/nắp capo, đổ nhiên liệu, thay ắc quy và thay đúng **lốp trước trái** đang hỏng. Ba lốp còn lại từ `60%` trở lên chỉ cho kiểm tra, không được tiêu hao lốp thay thế.
- Hoàn tất bốn action sửa chữa chưa tự sinh xe. Người chơi phải mở lại modal và bấm `KHỞI ĐỘNG XE`; State Authority kiểm tra người gửi, khoảng cách, trạng thái kiểm tra và toàn bộ repair bit trước khi đánh dấu hoàn tất/spawn xe Fusion. Request lặp hoặc chưa đủ điều kiện bị từ chối.

### Hai tuyến thoát hiểm và thời điểm khóa ending

- Sau khi đóng UI kiểm tra xe lần đầu, toàn đội được giới thiệu đồng thời hai tuyến thoát hiểm. Đây là lựa chọn **theo dõi**, chưa phải lựa chọn ending:

  - Tuyến A — Chiếc xe dân sự: tìm linh kiện, khôi phục xe chết máy, dùng xe để khám phá và đi tới lối thoát dân sự.
  - Tuyến B — Sơ tán quân sự: manh mối -> văn phòng -> căn cứ quân sự -> phòng thủ/sửa xe -> sơ tán.

- Nhật ký hiển thị hai tuyến ngang hàng. Thẻ cũ `Ghép lại tuyến đường` chỉ còn là tiến độ nội bộ của Tuyến B và không còn xuất hiện như một nhiệm vụ thứ ba bị trùng ý nghĩa.
- Đổi tuyến theo dõi không khóa nội dung và không làm mất tiến độ tuyến kia.
- Ending chỉ bị khóa tại điểm không thể quay lại, luôn có hộp xác nhận rõ ràng:

  - Tuyến A khóa khi tài xế đưa đúng chiếc xe dân sự đã sửa tới lối thoát và xác nhận rời khu vực.
  - Tuyến B khóa khi người chơi xác nhận kích hoạt báo động tại căn cứ quân sự, ngay trước `SiegeAndRepair`.

- `LockedEscapeRouteValue` trong `MainQuestManager` là state authoritative dùng chung cho cả đội. Tuyến được xác nhận đầu tiên thắng; mọi request khóa tuyến đối nghịch về sau bị từ chối. Late joiner nhận trạng thái khóa qua snapshot và Nhật ký đánh dấu `ĐÃ KHÓA` / `ĐÃ ĐÓNG`.
- Lối thoát dân sự ưu tiên Transform tên `CivilianEscapeExit`. Nếu scene chưa đặt object này, runtime tạo fallback tại `ViTriXeChetMay + (30, 0)`; nên QA và đặt marker scene chính thức sau khi chốt bố cục map.

### Radio giới thiệu và nguồn audio Tuyến B

- Sau khi đóng UI kiểm tra xe lần đầu, game không mở bảng A/B ngay. `RouteBRadioBroadcastUI` phát hai cue mở đầu: thông báo khẩn cấp giới thiệu hồ sơ tiếp tế/Văn phòng Điều phối, sau đó là phản ứng của nhân vật xác nhận cả hai hướng đều hợp lệ.
- Khi hai cue kết thúc hoặc người chơi nhấn `E` để bỏ qua, bảng chọn tuyến theo dõi mới xuất hiện. Thao tác này vẫn không ghi `LockedEscapeRouteValue`.
- `RouteBAudioContent` là nguồn nội dung canonical gồm 15 cue Việt/Anh từ lúc kiểm tra xe đến kết thúc sơ tán quân sự. Clip thu âm tùy chọn được nạp từ `Resources/Story/RouteB/`; khi chưa có clip, subtitle vẫn chạy và cue radio dùng static procedural.
- Quy ước tên file, hướng dẫn thu và thứ tự cue nằm tại `Assets/Resources/Story/RouteB/README_ROUTE_B_AUDIO_SOURCE.md`.

### UI cơ khí phương tiện

- Giao diện theo phong cách Project Zomboid: sơ đồ xe ở trái, bảng tình trạng và chi tiết bộ phận ở phải.
- Có thể bấm trực tiếp hotspot trên sơ đồ hoặc từng hàng để chọn 11 bộ phận: động cơ, ắc quy, ống xả, bình xăng, bốn lốp, nắp capo, kính chắn gió và cửa trước.
- Hotspot và viền pulse đang chọn dùng bộ RectTransform đã được căn tay trong Unity ngày 2026-08-20; giữ các giá trị trong `EnsureVehiclePartDefinitions`. Viền chỉ pulse alpha, không scale, để không tràn ra ngoài artwork.
- Nắp capo hư được tô bằng polygon đỏ trong suốt theo đúng hình nắp xe, không dùng khối chữ nhật đỏ kèm chữ trắng.
- Bộ phận đang chọn có viền/pulse nhẹ, có hàng được chọn và bảng chẩn đoán riêng.
- Nút hành động ngữ cảnh ở góc dưới phải của bảng chi tiết được giữ đúng bản thiết kế cuối:

  - `Sửa chữa`: động cơ, nắp capo.
  - `Thay linh kiện`: ắc quy và các lốp được đánh dấu cần thay.
  - `Đổ nhiên liệu`: bình xăng.
  - `Kiểm tra`: ống xả, các bộ phận chưa cần thay và thân xe.

- Bấm `Kiểm tra` hiển thị kết quả chẩn đoán. Các nút sửa/thay/đổ gửi request lên State Authority; server kiểm tra người chơi, khoảng cách, state và inventory trước khi thực hiện.
- Danh sách vật phẩm không còn nằm trong modal kiểm tra xe. Nhật ký `J` là nguồn hiển thị người chơi đang có hoặc còn thiếu gì.
- Transaction sửa xe hiện có tính idempotent theo từng hành động: mỗi bộ phận chỉ nhận sửa/thay thành công một lần, nên loot thừa có thể giữ, bỏ ra hoặc trao đổi cho đồng đội mà không gây sửa lặp. Bộ dụng cụ + búa được giữ lại; vật tư tiêu hao chỉ bị trừ khi server chấp nhận hành động.
- Runtime chỉ spawn xe Fusion sau flow `sửa cụm máy + nhiên liệu + ắc quy + lốp trước trái -> bấm KHỞI ĐỘNG XE -> server xác nhận`. Checklist trong `J` có bốn action sửa chữa và một bước khởi động riêng.

### Balo, tủ loot và bảo hiểm vật phẩm sửa xe

- Inventory Player là cố định **20 ô tổng**: 5 Hotbar + 15 ô kho. Không còn cấp balo, item balo hay thay đổi sức chứa runtime. Lời gọi legacy cố đổi sức chứa sẽ bị từ chối và giữ 20.
- UI kho Player hiển thị đủ 15 ô theo lưới 5x3. Panel đã hạ chiều cao từ 630 xuống 530 để tiêu đề `INVENTORY` không bị thanh tab `INVENTORY / HEALTH STATUS` che khi mở riêng. Scroll dọc vẫn được giữ làm fallback ở độ phân giải thấp.
- Loot Container mặc định **20 ô**, UI 4x5. Prefab nào chủ động cấu hình sức chứa thấp hơn vẫn ẩn các ô dư. Random loot thường chừa hai ô cho item nhiệm vụ.
- Khi State Authority khởi tạo khu nhà được chọn, server phân phối đúng một bộ bảo đảm gồm `Toolbox`, `Hammer`, `FuelCan`, `Battery`, `Tire`. Ưu tiên năm tủ khác nhau; nếu map có ít tủ hợp lệ thì cho phép dùng chung tủ để tránh soft-lock.
- Sau bộ bảo đảm, mỗi loại có `35%` cơ hội xuất hiện thêm đúng một bản hỗ trợ co-op/trao đổi. Không có vòng quét liên tục, không tự xóa đồ thừa và không ép mỗi loại chỉ tồn tại một bản trên toàn server.
- Cất đồ vào tủ là transaction authoritative: server kiểm tra đúng Player, số lượng thật và chỗ trống trước khi trừ inventory. Tủ đầy phải từ chối mà không làm mất item.

### Manh mối và bản đồ

- Manh mối tuyến đường là vật phẩm thật trong loot, không tăng tiến độ ngay lúc mở container. Tiến độ chỉ tăng khi vật phẩm thực sự vào inventory.
- Có ba tài liệu tuyến đường riêng biệt. State Authority quản lý mask đã nhặt, mask đã “bảo hiểm” trong container và số lần mở hụt.
- Tỉ lệ cơ bản hiện là 70%; sau một lần hụt, lần hợp lệ kế tiếp được bảo đảm. Nếu vật phẩm được bảo hiểm bị mất khỏi container, hệ thống có thể phục hồi để tránh soft-lock.
- Đủ `3/3` manh mối mới chuyển sang tìm văn phòng cổng tím.
- Khi đủ `3/3`, State Authority ghép tiến độ và tiêu hao ba tài liệu một cách authoritative bằng cách quét inventory của toàn đội; không còn phụ thuộc client nhặt manh mối cuối cùng tự xóa item.
- Chuỗi văn phòng: kiểm tra bàn/tài liệu, nghe radio, sau đó mở tủ/két đúng mục tiêu để lấy mảnh bản đồ tiếp theo. Không cho phép tương tác sai thứ tự làm nhảy quest.
- `J` mở nhật ký nhiệm vụ; `M` mở bản đồ. Vùng tìm kiếm khu dân cư và vùng văn phòng được reveal độc lập, không làm sáng toàn bản đồ.
- Nhãn `KHU VỰC TÌM MANH MỐI` có plate tối viền cam và chữ sáng để không chìm vào lớp màu của vùng tìm kiếm.
- Tuning map hiện tại:

  - Khu dân cư: Center `(0.503, 0.339)`, Size `(0.288, 0.176)`.
  - Khu văn phòng: Center `(0.505, 0.485)`, Size `(0.284, 0.138)`.

## Flow Main Play hiện tại

```text
Main Menu / Solo
  -> Intro_Cinematic: lái xe + radio chính phủ
  -> Main: spawn lần đầu cạnh xe tại ViTriXeChetMay
  -> Đứng trước mũi xe, giữ E, kiểm tra và đóng UI
  -> Hiện hai tuyến A/B; chọn tuyến THEO DÕI hoặc chọn sau (chưa khóa ending)
  -> Tuyến A: tìm vật tư sửa xe dân sự (song song, không bắt buộc để đi Tuyến B)
  -> Tuyến A: sửa cụm máy + nhiên liệu + ắc quy + lốp trước trái
  -> Mở lại modal, bấm KHỞI ĐỘNG XE; server xác nhận rồi mới sinh xe có thể lái
  -> Tuyến B: tìm kiếm các nhà được State Authority chọn
  -> Nhặt đủ 3 tài liệu tuyến đường thật trong inventory
  -> Suy luận và tìm văn phòng cổng tím
  -> Bàn/tài liệu -> radio -> tủ/két -> mảnh bản đồ
  -> Reveal tuyến tới căn cứ quân sự và tập hợp đội
  -> Tới căn cứ, kiểm tra xe thoát hiểm
  -> Xác nhận kích hoạt báo động = KHÓA TUYẾN B; bắt đầu SiegeAndRepair
  -> Bật máy phát, mở kho vũ khí/két văn phòng
  -> Thu thập và lắp ắc quy + nhiên liệu + bộ sửa chữa
  -> Giữ tương tác sửa xe theo tiến độ trong lúc phòng thủ cổng
  -> ReadyToEscape -> tập hợp người còn sống -> thoát
  -> Victory cutscene / bảng tổng kết riêng cho Tuyến B

Tuyến A sau khi xe dân sự đã sửa:
  -> Lái đúng xe tới CivilianEscapeExit
  -> Xác nhận rời khu vực = KHÓA TUYẾN A
  -> Bảng tổng kết thoát hiểm bằng xe dân sự
```

Các phase authoritative của căn cứ quân sự là `NotReached`, `Investigating`, `SiegeAndRepair`, `ReadyToEscape`, `Escaped`, `Failed`. Nếu toàn đội chết trong giai đoạn vây hãm/chuẩn bị thoát, nhiệm vụ chuyển sang `Failed`.

## Điều khiển liên quan

- Không tạo banner hướng dẫn `[J] MỞ NHẬT KÝ NHIỆM VỤ` khi vừa vào Main; người chơi nhận mục tiêu bằng sự kiện trong thế giới và vẫn có thể chủ động mở nhật ký bằng `J`.
- `E` giữ: kiểm tra xe khi đứng đúng vùng; `E` lần nữa đóng modal.
- Thanh tiến độ giữ `E` dùng một hàng cố định rộng 420 px, tự giảm cỡ chữ và không được wrap xuống dưới thanh.
- `Esc` hoặc `X`: đóng modal.
- `J`: nhật ký và checklist nhiệm vụ; tại đây mới kiểm tra vật phẩm đã có/đang thiếu.
- `M`: bản đồ nhiệm vụ.
- Trong nhật ký: `W/S` đổi nhiệm vụ, `Q/E` đổi tab, `V` bật/tắt theo dõi (khi UI cho phép).
- Chỉ trong Unity Editor, Solo/Host: `F8` cấp những vật tư sửa xe còn thiếu để test nhanh; không tự sửa, không tự khởi động xe và không tồn tại trong player build.

## Các file quan trọng

| File | Trách nhiệm |
| --- | --- |
| `Assets/Script/Tin/MainQuest/MainQuestManager.cs` | State Authority của nửa đầu Main, car-inspection state, nhà tìm kiếm, loot clue, stage và snapshot mạng. |
| `Assets/Script/Tin/PlayerMovement.cs` | Áp input di chuyển đã chiếu sang hệ isometric 2:1 cho Player; khi ngồi xe vẫn giữ input ga/đánh lái thô cho controller xe. |
| `Assets/Script/Tin/Prototype/IsometricMovementProjection.cs` | Utility thuần giữ tốc độ/magnitude và đổi bốn hướng chéo 45 độ sang trục đường isometric 2:1. Đây cũng là nền hướng dùng lại cho xe 8 chiều. |
| `Assets/Script/Tin/Prototype/EightWayDirection.cs` | Quy ước index cố định `N, NE, E, SE, S, SW, W, NW`, snap heading và vector hướng đã chiếu isometric cho sedan. |
| `Assets/Hau/Script/VehicleController.cs` | Controller Fusion dùng chung; xe cũ giữ layout 5x5/25 ảnh, sedan chọn layout tám hướng isometric và nhãn debug hướng khi local Player lái. |
| `Assets/Script/Tin/InventorySystem.cs` | Inventory Player cố định 20 ô (5 Hotbar + 15 kho) và transaction add/consume dùng slot index ổn định. |
| `Assets/Script/Tin/AutoUIManager.cs` | UI 15 ô kho Player, 20 ô Loot Container, bố cục tab/tiêu đề và request transfer authoritative. |
| `Assets/Script/Tin/MainQuest/MainArrivalStoryBootstrap.cs` | Nối Intro với Main, tìm `ViTriXeChetMay`, tạo/đặt xe và cấu hình spawn lần đầu. |
| `Assets/Script/Tin/MainQuest/BrokenArrivalCar.cs` | Vùng polygon trước mũi xe, prompt, giữ `E`, mở lại UI và báo lần đóng đầu. |
| `Assets/Script/Tin/MainQuest/ArrivalCarInspectionUI.cs` | Modal cơ khí, condition từng bộ phận, nắp capo đỏ, action ngữ cảnh và nút khởi động xe. |
| `Assets/Script/Tin/MainQuest/ArrivalCarItemCatalog.cs` | Stable item IDs, alias, icon/runtime ItemData và yêu cầu inventory cho từng hành động sửa xe. |
| `Assets/Script/Tin/Prototype/ArrivalCarRepairRules.cs` | Enum/bitmask thuần cho action, part ID, điều kiện hoàn tất bắt buộc và luật tiêu hao vật phẩm. |
| `Assets/Script/Tin/Prototype/EscapeEndingRoute.cs` | Enum và luật thuần: theo dõi không khóa, ending chỉ khóa một lần và không thể đổi sang tuyến đối nghịch. |
| `Assets/Script/Tin/MainQuest/EscapeRouteDecisionUI.cs` | Modal giới thiệu hai tuyến và hộp xác nhận point-of-no-return. |
| `Assets/Script/Tin/Prototype/RouteBAudioContent.cs` | Nguồn thoại/subtitle Việt-Anh, speaker, timing và đường dẫn clip cho toàn bộ Tuyến B. |
| `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs` | Panel radio nhỏ, phát clip tùy chọn/static fallback và đảm bảo radio chạy trước bảng chọn A/B. |
| `Assets/Script/Tin/MainQuest/CivilianEscapeRouteController.cs` | Marker/prompt lối thoát dân sự, kiểm tra đúng xe/tài xế/khoảng cách và gửi request authoritative. |
| `Assets/Script/Tin/MainQuest/UIPolygonGraphic.cs` | Graphic polygon UI tái sử dụng cho vùng/hình không chữ nhật. |
| `Assets/Script/Tin/MainQuest/VehiclePartPulseHighlight.cs` | Viền/pulse thẩm mỹ cho bộ phận đang chọn. |
| `Assets/Script/Tin/MainQuest/PreMilitaryQuestRuntimeBridge.cs` | Nối state mạng với UI, inventory, map reveal, boundary và cinematic trước căn cứ. |
| `Assets/Script/Tin/MainQuest/QuestRouteClueItemCatalog.cs` | Định nghĩa ba item manh mối, tên, nội dung đọc và icon runtime. |
| `Assets/Khoa/Code/LootContainer.cs` | Dữ liệu tủ 20 ô, loot/transfer authoritative và API đặt vật phẩm nhiệm vụ an toàn. |
| `Assets/Script/Tin/MainQuest/QuestMapRevealTuningTool.cs` | Thông số vùng reveal trước/sau quest và công cụ chỉnh trong Play Mode. |
| `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs` | Nhật ký `J`, checklist sửa xe và các overlay quest. |
| `Assets/Script/Tin/Prototype/QuestMapUIPrototype.cs` | Bản đồ `M` và các lớp reveal. |
| `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs` | State Authority của flow căn cứ, vật phẩm, lắp đặt, sửa xe, siege và thoát. |
| `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs` | Spawn player và gọi bootstrap story Main. |
| `Assets/Resources/Story/BrokenArrivalCar.prefab` | Prefab xe đầu game và polygon tương tác đã cấu hình. |
| `Assets/Hau/NewPrefab/Car/Car.prefab` | Prefab xe cũ/25 ảnh được giữ nguyên để tránh regression và chỉ làm nguồn cấu trúc controller. |
| `Assets/Hau/NewPrefab/Car/IntroSedanCar.prefab` | Prefab Fusion sedan tám hướng được server spawn khi xe đầu game sửa xong; renderer/Animator xe cảnh sát đã tắt. |

Asset canonical hướng NW của xe đầu game là `Assets/Art/Generated/IntroCar_UpperLeft.png`. Các hướng sedan nằm trong `Assets/Art/Generated/IntroCarDirections/`; PNG có alpha thật và import Sprite 100 PPU. Prefab hiện dùng `IntroCar_NE_MapTest_v3.png` tại index 1 và `IntroCar_SE_MapTest_v3.png` tại index 3. Hai bản này được xoay riêng quanh tâm xe để trục silhouette đạt lần lượt `-26.565°` và `+26.565°`, khớp trục 2:1 của map về mặt hình học; cảm giác phối cảnh/pivot cuối vẫn cần xác nhận trong gameplay. N, E, S, SW, W và NW hiện giữ nguyên, trong đó N/W đã được ghi nhận là chưa đúng phối cảnh. Các asset UI/hotspot liên quan nằm trong `Assets/Art/Generated/Car/`; khi thay sprite nhiều hướng phải giữ silhouette hoặc căn lại hotspot/polygon tương ứng.

## Nguyên tắc multiplayer và tránh regression

- `MainQuestManager` và `MilitaryBaseQuestManager` là nguồn sự thật. Client chỉ gửi request; server kiểm tra stage, vị trí và quyền rồi mới đổi state.
- Late joiner phải nhận snapshot canonical, gồm stage, house/clue masks, trạng thái kiểm tra xe, repair bitmask, trạng thái hoàn tất, ending đã khóa và NetworkObject xe đã spawn.
- Chọn tuyến để theo dõi không được ghi `LockedEscapeRouteValue`. Chỉ confirmation tại `CivilianEscapeExit` hoặc confirmation bật báo động quân sự mới được khóa.
- Multiplayer dùng một ending chung cho cả đội; không tạo ending riêng từng client.
- Không tăng tiến độ clue từ UI preview hoặc từ việc container vừa mở.
- Không khởi động tìm kiếm khu dân cư trước khi xe đã được kiểm tra và đóng UI lần đầu.
- Không chuyển checklist vật phẩm trở lại modal xe; checklist thuộc `J`.
- Không thay polygon nắp capo bằng hình chữ nhật hoặc nhãn đỏ che kín asset.
- Không phá flow hồi sinh cũ khi chỉnh spawn lần đầu cạnh xe.

## Kiểm thử và việc còn lại

- Mốc xác nhận mới nhất ngày 2026-08-22: Unity compile sạch, Console `0` compile error; EditMode `71/71` pass sau khi gắn NE/SE map-test v3. E2E PlayMode `MainMenu -> SOLO -> EASY -> ENTER THE DEAD ZONE -> Main -> sửa xe -> KHỞI ĐỘNG XE -> spawn IntroSedanCar` từng pass `1/1` (`15.59s`), gồm assertion prefab dùng tám sprite sedan, layout isometric, khởi tạo NW và tắt renderer xe cảnh sát. Hai lượt E2E sau đó bị chặn sớm bởi sai số layout UI không liên quan (`208.500031 > 208.5`) trước bước spawn xe; vì vậy bản v3 chưa được tính là đã QA runtime. Không nới tolerance UI chỉ để test xe đi qua.
- Checkpoint tọa độ Phase 4 đã vào code và được người dùng QA tay đạt: Player đi cardinal như cũ, còn `W+A`, `W+D`, `S+A`, `S+D` được chiếu từ chéo 45 độ sang trục 2:1 (`|y/x| = 0.5`) và normalize lại để không đổi tốc độ. Input analog giữ magnitude; input khi ngồi xe chưa bị đổi. Automated test đã pass.
- Test bao phủ inventory cố định 20 ô, consume không làm lệch index, tủ mặc định 20 ô và từ chối ô thứ 21; PlayMode kiểm tra đủ 15/20 ô UI, lưới 5x3/4x5, không tràn viewport, hai panel không chồng nhau và tiêu đề inventory mở riêng nằm dưới thanh tab.
- Quality gate modal xe kiểm tra hotspot, condition `0%`/`>=60%`, nút khởi động không chồng nút đóng/đường header, nút bị khóa trước khi sửa đủ, xe không tự spawn sau action cuối, và chỉ spawn sau request khởi động thành công.
- Lỗi PlayMode không ổn định trước đây chưa tái hiện sau nhiều lượt. Nguyên nhân lỗi `RenderTexture.Create failed: height > 0` là GameView/Test Runner có kích thước nội dung bằng 0, không phải logic gameplay. `MainMenuManager` hiện log rõ cleanup, yêu cầu load scene, `OnSceneLoadStart` và `OnSceneLoadDone`, đồng thời bắt exception và trả UI về trạng thái có thể thử lại.
- Cần QA tay ở độ phân giải mục tiêu: vị trí nút, text dài tiếng Việt, mở/đóng liên tục bằng `E`/`Esc`/`X`, thanh tiến độ `0 / 3`, tab hoàn thành rỗng và khóa di chuyển/tấn công.
- Cần QA multiplayer thật với host + client, đặc biệt: hai người cùng mở/cất/nhặt ở một tủ, tủ đầy không mất item, bộ năm vật tư luôn tồn tại lúc khởi tạo, item bonus có thể trao đổi, late join sau khi xe đã spawn và hai người gửi cùng repair action.
- Cần QA tay cả hai confirmation point-of-no-return và tình huống hai client đồng thời xác nhận hai tuyến đối nghịch; server phải chỉ chấp nhận request đầu tiên.
- Cần đặt/chốt vị trí scene `CivilianEscapeExit`; hiện có fallback chạy được nhưng chưa phải quyết định level-design cuối.
- **Phase 3 đã hoàn tất ở mức code + automated QA:** condition, ắc quy, một lốp hỏng, nút khởi động và server gate đã vào flow. Vẫn cần QA tay host + client thật để xác nhận cảm giác UI, transaction đồng thời và late join.
- **Phase 4 đang làm:** checkpoint nền tọa độ Player đã hoàn tất code + automated QA + QA tay. `VehicleControllerFusion` có layout tám hướng isometric riêng, vẫn tương thích layout 25 ảnh của xe cảnh sát; `IntroSedanCar.prefab` đã nối vào `repairedArrivalCarPrefab` của Main. NE/SE map-test v3 đang là checkpoint art mới nhất; bước kế tiếp là QA runtime hai hướng này rồi sửa lần lượt sáu hướng provisional. Sau khi mốc tám hướng ổn định mới mở rộng 16 hướng.
- Cần người dùng quyết định phần thưởng runtime `MilitaryBackpackLevel3` cũ của Tuyến B: xóa hẳn hay đổi thành bundle tiếp tế. Inventory cố định 20 ô sẽ không nhận hiệu ứng tăng sức chứa từ item này.
