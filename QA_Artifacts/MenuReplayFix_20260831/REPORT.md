# Sửa vào lại game bị nền MainMenu che — 2026-08-31

- User đã cho phép sửa nhanh lỗi này để test tay Fog. Giữ nguyên toàn bộ vision đã phục hồi.
- Nguyên nhân: `GameplayReadinessCoordinator` giữ trạng thái static ReleasedToGameplay khi Domain Reload tắt. `ShowLoadingScreen` thấy trạng thái lượt trước và return trước khi khởi chạy coroutine ẩn menu. Tạo runner mới cũng chưa reset coordinator/flags trước callbacks.
- Thêm reset static state, events, Canvas registry ở SubsystemRegistration; reset attempt sau cleanup runner cũ và trước StartGame. Giữ guard chống callback Client đến trễ mở lại loading trong cùng trận.
- Hotbar không được tạo ngoài Play/khi quitting; cờ shutdown reset mỗi runtime. Menu OnDestroy chỉ cleanup, không bật lại HUD có thể chạy Awake lúc đang đóng scene. AutoUI dùng null-safe access khi khởi tạo Hotbar.
- Không thay ProjectSettings, scene, prefab, Fog, shader, inventory, quest, State Authority, RPC hay điều kiện readiness/release. Không dùng bật Domain Reload để che lỗi.

## Xác minh

- Compile thành công; `git diff --check` pass.
- Job `5ee7bb969f064115b86973ccafcb234f`: **1/1 EditMode integration test passed**, 25.8672943s. Test dùng EnterPlayMode/ExitPlayMode ba lần liên tiếp với Domain Reload vẫn tắt; mỗi lần mở Solo Medium qua Button events, đợi Player và readiness, assert menu Canvas đã tắt, chụp ảnh, thoát Play và assert không còn Hotbar. Không compile giữa các vòng.
- Đã mở xem `replay-1.png`, `replay-2.png`, `replay-3.png`: đều là map thật cùng HUD, không còn nền MainMenu che.
- Job `c076cdddbcbc4b67bec057f2a0f4655d`: **43/43 ReadinessAndChatEditorTests passed**, gồm guard không reopen loading sau Client/late join đã released, Failed terminal và require-local-ready gate.
- Desktop: click Solo → Medium → Enter đã vào map thật ở lượt đầu; Stop không có error. Lượt click kế tiếp bị vấn đề focus của công cụ (EventSystem báo isFocused=false), nên không ghi là ba lượt test tay thành công. Ba vòng lặp nêu trên là automation chạy Play thật, không phải thao tác chuột.
- Sau ca ba vòng không có gameplay Error hoặc cảnh báo Hotbar sót; tool có log runner `Saving results to...` được phân loại Exception.
- XML: `three-play-cycles.xml`, `readiness-tests.xml`. Backup trước sửa: `before-menu-replay-fix.zip`.

## Bàn giao

- Test tay: từ MainMenu bấm Play → Solo → chọn độ khó → vào game; chờ loading kết thúc. Mong đợi Player/map/HUD hiện và nền menu biến mất. Stop rồi Play lại ít nhất hai lần, không restart Unity/recompile; kết quả phải giống nhau và không sinh Hotbar ngoài Play.
- Chưa chạy Host+Client, build hoặc full suite sau hotfix; 173/15 trong báo cáo Fog trước thuộc checkpoint trước hotfix. Chưa coi automation là nghiệm thu của người dùng.
- Git: `codex/restore-indoor-vision-20260831`, HEAD `a23c33247`, sửa local chung với rollback vision; chưa stage/commit/push/merge.
- Hiểu biết Fog đã đối chiếu: Fog/thời tiết ngoài trời; indoor fan 180 tia/15Hz đúng công trình, tường lớn có thể ở ngoài hierarchy mái; flashlight/doorway; awareness zombie 1.5m, fade và X-Ray local; fallback mái trường chia polygon. Những lỗi chưa được mô tả hoặc chưa có bằng chứng runtime vẫn cần tái hiện trước khi cải thiện tiếp.
