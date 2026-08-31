# Prompt to continue in a new Codex task

Tiếp tục tính năng Indoor Fog Surface V2 trong Unity project hiện tại. Đây là task kế thừa; không bắt đầu lại và không tự mở rộng phạm vi.

Trước khi sửa code, hãy đọc `AGENTS.md`, các project skills Unity, phần mới nhất của `CODEX_PROJECT_WORK_LOG.md`, `QA_Artifacts/IndoorFogPrototype_20260831/README.md` và `QA_Artifacts/IndoorFogPrototype_20260831/NEXT_SESSION_HANDOFF.md`. Sau đó kiểm tra Git, diff hiện tại, Unity state và Console để xác minh tài liệu.

User đã test tay V1 và đánh giá khá ổn nhưng phát hiện hai lỗi phải xử lý:

1. Biên chuyển từ vùng sáng sang vùng tối còn cứng, chưa mềm như concept đã duyệt.
2. Khi Player tiến gần một số tường xuất hiện vệt đen; khi di chuyển gần tường các vệt có thể nhấp nháy liên tục.

User muốn tiếp tục hoàn thiện. Ưu tiên xác định và sửa nguồn gây vệt/nhấp nháy trước khi thêm feather/blur. Không coi các giả thuyết trong handoff là nguyên nhân đã xác nhận. Giữ prototype ở đúng căn nhà mẫu; không sửa scene/prefab hoặc mở rộng toàn map nếu chưa có bằng chứng và phê duyệt.

Làm theo từng bước nhỏ. Sau mỗi bước, chụp ảnh Game View thật trong Unity tại pose cố định, đối chiếu với concept đã duyệt và ảnh lỗi V1 trong `V1_UserReview_20260831/`, rồi tự ghi đánh giá: cải thiện gì, còn lệch gì, có leak qua tường hay nhấp nháy không. Với lỗi theo thời gian, cần chuỗi frame hoặc quan sát chuyển động chứ không chỉ ảnh tĩnh. Đo lại hiệu năng cùng điều kiện A/B/A.

Giữ checkpoint gốc `ddf440424` nguyên vẹn. Không reset/clean, không commit/push/merge ngoài phạm vi được phép. Khi bàn giao, cập nhật work log với trạng thái đã chẩn đoán/đã triển khai/đã test tự động/đã test tay tách biệt, nêu test thực tế, kết quả mong đợi, rủi ro và Git.

