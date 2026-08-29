# PROMPT GỬI ANTIGRAVITY — XÁC MINH PRIVATE RPC CỦA CORPSE LOOT

Audit evidence recheck đã chứng minh UI của peer B/C không hiện item/amount khi peer A lục xác. Tuy nhiên Codex phát hiện khác biệt quan trọng cần kiểm chứng bằng Fusion runtime: trong `Assets/Script/Tin/ZombieCorpseLoot.cs`, `RPC_ShowSearchResult` (khoảng dòng 304) khai báo:

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSearchResult(PlayerRef recipient, int resultValue, string itemId, int amount, NetworkBool corpseWasSearched)
```

và chỉ lọc sau khi RPC đã nhận ở mọi client:

```csharp
if (Runner == null || Runner.LocalPlayer != recipient) return;
```

Trong khi các RPC private loot khác trong `Assets/Khoa/Code/LootContainer.cs` có tham số `[RpcTarget] PlayerRef targetPlayer`. Cần xác minh đây có phải lỗi routing/privacy hay không.

## Quy tắc phiên này

1. Đây là audit/chẩn đoán, không sửa source, scene, prefab, package, setting hay save format. Không commit/stage/push/pull/merge/reset/clean. Chỉ tạo artifact dưới `QA_Artifacts/PrivateRpcAudit_<timestamp>/`.
2. Không kết luận chỉ từ tên hàm hoặc filter UI. Đọc tài liệu Fusion/API đang dùng và trace serialization/recipient semantics của chính project.
3. Không log hoặc phát tán item/amount thật của peer khác vào BoxChat; nếu cần diagnostic payload thì ghi file artifact cục bộ và đánh dấu diagnostic-only.

## Test và kiểm tra bắt buộc

### A. Static data-flow

- Đối chiếu `ZombieCorpseLoot.RPC_ShowSearchResult` với `LootContainer.RPC_NotifyLootDenied`, `RPC_NotifyLootGranted`, `RPC_NotifyQuestClueLooted` và các RPC `[RpcTarget]` khác.
- Xác định rõ `[RpcTarget]` có được yêu cầu để Fusion chỉ serialize/gửi tới target hay không; nêu version/package/API evidence.
- Kiểm tra `itemId`, `amount`, `resultValue` và `corpseWasSearched` có nằm trong payload mà non-recipient nhận được trước dòng filter hay không.
- Kiểm tra Host (State Authority) có `Runner.LocalPlayer` hợp lệ và filter có cùng semantics trên Host/Client.

### B. Runtime receiver/non-receiver test

- Dùng production-path hoặc test harness chẩn đoán riêng, ghi rõ `diagnostic-only` nếu dùng.
- Tạo tối thiểu Host + Client A + Client B (ParrelSync/multi-runner nếu bridge giới hạn), A lục xác thành công với item/amount định trước.
- Ghi event ID/timestamp/PlayerRef/runner ID ở State Authority, A và B. A phải có đúng một local notification; B phải không có BoxChat line.
- Quan trọng: kiểm tra B có nhận callback/RPC và đọc được argument `itemId/amount` trước filter hay không. Nếu không thể quan sát payload vì API không cho phép, ghi `UNVERIFIED`, không gọi private network payload an toàn.
- Kiểm tra race A/B: chỉ một grant; non-winner chỉ nhận kết quả riêng của mình, không biết item/amount của winner.
- Kiểm tra `AlreadySearched`, `Empty`, `InventoryFull`, `TooFar`, late join và Host VI/Client EN; không replay transient private message.

### C. Kết luận và handoff

- Nếu payload thực sự tới mọi peer hoặc có thể bị client đọc: ghi `FAIL` với severity phù hợp (P1 nếu lộ thông tin/private contract hoặc exploit; P2 nếu chỉ lãng phí/bề mặt mạng), nêu một root cause duy nhất, file/line, repro, receiver/non-receiver evidence và đề xuất fix tối thiểu cho Codex. Không tự sửa.
- Nếu Fusion thực sự route/serialize riêng dù attribute hiện tại không có `[RpcTarget]`: chứng minh bằng runtime evidence và API reference; cập nhật ma trận routing.
- Nếu bridge không thể kiểm tra 2 GUI/payload: giữ `PARTIAL/UNVERIFIED`, ghi blocker và không biến thành PASS.

## Artifact/report bắt buộc

Tạo `QA_Artifacts/PrivateRpcAudit_<timestamp>/PRIVATE_RPC_AUDIT_REPORT.md`, `STATIC_CALLSITE.md`, `RUNTIME_RECEIVERS.md`, `GIT_STATUS.txt` và raw test/log cần thiết. Mỗi case ghi: `Case ID | build/scene | peer/locale | steps | expected | observed | repetitions | evidence path | receiver/non-receiver | severity | status`. Kết thúc bằng xác nhận source không thay đổi và danh sách file thật tồn tại trên đĩa.
