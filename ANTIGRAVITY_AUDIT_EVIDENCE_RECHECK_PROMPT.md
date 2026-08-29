# PROMPT GỬI ANTIGRAVITY — EVIDENCE RECHECK SAU BÁO CÁO V0–V7

Bạn vừa trả báo cáo `READY/PASS` nhưng Codex kiểm tra độc lập thấy thư mục `QA_Artifacts` hiện tồn tại nhưng không có file log/screenshot nào, trong khi báo cáo lại trích dẫn hàng chục đường dẫn `QA_Artifacts/*.log`. Vì vậy phải recheck tính trung thực của bằng chứng trước khi dùng báo cáo để kết luận dự án đạt.

## Quy tắc bắt buộc

1. Đây là phiên kiểm toán và sửa báo cáo, không phải phiên sửa game. Không sửa C#/scene/prefab/ScriptableObject/package/project setting/save format; không commit, stage, push, pull, merge, reset hoặc clean Git. Không tự sửa source dù phát hiện lỗi. Chỉ được tạo thư mục/file bằng chứng dưới `QA_Artifacts/FullGameAudit_<timestamp>/`.
2. Không được giữ trạng thái `PASS` nếu chỉ suy ra từ đọc code, test cũ, hoặc câu mô tả không có artifact mới. Mỗi case phải là đúng một trạng thái `PASS`, `FAIL`, `FIXED`, `PARTIAL` hoặc `UNVERIFIED` và phải có đường dẫn thật tồn tại trên đĩa.
3. Kiểm tra trực tiếp bằng PowerShell/Unity MCP: `TestResults.xml`, `Editor.log`, Console, Git status/diff, Build Settings, scene/prefab/reference và thư mục artifact. Ghi full path, timestamp bắt đầu/kết thúc, job ID, test name, result và hash/kích thước file nếu có. Phân biệt job hiện tại với mọi kết quả cũ.
4. Nếu một test chạy được nhưng không sinh ảnh/video/log receiver/non-receiver thì không được gọi đó là bằng chứng runtime đầy đủ; hạ xuống `PARTIAL`/`UNVERIFIED` và ghi chính xác phần đã kiểm tra.
5. Không tuyên bố đã soak 30–60 phút, live dual-GUI ParrelSync, 5–10 peer, 4K/720p, audio hoặc manual production flow nếu không có timestamp/evidence mới tương ứng. MCP chỉ điều khiển được một HTTP bridge thì V7/M09 live GUI phải là `PARTIAL`; test logic multi-runner không thay thế được dual-GUI.

## Việc phải làm ngay

### A. Đối chiếu báo cáo cũ

- Lập bảng `reported artifact path | tồn tại? | kích thước | timestamp | nội dung có thể kiểm tra? | case liên quan` cho mọi `QA_Artifacts/...` đã nêu.
- Nếu đường dẫn không tồn tại, sửa lại report; tuyệt đối không tạo log giả để khớp câu chữ.
- Đối chiếu con số 144 EditMode/10 PlayMode với file kết quả thật. Nếu chỉ có `C:/Users/triti/AppData/LocalLow/DefaultCompany/DuAnTotNghiep/TestResults.xml`, ghi rõ đây là file nào, timestamp nào, test names nào và không gán nhầm cho một job khác.

### B. Chạy lại phần có thể chứng minh

- Chạy compile/Console check và toàn bộ EditMode/PlayMode qua Unity MCP với job ID mới; lưu raw output và kết quả XML vào artifact.
- Chạy critical smoke Solo Easy/Normal/Hard, loading gate, UI không đè hotbar, locale VI↔EN, corpse/container loot, inventory 15→50 + 5 hotbar, backpack L1–L5, difficulty authority và message routing. Chỉ ghi `PASS` khi có steps, repetitions, expected/observed, timestamps và log path mới.
- Với corpse loot multiplayer: A nhận item/amount thì chỉ A thấy; B/C chỉ thấy corpse state; race chỉ grant một lần; empty/full/too-far chỉ actor thấy; late join không replay; Host VI/Client EN dịch độc lập. Phải ghi receiver và non-receiver bằng chứng, không suy ra từ tên RPC.
- Kiểm tra V1–V7 theo khả năng môi trường. Case không thực hiện được phải `PARTIAL`/`UNVERIFIED`, nêu blocker và bước tiếp theo; không được lấp bằng số liệu ước đoán.

### C. Quy tắc kết luận

- Nếu phát hiện lỗi, dừng scenario theo severity, ghi một giả thuyết root cause duy nhất, file/line/call-site, repro và artifact rồi báo Codex; không sửa.
- Nếu không có lỗi nhưng còn thiếu bằng chứng, kết luận phải là `NOT READY / EVIDENCE INCOMPLETE`, không phải `READY`.
- Cuối báo cáo tạo các file thật: `FULL_GAME_AUDIT_REPORT.md`, `BASELINE.md`, `TEST_RESULTS.md`, `MESSAGE_ROUTING_MATRIX.md`, `GIT_STATUS.txt` và mọi screenshot/log thực tế dưới `QA_Artifacts/FullGameAudit_<timestamp>/`. In danh sách bằng chứng tồn tại bằng full path.
- Báo cáo cuối phải có: Executive Summary trung thực; Environment; Skill Trace; coverage PASS/FAIL/FIXED/PARTIAL/UNVERIFIED theo V0–V7; Defect Ledger; routing matrix; performance evidence; unverified risks; Git status. Xác nhận audit-only không có source change.

Hãy bắt đầu bằng kiểm tra thư mục artifact và file kết quả thật. Không trả lời `READY/PASS 100%` cho tới khi từng con số và đường dẫn đã được đối chiếu trên đĩa.
