# PROMPT RECHECK — HIỆU CHỈNH BẰNG CHỨNG PRIVATE RPC, KHÔNG CLAIM QUÁ MỨC

Bạn vừa hoàn tất patch P2 cho `ZombieCorpseLoot.RPC_ShowSearchResult`. Codex đã kiểm tra độc lập source và artifact. Patch `[RpcTarget]` hiện đúng về mặt static/codegen; tuy nhiên báo cáo hiện tại đang có một số claim không khớp bằng chứng vật lý. Hãy recheck và sửa **báo cáo/artifact**, đồng thời chỉ sửa source nếu kiểm chứng cho thấy thật sự cần.

## 1. Các sai lệch đã được xác định — phải xử lý, không được bỏ qua

### 1.1. Test name không tồn tại

`QA_Artifacts/PrivateRpcFix_20260829_190130/RUNTIME_ROUTING_EVIDENCE.md` và `FINAL_REPORT.md` đang ghi:

- `NetworkAuthorityRegressionTests.CorpseLootRaceBetweenTwoPeersGrantsOnlyOnce`
- `NetworkAuthorityRegressionTests.LateJoinerReceivesFullSnapshotWithoutRollback`

Codex đã đọc trực tiếp `Assets/Script/Tin/Prototype/Tests/PlayMode/NetworkAuthorityRegressionTests.cs`: class này hiện chỉ có 4 test:

- `HostModeSpawner_Rejects_Spoofed_Spawn_And_Loading_Ack`
- `HostModeSpawner_Readiness_Deduplicates_Acks_Correctly`
- `ArrivalStoryBootstrap_Provides_Ten_Unique_Spawn_Positions`
- `Bandage_Request_Identifies_Wound_And_Consumes_Single_Item`

Không có hai test corpse/race/late-join nói trên. Hãy xóa tên test không tồn tại khỏi báo cáo hoặc đánh dấu `UNVERIFIED/NOT RUN`; tuyệt đối không gọi chúng là PlayMode evidence.

### 1.2. Không có bằng chứng runtime dual-peer/packet capture

File `RUNTIME_ROUTING_EVIDENCE.md` hiện chỉ là bảng narrative 27 dòng, không có raw per-peer log, packet capture, receiver counter, timestamp từng case, hoặc trace ParrelSync. Unity hiện chỉ có một Editor process active; không được biến suy luận từ `[RpcTarget]` thành quan sát runtime B/C.

Phân loại bắt buộc:

- Static declaration + Fusion codegen parity: `PASS — STATIC/CODEGEN` nếu đã compile/weave sạch.
- B/C không nhận payload ở tầng transport: `UNVERIFIED` nếu chưa có dual-peer instrumentation/capture thật.
- BoxChat không hiện ở B/C: `PRESENTATION ONLY` nếu chỉ kiểm tra filter/UI.
- Global corpse visual và late-join: `UNVERIFIED` nếu chưa chạy host + client thật và chưa có log/screenshot tương ứng.
- Race/duplicate grant: `UNVERIFIED` nếu không có test/runtime thật; không lấy tên test không tồn tại làm bằng chứng.

### 1.3. Log compile/warning phải phản ánh log thật

`UNITY_COMPILE_AND_CONSOLE.log` hiện ghi `0 errors, 0 warnings`. Hãy đối chiếu Editor.log có timestamp sau thời điểm patch/test. Các cảnh báo `VoiceNetworkObject.RecorderInUse.TransmitEnabled is false` đã xuất hiện trong `C:\Users\triti\AppData\Local\Unity\Editor\Editor.log` trong phiên chạy này. Nếu chúng là cảnh báo expected do push-to-talk tắt, ghi rõ `expected/non-blocking`; không được ghi “0 warnings” một cách tuyệt đối. Nếu không thể xác định warning thuộc phiên hiện tại, ghi `warning status not independently attributable`.

### 1.4. Kích thước artifact phải lấy từ disk

Artifact thực tế lúc recheck:

- `QA_Artifacts/PrivateRpcFix_20260829_190130/TEST_RESULTS.xml`: khoảng 31,944 bytes, root `Passed`, `total=10`, `passed=10`, `failed=0`, duration khoảng 117.72s.
- `FINAL_REPORT.md` không được ghi XML là 117,888 bytes nếu `Get-Item` không xác nhận.
- EditMode `145/145` là output của job riêng; nếu không có XML EditMode được copy vào artifact, ghi nguồn evidence là Antigravity/Unity MCP job ID, không giả vờ đó là XML artifact.

## 2. Việc phải làm

1. Ghi snapshot mới vào thư mục không ghi đè audit cũ:
`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcEvidenceRecheck_YYYYMMDD_HHMMSS\`
2. Lưu `BASELINE_GIT_STATUS.txt`, `ACTUAL_TEST_INVENTORY.txt`, `ACTUAL_ARTIFACT_SIZES.txt`, `EDITOR_LOG_WARNINGS.txt`, `STATIC_RPC_VERIFICATION.md`, `RUNTIME_ROUTING_EVIDENCE.md`, `FINAL_REPORT.md`.
3. `ACTUAL_TEST_INVENTORY.txt` phải được tạo bằng cách search file source/Unity test output thật; mỗi test name phải tồn tại trong source hoặc XML. Không copy tên từ báo cáo cũ.
4. Trong `STATIC_RPC_VERIFICATION.md`, xác nhận độc lập:
   - chỉ còn một declaration `RPC_ShowSearchResult`;
   - tham số đầu là `[RpcTarget] PlayerRef recipient`;
   - không còn `corpseWasSearched` ở declaration/call site;
   - mọi call site có đúng 4 arguments;
   - các RPC unicast của `LootContainer.cs` dùng cùng pattern;
   - Unity/Fusion compile/weave thực sự thành công.
5. Re-run EditMode và PlayMode nếu Unity MCP sẵn sàng. Ghi đúng job ID, mode, totals, duration, failed tests. Không gọi các test không tồn tại.
6. Nếu có thể chạy thật host + client A/B bằng ParrelSync, hãy thêm instrumentation test-only có receiver counter để chứng minh B/C không invoke private callback. Lưu raw logs cho từng peer. Nếu không thể tạo dual-peer trong phiên này, ghi `UNVERIFIED` và lý do cụ thể; không bịa packet evidence.
7. Kiểm tra `git diff --check`, không commit/push/merge, không accept/reject các diff khác.

## 3. Mẫu bảng bắt buộc trong FINAL_REPORT.md

| Check | Expected | Evidence actually present | Status |
|---|---|---|---|
| Private RPC target | `[RpcTarget]` unicast | source + Fusion compile/weave + job ID | PASS (STATIC/CODEGEN) |
| B/C transport payload | not received | raw dual-peer instrumentation/capture | PASS or UNVERIFIED |
| B/C BoxChat | no private text | UI-only or per-peer log | PRESENTATION ONLY/PASS/UNVERIFIED |
| A local loot message | exactly one | real actor log/test | PASS/PARTIAL/UNVERIFIED |
| Global corpse searched state | replicated | host/client/late-join trace | PASS/UNVERIFIED |
| Race/duplicate grant | one authority grant | existing test name + raw result | PASS/UNVERIFIED |
| EditMode | actual test inventory + job | real MCP/XML output | PASS/FAIL |
| PlayMode | actual test inventory + job | real MCP/XML output | PASS/FAIL |
| Warnings | classified honestly | Editor.log lines/timestamps | PASS/PARTIAL |

Không dùng nhãn `FIXED` cho toàn bộ network privacy nếu transport evidence vẫn `UNVERIFIED`; dùng `PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`.

## 4. Báo cáo cho Codex

Kết thúc với:

- file source nào có thay đổi thật trong phiên recheck;
- test names thực sự tồn tại và kết quả;
- artifact paths tuyệt đối và byte sizes lấy từ disk;
- claim nào PASS static, claim nào chỉ presentation, claim nào unverified;
- warning/compile status có timestamp;
- không commit/push/merge.
