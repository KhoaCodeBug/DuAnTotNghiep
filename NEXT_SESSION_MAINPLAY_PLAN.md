# MainPlay — Kế hoạch phiên tiếp theo

> Cập nhật: 2026-08-25
> Ưu tiên hiện tại: triển khai chương bệnh viện mới đã khóa trong `HOSPITAL_ROUTE_DESIGN_LOCK.md`.
> Không bắt đầu bằng flow cũ Bàn Điều phối → Radio → Tủ hồ sơ và không chờ LootContainer bệnh viện.

## Tài liệu bắt buộc đọc

1. `HOSPITAL_ROUTE_DESIGN_LOCK.md` — nguồn canonical cho thiết kế bệnh viện mới.
2. `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` — trạng thái toàn Tuyến B và ranh giới với căn cứ.
3. `README_MAINPLAY_CODEX_HANDOFF.md` — những gameplay đã thực sự triển khai.

## Trạng thái bàn giao

- Chủ dự án đã đặt trong scene `Main`:
  - `HospitalQuest_ShiftLog` tại quầy tiếp tân.
  - `HospitalQuest_ShiftLog2` tại văn phòng ngay sau tiếp tân.
  - `HospitalQuest_RadioRoom/DoorInteraction`.
  - `HospitalQuest_RadioRoom/RadioInteraction`.
  - `HospitalQuest_ZombieEntry_A/B`.
- Chủ dự án xác nhận đường từ văn phòng ra thẳng phòng Radio không bị collider/tilemap chặn.
- Phòng Radio nhỏ; Door/Radio phải dùng vùng nhỏ, state gating và xác thực server để không tương tác xuyên tường.
- Thiết kế mới chưa có code, component, tile cửa, audio mới hoặc test.
- Runtime/debug hiện vẫn chạy flow marker cũ; không được mô tả flow mới là đã hoạt động.
- Working tree có thay đổi scene/map và công cụ môi trường của người dùng/thành viên khác. Không restore, không gom vào commit bệnh viện nếu không thuộc phạm vi được xác nhận.

## H1 — Cửa và vùng tương tác

Mục tiêu: tạo nền cửa Radio đúng và kiểm chứng căn phòng nhỏ trước khi nối quest.

- Kiểm kê tile/sprite cửa đóng và cửa mở có sẵn; ưu tiên asset hiện có.
- Cửa bắt đầu đóng, có collider chặn thật.
- `DoorInteraction` chỉ hoạt động khi cửa đóng.
- `RadioInteraction` vô hiệu tuyệt đối khi cửa chưa mở.
- Sau khi mở: đổi hình cửa, tắt collider, tắt Door và bật Radio.
- Vùng cửa khuyến nghị `0,55–0,7`; vùng Radio `0,45–0,6`, điều chỉnh theo PlayMode thật.
- State Authority kiểm tra state và khoảng cách; client không tự mở cửa.
- Không thay đổi flow quest ở checkpoint này ngoài test harness tối thiểu nếu cần.

**Bài test bàn giao H1:**

- Solo/Host không thể bấm Radio qua cửa.
- Không xuất hiện hai prompt cùng lúc.
- Mở cửa xong mới tiếp cận được Radio.
- Client/late join nhìn đúng trạng thái cửa.

## H2 — Manh mối và chìa khóa shared

Mục tiêu: chạy được chuỗi bệnh viện tới lúc mở phòng Radio.

- Nối đủ ba tài liệu khu dân cư tới objective bệnh viện mới.
- `HospitalQuest_ShiftLog` cho biết trạm liên lạc nằm phía sau bệnh viện và chìa ở văn phòng trưởng ca.
- `HospitalQuest_ShiftLog2` trao shared quest key và lệnh phong tỏa.
- Chìa khóa không chiếm inventory, không mất khi chết/disconnect.
- Journal + waypoint dẫn tới `HospitalQuest_RadioRoom`.
- Nếu Player tìm cửa Radio trước, prompt chỉ dẫn về văn phòng trưởng ca; không soft-lock.
- Mở cửa cập nhật toàn đội authoritative.

**Bài test bàn giao H2:**

- Chạy cả đường canonical ShiftLog → ShiftLog2 → cửa.
- Thử tìm cửa trước ShiftLog.
- Hai client tách đội; một người lấy chìa, người còn lại mở được cửa.
- Disconnect người lấy chìa và late join không làm mất state.

## H3 — Radio, lời thoại và map reveal

Mục tiêu: hoàn tất logic cốt truyện bệnh viện chưa có zombie cao trào.

- Radio cần khoảng 18 giây để khôi phục.
- Chỉ một người vận hành; thả/rời vùng giữ tiến độ; người khác tiếp tục được.
- Không khóa UI/gameplay của đồng đội ở xa.
- Viết lại Cue 05–09 theo nội dung canonical trong design lock.
- Người gần Radio nghe audio; transcript lưu Journal cho người ở xa/late join.
- Radio trao trực tiếp tọa độ/Mảnh bản đồ 2; không tạo Records Cabinet.
- Giữ thứ tự: reward → map mở/reveal riêng căn cứ → map đóng → cinematic → bảng chọn tuyến lần hai.
- Bảng chọn lần hai vẫn chỉ đổi waypoint; chưa khóa ending.
- Cập nhật F6/F12 và PlayMode test theo flow mới, bỏ giả lập Tủ hồ sơ.

**Bài test bàn giao H3:**

- Đổi người vận hành giữa chừng không mất tiến độ.
- Cue không phát trùng và không ép người ở xa dừng gameplay.
- Late join có đúng transcript, map fragment và stage.
- Marker bệnh viện bị gỡ, marker căn cứ xuất hiện, minimap vẫn tắt.

## H4 — Cao trào và kể chuyện môi trường

Mục tiêu: thêm căng thẳng mà không biến bệnh viện thành horde bắt buộc.

- Bố trí tối đa bốn xác kể chuyện có chủ đích.
- Xác là prop tĩnh từ sprite/death frame zombie, không có AI và không có `ZombieCorpseLoot`.
- Thêm dấu dẫn đường vừa đủ tới Trạm Radio: bảng, đèn, cáp hoặc xác trên lối đi.
- Radio phát noise để gọi zombie đang tồn tại.
- Chỉ dùng `HospitalQuest_ZombieEntry_A/B` khi vùng quá trống.
- Nhóm dự phòng spawn một lần, cân theo số Player ở gần bệnh viện và có giới hạn cứng.
- Không khóa cửa, không ép tập hợp toàn đội, không wave vô hạn.

**Bài test bàn giao H4:**

- Solo có thể ngừng vận hành để chiến đấu rồi tiếp tục.
- Co-op cho phép một người vận hành và đồng đội phòng thủ.
- Người ở xa không làm tăng sai số zombie.
- Kết thúc Radio không để lại spawner/event chạy lặp.

## H5 — QA toàn tuyến và tài liệu

- Luôn bắt đầu `MainMenu → Solo/Host → Main`.
- Chạy: kiểm tra xe → ba tài liệu → bệnh viện → ShiftLog → ShiftLog2/key → Door → Radio → map reveal → căn cứ.
- Test Solo, Host/Client, tách đội, đổi operator, nhận sát thương, chết, disconnect và late join.
- Regression Tuyến A, bảng chọn tuyến, Journal, map, audio/modal, xe cảnh sát và ending lock.
- Chỉ sau extraction mới đánh dấu Journal hoàn tất.
- Cập nhật lại `README_MAINPLAY_CODEX_HANDOFF.md` bằng những gì thực sự đã triển khai và kết quả test thật.
- Cập nhật `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` để bỏ nhãn “runtime flow cũ” khi H1–H5 đã đạt.

## Backlog sau bệnh viện

1. Quyết định finale căn cứ dùng ba vật phẩm + giữ `E` cũ hay minigame năm hạng mục.
2. Tích hợp gameplay sửa xe vào căn cứ quân sự sau khi bố cục/asset được chốt.
3. QA siege, generator, cổng, late join và extraction.
4. QA thủ công Host + Client cho trạm sửa xe cảnh sát ven đường.
5. Silhouette local Player và sprite xe nhiều hướng là phần mở rộng sau gameplay chính.

## Prompt mở chat triển khai

> Đọc đầy đủ `HOSPITAL_ROUTE_DESIGN_LOCK.md`, `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` và `NEXT_SESSION_MAINPLAY_PLAN.md`. Chương bệnh viện mới đã khóa thiết kế nhưng chưa triển khai; runtime/debug vẫn là flow cũ. Bắt đầu đúng checkpoint H1: kiểm kê cửa hiện có, làm cửa đóng/mở và state gating Door/Radio trong căn phòng nhỏ. Không sửa hoặc di chuyển các anchor người dùng đã đặt, không restore thay đổi scene/map hiện có, chưa nối toàn flow hoặc làm cao trào trước khi H1 được kiểm tra trực tiếp trong Unity.
