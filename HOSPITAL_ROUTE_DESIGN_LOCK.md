# Tuyến B — Hospital Route Design Lock

> Chốt thiết kế: 2026-08-25
> Phạm vi: chương bệnh viện từ khi đủ ba tài liệu khu dân cư tới khi mở tọa độ căn cứ quân sự.
> Trạng thái: **đã chốt thiết kế, chưa triển khai code/tile/component**. Runtime hiện vẫn chạy flow marker cũ; chat triển khai phải thay flow cũ theo tài liệu này.

## 1. Mục tiêu thiết kế

Chương bệnh viện phải:

- Cho Player một động cơ rõ ràng để tới bệnh viện: tìm bản ghi liên lạc cuối cùng và dấu vết của đoàn xe sơ tán quân sự.
- Kể chuyện bằng môi trường, ghi chú, Journal và một bản ghi Radio chính; không chèn độc thoại Player liên tục.
- Phù hợp co-op open world: người chơi có thể tách đội, không bị ép cùng xem cutscene hoặc đứng chung một vùng.
- Có chuỗi nhân quả dễ hiểu: manh mối → chìa khóa → phòng Radio → tín hiệu → căn cứ.
- Có một cao trào gameplay ngắn, khả thi với hệ noise, zombie và hold-interaction hiện có.
- Không cần LootContainer, NPC quân đội, AI đồng minh, puzzle mới, boss hoặc asset bệnh viện mới.

## 2. Tiền đề cốt truyện

Bệnh viện từng là điểm điều trị khẩn cấp và trạm liên lạc với căn cứ quân sự. Khi phát hiện ca nhiễm, quân đội xếp bệnh viện vào vùng phong tỏa đỏ, cấm đoàn xe dừng lại và rút toàn bộ phương tiện về căn cứ phía bắc.

Nhân viên vận hành Radio của bệnh viện đã bị cắn. Người này tự khóa mình trong trạm liên lạc phụ trợ nằm phía sau bệnh viện để tiếp tục cầu cứu mà không gây nguy hiểm cho người khác. Chìa khóa dự phòng được giữ tại văn phòng trưởng ca.

Player không tới căn cứ vì tin quân đội chắc chắn còn sống. Bản ghi chỉ chứng minh:

- Quân đội đã nhận được lời cầu cứu nhưng ưu tiên lệnh phong tỏa.
- Đoàn xe, nhiên liệu và vật tư sơ tán đã được rút về căn cứ.
- Một beacon tại căn cứ vẫn phát tín hiệu lặp lại; có thể chỉ là máy tự động.

Động cơ Tuyến B là lấy phương tiện quân sự để **tự thoát**, không phải chờ quân đội tới cứu.

## 3. Flow canonical

```text
Đủ 3 tài liệu khu dân cư
    ↓
Mở vị trí bệnh viện — biết nơi này từng giữ tần số quân sự
    ↓
HospitalQuest_ShiftLog tại quầy tiếp tân
    ↓
Biết trạm Radio nằm ngoài khu chính và chìa dự phòng ở văn phòng trưởng ca
    ↓
HospitalQuest_ShiftLog2 tại văn phòng ngay sau tiếp tân
    ↓
Nhận chìa khóa shared + đọc lệnh phong tỏa
    ↓
Journal/waypoint dẫn tới trạm liên lạc phụ trợ phía sau bệnh viện
    ↓
DoorInteraction — mở cửa bằng chìa khóa
    ↓
RadioInteraction — giữ E khôi phục tín hiệu
    ↓
Noise thu hút zombie / nhóm dự phòng từ hai anchor
    ↓
Nghe bản ghi cầu cứu + lệnh quân sự bỏ điểm y tế
    ↓
Khôi phục tọa độ và Mảnh bản đồ 2 ngay tại Radio
    ↓
Reveal căn cứ → cinematic → bảng đổi mục tiêu theo dõi lần hai
```

Không còn bước `RecordsCabinet`/Tủ hồ sơ riêng.

## 4. Anchor scene đã có

Tất cả nằm dưới `==========Map========== /Map/hospital` trong scene `Main`:

| Anchor | Vai trò đã chốt |
| --- | --- |
| `HospitalQuest_ShiftLog` | Điểm đọc sổ tiếp nhận tại quầy tiếp tân. |
| `HospitalQuest_ShiftLog2` | Văn phòng trưởng ca; tiết lộ lệnh phong tỏa và nơi giấu chìa khóa. |
| `KeyLoot 1*` | Sáu candidate hiện tại cho chìa Radio; mỗi điểm có component và Polygon riêng, có thể duplicate thêm. |
| `HospitalQuest_RadioRoom` | Root của trạm Radio phụ trợ. |
| `HospitalQuest_RadioRoom/DoorInteraction` | Tương tác cửa khi phòng còn khóa. |
| `HospitalQuest_RadioRoom/RadioInteraction` | Tương tác khôi phục Radio sau khi cửa đã mở. |
| `HospitalQuest_ZombieEntry_A` | Điểm zombie dự phòng phía thứ nhất. |
| `HospitalQuest_ZombieEntry_B` | Điểm zombie dự phòng phía thứ hai. |

Khoảng cách đã kiểm tra trong Editor:

- `ShiftLog → ShiftLog2`: khoảng 3,2 world-unit; phù hợp flow tiếp tân → văn phòng.
- `ShiftLog2 → KeyLoot ngẫu nhiên → RadioRoom`: waypoint dùng ID do Host chọn và giữ nguyên cho mọi client/late join.
- `DoorInteraction → RadioInteraction`: khoảng 1,76 world-unit; đủ tách nhưng phải dùng vùng nhỏ và state gating.
- Chủ dự án xác nhận Player có thể đi một mạch từ bệnh viện tới phòng Radio, không có collider/tilemap chặn đường.

Không di chuyển hoặc tự sửa các anchor trên nếu chưa có yêu cầu mới.

## 5. Trình tự và cách truyền đạt

### 5.1. Sau ba tài liệu khu dân cư

Ba tài liệu giữ ba vai trò hiện có nhưng nội dung phải link lại:

1. Hồ sơ tiếp tế: pin, thuốc và vật tư từng được chuyển tới bệnh viện/trạm Radio.
2. Lịch trình sơ tán: bệnh viện bị gỡ khỏi tuyến đón sau lệnh phong tỏa đỏ; đoàn xe rút về căn cứ.
3. Ghi chú Điều phối: tần số dự phòng và vị trí bệnh viện là đầu mối cuối cùng.

Journal mở vị trí bệnh viện. Không khẳng định quân đội còn sống.

### 5.2. `HospitalQuest_ShiftLog`

Đây là manh mối dẫn đường, không trao chìa khóa.

Nội dung ngắn:

> Khu điều trị đã đóng.
> Toàn bộ liên lạc khẩn cấp chuyển sang Trạm phụ trợ phía sau bệnh viện.
> Chìa khóa dự phòng do trưởng ca giữ tại văn phòng hành chính.

Journal toàn đội:

> Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân.

### 5.3. `HospitalQuest_ShiftLog2`

Đây là bước bắt buộc. Người tương tác tìm thấy:

- Lệnh phong tỏa cấp đỏ.
- Ghi chú nhân viên Radio đã tự khóa mình tại trạm phụ trợ sau khi bị cắn.
- Sổ bàn giao cho biết chìa dự phòng được giấu tại một vị trí trong khu văn phòng.

Nội dung ngắn:

> Lệnh phong tỏa cấp đỏ đã được xác nhận.
> Đoàn xe không được dừng tại bệnh viện.
> Nhân viên liên lạc có dấu hiệu nhiễm bệnh và đã tự khóa mình tại Trạm phụ trợ để giữ kênh Radio hoạt động.

Sau khi đọc, State Authority chọn ngẫu nhiên một `HospitalRadioKeyLootPoint` và replicate stable ID. Chỉ điểm được chọn có prompt/waypoint; các điểm còn lại vô hiệu. Player giữ `E` trong Polygon của điểm được chọn để nhận chìa khóa shared. Chìa không chiếm inventory và không thể mất do chết/disconnect.

Journal:

> Tìm chìa khóa Radio tại vị trí được đánh dấu.

Sau khi nhặt:

> Dùng chìa khóa mở Trạm liên lạc phụ trợ phía sau bệnh viện.

### 5.4. Dẫn tới Radio

Sau khi có chìa khóa phải dùng đồng thời:

- Journal ghi rõ trạm nằm phía sau bệnh viện/gần lối dịch vụ phù hợp bố cục thật.
- Waypoint có điều kiện tại `HospitalQuest_RadioRoom` khi đang theo dõi Tuyến B.
- Dấu môi trường: bảng `KHU PHỤ TRỢ / COMMUNICATION`, đèn đỏ, dây cáp hoặc xác chết dẫn đường.

Open world vẫn dùng waypoint; waypoint chỉ xuất hiện sau khi Player đã tìm được manh mối hợp lý.

### 5.5. Cửa và Radio trong phòng nhỏ

Khi cửa đóng:

- Chỉ `DoorInteraction` hoạt động.
- `RadioInteraction` phải bị vô hiệu hoàn toàn, không chỉ ẩn prompt.
- Mỗi điểm tương tác bệnh viện có child `InteractionZone` chứa `PolygonCollider2D` riêng, editable trực tiếp trong scene.
- Client hiện prompt và server xác thực bằng cùng `PolygonCollider2D.OverlapPoint`; Polygon authored thay cho radius/line-of-sight dễ lỗi quanh quầy sâu.
- Server vẫn kiểm tra stage, quest key, stable target ID và Player còn sống.

Sau khi mở:

- Tile/sprite cửa đổi sang mở và collider cửa tắt.
- `DoorInteraction` tắt hoàn toàn.
- `RadioInteraction` mới được bật.
- Polygon Radio đặt sát console và chỉ bao phủ vùng tiếp cận được từ trong phòng.
- Resolver chỉ chọn interaction hợp lệ theo state để không hiện hai prompt hoặc bấm xuyên tường.

State cửa đồng bộ Host/Client và late join.

### 5.6. Khôi phục Radio

Thiết kế đã duyệt và triển khai H4:

- Tổng thời gian cơ bản: `14 giây`, chia đều thành `3` chặng/vạch vàng.
- Chỉ một Player vận hành tại một thời điểm. Thả `E` hoặc rời vùng thì tạm dừng nhưng giữ tiến độ; người khác có thể tiếp tục.
- Khi hoàn tất chặng 1 hoặc chặng 2, thao tác tự dừng, Radio phát nhiễu và nhả operator. Player phải thả rồi giữ `E` lại để bắt đầu chặng kế tiếp.
- Mỗi mốc đầu gọi theo độ khó tại cả A và B: Dễ `3/điểm`, Thường `4/điểm`, Hardcore `5/điểm`. Các con xuất hiện tuần tự cách nhau `0,25 giây`, trải đều trái/phải với spacing `0,8` world-unit.
- Nhiễu Radio dài `2,7 giây` (hai chu kỳ) để còn nghe rõ trong lúc wave tuần tự xuất hiện.
- Chặng 3 hoàn tất bản ghi và trao phần thưởng, không tạo thêm zombie.
- Không có kill gate: Player được tiếp tục sửa ngay dù zombie của mốc trước còn sống. Ra ngoài kiểm tra/tiêu diệt chúng là lựa chọn chiến thuật.
- Mỗi mốc chỉ kích hoạt một lần theo state authoritative; tổng H4 là `12/16/20` zombie tương ứng Dễ/Thường/Hardcore, không phụ thuộc số Player ở xa và không có wave vô hạn.
- Người ở gần Radio nghe nhiễu/thấy cảnh báo; người ở xa không bị ép UI hoặc audio.

## 6. Nội dung Radio canonical

Đây là đoạn thoại dài duy nhất của chương bệnh viện. Không chèn độc thoại Player giữa các câu.

### Bản ghi của bệnh viện

> “Trạm Y tế Mười Bốn gọi Căn cứ phía Bắc.”
> “Khu điều trị đã bị xuyên thủng. Chúng tôi còn hai mươi sáu dân thường và bảy nhân viên.”
> “Yêu cầu một hành lang sơ tán. Xin xác nhận.”

Lời gọi lặp lại qua nhiễu.

### Phản hồi quân sự bị đứt đoạn

> “…lệnh phong tỏa cấp đỏ…”
> “…mọi phương tiện vận tải rút về căn cứ…”
> “…không dừng tại các cơ sở y tế có ca nhiễm…”
> “…duy trì im lặng vô tuyến…”

### Lời cuối của nhân viên Radio

> “Các anh đã nghe thấy chúng tôi.”
> “Các anh chỉ không quay lại.”
> “Nếu có ai tìm được bản ghi này… đoàn xe đã rút về căn cứ phía Bắc.”
> “Tần số đèn hiệu và tọa độ vẫn còn trong bộ nhớ máy.”
> “Tôi không biết ở đó còn ai nữa.”

Sau đó chỉ còn beacon lặp lại:

> “BRAVO–BẮC… CỔNG NAM… BRAVO–BẮC…”

Không có giọng người trả lời trực tiếp. Beacon có thể chỉ là hệ thống tự động.

## 7. Kể chuyện môi trường bằng xác chết

Chỉ dùng số lượng vừa đủ và mỗi xác phải có mục đích:

1. Xác bệnh nhân trong hành lang: bệnh viện thất thủ đột ngột.
2. Xác nhân viên/bảo vệ gần văn phòng trưởng ca: củng cố khu hành chính.
3. Xác trên đường ra trạm phụ trợ: dẫn mắt người chơi ra ngoài.
4. Xác nhân viên vận hành Radio gục cạnh console: xác nhận người gửi lời cầu cứu đã chết tại chỗ.

Các xác này là prop tĩnh dùng sprite/final death frame từ asset zombie. Không gắn AI mạng và không gắn `ZombieCorpseLoot`, tránh xung đột phím `E` với quest.

## 8. Quy tắc co-op/open world

- State ShiftLog, chìa khóa, cửa, tiến độ Radio và map fragment do State Authority giữ.
- Một thành viên hoàn thành manh mối sẽ cập nhật objective cho cả đội.
- Người ở xa không bị ép mở UI, nghe thoại hoặc dừng gameplay.
- Người ở gần Radio nghe world/local audio; transcript được lưu trong Journal để người ở xa và late join đọc lại.
- Nếu Player tìm cửa Radio trước, cửa chỉ báo đang khóa và chỉ dẫn tìm chìa tại văn phòng trưởng ca; không soft-lock vì bỏ qua ShiftLog đầu.
- Bảng chọn tuyến lần hai sau bệnh viện vẫn chỉ đổi waypoint đang theo dõi, chưa khóa ending.
- Ending B chỉ khóa tại xác nhận kích hoạt báo động ở căn cứ.

## 9. Liên kết với phần căn cứ

Sau Radio:

- Mảnh bản đồ 2 và tọa độ được khôi phục trực tiếp từ console; không có Records Cabinet.
- Map reveal riêng vùng căn cứ, giữ minimap tắt và không mở toàn bản đồ.
- Cue tiếp cận căn cứ phải dùng lại mã `BRAVO–BẮC`/cổng nam để xác nhận đây đúng là nơi đoàn xe rút về.
- Player vẫn không biết căn cứ còn người sống.
- Tới căn cứ để lấy xe, nhiên liệu và vật tư; không phải đi chờ cứu viện.
- Chỉ sau khi xác nhận bật hệ thống/báo động, Ending B mới bị khóa và siege bắt đầu.

## 10. Kế hoạch triển khai

### H1 — Cửa và vùng tương tác

- Chốt tile/sprite cửa đóng/mở.
- Làm state cửa và state gating Door/Radio.
- Xác minh không bấm xuyên tường hoặc hiện hai prompt.

### H2 — Manh mối và chìa khóa shared

- Nối ba tài liệu tới bệnh viện.
- Nối ShiftLog → ShiftLog2 → KeyLoot ngẫu nhiên → shared quest key → mở cửa.
- Đồng bộ Host/Client/disconnect/late join.

### H3 — Radio và cốt truyện

- Làm tiến độ Radio có thể chuyển người vận hành.
- Thay nội dung Cue 05–09 theo flow mới.
- Lưu transcript, trao Mảnh bản đồ 2 và reveal căn cứ.

### H4 — Cao trào và môi trường

- Bố trí bốn xác chết tĩnh làm dấu dẫn đường tới trạm phụ trợ.
- Chia Radio thành ba chặng; nối noise và hai đợt zombie chắc chắn tại hai anchor.
- Giữ lựa chọn tiếp tục sửa không cần diệt hết zombie; cân chỉnh Solo/Host/Client mà không biến thành horde vô hạn.

### H5 — QA toàn tuyến

- Đã chuyển toàn bộ interaction bệnh viện sang Polygon riêng và thêm key random authoritative.
- Đã khóa difficulty scaling/noise duration/spawn cadence bằng tests và regression production path.
- Bắt đầu từ `MainMenu → Solo/Host → Main`.
- Chạy ba tài liệu → bệnh viện → Radio → reveal căn cứ.
- Test người chơi tách đội, đổi người vận hành, disconnect và late join.
- Regression bảng chọn tuyến, Tuyến A, map reveal, xe cảnh sát và UI/modal.

## 11. Tiêu chí chấp nhận

- Player hiểu vì sao phải tới bệnh viện và vì sao phải tới căn cứ.
- Có thể tìm Radio dù nó nằm ngoài khu bệnh viện chính.
- Không cần độc thoại Player liên tục để hiểu chuyện.
- Không thể bấm Radio xuyên cửa hoặc kích hoạt sai thứ tự.
- Không soft-lock nếu tìm các điểm theo thứ tự khác, chết hoặc disconnect.
- Một người làm quest không cưỡng chế UI/gameplay của người ở xa.
- Bản ghi thể hiện quân đội đã bỏ bệnh viện theo lệnh phong tỏa nhưng không khẳng định căn cứ hiện còn người sống.
- Sau bệnh viện, động cơ tới căn cứ là lấy phương tiện tự thoát.
- Flow cũ `Bàn Điều phối → Radio → Tủ hồ sơ` không còn được dùng làm yêu cầu triển khai.
