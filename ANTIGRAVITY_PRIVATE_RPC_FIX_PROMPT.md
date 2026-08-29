# PROMPT CHO GOOGLE ANTIGRAVITY — SỬA LỖI PRIVATE RPC LOOT XÁC ZOMBIE (P2)

Bạn đang làm việc trực tiếp trong Unity project `ProJectZomboiNhai` (Unity 6 `6000.0.69f1`, Photon Fusion 2, Host Mode). Hãy thực hiện đúng quy trình dưới đây. Đây là prompt triển khai sửa lỗi đã được Codex kiểm tra độc lập; không được coi báo cáo audit là giả định và không được tự ý mở rộng phạm vi.

## 0. Ràng buộc bắt buộc

1. Chỉ sửa code khi đã đọc toàn bộ file liên quan và đối chiếu với các RPC unicast đang chạy ổn trong project, đặc biệt `Assets/Khoa/Code/LootContainer.cs`.
2. Không accept/reject/overwrite các thay đổi khác đang có trong Git. Không reset, checkout, clean, stash, commit, push, merge hoặc đổi branch.
3. Không sửa tạm trong Library/Temp/Logs để che lỗi. Mọi thay đổi phải nằm trong source/test cần thiết và phải có artifact kiểm chứng.
4. Không coi việc UI của client B không hiện text là đủ. Tiêu chí chính là client B/C không được nhận/deserialize gói reward riêng tư của A trên network transport.
5. Nếu Unity MCP hoặc test runner không hoạt động, ghi rõ `BLOCKED/UNVERIFIED`, không tự bịa số liệu, screenshot, packet capture hay kết quả test.
6. Sau khi sửa, phải báo cáo các file/line thực sự thay đổi, lệnh/test thực sự đã chạy, thời gian chạy, pass/fail và artifact path tuyệt đối.

## 1. Bằng chứng lỗi đã được xác nhận

File lỗi: `Assets/Script/Tin/ZombieCorpseLoot.cs`, method `RPC_ShowSearchResult` khoảng dòng 304–320 hiện có dạng tương đương:

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSearchResult(
    PlayerRef recipient,
    int resultValue,
    string itemId,
    int amount,
    NetworkBool corpseWasSearched)
{
    if (corpseWasSearched) locallyKnownSearched = true;
    if (Runner == null || Runner.LocalPlayer != recipient) return;
    isAwaitingSearchResult = false;
    if (!corpseWasSearched) locallyKnownSearched = false;
    AutoChatManager.Instance?.AddSystemMessage(BuildLocalResultMessage((SearchResult)resultValue, itemId, amount));
}
```

Vì `recipient` không có `[RpcTarget]`, `RpcTargets.All` broadcast toàn bộ payload (`itemId`, `amount`, kết quả) đến mọi peer rồi mới return ở client không phải người loot. Đây là over-broadcast ở tầng transport, dù tầng UI hiện đang lọc đúng.

Đối chiếu chuẩn đã có trong `Assets/Khoa/Code/LootContainer.cs`: các RPC `RPC_NotifyLootGranted`, `RPC_NotifyLootDenied`, `RPC_NotifyQuestClueLooted` dùng `[RpcTarget] PlayerRef targetPlayer` để unicast. Fusion CodeGen của project chỉ dùng target parameter để định tuyến unicast khi tham số có `[RpcTarget]`; tham số `PlayerRef` trần không tạo target.

Artifact audit tham khảo (đã tồn tại trên disk, chỉ đọc):
`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcAudit_20260829_185400\PRIVATE_RPC_AUDIT_REPORT.md`

## 2. Mục tiêu hành vi sau sửa

### 2.1. Dữ liệu riêng tư

- Kết quả loot, `itemId`, `amount`, thông báo “đã lục soát/nhận được/đầy túi/đã bị người khác lấy” chỉ được gửi và hiển thị cho player thực hiện request (hoặc đúng target được chỉ định).
- Host/A nhận đúng một callback/result của chính A.
- Client B/C không nhận được private RPC trên transport, không deserialize `itemId`/`amount`, không gọi `BuildLocalResultMessage` và không thêm dòng BoxChat của A.
- Không dùng client-side `if (Runner.LocalPlayer != recipient) return;` như biện pháp bảo mật duy nhất; `[RpcTarget]` phải là cơ chế định tuyến.

### 2.2. Trạng thái toàn cục

- Trạng thái xác zombie đã bị lục (`HasCorpseBeenSearched`/state tương đương), sprite/visual đã searched và việc ngăn người khác lục lại vẫn phải replicate tới mọi peer.
- Late joiner thấy đúng visual/state đã lục nhưng không nhận lại private loot result cũ.
- Không truyền `itemId` hoặc `amount` qua RPC global chỉ để cập nhật visual.

## 3. Cách sửa tối thiểu, an toàn

1. Đọc toàn bộ `ZombieCorpseLoot.cs`, nhất là `ConsumeCorpse`, `Render`/OnChanged, các call site RPC khoảng dòng 249, 255, 261, 269, 275, 282, 289, 294 và các biến `HasCorpseBeenSearched`, `locallyKnownSearched`, `isAwaitingSearchResult`.
2. Sửa `RPC_ShowSearchResult` để tham số đích là target parameter của Fusion, theo đúng cú pháp version đang dùng và nhất quán với `LootContainer.cs`:

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSearchResult(
    [RpcTarget] PlayerRef recipient,
    int resultValue,
    string itemId,
    int amount)
{
    if (Runner == null || Runner.LocalPlayer != recipient) return;
    isAwaitingSearchResult = false;
    AutoChatManager.Instance?.AddSystemMessage(
        BuildLocalResultMessage((SearchResult)resultValue, itemId, amount));
}
```

Đoạn trên là hướng dẫn về ý đồ; phải giữ đúng logic null/lifecycle và coding convention thực tế của project. Nếu `[RpcTarget]` yêu cầu cách gọi khác trong Fusion version hiện tại, đối chiếu codegen và các RPC đang compile trước khi chọn cú pháp.
3. Bỏ `corpseWasSearched` khỏi private result RPC và cập nhật toàn bộ call site; không để overload/call site cũ vô tình broadcast payload.
4. Di chuyển/giữ cập nhật visual searched ở state networked hiện có. Không đánh mất việc clear `isAwaitingSearchResult` cho actor sau các kết quả `Granted`, `AlreadySearched`, `Empty`, `Full`, `TooFar`, lỗi/timeout. Nếu visual cần callback, dùng `[Networked]` state/Render/OnChanged hiện hữu, không nhét reward data vào global RPC.
5. Không đổi semantics loot, tỉ lệ loot, inventory, BoxChat global/private hoặc localization ngoài phần cần để giữ test pass.

## 4. Test hồi quy bắt buộc

Trước hết ghi baseline Git/status và đọc compile log. Sau sửa:

### 4.1. Static/codegen

- Search toàn repo để chắc chắn `RPC_ShowSearchResult` chỉ còn một declaration đúng target signature và mọi call site đúng số tham số.
- So sánh với `[RpcTarget]` trong `LootContainer.cs`.
- Compile Unity không có CS errors, Fusion weave/codegen errors, duplicate RPC signature hoặc warning mới liên quan.

### 4.2. EditMode/PlayMode tự động

Nếu test infrastructure phù hợp, thêm test nhỏ có tên rõ ràng; không làm test giả chỉ assert source text. Tối thiểu kiểm tra:

1. target parameter tồn tại/được Fusion codegen chấp nhận.
2. một search request chỉ tạo một private result cho actor.
3. `AlreadySearched`, `Empty`, `Full`, `TooFar` đều clear pending state đúng actor.
4. `HasCorpseBeenSearched`/visual replicate global.
5. late joiner nhận visual searched nhưng không replay reward message.

### 4.3. Hai/ba peer Fusion

Chạy host + ít nhất client A/B bằng ParrelSync/runner mà project đang dùng; thêm client C nếu môi trường cho phép. Kịch bản phải có bằng chứng:

1. A lục corpse còn loot: A nhận đúng message và item/amount; B/C không nhận private result.
2. Kiểm tra cả UI BoxChat và tầng callback/transport. Nếu không có packet capture, thêm instrumentation tạm chỉ trong test hoặc log test có thể chứng minh receiver; không claim packet-level PASS chỉ từ UI.
3. A lục corpse empty/full/already-searched/too-far; B/C vẫn không thấy dữ liệu riêng của A.
4. Hai actor request gần như đồng thời; chỉ một grant hợp lệ, không duplicate item/message.
5. Sau khi corpse đã searched, late joiner B vào; B thấy visual/state đúng, không thấy message loot cũ của A.
6. Kiểm tra host player cũng không bị trường hợp target sai do `PlayerRef.None`, disconnect hoặc object despawn.

Nếu không thể chạy hai GUI/ParrelSync thật, ghi chính xác lý do và đánh dấu `UNVERIFIED`; không biến static proof thành runtime proof.

## 5. Artifact và tiêu chí pass

Tạo thư mục mới, không ghi đè audit cũ:
`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcFix_YYYYMMDD_HHMMSS\`

Bắt buộc lưu:

- `BASELINE_GIT_STATUS.txt`
- `CHANGED_FILES.txt` (path + line summary)
- `STATIC_RPC_VERIFICATION.md`
- `TEST_RESULTS.xml` nếu runner tạo được
- `RUNTIME_ROUTING_EVIDENCE.md` (host/A/B/C matrix, từng case, receiver thực tế, timestamp)
- `UNITY_COMPILE_AND_CONSOLE.log` hoặc đường dẫn log thực tế
- `FINAL_REPORT.md`

Trong `FINAL_REPORT.md` phải có bảng:

| Check | Expected | Actual evidence | Status |
|---|---|---|---|
| Private RPC target | unicast only | codegen/runtime artifact | PASS/PARTIAL/FAIL |
| B/C private payload | not received | instrumentation/packet evidence | PASS/PARTIAL/UNVERIFIED |
| A BoxChat | one local message | runtime log | PASS/FAIL |
| Global corpse visual | replicated | A/B/late-join evidence | PASS/FAIL |
| Race/duplicate grant | one authoritative grant | runtime/test evidence | PASS/FAIL |
| Compile/tests | no new errors | real logs/XML | PASS/FAIL |

Nếu một claim chỉ dựa static analysis, ghi `STATIC ONLY`; nếu chỉ thấy UI, ghi `PRESENTATION ONLY`. Không ghi “0 warning/0 error” nếu Editor.log còn warning timestamped.

## 6. Báo cáo cho Codex

Kết thúc bằng một báo cáo ngắn nhưng đầy đủ:

- `FIXED`, `PARTIALLY FIXED` hoặc `BLOCKED`.
- Root cause và diff thực tế.
- Danh sách test đã chạy với pass/fail/blocked.
- Artifact paths tuyệt đối.
- Các rủi ro còn lại (đặc biệt nếu chưa có dual-GUI hoặc packet-level evidence).
- Không commit/push/merge.
