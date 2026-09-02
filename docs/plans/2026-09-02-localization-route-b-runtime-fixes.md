# Localization, Route B & Runtime Reliability Implementation Plan

> **For Antigravity:** Implement this plan task-by-task, keeping the scope limited to the files listed below and reporting every changed file and verification result.

**Goal:** Loại bỏ các lỗi runtime đã tái hiện, đồng bộ toàn bộ chuỗi hiển thị giữa tiếng Việt và tiếng Anh, đồng thời xác minh tuyến B và các kết nối multiplayer bằng PlayMode/manual flow.

**Architecture:** Dùng `GameLocalization` làm nguồn chuỗi duy nhất cho mọi text mà người chơi nhìn thấy. Các UI/chat/waypoint được tạo hoặc cập nhật sau khi đổi ngôn ngữ phải lấy locale hiện tại tại thời điểm hiển thị và refresh ngay khi locale thay đổi. Các lỗi runtime được sửa ở nơi tạo dữ liệu lỗi, trước khi dữ liệu đi vào Fusion RPC/UI, không chỉ che exception tại nơi phát hiện.

**Tech Stack:** Unity 6.0.0.69f1, C#, Photon Fusion, TextMeshPro, PlayMode/manual verification. Không chạy EditMode theo yêu cầu của người dùng.

---

## Nguyên tắc bắt buộc về ngôn ngữ

Locale người chơi đã chọn là một **invariant của toàn bộ UI**:

- Khi chọn **tiếng Việt**, mọi text người chơi nhìn thấy phải là tiếng Việt: menu, Options, objective, HUD, waypoint, dialogue, chat, toast, popup, nút, placeholder, loading, death message, route vote, gate bar, extraction và Victory Summary.
- Khi chọn **English**, các vị trí tương tự phải là English. Không được chỉ dịch scene đầu hoặc chỉ dịch text được tạo lúc khởi động.
- Phải kiểm tra cả text tĩnh và text sinh động sau này từ state/RPC/network event; text mới tạo sau khi đổi locale cũng phải theo locale hiện tại.
- Phải kiểm tra ba thời điểm: chọn locale trước khi vào game, đổi locale rồi đi tiếp không reload, và rời scene/quay lại hoặc mở lại summary sau khi đổi locale.
- Chỉ cho phép giữ nguyên các mã/tên kỹ thuật đã thống nhất như `F6`, `F10`, `HP`, `AK47`, số liệu, đơn vị và tên API trong Console. Những thứ người chơi đọc được không được dùng ngoại lệ này để che literal chưa dịch.
- Mỗi key phải có đủ bản dịch; thiếu key phải được phát hiện trong review/PlayMode, không âm thầm rơi về một ngôn ngữ khác.

**Language audit gate:** Ở mỗi checkpoint của Route A, Route B, menu và multiplayer, phải ghi lại toàn bộ text đang hiển thị rồi đối chiếu với locale đã chọn. Chỉ đạt khi không có từ/cụm bất ngờ của ngôn ngữ còn lại, trừ danh sách mã/tên riêng đã duyệt.

## Bằng chứng và nguyên nhân gốc đã xác nhận

1. **Crash khi zombie giết người chơi:** `PlayerHealth.TriggerDeathLogic()` truyền `killerName = null` vào RPC ở `PlayerHealth.cs:526`; Fusion serialize tham số null và phát `ArgumentNullException: Value cannot be null. Parameter name: s` tại `PlayerHealth.cs:547`. Lỗi đã tái hiện trong PlayMode khi bị zombie hạ.
2. **AutoNoiseMeter NullReferenceException:** `AutoNoiseMeter.UpdateVisuals()` ghi `segments[i].color` tại `AutoNoiseMeter.cs:236` khi phần tử UI chưa có hoặc mảng không đủ phần tử. Lỗi lặp hàng nghìn lần trong run trước.
3. **Tuyến B tiếng Anh vẫn hiện tiếng Việt:** các HUD, waypoint, gate health bar, debug chat và Victory Summary còn literal tiếng Việt. Run mới bằng F6/F10 đã đi đến `mil=Escaped`, nhưng Victory Summary vẫn toàn tiếng Việt.
4. **Tutorial loading timeout:** run trước có `[LOADING TIMEOUT] Global loading timeout reached (35.0s)` tại `MainMenuManager.cs:1731`; trình tự cinematic/spawn/player-ready ở `IntroTutorialDirector.cs:220-228` có dấu hiệu race.
5. **Console warning emoji:** TextMeshPro báo các emoji không có glyph. Đây chưa phải blocker gameplay nhưng gây nhiễu Console và có thể hiển thị sai.
6. **Multiplayer:** code có Fusion networked state/RPC, nhưng chưa có bằng chứng mới của một phiên host/client hai cửa sổ chạy xuyên suốt tuyến A/B. Hạng mục này cần xác minh riêng, không được suy luận là ổn chỉ từ solo run.

## Phạm vi file dự kiến

- `Assets/Script/Tin/GameLocalization.cs`
- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/VictorySummaryUI.cs`
- `Assets/Script/Tin/MainMenuManager.cs`
- `Assets/Script/Tin/PlayerHealth.cs`
- `Assets/Script/Tin/AutoNoiseMeter.cs`
- `Assets/Script/Tin/IntroTutorialDirector.cs`
- PlayMode tests dưới `Assets/Script/Tin/Prototype/Tests/PlayMode/` nếu cần bổ sung.

Không sửa scene/prefab/asset ngoài phạm vi nếu chưa chứng minh có liên quan. Không xóa dữ liệu. Trước khi bắt đầu implementation phải gửi prompt giới hạn phạm vi cho Antigravity.

### Task 1: Chuẩn hóa nguồn dịch và quy tắc locale

**Files:**
- Modify: `Assets/Script/Tin/GameLocalization.cs`
- Review/modify only if needed: các file tạo UI ở các task sau.

**Steps:**

1. Lập danh sách key cho các chuỗi Route B, menu, noise meter, debug chat và Victory Summary; không để literal user-facing mới trong gameplay code.
2. Mỗi key phải có bản tiếng Việt và tiếng Anh; fallback phải rõ ràng, không trả về null hoặc chuỗi rỗng.
3. Quy định chuỗi nào dịch và chuỗi nào giữ nguyên: F6/F10, số liệu, phần trăm, mét, `AK47` và tên kỹ thuật chỉ giữ nguyên khi là mã/tên riêng; tiêu đề, objective, chat, tooltip, nút, waypoint và tên thống kê phải theo locale hiện tại.
4. Chuỗi chỉ xuất Console/debug log có thể giữ một ngôn ngữ kỹ thuật ổn định; nếu đi qua `AutoChatManager` hoặc UI thì bắt buộc dịch.
5. Tạo/điều chỉnh cơ chế refresh khi locale đổi để text đã tồn tại trong scene không giữ ngôn ngữ cũ.

**Acceptance:** Với cùng một state, chọn English thì mọi text người chơi nhìn thấy ở các màn kiểm tra đều là English; chọn Vietnamese thì là Vietnamese; không có key thiếu làm lộ literal hoặc để trống.

### Task 2: Sửa toàn bộ rò rỉ tiếng Việt trong Route B

**Files:**
- Modify: `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- Modify: `Assets/Script/Tin/MainQuest/MainQuestManager.cs`

**Steps:**

1. Chuyển HUD trước căn cứ quân sự ở `MilitaryBaseQuestManager.cs:2542-2576` sang key dịch, đặc biệt các chuỗi clue `ĐÃ ĐỦ 3/3...` và `KHÁM PHÁ TRƯỜNG HỌC...`.
2. Chuyển waypoint xe cảnh sát tại `MilitaryBaseQuestManager.cs:2619-2640` sang key dịch.
3. Chuyển gate health bar tại `MilitaryBaseQuestManager.cs:2578-2617` sang key dịch; định dạng số và phần trăm phải giữ nguyên ở hai locale.
4. Rà soát và chuyển các branch tự nhiên của school/dialogue/route vote tại `MilitaryBaseQuestManager.cs:729-785`.
5. Rà soát và chuyển các chat/status của repair/extraction tại `MilitaryBaseQuestManager.cs:1184-1220`, `1553-1615` và `1643-1683`.
6. Với shortcut F6/F10, chuyển message hiển thị trong `AutoChatManager` tại `MainQuestManager.cs:545-617` và `648-651` sang locale hiện tại. Log chỉ Console có thể giữ ổn định nhưng không được gửi literal tiếng Việt vào chat/UI.
7. Giữ nguyên state transition và ý nghĩa shortcut; chỉ thay nguồn text và cơ chế refresh.

**Acceptance:** F6 đi qua phần đầu Route B và F10 đi qua căn cứ quân sự không còn text tiếng Việt khi locale là English; cùng các checkpoint đó hiển thị tiếng Việt khi locale là Vietnamese.

### Task 3: Sửa Victory Summary và UI động ngoài tuyến

**Files:**
- Modify: `Assets/Script/Tin/MainQuest/VictorySummaryUI.cs`
- Modify: `Assets/Script/Tin/MainMenuManager.cs`
- Modify if required: `Assets/Script/Tin/GameLocalization.cs`

**Steps:**

1. Chuyển title/subtitle, tên difficulty, stats và nút quay về menu ở `VictorySummaryUI.cs:82-138` sang key dịch.
2. Không cache text theo locale cũ; khi summary mở, lấy locale hiện tại và khi locale đổi thì refresh summary nếu nó đang tồn tại.
3. Kiểm tra label `NGÔN NGỮ` còn sót sau khi chuyển live sang English trong Options; callback đổi ngôn ngữ phải refresh label hiện có ngay.
4. Chuyển placeholder multiplayer `VD: Refugee Camp...` tại `MainMenuManager.cs:977` sang key `room_placeholder` hiện có hoặc key mới tương đương.
5. Kiểm tra `CUSTOMIZE SURVIVOR` và các dynamic label tại `MainMenuManager.cs:659-668`, `1097`, `2190-2204`, bảo đảm không có nhánh chỉ dịch sau khi đổi scene.

**Acceptance:** English Route B kết thúc với title/subtitle/stats/button bằng English; Vietnamese kết thúc bằng Vietnamese. Options, multiplayer host/join và Main Menu không giữ label cũ sau khi đổi locale.

### Task 4: Sửa crash PlayerHealth ở nguồn dữ liệu

**Files:**
- Modify: `Assets/Script/Tin/PlayerHealth.cs`
- Add/modify: một PlayMode regression test trong `Assets/Script/Tin/Prototype/Tests/PlayMode/` nếu test harness hiện có hỗ trợ.

**Steps:**

1. Viết test/reproduction cho zombie death không có killer và kiểm tra payload trước khi gọi RPC.
2. Bảo đảm mọi tham số RPC liên quan tên nạn nhân/kẻ hạ là non-null, có fallback rõ ràng cho zombie/environment; không truyền null qua Fusion serialization.
3. Giữ đúng nhánh PvP có tên người chơi và nhánh PvP không có tên; không làm mất nội dung localized của death message.
4. Chạy PlayMode riêng cho zombie death, PvP death nếu có harness, respawn và tiếp tục F6/F10.

**Acceptance:** Console không còn `ArgumentNullException` tại `PlayerHealth.cs:547`; cả zombie death và PvP death vẫn hiện đúng message theo locale.

### Task 5: Sửa AutoNoiseMeter và tutorial readiness

**Files:**
- Modify: `Assets/Script/Tin/AutoNoiseMeter.cs`
- Modify: `Assets/Script/Tin/MainMenuManager.cs`
- Modify: `Assets/Script/Tin/IntroTutorialDirector.cs`

**Steps:**

1. Viết reproduction/PlayMode check cho noise meter trong lúc UI chưa bind đủ.
2. Trong `UpdateVisuals`, kiểm tra null, độ dài mảng và trạng thái object trước khi ghi màu; không dùng một guard làm mất toàn bộ cập nhật nếu chỉ thiếu một segment.
3. Refresh title noise khi locale đổi; không giữ title đã cache từ locale cũ.
4. Trace thứ tự cinematic end → initial spawn → player ready ở `IntroTutorialDirector.cs:220-228` và loading timeout ở `MainMenuManager.cs:1731`.
5. Thay timeout phụ thuộc thời gian bằng điều kiện readiness đã có; nếu vẫn cần timeout, chỉ bắt đầu đếm sau khi hệ thống spawn/player đã sẵn sàng và log rõ component nào chưa ready.
6. Kiểm tra lại từ Main Menu vào tutorial, thoát/quay lại và đổi locale trước khi vào game.

**Acceptance:** Không còn `AutoNoiseMeter.UpdateVisuals` NullReferenceException; không còn timeout 35 giây trong flow hợp lệ; noise title và tutorial UI theo đúng locale.

### Task 6: Dọn cảnh báo TextMeshPro không có glyph

**Files:**
- Review the text/prefab/font asset that emits the warning only after locating the exact source.

**Steps:**

1. Xác định text nào đang dùng U+1F392, U+1F4CB, U+1F6E1, U+1F4E6, U+1F4FB.
2. Ưu tiên thay emoji bằng icon/UI asset đã có; chỉ thêm fallback font nếu asset pipeline hiện tại đã hỗ trợ và không làm phình phạm vi.
3. Chạy lại flow và phân biệt warning còn lại của package với warning do project.

**Acceptance:** Không còn warning emoji do UI của project; nếu package vẫn cảnh báo, phải ghi rõ nguồn và lý do không sửa.

### Task 7: Xác minh multiplayer thực tế

**Files:**
- Không thay đổi file chỉ để làm test; chỉ sửa thêm nếu kiểm thử chứng minh lỗi trong các file nêu trên.

**Steps:**

1. Chạy một phiên Host và một Client bằng build/editor window khả dụng; ghi lại network mode, session join, player spawn và scene/state đồng bộ.
2. Kiểm tra route choice, F6/F10 shortcut, clue count, hospital/radio state, military base phase, gate health và final `Escaped` trên cả hai peer.
3. Kiểm tra late join tại ít nhất một checkpoint; client mới phải nhận đúng state hiện tại, không reset route hoặc hiển thị text locale sai.
4. Kiểm tra locale là preference của từng client hay state dùng chung; nếu là preference từng client, mỗi peer phải thấy text theo locale của chính peer mà không làm đổi peer còn lại.
5. Ghi rõ trường hợp không thể mở hai peer trong môi trường hiện tại thay vì kết luận multiplayer ổn.

**Acceptance:** Có log/checklist riêng cho Host và Client, hoặc báo blocker cụ thể nếu môi trường không thể tạo phiên hai peer.

### Task 8: Verification sau khi Antigravity sửa

**Không chạy:** EditMode, theo yêu cầu của người dùng.

**Chạy:**

1. Review `git diff`, danh sách file thay đổi và Unity compile state; không có sửa ngoài scope.
2. Chạy PlayMode/manual từ Main Menu.
3. English: Options → English → Solo → Route B; dùng F6 cho phần đầu, chọn Route B khi vote, dùng F10 cho căn cứ; kiểm tra school HUD, vehicle waypoint, gate bar, PONR, ready overlay, evacuation overlay và Victory Summary.
4. Vietnamese: lặp lại các checkpoint tương tự và kiểm tra không còn English literal ngoài mã/tên riêng đã thống nhất.
5. Tái hiện zombie death/respawn trong một run; kiểm tra Console sau mỗi checkpoint.
6. Kiểm tra Main Menu, Options, multiplayer host/join placeholder và noise meter sau live language switch.
7. Chụp screenshot mới ở các checkpoint English/Vietnamese; lưu artifact theo quy ước hiện có trong `Assets/Screenshots/`.
8. Chỉ báo đạt khi có bằng chứng mới: Console không có exception PlayerHealth/AutoNoiseMeter/tutorial timeout, final summary đúng locale, và multiplayer có kết quả Host/Client hoặc blocker được ghi rõ.

## Ma trận kiểm tra ngôn ngữ bắt buộc

| Locale đã set | Route A | Route B | Menu/Options | Multiplayer |
|---|---|---|---|---|
| Vietnamese | Toàn bộ objective, HUD, dialogue, ending | F6, route vote, căn cứ, extraction, summary | Label, button, placeholder, loading | Host/client đều không lộ English ngoài mã/tên riêng |
| English | Toàn bộ objective, HUD, dialogue, ending | F6, route vote, căn cứ, extraction, summary | Label, button, placeholder, loading | Host/client đều không lộ Vietnamese ngoài mã/tên riêng |

Mỗi ô phải được kiểm tra ở trạng thái khởi động, sau live language switch nếu flow cho phép, và sau khi text được tạo bởi RPC/network event. Một lỗi trộn ngôn ngữ ở bất kỳ ô nào được ghi là **fail**, dù gameplay vẫn tiếp tục được.

## Thứ tự ưu tiên

- **P0:** Task 4 — `PlayerHealth` crash làm hỏng runtime khi người chơi chết.
- **P1:** Task 1–3 — đồng bộ localization, đặc biệt toàn bộ Route B English và Victory Summary.
- **P1:** Task 5 — AutoNoiseMeter NRE và tutorial timeout.
- **P1:** Task 7 — xác minh multiplayer thực tế.
- **P2:** Task 6 — cảnh báo emoji/glyph nếu không ảnh hưởng gameplay.

## Quy trình bàn giao

1. Người dùng duyệt kế hoạch này.
2. Codex gửi prompt triển khai có giới hạn file, acceptance criteria và yêu cầu Antigravity báo file/test.
3. Antigravity triển khai; Codex review diff và kiểm thử độc lập bằng PlayMode/manual, không chạy EditMode.
4. Nếu còn lỗi, Codex ghi rõ vị trí/nguyên nhân và gửi prompt sửa tiếp cho Antigravity; không tự vá source.
5. Chỉ sau khi các bằng chứng đạt mới báo kết quả cuối và liệt kê các hạng mục multiplayer chưa thể xác minh nếu có.
