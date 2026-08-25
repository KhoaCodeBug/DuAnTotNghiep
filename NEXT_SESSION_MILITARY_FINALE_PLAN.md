# Next session — Military finale design and implementation plan

> Bắt buộc: phiên tiếp theo phải đọc `HOSPITAL_H1_H5_IMPLEMENTATION_README.md`, file này và code hiện tại; sau đó thảo luận lại với chủ dự án để xác nhận thiết kế trước khi thay đổi gameplay căn cứ.

## Điểm bắt đầu canonical

Player rời bệnh viện với Mảnh bản đồ 2 và tọa độ beacon `BRAVO–BẮC / CỔNG NAM`. Bản ghi chỉ nói đoàn xe từng rút về căn cứ, không xác nhận còn người sống. Hai hướng thoát vẫn mở cho tới khi Player xác nhận bật báo động tại căn cứ.

Code hiện có đã có prototype cho tiếp cận căn cứ, bảng xác nhận point-of-no-return, siege/gate, generator, ba vật phẩm, armory/safe, xe sơ tán và extraction. Không được xem prototype hiện tại là thiết kế cuối nếu chưa audit scene và được chủ dự án duyệt.

## Các quyết định phải hỏi và chốt trước khi code

1. **Core finale:** giữ mechanic ba vật phẩm cũ hay chuyển sang minigame sửa xe năm hạng mục đã thử ở xe cảnh sát?
2. **Nguồn vật phẩm:** loot container, vị trí cố định, kho mở theo generator, zombie drop hay kết hợp?
3. **Trình tự bắt buộc:** báo động → siege → generator → kho/linh kiện → sửa xe → extraction, hay cho phép làm song song?
4. **Vai trò co-op:** một người sửa/một người thủ; có cho nhiều người góp cùng hạng mục không; chết/disconnect operator xử lý thế nào?
5. **Siege:** số wave, spawn anchor, scaling theo độ khó/số người gần căn cứ, điều kiện fail khi cổng vỡ hoặc toàn đội chết.
6. **Generator:** hold interaction hay skill-check; có cần nhiên liệu/part; tác dụng chính xác lên cổng/đèn/kho.
7. **Xe sơ tán:** asset/direction/vị trí cuối, năm hạng mục cụ thể, thời gian/skill-check, điều kiện tập hợp đội để rời đi.
8. **Point of no return:** copy cảnh báo, quyền xác nhận Host hay bất kỳ Player, vote hay click một người, cách khóa Tuyến A cho late join.
9. **Audio Cue 10–15:** nội dung nào còn đúng, file nào cần thu lại, thứ tự và phạm vi nghe local/world/team.
10. **Mức polish:** cinematic camera, alarm/light/VFX, UI siege, boss/horde, Victory Summary và thống kê.

## Audit scene trước implementation

Phải kiểm tra và chụp lại vị trí thật của:

- Cổng nam/trigger tiếp cận.
- Bảng xác nhận báo động.
- Hai hoặc nhiều spawn entrance của zombie.
- Cổng phòng thủ và collider.
- Generator.
- Safe/armory/cache.
- Xe sơ tán và vùng sửa.
- Điểm extraction/camera finale.

Không tự di chuyển anchor hoặc tạo layout mới khi chưa được chủ dự án xác nhận.

## Milestone đề xuất

### M0 — Design lock

- Audit code + scene + audio 10–15.
- Trình bày 2–3 phương án core finale với trade-off.
- Chủ dự án chốt state machine, co-op ownership, difficulty scaling và acceptance criteria.
- Cập nhật một file design lock trước khi code.

### M1 — Arrival và point of no return

- Waypoint/cổng nam rõ ràng.
- Cue tiếp cận không khẳng định còn quân đội sống.
- Bảng xác nhận nói rõ khóa Tuyến A cho toàn đội.
- State Authority xác thực vị trí/quyền xác nhận; late join thấy đúng locked route.

### M2 — Siege và generator

- Spawn authoritative, bounded, không nhân theo client ở xa.
- Gate HP/fail state replicate.
- Generator interaction có thể handoff, không khóa đồng đội.
- Test death/disconnect/operator handoff.

### M3 — Loot/repair vehicle

- Chốt và triển khai mechanic ba part hoặc năm hạng mục.
- Inventory/loot transaction authoritative nếu có.
- Progress độc lập từng hạng mục, late join/handoff đúng.
- UI chỉ khóa active repairer.

### M4 — Extraction và story closure

- Điều kiện tập hợp đội, cutscene và shutdown an toàn.
- Cue 14/15 đúng thứ tự.
- Victory Summary không mở sớm và chỉ Journal Completed sau extraction.

### M5 — Multiplayer QA và balance

- Solo Easy/Normal/Hardcore.
- Host + Client, tách đội, reconnect, late join, Host migration/disconnect nếu kiến trúc hỗ trợ.
- Gate break, toàn đội chết, operator chết, inventory mất/reset.
- Regression Tuyến A, hai bảng tracking, map, modal/audio và trở về MainMenu.

## Acceptance chung

- Không action nào của Client tự sửa state gameplay.
- Người ở xa không làm tăng horde hoặc bị ép modal/audio.
- Không wave vô hạn, không duplicate reward/part, không soft-lock khi operator rời game.
- Chỉ xác nhận point-of-no-return mới khóa Ending B.
- Không khẳng định căn cứ còn người sống trước khi Player có bằng chứng trực tiếp.
- Mỗi milestone bàn giao phải có kết quả tự động, test tay, kết quả mong đợi và kế hoạch kế tiếp.

## Prompt cho phiên tiếp theo

> Đọc đầy đủ `HOSPITAL_H1_H5_IMPLEMENTATION_README.md`, `NEXT_SESSION_MILITARY_FINALE_PLAN.md`, `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` và code military hiện tại. Trước khi code, audit scene/prototype và thảo luận lại với tôi để chốt: core finale 3 part hay 5 hạng mục, trình tự siege/generator/loot/repair, co-op ownership, scaling và point-of-no-return. Không tự quyết định hoặc di chuyển anchor.
