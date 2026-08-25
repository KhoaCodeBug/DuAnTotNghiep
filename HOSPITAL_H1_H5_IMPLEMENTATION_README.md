# Hospital Route H1–H5 — Implementation handoff

> Cập nhật: 2026-08-25  
> Scene: `Assets/Scenes/Main.unity`  
> Unity: `6000.0.69f1`

Tài liệu này mô tả phần bệnh viện đã thực sự triển khai, thay đổi cốt truyện, contract multiplayer, cách author Polygon/KeyLoot và cách test. Nguồn thiết kế dài hơn nằm trong `HOSPITAL_ROUTE_DESIGN_LOCK.md`.

## Flow canonical hiện tại

```text
Ba hồ sơ khu dân cư
→ xác định bệnh viện
→ ShiftLog tại quầy tiếp tân
→ ShiftLog2 tại văn phòng trưởng ca
→ Host chọn ngẫu nhiên một KeyLoot
→ Player nhặt shared Radio key
→ mở cửa Trạm Radio phụ trợ
→ sửa Radio 14 giây / 3 chặng
→ hai đợt nhiễu + zombie theo độ khó
→ nghe bản ghi bệnh viện
→ nhận Mảnh bản đồ 2 / tọa độ Căn cứ phía Bắc
→ map reveal + cinematic + bảng chọn mục tiêu lần hai
```

Không còn flow `Bàn Điều phối → Radio → Tủ hồ sơ`. Radio trao trực tiếp tọa độ và Mảnh bản đồ 2.

## Thay đổi cốt truyện

- Bệnh viện từng vận hành một trạm liên lạc phụ trợ, không phải nơi quân đội đang chờ Player.
- Nhân viên Radio bị cắn, tự khóa mình trong trạm và tiếp tục gửi cầu cứu.
- Bản ghi cho thấy quân đội nhận được tín hiệu nhưng bỏ bệnh viện theo lệnh phong tỏa đỏ.
- Beacon/tọa độ chỉ chứng minh đoàn xe từng rút về Căn cứ phía Bắc; không khẳng định còn người sống.
- Player tới căn cứ để tìm phương tiện/vật tư tự thoát, không phải chờ cứu viện.
- Hai bảng chọn A/B trước finale chỉ đổi objective đang theo dõi. Ending B chỉ khóa khi toàn đội xác nhận kích hoạt báo động tại căn cứ.

## H1–H5 đã làm gì

### Cửa và Radio

- Cửa dùng tile đóng/mở thật và `DoorBlocker` trên layer `Obstacle`.
- Door/Radio state-gated; Radio không thể dùng xuyên cửa.
- Tiến độ Radio là state dùng chung, chỉ một operator tại một thời điểm; thả E/rời vùng giữ tiến độ và người khác tiếp tục được.

### Polygon interaction

Mười điểm hiện có child `InteractionZone` với `PolygonCollider2D` riêng:

- `HospitalQuest_ShiftLog`.
- `HospitalQuest_ShiftLog2`.
- Sáu `KeyLoot 1*`.
- `DoorInteraction`.
- `RadioInteraction`.

Local prompt và State Authority đều gọi cùng `PolygonCollider2D.OverlapPoint`. Khi có Polygon, radius/line-of-sight cũ chỉ còn fallback cho scene cũ.

Để chỉnh quầy tiếp tân: chọn `HospitalQuest_ShiftLog/InteractionZone` → `Edit Collider` → vẽ vùng đứng hợp lệ ở phía công cộng của quầy. Chạy lại setup không ghi đè các vertex đã chỉnh.

### Random KeyLoot

- Scene hiện có 6 `HospitalRadioKeyLootPoint`.
- Sau ShiftLog2, chỉ State Authority random một stable ID và ghi vào `[Networked] SelectedHospitalRadioKeyLootId`.
- Chỉ điểm được chọn hiện prompt/waypoint và nhận request nhặt key.
- Host tái xác thực stage, stable ID, Player còn sống và Player nằm trong đúng Polygon.
- Nhặt thành công đặt `[Networked] HasHospitalRadioKey`; key dùng chung, không chiếm inventory và không mất do chết/disconnect.
- Late join nhận cùng selected ID, key và stage.

Để thêm điểm: duplicate một KeyLoot hiện tại, đổi vị trí rồi Edit Collider trên child `InteractionZone`. Component và Polygon được copy; code không giới hạn số candidate và không phụ thuộc hậu tố tên.

### Radio H4 theo độ khó

| Độ khó | Zombie mỗi anchor/mốc | Mỗi mốc A+B | Tổng hai mốc |
| --- | ---: | ---: | ---: |
| Dễ | 3 | 6 | 12 |
| Thường | 4 | 8 | 16 |
| Hardcore | 5 | 10 | 20 |

- A và B sinh một con đồng thời ở mỗi nhịp; nhịp kế tiếp sau `0,25 giây`.
- Vị trí được trải đều trái/phải với spacing `0,8` world-unit.
- Chặng 1 và 2 tự dừng, nhả operator và phát nhiễu dài khoảng `2,7 giây` (hai chu kỳ).
- Không có kill gate; Player được tiếp tục sửa dù zombie còn sống.
- Chặng 3 không sinh thêm zombie.
- Wave chỉ được State Authority gọi một lần/checkpoint; người chơi ở xa không nhân số lượng.

### Kể chuyện môi trường

Bốn xác zombie tĩnh làm breadcrumb từ bệnh viện ra trạm phụ trợ. Chúng chỉ có Transform + SpriteRenderer, không collider, AI, networking, interaction hoặc loot.

## Contract multiplayer

Các state sau là Fusion `[Networked]`:

- Hospital investigation stage.
- Selected KeyLoot ID.
- Shared Radio key.
- Door open state.
- Radio progress và active operator.
- Radio checkpoint count.
- Threat spawn counter.
- Fragment/map state.

Client chỉ gửi request. State Authority tái resolve scene point và xác thực stage/ID/Polygon/living player trước khi thay đổi state. Random key, Radio progress và zombie spawn không chạy phía client.

## Audio mở đầu cần thành viên thu lại — chưa sửa trong branch này

Vị trí cần thay sau khi có voice đồng nhất: `RouteBAudioContent.cs`, cue `OpeningEmergencyBroadcast`; nên audit luôn `PlayerRouteReaction`.

Lý do: lời hiện tại nói thẳng “Văn phòng Điều phối vẫn lưu bản đồ dẫn tới điểm tập kết quân sự”, tiết lộ bệnh viện/bản đồ/tuyến quân sự trước khi Player thu thập ba hồ sơ và làm mất cảm giác bản tin tự động mơ hồ.

Nội dung đề xuất để thu lại:

> **BẢN TIN SƠ TÁN TỰ ĐỘNG**  
> “…Tuyến sơ tán dân sự phía Bắc đã ngừng hoạt động… Người sống sót không được tiếp cận các chốt phong tỏa…”  
> “…Các điểm tiếp tế tại khu dân cư phía Đông có thể còn vật tư và hồ sơ điều chuyển…”  
> “…Không có xác nhận cứu hộ. Bản tin này sẽ tự động phát lại…”

Phản ứng Player đề xuất:

> “Không còn ai điều hành tín hiệu này. Nhưng hồ sơ tiếp tế có thể cho biết họ đã chuyển người và vật tư đi đâu. Chiếc xe vẫn là phương án còn lại.”

Yêu cầu khi nhận file mới: cập nhật cả Việt/Anh, subtitle, fallback duration và resource path; không dùng voice AI khác giọng để vá tạm.

## Test thực tế H5

QA tự động chốt ngày 2026-08-25:

- EditMode toàn project: `96/96` pass.
- PlayMode scene H5 và regression `MainMenu → Ending B`: `2/2` pass.
- Full PlayMode toàn project: `4/5`; test cũ của xe cảnh sát fail do scene hiện thiếu fixture `ViTriXeTest`/`VungKiemTraXeCanhSat`. H5 không sửa/tự sinh fixture quân sự này vì phần đó thuộc phiên thảo luận finale tiếp theo.
- Unity không có compile/gameplay error; Console chỉ có thông báo Test Runner ghi `TestResults.xml` bị Unity phân loại là `Exception`.
- Chưa chạy acceptance Host/Client trên hai máy thật; không được suy diễn Fusion Single thành xác nhận multiplayer thực tế.

### Solo

1. `MainMenu → Solo → Easy → Main`, cheat tới bệnh viện.
2. Đứng trong Polygon quầy tiếp tân; giữ E đọc ShiftLog.
3. Đọc ShiftLog2.
4. Kiểm tra waypoint dẫn tới đúng một trong sáu KeyLoot; thử các điểm khác không có prompt.
5. Nhặt selected key, mở cửa Radio.
6. Hoàn tất ba vạch Radio; không giết wave đầu và tiếp tục sửa để kiểm tra không có kill gate.

Kết quả mong đợi: Easy sinh `6` zombie ở mốc 1 và thêm `6` ở mốc 2; static kéo dài khoảng 2,7 giây; mốc 3 không sinh thêm; map/cinematic chạy đúng thứ tự.

### Host/Client hai máy — acceptance thủ công còn phải thực hiện

1. Host và Client vào cùng phòng; tách đội trước ShiftLog2.
2. Một người đọc ShiftLog2; cả hai phải nhận cùng selected KeyLoot/waypoint.
3. Người còn lại nhặt key; cả đội cùng có key và Host/Client đều mở được cửa.
4. Đổi operator Radio giữa chặng; tiến độ không mất.
5. Cho Client đứng xa: không nhận static/modal cục bộ nhưng vẫn nhận stage/transcript qua Journal.
6. Disconnect người đã nhặt key/operator rồi reconnect hoặc late join.

Kết quả mong đợi: selected ID/key/checkpoint/progress không đổi; không nhân đôi wave; mỗi checkpoint chỉ spawn một lần theo độ khó Host.

## File chính

- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/HospitalQuestClueInteractionPoint.cs`
- `Assets/Script/Tin/MainQuest/HospitalRadioKeyLootPoint.cs`
- `Assets/Script/Tin/MainQuest/HospitalRadioRoomController.cs`
- `Assets/Editor/HospitalRadioH1Setup.cs`
- `Assets/Script/Tin/MainQuest/PreMilitaryQuestRuntimeBridge.cs`
- `Assets/Script/Tin/Prototype/QuestFlowUIPrototype.cs`
- `Assets/Script/Tin/Prototype/Tests/Editor/HospitalRadioRoomRulesTests.cs`
- `Assets/Script/Tin/Prototype/Tests/PlayMode/MainMenuToMilitaryQuestFlowTests.cs`

Kế hoạch tiếp theo: `NEXT_SESSION_MILITARY_FINALE_PLAN.md`.
