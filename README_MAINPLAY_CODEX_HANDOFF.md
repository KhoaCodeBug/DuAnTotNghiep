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
- Chỉ sau khi đóng UI lần đầu, server mới ghi nhận đã kiểm tra xe và mở màn giới thiệu hai tuyến thoát hiểm A/B.
- Hư hỏng cốt truyện: động cơ quá nhiệt/bộ đề kẹt, nhiên liệu gần cạn. Bộ dụng cụ, búa và can nhiên liệu là vật phẩm bắt buộc; ắc quy và lốp là nâng cấp tùy chọn.

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
- Transaction sửa xe đầu game đã hoàn thiện và có tính idempotent theo từng hành động. Bộ dụng cụ + búa được giữ lại; can nhiên liệu bị tiêu hao; ắc quy + lốp là tùy chọn và chỉ bị tiêu hao khi người chơi chủ động lắp.
- Khi đã sửa bộ đề và đổ nhiên liệu, server đánh dấu giai đoạn chuẩn bị Tuyến A hoàn tất, spawn prefab xe Fusion có thể lái tại vị trí xe hỏng và đồng bộ NetworkObject cho host/client/late joiner. Xe hỏng cũ tự ẩn sprite, collider và vùng tương tác.

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
  -> Tuyến A: tìm dụng cụ + búa + nhiên liệu, sửa xe và nhận xe Fusion có thể lái
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

## Các file quan trọng

| File | Trách nhiệm |
| --- | --- |
| `Assets/Script/Tin/MainQuest/MainQuestManager.cs` | State Authority của nửa đầu Main, car-inspection state, nhà tìm kiếm, loot clue, stage và snapshot mạng. |
| `Assets/Script/Tin/MainQuest/MainArrivalStoryBootstrap.cs` | Nối Intro với Main, tìm `ViTriXeChetMay`, tạo/đặt xe và cấu hình spawn lần đầu. |
| `Assets/Script/Tin/MainQuest/BrokenArrivalCar.cs` | Vùng polygon trước mũi xe, prompt, giữ `E`, mở lại UI và báo lần đóng đầu. |
| `Assets/Script/Tin/MainQuest/ArrivalCarInspectionUI.cs` | Modal cơ khí, chọn bộ phận, nắp capo đỏ và nút hành động ngữ cảnh. |
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
| `Assets/Script/Tin/Loot/LootContainer.cs` | Bảo đảm năm item sửa xe được phân phối authoritative vào các container nhà đã chọn. |
| `Assets/Script/Tin/MainQuest/QuestMapRevealTuningTool.cs` | Thông số vùng reveal trước/sau quest và công cụ chỉnh trong Play Mode. |
| `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs` | Nhật ký `J`, checklist sửa xe và các overlay quest. |
| `Assets/Script/Tin/Prototype/QuestMapUIPrototype.cs` | Bản đồ `M` và các lớp reveal. |
| `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs` | State Authority của flow căn cứ, vật phẩm, lắp đặt, sửa xe, siege và thoát. |
| `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs` | Spawn player và gọi bootstrap story Main. |
| `Assets/Resources/Story/BrokenArrivalCar.prefab` | Prefab xe đầu game và polygon tương tác đã cấu hình. |
| `Assets/Hau/NewPrefab/Car/Car.prefab` | Prefab Fusion được server spawn khi xe đầu game sửa xong. |

Asset ảnh UI xe nằm trong `Assets/Art/Generated/Car/`. Ảnh nguồn nhỏ có thể hơi mềm khi phóng lớn; code dùng cách hiển thị giữ tỉ lệ và overlay vector để hạn chế lộ vỡ ảnh. Nếu thay asset độ phân giải cao, giữ cùng silhouette hoặc chỉnh lại hotspot/polygon.

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

- Mốc xác nhận sau khi thêm radio và nguồn audio Tuyến B ngày 2026-08-21: EditMode `43/43` và PlayMode `1/1` đã pass bằng Unity Test Runner.
- Test đã bao phủ repair rules, snapshot/late join UI, trạng thái vật phẩm giữ lại/tiêu hao/nâng cấp, progress text chỉ còn `0 / 3`, divider tab hoàn thành rỗng, action bar không wrap, hotspot trùng khung artwork và flow PlayMode spawn xe lái được sau khi sửa.
- Cần QA tay ở độ phân giải mục tiêu: vị trí nút, text dài tiếng Việt, mở/đóng liên tục bằng `E`/`Esc`/`X`, thanh tiến độ `0 / 3`, tab hoàn thành rỗng và khóa di chuyển/tấn công.
- Cần QA multiplayer thật với host + client, đặc biệt late join sau khi xe đã spawn, hai người gửi cùng repair action, hai người mở cùng container, mất/nhặt lại clue và manh mối nằm rải trong inventory nhiều người.
- Cần QA tay cả hai confirmation point-of-no-return và tình huống hai client đồng thời xác nhận hai tuyến đối nghịch; server phải chỉ chấp nhận request đầu tiên.
- Cần đặt/chốt vị trí scene `CivilianEscapeExit`; hiện có fallback chạy được nhưng chưa phải quyết định level-design cuối.
- Cần quyết định UX cho ắc quy/lốp tùy chọn nếu người chơi hoàn tất bộ đề + nhiên liệu trước: hiện xe được spawn ngay và các nâng cấp tùy chọn chỉ có thể lắp trước mốc hoàn tất bắt buộc.

## Checklist cho Codex ở máy khác

1. Đọc file này và hai task ID ở trên nếu máy còn quyền truy cập task.
2. Chạy `git status --short --branch` và đọc toàn bộ diff trước khi sửa.
3. Mở scene `Main`, xác nhận có `ViTriXeChetMay` và prefab xe xuất hiện đúng hướng.
4. Chạy EditMode và PlayMode tests trước/sau thay đổi.
5. Khi mở rộng transaction sửa xe, giữ State Authority làm nguồn thật, không đổi luật giữ bộ dụng cụ/búa và tái sử dụng checklist inventory trong `J`.
6. Không commit `.codex-artifacts`, `.codex-remote-attachments` hay thư mục tạm; chỉ commit source/assets/project cần để Unity chạy trên máy khác.
