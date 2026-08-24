# ROUTE B — COMPLETE FLOW & CODEX HANDOFF

> Cập nhật: 2026-08-24  
> Mục đích: tài liệu bàn giao canonical để mở chat Codex mới và tiếp tục Tuyến B mà không phải đọc lại toàn bộ lịch sử.  
> Trạng thái hiện tại: **Toàn bộ state machine, Nhật ký, 15 audio, lựa chọn ending và đường test Tuyến B đã chạy được từ MainMenu tới Ending B mà không cần LootContainer. Gameplay loot/prop thật trong bệnh viện và căn cứ vẫn tạm ngưng cho tới khi chủ dự án xác nhận đã setup LootContainer.**

## 1. Quyết định thiết kế đã chốt

- Game có hai tuyến thoát hiểm ngang hàng:
  - **Tuyến A — Khôi phục chiếc xe:** tự sửa xe dân sự, khám phá đường thoát và vượt vòng phong tỏa.
  - **Tuyến B — Lần theo tín hiệu quân sự:** tìm hồ sơ, tới Khu Điều phối, xác định căn cứ quân sự, phòng thủ và dùng xe sơ tán để thoát.
- Người chơi được biết cả hai tuyến ngay sau khi kiểm tra chiếc xe hỏng đầu game.
- Bảng chọn đầu tiên và bảng chọn sau bệnh viện **chỉ đổi tuyến đang theo dõi**, không khóa ending.
- Người chơi có thể chuẩn bị cả hai tuyến song song trước điểm không thể quay lại.
- Ending B chỉ bị khóa khi người chơi tới xe quân sự, nghe cảnh báo và **xác nhận kích hoạt báo động/phòng thủ**. Việc khóa áp dụng cho toàn đội.
- Bệnh viện là địa điểm. **Khu/Văn phòng Điều phối là một khu chức năng nằm bên trong bệnh viện**, không phải một tòa nhà văn phòng riêng.
- Chuỗi bệnh viện chưa được chốt bố trí. Chờ loot container và prop thật rồi mới quyết định vị trí Bàn Điều phối → Radio → Tủ hồ sơ.

## 2. Flow tổng quát

```text
Main Menu → Main
    ↓
Xe đầu game trục trặc → kiểm tra xe
    ↓
Cue 01–02 → bảng chọn tuyến lần 1 (chỉ theo dõi)
    ↓
Tìm 3 tài liệu tại khu dân cư
    ↓
Cue 03 → Cue 04 → Cue 05
    ↓
Mở vị trí bệnh viện/Khu Điều phối trên bản đồ
    ↓
Vào Khu Điều phối trong bệnh viện
    ↓
Cue 06
    ↓
Bàn Điều phối → Radio → Tủ hồ sơ/Bản đồ
    ↓
Cue 07 → Cue 08 → Cue 09
    ↓
Phần thưởng dùng ảnh bản đồ thật → cinematic căn cứ → bảng đổi mục tiêu theo dõi lần 2
    ↓
Đi tới căn cứ quân sự
    ↓
Cue 10
    ↓
Kiểm tra xe quân sự → Cue 11 → xác nhận điểm không thể quay lại
    ↓
Khóa Ending B cho toàn đội → Cue 12 → bắt đầu siege
    ↓
Khởi động máy phát + tìm/lắp linh kiện + sửa xe trong lúc chống zombie
    ↓
Cue 13 → Cue 14
    ↓
Tập hợp đội → rời căn cứ
    ↓
Cue 15 → cutscene → Victory Summary Ending B
```

## 3. Quy trình chi tiết từng nhiệm vụ

### Nhiệm vụ B0 — Kiểm tra chiếc xe hỏng

**Stage authoritative:** `NotStarted`

1. Người chơi đi từ Main Menu vào scene `Main` và được spawn bình thường.
2. Đến gần chiếc xe hỏng từ Intro, đứng trong vùng kiểm tra trước mũi xe và mở bảng tình trạng xe.
3. Khi xác nhận kiểm tra, Host/State Authority đặt `IsArrivalCarInspected = true`.
4. UI kiểm tra xe đóng lại.
5. Client đã kích hoạt nghe hai cue mở đầu:
   - Cue 01 — `OpeningEmergencyBroadcast`: radio nói tuyến sơ tán dân sự phía bắc bị chặn; khu dân cư phía đông có hồ sơ tiếp tế; Khu Điều phối giữ bản đồ tới điểm tập kết quân sự.
   - Cue 02 — `PlayerRouteReaction`: nhân vật nhận ra có hai hướng thoát.
6. Sau cue 02, mở bảng chọn tuyến lần đầu:
   - Phím/nút Tuyến A: theo dõi Khôi phục chiếc xe.
   - Phím/nút Tuyến B: theo dõi Lần theo tín hiệu quân sự.
   - Chọn sau/đóng bảng vẫn được phép.
7. Lựa chọn này chỉ đổi journal/HUD đang theo dõi, không ghi `LockedEscapeRoute`.
8. `PreMilitaryQuestRuntimeBridge` chọn tối đa 6 căn nhà hợp lệ, yêu cầu ít nhất 3, rồi Host chuyển stage sang `SearchNeighborhood`.

**Vận hành song song với Tuyến A:** lúc khởi tạo khu dân cư, code cũng phân phối các vật phẩm sửa xe đầu game vào các nhà. Người chơi có thể nhặt đồ sửa xe trong lúc tìm tài liệu Tuyến B.

### Nhiệm vụ B1 — Tìm ba tài liệu tại khu dân cư

**Stage authoritative:** `SearchNeighborhood`  
**Mục tiêu:** thu thập `3/3` tài liệu Tuyến B thật sự từ `LootContainer`.

Ba loại manh mối:

1. Hồ sơ tiếp tế → Cue 03 `FirstSupplyDocument`.
2. Lịch trình sơ tán → Cue 04 `SecondEvacuationDocument`.
3. Địa chỉ Điều phối/tần số dự phòng → Cue 05 `ThirdCoordinationDocument`.

**Cách loot vận hành:**

- Chỉ container nằm trong một `QuestLocationIdentity` loại `ResidentialHouse` mới hợp lệ.
- Mỗi container mới mở có một lần roll authoritative.
- Tỉ lệ cơ bản hiện tại là 70%.
- Nếu một container trượt, container hợp lệ mới tiếp theo được bảo đảm sinh manh mối.
- Manh mối được đặt thành item giấy thật bên trong container; chỉ mở container không hoàn thành nhiệm vụ.
- Chỉ khi người chơi lấy item qua giao dịch inventory authoritative thì `RouteClueMask` mới tăng.
- Manh mối trùng không tăng tiến độ.
- Nếu container mang manh mối bị mất/reset, hệ thống có cơ chế đưa lại đúng tài liệu ở lần mở container hợp lệ tiếp theo để tránh soft-lock.
- Khi đủ 3, các tài liệu đã thu được được tiêu thụ authoritative, stage chuyển sang `LocateOffice` và phần thưởng Mảnh bản đồ 1 được trình bày.

**Ranh giới nhiệm vụ:**

- Chỉ hoạt động trong `SearchNeighborhood`.
- Hình chữ nhật trên bản đồ và ranh giới gameplay dùng cùng dữ liệu.
- Khi ra ngoài: cảnh báo Outside Search Area xuất hiện, sương/blackout tăng theo khoảng cách.
- Sau 2 giây ở ngoài vùng, client gửi yêu cầu và State Authority dịch chuyển người chơi về điểm an toàn trong vùng; nếu request bị rơi, hệ thống thử lại sau 1,25 giây.
- Sang `LocateOffice`, ranh giới, cảnh báo và cưỡng chế quay về của khu dân cư phải tắt. Người chơi được tự do đi đến bệnh viện.

**Multiplayer:** danh sách nhà, mask tài liệu và stage do State Authority giữ; mọi client và late join dùng cùng khu vực, cùng tiến độ. Cue 03–04 tập trung vào client nhặt tài liệu; phần hoàn tất 3/3 hiện đang được trình bày đồng bộ qua RPC.

### Nhiệm vụ B2 — Xác định bệnh viện và tìm Khu Điều phối

**Stage authoritative:** `LocateOffice`

1. Sau Cue 05, bản đồ chuyển từ chỉ biết khu dân cư sang biết vị trí chính xác của mục tiêu kế tiếp.
2. Journal và HUD phải đổi ngay sang mục tiêu mới, đồng bộ với stage authoritative.
3. Người chơi mở bản đồ bằng `M` và đi tới bệnh viện được đánh dấu.
4. Khi local player đi vào collider `KhuVucNhiemVu`, client gửi request tới State Authority.
5. Server xác thực:
   - Stage vẫn là `LocateOffice`.
   - Trigger ID hợp lệ.
   - Vị trí authoritative của player thật sự nằm trong collider.
   - Có ít nhất ba `MainQuestSearchCabinet` hợp lệ.
6. Server đặt `IsOfficeDiscovered = true`, chọn điểm đầu tiên, chuyển stage sang `FindCityMap`.
7. Client kích hoạt nghe Cue 06 `OfficeLocated`.

**Câu UI canonical cần đổi khi làm tiếp:**

- Hiện code vẫn dùng: `Tìm văn phòng màu tím trong khu vực đã xác định`.
- Thiết kế đã chốt nên dùng: `Tìm khu Điều phối bên trong bệnh viện được đánh dấu`.
- Bản tiếng Anh đề xuất: `Find the Coordination Section inside the marked hospital`.
- Audio 01–15 không bắt buộc thu lại vì “Văn phòng/Khu Điều phối” vẫn là tên khu chức năng; chỉ cần UI và bố trí môi trường làm rõ bệnh viện là tòa nhà chứa nó.

### Nhiệm vụ B3 — Điều tra Khu Điều phối trong bệnh viện

**Stage authoritative:** `FindCityMap`  
**TRẠNG THÁI:** logic/state/audio/Journal và marker test đã chạy được; interaction với prop/LootContainer thật vẫn tạm ngưng.

Flow cốt truyện đã thống nhất:

1. **Bàn Điều phối**
   - Đến đúng điểm và giữ `E` khoảng 1,8 giây.
   - UI tiến độ: đang kiểm tra Bàn Điều phối.
   - Hoàn thành phát Cue 07 `DispatchDeskLog`.
   - Nội dung: nhật ký bàn trực dẫn người chơi tới radio và chìa khóa/tủ hồ sơ.
   - Server đánh dấu điểm đã kiểm tra và chuyển objective sang Radio.

2. **Radio**
   - Đến đúng điểm và giữ `E` khoảng 1,8 giây.
   - UI tiến độ: đang khôi phục bản ghi radio.
   - Hoàn thành phát Cue 08 `OfficeRadioRecording`.
   - Nội dung: bản ghi liên lạc cuối cùng và cảnh báo báo động sẽ thu hút nguy hiểm.
   - Server chuyển objective sang Tủ hồ sơ/Bản đồ.

3. **Tủ hồ sơ / Bản đồ quân sự**
   - Đến đúng điểm và giữ `E` khoảng 1,8 giây.
   - UI tiến độ: đang mở tủ hồ sơ.
   - Hoàn thành mở map quân sự, đặt `IsCityMapUnlocked = true`, xóa objective cabinet và chuyển stage sang `CityMapFound`.
   - Phát Cue 09 `MilitaryRouteRevealed`.
   - Sau Cue 09, hiện thẻ phần thưởng `BẢN ĐỒ TUYẾN QUÂN SỰ`; ảnh phần thưởng dùng chính texture raster của bản đồ game, không dùng icon giả.
   - Sau phần thưởng, camera lia tới `KhuVucQuanSu`, map mở vùng căn cứ và marker căn cứ; marker bệnh viện chuyển thành `KHU ĐIỀU PHỐI • HOÀN THÀNH`.
   - Cuối cinematic mới mở bảng đổi mục tiêu theo dõi lần hai.

**Ý nghĩa bảng chọn lần hai:**

- Bảng ghi rõ đây là **cập nhật mục tiêu/waypoint đang theo dõi**, không phải xác nhận ending.
- Chọn Tuyến A hoặc B chỉ đổi Journal, HUD và waypoint đang theo dõi.
- Không khóa ending tại đây.
- Dòng `CHƯA KHÓA ENDING` đã bị xóa; phần mô tả vẫn nói rõ ending chỉ khóa tại hành động cuối.

**Marker tạm hiện có:**

- `[1] BÀN ĐIỀU PHỐI` tại khoảng `(-36.08, 22.56)`.
- `[2] RADIO` tại khoảng `(-43.97, 18.23)`.
- `[3] TỦ HỒ SƠ / BẢN ĐỒ` tại khoảng `(-44.78, 27.27)`.
- Editor marker nằm trong `Assets/Script/Tin/Editor/OfficeMissionPointGizmo.cs`.
- Ảnh tham chiếu: `Captures/hospital-route-b-required-points.png`.

**Vấn đề phải sửa khi loot container được setup:**

- Ba vị trí trên chỉ là tham chiếu tạm và hiện tạo đường zigzag chưa hợp lý.
- Runtime hiện suy thứ tự theo tọa độ: điểm có X lớn nhất → Y thấp nhất → Y cao nhất. Cách này dễ hỏng khi di chuyển prop.
- Khi tiếp tục, phải thay bằng role/ID explicit, ví dụ `DispatchDesk`, `Radio`, `RecordsCabinet` hoặc danh sách serialized cố định.
- Bố trí nên thành một cụm Điều phối có quan hệ không gian rõ ràng, không phải ba marker ngẫu nhiên.
- Chưa gắn loot container thật, chưa chốt chìa khóa, chưa chốt model bàn/radio/tủ.
- Wiring hoàn thành bệnh viện hiện đã là `Cue 09 → reward bản đồ thật → cinematic căn cứ → bảng đổi mục tiêu theo dõi`.

### Nhiệm vụ B4 — Đi tới căn cứ quân sự

**Main stage:** `CityMapFound`  
**Military phase:** `NotReached → Investigating`

1. Bản đồ và marker quân sự được mở.
2. Chỉ bản đồ nhiệm vụ toàn màn hình được mở khóa; minimap góc phải luôn giữ tắt.
3. HUD/Journal đổi thành đi tới khu quân sự nếu người chơi đang theo dõi Tuyến B; marker bệnh viện không còn là mục tiêu active.
4. Khi một người chơi sống tiến vào bán kính khoảng 7 đơn vị quanh xe quân sự, State Authority chuyển phase thành `Investigating`.
5. Client tiếp cận nghe Cue 10 `MilitaryBaseApproach`.
6. Người chơi vẫn có thể quay lại làm Tuyến A vì chưa xác nhận điểm không thể quay lại.

### Nhiệm vụ B5 — Điểm không thể quay lại

**Military phase:** `Investigating`

1. Người chơi đến gần xe quân sự và bấm `E` để kiểm tra.
2. Phát Cue 11 `AlarmPointOfNoReturn`.
3. Sau audio, mở bảng xác nhận cuối:
   - Xác nhận Tuyến B: kích hoạt báo động và cuộc phòng thủ.
   - Hủy/đóng: chưa khóa gì, người chơi có thể rời đi.
4. Khi xác nhận, request đi tới State Authority.
5. Server kiểm tra stage `CityMapFound`, vị trí người chơi và quy tắc khóa.
6. `LockedEscapeRoute` được đặt thành `MilitaryEvacuation` cho toàn đội.
7. Tuyến A bị khóa; không thể bắt đầu finale bằng xe dân sự.
8. Military phase chuyển sang `SiegeAndRepair`.
9. Phát Cue 12 `SiegeStarted`, đóng cổng và bắt đầu horde.

Đây là **điểm mấu chốt khóa Ending B**, không phải bảng chọn đầu game hoặc bảng chọn sau bệnh viện.

### Nhiệm vụ B6 — Phòng thủ và chuẩn bị xe sơ tán

**Military phase:** `SiegeAndRepair`

Các việc có thể chia cho nhiều người chơi:

1. **Khởi động máy phát điện**
   - Tương tác tại điểm Generator.
   - Tăng sức chịu đựng tối đa của cổng và giữ tỉ lệ máu cổng hiện tại.
   - Phát Cue 13 `GeneratorOnline`.

2. **Tìm ba vật phẩm xe sơ tán hiện tại**
   - Military Battery.
   - Fuel Canister.
   - Repair Kit.
   - Mỗi cache chỉ được claim một lần authoritative.

3. **Lắp vật phẩm vào xe**
   - Người mang item đến gần xe quân sự và bấm `E`.
   - Item bị tiêu thụ khi server xác nhận lắp thành công.

4. **Loot hỗ trợ tùy chọn**
   - Két an toàn văn phòng quân sự cho Armory Key, S12K và đạn nếu map quân sự đã được mở.
   - Armory dùng key để mở AK47, S12K, đạn và backpack cấp 3.

5. **Sửa xe**
   - Code finale cũ hiện yêu cầu đủ ba vật phẩm rồi giữ `E` để tăng `VehicleRepairProgress`.
   - Khi đạt 100%, phase chuyển thành `ReadyToEscape` và phát Cue 14 `EscapeVehicleReady` cho người hoàn thành.
   - Zombie liên tục đánh cổng; nếu cổng vỡ, horde chuyển mục tiêu sang đội sống sót.
   - Nếu toàn bộ người chơi chết trong `SiegeAndRepair` hoặc `ReadyToEscape`, phase chuyển `Failed`.

**Lưu ý kiến trúc quan trọng:**

- Hệ thống sửa xe kiểu Dead by Daylight 5 hạng mục hiện nằm trong `MilitaryBaseQuestManager` nhưng đang được dùng như **roadside police-car gameplay test**.
- Nó có 5 hạng mục: động cơ, nắp capo, nhiên liệu, ắc quy, lốp; mỗi hạng mục có tiến độ riêng, skill-check 4–7 giây, Success/Perfect/Miss và không sinh skill-check từ 95% trở lên.
- Trong minigame, chỉ người sửa bị khóa input; đồng đội vẫn hoạt động. State Authority quyết định tiến độ và chỉ một người sửa được tại một thời điểm.
- Finale căn cứ đang chạy mechanic cũ ba vật phẩm + giữ `E`; chưa được hợp nhất hoàn toàn với minigame 5 hạng mục. Chat sau không được mặc định rằng việc tích hợp này đã hoàn tất.

### Nhiệm vụ B7 — Sơ tán và Ending B

**Military phase:** `ReadyToEscape → Escaped`

1. Một người chơi đến gần xe đã sẵn sàng và bấm `E`.
2. Server xác nhận Ending B đang bị khóa và player ở đúng vị trí.
3. Tất cả người chơi còn sống được tập hợp cho extraction.
4. Phase chuyển sang `Escaped`.
5. Phát Cue 15 `MilitaryEvacuationComplete` cho người kích hoạt.
6. Horde dừng.
7. Xe chạy tới exit bằng cutscene khoảng 2,25 giây.
8. Hiện `VictorySummaryUI` với route `MilitaryEvacuation` và thời gian sống sót authoritative.

## 4. Audio và UI hội thoại

15 file nằm tại:

`Assets/Resources/Sound/Story/RouteB/`

Catalog canonical:

`Assets/Script/Tin/Prototype/RouteBAudioContent.cs`

Nguồn lời thoại đầy đủ:

`Assets/Resources/Story/RouteB/README_ROUTE_B_AUDIO_SOURCE.md`

Quy tắc runtime:

- Khi voice phát trên một client, game audio của chính client đó duck xuống 18%.
- Bảng thoại nằm dưới màn hình, ngay phía trên hotbar và chỉ hiện tên người nói, không hiện tên tuyến.
- HUD/Canvas khác bị ẩn tạm; shortcut UI bị khóa; player của client đó gửi network input rỗng.
- Không dùng `Time.timeScale`; những người chơi khác trong multiplayer vẫn di chuyển, chiến đấu và mở UI bình thường.
- Hết audio hoặc skip thì phục hồi đúng volume, Canvas và input trước đó.
- Nếu MP3 thiếu, flow vẫn chạy bằng subtitle và radio-static fallback.
- Text Nhật ký, lựa chọn tuyến, thông báo và subtitle hỗ trợ đổi Việt/Anh theo từng client.

## 5. State authoritative và late join

`MainQuestManager` giữ phần trước căn cứ:

- `NetworkQuestStage`
- danh sách nhà tìm kiếm
- `RouteClueMask`
- `InsuredRouteClueMask`
- `IsOfficeDiscovered`
- `CheckedCabinetMask`
- `IsCityMapUnlocked`
- `LockedEscapeRouteValue`

`MilitaryBaseQuestManager` giữ phần căn cứ:

- `MilitaryPhase`
- máu cổng
- máy phát/armory/két
- các cache và item đã lắp
- tiến độ sửa xe
- active repairer
- trạng thái siege và thời gian sống sót

Client chỉ gửi request tương tác. State Authority xác thực stage, khoảng cách, inventory và cập nhật state. UI lấy snapshot authoritative nên late join phải thấy đúng stage và tiến độ hiện tại; audio/cinematic đã phát trước khi late join không nhất thiết phát lại.

## 6. Đường test Tuyến B không dùng LootContainer

Chỉ khả dụng trong Unity Editor hoặc Development Build, và chỉ Solo/Host có State Authority được phép chạy:

| Phím/CheatMenu | Tác dụng |
| --- | --- |
| `F6` | Tiến một beat ở nửa đầu Tuyến B: kiểm tra xe → từng tài liệu → tới bệnh viện → từng điểm điều tra |
| `F7` | Hoàn tất ngay `3/3` tài liệu khu dân cư bằng state authoritative, không sinh/đọc/sửa LootContainer |
| `F10` | Tiến một beat ở căn cứ: tiếp cận → cảnh báo/xác nhận finale → máy phát → mô phỏng đủ ba part → xe sẵn sàng → extraction |
| `F11` | Phát lại audio phù hợp với stage/phase Tuyến B hiện tại |
| `F12` | Dịch chuyển Host/Solo tới điểm tương tác hiện tại; từ chối các objective liên quan LootContainer/cache loot |
| `P` | Mở CheatMenu; năm thao tác trên nằm trong nhóm `ROUTE B FLOW TEST — NO LOOT CONTAINERS` |

Quy trình test nhanh đã chốt:

1. Luôn vào game bằng `MainMenu → Solo → Easy → Main` để player và Fusion runner spawn đúng.
2. Nhấn `F6` để mô phỏng kiểm tra xe; nghe/skip Cue 01–02 rồi chọn tuyến đang theo dõi.
3. Dùng `F6` ba lần để xem từng tài liệu, hoặc `F7` để đưa thẳng tiến độ lên 3/3. Cả hai cách đều không chạm LootContainer trong đường test.
4. Sau Cue 05, nhấn `F6` để tới Khu Điều phối; tiếp tục `F6` theo thứ tự Bàn Điều phối → Radio → Tủ hồ sơ. Chờ audio kết thúc hoặc nhấn `E` để skip trước lần nhấn tiếp theo.
5. Sau Cue 09, kiểm tra thẻ thưởng dùng ảnh giấy rách `Mảnh bản đồ 2`, sau đó map tự mở và reveal riêng vùng căn cứ quân sự. Khi map đóng mới chạy cinematic lia tới căn cứ và bảng đổi waypoint lần hai; dùng `F12` để tới căn cứ hoặc `F10` để mô phỏng phase tiếp cận.
6. `F10` lần kế tiếp phát Cue 11 rồi mở bảng xác nhận. Ending chỉ khóa khi click **XÁC NHẬN TUYẾN B**.
7. Sau khi xác nhận, tiếp tục `F10`: máy phát → mô phỏng đủ part → xe sẵn sàng → extraction/Cue 15/Victory Summary.

Debug path gọi chung các hàm core authoritative với gameplay thật. Chỉ phần không có LootContainer mới được mô phỏng; xác nhận điểm không thể quay lại và khóa ending không bị bỏ qua.

## 7. Trạng thái triển khai hiện tại

| Hạng mục | Trạng thái |
| --- | --- |
| Kiểm tra xe, Cue 01–02, bảng chọn lần 1 | Đã triển khai |
| Chọn và đồng bộ khu dân cư | Đã triển khai |
| Loot 3 tài liệu trong nhà + pity/insurance | Đã triển khai |
| Ranh giới sương, cảnh báo, trả player sau 2 giây | Đã triển khai cho nhiệm vụ khu dân cư |
| Cue 03–05, map fragment 1, cập nhật journal | Đã triển khai; cần regression test khi sửa tiếp |
| Bệnh viện chứa Khu Điều phối | Đã chốt; Nhật ký, HUD, bản đồ và bảng chọn đã đổi sang thuật ngữ bệnh viện/Khu Điều phối |
| Trigger vào Khu Điều phối | Có code và marker tạm |
| Bàn Điều phối → Radio → Tủ hồ sơ | Logic/state/audio/Journal chạy được bằng marker/debug; prop và LootContainer thật vẫn chờ setup |
| Cue 06–09 và bảng chọn lần 2 | Đã nối và được test trong đường end-to-end |
| Reward Mảnh bản đồ 2 + map reveal + cinematic | Đã nối theo thứ tự sau Cue 09; reward dùng cùng art giấy rách với Mảnh 1, map thật chỉ xuất hiện trong màn hình bản đồ |
| Marker bệnh viện sau Mảnh 2 | Đã xóa khỏi map; chỉ còn marker căn cứ quân sự để tránh hai mục tiêu cạnh tranh |
| Minimap sau bệnh viện | Đã tách khỏi map unlock; giữ tắt |
| Điểm không thể quay lại và khóa Ending B | Có code authoritative |
| Siege, generator, cache, armory, extraction | Có prototype/runtime code; debug path chỉ mô phỏng part còn thiếu, không tạo LootContainer |
| Journal phần căn cứ | Đã theo dõi `NotReached → Investigating → SiegeAndRepair → ReadyToEscape → Escaped/Failed`; chỉ chuyển Completed sau extraction |
| CheatMenu/phím test không loot | Đã triển khai `F6/F7/F10/F11/F12` và nhóm nút riêng |
| Test tự động toàn Tuyến B | Đã qua MainMenu → Main → Ending B không LootContainer |
| Minigame sửa xe 5 hạng mục tích hợp vào finale căn cứ | Chưa hoàn tất; hiện là roadside police-car test |
| Loot dùng chung Tuyến A/B trong khu bệnh viện/văn phòng | Chưa làm; chờ setup môi trường |

## 8. Việc cần làm khi tiếp tục

Chỉ bắt đầu sau khi chủ dự án xác nhận loot container bệnh viện đã được setup:

1. Kiểm kê container/prop thực tế trong bệnh viện và chụp Scene View.
2. Chọn một cánh làm Khu Điều phối; đặt biển chỉ dẫn từ sảnh bệnh viện.
3. Gán role explicit cho Bàn Điều phối, Radio và Tủ hồ sơ; bỏ cách suy thứ tự bằng tọa độ.
4. Gắn interaction quest vào prop/container thật mà không làm thay đổi lời thoại 06–09.
5. Giữ localization hiện đã đồng bộ: “Tìm Khu Điều phối bên trong bệnh viện được đánh dấu”.
6. Kiểm tra map reveal: khu dân cư tối bên ngoài; sau 3/3 chỉ mở hành lang tới bệnh viện; sau tủ hồ sơ map tự mở, tập trung camera và mở sáng riêng vùng căn cứ. Không mở full map vì sẽ làm mất giá trị khám phá/phần thưởng bản đồ sau này.
7. Kiểm tra journal/HUD cập nhật cùng frame với authoritative stage.
8. Kiểm tra bảng chọn lần hai sau Cue 09 và đảm bảo vẫn chưa khóa ending.
9. Quyết định finale căn cứ dùng mechanic ba vật phẩm cũ hay tích hợp minigame sửa xe 5 hạng mục.
10. Test tuần tự bắt buộc: `MainMenu → Main`, Solo → Host/Client → late join.

## 9. Kết quả và checklist test end-to-end

Kết quả tự động ngày 2026-08-24:

- EditMode `QuestFlowUIPrototypeTests`: **41/41 passed**; đã bao gồm test Mảnh 2 dùng art giấy rách, marker căn cứ thay thế marker bệnh viện và raster map không bị dùng sai làm reward.
- PlayMode `RouteBDebugFlowRunsFromMainMenuThroughMilitaryExtractionWithoutLootContainers`: **1/1 passed**, khoảng 10 giây.
- Test này thực sự đi `MainMenu → Main`, spawn player/Fusion, chạy đủ stage bệnh viện, khóa Ending B tại xác nhận, chạy căn cứ và chỉ đánh dấu Journal Completed sau extraction.
- Test cũng xác nhận sau `CityMapFound`: marker căn cứ xuất hiện trên bản đồ nhiệm vụ và Canvas minimap vẫn tắt.
- Khi chạy chung cả class PlayMode, test cũ `RoadsideRepairTestStationSpawnsOnLockedPoliceCarNearArrival` vẫn báo không tìm thấy manager/station ở lần chạy đó. Đây là regression test riêng của xe cảnh sát/DBD, không phải lỗi assertion trong test Tuyến B mới; cần điều tra riêng, không được ghi nhận là đã pass.

- [ ] Kiểm tra xe chỉ kích hoạt một lần.
- [ ] Cue 01–02 đúng thứ tự; bảng chọn đầu không khóa ending.
- [ ] Host chọn cùng khu nhà cho mọi client.
- [ ] Chỉ lấy item manh mối mới tăng tiến độ.
- [ ] Pity không soft-lock và không sinh trùng tài liệu.
- [ ] Ra ngoài vùng khu dân cư bị sương/blackout và được trả về sau 2 giây, lặp lại được nhiều lần.
- [ ] Sau 3/3, journal chuyển ngay sang bệnh viện và ranh giới khu dân cư tắt.
- [x] Fragment 2 mở marker căn cứ và giữ minimap tắt trong test MainMenu → Main.
- [x] Reward Fragment 2 dùng cùng art giấy rách với Fragment 1; raster map chỉ dùng trong màn hình bản đồ.
- [x] Sau Fragment 2, marker bệnh viện bị gỡ và chỉ marker căn cứ còn hoạt động.
- [ ] Bản đồ chỉ lộ đúng vùng được phép theo từng stage khi quan sát trực tiếp trên nhiều độ phân giải.
- [ ] Trigger bệnh viện không chạy sớm và chỉ chạy khi player thật sự ở trong collider.
- [ ] Ba tương tác bệnh viện buộc đúng thứ tự.
- [ ] Cue 06–09 và notification không chồng UI.
- [ ] Bảng chọn lần hai không khóa ending.
- [ ] Tiếp cận căn cứ phát Cue 10 một lần.
- [ ] Cue 11 xuất hiện trước bảng xác nhận điểm không thể quay lại.
- [ ] Chỉ nút xác nhận cuối mới khóa Ending B cho toàn đội.
- [ ] Siege/generator/cache/install/repair đồng bộ Host và Client.
- [ ] Chỉ active repairer bị khóa input; đồng đội không bị khóa.
- [ ] Late join nhận đúng stage, item mask, phase, máu cổng và tiến độ sửa.
- [ ] Cue 14 chỉ phát khi xe sẵn sàng; Cue 15 và Victory Summary xuất hiện khi extraction hoàn tất.

## 10. File chính cần đọc trong chat mới

- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/PreMilitaryQuestRuntimeBridge.cs`
- `Assets/Script/Tin/MainQuest/MainQuestStartTrigger.cs`
- `Assets/Script/Tin/MainQuest/MainQuestSearchCabinet.cs`
- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- `Assets/Script/Tin/MainQuest/MilitaryEscapeVehicleRepair.cs`
- `Assets/Script/Tin/MainQuest/EscapeRouteDecisionUI.cs`
- `Assets/Script/Tin/DevCheatManager.cs`
- `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs`
- `Assets/Script/Tin/Prototype/PreMilitaryQuestProgress.cs`
- `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs`
- `Assets/Script/Tin/Prototype/QuestMapUIPrototype.cs`
- `Assets/Script/Tin/Prototype/RouteBAudioContent.cs`
- `Assets/Script/Tin/GameLocalization.cs`
- `README_MAINPLAY_CODEX_HANDOFF.md`

Ảnh Game View thật để chốt vị trí prompt `GIỮ [E]`:

- `Captures/hold-e-option-a-above-hotbar.png` — cố định ngay trên hotbar.
- `Captures/hold-e-option-b-world-target.png` — bám mục tiêu trong thế giới, trực quan nhưng dễ phải clamp/nhảy vị trí.
- `Captures/hold-e-option-c-top-objective.png` — cố định dưới vùng objective ở phía trên.

Chủ dự án đã chọn C. Production hiện dùng thẻ tương tác riêng ở dưới vùng objective: keycap `[E]` màu cam, eyebrow `TƯƠNG TÁC • GIỮ PHÍM`, hành động ở dòng hai, nền xanh-đen cùng sọc cam bên trái. Nó không dùng cấu trúc tiêu đề/nội dung của mission toast nên người chơi dễ phân biệt. Ảnh kiểm tra thực tế: `Captures/hold-e-option-c-improved-interaction-card.png`. Quy tắc ẩn prompt khi hội thoại, bảng chọn, Journal, map hoặc UI khác mở tiếp tục được giữ bằng `LocalGameplayUIState.BlocksWorldInteractionHints`.

## 11. Câu lệnh bàn giao gợi ý cho chat Codex mới

> Hãy đọc toàn bộ `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` và `README_MAINPLAY_CODEX_HANDOFF.md`, sau đó kiểm tra code hiện tại trước khi sửa. Tuyến B đã có đường state/Journal/audio/test từ MainMenu tới Ending B bằng F6/F7/F10/F11 mà không dùng LootContainer. Chỉ triển khai gameplay loot/prop thật khi tôi xác nhận LootContainer bệnh viện/căn cứ đã setup; giữ nguyên quyết định rằng hai bảng chọn đầu chỉ theo dõi và Ending B chỉ khóa tại xác nhận kích hoạt báo động ở căn cứ.
