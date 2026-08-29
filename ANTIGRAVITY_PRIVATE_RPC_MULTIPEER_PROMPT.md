# PROMPT MULTI-PEER — XÁC MINH THỰC TẾ PRIVATE RPC BẰNG PARRELSYNC

Mục tiêu của lượt này là thu thập **bằng chứng runtime thật** cho patch `[RpcTarget]` của `ZombieCorpseLoot.RPC_ShowSearchResult`. Không được sửa source production chỉ để làm cho báo cáo đẹp hơn. Nếu môi trường không thể chạy hai peer độc lập, phải ghi rõ blocker và giữ trạng thái `UNVERIFIED`.

## 0. Trạng thái đã biết

- Patch source đã có trong `Assets/Script/Tin/ZombieCorpseLoot.cs`: `RPC_ShowSearchResult([RpcTarget] PlayerRef recipient, int resultValue, string itemId, int amount)`.
- EditMode job `1e9aa64923ca48d2b7fd26cce72d1ee1`: 145/145 Passed.
- PlayMode job `2d7520805be94cb3b57d7b08f13fa51e`: 10/10 Passed, XML thật 31,944 bytes.
- Những test corpse race/late join từng ghi trong report cũ không tồn tại trong source; không được dùng lại.
- Unity MCP hiện có thể chỉ điều khiển một Unity Editor. Phải kiểm tra giới hạn này trước khi claim multi-peer.

## 1. Ràng buộc an toàn

1. Không commit, push, merge, checkout, reset, clean hoặc accept/reject diff khác.
2. Không giết process Unity/Antigravity đang chạy nếu không có lý do và không ghi lại lý do.
3. Không ghi đè artifact cũ. Tạo thư mục mới:
   `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcMultiPeer_YYYYMMDD_HHMMSS\`
4. Không gọi UI-only observation là transport proof. Muốn claim B/C không nhận packet phải có receiver counter/log ở từng process hoặc packet capture thật.
5. Nếu cần instrumentation, chỉ thêm test-only/development-only code, ghi rõ file/compile symbol và không để instrumentation làm đổi gameplay. Nếu không thể dọn instrumentation an toàn, không thêm.
6. Nếu test bị block bởi port, license, clone lock, MCP single-instance, missing scene/setup hoặc không có cách tạo corpse hợp lệ, lưu blocker log và đánh dấu `BLOCKED/UNVERIFIED`; không tự tạo số liệu.

## 2. Chuẩn bị hai peer độc lập

1. Đọc `ProjectSettings/ProjectVersion.txt`, package ParrelSync và cấu hình runner hiện có.
2. Xác định project chính và clone ParrelSync thật trên disk. Ưu tiên clone đã tồn tại dưới cùng workspace; không tạo thư mục ngoài workspace.
3. Xác định hai process Unity riêng biệt, cùng Unity version; ghi PID, executable, project path, start time vào `PROCESS_INVENTORY.txt`.
4. Chạy host trong project chính và client từ ParrelSync clone bằng flow hiện có (`HostModeSpawner`/Main Menu), không tự viết networking mock.
5. Kiểm tra hai peer có cùng room/session, mỗi peer có PlayerRef khác nhau; ghi PlayerRef và timestamp. Nếu chỉ có một process, dừng phần runtime và ghi chính xác blocker.

## 3. Cách tạo/quan sát corpse mà không làm sai gameplay

1. Đọc `ZombieCorpseLoot.cs`, zombie death pipeline, prefab/scene và dev cheat hiện có (`DevCheatManager`) để tìm cách hợp lệ tạo một corpse có loot.
2. Dùng loot amount thực tế trong state authority; không giả định item/amount nếu không thấy log. Với case cần deterministic, dùng seed/cheat có sẵn hoặc ghi rõ item/amount thực tế.
3. Instrumentation test-only (nếu cần) phải ghi cho từng process:
   - process role (Host/A/B/C), PID, PlayerRef;
   - corpse NetworkId;
   - RPC callback/receiver count;
   - `resultValue`, `itemId`, `amount` **chỉ trong log của process thực sự invoke callback**;
   - BoxChat message count/text và `HasCorpseBeenSearched` visual/state;
   - timestamp UTC/local.
4. Không log `itemId`/`amount` trên peer không phải recipient nếu `[RpcTarget]` hoạt động; việc không có dòng/callback phải được chứng minh bằng counter/log, không chỉ bằng screenshot.

## 4. Test matrix — chạy ít nhất 3 lần cho mỗi case có thể chạy

### Case A — Grant private loot

- Host/state authority tạo corpse còn loot.
- Client A search và nhận item.
- Client B đứng gần làm observer.
- Lặp 3 lần với corpse mới.
- Expected: A có đúng một result/message và inventory tăng đúng amount; B không invoke `RPC_ShowSearchResult`, không deserialize/log `itemId`/`amount`, không có private BoxChat text; visual searched của corpse vẫn replicate.

### Case B — Empty / invalid / full / too far

- Chạy từng kết quả `Empty`, `InvalidLoot` (nếu tạo được hợp lệ), `InventoryFull`, `TooFar`, `AlreadySearched`.
- Expected: chỉ actor nhận message/result; không peer khác nhận private payload; corpse state chỉ đổi khi logic authoritative yêu cầu.

### Case C — Simultaneous race

- A và B gửi search gần như cùng tick tới cùng corpse.
- Lặp ít nhất 3 lần nếu có thể.
- Expected: State Authority chỉ grant một lần, corpse searched một lần; peer thắng nhận item/amount riêng; peer còn lại chỉ nhận `AlreadySearched` riêng; không duplicate inventory/message.

### Case D — Late join

- A search corpse trước; sau đó client C join bằng ParrelSync/runner thật.
- Expected: C nhận snapshot visual/state `HasCorpseBeenSearched`; C không replay private reward message và không nhận payload cũ.

### Case E — Host as actor và disconnect edge

- Lặp Case A với Host trực tiếp là actor.
- Kiểm tra target `PlayerRef.None`, disconnect trước callback, despawn corpse; không crash/duplicate.

## 5. Artifact bắt buộc

Trong thư mục mới phải có:

- `PROCESS_INVENTORY.txt` — hai process/path/PID/Unity version/PlayerRef.
- `SETUP_AND_BLOCKERS.md` — setup, room, ports, blocker nếu có.
- `HOST_LOG.txt`, `CLIENT_A_LOG.txt`, `CLIENT_B_LOG.txt`, `CLIENT_C_LOG.txt` hoặc file thật tương đương; raw, timestamped, không tóm tắt thay thế.
- `RPC_RECEIVER_COUNTERS.csv` — từng case/lần/peer/expected/actual callback count.
- `BOXCHAT_AND_VISUAL_MATRIX.md` — private/global message và corpse state từng peer.
- `RUNTIME_TEST_RESULTS.md` — từng case × 3 runs, pass/fail/blocked, item/amount thực tế.
- `STATIC_RPC_CHECK.md` — source declaration, call sites, Fusion codegen/compile.
- `FINAL_REPORT.md` — honest conclusion.

Nếu chưa có dual-peer evidence, `FINAL_REPORT.md` phải dùng chính xác:
`PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`.

## 6. Tiêu chí kết luận

- `PASS (RUNTIME MULTI-PEER)` chỉ khi có hai process độc lập và raw receiver evidence cho A/B/C.
- `PASS (STATIC/CODEGEN)` cho `[RpcTarget]` + compile/weave sạch.
- `PASS (PRESENTATION ONLY)` chỉ cho BoxChat/UI filter.
- `UNVERIFIED/BLOCKED` cho case không chạy hoặc không có receiver/packet evidence.
- Không claim “no payload on B/C” chỉ vì B/C không thấy UI.
- Không claim test name không tồn tại; mọi tên test trong report phải khớp source/XML.
- Ghi warnings compile/runtime với timestamp; cảnh báo VoiceNetworkObject expected phải được phân loại, không ghi tuyệt đối “0 warnings”.

Kết thúc bằng danh sách file source/test **thực sự** thay đổi trong lượt này và xác nhận không commit/push/merge.
