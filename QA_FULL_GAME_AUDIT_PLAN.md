# Kế hoạch QA toàn bộ game — góc nhìn người chơi

> Phiên bản thiết kế đã được người dùng xác nhận trước khi tạo prompt cho
> Antigravity. Tài liệu này là kế hoạch kiểm thử và hợp đồng bằng chứng; nó
> không phải là tuyên bố rằng các case đã pass.

## 1. Understanding summary

- Dự án là game sinh tồn zombie top-down trên Unity `6000.0.69f1`, có Solo và
  Fusion Host Mode, với giới hạn thiết kế tối đa 10 Player.
- Phạm vi audit là toàn bộ trải nghiệm người chơi: khởi động, menu, input,
  loading/chuyển scene, combat, survival, zombie, loot, inventory, quest,
  vehicle, death/respawn/retry, UI, chat, localization, audio và ending.
- Cả Route A và Route B phải được kiểm tra từ một session mới đến kết thúc,
  cùng các nhánh thất bại, retry, disconnect và late join.
- Multiplayer phải được kiểm tra riêng với Solo; không được suy luận Host/Client
  đúng chỉ vì unit test hoặc PlayMode helper pass.
- Antigravity là người trực tiếp chạy Unity, test runner, ParrelSync, Profiler
  và các thao tác chơi. Codex là cổng review độc lập: đọc log/diff/evidence,
  xác minh root cause và gửi prompt sửa có phạm vi giới hạn.
- Mọi kết luận phải phân biệt `PASS`, `FAIL`, `FIXED`, `PARTIAL` và
  `UNVERIFIED`. Không được dùng báo cáo cũ thay cho evidence mới.

## 2. Assumptions and constraints

- Audit bao gồm cả ba difficulty `Easy`, `Normal`, `Hard`, cả hai route, một
  Windows build ngoài Editor nếu môi trường cho phép, và các độ phân giải PC
  phổ biến.
- Multiplayer ưu tiên hai peer GUI thực trước; sau đó mở rộng 4, 5 và 10 peer
  bằng ParrelSync/automation nếu máy chịu được. Nếu MCP chỉ điều khiển được một
  GUI hoặc không đủ tài nguyên, phải ghi `PARTIAL/UNVERIFIED`, không suy diễn.
- Critical flow chạy ít nhất ba lần sạch. Transition/timing/race chạy thêm các
  lần lặp được nêu trong từng round. Các roll ngẫu nhiên phải dùng seed hoặc số
  mẫu đủ lớn và ghi rõ phương pháp.
- Không tự thay đổi thiết kế gameplay, cân bằng, scene, prefab hoặc save format
  chỉ vì cảm giác cá nhân. Audit-only không sửa code; chỉ sửa sau prompt sửa
  riêng của Codex.
- Không reset/clean/restore hoặc ghi đè thay đổi user-owned. Không commit,
  push, merge hoặc thao tác Git trong đợt QA này.
- Nếu cùng một giả thuyết sửa thất bại ba lần, phải dừng và xem xét vấn đề
  kiến trúc thay vì thử patch thứ tư.

## 3. Routing contract cho mọi thông báo

Tên `SystemMessage` hoặc màu vàng không quyết định phạm vi người nhận. Mỗi event
phải có `scope`, semantic key, tham số, nguồn authority và event ID.

### GLOBAL — cả đội thấy

- Tin nhắn chat do người chơi gửi.
- Player đã vào/rời phòng; Player đã chết bởi zombie, nguyên nhân survival
  hoặc Player khác.
- Tiến trình chung: clue/quest, Radio, mở route, vote đã hoàn tất, gate vỡ,
  horde bắt đầu, repair `5/5`, xe khởi động, extraction và ending.
- Với quest loot, chỉ broadcast tiến độ chung (ví dụ Player A đã thu thập clue),
  không broadcast toàn bộ inventory cá nhân.

### PRIVATE_SELF — chỉ actor thấy

- Lục xác zombie: nhận item/số lượng, xác rỗng, đã lục, quá xa, inventory đầy.
- Lấy ammo/balo/weapon thường từ container, dùng/ăn/heal/equip/reload/drop.
- Prompt bị từ chối, cooldown, thiếu điều kiện, cảnh báo hunger/thirst/
  bleeding/infection, loading/readiness cá nhân, death screen, respawn point
  và retry result.

### TARGETED — chỉ các Player liên quan thấy

- Kết quả trade/revive/request giữa các Player.
- Lý do riêng gửi cho tài xế khi chưa đủ người; cả đội chỉ thấy trạng thái thiếu
  người tổng quát.
- Vote choice gửi cho người bấm; kết quả cuối cùng là GLOBAL.
- Hướng dẫn reconnect gửi người bị mất kết nối; đội thấy Player đó disconnect.
- Corpse-loot result phải target đúng `PlayerRef`; không được fallback sang
  `RpcTargets.All` để hiển thị item/amount.

### LOCAL_PRESENTATION — không đưa vào BoxChat

- Âm thanh, animation, marker, damage number, camera shake và prompt gần nhân
  vật. State thế giới vẫn có thể replicate, nhưng không tạo chat line dư thừa.

### Localization and deduplication

- Network truyền semantic key và tham số; mỗi peer tự dịch theo locale hiện tại.
- Global event phát một lần từ State Authority; private/targeted event kiểm tra
  recipient ở cả server và client. Late join nhận state hiện tại nhưng không
  replay transient message cũ, ngoại trừ join announcement được quy định.
- `AddMessage`/`AddSystemMessage` chỉ chịu trách nhiệm format; mọi call-site
  phải được audit và map explicit vào một scope ở trên.

## 4. Audit rounds

### V0 — Baseline and inventory

1. Antigravity đọc đầy đủ và ghi `Skill Trace` cho:
   `unity-project-workflow`, `unity-developer`, `systematic-debugging`,
   `test-fixing`, test-driven approach, `multiplayer`, `i18n-localization`,
   `ui-visual-validator`, Profiler guidance và `verification-before-completion`.
2. Kiểm tra Unity version, package, Build Settings, Input System, Fusion config,
   NetworkPrefabTable, scenes/prefabs/scripts/reference, test assemblies,
   Console và Git status/diff.
3. Lập bảng inventory: menu, loading, player, zombie, item, inventory, quest
   Route A/B, vehicle, UI, chat, localization, save/retry, networking, audio,
   performance; ghi `code/scene/test/manual/unknown`.
4. Không sửa gì. Output là baseline log và danh sách gap cần kiểm chứng.

### V1 — Launch, menu and loading

- `V1-01`: cold launch từ trạng thái tắt; menu đủ nút, font và không exception.
- `V1-02`: Solo/Host/Join/Options/Back/Exit; không tạo duplicate runner khi
  double-click hoặc bấm back nhanh.
- `V1-03`: đổi Vietnamese/English trước Start; menu/difficulty/loading/HUD cùng
  locale.
- `V1-04`: timestamp `Start → scene load → Fusion ready → player spawn →
  avatar/HUD ready → release`; progress monotonic và không delay giả.
- `V1-05`: trong loading ẩn hotbar/chat/prompt/icon/quest và chặn input; sau
  release mọi HUD xuất hiện đúng một lần.
- `V1-06`: load failure, cancel, mất kết nối, alt-tab, resize, 720p/1080p/
  1440p; không kẹt loading hoặc giữ runner cũ.

### V2 — Core Solo

- `V2-01`: movement 8 hướng, collision, camera, aim; không xuyên tường hoặc mất
  camera target.
- `V2-02`: equip/fire/reload/empty magazine/switch/drop; ammo random ordinary loot
  nằm trong 5–10.
- `V2-03`: health, hunger, thirst, fatigue, bleeding, infection, zombie attack,
  death cause, death lock, corpse collider/Fog/LOS.
- `V2-04`: corpse/container success, empty, too far, LOS, cancel, damage
  interruption, inventory full và double search.
- `V2-05`: storage 15→50 + 5 hotbar; backpack L1–L5; stack/split/move/use/drop,
  overflow, item count và snapshot respawn.
- `V2-06`: chết trước/sau checkpoint, retry và reload scene; không mất quest,
  item, backpack hoặc UI state.

### V3 — Difficulty contract

- `V3-01`/`02`/`03`: session mới Easy/Normal/Hard; đối chiếu mô tả với starter
  loadout, zombie density/spawn, incoming damage, loot rate, ammo và gate timer.
- `V3-04`: repeated seeded roll để xác nhận xu hướng, không kết luận từ một roll.
- `V3-05`: Host Hard + Client Easy và ngược lại; Host là canonical, Client và
  late join không ghi đè bằng PlayerPrefs.
- `V3-06`: boundary Solo, 2–4, 5–6, 7–8, 9–10; respawn pool, horde cap,
  readiness và waiting-room tier đúng contract.

### V4 — Route A and Route B

#### Route A

- `V4A-01`: tìm đủ item, kiểm tra xe; thiếu item báo rõ và không consume sai.
- `V4A-02`: sửa từng phần, bị đánh, cancel/retry; progress/inventory canonical.
- `V4A-03`: start/drive/exit, checkpoint, outro, victory/failure và retry.
- `V4A-04`: chết/disconnect/reload tại từng checkpoint; save point không rollback.

#### Route B

- `V4B-01`: tài liệu → bệnh viện → ShiftLog → Radio/key; clue/door/map reveal
  đúng thứ tự.
- `V4B-02`: Radio ba chặng; wave theo difficulty; cancel/damage/retry không kẹt.
- `V4B-03`: School clues, vote/điểm không quay lại, cinematic đóng gate.
- `V4B-04`: gate timer, horde bốn hướng, spawn cap, đủ 5 repair item, marker
  còn/hết đồ.
- `V4B-05`: repair `5/5`, siren, readiness, `W` startup, waypoint
  `EndB1 → EndB2 → EndB3 → EndBFinal2`, camera/fade/Summary.
- `V4B-06`: chết, retry, late join/disconnect ở mỗi mốc; Route A vẫn giữ nguyên.

Mỗi checkpoint còn phải kiểm tra quest state, marker, prompt, audio, item
consumption, save-respawn và không skip phase.

### V5 — UI, UX, chat and localization

- `V5-01`: health/survival/clock/ammo/hotbar/inventory/quest/map/minimap/prompt
  đồng thời; Canvas sorting không che nhau.
- `V5-02`: mọi prompt loot/repair/vehicle/clue/exit không đè hotbar/chat ở các
  resolution đã chọn.
- `V5-03`: loading/pause/modal/chat khóa input và ẩn đúng HUD; thoát trạng thái
  khôi phục đầy đủ.
- `V5-04`: player chat broadcast đúng sender/message; rich text/script bị lọc,
  giới hạn độ dài và không phá layout.
- `V5-05`: global event xuất hiện đúng một lần, màu vàng, đúng locale trên mọi
  peer.
- `V5-06`: private self không xuất hiện trên peer khác; bao gồm corpse loot,
  item/amount, empty/full/invalid và local warnings.
- `V5-07`: targeted event chỉ tới danh sách liên quan.
- `V5-08`: A lấy quest clue → đội chỉ thấy progress, A thấy item confirmation;
  A lấy ammo/balo thường → chỉ A thấy item/amount.
- `V5-09`: Host Vietnamese/Client English cùng semantic event nhưng dịch độc lập.
- `V5-10`: screenshot visual/accessibility: text không tràn/crop, contrast/focus/
  keyboard/scroll và notification không che gameplay.
- `V5-11`: audit mọi `AddMessage`, `AddSystemMessage`, RPC message; map explicit
  `GLOBAL/PRIVATE/TARGETED/LOCAL` và ghi call-site sai.

### V6 — Reliability and performance

- `V6-01`: soak 30–60 phút; ghi average/1% low FPS, frame-time, GC, memory,
  network traffic, Console và state drift.
- `V6-02`: horde tier Solo/2–4/5–10 tại gate break/xe chạy; không freeze, spawn
  vượt cap, AI đứng yên hoặc CPU spike kéo dài.
- `V6-03`: 20 corpse/container search; loot amount/rate, despawn, marker và chat
  không duplicate/leak.
- `V6-04`: inventory near-full/full, L1–L5, save/respawn/reload; không mất/nhân
  item.
- `V6-05`: reload scene/session 5 lần, chết/retry 5 lần, alt-tab/resolution;
  không duplicate singleton/runner/listener/Canvas.
- `V6-06`: invalid slot/item, spoof RPC, out-of-range, object despawn, cancel
  timer; Authority từ chối an toàn và không kẹt.

### V7 — Fusion Multiplayer and final regression

- `V7-01`: Host + Client 2 người: join, loading release, movement, combat,
  camera và chat.
- `V7-02`: 4 người: corpse/container race, quest clue, repair, vehicle seats,
  death/respawn.
- `V7-03`: 5–10 người: waiting room 2×5, readiness, horde/respawn tier, UI card,
  bandwidth; ghi số peer thật sự quan sát được.
- `V7-04`: late join ở Main load, clue, Radio, School, siege, repair, startup,
  outro; snapshot đúng, không replay private transient message.
- `V7-05`: disconnect/reconnect/death đồng thời; quest/corpse/inventory/gate/
  repair/pool/camera không rollback.
- `V7-06`: Host difficulty override và latency/jitter nếu có công cụ; client không
  spoof state.
- `V7-07`: sau mọi fix chạy case lỗi, round liên quan và critical smoke tối thiểu
  3 lần; cuối cùng compile, toàn bộ EditMode/PlayMode, Console clear và diff check.

## 5. Defect and fix handoff

Antigravity không tự sửa lỗi trong audit-only. Khi gặp lỗi:

1. Reproduce tối thiểu; đọc đầy đủ stack trace/log; kiểm tra thay đổi gần đây và
   trace data-flow theo `systematic-debugging`.
2. Dừng case (P0 ngay; P1 scenario bị ảnh hưởng; P2/P3 có thể hoàn tất round),
   lưu screenshot/video/log và ghi defect ledger.
3. Báo cho Codex với: ID, build/scene/peer/locale, bước tái hiện, expected /
   observed, tần suất, evidence, severity và giả thuyết — không ghi “đã sửa”.
4. Codex kiểm tra diff/log/test độc lập, xác nhận hoặc bác bỏ root cause, rồi gửi
   prompt sửa chỉ rõ file/phạm vi, test regression và acceptance criteria.
5. Antigravity tạo test hồi quy trước khi sửa nếu có thể, sửa một nhóm logic cô
   lập, compile, chạy test và gửi evidence mới.
6. Chỉ đánh dấu `FIXED` khi case gốc, regression liên quan và critical smoke
   pass mới. Nếu ba lần sửa cùng giả thuyết thất bại, dừng để bàn kiến trúc.

## 6. Report contract

Mỗi dòng case dùng format:

`Case ID | build/scene | peer/locale/resolution | precondition | exact steps |
expected | observed | repetitions | evidence path | log range | recipient
scope | severity | status`

Báo cáo cuối phải có:

1. Executive summary, môi trường và giới hạn.
2. Skill Trace, coverage V0–V7 và số pass/fail/skip.
3. Defect ledger với root cause, commit/diff (nếu có), regression evidence.
4. Index ảnh/video/log/Profiler và metric performance/network.
5. Danh sách `PARTIAL/UNVERIFIED`, lý do môi trường và bước test còn thiếu.
6. Git status: branch, dirty files, commit/push status; không được tự tạo commit.

## 7. Acceptance gate

Audit chỉ được gọi là đạt khi có evidence mới chứng minh tất cả điều sau:

- Compile sạch, Console không có lỗi mới, EditMode/PlayMode có số liệu đầy đủ.
- Không còn P0/P1 chưa được xử lý hoặc ghi nhận rõ ràng là blocker.
- Mọi critical flow Solo/Route A/Route B/difficulty pass tối thiểu 3 lần.
- Global/private/targeted routing, đặc biệt corpse loot, được chứng minh trên ít
  nhất hai peer bằng screenshot/log receiver và non-receiver.
- Loading, UI/layout/localization và performance có bằng chứng runtime; không
  dùng reflection/code inspection thay cho visual hoặc Profiler.
- Host/Client/late join/reconnect được kiểm tra ở mức môi trường cho phép; phần
  không chạy được phải giữ `PARTIAL/UNVERIFIED`.

## 8. Decision log

- **D1 — Audit theo rủi ro, 8 vòng:** được chọn vì tách lỗi gameplay, UI, timing,
  performance và network; cho phép checkpoint và truy root cause.
- **D2 — Routing explicit:** scope người nhận là dữ liệu bắt buộc của event; tên
  `SystemMessage` và màu vàng không được dùng làm routing.
- **D3 — Corpse loot private targeted:** item/amount/result chỉ gửi đúng actor;
  corpse state replicate cho mọi peer, quest progress mới có thể GLOBAL.
- **D4 — Automation + chơi tay:** automation bao phủ rộng và regression; chơi
  tay xác nhận cảm giác delay, visual, thao tác và recipient; không loại bên nào.
- **D5 — Honest limitations:** live 5–10 peer, target hardware và build chỉ được
  gọi PASS khi có evidence; MCP/resource limit phải ghi PARTIAL/UNVERIFIED.
- **D6 — Fix loop có cổng Codex:** Antigravity phát hiện/báo cáo; Codex kiểm tra
  root cause và gửi prompt sửa; Antigravity sửa và chạy regression.
