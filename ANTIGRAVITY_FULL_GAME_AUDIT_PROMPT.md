# PROMPT GỬI ANTIGRAVITY — FULL GAME QA AUDIT V0–V7

Bạn là QA/Game-Play/Network engineer độc lập cho dự án Unity zombie-survival
hiện tại. Hãy thực hiện một đợt audit rất sâu dưới góc nhìn người chơi thật,
không chỉ đọc code và không chỉ chạy unit test. Mục tiêu là tìm ra lỗi, chứng
minh lỗi bằng evidence, và báo cáo trung thực; không được cố làm cho báo cáo
trông đẹp hơn thực tế.

## 0. Quy tắc bắt buộc trước khi làm

1. Đọc đầy đủ, không lướt, các tài liệu sau trước khi chạy test:
   - `CODEX_PROJECT_WORK_LOG.md`.
   - `QA_FULL_GAME_AUDIT_PLAN.md`.
   - `.agents/skills/unity-project-workflow/SKILL.md`.
   - `.codex/skills/unity-project-workflow/SKILL.md`.
   - Các skill chuyên môn đã cài, theo đúng lúc sử dụng: `unity-developer`,
     `systematic-debugging`, `test-fixing`, test-driven approach,
     `multiplayer`, `i18n-localization`, `ui-visual-validator`, Profiler/
     performance guidance và `verification-before-completion`.
2. Ở đầu báo cáo tạo `Skill Trace`, ghi tên skill, file đã đọc và task nào
   skill đó chi phối. Không được chỉ ghi một câu “đã dùng skill”.
3. Kiểm tra và ghi lại trước khi test: Unity version, package, Build Settings,
   Input System, Fusion/NetworkPrefabTable, scene list, prefab/reference,
   test assemblies, Console, branch, dirty files và diff. Repository hiện có
   thể chứa thay đổi local của người dùng; tuyệt đối không reset, clean,
   restore, overwrite hoặc xóa chúng.
4. Đây là audit-only. Không sửa code, scene, prefab, ScriptableObject, package,
   project setting hay save format trong prompt này. Không tạo commit, branch,
   push, pull, merge hoặc stage Git. Chỉ được ghi log/screenshot/video/report
   vào thư mục artifact riêng, ví dụ `QA_Artifacts/FullGameAudit_<timestamp>/`.
5. Không dùng teleport, cheat, F1, test harness, reflection hoặc mock để tuyên
   bố production player flow pass. Có thể dùng chúng ở một case chẩn đoán riêng,
   nhưng phải gắn nhãn `diagnostic-only`; sau đó vẫn phải chạy lại production
   path bằng thao tác người chơi.
6. Không dùng delay nhân tạo để “chờ cho đẹp”, không bỏ qua loading, không tắt
   collider/AI/UI bằng tay, và không bỏ qua exception/warning quan trọng. Nếu
   có giới hạn môi trường, ghi `PARTIAL` hoặc `UNVERIFIED`.

## 1. Phân loại trạng thái và mức độ lỗi

Mỗi test case phải có đúng một trạng thái tại thời điểm báo cáo:

- `PASS`: có evidence mới, case chạy đúng expected.
- `FAIL`: tái hiện được lỗi; chưa có fix.
- `FIXED`: chỉ dùng khi đã có prompt sửa riêng, test hồi quy mới và case gốc
  cùng pass lại.
- `PARTIAL`: chỉ kiểm tra được một phần (ví dụ chỉ Host GUI, chưa có Client).
- `UNVERIFIED`: không thể kiểm tra do môi trường/tài nguyên/thiếu asset; không
  được biến thành PASS.

Severity:

- `P0`: crash, mất dữ liệu, exploit authority, không thể vào/chơi.
- `P1`: chặn flow chính, state quest/loot/network sai, scene kẹt, private
  message lộ cho peer khác hoặc người chơi không thể tiếp tục.
- `P2`: lỗi chức năng có workaround, UI sai rõ ràng, performance tụt đáng kể.
- `P3`: polish nhỏ về text, âm thanh, alignment hoặc feedback.

P0 phải dừng ngay round. P1 dừng scenario bị ảnh hưởng và không đánh dấu pass
các case phụ thuộc. P2/P3 có thể hoàn tất round nhưng phải đưa vào defect ledger.
Nếu cùng giả thuyết sửa thất bại ba lần, không tự thử patch thứ tư; báo vấn đề
kiến trúc để Codex xem xét.

## 2. Hợp đồng routing cho thông báo multiplayer

Tên `SystemMessage` hoặc màu vàng không quyết định recipient. Audit mọi
`AddMessage`, `AddSystemMessage`, RPC chat/notification và đánh dấu rõ một trong
bốn scope sau:

### GLOBAL — mọi peer trong đội thấy

- Tin nhắn chat do người chơi gửi.
- Player đã vào/rời phòng; Player đã chết bởi zombie, survival hoặc Player khác.
- Tiến trình chung: clue/quest, Radio, mở route, vote hoàn tất, gate vỡ, horde
  bắt đầu, repair `5/5`, xe khởi động, extraction và ending.
- Nếu quest item được lấy, chỉ broadcast tiến trình chung (ví dụ “A đã thu thập
  manh mối”), không broadcast toàn bộ inventory cá nhân.

### PRIVATE_SELF — chỉ người thao tác thấy

- Lục xác zombie: item/số lượng, xác rỗng, đã lục, quá xa, inventory đầy.
- Lấy ammo/balo/weapon thường, dùng/ăn/heal/equip/reload/drop item.
- Prompt bị từ chối, cooldown, thiếu điều kiện, cảnh báo hunger/thirst/
  bleeding/infection, loading/readiness cá nhân, death screen, respawn point,
  retry result.

### TARGETED — chỉ Player liên quan thấy

- Kết quả trade/revive/request.
- Lý do riêng gửi tài xế khi chưa đủ người; cả đội chỉ thấy trạng thái tổng quát.
- Vote choice gửi người bấm; kết quả cuối GLOBAL.
- Hướng dẫn reconnect gửi người bị mất kết nối; đội thấy Player disconnect.
- Corpse-loot result phải target chính xác `PlayerRef`, không fallback
  `RpcTargets.All` để hiển thị item/amount.

### LOCAL_PRESENTATION — không viết BoxChat

- Audio, animation, marker, damage number, camera shake và prompt gần nhân vật.

Network phải truyền semantic key + tham số + scope + event ID; mỗi peer tự dịch.
Global event chỉ phát một lần từ State Authority; private/targeted phải kiểm tra
recipient cả server lẫn client. Late join nhận state hiện tại nhưng không replay
transient message cũ, ngoại lệ duy nhất là join announcement đã quy định.

Case bắt buộc cho corpse loot:

- A lục thành công: chỉ A thấy “Bạn nhận X item”; B/C không thấy item hoặc amount.
- B lục cùng lúc: State Authority chỉ cấp một lần; B nhận kết quả riêng như
  “Xác đã bị lục”, không biết A lấy gì.
- A túi đầy/rỗng/quá xa: chỉ A thấy lý do.
- B thấy corpse state/marker đổi theo replication nhưng không có chat line loot.
- Late join không xem lại notification cũ.
- Host Vietnamese và Client English cùng event semantic nhưng dịch độc lập.

## 3. Evidence bắt buộc của từng case

Ghi một dòng theo mẫu:

`Case ID | build/scene | peer/locale/resolution | precondition | exact steps |
expected | observed | repetitions | evidence path | log range | recipient scope |
severity | status`

Evidence phải bao gồm những gì cần thiết để người khác kiểm tra lại: ảnh/video
trước-sau, timestamp từ click đến milestone, Console/Player.log, test runner
summary, Profiler capture, network/peer identifier và receiver/non-receiver
message capture. Không chấp nhận “nhìn có vẻ ổn”, “đã test rồi”, hay ảnh chỉ
của Host cho case yêu cầu Client.

## 4. V0 — Baseline and inventory

1. Đọc work log và audit plan; đối chiếu với repository hiện tại, không tin mù
   báo cáo cũ.
2. Kiểm tra compile/Console, Build Settings, scene/prefab/reference, Input,
   Fusion authority và test assembly.
3. Lập inventory có các cột `system | code | scene/prefab | automated test |
   manual test | current evidence | risk | gap` cho: menu, loading, player,
   zombie, item, inventory, quest Route A/B, vehicle, UI, chat, localization,
   save/retry, networking, audio và performance.
4. Chạy smoke tối thiểu một lần để xác nhận test environment; không sửa lỗi
   trong V0. Ghi rõ artifact path và các gap trước khi sang V1.

## 5. V1 — Launch, menu and loading

- `V1-01`: cold launch từ trạng thái tắt; menu đủ nút, font, không crash/
  exception.
- `V1-02`: Solo/Host/Join/Options/Back/Exit; double-click/back nhanh không tạo
  duplicate runner hoặc session.
- `V1-03`: đổi Vietnamese ↔ English trước Start; menu, difficulty, loading,
  HUD dùng cùng locale.
- `V1-04`: timestamp `Start → scene load → Fusion ready → player spawn →
  avatar/HUD ready → release`; progress monotonic và phản ánh milestone thật,
  không delay giả/không release sớm.
- `V1-05`: trong loading ẩn hotbar, chat, prompt, icon, quest và chặn input;
  sau release HUD xuất hiện đúng một lần.
- `V1-06`: load failure/cancel/mất kết nối/runner cũ; loading báo lỗi hữu ích,
  không kẹt, không treo input.
- `V1-07`: alt-tab, mất focus, resize và 720p/1080p/1440p; scene trở lại đúng
  state, không crop/overlap.

Mỗi flow chạy tối thiểu 3 lần sạch. Với lỗi timing, lưu timestamp của mỗi lần.

## 6. V2 — Core Solo

- `V2-01`: movement tám hướng, collision, camera, aim; không xuyên tường/kẹt
  collider/mất target.
- `V2-02`: equip/fire/reload/empty magazine/switch/drop; ammo ordinary loot 5–10.
- `V2-03`: health, hunger, thirst, fatigue, bleeding, infection, zombie attack,
  bảy death causes, death lock, corpse collider/Fog/LOS.
- `V2-04`: corpse/container success, empty, too far, LOS, cancel, damage
  interruption, inventory full, double search.
- `V2-05`: storage 15→50 + 5 hotbar; backpack L1–L5; stack/split/move/use/drop,
  overflow, item count, save/respawn snapshot.
- `V2-06`: chết trước/sau checkpoint, retry, reload; không mất quest/item/balo/
  UI và không bắn/loot khi terminal death state.

## 7. V3 — Difficulty contract

- `V3-01`, `V3-02`, `V3-03`: session mới Easy/Normal/Hard; kiểm tra starter
  loadout, density/spawn, incoming damage, loot rate, ammo, gate timer so với
  mô tả và runtime observation.
- `V3-04`: seeded roll/repeated run để kiểm tra xu hướng; ghi sample size và
  phương pháp, không kết luận từ một roll.
- `V3-05`: Host Hard + Client Easy và Host Easy + Client Hard; Host canonical,
  Client/late join không ghi đè bằng PlayerPrefs.
- `V3-06`: boundary Solo, 2–4, 5–6, 7–8, 9–10; waiting room, readiness,
  horde cap và respawn pool đúng tier.

## 8. V4 — Route A và Route B production path

### Route A

- `V4A-01`: tìm đủ item/kiểm tra xe; thiếu item báo rõ, không consume sai.
- `V4A-02`: sửa từng phần, bị đánh, cancel/retry; progress/inventory canonical.
- `V4A-03`: start/drive/exit, checkpoint, outro, victory/failure/retry.
- `V4A-04`: chết/disconnect/reload tại từng checkpoint; save point không rollback.

### Route B

- `V4B-01`: tài liệu → bệnh viện → ShiftLog → Radio/key; clue/door/map reveal
  đúng thứ tự.
- `V4B-02`: Radio ba chặng; wave theo difficulty; cancel/damage/retry không kẹt.
- `V4B-03`: School clues, vote/điểm không quay lại, cinematic đóng gate.
- `V4B-04`: gate timer, horde bốn hướng, spawn cap, đủ 5 repair item, marker
  còn/hết đồ.
- `V4B-05`: repair `5/5`, siren, readiness, `W` startup, waypoint
  `EndB1 → EndB2 → EndB3 → EndBFinal2`, camera/fade/Summary.
- `V4B-06`: chết/retry/late join/disconnect ở từng mốc; Route A vẫn giữ object,
  reference và flow.

Ở mỗi checkpoint kiểm tra quest state, marker, prompt, audio, item consumption,
save-respawn và không skip phase. Debug shortcut chỉ được dùng để chẩn đoán,
không thay thế production path.

## 9. V5 — UI/UX, chat, localization, accessibility

- `V5-01`: health/survival/clock/ammo/hotbar/inventory/quest/map/minimap/prompt
  đồng thời; Canvas sorting không che nhau.
- `V5-02`: prompt loot/repair/vehicle/clue/exit không đè hotbar/chat tại 720p,
  768p, 900p, 1080p, 1440p, 4K và aspect ratio khả dụng.
- `V5-03`: loading/pause/modal/chat khóa input và ẩn đúng HUD; thoát ra khôi phục.
- `V5-04`: player chat broadcast đúng sender/message; rich text/script bị lọc,
  giới hạn độ dài, không phá layout.
- `V5-05`: global event đúng một lần, màu vàng và đúng locale trên mọi peer.
- `V5-06`: private event không xuất hiện trên peer khác; đặc biệt corpse loot,
  item/amount, empty/full/invalid và personal warning.
- `V5-07`: targeted event chỉ tới danh sách liên quan.
- `V5-08`: quest clue: đội thấy progress, actor thấy item confirmation; ammo/
  balo thường: chỉ actor thấy item/amount.
- `V5-09`: Host Vietnamese/Client English dịch độc lập từ semantic key.
- `V5-10`: screenshot: text không tràn/crop, contrast đọc được, focus/keyboard,
  chat scroll, notification không che gameplay.
- `V5-11`: audit mọi call-site message/RPC, đánh dấu scope explicit và call-site
  sai; không dùng tên hàm để suy luận routing.

## 10. V6 — Reliability và performance

- `V6-01`: soak 30–60 phút; ghi average/1% low FPS, frame-time, GC, memory,
  network traffic, Console và state drift.
- `V6-02`: horde tier Solo/2–4/5–10 tại gate break/xe chạy; không freeze, spawn
  vượt cap, AI đứng yên hoặc CPU spike kéo dài.
- `V6-03`: 20 corpse/container search; loot amount/rate, despawn, marker và chat
  không duplicate/leak.
- `V6-04`: inventory near-full/full, L1–L5, save/respawn/reload; không mất/nhân.
- `V6-05`: reload scene/session 5 lần, chết/retry 5 lần, alt-tab/resolution;
  không duplicate singleton/runner/listener/Canvas.
- `V6-06`: invalid slot/item, spoof RPC, out-of-range, object despawn, cancel
  timer; Authority từ chối an toàn, không kẹt.

Ghi baseline trước stress và so sánh thay đổi; không tự đặt ngưỡng FPS nếu chưa
có target hardware. Tuy nhiên bất kỳ hang/freeze, allocation tăng vô hạn,
exception lặp hoặc state deadlock đều là lỗi cần báo.

## 11. V7 — Fusion multiplayer, ParrelSync và final regression

- `V7-01`: Host + Client 2 người: join, loading release, movement, combat,
  camera, chat và routing message.
- `V7-02`: 4 người: corpse/container race, quest clue, repair, vehicle seats,
  death/respawn và global/private/targeted receiver matrix.
- `V7-03`: 5–10 người: waiting room 2×5, readiness, horde/respawn tier, UI card,
  bandwidth; ghi số peer live thật sự quan sát được.
- `V7-04`: late join tại Main load, clue, Radio, School, siege, repair, startup,
  outro; snapshot quest/loot/gate/repair/death/corpse đúng; không replay private
  transient message.
- `V7-05`: disconnect/reconnect/death đồng thời; không rollback quest/corpse/
  inventory/gate/repair/pool/camera.
- `V7-06`: Host difficulty override và latency/jitter nếu công cụ cho phép;
  client không spoof state.
- `V7-07`: sau mỗi fix chạy case lỗi, round liên quan và critical smoke 3 lần;
  cuối cùng compile, toàn bộ EditMode/PlayMode, Console clear và diff check.

Tạo/kiểm tra clone ParrelSync tại thư mục dự án đã cấu hình. Nếu MCP không thể
điều khiển hai GUI cùng lúc, vẫn phải chạy phần logic có thể chạy, nhưng ghi rõ
`PARTIAL` và không gọi live dual-GUI/10-player là PASS.

## 12. Quy trình khi phát hiện lỗi

1. Dừng theo stop rule; tái hiện tối thiểu và lưu evidence.
2. Đọc đầy đủ stack trace/log, kiểm tra diff gần đây, so sánh working example,
   trace data-flow và nêu **một** giả thuyết root cause theo `systematic-debugging`.
3. Không sửa code trong phiên audit. Gửi báo cáo cho Codex gồm Case ID, môi
   trường/peer/locale, bước tái hiện, expected/observed, tần suất, evidence,
   severity, scope recipient và giả thuyết.
4. Chờ prompt sửa riêng từ Codex. Không tự “tiện tay” refactor hoặc sửa nhiều
   nhóm logic.
5. Sau khi nhận prompt sửa: tạo regression test nếu có thể, sửa tối thiểu,
   compile, chạy case gốc + regression + round liên quan + critical smoke.
6. Chỉ ghi `FIXED` khi có evidence mới; nếu chưa đạt giữ `FAIL` và nêu nguyên
   nhân còn lại.

## 13. Báo cáo cuối bắt buộc

Kết thúc mỗi round gửi summary; khi không có P0/P1 và đã chạy hết round, gửi báo
cáo cuối với các mục sau:

1. `Executive Summary`: trạng thái thật, không quảng cáo.
2. `Environment`: Unity/build/OS/máy/peer/locale/resolution/network limits.
3. `Skill Trace`: skill nào đã dùng ở round nào.
4. `Coverage`: số case PASS/FAIL/FIXED/PARTIAL/UNVERIFIED theo V0–V7.
5. `Defect Ledger`: từng lỗi, severity, root cause, repro, evidence, fix prompt,
   regression result.
6. `Message Routing Matrix`: event, scope, expected recipients, observed
   recipients, ảnh/log receiver và non-receiver; nhấn mạnh corpse loot.
7. `Performance`: baseline/stress/soak metrics, Profiler path và giới hạn.
8. `Files/Changes`: file artifact hoặc file source đã bị thay đổi (audit này
   không được có source change), Git status và xác nhận không commit/push.
9. `Unverified/Risks`: live 5–10 peer, target hardware, build, audio, visual
   hoặc case nào chưa chứng minh được; nêu bước tiếp theo.

Chỉ được ghi audit đạt khi: compile/Console sạch; EditMode/PlayMode có số liệu
mới; critical Solo/Route A/Route B/difficulty pass tối thiểu 3 lần; không còn
P0/P1; routing được chứng minh trên ít nhất hai peer; loading/UI/localization/
performance có runtime evidence; và mọi giới hạn còn lại được ghi rõ.

Hãy bắt đầu bằng V0 và gửi `Baseline + Skill Trace + Inventory + Known Gaps`.
Nếu V0 không phát hiện blocker, tự tiếp tục V1–V7. Khi gặp P0/P1 hoặc một lỗi
P2/P3 cần sửa, dừng theo quy tắc ở trên và báo cho Codex thay vì tự sửa.
