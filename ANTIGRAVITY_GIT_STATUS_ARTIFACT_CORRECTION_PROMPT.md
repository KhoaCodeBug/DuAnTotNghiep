# PROMPT SỬA CÂU GIT STATUS TRONG ARTIFACT — KHÔNG ĐỤNG SOURCE

Artifact `QA_Artifacts/PrivateRpcMultiPeerEvidenceRecheck_20260829_192250/FINAL_REPORT.md` hiện kết luận “Git Status: Sạch”. Codex đã kiểm tra độc lập và working tree **không sạch**: branch đang ở `main...origin/main`, có nhiều file source modified từ trước, nhiều prompt/artifact untracked; điều đúng là **không có commit/push/merge được thực hiện**, không phải working tree clean.

Hãy tạo thư mục mới `QA_Artifacts/GitStatusEvidenceRecheck_YYYYMMDD_HHMMSS/` và sửa artifact/report theo các yêu cầu:

1. Chạy `git status --short --branch` trực tiếp trong project, lưu nguyên output vào `ACTUAL_GIT_STATUS.txt`.
2. Chạy `git log -1 --oneline --decorate` và lưu vào `HEAD_INFO.txt`; không tạo commit.
3. Ghi `git diff --check` riêng; phân biệt warning LF→CRLF với lỗi whitespace.
4. Cập nhật kết luận chính xác:
   - `Working tree: DIRTY (pre-existing/user changes plus audit artifacts)`.
   - `Branch: main tracking origin/main`.
   - `Commit/push/merge/reset/checkout: NOT PERFORMED`.
   - `Production source changed in this Git-status-only recheck: NONE`.
5. Không được dùng chữ “Git Status: Sạch” hoặc “0 files modified”. Nếu muốn nói patch không thay đổi source trong lượt recheck, viết đúng là `No production source changed in this recheck; repository remains dirty.`
6. Không accept/reject diff, không sửa source, không xóa artifact, không commit/push/merge.
7. Cập nhật `FINAL_REPORT.md` mới để link tới `ACTUAL_GIT_STATUS.txt`, `HEAD_INFO.txt`, `DIFF_CHECK.txt` và giữ kết luận chung `PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`.
