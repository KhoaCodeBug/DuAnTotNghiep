# ROUTE B — COMPLETE FLOW & CODEX HANDOFF

> **TRẠNG THÁI TÀI LIỆU:** hồ sơ thiết kế/triển khai lịch sử tới 2026-08-26. Nguồn bàn giao hiện tại là `CODEX_PROJECT_WORK_LOG.md`; nếu có mâu thuẫn, entry mới hơn trong work log và repository hiện tại được ưu tiên. Phần 12 ở cuối file ghi các thay đổi lớn đã thay thế B6/B7 cũ.

> Cập nhật: 2026-08-25
> Mục đích lịch sử: lưu thiết kế chi tiết Route B để tra cứu mà không làm mất các quyết định cũ.
> Snapshot tại thời điểm 2026-08-25: **State machine, Nhật ký, lựa chọn ending và đường test Tuyến B đã chạy được từ MainMenu tới Ending B; H1–H5 của chương bệnh viện đã triển khai. Đây không phải trạng thái repository mới nhất.**

## 1. Quyết định thiết kế đã chốt

- Game có hai tuyến thoát hiểm ngang hàng:
  - **Tuyến A — Khôi phục chiếc xe:** tự sửa xe dân sự, khám phá đường thoát và vượt vòng phong tỏa.
  - **Tuyến B — Lần theo tín hiệu quân sự:** tìm hồ sơ, tới Khu Điều phối, xác định căn cứ quân sự, phòng thủ và dùng xe sơ tán để thoát.
- Người chơi được biết cả hai tuyến ngay sau khi kiểm tra chiếc xe hỏng đầu game.
- Bảng chọn đầu tiên và bảng chọn sau bệnh viện **chỉ đổi tuyến đang theo dõi**, không khóa ending.
- Người chơi có thể chuẩn bị cả hai tuyến song song trước điểm không thể quay lại.
- Ending B chỉ bị khóa khi người chơi tới xe quân sự, nghe cảnh báo và **xác nhận kích hoạt báo động/phòng thủ**. Việc khóa áp dụng cho toàn đội.
- Bệnh viện là địa điểm chính. Quầy tiếp tân và văn phòng trưởng ca nằm trong bệnh viện; **Trạm liên lạc phụ trợ nằm phía sau, ngoài khu nhà chính nhưng thuộc cùng cụm `hospital`**.
- Chương bệnh viện mới không phụ thuộc LootContainer: `ShiftLog tại tiếp tân → ShiftLog2 tại văn phòng → một trong các KeyLoot được Host chọn → shared key → mở phòng Radio → khôi phục tín hiệu → nhận Mảnh bản đồ 2`.
- Không còn bước `RecordsCabinet`/Tủ hồ sơ riêng. Radio trao trực tiếp tọa độ và Mảnh bản đồ 2.
- Cốt truyện đã chốt: quân đội phong tỏa bệnh viện và rút đoàn xe về căn cứ; beacon còn phát nhưng không chứng minh quân đội còn sống. Player tới căn cứ để lấy phương tiện tự thoát, không phải chờ cứu viện.
- Thiết kế chi tiết, anchor, lời Radio, multiplayer và tiêu chí chấp nhận nằm trong `HOSPITAL_ROUTE_DESIGN_LOCK.md`.

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
Mở vị trí bệnh viện trên bản đồ
    ↓
Tới quầy tiếp tân → đọc ShiftLog
    ↓
Kiểm tra văn phòng trưởng ca (ShiftLog2) → tìm KeyLoot ngẫu nhiên → nhận chìa khóa shared
    ↓
Theo waypoint ra Trạm liên lạc phụ trợ → mở cửa
    ↓
Khôi phục Radio trong lúc noise thu hút zombie
    ↓
Bản ghi cầu cứu + lệnh phong tỏa → tọa độ/Mảnh bản đồ 2
    ↓
Cue 05–09 được viết lại → map reveal → cinematic căn cứ → bảng đổi mục tiêu theo dõi lần 2
    ↓
Vào trường học trong khu quân sự, tự do khám phá và kiểm tra 3 manh mối quest-state
    ↓
Đủ 3/3 rồi rời __SchoolRoofTrigger_FIXED → mở waypoint Car cảnh sát
    ↓
Giữ E kiểm tra Car → vote nhất trí toàn phòng tại điểm không thể quay lại
    ↓
Toàn đội đồng ý → khóa Ending B → cinematic Host/Car → đóng cổng
    ↓
Gom đội vào trong cổng → horde từ 4 điểm spawn → bảo vệ cổng và sửa Car 5 hạng mục
    ↓
Hoàn tất đủ 5 hạng mục → Cue 14
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
6. Server đặt trạng thái đã tới bệnh viện và chuyển objective sang `HospitalQuest_ShiftLog` tại quầy tiếp tân.
7. UI canonical phải nói rõ đây là bệnh viện và đầu mối liên lạc, không dùng lại câu `Tìm văn phòng màu tím trong khu vực đã xác định`.

**Yêu cầu triển khai mới:**

- Không còn phụ thuộc việc tìm đủ ba `MainQuestSearchCabinet` theo tọa độ trong bệnh viện.
- Cue 05–06 phải được viết lại để nêu bệnh viện từng giữ trạm liên lạc dự phòng, nhưng không khẳng định quân đội còn sống.
- Người chơi có thể đi thẳng tới cửa Radio trước. Khi đó chỉ nhận thông báo cửa khóa và chỉ dẫn tìm chìa tại văn phòng trưởng ca; không được soft-lock.
- Bảng chọn tuyến lần hai vẫn chỉ đổi mục tiêu đang theo dõi và chưa khóa ending.

### Nhiệm vụ B3 — Điều tra Khu Điều phối trong bệnh viện

**Stage authoritative:** `FindCityMap`
**TRẠNG THÁI:** H1–H5 đã triển khai và có regression tự động tới Ending B; còn acceptance Host/Client thực trên hai máy.

Flow canonical mới:

1. **ShiftLog tại quầy tiếp tân**
   - Anchor: `HospitalQuest_ShiftLog`.
   - Cho biết liên lạc đã chuyển sang Trạm phụ trợ phía sau bệnh viện.
   - Dẫn tới văn phòng trưởng ca ngay sau quầy tiếp tân.
   - Không trao chìa khóa.

2. **Văn phòng trưởng ca / ShiftLog2**
   - Anchor: `HospitalQuest_ShiftLog2`.
   - Không trao key trực tiếp. Host chọn ngẫu nhiên một trong các `HospitalRadioKeyLootPoint` và replicate stable ID.
   - Tiết lộ lệnh phong tỏa đỏ và việc nhân viên Radio bị cắn, tự khóa mình tại trạm phụ trợ.
   - Journal/waypoint dẫn tới đúng KeyLoot được chọn; chỉ Polygon của điểm đó nhận tương tác.
   - Nhặt key mới tạo quest state shared, không chiếm inventory; sau đó waypoint mới chuyển tới Radio.

3. **Mở phòng Radio**
   - Root: `HospitalQuest_RadioRoom`.
   - `DoorInteraction` chỉ hoạt động khi cửa đóng; `RadioInteraction` bị vô hiệu tuyệt đối.
   - Sau khi Host xác nhận chìa khóa và khoảng cách, cửa mở đồng bộ, collider cửa tắt, Door interaction tắt và Radio mới bật.
   - Mỗi ShiftLog/KeyLoot/Door/Radio có `InteractionZone` Polygon riêng; prompt và server dùng cùng `OverlapPoint`.

4. **Khôi phục Radio**
   - Một Player giữ `E` tổng cộng khoảng 14 giây, chia ba chặng vàng; thả/rời vùng thì giữ tiến độ, người khác có thể tiếp tục.
   - Chỉ một người vận hành tại một thời điểm; đồng đội không bị khóa gameplay.
   - Chặng 1 và 2 tự dừng, phát nhiễu 2,7 giây và nhả operator. Mỗi chặng sinh tại cả A/B: Dễ 3, Thường 4, Hardcore 5 zombie mỗi điểm; nhịp 0,25 giây, trải đều trái/phải.
   - Không có kill gate. Chặng 3 không spawn thêm; tổng H4 là 12/16/20 tùy độ khó.
   - Bản ghi cho thấy bệnh viện bị quân đội bỏ theo lệnh phong tỏa, đoàn xe đã rút về căn cứ và beacon `BRAVO–BẮC / CỔNG NAM` vẫn phát tự động.

5. **Nhận tọa độ và Mảnh bản đồ 2**
   - Tọa độ/Mảnh bản đồ 2 được khôi phục trực tiếp từ console Radio; không có Records Cabinet.
   - Server đặt `IsCityMapUnlocked = true` và chuyển stage sang `CityMapFound`.
   - Map tự mở, reveal riêng căn cứ, giữ minimap tắt; marker bệnh viện bị gỡ sau khi hoàn thành.
   - Sau map reveal mới chạy cinematic căn cứ và bảng đổi mục tiêu theo dõi lần hai.
   - Player tới căn cứ để lấy phương tiện tự thoát, không phải vì tin quân đội chắc chắn còn sống.

**Ý nghĩa bảng chọn lần hai:**

- Bảng ghi rõ đây là **cập nhật mục tiêu/waypoint đang theo dõi**, không phải xác nhận ending.
- Chọn Tuyến A hoặc B chỉ đổi Journal, HUD và waypoint đang theo dõi.
- Không khóa ending tại đây.
- Dòng `CHƯA KHÓA ENDING` đã bị xóa; phần mô tả vẫn nói rõ ending chỉ khóa tại hành động cuối.

**Anchor và đường đi đã xác nhận:**

- `ShiftLog → ShiftLog2` khoảng 3,2 world-unit.
- `ShiftLog2 → RadioRoom` khoảng 19,8 world-unit; phải có waypoint và chỉ dẫn “Trạm liên lạc phụ trợ phía sau bệnh viện”.
- `DoorInteraction → RadioInteraction` khoảng 1,76 world-unit; dùng vùng nhỏ và state gating.
- Chủ dự án xác nhận đường từ văn phòng ra phòng Radio có thể đi liền mạch, không bị collider/tilemap chặn.
- Hai điểm zombie dự phòng đã có ở hai phía bệnh viện.
- Chi tiết đầy đủ và nội dung Radio canonical: `HOSPITAL_ROUTE_DESIGN_LOCK.md`.

### Nhiệm vụ B4 — Đi tới căn cứ quân sự

**Main stage:** `CityMapFound`  
**Military phase:** `NotReached → Investigating`

1. Bản đồ và marker quân sự được mở.
2. Chỉ bản đồ nhiệm vụ toàn màn hình được mở khóa; minimap góc phải luôn giữ tắt.
3. Vào trường học không tự kích hoạt event. Ba điểm hồ sơ runtime là state nhiệm vụ, không thêm item vào inventory.
4. Sau khi đủ `3/3`, một Player phải thật sự rời trigger `__SchoolRoofTrigger_FIXED`.
5. State Authority chuyển phase sang `Investigating` và mở waypoint trên `Car` cảnh sát.
6. Người chơi vẫn có thể quay lại Tuyến A vì chưa có vote khóa tuyến.

### Nhiệm vụ B5 — Điểm không thể quay lại

**Military phase:** `Investigating`

1. Người chơi đứng trước `Car`, giữ `E` để kiểm tra. `Car` là xe hoàn thiện có sẵn trong scene và không còn bị chuyển tới `ViTriXeTest`.
2. State Authority chụp snapshot toàn bộ `Runner.ActivePlayers`; bất kỳ Player nào cũng được khởi tạo vote.
3. Vote yêu cầu nhất trí: đủ tất cả phiếu đồng ý mới khóa Tuyến B. Một phiếu từ chối/ESC hủy vote; xe được tương tác lại.
4. Người disconnect bị loại khỏi snapshot; late join không được thêm vào vote đang diễn ra.
5. Khi đủ phiếu, `LockedEscapeRoute = MilitaryEvacuation`; sau đó mới chạy cinematic.
6. Khi cinematic bắt đầu, Host dọn toàn bộ zombie nằm trong PolygonCollider2D `KhuVucQuanSu`. Bản sao visual Host xuất phát từ một điểm hợp lệ **bên trong vùng, gần Car**; Host thật bị ẩn bằng `forceRenderingOff` + nametag Canvas + Light2D/PlayerVision suppression. Clone kế thừa Fog/vision/flashlight, chỉ dùng một nguồn footstep, và dùng đúng Animator + `walkSpeed/runSpeed` thật để đi tới xe → đề thất bại → còi liên tục → thoại → chạy tới `ViTriDongCong`.
7. Trong fade đen, sáu tile hàng rào do scene author quanh `CongRao` hiện lên và collider runtime chặn cả Player/Zombie; không sinh hình hàng rào màu hoặc thay asset. Tất cả Player sống được tập hợp ở phía trong cổng.
8. Cinematic kết thúc mới chuyển sang `SiegeAndRepair` và bắt đầu horde.

Đây là **điểm mấu chốt khóa Ending B**, không phải bảng chọn đầu game hoặc bảng chọn sau bệnh viện.

### Nhiệm vụ B6 — Phòng thủ và chuẩn bị xe sơ tán

**Military phase:** `SiegeAndRepair`

1. Horde dùng đúng bốn marker `ViTriSpawnZombie*`, kiểm tra mỗi `5 giây`.
2. Solo: `2` zombie/điểm = `8` mỗi batch, tạm dừng khi có `24+` zombie siege gần cổng. Cả bốn điểm spawn cách cổng tối thiểu `18m` ở runtime; zombie ambient cũ trong `7,5m` sát cổng được dọn trước khi wave bắt đầu.
3. Từ hai Player: `4` zombie/điểm = `16` mỗi batch, tạm dừng khi có `50+` gần cổng. Hard safety cap lần lượt `36/72` tránh spawn mất kiểm soát.
4. Zombie siege dùng đúng chase speed của từng AI prefab (`ZombieAI`: `moveSpeed × chaseSpeedMultiplier`; hai AI Khoa: `speed`), không dùng tốc độ cinematic tùy ý. Zombie có sẵn gần cổng cũng được chuyển sang objective công thành; mục tiêu được rải trên 13 lane dọc cổng thay vì chồng tại một điểm. Khi cổng vỡ, các AI gốc được trả lại để săn Player.
5. Không có Generator, không tăng `150% HP`, không điện giật/làm choáng zombie. Các đường gọi prototype Generator đã bị vô hiệu hóa.
6. Multi giữ cổng `5.000 HP`, damage theo nhịp attack `12 HP/hit`, tối đa `4 hit/giây` toàn cổng. Solo dùng pool `8.640`: chỉ khi zombie đánh cổng lần đầu mới bắt đầu DPS authority đều, vỡ sau đúng `180 giây`. Thanh máu lớn nằm phía trên hotbar. Collider nằm trên layer `Obstacle`, cập nhật A* ngay khi đóng/mở nhưng được Player Fog/LOS bỏ qua để nhìn xuyên hàng rào.
7. Multi dùng checkpoint quanh `Car`: team chung `3` lượt hồi sinh tự động sau `10 giây`; mode Multi được khóa lúc siege bắt đầu, spawn fail không mất lượt, inventory/hotbar và ammo đang dùng được snapshot/restore. Solo chết một lần là Failed; cả đội Multi chết cùng lúc cũng Failed.
8. `Car` dùng trực tiếp minigame năm hạng mục: động cơ, capo, nhiên liệu, ắc quy và lốp. State/progress authoritative, chỉ một người sửa, tiến độ từng hạng mục được giữ khi rời.
9. Đủ `5/5` thì mở khóa khả năng lái sẵn có của `Car` và chuyển `ReadyToEscape`.
10. **Nguồn loot cho năm hạng mục đã triển khai chính thức:** khi authority chuyển sang `SiegeAndRepair`, đúng năm prefab Fusion `MilitaryRepairLootContainer` được sinh tại năm marker author ID `1..5` trong `Main.unity`. Manifest random theo seed nhưng luôn có đủ Toolbox, Hammer, FuelCan, Battery và Tire của `PoliceCarItemCatalog`; mỗi tủ kèm AK47/Ammo762 hoặc S12K/Ammo12Gauge. Tủ tái sử dụng UI/giao dịch `LootContainer`, chỉ khả dụng trong siege, highlight đỏ toàn sprite và server kiểm tra PlayerRef/phase/khoảng cách/vật cản/inventory/slot. Setup thiếu dữ liệu sẽ rollback và retry, không công bố trạng thái một phần. Bản Ox Alpha cũ vẫn bị discard; bản này không dùng runtime-placement quanh xe/cổng và không dùng fallback Editor.
11. QA tự động 2026-08-26: build Assembly-CSharp `0 error`; loot EditMode `4/4`; PlayMode full MainMenu → cinematic → siege → năm tủ → inventory-full → chống double-claim → sửa `5/5` → extraction `1/1` pass (`35,34s`); test bảo toàn vị trí `Car` authored `1/1` pass (`7,39s`). Acceptance Host + Client hai máy và kiểm tra tay lối đi/collider tại marker vẫn còn mở.
12. Hotfix UI cinematic: `SiegeStarted` được phát sau khi `MilitaryRouteCinematicController` trả `AutoCanvas` và input về gameplay. Không phát radio trong lúc canvas đang bị cinematic khóa, tránh radio ghi nhớ rồi khôi phục inventory/loot canvas ở trạng thái disabled.

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
| `F6` | Tiến flow: kiểm tra xe → từng tài liệu → ShiftLog → ShiftLog2/chọn KeyLoot → nhặt shared key → Door/RadioReady. Tại RadioReady, ba lần tiếp theo chạy chặng 1, 2, 3; hai lần đầu kích hoạt H4 |
| `F7` | Hoàn tất ngay `3/3` tài liệu khu dân cư bằng state authoritative, không sinh/đọc/sửa LootContainer |
| `F10` | Tiến một beat ở căn cứ: tiếp cận → cảnh báo/xác nhận finale → máy phát → mô phỏng đủ ba part → xe sẵn sàng → extraction |
| `F11` | Phát lại audio phù hợp với stage/phase Tuyến B hiện tại |
| `F12` | Dịch chuyển Host/Solo tới điểm tương tác hiện tại; tại `LocateOffice` ưu tiên marker `TeleportToHospital`; từ chối các objective liên quan LootContainer/cache loot |
| `P` | Mở CheatMenu; năm thao tác trên nằm trong nhóm `ROUTE B FLOW TEST — NO LOOT CONTAINERS` |

Quy trình test nhanh đã chốt:

1. Luôn vào game bằng `MainMenu → Solo → Easy → Main` để player và Fusion runner spawn đúng.
2. Nhấn `F6` để mô phỏng kiểm tra xe; nghe/skip Cue 01–02 rồi chọn tuyến đang theo dõi.
3. Dùng `F6` ba lần để xem từng tài liệu, hoặc `F7` để đưa thẳng tiến độ lên 3/3. Cả hai cách đều không chạm LootContainer trong đường test.
4. H2/H5 chạy `ShiftLog → ShiftLog2 → FindRadioKey → shared key → Door → RadioReady`; F12 tới đúng selected KeyLoot rồi Radio.
5. Nhấn F6 ba lần ở `RadioReady`: lần 1/2 dừng tại checkpoint và sinh theo độ khó; lần 3 mới hoàn tất. Gameplay thật yêu cầu ba lần giữ E, tổng 14 giây.
6. Sau chuỗi Radio, kiểm tra thẻ thưởng dùng ảnh giấy rách `Mảnh bản đồ 2`, map tự mở/reveal riêng căn cứ, rồi cinematic. Cue 09 clean phát sau cinematic và trước bảng đổi waypoint lần hai; dùng `F12` để tới căn cứ hoặc `F10` để mô phỏng phase tiếp cận.
7. `F10` lần kế tiếp phát Cue 11 rồi mở bảng xác nhận. Ending chỉ khóa khi click **XÁC NHẬN TUYẾN B**.
8. Sau khi xác nhận, tiếp tục `F10`: máy phát → mô phỏng đủ part → xe sẵn sàng → extraction/Cue 15/Victory Summary.

Debug path gọi chung các hàm core authoritative với gameplay thật. Chỉ phần không có LootContainer mới được mô phỏng; xác nhận điểm không thể quay lại và khóa ending không bị bỏ qua.

## 7. Trạng thái triển khai hiện tại

| Hạng mục | Trạng thái |
| --- | --- |
| Kiểm tra xe, Cue 01–02, bảng chọn lần 1 | Đã triển khai |
| Chọn và đồng bộ khu dân cư | Đã triển khai |
| Loot 3 tài liệu trong nhà + pity/insurance | Đã triển khai |
| Ranh giới sương, cảnh báo, trả player sau 2 giây | Đã triển khai cho nhiệm vụ khu dân cư |
| Cue 03–05, map fragment 1, cập nhật journal | Đã triển khai; cần regression test khi sửa tiếp |
| Thiết kế chương bệnh viện mới | Đã khóa trong `HOSPITAL_ROUTE_DESIGN_LOCK.md`; H1–H4 đã triển khai |
| Trigger vào bệnh viện | Đã nối objective mới tới `HospitalQuest_ShiftLog` |
| ShiftLog → ShiftLog2 → random KeyLoot → Door → RadioReady | 6 candidate, stable selected ID `[Networked]`, shared key và Journal/waypoint authoritative; 10 Polygon riêng thay cho range/LOS dễ lỗi |
| Flow cũ Bàn Điều phối → Radio → Tủ hồ sơ | 5 interaction marker đã vô hiệu hóa; H3 không còn phụ thuộc Tủ hồ sơ |
| H3 Radio 14 giây/3 chặng + operator handoff | Đã triển khai authoritative; pause/rời vùng giữ tiến độ, chỉ một operator, người khác tiếp tục được |
| H4 noise, zombie và kể chuyện môi trường | Nhiễu 2,7s; mỗi mốc sinh 3/4/5 zombie × 2 anchor theo Easy/Normal/Hardcore, nhịp 0,25s; không kill gate. Bốn xác tĩnh làm breadcrumb |
| Cue 05–09 và bảng chọn lần 2 | Cue 06–08 đã thay bằng transcript canonical + static chờ thu voice mới; Cue09 clean 6,65s; bảng chọn lần hai vẫn chỉ “theo dõi” |
| Reward Mảnh bản đồ 2 + map reveal + cinematic | Đã nối theo thứ tự Radio → reward → map reveal → cinematic → Cue09 → bảng chọn; map thật chỉ xuất hiện trong màn hình bản đồ |
| Marker bệnh viện sau Mảnh 2 | Đã xóa khỏi map; chỉ còn marker căn cứ quân sự để tránh hai mục tiêu cạnh tranh |
| Minimap sau bệnh viện | Đã tách khỏi map unlock; giữ tắt |
| Điểm không thể quay lại và khóa Ending B | Có code authoritative |
| Siege, sửa `Car`, extraction | Có runtime code; Generator/cache/armory cũ không thuộc flow canonical |
| Journal phần căn cứ | Đã theo dõi `NotReached → Investigating → SiegeAndRepair → ReadyToEscape → Escaped/Failed`; chỉ chuyển Completed sau extraction |
| CheatMenu/phím test không loot | Đã triển khai `F6/F7/F10/F11/F12` và nhóm nút riêng |
| Test tự động toàn Tuyến B | Đã qua MainMenu → Main → Ending B không LootContainer |
| Minigame sửa xe 5 hạng mục tích hợp vào finale căn cứ | Đã nối trực tiếp vào `Car` sau cinematic/siege |
| Loot dùng chung Tuyến A/B trong bệnh viện | Không còn là blocker của chương Radio mới; nếu làm loot phụ thì là phạm vi riêng |

## 8. Việc cần làm khi tiếp tục

Đọc `HOSPITAL_ROUTE_DESIGN_LOCK.md` trước khi sửa. Triển khai đúng thứ tự:

1. **H1 — Cửa và interaction — ĐÃ XONG 2026-08-25:** `Door13_W` đóng, `Door14_W` mở, network state authoritative, blocker riêng và vùng Door/Radio nhỏ chống bấm xuyên tường. `89/89` EditMode, H1 PlayMode `1/1`, regression Tuyến B `1/1` pass. Test tay Solo đã pass toàn tuyến spawn → cheat bệnh viện → mở cửa → dùng Radio; multiplayer chưa xác nhận.
2. **H2/H5 — Manh mối và chìa khóa — ĐÃ TRIỂN KHAI 2026-08-25:** ShiftLog → ShiftLog2 → random KeyLoot/shared key → cửa → RadioReady; stable ID và state replicate cho late join.
3. **H3 — Radio và cốt truyện — ĐÃ TRIỂN KHAI 2026-08-25:** 14 giây shared progress/handoff, transcript, Mảnh 2, map reveal, Cue09 clean và bảng chọn lần hai. Chờ test tay Solo + Host/Client/late join.
4. **H4 — Cao trào/môi trường — ĐÃ TRIỂN KHAI 2026-08-25:** ba vạch; hai mốc tự dừng/nhiễu 2,7s; mỗi mốc 3/4/5 × 2 anchor, nhịp 0,25s; không kill gate; bốn xác tĩnh.
5. **H5 — NETWORK/POLYGON/REGRESSION — ĐÃ TRIỂN KHAI:** shared state và request validation authoritative; scene/regression tự động pass. Test tay Host/Client hai máy là acceptance còn lại.
6. Giữ bảng chọn lần hai sau Radio là lựa chọn theo dõi, chưa khóa ending.
7. Finale căn cứ đã chốt dùng minigame sửa xe 5 hạng mục trên `Car`; mechanic Generator/ba vật phẩm prototype không còn canonical.

## 9. Kết quả và checklist test end-to-end

Kết quả tự động ngày 2026-08-24:

- EditMode `QuestFlowUIPrototypeTests`: **41/41 passed**; đã bao gồm test Mảnh 2 dùng art giấy rách, marker căn cứ thay thế marker bệnh viện và raster map không bị dùng sai làm reward.
- PlayMode `RouteBDebugFlowRunsFromMainMenuThroughMilitaryExtractionWithoutLootContainers`: **1/1 passed**, khoảng 10 giây.
- Test này thực sự đi `MainMenu → Main`, spawn player/Fusion, chạy đủ stage bệnh viện, khóa Ending B tại xác nhận, chạy căn cứ và chỉ đánh dấu Journal Completed sau extraction.
- Test cũng xác nhận sau `CityMapFound`: marker căn cứ xuất hiện trên bản đồ nhiệm vụ và Canvas minimap vẫn tắt.
- Finale quân sự: `51/51` EditMode liên quan và `2/2` PlayMode trọng tâm đạt; Unity script validation báo `0` lỗi. Hai PlayMode xác nhận `Car` giữ nguyên vị trí author và flow MainMenu → Ending B. Full nhóm PlayMode rộng hơn đạt `3/4`; failure còn lại thuộc assertion trả Player về ranh giới khu dân cư, không thuộc finale quân sự.

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
- [ ] ShiftLog dẫn tới ShiftLog2; ShiftLog2 chọn đúng một KeyLoot; nhặt key mới dẫn tới Radio.
- [ ] Tìm cửa Radio trước không soft-lock; prompt hướng về văn phòng trưởng ca.
- [ ] Door/Radio không chồng prompt và không thể bấm Radio xuyên cửa.
- [ ] Radio giữ tiến độ khi đổi người vận hành; người ở xa không bị khóa UI/gameplay.
- [ ] Cue 05–09 mới và notification không chồng UI; transcript có trong Journal cho người ở xa/late join.
- [ ] Hai mốc cao trào chỉ xảy ra đúng một lần/mốc; đủ 3/4/5 zombie tại A và B theo độ khó, không wave lặp/kill gate.
- [ ] Bảng chọn lần hai không khóa ending.
- [ ] Tiếp cận căn cứ phát Cue 10 một lần.
- [ ] Cue 11 xuất hiện trước bảng xác nhận điểm không thể quay lại.
- [ ] Chỉ nút xác nhận cuối mới khóa Ending B cho toàn đội.
- [ ] Siege/vote/cinematic/sửa `Car` đồng bộ Host và Client; xác nhận không còn Generator.
- [ ] Chỉ active repairer bị khóa input; đồng đội không bị khóa.
- [ ] Late join nhận đúng stage, item mask, phase, máu cổng và tiến độ sửa.
- [ ] Cue 14 chỉ phát khi xe sẵn sàng; Cue 15 và Victory Summary xuất hiện khi extraction hoàn tất.

### Hotfix regression 2026-08-26

- Zombie siege không bất tử do loot xác. Nguyên nhân là `SiegeZombieObjective` tiếp tục ghi movement/attack lên zombie đã chết; objective nay retire ngay theo replicated death state, còn `ZombieCorpseLoot` tiếp tục giữ xác để lục.
- UI mất sau cinematic có hai nguồn chồng nhau: canvas snapshot của radio và callback reward/map/chọn tuyến cũ. Radio nay reconcile canvas theo logical modal state; mọi bước giới thiệu route cũ dừng ngay khi ending đã khóa.
- PlayMode `RouteBDebugFlowRunsThroughAuthoritativeRepairLootAndMilitaryExtraction`: `1/1 passed`, `41,40s`. Test bao gồm zombie chết không đứng dậy/đánh cổng, corpse loot còn tồn tại, AutoCanvas phục hồi, inventory mở được và tủ Route B mở bằng interaction thật.

### Finale siege hardening sau test tay 2026-08-26

- Biến thể áo vàng là `ZombieKhoaRebuilt`. Death state của nó được trình bày trong `Render()`, nên không được disable component như zombie Thai; objective công thành nay dừng còn Render dead-only vẫn giữ `IsDead` và collider tắt.
- Horde batch luân phiên hai prefab. Khi cổng vỡ, objective bật AI gốc và force target Player sống gần nhất; loop spawn không còn bị điều kiện `releasedToPlayers` chặn, zombie sinh sau vỡ cổng được release ngay.
- Finale time lock bắt đầu ngay khi vote chốt: `16:00`, không trôi giờ, không sleep transition/forced collapse, sleepiness và fatigue luôn 0, panel đồng hồ góc màn hình ẩn.
- `NotifyPlayerDamaged` chỉ interrupt repair khi nguồn damage được đánh dấu direct zombie attack. Starvation, thirst và bleeding/DOT vẫn trừ máu nhưng không hủy minigame.
- Còi xe: 100% trong cinematic → loop 20% sau cinematic → dừng khi `ArePoliceCarRepairsComplete` chuyển true.
- QA mới: EditMode rule `1/1 passed`; full Route B PlayMode `1/1 passed` trong `37,27s`; Unity compile/Console trước test không có compile error. Test tự động bao phủ riêng zombie áo vàng, target + spawn sau vỡ cổng, time/sleep/clock, vòng đời còi và điều kiện interrupt.

## 10. File chính cần đọc trong chat mới

- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/PreMilitaryQuestRuntimeBridge.cs`
- `Assets/Script/Tin/MainQuest/MainQuestStartTrigger.cs`
- `Assets/Script/Tin/MainQuest/MainQuestSearchCabinet.cs`
- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- `Assets/Script/Tin/MainQuest/MilitarySchoolCluePoint.cs`
- `Assets/Script/Tin/MainQuest/MilitaryRouteVoteUI.cs`
- `Assets/Script/Tin/MainQuest/MilitaryRouteCinematicController.cs`
- `Assets/Script/Tin/MainQuest/SiegeHordeDirector.cs`
- `Assets/Script/Tin/MainQuest/RoadsideVehicleRepairStation.cs`
- `Assets/Script/Tin/Prototype/MilitaryStoryFlowRules.cs`
- `Assets/Script/Tin/DevCheatManager.cs`
- `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs`
- `Assets/Script/Tin/Prototype/PreMilitaryQuestProgress.cs`
- `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs`
- `Assets/Script/Tin/Prototype/QuestMapUIPrototype.cs`
- `Assets/Script/Tin/Prototype/RouteBAudioContent.cs`
- `Assets/Script/Tin/GameLocalization.cs`
- `HOSPITAL_ROUTE_DESIGN_LOCK.md`
- `README_MAINPLAY_CODEX_HANDOFF.md`

Ảnh Game View thật để chốt vị trí prompt `GIỮ [E]`:

- `Captures/hold-e-option-a-above-hotbar.png` — cố định ngay trên hotbar.
- `Captures/hold-e-option-b-world-target.png` — bám mục tiêu trong thế giới, trực quan nhưng dễ phải clamp/nhảy vị trí.
- `Captures/hold-e-option-c-top-objective.png` — cố định dưới vùng objective ở phía trên.

Chủ dự án đã chọn C. Production hiện dùng thẻ tương tác riêng ở dưới vùng objective: keycap `[E]` màu cam, eyebrow `TƯƠNG TÁC • GIỮ PHÍM`, hành động ở dòng hai, nền xanh-đen cùng sọc cam bên trái. Nó không dùng cấu trúc tiêu đề/nội dung của mission toast nên người chơi dễ phân biệt. Ảnh kiểm tra thực tế: `Captures/hold-e-option-c-improved-interaction-card.png`. Quy tắc ẩn prompt khi hội thoại, bảng chọn, Journal, map hoặc UI khác mở tiếp tục được giữ bằng `LocalGameplayUIState.BlocksWorldInteractionHints`.

## 11. Câu lệnh bàn giao gợi ý cho chat Codex mới

> Hãy đọc `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md`, đặc biệt B4–B7, và các file finale quân sự trong mục 10. H1–H5 bệnh viện đã triển khai; finale dùng 3 manh mối quest-state → rời mái trường → vote nhất trí tại `Car` → cinematic đóng cổng → horde + sửa 5 hạng mục. Không khôi phục Generator/150% HP/electric stun.

## 12. Đính chính trạng thái Route B hiện tại — 2026-08-29

Phần này thay thế các mô tả B6/B7 cũ khi có xung đột:

- Loot sửa xe dùng năm `LootQuanSu` đã được author trong School, không còn spawn năm `MilitaryRepairLootContainer` runtime tại marker.
- Zombie công thành dùng offset hash liên tục theo bề ngang/chiều sâu và lệch pha animation; không còn 13 lane cố định. Hướng chết được authority chọn ngẫu nhiên và đồng bộ.
- Sau khi cổng vỡ, objective công thành chỉ trả zombie về AI gốc. Không còn quét Player hoặc manual chase theo từng zombie; tiếng xe cảnh sát authority-side thu hút zombie theo hệ thống noise canonical.
- Xe sửa đủ không tự kết thúc siege. Tài xế khởi động bằng `W` khi mọi Player sống đạt readiness. Với tối đa 10 Player, readiness là ngồi đúng xe hoặc đứng ngoài xe trong bán kính 6m; người không có ghế trở thành virtual follower được bảo vệ trong outro.
- Xe thật phải đi tuần tự `EndB1 → EndB2 → EndB3 → EndBFinal`. Tại `EndBFinal`, authority căn xe theo hướng `EndBFinal2`, khóa lái và tự chạy thẳng tới đó.
- Camera đi tới `EndBToCinemachine` trong 6 giây với profile tăng tốc chậm rồi giảm dài, giữ 2 giây, sau đó mới fade và mở Victory Summary.
- `F1` là shortcut Editor/Development cho Host/Solo: hoàn tất phần trước quân sự và teleport tới School; không tự lấy ba clue School, không vote và không khóa ending.
- Route B final polish đã vào `main` qua PR #323. Chi tiết mới hơn, test và QA còn mở nằm trong `CODEX_PROJECT_WORK_LOG.md`.
