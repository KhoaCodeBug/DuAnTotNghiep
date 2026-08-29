# Checklist test tổng sau P0/P1/P2

Chạy trên branch `codex/p0-verification-docs-20260829`. Trước mỗi nhóm network,
thoát Play sạch và không sửa asset khi đang Play. Nếu lỗi, giữ `Editor.log`, ảnh/video,
tên room, vai trò Host/Client, số người và thời điểm xảy ra.

## 1. Smoke P0 — Solo và hai Route

1. MainMenu → Solo → Main ở cả Easy, Normal và Hard.
   - Kỳ vọng: loading tăng đơn điệu, không hiện prompt gameplay phía sau overlay;
     Player/HUD/loadout xuất hiện rồi mới giải phóng gameplay.
   - Easy: AK47 + 30 Ammo762 + 1 Meat; Normal: Flashlight + Bandage; Hard: không starter item.
2. Route A: chơi/sử dụng shortcut hợp lệ tới sửa xe dân sự và thoát.
   - Kỳ vọng: quest không kẹt, ending/fade chỉ chạy một lần.
3. Route B: dùng F1 vào chương quân sự, đi tới sửa xe/cổng và Ending B.
   - Kỳ vọng: xe đi `EndB1 → EndB2 → EndB3 → EndBFinal → EndBFinal2`, camera 6 giây,
     giữ 2 giây rồi fade; không mất quyền điều khiển vĩnh viễn.
4. Quan sát đoạn dùng vị trí xe hỏng.
   - Kỳ vọng hiện tại: fallback hoạt động dù Scene chưa có marker `ViTriXeChetMay`.

## 2. P1 — Loading và difficulty authoritative (Host + Client)

1. Trên Client lưu local Easy; Host tạo room Hard rồi Start.
   - Kỳ vọng: Client nhận Hard từ Host, không dùng Easy local; không starter item,
     loot/damage/zombie multiplier theo Hard.
2. Đảo chiều: Client local Hard; Host tạo room Easy.
   - Kỳ vọng: Client nhận Easy; có đúng ba starter item Easy.
3. Cả hai lần theo dõi loading.
   - Kỳ vọng: Host ghi readiness 2/2, cả hai vào gameplay; không Fusion Timeout.
   - Với bản hiện tại, config runtime phải ghi `ConnectionTimeout=120.0`.
4. Thử Client join sau khi Host đã khóa/bắt đầu room.
   - Kỳ vọng: không chen vào room đang chạy dưới trạng thái sai; UI báo lỗi/không tìm thấy
     rõ ràng, không treo loading.

## 3. P1 — Corpse loot privacy/race (Host + 2 Client)

1. Cho Client A lục một xác có loot; Client B và Host đứng cạnh, mở BoxChat.
   - Kỳ vọng: chỉ A nhận đúng một system message vàng có item/số lượng.
   - B và Host không thấy nội dung riêng của A.
   - Inventory chỉ A tăng; trạng thái xác đã lục đồng bộ trên mọi peer.
2. A và B giữ E/cùng hoàn tất lục một xác gần như đồng thời.
   - Kỳ vọng: đúng một người được grant; người còn lại nhận “đã bị lục”; không duplicate item.
3. Lấp đầy inventory A rồi lục xác có loot.
   - Kỳ vọng: chỉ A nhận “túi đầy”; xác chưa bị consume và B vẫn có thể lục thành công.
4. Lục xác rỗng.
   - Kỳ vọng: người đầu nhận kết quả rỗng và xác bị consume cho toàn bộ peer.
5. Cho Client C join/reconnect trước khi xác đã lục despawn.
   - Kỳ vọng: C thấy canonical searched state, không thể grant lại.
6. Thử request ngoài tầm hoặc giả requester nếu có dev harness.
   - Kỳ vọng: Host từ chối; không đổi inventory/state và không lộ private message.

## 4. P1 — Readiness/extraction/respawn với 5–10 Player

1. Xác nhận 10 vị trí spawn không chồng nhau và lobby hiển thị đủ lưới 2x5.
2. Ở bước thoát quân sự, trộn ba nhóm:
   - ngồi đúng xe cảnh sát;
   - đứng bộ cách xe `<= 6m`;
   - ngồi xe khác hoặc đứng `> 6m`.
   - Kỳ vọng: hai nhóm đầu ready; hai nhóm sau không ready.
3. Đúng lúc Host bấm W, cho một Client disconnect và một Player chết.
   - Kỳ vọng: authority tính lại tập active/ready, không deadlock hoặc chạy ending hai lần.
4. Với người không có ghế, kiểm tra virtual follower dưới latency thực.
   - Kỳ vọng: theo đoàn/được tính đúng, không teleport rung hoặc bị bỏ lại khỏi ending.
5. Kiểm tra team respawn sau 10 giây:
   - 2–4 Player: 3 lượt; 5–6: 5; 7–8: 6; 9–10: 8.
   - Kỳ vọng: mỗi lần respawn chỉ trừ một lượt, không âm; inventory/weapon snapshot đúng.

## 5. P2 — Horde Profiler và soak

1. Chạy finale với 5–10 Player, mở Unity Profiler trước khi siege bắt đầu.
2. Xác nhận tier lớn:
   - nearby target `80` zombie;
   - mỗi batch `24` zombie (`4 điểm × 6`);
   - hard safety cap `112`.
3. Capture ít nhất ba đoạn: trước siege, khi đạt khoảng 80, và gần cap 112.
   - Ghi CPU main-thread/frame time, Rendering, GC Alloc/frame, memory, network bandwidth,
     average FPS và 1% low FPS.
   - Kỳ vọng chức năng: số zombie không vượt 112, không spawn vô hạn, quest/repair/network
     vẫn tiến triển, không có GC spike lặp vô hạn hoặc memory tăng không hồi phục.
4. Soak 60 phút qua combat, chết/respawn, loot, chat và disconnect/reconnect.
   - Kỳ vọng: không crash, timeout giả, duplicate authority event, kẹt loading/quest,
     hoặc memory/network usage tăng liên tục không ổn định.

## Điều kiện nghiệm thu

- Gửi lại kết quả theo từng mục `PASS/FAIL`, kèm ảnh/video/log cho mọi FAIL.
- Automated pass không thay thế các mục live bên trên.
- Không push cho tới khi các case bạn thực sự chạy đã đạt hoặc phần còn thiếu được chấp nhận rõ.
