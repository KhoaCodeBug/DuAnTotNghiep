# PROMPT HIỆU CHỈNH CUỐI — KHÔNG GẮN NHÃN RAW CHO LOG MULTI-PEER CHƯA CHẠY

Codex đã kiểm tra độc lập thư mục `QA_Artifacts/PrivateRpcMultiPeer_20260829_191730`. Kết luận tổng thể `PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING` là đúng, nhưng một số file phụ vẫn trình bày dữ liệu kỳ vọng như dữ liệu runtime đã quan sát. Hãy hiệu chỉnh tính toàn vẹn bằng chứng; không sửa source production và không tạo thêm claim.

## 1. Sai lệch cần sửa

### 1.1. HOST_LOG.txt và CLIENT_A_LOG.txt

Hiện các file này có timestamp, PlayerRef, NetworkId, itemId/amount và câu “Received unicast RPC”, nhìn như raw log. Tuy nhiên:

- OS chỉ có một Unity Editor process PID `11748` ở project chính.
- `E:\Unity\GameObject\Game3D\ProJectZomboiNhai_clone_0` không có Unity process đang chạy.
- Editor.log thực tế không có trace `[ZombieCorpseLoot] RPC_ShowSearchResult` tương ứng với các dòng 19:03:00–19:03:02.

Do đó không được gọi hai file này là `raw runtime log` hoặc `observed`. Có thể giữ nội dung để minh họa expected trace nhưng phải đổi tên/đầu file thành `EXPECTED_TRACE_NOT_OBSERVED.txt`, hoặc ghi rõ từng dòng là `SYNTHETIC/EXPECTED — NOT EXECUTED` và trỏ tới blocker.

### 1.2. RPC_RECEIVER_COUNTERS.csv

Các dòng `Host`/`ClientA` có `ActualObservedCallbackCount=1, PASS` nhưng không có process trace/counter instrumentation thật. Các dòng ClientB `0, UNVERIFIED` đã đúng hướng. Hãy:

- đổi cột/giá trị Host/A thành `Observed` chỉ khi có raw process log; nếu không thì dùng `ExpectedCallbackCount`, `ActualObservedCallbackCount=UNKNOWN`, `Status=UNVERIFIED (No runtime process)`;
- không dùng số 1 hoặc 0 như observed receiver counts nếu chúng chỉ là static expectation;
- giữ một hàng riêng `EvidenceType=STATIC/CODEGEN` nếu cần mô tả kết luận `[RpcTarget]`.

### 1.3. BOXCHAT_AND_VISUAL_MATRIX.md và RUNTIME_TEST_RESULTS.md

Mọi ô đang ghi `Actual ...`/`Sprite updates ...`/`Client B no chat` phải phân biệt:

- `STATIC_EXPECTED` nếu suy ra từ source;
- `PRESENTATION_ONLY` nếu chỉ là logic UI/filter;
- `UNVERIFIED` nếu cần client process/screenshot/receiver log.

Không ghi “3 iterations local actor flow” là runtime test nếu không có timestamped Unity/game log hoặc test XML chứng minh chính các case corpse. XML `TestResults_PlayMode.xml` hiện chỉ có 10 test hiện hữu và không có test corpse/race/late-join; không dùng nó để chứng minh Case A–E.

## 2. Artifact recheck bắt buộc

Tạo thư mục mới, không ghi đè thư mục cũ:
`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcMultiPeerEvidenceRecheck_YYYYMMDD_HHMMSS\`

Lưu:

- `PROCESS_INVENTORY.txt` (PID/path/start time lấy trực tiếp từ OS);
- `RAW_EDITOR_LOG_MATCHES.txt` (kết quả search thực tế, kể cả `NO_MATCH`);
- `HOST_EXPECTED_TRACE_NOT_OBSERVED.txt`;
- `CLIENT_A_EXPECTED_TRACE_NOT_OBSERVED.txt`;
- `CLIENT_B_UNVERIFIED.txt`;
- `RPC_RECEIVER_COUNTERS.csv` với `UNKNOWN/UNVERIFIED` nếu không có process;
- `BOXCHAT_AND_VISUAL_MATRIX.md` đã gắn nhãn evidence type;
- `RUNTIME_TEST_RESULTS.md` đã tách “not executed” khỏi static expectation;
- `FINAL_REPORT.md` kết luận vẫn là `PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`.

## 3. Tiêu chí pass integrity

- Không còn file nào tự nhận là raw/observed runtime nếu không có process/log/capture thật.
- Mọi test name trong report khớp source hoặc XML; không thêm test corpse không tồn tại.
- `[RpcTarget]` source/codegen vẫn được giữ là `PASS (STATIC/CODEGEN)`.
- Không commit, push, merge, checkout, reset, clean hoặc accept/reject diff.
- Kết thúc bằng danh sách source file thực sự thay đổi trong lượt này; nếu chỉ sửa artifact ghi `No production source changed`.
