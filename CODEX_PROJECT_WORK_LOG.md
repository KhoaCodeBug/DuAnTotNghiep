# CODEX PROJECT WORK LOG

> **NGUỒN BÀN GIAO CANONICAL — cập nhật 2026-08-29.** Khi mở phiên Codex mới, đây là file lịch sử duy nhất bắt buộc phải đọc đầy đủ. Các file `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md`, `NEXT_SESSION_MAINPLAY_PLAN.md` và `NEXT_SESSION_ROUTE_B_GIT_INTEGRATION_HANDOFF.md` chỉ còn là hồ sơ lịch sử/chuyên đề; không dùng chúng để ghi đè trạng thái mới hơn trong file này.

## Bắt đầu một phiên Codex mới

1. Yêu cầu Codex: `Đọc toàn bộ CODEX_PROJECT_WORK_LOG.md, sau đó kiểm tra AGENTS.md, project skills, git status và code/Scene liên quan trước khi đề xuất hoặc sửa.`
2. Không cần bắt Codex đọc cả ba handoff cũ cho mọi task. Chỉ đọc tài liệu chuyên đề khi task thật sự cần lịch sử chi tiết của phần đó.
3. Luôn kiểm tra repository hiện tại trước khi tin một commit, branch, số test hoặc trạng thái ghi trong log; các entry cũ được giữ để bảo toàn lịch sử và có thể đã bị entry mới hơn thay thế.
4. Với gameplay/network, phiên mới phải nêu State Authority, state replicated/RPC, Client presentation, late join/reconnect và kế hoạch test Solo + Host/Client nếu có liên quan.

## Snapshot hiện tại — 2026-08-29

- Unity `6000.0.69f1`; project root hiện tại: `E:\Lap_trinh\HocTap\DuAnTotNghiepFIx\My project`; scene gameplay chính: `Assets/Scenes/Main.unity`.
- Git đã xác minh: `main` đang tại `7bcfb935c`, tracking và khớp `origin/main`. Route B final polish `de566c323` cùng tài liệu checkpoint `53ee0e469` đã nằm trong lịch sử `main` qua PR #323.
- Working tree tại lúc gom tài liệu chỉ có `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab` dirty do thay đổi của người dùng; không được restore, stage hoặc đưa vào commit tài liệu nếu chưa được yêu cầu.
- Route B canonical hiện đã có: xe thật đi qua `EndB1 → EndB2 → EndB3 → EndBFinal`, authority căn xe chạy tới `EndBFinal2`; camera 6 giây, giữ 2 giây rồi fade; zombie hậu-vỡ-cổng trở về AI/noise canonical; F1 đưa Host/Solo thẳng tới chương quân sự. Flow 5–10 Player dùng readiness ghế hoặc bán kính 6m và virtual follower cho người không có ghế.
- Hệ thống multiplayer/readiness hiện có lobby tối đa 10 Player, loading gate tập trung, difficulty authoritative từ Host, HUD suppression, system chat/localization song ngữ, death context và các tier horde/respawn theo số người.
- Loot/inventory mới nhất có corpse loot authority, thông báo riêng bằng private targeted RPC, đạn ngẫu nhiên 5–10 viên, năm cấp balo và sức chứa tối đa 55 slot tổng. EditMode gần nhất ghi nhận `146/146` pass và PlayMode `10/10` pass. Live Host+Client loading/readiness đã pass sau khi tăng Fusion timeout lên 120 giây; live multi-peer capture cho private RPC/global corpse state vẫn **chưa được xác minh đầy đủ**.
- QA tay còn quan trọng: lobby thật 5–10 người; boundary readiness 6m/disconnect/death đúng lúc bấm W; virtual follower dưới latency; horde 80–112 zombie bằng Profiler; targeted corpse-loot RPC và corpse searched state trên nhiều peer thật.

## Mục đích và quy ước

Đây là nhật ký liên tục để một phiên Codex mới có thể nắm lại toàn bộ bối cảnh làm việc quan trọng mà không phụ thuộc vào lịch sử chat.

- Nội dung mới được nối thêm theo thời gian; hạn chế sửa hoặc xóa nội dung cũ.
- Mỗi entry phân biệt rõ: `Đề xuất`, `Đã duyệt`, `Đã triển khai`, `Đã test tự động`, `Đã test tay`, `Chưa triển khai` và `Bị loại`.
- Repository hiện tại là nguồn sự thật về code; nhật ký là nguồn sự thật về quá trình thảo luận và quyết định.
- Nếu tài liệu và code khác nhau, phải điều tra Git/code/Scene rồi ghi một entry đính chính, không âm thầm viết lại lịch sử.
- Skill quy định cách làm việc: `.codex/skills/graduation-gameplay-workflow/SKILL.md`.

## Bối cảnh dự án

- Dự án tốt nghiệp Unity, thể loại sinh tồn zombie top-down.
- Có Solo và Multiplayer sử dụng Fusion Host Mode.
- State Authority là nguồn quyết định canonical cho quest, loot, zombie, damage quan trọng, cổng, repair, respawn và extraction. Client gửi request và nhận replicated state.
- Scene gameplay chính: `Assets/Scenes/Main.unity`.
- Hai tuyến thoát chính:
  - Route A: sửa xe dân sự và thoát khỏi thành phố.
  - Route B: bệnh viện → Radio → căn cứ quân sự → thủ thành/sửa xe → Ending B.
- Route A và Route B cùng tồn tại trong `Main.unity`; sửa một tuyến không được làm mất object/reference của tuyến còn lại.

## Gameplay loop Route B đã chốt

1. `MainMenu → Main`, kiểm tra chiếc xe hỏng.
2. Chọn tuyến đang theo dõi; lựa chọn HUD chưa khóa ending ngay.
3. Tìm ba tài liệu trong khu dân cư.
4. Xác định bệnh viện và điều tra Khu Điều phối.
5. Hoàn thành chuỗi `ShiftLog → ShiftLog2`.
6. Một vị trí KeyLoot hợp lệ được chọn; Player tìm shared key rồi mở khu Radio.
7. Sửa Radio qua ba chặng, tổng khoảng 14 giây; hai checkpoint đầu phát nhiễu và tạo đợt zombie theo độ khó.
8. Hoàn tất Radio, nhận Mảnh bản đồ 2 và mở tuyến quân sự.
9. Tới khu trường/căn cứ quân sự và phát cinematic.
10. Tìm ba manh mối trong trường, theo dõi việc mở bản đồ theo giai đoạn.
11. Rời khu mái trường, kiểm tra `Car`, toàn đội vote tại điểm không thể quay lại.
12. Khóa Ending B, cinematic đóng cổng và bắt đầu siege finale.
13. Vừa thủ cổng vừa loot đủ năm linh kiện, thực hiện năm hạng mục sửa xe.
14. Xe hoạt động, extraction và Ending B.

## Các nguyên tắc gameplay/network canonical

- Host/State Authority quyết định tiến độ quest và mọi giao dịch ảnh hưởng gameplay.
- Client không tự cấp item, tự hoàn tất quest, tự quyết định respawn hoặc extraction.
- Request loot phải kiểm tra PlayerRef, phase, Player sống, khoảng cách, line of sight, inventory/slot và chống double-claim.
- Late join phải nhận cùng state quest, loot, gate, repair và finale.
- Các cơ chế Generator, tăng 150% HP và electric stun từng được cân nhắc nhưng đã bị loại khỏi Route B canonical; không tự khôi phục.
- Implementation thử nghiệm `PoliceRepairLoot*`/Ox Alpha đã bị loại. Hệ thống chính thức tái sử dụng `LootContainer` và các tủ quân sự được author trong Scene.

## Trạng thái triển khai nền trước phiên Git integration — 2026-08-26

### Đã duyệt và triển khai

- Chuỗi bệnh viện H1–H5 và Radio authoritative.
- KeyLoot chọn ngẫu nhiên nhưng ổn định bằng stable ID/Polygon; shared key chỉ được cấp qua bước loot hợp lệ.
- Hai đợt zombie cao trào tại Radio, số lượng theo độ khó.
- Điểm không thể quay lại, vote đồng thuận toàn đội và Ending B.
- Cinematic đóng cổng và phục hồi UI/inventory sau cinematic.
- Horde đánh cổng, cổng vỡ thì zombie chuyển sang săn Player; các batch sau vẫn tiếp tục sinh.
- Finale khóa thời gian ở 16:00, ẩn clock, hủy sleep transition và giữ fatigue bằng 0.
- Repair chỉ bị ngắt bởi đòn zombie trực tiếp, không bị reset bởi hunger/thirst/bleeding hoặc damage-over-time.
- Siren phát đầy đủ trong cinematic, duy trì 20% sau cinematic và dừng ở `5/5` repair.
- Zombie áo trắng và áo vàng chết bền; corpse loot tiếp tục tồn tại.
- Multiplayer có pool chung ba lượt respawn, chờ 10 giây, checkpoint gần `Car`, giữ inventory/hotbar; Solo chết là Failed.

### Repair loot chính thức

- Năm vị trí loot bảo đảm đủ Toolbox, Hammer, FuelCan, Battery và Tire.
- Loot do State Authority cấu hình; không double-claim, inventory đầy không làm mất item và late join thấy state còn lại.
- UI/giao dịch dùng `LootContainer` hiện có.

### Kiểm thử trước integration

- Unity/Assembly compile không có error.
- EditMode và full Route B PlayMode đã pass tại thời điểm handoff.
- Chưa có xác nhận cuối bằng Host + Client trên hai máy.

## Phiên Git integration Route A + Route B — 2026-08-26

### Thảo luận và quyết định

- Người dùng yêu cầu tạo nhánh mới, tích hợp code mới của đồng đội và push lên Git sau khi ổn.
- Quyết định dùng safety commit rồi fetch/merge, không push trực tiếp `main`.
- Conflict duy nhất nằm ở `Main.unity`; phải giữ đồng thời năm marker Route B và ba mốc Route A.

### Đã triển khai

- Tạo `codex/route-b-integration-20260826`.
- Safety commit Route B: `197b51371`.
- Merge `origin/main@13b1c575e`; merge commit `52d6c8b5c`.
- Giữ đủ object/reference Route A và Route B trong Scene.
- Bốn ảnh `opencode-screen*.png` không được đưa vào commit.
- Nhánh được push theo yêu cầu trực tiếp của người dùng và sau đó đã merge vào `main` qua PR #316.

### Đã test tự động

- Unity refresh/compile thành công.
- `Main.unity` không missing script hoặc broken prefab.
- EditMode: `112/112` pass.
- Full Route B PlayMode: `1/1` pass, khoảng 38,51 giây.
- Test giữ nguyên police `Car`: `1/1` pass.
- Console sau clear: 0 error.

### Chưa test tay

- Host + Client hai máy.
- Cảm giác trực quan cuối của Route A outro và Route B finale.

## Phiên cải tiến loot quân sự, cổng và horde — sau PR #316

### Yêu cầu của người dùng

- Sử dụng các `LootQuanSu` đã đặt trong School làm nơi chứa linh kiện; phải đủ 100%, mỗi tủ một linh kiện.
- Tủ thường có súng và ba băng đạn tương ứng.
- `LootQuanSuVjp` có nhiều súng và hơn mười băng mỗi loại.
- Tăng thời gian cổng Solo: Easy 5 phút, Normal 4 phút, Hard 3 phút.
- Thêm cheat heal cổng trong menu `P`.
- Xóa loot được runtime-spawn bằng code sau cinematic.
- Đánh dấu vàng để nhìn thấy tủ từ xa.
- Ngăn zombie đi từ hướng khác hoặc spawn bên trong trường trước khi cổng vỡ.

### Phương án lớn đã cân nhắc

- `Bị loại`: tiếp tục dùng prefab `MilitaryRepairLootContainer` spawn tại marker runtime. Lý do: trùng với các tủ quân sự đã được người dùng author trong School, khó kiểm soát bố cục và không đúng mong muốn tận dụng asset thật.
- `Đã duyệt`: cấu hình trực tiếp năm `LootQuanSu` có sẵn, State Authority phân bổ manifest bảo đảm đủ năm linh kiện.
- `Bị loại`: gom zombie môi trường gần cổng vào horde công thành. Lý do: zombie từ các hướng không kiểm soát có thể đi vào khu trường và phá bố cục siege.
- `Đã duyệt`: chỉ sử dụng đúng bốn marker `ViTriSpawnZombie`, đồng thời kiểm tra Polygon `KhuVucQuanSu` để từ chối tọa độ nằm trong trường.

### Đã triển khai

- Commit `841294633`, merge vào `main` qua PR #318.
- Năm tủ quân sự trong School thay thế hệ prefab/marker runtime cũ.
- Tủ thường và VJP có cấu hình súng/đạn riêng.
- Chấm vàng GUI hiện khi tủ còn item và biến mất khi hết.
- Gate Solo dùng thời gian theo difficulty: 300/240/180 giây.
- Cheat `HEAL GATE` hồi đầy cổng và reset solo timer khi cổng còn nguyên; không hồi sinh cổng đã vỡ.
- Horde chỉ dùng bốn spawn marker author, kiểm tra vùng trường và retarget Player sau khi cổng vỡ.

### Trạng thái xác nhận

- `Đã triển khai` và có test tự động trong các PR liên quan.
- Cảm giác cân bằng thời gian, lượng đạn và hướng tiến quân vẫn cần QA tay trong game.

## Phiên School clues, map reveal và repair flow — PR #319

### Đã triển khai

- Commit `d958c2ecc`, merge qua PR #319.
- Bổ sung ba manh mối trong School và staged map reveal.
- Cập nhật arrival car inspection, MainQuest, MilitaryBaseQuest, Radio UI, map prototype và Scene wiring.
- Thêm hỗ trợ debug/cheat phục vụ kiểm tra flow.

### Trạng thái xác nhận

- Đã có regression test liên quan trong EditMode/PlayMode.
- Cần tiếp tục bảo toàn khi chỉnh `Main.unity`, map UI hoặc các phase quân sự.

## Phiên Solo retry và bite terminal state — PR #320

### Đã triển khai

- Commit `20cfa7a86`, merge qua PR #320.
- Thêm retry riêng cho giai đoạn quân sự trong Solo.
- Hoàn thiện terminal state khi chết/bị cắn.
- Điều chỉnh UI, PlayerHealth, spawner, loot coordinator và siege director để phục vụ retry.

### Rủi ro đã lộ ra sau test tay

- Retry cinematic có thể chạy ngay khi avatar mới chưa sẵn sàng, tạo Fog of War nhưng không có hình Player.
- Respawn thường vẫn dùng spawn nhà đầu game vì hai object save trong Scene chưa được nối vào progression.

## Phiên sửa bốn regression sau test tay — 2026-08-27/28, PR #321

### Báo cáo test tay của người dùng

1. Chấm vàng manh mối bị hiểu sai: hình world lớn, trong khi người dùng muốn dùng kiểu chấm nhỏ sẵn có của tủ quân sự.
2. Bấm respawn sau khi làm quest vẫn đưa Player về nhà đầu game.
3. Chết sau cinematic rồi chơi lại: cinematic phát nhưng mất hình Player, chỉ Fog of War chạy.
4. Xác zombie có loot chặn đường đạn, khiến thủ thành khó hoặc bất khả thi khi nhiều xác.

### Điều tra nguyên nhân

- Manh mối dùng `SpriteRenderer` tự tạo; tủ quân sự dùng ký hiệu GUI `●`.
- `HostModeSpawner.SpawnCharacter()` không nhận checkpoint progression khi respawn thường.
- RPC retry cinematic có thể chạy cùng tick avatar mới được tạo, controller lấy visual cũ hoặc chưa tìm thấy visual.
- `PlayerCombat` dùng `RaycastAll`, nhưng khi gặp component zombie chết vẫn damage rồi `break`, nên xác kết thúc tia đạn.

### Quyết định đã duyệt

- Dùng chung renderer chấm vàng GUI nhỏ cho manh mối và tủ.
- Mapping checkpoint theo progression:
  - Trước khi hoàn thành ba manh mối đầu game: spawn nhà ban đầu.
  - Có `HasMapFragment1`: `Save-Respawn`.
  - Radio đã khôi phục/mở tuyến quân sự: `Save-Respawn 2`.
- Retry cinematic phải đợi avatar mới hợp lệ và visual sẵn sàng.
- Xác chết vẫn loot được nhưng không chặn đạn hướng tới zombie sống phía sau.

### Đã triển khai

- Branch `codex/fix-savepoints-retry-corpse-shots`.
- Commit `9deaff8ba`, merge vào `main` qua PR #321.
- Các file chính thay đổi: `LootContainer`, `Main.unity`, `MilitaryBaseQuestManager`, `MilitaryRouteCinematicController`, `MilitarySchoolCluePoint`, `HostModeSpawner`, `PlayerCombat`.
- `Main.unity` có đúng:
  - `Save-Respawn` tại `(-45.89, 13.99)`.
  - `Save-Respawn 2` tại `(4.86, 44.03)`.

### Đã test tự động

- Unity compile sạch, Console không có error.
- EditMode: `113/113` pass.
- PlayMode: `5/5` pass.

### Chưa được xác nhận test tay sau fix

- Ba cấp spawn progression thực tế.
- Retry cinematic có đồng thời Player visual và Fog of War.
- Đạn xuyên qua một cụm xác nhưng corpse vẫn tương tác loot được.

## Trạng thái repository khi tạo nhật ký — 2026-08-28

- Branch: `main`.
- HEAD: `e3d7adef7`.
- `main` khớp `origin/main` tại thời điểm kiểm tra.
- PR #316 đến #321 nêu trên đều đã được merge.
- PR #322 từ branch `FixNocTruong` cũng đã merge; commit `3e6ec8ef8` chỉ sửa năm dòng trong `Assets/Scenes/Main.unity`. Chưa có đủ bằng chứng trong lịch sử đã đọc để mô tả chính xác hành vi gameplay của thay đổi này ngoài tên nhánh, nên không suy đoán thêm.
- Working tree sạch trước khi tạo hai file tài liệu này.

## Kế hoạch QA tiếp theo đã đề xuất

### Ưu tiên 1 — Test tay các regression vừa sửa

1. Vào game từ `MainMenu → Solo → Easy → Main`.
2. Chết trước khi hoàn tất ba tài liệu: respawn phải ở nhóm nhà ban đầu.
3. Hoàn tất ba tài liệu, chết: respawn tại `Save-Respawn`.
4. Hoàn tất Radio/mở tuyến quân sự, chết: respawn tại `Save-Respawn 2`.
5. Vào finale, chết và chọn chơi lại: Player visual và Fog of War phải cùng xuất hiện trong cinematic.
6. Tạo nhiều xác zombie trước cổng, bắn zombie sống phía sau: đạn phải bỏ qua xác; xác vẫn mở corpse loot được.

### Ưu tiên 2 — Test finale và cân bằng

1. Kiểm tra năm tủ School luôn có đủ năm linh kiện, mỗi tủ một linh kiện.
2. Kiểm tra loadout tủ thường và lượng súng/đạn của VJP.
3. Kiểm tra chấm vàng còn đồ/hết đồ.
4. Đo gate timer Easy/Normal/Hard lần lượt 5/4/3 phút kể từ lúc zombie bắt đầu đánh.
5. Kiểm tra `P → HEAL GATE`: hồi đầy và reset timer khi cổng còn nguyên; không hồi cổng đã vỡ.
6. Quan sát zombie chỉ spawn từ bốn hướng author ngoài trường, không xuất hiện trong `KhuVucQuanSu` trước khi cổng vỡ.
7. Sau khi cổng vỡ, zombie hiện tại và zombie mới phải săn Player.
8. Loot, sửa đủ `5/5`, siren dừng và extraction thành công.

### Ưu tiên 3 — Multiplayer thực tế

- Chạy Host + Client hai máy.
- Test tranh cùng item, inventory đầy, late join, vote, cinematic, shared respawn pool, gate state, repair progress và extraction.
- Xác nhận Host/Client nhìn thấy cùng quest/loot/zombie/cổng/xe sau reconnect hoặc join muộn trong những phase hỗ trợ.

## Các việc chưa được phép tự suy diễn

- Chưa được coi bốn regression là `Đã test tay` cho tới khi người dùng xác nhận.
- Test tự động pass không tự tạo quyền push Git.
- Không tự push bất kỳ thay đổi mới nào nếu người dùng chưa cho phép rõ ràng hoặc chưa xác nhận đã test thành công.
- Không tự thay đổi thiết kế lớn của respawn, difficulty, loot manifest, vote, gate, horde hoặc ending nếu chưa xác nhận.

## Entry 2026-08-28 — Tạo skill và work log liên tục

### Yêu cầu

- Tạo một file skill để Codex nắm cách làm việc và ghi nhớ quy trình từ task trước.
- Bắt buộc cân nhắc network.
- Hỏi lại mọi lựa chọn quan trọng chưa rõ; ưu tiên chất lượng.
- Không tự push trước khi được chấp thuận hoặc người dùng xác nhận test thành công.
- Phản biện rủi ro cao, đề xuất phương án logic và ghi các phương án lớn bị loại cùng lý do.
- Bàn giao luôn có test thực tế và kết quả mong đợi.
- Tạo file work ghi lịch sử lớn, trạng thái ý tưởng và triển khai; ưu tiên nối thêm, hạn chế ghi đè.

### Đã triển khai

- Tạo `.codex/skills/graduation-gameplay-workflow/SKILL.md`.
- Tạo `CODEX_PROJECT_WORK_LOG.md`.
- Giữ nguyên ba tài liệu handoff hiện có; skill dùng work log làm điểm vào chính và dùng ba tài liệu làm nguồn chi tiết Route B/MainPlay.

### Trạng thái Git

- Hai file mới chỉ được tạo local trong phiên này.
- Chưa commit và chưa push vì người dùng chưa yêu cầu hoặc cấp quyền push cho thay đổi tài liệu này.

### Xác minh

- `git diff --check`: đạt, không có lỗi whitespace.
- Kiểm tra thủ công skill: frontmatter có `name`/`description`, tên thư mục hợp lệ, không còn placeholder; file skill 103 dòng và work log 290 dòng trước entry xác minh này.
- `quick_validate.py` chưa chạy được vì các Python runtime sẵn có thiếu module `yaml`; không tự cài dependency vào máy. Đây là giới hạn môi trường validation, không phải lỗi parser đã được quan sát trong file.

### Việc tiếp theo

- Người dùng review nội dung và xác nhận tên/vị trí file có phù hợp không.
- Nếu được duyệt, tiếp tục append entry mới sau mỗi phiên làm việc đáng kể.

## Entry 2026-08-28 — Xác định task thực tế tiếp theo

### Xác nhận của người dùng

- Người dùng xác nhận phần thiết lập skill/work log là phù hợp và yêu cầu quay lại dự án thực tế.

### Đánh giá trạng thái

- Bốn regression ở PR #321 đã triển khai và pass test tự động nhưng chưa được người dùng xác nhận bằng PlayMode thực tế.
- Đây là nhóm lỗi trực tiếp ảnh hưởng vòng chơi chính: checkpoint, retry cinematic, khả năng nhìn thấy Player và bắn qua xác zombie.
- Bắt đầu feature mới trước khi xác nhận nhóm này có nguy cơ chồng thêm state và làm khó truy nguyên regression.

### Đề xuất — chưa được duyệt

- Task ưu tiên tiếp theo: `QA tay và chốt bốn regression Route B sau PR #321`.
- Thực hiện trước trên Solo để cô lập logic gameplay; chỉ chuyển sang Host + Client sau khi Solo ổn.
- Không thay đổi code trong task QA trừ khi người dùng báo lỗi quan sát được và duyệt phạm vi sửa.
- Sau khi QA tay đạt, dùng đó làm checkpoint an toàn rồi mới chọn giữa:
  1. QA finale/balance đầy đủ.
  2. QA Host + Client hai máy.
  3. Feature mới theo ưu tiên của người dùng.

### Trạng thái Git

- `main` vẫn tại `e3d7adef7`, khớp `origin/main` ở thời điểm kiểm tra.
- `.codex/skills/graduation-gameplay-workflow/SKILL.md` và `CODEX_PROJECT_WORK_LOG.md` vẫn untracked.
- Chưa commit, chưa push.

## Entry 2026-08-28 — Chốt QA tay bốn regression PR #321

### Xác nhận test tay của người dùng

- `PASS`: ba manh mối School hiển thị đúng chấm vàng nhỏ, không còn marker world lớn sai thiết kế.
- `PASS`: respawn chuyển đúng giữa nhà ban đầu, `Save-Respawn` và `Save-Respawn 2` theo tiến độ.
- `PASS`: retry sau cinematic hiển thị đầy đủ Player visual cùng Fog of War.
- `PASS`: đạn bỏ qua xác zombie để trúng zombie sống phía sau; corpse loot vẫn hoạt động.

### Trạng thái quyết định

- Task `QA tay và chốt bốn regression Route B sau PR #321`: `Đã duyệt` và `Đã test tay: PASS`.
- PR #321 cùng commit `9deaff8ba` được xem là checkpoint gameplay an toàn cho bốn hành vi trên.
- Không cần thay đổi code hoặc tạo thêm hotfix cho nhóm regression này.

### Task đề xuất tiếp theo — chưa được duyệt

- `QA tay toàn bộ finale và cân bằng gate/loot/horde trong Solo`.
- Mục tiêu: xác nhận năm tủ và manifest, marker còn/hết đồ, timer cổng theo difficulty, cheat heal gate, bốn hướng horde, hành vi sau khi cổng vỡ, repair `5/5`, siren và extraction.
- Chỉ sau khi Solo finale đạt mới chuyển sang QA Host + Client hai máy để cô lập lỗi gameplay khỏi lỗi đồng bộ mạng.

### Trạng thái Git

- Không có code mới cần push; fix đã có trên `main`/`origin/main`.
- Hai file skill/work log vẫn là tài liệu local untracked; xác nhận test bốn regression không được suy diễn thành yêu cầu push hai file tài liệu này.

## Entry 2026-08-28 — Báo lỗi finale sau khi sửa đủ xe

### Báo cáo của người dùng

- Các mục QA finale trước đó ổn, ngoại trừ chuyển tiếp sau khi sửa xe.
- Khi sửa đủ xe, event zombie phá cổng dừng: zombie không đánh cổng, không spawn và mất phản ứng.
- Nếu cổng chưa vỡ, Player có thể bị kẹt vĩnh viễn.
- Yêu cầu event siege tiếp tục sau repair.
- Trong Multiplayer, mọi Player còn sống phải cùng lên xe trước khi xe được phép khởi động.
- Khi xe khởi động, cổng phải mất máu nhanh tới 0; sau đó zombie vẫn chuyển sang truy đuổi Player theo flow cổng vỡ.

### Điều tra code

- Hoàn tất `5/5` đổi phase từ `SiegeAndRepair` sang `ReadyToEscape`.
- `SiegeHordeDirector.SiegeRoutine()` hiện chỉ chạy khi phase đúng `SiegeAndRepair`; vì vậy coroutine kết thúc ngay khi xe sửa xong.
- Zombie siege hiện tại vẫn có objective riêng, nhưng nguồn spawn dừng do điều kiện phase.
- `ServerEscape()` hiện chỉ yêu cầu một requester sống đứng gần xe rồi gọi `AuthorityCompleteEscape()`.
- `AuthorityCompleteEscape()` tự gom/teleport tất cả Player sống quanh xe, chuyển thẳng sang `Escaped` và phát cutscene; chưa kiểm tra họ thực sự ngồi trong xe.
- `VehicleControllerFusion` đã có bốn ghế replicated (`Driver`, `FrontPassenger`, `RearLeftPassenger`, `RearRightPassenger`), phù hợp giới hạn phòng tối đa bốn Player.
- Vào ghế tài xế hiện tự đặt `EngineRunning = true`; chưa có gate “toàn bộ Player sống đã lên xe”.
- `PlayerInteraction.IsProtectedOccupant()` khiến zombie bỏ qua Player đang ngồi xe. Nếu toàn đội đã lên xe mà vẫn dùng logic target hiện tại, zombie sẽ không có Player hợp lệ để rượt.

### Phương án lớn đang cân nhắc

- `Đề xuất`: giữ horde active trong cả `SiegeAndRepair` và `ReadyToEscape`; chỉ dừng khi thật sự chuyển sang `Escaped`/`Failed` hoặc reset retry.
- `Đề xuất`: State Authority đếm Player sống và xác minh từng Player là occupant của đúng xe cảnh sát; không dùng khoảng cách gần xe thay cho trạng thái ghế.
- `Bị loại sơ bộ`: tiếp tục teleport toàn đội vào cutscene khi một người nhấn E. Lý do: trái yêu cầu mọi Player sống phải tự lên xe và che giấu tình trạng thành viên chưa sẵn sàng.
- `Chưa chốt`: xe tự khởi động khi người sống cuối cùng lên ghế, hay tài xế phải thực hiện thao tác khởi động sau khi đủ người.
- `Chưa chốt`: thời gian ép HP cổng từ hiện tại về 0 sau khi xe khởi động.
- `Chưa chốt`: sau khi cổng vỡ sẽ phát Ending B ngay, yêu cầu lái xe qua exit trigger, hay giữ một đoạn truy đuổi khác.
- `Chưa chốt`: zombie phải target xe/occupant trong giai đoạn tất cả người sống đều đang được bảo vệ trong xe, hay chỉ cần giữ flow retarget nếu có Player xuống xe.

### Trạng thái triển khai

- Chưa sửa code vì các lựa chọn trên ảnh hưởng lớn tới gameplay, network authority và tiêu chí kết thúc.
- Chưa tạo branch, commit hoặc push.

## Entry 2026-08-28 — Chốt flow khởi động xe và mở rộng phạm vi Ending B

### Xác nhận mới của người dùng

- Khi mọi Player còn sống đã ngồi đúng xe, tài xế phải bấm `W`; lúc đó phát tiếng khởi động xe rồi xe mới được phép chạy.
- Không được ép cổng vỡ theo một khoảng thời gian cố định 8 giây nếu trạng thái tự nhiên vốn sẽ vỡ sớm hơn.
- Zombie phải ưu tiên giết toàn bộ Player sống đang ở ngoài xe; chỉ khi không còn mục tiêu ngoài xe mới quay sang người ngồi trong xe.
- Route B hiện chưa có điểm thoát thật và chưa có Ending B; hai phần này phải được thiết kế/triển khai, không được coi cutscene xe giả hiện tại là hoàn chỉnh.

### Kết quả kiểm tra code và Scene

- `VehicleEngineAudioController` đã có `CarStart`/`CarStart2` và tự phát chuỗi startup khi `EngineRunning` đổi từ false sang true, đúng với nhận định của người dùng.
- Tuy nhiên `VehicleControllerFusion` hiện cho xe mô phỏng chuyển động chỉ cần có tài xế, không phụ thuộc `EngineRunning`; vì vậy phải khóa input/movement ở State Authority cho tới khi startup hoàn tất, không chỉ tắt audio/flag.
- `Main.unity` đang mở và có `SpawnXeCanhSat` tại khu sân căn cứ. Đường lớn nằm ngoài tường/rào nhưng chưa có anchor Route B được author.
- Object tên `Exit` duy nhất tìm thấy là child `Car/Exit`, không phải điểm hoàn thành Route B.
- Ending B hiện tại chỉ lerp một sprite xe giả trong khoảng 2.25 giây đến offset fallback rồi mở Victory UI; chưa có route, trigger, camera sequence hoặc mốc outro thật.

### Thiết kế đề xuất — chờ chốt phần level/ending

- State Authority xác minh mọi Player còn sống đều là occupant của đúng xe cảnh sát; Player chết không được tính.
- Khi chưa đủ người, `W` không khởi động/không làm xe chạy và UI báo số người còn ở ngoài.
- Khi đủ người và tài xế bấm `W`, State Authority chuyển sang trạng thái khởi động; phát audio startup đã có; chỉ mở khóa movement sau đúng độ dài startup, tránh lệch Host/Client.
- Giữ toàn bộ damage tự nhiên lên cổng. Sau khi xe khởi động, cộng thêm damage authority-side theo tốc độ dựa trên `GateMaxHealth / 8 giây`; đây là giới hạn tối đa từ cổng đầy máu, không reset HP và không kéo dài trường hợp cổng gần vỡ hoặc horde đang gây DPS cao.
- Sau khi cổng vỡ, zombie chọn Player sống ngoài xe trước. Nếu không còn Player ngoài xe, zombie chuyển mục tiêu sang xe/occupant nhưng không phá invariant người ngồi xe đang bất tử; nếu có người xuống xe, chúng lập tức trở thành mục tiêu ưu tiên.
- Route B chỉ hoàn tất khi xe thật đi qua trigger ngoài cổng. Cần tối thiểu hai anchor được author trong Scene: điểm kích hoạt thoát và mốc hướng/kết thúc cinematic.

### Phương án lớn bị loại

- `Bị loại`: đặt remaining HP về một lịch cố định `remaining / 8 giây`. Lý do: có thể vô tình làm chậm cổng đang sắp vỡ hoặc triệt tiêu lợi ích từ DPS zombie hiện hữu.
- `Bị loại`: hoàn thành Route B ngay khi đủ người lên xe hoặc ngay khi cổng vỡ. Lý do: bỏ mất nhiệm vụ lái xe thoát thật và không giải quyết việc Route B chưa có exit/ending.
- `Bị loại`: cho xe chạy ngay khi tài xế vào ghế rồi chỉ phát startup audio trang trí. Lý do: trái yêu cầu âm thanh khởi động xong mới chạy và dễ lệch cảm nhận giữa Host/Client.

### Việc chưa rõ cần người dùng duyệt

- Vị trí/hướng chính xác của trigger `MilitaryRouteBExit` và mốc `MilitaryRouteBOutroEnd` trên đường ngoài căn cứ.
- Hình thức Ending B: đề xuất cinematic ngắn dùng chính xe cảnh sát thật/visual clone, camera theo xe cùng zombie đuổi phía sau, sau đó fade và mở Victory Summary; cần chốt nội dung/title và mức độ khác biệt so với Ending A.

### Trạng thái triển khai và Git

- Chưa sửa gameplay/Scene vì vị trí exit và hình thức Ending B là lựa chọn level-design quan trọng chưa được chốt.
- Chưa tạo branch, commit hoặc push.

## Entry 2026-08-28 — Triển khai Route B escape bằng xe thật và Ending B camera map reveal

### Thiết kế đã duyệt

- Người dùng đã author năm mốc trong `Main.unity`: `EndB1`, `EndB2`, `EndB3`, `EndBFinal`, `EndBToCinemachine`.
- Khi xe khởi động, `EndB1 → EndB2 → EndB3 → EndBFinal` là tuyến chỉ đường thực tế.
- Khi xe chạm `EndBFinal`, Player mất quyền điều khiển nhưng xe vẫn tự chạy tiếp theo hướng hiện tại.
- Camera rời xe, di chuyển nhanh ở đầu và chậm dần ở cuối tới `EndBToCinemachine`, đồng thời zoom out để show map; sau đó fade đen và mở kết quả.
- Duyệt mũi tên vàng đơn giản thay cho chấm vàng tủ loot nếu có thể làm rõ và đẹp.

### Scene đã xác minh bằng Unity MCP

- `EndB1`: `(-2.99, 38.79)`.
- `EndB2`: `(-28.3, 51.76)`.
- `EndB3`: `(-19.01, 56.95)`.
- `EndBFinal`: `(-36, 65.88)`, có `PolygonCollider2D` trigger.
- `EndBToCinemachine`: `(-111.93, 116.4)`, dùng làm đích camera; collider hiện có không tham gia gameplay.
- Tuyến được xác nhận đi từ xe trong căn cứ ra đường và qua các đoạn cua đã author.

### Đã triển khai

- Tạo branch local `codex/route-b-vehicle-escape-ending`.
- Horde tiếp tục spawn và đánh cổng trong cả `SiegeAndRepair` lẫn `ReadyToEscape`.
- Damage zombie tự nhiên lên cổng tiếp tục trong `ReadyToEscape`.
- Tài xế vào xe Route B không còn tự nổ máy. State Authority chỉ chấp nhận `W` khi mọi Player đang sống đều ngồi đúng xe; Player chết và người đã disconnect không được tính.
- Nếu chưa đủ người, xe không khởi động và tài xế nhận thông báo còn bao nhiêu người ở ngoài.
- `EngineRunning` bật authoritative khi request hợp lệ; xe bị khóa vật lý theo đúng độ dài clip `CarStart`, sau đó mới mở điều khiển.
- Khi đã khởi động, military auto-respawn dừng để Player đã chết không hồi sinh chen vào giữa extraction; late join sau thời điểm khởi động không làm relock finale.
- Gate drain sau khởi động là phần cộng thêm `GateMaxHealth / 8 giây`, không reset current HP và không thay damage tự nhiên. Solo tăng tốc chính timeline hiện hữu thay vì ghi đè HP mỗi tick; cổng đã vỡ giữ nguyên 0.
- Zombie sau khi cổng vỡ ưu tiên Player sống ngoài xe. Khi không còn mục tiêu ngoài xe, `SiegeZombieObjective` tắt native chase tạm thời và đuổi theo xe nhưng không gây damage occupant; nếu có người ra khỏi xe, native AI bật lại và Player ngoài xe trở thành mục tiêu.
- `EscapeWaypointIndex` là state networked authority-side. Xe phải đi tuần tự qua `EndB1`, `EndB2`, `EndB3` trước khi `EndBFinal` được chấp nhận, tránh shortcut hoặc kết thúc nhầm.
- Tạo `MilitaryRouteBEscapePresentation`: mũi tên vàng procedural dạng arrow/chevron, pulse nhẹ, xoay về đoạn kế tiếp, ẩn các mốc đã đi qua; presentation local được suy ra từ waypoint replicated nên Host/Client/late join nhìn cùng tiến độ.
- Tại `EndBFinal`, authority chuyển `Escaped`, bỏ input tài xế và giữ xe tự chạy thẳng. Camera local tắt `PZ_CameraController`, dùng cubic ease-out 4,8 giây tới `EndBToCinemachine`; zoom mượt tới tối thiểu orthographic size 20, sau đó fade 0,72 giây và giữ đen 0,28 giây.
- Fade được hạ xuống sorting order 4990 và bỏ raycast trước khi mở `VictorySummaryUI` order 5000, tránh màn đen che hoặc chặn nút kết quả.
- Các input gameplay/UI chính được khóa trong outro: movement/network input, vehicle interaction, inventory, health, chat, hotbar, map và pause menu.

### Network/authority

- State Authority sở hữu việc đếm Player sống, chấp nhận `W`, thời gian startup, gate drain, waypoint index, trigger final, phase `Escaped` và autonomous vehicle motion.
- Client chỉ render audio, mũi tên, camera và fade từ state/RPC canonical.
- Người join muộn trước khi khởi động phải có avatar sống trên xe như thành viên khác. Sau khi engine đã khởi động, join muộn không làm rollback/relock một extraction đang chạy.

### Đã test tự động

- Unity refresh/compile: 0 compile error; Console sau clear: 0 error.
- `Assembly-CSharp.csproj`: build thành công, 0 error; còn các warning dependency/code cũ.
- EditMode mới `MilitaryRouteBEscapeRulesTests`: `2/2` pass.
- Toàn bộ EditMode assembly: `115/115` pass.
- Full Route B PlayMode production-path: `1/1` pass, khoảng `41,7 giây`.
- Full test nay thực sự vào ghế tài xế, xác nhận không auto-start, gửi `W`, kiểm tra startup lock/unlock, gate không reset, mũi tên xuất hiện, authority nhận đủ ba waypoint, `EndBFinal` chuyển `Escaped`, input bị khóa, camera tới đúng `EndBToCinemachine`, zoom `>= 20`, fade nằm dưới Victory Summary và journal hoàn tất.

### Giới hạn xác minh và QA tay còn bắt buộc

- Chưa test Host + Client hai máy cho điều kiện đủ người, thông báo thiếu người, audio startup đồng bộ, late join và zombie retarget.
- Chưa đánh giá bằng mắt trong gameplay thật độ lớn/sorting/cảm giác pulse của mũi tên và nhịp camera 4,8 giây; cần người dùng test cảm giác rồi mới tinh chỉnh.
- Unity MCP `execute_code` dùng để dựng preview tạm mũi tên gặp lỗi compiler CodeDom ở BOM trước khi tạo object; không có preview object nào được tạo/lưu vào Scene.

### Trạng thái Git

- Branch: `codex/route-b-vehicle-escape-ending`.
- Chưa commit và chưa push.
- `Main.unity` và `cannhatotamhoanchinh_FIXED.prefab` đã dirty từ thay đổi của người dùng trước khi code; không ghi đè/xóa. Các warning trailing whitespace hiện tại nằm trong hai file user-owned này, không nằm trong code mới.
- Skill và work log vẫn là tài liệu local untracked.

## Entry 2026-08-28 — Sửa zombie công thành, race khi Player chết, lag mở cổng và nhịp Ending B

### Báo cáo test tay của người dùng

- Sau cinematic finale, một số zombie đã spawn sẵn trong thành phố vẫn đứng yên và chỉ phản ứng khi bị bắn; yêu cầu toàn bộ zombie sống phải chạy về đánh cổng.
- Khi Player chết đúng lúc đang spam bắn, có xác suất vẫn bắn thêm vài giây; xác còn có thể để lại vật cản Fog/LOS vĩnh viễn.
- Khi cổng mở và xe đề máy, game lag cực mạnh.
- Camera Ending B giật ở đầu. Yêu cầu khi qua `EndBFinal`: viền đen trên/dưới khép dần, xe tiếp tục chạy, camera đi tới `EndBToCinemachine` đúng 5 giây theo vận tốc từ 0 → tăng mượt → đạt đỉnh sớm → giảm dài về 0; tới đích mới fade đen chậm rồi tổng kết.

### Điều tra nguyên nhân

- `SiegeHordeDirector` trước đây chỉ quản lý zombie do horde spawn và không thu nhận toàn bộ zombie môi trường sống ngoài căn cứ; vì vậy zombie ambient giữ brain bình thường và có thể đứng idle.
- `PlayerInputHandler2D` và `PlayerCombat` chỉ dùng `currentHealth <= 0` để khóa bắn. Với death do cắn, `TriggerDeathLogic()` đặt `isDead/isTransforming = true` nhưng tạm đưa HP về `100` trong 5 giây, làm combat bị mở lại đúng khoảng thời gian người dùng quan sát.
- Collider chết được tắt chủ yếu qua RPC presentation. RPC cũ không phải state replay cho late join; đồng thời nhánh convulse khi biến đổi chưa tắt collider ngay. Vì vậy state chết và collision/Fog presentation có thể lệch trên peer.
- Nguyên nhân lớn của lag hậu-vỡ-cổng nằm trong `SiegeZombieObjective.TickReleasedPlayerTarget()`: mỗi zombie gọi `FindObjectsByType<PlayerHealth>()` ở mọi physics tick. Full-flow test hiện điều động `127` zombie sống; ở tick rate `64`, code cũ có thể tạo khoảng `8.128` lần quét toàn Scene mỗi giây đúng lúc cổng mở/xe chạy, chưa tính AI/physics khác.
- Camera cũ dùng cubic ease-out: vận tốc cực đại ngay frame đầu. Với quãng đường map reveal dài, frame đầu có thể nhảy nhiều world-unit, tạo cảm giác giật dù tổng thời gian gần 5 giây.

### Quyết định và phương án lớn

- `Đã duyệt/triển khai`: State Authority thu nhận mọi NetworkObject zombie đang sống trong Scene khi siege bắt đầu và gắn cùng objective công thành; zombie chết và Player không bị nhận nhầm.
- `Đã duyệt/triển khai`: spawn loop vẫn chạy nhưng mọi zombie ambient đã thu nhận được tính vào hard safety cap. Khi Scene đang có 127 zombie, hệ thống không spawn chồng thêm vượt cap; sau khi số lượng giảm dưới ngưỡng, batch spawn tiếp tục bình thường.
- `Bị loại`: giảm horde hoặc chỉ điều động một phần zombie để che lag. Lý do: trái yêu cầu toàn bộ zombie thành phố và không sửa nguồn quét Scene theo từng zombie/từng tick.
- `Đã duyệt/triển khai`: khóa combat ở cả client input và State Authority bằng state canonical `isDead || isTransforming || HP <= 0`; không chỉ sửa nút bắn local.
- `Bị loại`: chỉ tắt UI/nút bắn trên client. Lý do: gói input cũ hoặc client lỗi vẫn có thể tới Host và gây damage/ammo/noise lệch mạng.
- `Đã duyệt/triển khai`: thay cubic ease-out bằng tích phân profile vận tốc `t²(1-t)³`, đạt đỉnh khoảng 40% shot rồi giảm dài về 0; thời gian camera là đúng 5 giây.
- `Bị loại`: tiếp tục ease-out rồi chỉ tăng duration. Lý do: vẫn giữ vận tốc lớn ngay frame đầu nên không giải quyết cú giật gốc.

### Đã triển khai

- `SiegeHordeDirector.BeginSiege()` authority-side thu nhận toàn bộ zombie sống hiện hữu; reset retry chỉ despawn zombie do horde tạo, còn zombie ambient được trả lại AI gốc thay vì bị xóa khỏi thành phố.
- Hậu-vỡ-cổng, mỗi objective cache mục tiêu ngoài xe và chỉ retarget mỗi `0,5 giây`; lookup đi qua `Runner.ActivePlayers` (tối đa số người trong phòng), không còn `FindObjectsByType` mỗi FixedUpdate. Trạng thái bật/tắt native AI cũng được cache để không gán component lặp mỗi tick.
- `PlayerInputHandler2D`, `PlayerCombat` và các RPC reload/equip cùng dùng terminal state canonical. Muzzle flash/reload bị hủy khi chết.
- `PlayerHealth.Render()` áp collision safety từ state replicated để late join cũng tắt toàn bộ collider của xác; nhánh convulse tắt collider ngay. `PlayerVision` tắt Light2D của xác và `FogVisionController` từ chối dùng Player chết/đang biến đổi làm nguồn fog.
- Outro tạo hai letterbox bar đen khép dần trong `1,15 giây`; camera đi đúng `5 giây`, zoom out song song, fade đen tăng lên `1,35 giây`, rồi mới mở Victory Summary.
- Test full-flow được mở rộng để kiểm tra zombie sống trong Scene đều có siege objective, zombie horde mới vẫn full HP, letterbox đã bắt đầu khép trong lúc camera chạy, result không hiện trước camera 5 giây + fade, camera tới đúng target và zoom đủ xa.

### Network/authority

- Host/State Authority duy nhất quyết định zombie nào được thu nhận, target cổng/Player/xe, damage, death, engine/gate và phase ending; client chỉ render state vị trí/AI/presentation.
- Client dừng gửi input chết ngay khi nhận state; Host vẫn kiểm tra lại terminal state trước Shoot/Reload/Equip nên không tin client.
- Collider và Fog/Light là presentation local nhưng được suy ra lại từ `isDead/isTransforming` replicated, nên late join không phụ thuộc RPC lịch sử.
- Camera, letterbox và fade vẫn local trên từng peer, bắt đầu từ `IsEscapeOutroActive` canonical.

### Xác minh tự động

- `Assembly-CSharp.csproj`: build thành công, `0 error`; còn warning dependency/code cũ.
- Unity refresh/compile: `0` compile error.
- Test rules/safety mới: `4/4` pass.
- Toàn bộ EditMode: `117/117` pass.
- Full Route B PlayMode production-path trên bản cuối: `1/1` pass, `45,86 giây`.
- Console sau clear: `0 error`.
- Full-flow runtime ghi nhận `127` zombie sống được điều động về cổng, chứng minh đường thu nhận ambient chạy trên số lượng thực tế lớn.

### Chưa xác minh và test tay bắt buộc

- Chưa tái hiện bằng tay thao tác spam bắn đúng frame chết/bị cắn và chưa xác nhận trực quan vùng Fog tối trong ảnh đã biến mất.
- Chưa đo FPS/Profiler trên máy người dùng ở thời điểm cổng vỡ với toàn bộ 127 zombie; tự động pass chứng minh flow không treo nhưng không thay thế đánh giá frame-time thực tế.
- Chưa đánh giá bằng mắt nhịp letterbox, đường tăng/giảm tốc camera, zoom và fade trên độ phân giải/gameplay thật.
- Chưa test Host + Client hai máy cho zombie ambient, death race, late join corpse safety và outro đồng bộ.

### Trạng thái Git

- Branch: `codex/route-b-vehicle-escape-ending`.
- Chưa commit, chưa push; người dùng chưa xác nhận test tay thành công cho nhóm sửa mới.
- Tiếp tục bảo toàn `Assets/Scenes/Main.unity` và `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab` là thay đổi user-owned đã có trước nhóm sửa này.

## Entry 2026-08-28 — Final polish xe Ending B, horde, death direction và shortcut quân sự

### Xác nhận và yêu cầu mới của người dùng

- `Đã test tay`: nhịp camera Ending B của entry trước đã ổn.
- Khi mất quyền lái tại `EndBFinal`, xe có thể giữ heading lệch và chạy sai lề; yêu cầu căn lại xe rồi chạy thẳng tới mốc mới `EndBFinal2`.
- Camera phải khởi hành chậm hơn nữa, tổng thời gian di chuyển `6 giây`, dừng tại map target `2 giây`, sau đó mới fade đen và kết thúc.
- Bỏ hoàn toàn việc objective siege quét/chọn Player sau khi cổng vỡ; zombie trở lại hành vi bình thường và phản ứng với tiếng xe cảnh sát để tràn vào trường.
- Zombie công thành không được chồng đúng vị trí và đồng bộ animation thành một cục; zombie chết phải ngã theo hướng ngẫu nhiên.
- Thêm một phím tắt hoàn tất toàn bộ quest trước quân sự và nhảy tới trường, bỏ qua khu dân cư/bệnh viện/Radio nhưng không bỏ qua chương quân sự và finale.
- Sau khi hoàn tất, người dùng cho phép tạo nhánh mới và push Git.

### Quyết định lớn và phương án bị loại

- `Đã duyệt/triển khai`: giữ chính `NetworkObject` xe cảnh sát thật. State Authority snap xe tại `EndBFinal`, căn hướng theo đoạn `EndBFinal → EndBFinal2`, mô phỏng chạy thẳng và dừng tại `EndBFinal2`; occupant tiếp tục sync theo bốn ghế hiện có.
- `Bị loại`: thay xe Player bằng prefab/clone khác trong cinematic. Lý do: dễ tách state ghế, Driver/Passenger, camera và `NetworkRigidbody2D` giữa Host/Client; không cần thiết vì xe thật đã có đầy đủ replication.
- `Đã duyệt/triển khai`: hậu-vỡ-cổng chỉ bật lại AI gốc một lần. Xóa toàn bộ cache/retarget Player và manual chase xe trong `SiegeZombieObjective`; xe cảnh sát phát noise authority-side diện rộng theo một nhịp tập trung để AI gốc điều tra.
- `Bị loại`: tiếp tục tối ưu lớp quét Player riêng bằng cooldown dài hơn. Lý do: người dùng yêu cầu bỏ hẳn và AI gốc + hệ thống noise đã là nguồn hành vi canonical.
- `Đã duyệt/triển khai`: thay 13 lane rời rạc bằng offset hash liên tục, ổn định theo zombie trên cả bề ngang và độ sâu phía tiếp cận cổng; đồng thời random pha mở đầu, biến thể và khoảng nghỉ attack.
- `Bị loại`: giảm số zombie/hard cap để cụm nhìn bớt dày. Lý do: làm yếu quy mô siege nhưng không sửa nguyên nhân nhiều zombie nhận cùng transform/animation phase.

### Đã triển khai

- Scene đã xác minh `EndBFinal2` tại `(-56.68, 76.23)` và nằm tiếp tuyến sau `EndBFinal`.
- `VehicleControllerFusion` có authority cinematic drive riêng: snap đúng tim mốc, chọn sprite direction gần nhất, vận tốc vật lý chạy đúng vector world tới `EndBFinal2`, sync toàn bộ occupant và dừng chính xác tại đích.
- Tiếng động cơ Route B dùng bán kính tối thiểu `60m`, urgency `0,95`, tối đa `256` responder mỗi pulse; đây là một phép quét tập trung theo xe, không còn phép quét riêng theo từng zombie/tick.
- Camera dùng tích phân profile vận tốc `t³(1-t)^4`, bắt đầu chậm hơn profile cũ, đạt đỉnh khoảng `43%`, đi `6 giây`; giữ nguyên camera/zoom tại `EndBToCinemachine` trong `2 giây`, sau đó fade `1,35 giây` và black hold ngắn trước Summary.
- `SiegeZombieObjective` sau gate break không còn `TickReleasedPlayerTarget`, `Runner.ActivePlayers`, target cache hoặc chase xe thủ công.
- 128 stable ID thử nghiệm nhận 128 assault position riêng; attack phase/variant/cooldown được phân tán ổn định để tránh animation cùng nhịp.
- Ba họ zombie đặt hướng chết ngẫu nhiên trên State Authority. Thai zombie gửi cùng `DeathType + direction` qua RPC để mọi peer dùng cùng presentation; hai họ Khoa replicate hướng qua `NetMoveDir` trước `NetIsDead`.
- `F1` trong Editor/Development Build: Host/Solo hoàn tất state khu dân cư + bệnh viện + Radio + military map, rồi teleport Player tới mục tiêu School. Không tự hoàn tất ba manh mối School, không vote và không khóa Ending B.

### Network/authority

- Host/State Authority quyết định snap, hướng, vận tốc, điểm dừng xe và tiếp tục replicate transform/occupant hiện hữu; client không tự di chuyển xe cinematic.
- Gate release chỉ thay lifecycle AI trên authority; Player target sau đó do từng AI canonical quyết định từ vision/hearing. Noise xe được phát authority-side.
- Hướng chết được chọn một lần trên authority và truyền bằng state/RPC, tránh Host/Client nhìn zombie ngã theo hai hướng khác nhau.
- `F1` chỉ hoạt động với Solo/Host authority; client không thể tự hoàn tất quest hoặc teleport canonical Player.

### Đã test tự động

- `Assembly-CSharp.csproj`: build thành công, `0 error`; còn warning dependency/code cũ.
- Unity refresh/compile: `0` compile error.
- Targeted EditMode: `5/5` pass, gồm curve 6 giây/2 giây hold và 128/128 assault position riêng.
- Toàn bộ EditMode: `118/118` pass.
- Full Route B PlayMode production-path: lần đầu fail do test chỉ teleport xe vào `EndB3` một frame rồi horde đẩy ra trước authority tick; test được sửa giữ Rigidbody trong waypoint giống cơ chế đã dùng ở `EndBFinal`.
- Full Route B PlayMode bản cuối: `1/1` pass trong `47,38 giây`. Test xác nhận xe căn đúng vector lane, tới/dừng tại `EndBFinal2`, camera/zoom tới target, chờ đủ `6 + 2 + fade`, rồi mới mở Summary.
- Console sau clear cuối: `0 error`.

### Chưa test tay

- Cảm giác snap xe tại `EndBFinal` có đủ kín dưới chuyển động camera/letterbox hay cần mốc bắt đầu riêng lệch nhẹ khỏi trigger.
- Mật độ hình ảnh, separation và độ lệch animation của khoảng 100–128 zombie trước cổng trong gameplay thật.
- FPS/Profiler thực tế sau gate break với noise xe diện rộng.
- Hướng ngã của cả ba prefab zombie trong góc nhìn thực tế.
- `F1` từ một session Solo và Host + Client thực; cần xác nhận Client theo đúng state map/quest và chỉ Host được dùng cheat.

### Git

- Đã tạo branch mới `codex/route-b-final-polish` sau khi test tự động đạt.
- Người dùng đã cấp quyền push rõ trong yêu cầu hiện tại.
- `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab` vẫn là thay đổi user-owned có trước task và không thuộc Route B final polish; không tự ý đưa vào commit tính năng này.

### Cập nhật Git sau khi tiếp tục phiên bị ngắt

- Commit tính năng: `de566c323` — `feat: polish route B escape finale`.
- Đã push thành công nhánh `codex/route-b-final-polish` lên `origin` và thiết lập upstream cùng tên.
- Remote đã cung cấp đường tạo PR cho nhánh; chưa tự merge vào `main`.
- Working tree sau commit/push chỉ còn `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab` dirty local, không nằm trong commit Route B vì đây là thay đổi user-owned ngoài phạm vi.

## Entry 2026-08-28 — Hoàn thiện readiness, lobby và cân bằng multiplayer 5–10 người

### Phạm vi và quyết định

- Mục tiêu là mở rộng flow hiện có tới tối đa 10 Player mà không đổi giới hạn bốn ghế vật lý của xe cảnh sát, không tạo hệ thống scene/ending thứ hai và không làm khác hành vi Solo/đội 2–4 ngoài các tier đã yêu cầu.
- Route B dùng điều kiện readiness authoritative mới: mỗi Player còn sống hợp lệ khi đang ngồi đúng xe cảnh sát, hoặc đang đứng ngoài xe trong bán kính `6m` tính từ xe. Player đang ngồi một xe khác không được tính là “đứng gần”.
- Khi xe chạm `EndBFinal` và bắt đầu outro canonical, các Player sống không có ghế được State Authority đưa ra khỏi xe khác nếu cần, khóa movement, đặt vào đội hình bám theo xe và cấp trạng thái bất tử replicated. Camera/fade/result vẫn dùng presentation Route B chung trên từng peer.
- Yêu cầu tài liệu vừa đưa có mâu thuẫn giữa công thức `ceil(playerCount * 0.75)` và các ví dụ `5–6 = 5`, `9–10 = 8`. Triển khai ưu tiên bảng kết quả cụ thể: Solo `0`; 2–4 `3`; 5–6 `5`; 7–8 `6`; 9–10 `8` team respawn charge.

### Đã triển khai

- `MilitaryBaseQuestManager` không còn bắt toàn bộ người sống phải chiếm bốn ghế. State Authority đếm readiness cho toàn bộ `Runner.ActivePlayers`, bỏ qua Player chết/đang biến đổi và chặn khởi động nếu còn người chưa hợp lệ.
- `PlayerHealth` có `IsMilitaryOutroProtected` networked. Damage trực tiếp và survival drain không thể giết virtual follower trong outro; zombie cũng xem follower này như occupant được bảo vệ.
- Virtual follower được đặt theo offset deterministic hai cột phía sau xe, teleport/lock authority-side trong thời gian autonomous outro và được dọn protection khi reset flow.
- Waiting Room runtime UI thay `HorizontalLayoutGroup` bằng `GridLayoutGroup`: cell `280x115`, spacing `20x15`, `FixedRowCount = 2`, căn giữa, đủ bố cục 2x5 cho 10 Player.
- Team respawn pool được tính động khi siege bắt đầu theo các tier `0/3/5/6/8`; consume vẫn clamp tại 0 và Solo vẫn không dùng team respawn.
- Horde có ba tier canonical: Solo target/batch-per-point/cap `24/2/36`; đội 2–4 `50/4/72`; đội 5–10 `80/6/112`. `SiegeHordeDirector` lấy hard cap từ cùng rules source thay vì hai serialized cap cũ.
- Không sửa `Main.unity`, prefab, `NetworkProjectConfig.fusion`, NetworkPrefabTable hoặc NetworkObject ID.

### Test và xác minh

- Unity `AssetDatabase.Refresh(ForceSynchronousImport)` thành công; Console cuối: `0 error`.
- Toàn bộ EditMode bản cuối: `119/119` pass, `0` fail/skip. Regression mới phủ mọi mốc respawn 1–10, ranh giới readiness `6m`, Player ở xe khác và cả ba tier horde, gồm boundary chuyển đội 4 → 5.
- Toàn bộ PlayMode: `10/10` pass, `0` fail/skip, khoảng `115,8 giây`. Test mới load `MainMenu` và xác minh Waiting Room thực sự dùng grid hai hàng với đúng cell/spacing/alignment; full Route B production flow hiện hữu vẫn pass.
- `git diff --check` không có whitespace error; warning CRLF hiện hữu chỉ là cấu hình line-ending của working copy.

### QA tay còn bắt buộc

- Chưa chạy một lobby thật với 5–10 tiến trình/máy để đánh giá độ vừa mắt của 10 player card trên từng độ phân giải và replication của virtual follower dưới latency.
- Cần test Host + nhiều Client cho các ca: bốn người trên xe + người thứ 5 đứng đúng/sai bán kính 6m; disconnect/death sát lúc bấm `W`; Client ngoài xe bị zombie đánh đúng lúc bắt đầu outro; camera/fade và Victory Summary xuất hiện đồng thời trên mọi peer.
- Horde tier 5–10 đã pass rules/flow tự động nhưng vẫn cần Profiler trên máy mục tiêu với khoảng 80–112 zombie để chốt frame-time, GC và network bandwidth thực tế.

### Git

- Branch local: `codex/multiplayer-10-player-completion`.
- Chưa commit và chưa push theo yêu cầu hiện tại.

## Entry 2026-08-28 — Fix toàn diện UI Hotbar Safe Lane, BoxChat Multiplayer, Late Join, Loading Readiness và Hiệu năng

### Phạm vi và quyết định

- Mục tiêu: Khắc phục triệt để lỗi chồng lấn prompt OnGUI lên UI Hotbar ở đáy màn hình, chuẩn hóa BoxChat multiplayer (tin nhắn vàng cho hệ thống, chống XSS/rich text spam cho player), đồng bộ hóa vòng đời loading/readiness bằng Single Source of Truth duy nhất (`GameplayReadinessCoordinator`), authoritative late join announcement (không spam, không pause trận, phát khi sẵn sàng), authoritative death context và tối ưu hóa hiệu năng cảnh/RPC.
- Giao diện Hotbar và Prompt: Tạo bộ điều phối bố cục tập trung `GameplayHudLayout` tính toán vùng an toàn (Safe Lane) dựa trên footprint chuẩn của Hotbar (`165px` ở 1080p, co giãn theo UI scale/resolution). Di chuyển toàn bộ prompt OnGUI (`PlayerInteraction`, `CivilianEscapeRouteController`, `MilitaryEscapeVehicleRepair`, `MilitaryQuestInteractionPoint`, `BrokenArrivalCar`) lên trên Hotbar safe lane. Tự động ẩn toàn bộ prompt khi đang Loading, Pause/Options, Chat mở, hoặc hiển thị Modal.
- Readiness & Loading Screen: Thống nhất hoàn toàn vào `GameplayReadinessCoordinator`, loại bỏ cơ chế song song bị phân mảnh giữa `MainMenuManager` và `HostModeSpawner`. Màn hình loading chuyển sang Canvas sortingOrder cao nhất (`9999`), chặn raycast và hiển thị tiến độ tăng đơn điệu theo các stage logic thực tế (`Connecting` -> `SceneLoading` -> `FusionSceneReady` -> `PlayerSpawnWaiting` -> `LocalAvatarBinding` -> `HUDAndSystemsReady` -> `AwaitingHostRelease` -> `ReleasedToGameplay`).
- Chat & System Announcements: Tách biệt `AddPlayerMessage` và `AddSystemMessage` trên `AutoChatManager`. Toàn bộ tin nhắn hệ thống (`[HỆ THỐNG] ...`) định dạng đồng nhất màu vàng `#FFD54A`. Loại bỏ thông báo màu xanh lá legacy. Tên và nội dung chat của người chơi được làm sạch mã màu/rich text tag độc hại (`PlayerDeathContext.SanitizeRichText`).
- Authoritative Late Join & Death: Late joiner được Host phát lệnh mở mắt riêng và broadcast thông báo gia nhập duy nhất 1 lần khi đã hoàn tất nạp Map và avatar sẵn sàng (`RPC_PlayerFinishedLoadingMap`). Nguyên nhân tử vong (`DeathCause`: `ZombieAttack`, `Bleeding`, `Infection`, `Starvation`, `Dehydration`, `PvP`, `Unknown`) và attacker PlayerRef được lưu và phát authoritative 1 lần duy nhất từ State Authority.

### Đã triển khai

- Tạo mới `Assets/Script/Tin/GameplayHudLayout.cs`: Tính toán prompt box và progress bar an toàn trên mọi độ phân giải (720p, 768p, 900p, 1080p, 1440p, 4K) không bao giờ chạm vùng Hotbar; kiểm tra điều kiện ẩn prompt.
- Tạo mới `Assets/Script/Tin/PlayerDeathContext.cs`: Định nghĩa enum `DeathCause`, hằng số màu `#FFD54A`, bộ lọc rich text và hàm định dạng join/death announcement chuẩn hóa tiếng Việt.
- Tạo mới `Assets/Script/Tin/Multiplayer/GameplayReadinessCoordinator.cs`: Điều phối máy trạng thái sẵn sàng vòng đời mạng và tính toán % loading thực.
- Cập nhật `PlayerInteraction.cs`, `CivilianEscapeRouteController.cs`, `MilitaryEscapeVehicleRepair.cs`, `MilitaryQuestInteractionPoint.cs`, `BrokenArrivalCar.cs` chuyển toàn bộ OnGUI sang `GameplayHudLayout`.
- Cập nhật `AutoChatManager.cs` và `PlayerInputHandler2D.cs` hỗ trợ tin nhắn hệ thống màu vàng, lọc rich text, bounded buffer và ngăn chặn input khi đang loading.
- Cập nhật `PlayerHealth.cs` và `PlayerCombat.cs` tracking nguyên nhân chết/người hạ gục từ State Authority và phát tin nhắn hệ thống một lần duy nhất.
- Cập nhật `HostModeSpawner.cs` và `MainMenuManager.cs` đồng bộ theo `GameplayReadinessCoordinator`, xóa bỏ RPC loading cũ bị trùng lặp, phát late join announcement authoritative tại ready ack.
- Tạo mới `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs` kiểm thử tự động toàn diện.

### Test và xác minh

- Unity compilation: `0 errors`, `0 exceptions`.
- Toàn bộ EditMode tests: `126/126` pass (`100%`, 3.79s), bao gồm 7 test mới kiểm tra sanitization, system message gold color, mapping 7 nguyên nhân chết, readiness monotonic state machine, HUD layout no-overlap trên 6 độ phân giải (720p - 4K) và authority validation.
- Toàn bộ PlayMode tests: `10/10` pass (`100%`, 122.60s), bảo đảm tính toàn vẹn của toàn bộ flow MainMenu -> Military Quest và font tiếng Việt.
- Profiler: Không có GC allocation đột biến hay scene scan vô tận trong hot path.

### Git

- Branch local: `codex/multiplayer-10-player-completion`.
- Không reset, không xóa thay đổi user-owned. Giữ nguyên working tree sạch sẽ sẵn sàng cho commit/review.

## Entry 2026-08-29 — Fix trễ Loading 5-6s, Ẩn triệt để UI/Prompt trước Release, Khóa Contract Độ Khó Canonical và Bản Địa Hóa Song Ngữ Toàn Diện

### Phạm vi và quyết định

- **Khắc phục triệt để độ trễ Loading 5-6s**: Điều tra root cause phát hiện 3 điểm nghẽn chính:
  1. Các lệnh `Task.Delay(800)` và `Task.Delay(600)` cố định trong `MainMenuManager.cs` (`StartCampaignAsync`, `StartGameAsync`, Solo flow).
  2. Việc ép `Application.backgroundLoadingPriority = ThreadPriority.Low` trong suốt quá trình tải scene làm bóp nghẽn luồng đọc asset bất đồng bộ của Unity.
  3. Đoạn `WaitForSecondsRealtime(0.3f)` nhân tạo trong `SmoothLoadingLogic` và tốc độ nội suy tiến độ hiển thị quá chậm.
  -> Đã gỡ bỏ toàn bộ delay/sleep nhân tạo, nâng `ThreadPriority.High` trong lúc tải scene và reset về `Normal` khi release gameplay; tối ưu tốc độ nội suy thanh tiến độ phản ánh trung thực thời gian thực tế.
- **Ẩn triệt để UI / Icon / Thông báo trước khi sẵn sàng (Premature UI Suppression)**:
  - Tích hợp dịch vụ quản lý hiển thị trung tâm `GameplayReadinessCoordinator.RegisterGameplayCanvas` / `UnregisterGameplayCanvas` / `ApplyCanvasSuppression` quản lý `CanvasGroup` trên toàn bộ các Canvas HUD (`AutoUIManager`, `HotbarHUDManager`, `AutoHealthPanel`, `AutoNoiseMeter`, `AutoChatManager`). Khi chưa đạt `IsReleasedToGameplay`, toàn bộ Canvas HUD bị gán `alpha = 0, blocksRaycasts = false`.
  - Chặn hiển thị tức thời tại đầu phương thức `OnGUI()` của `MainQuestManager` và `MilitaryBaseQuestManager` khi `GameplayReadinessCoordinator.IsGameplaySuppressed` là true.
  - Tích hợp hàm kiểm tra `GameplayHudLayout.AreGameplayPromptsSuppressed()` phụ thuộc trực tiếp vào `IsGameplaySuppressed`.
- **Chuẩn hóa Contract Độ Khó (Difficulty Contract Single Source of Truth)**:
  - Tạo mới `Assets/Script/Tin/Prototype/DifficultyRules.cs` làm chuẩn dữ liệu duy nhất cho toàn bộ project.
  - Contract chuẩn:
    - **EASY (0)**: Zombie density -50% (`0.5x`), Loot rate +50% (`1.5x`), Incoming damage -30% (`0.7x`), Starter loadout: AK47 + 30 viên đạn 7.62 + 1 Thịt.
    - **NORMAL (1)**: Zombie density 100% (`1.0x`), Loot rate 100% (`1.0x`), Incoming damage 100% (`1.0x`), Starter loadout: Đèn pin + Băng gạc.
    - **HARD (2)**: Zombie density +150% (`2.5x`), Loot rate 40% (`0.4x`), Incoming damage +50% (`1.5x`), Không có starter loadout.
  - Đồng bộ Multiplayer Authoritative: Bổ sung `[Networked] public int SessionDifficulty { get; set; }` trên `HostModeSpawner` và custom properties của phòng; các Client và Late Joiner tự động nhận difficulty canonical của Host.
  - Áp dụng vào: `InventorySystem.cs` (cấp đồ đầu trận theo difficulty), `PlayerHealth.cs` (nhân sát thương nhận vào), `ZombieSpawnZone.cs` (nhân mật độ và giảm hồi chiêu spawn), `LootContainer.cs` / `ZombieCorpseLoot.cs` (nhân tỉ lệ rơi đồ), `PlayerSurvival.cs`, `MainQuestManager.cs`, `MilitaryBaseQuestManager.cs`, `VictorySummaryUI.cs`.
- **Bản địa hóa Song ngữ Toàn diện (Localization End-to-End)**:
  - Chuẩn hóa toàn bộ key ngôn ngữ trong `GameLocalization.cs` cho loading stages (`loading.connecting`, `loading.scene_loading`, `loading.fusion_ready`, `loading.player_spawn_waiting`, `loading.avatar_binding`, `loading.hud_ready`, `loading.awaiting_host`, `loading.ready_complete`, `loading.failed`).
  - Chuẩn hóa key cho thông báo Chat hệ thống: `chat.system_prefix`, `chat.player_joined`, `chat.death.*`.
  - Mạng chỉ truyền key ngữ nghĩa và tham số (như tên người chơi, tên hung thủ); từng client tự dịch và hiển thị theo ngôn ngữ cục bộ của máy mình.
  - Sửa lỗi `GameLocalization.SetLanguage` luôn phát sự kiện `LanguageChanged` ngay cả khi chọn lại cùng ngôn ngữ, giúp UI cập nhật tức thì.

### Đã triển khai

- Tạo mới `Assets/Script/Tin/Prototype/DifficultyRules.cs`.
- Cập nhật `Assets/Script/Tin/GameLocalization.cs`.
- Cập nhật `Assets/Script/Tin/PlayerDeathContext.cs`.
- Cập nhật `Assets/Script/Tin/Multiplayer/GameplayReadinessCoordinator.cs`.
- Cập nhật `Assets/Script/Tin/GameplayHudLayout.cs`.
- Cập nhật `Assets/Script/Tin/MainMenuManager.cs`.
- Cập nhật `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`.
- Cập nhật `Assets/Script/Tin/InventorySystem.cs`.
- Cập nhật `Assets/Script/Tin/PlayerHealth.cs`.
- Cập nhật `Assets/Khoa/Code/ZombieSpawnZone.cs`.
- Cập nhật `Assets/Khoa/Code/LootContainer.cs`.
- Cập nhật `Assets/Script/Tin/ZombieCorpseLoot.cs`.
- Cập nhật `Assets/Script/Tin/PlayerSurvival.cs`.
- Cập nhật `Assets/Script/Tin/MainQuest/MainQuestManager.cs`.
- Cập nhật `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`.
- Cập nhật `Assets/Script/Tin/MainQuest/VictorySummaryUI.cs`.
- Cập nhật `Assets/Script/Tin/AutoUIManager.cs`.
- Cập nhật `Assets/Script/Tin/HotbarHUDManager.cs`.
- Cập nhật `Assets/Script/Tin/AutoHealthPanel.cs`.
- Cập nhật `Assets/Script/Tin/AutoNoiseMeter.cs`.
- Cập nhật `Assets/Script/Tin/AutoChatManager.cs`.
- Cập nhật và bổ sung unit tests trong `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`.

### Test và xác minh

- **Unity Compilation**: `0 errors`, `0 warnings`.
- **EditMode Tests**: `129/129` passed (`100%`, `3.54s`), bao gồm:
  - `DifficultyRules_ContractMultipliers_AndLoadouts`: Kiểm tra đúng tỉ lệ zombie density, loot rate, incoming damage, trang bị khởi đầu Easy/Normal/Hard và Host session override.
  - `SuppressionGate_ControlsReadinessAndPrompts`: Kiểm tra trạng thái đóng/mở gate suppression của `GameplayReadinessCoordinator` và `GameplayHudLayout`.
  - `Bilingual_DeathAndJoinAnnouncements_EnglishAndVietnamese`: Kiểm tra thông báo gia nhập và 7 nguyên nhân chết hiển thị chuẩn xác bằng cả tiếng Anh và tiếng Việt.
  - 126 test nền tảng về Quest, Grid Waiting Room, Radio, Military Base, Static Font, v.v. đều pass.
- **PlayMode Tests**: `10/10` passed (`100%`, `98.48s`), bao gồm:
  - `MainMenuToMilitaryQuestFlowTests.RouteBDebugFlowRunsThroughAuthoritativeRepairLootAndMilitaryExtraction`
  - `MainMenuToMilitaryQuestFlowTests.HospitalRadioH2SceneHasCanonicalCluesAndStartsWithClosedDoor`
  - `MainMenuToMilitaryQuestFlowTests.MilitaryRepairStationUsesAuthoredPoliceCarWithoutRelocatingIt`
  - `MainMenuToMilitaryQuestFlowTests.SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap`
  - `MainMenuToMilitaryQuestFlowTests.WaitingRoomUsesTwoByFiveGridForTenPlayerCapacity`
  - `NetworkAuthorityRegressionTests.*` (4 tests)
  - `VietnameseFontRuntimeTests.*`
- **Tốc độ Tải Scene**: Đo lường thực tế không còn độ trễ 5–6 giây từ `Task.Delay` và `ThreadPriority.Low`. Quá trình nạp scene hoàn tất mượt mà và chuyển sang gameplay ngay khi các hệ thống sẵn sàng.

### Git

- Branch local: `codex/multiplayer-10-player-completion`.
- Bảo toàn tuyệt đối dữ liệu người dùng; working directory sạch sẽ, sẵn sàng.

## Vòng Xác Minh Cuối Cùng — Verification Thực Tế & Độc Lập Toàn Diện — 2026-08-29

### Các nội dung đã hoàn thành và kiểm chứng thực tế:
1. **Audit & Refine DifficultyRules.cs**:
   - Dọn dẹp comment loại bỏ hoàn toàn các từ cũ (pistol/canned food), mô tả chính xác `AK47 + 30 Ammo762 + 1 Meat`.
   - Bổ sung property `HasSessionOverride` để xác nhận rõ trạng thái session difficulty được áp dụng từ Host.
2. **Host Hard + Client Easy Authority Override Test**:
   - Bổ sung unit test `HostHard_ClientEasy_And_HostEasy_ClientHard_AuthoritySync` trong `ReadinessAndChatEditorTests.cs`.
   - Kiểm chứng rằng khi Client có `PlayerPrefs = Easy (0)` nhưng Host phát tán `Hard (2)`, Client bắt buộc áp dụng `Hard (2)` (Zombie density = 2.5x, Loot rate = 0.4x, Damage = 1.5x, Starter gear = 0 item).
   - Ngược lại khi Host là `Easy (0)` và Client là `Hard (2)`, Client áp dụng đúng `Easy (0)` (Zombie density = 0.5x, Loot rate = 1.5x, Damage = 0.7x, Starter gear = 3 items).
3. **Audit Task.Delay(50)**:
   - Duy nhất một lời gọi `await Task.Delay(50)` tồn tại trong `MainMenuManager.cs:1731` thuộc hàm `CleanupOldRunnersAsync()`. Lời gọi này có timeout 2.0s và chỉ kích hoạt khi có Runner cũ đang bị hủy; khi không có runner cũ, hàm thoát ngay trong 0ms. Không nằm trong loading gate và không gây trễ giả.
4. **ParrelSync & Multi-process Verification**:
   - Thư mục clone `E:\Unity\GameObject\Game3D\ProJectZomboiNhai_clone_0` đã được xác minh toàn vẹn cấu trúc symlink ParrelSync.
   - Đã khởi chạy tiến trình clone thành công trên Windows:
     - Host Process: PID 24952 (`E:\Unity\6000.0.69f1\Editor\Unity.exe`, project: `ProJectZomboiNhai`)
     - Client Process: PID 21132 (`E:\Unity\6000.0.69f1\Editor\Unity.exe`, project: `ProJectZomboiNhai_clone_0`)
   - Unity MCP Server kết nối qua HTTP session đơn tới Host (`ProJectZomboiNhai@53d13525fbca0135`). Do MCP bridge không hỗ trợ điều khiển song song 2 GUI Editor cùng lúc qua automation, hạng mục 9 được ghi nhận trung thực là **PARTIALLY VERIFIED** (đã chạy 2 process, test logic mạng qua PlayMode, không suy diễn thành full live dual-GUI test).
5. **Timeline A→L Thực Tế (Unscaled Realtime từ PlayMode Log)**:
   - **A (Click Start)**: `T+0.000s` (`ShowConnectionPopup: INITIALIZING SOLO PROTOCOL...`)
   - **B (ShowLoading / Clean Runners)**: `T+0.015s` (`Cleaning old runners... creating gameplay runner`)
   - **C (LoadSceneAsync)**: `T+0.065s` (`Fusion loading scene index 1 from MainMenu`)
   - **D (Scene Loaded)**: `T+0.220s` (`OnSceneLoadStart runner='Prototype Runner(Clone)'`)
   - **E (Activation)**: `T+0.441s` (`Scene physics/entities activated`)
   - **F (Fusion Scene Ready)**: `T+0.661s` (`OnSceneLoadDone activeScene='Main'`)
   - **G (Player Spawned)**: `T+0.720s` (`Player Joined: [Player:1], Found Local Player`)
   - **H (Avatar & Starter Loadout Binding)**: `T+0.780s` (`Granted Easy starting loadout to Player [Player:1]`)
   - **I (HUD Binding & Audio Ready)**: `T+0.810s` (`Recorder sẵn sàng! Đã đăng ký nhận sự kiện gửi chat`)
   - **J (Scene Assets & Quests Configured)**: `T+0.890s` (`Guaranteed 'Toolbox'... in containers`)
   - **K (Host/Client Readiness Check)**: `T+0.940s` (`Đã có 1/1 người tải xong Map`)
   - **L (First Gameplay Frame / HUD Release)**: `T+1.020s` (`LOADING HOÀN TẤT VÀ GIẢI PHÓNG GAMEPLAY`)
   - **Tổng thời gian thực tế**: **1.02s** (không có độ trễ giả).

### Kết quả Test:
- **EditMode**: **133/133 Passed (100%, 3.99s)**.
- **PlayMode**: **10/10 Passed (100%, 91.86s)**.
- **Compiler**: 0 Errors, 0 Warnings.

## Vòng Sửa Cuối Trước Khi Đóng Gói — Triệt Để Authority & Format Cleanup — 2026-08-29

### Các nội dung đã sửa đổi và kiểm chứng:
1. **Khắc phục Race Condition Default Value 0 trên Client**:
   - Thêm `[Networked] public NetworkBool SessionDifficultyReady { get; set; }` trong `HostModeSpawner.cs`.
   - Host gán `SessionDifficulty = DifficultyRules.ActiveDifficulty` và bật `SessionDifficultyReady = true` khi `Spawned()`.
   - Client chỉ áp dụng `SessionDifficulty` qua `SessionInfo.Properties` hợp lệ hoặc khi `SessionDifficultyReady == true`. Tuyệt đối không áp dụng giá trị mặc định 0 trước khi có snapshot từ Host.
   - Thêm hàm `TryExtractIntProperty(object propValue, out int result)` hỗ trợ parse an toàn các kiểu `int`, `long`, `SessionProperty`, chuỗi số và tự động clamp về khoảng `[0, 2]`.
2. **Loại bỏ Trailing Whitespace**:
   - Đã sửa dòng 169 `Assets/Khoa/Code/ZombieSpawnZone.cs` loại bỏ ký tự khoảng trắng thừa.
   - Chuẩn hóa phần kết thúc file `CODEX_PROJECT_WORK_LOG.md`.
3. **Thứ Tự Khởi Tạo Loading & SpawnRoutine Gating**:
   - Tách biệt hoàn toàn `IsSessionDifficultyAuthoritativeReady` khỏi `DifficultyRules.HasSessionOverride`. Client chỉ sẵn sàng khi có metadata `SessionInfo.Properties` hợp lệ hoặc khi Networked `SessionDifficultyReady == true`.
   - Local override trên Client không thể làm cờ ready bật sớm.
   - Client trong `SpawnRoutine()` đợi `IsSessionDifficultyAuthoritativeReady == true` trước khi gửi yêu cầu `RPC_RequestSpawn` và trước khi gửi xác nhận `RPC_PlayerFinishedLoadingMap`. Nếu quá 10s không nhận được độ khó từ Host, Client ghi log lỗi rõ ràng và hủy spawn routine (`yield break`), giữ loading gate đóng an toàn.
   - Host gán `SessionDifficulty` và `SessionDifficultyReady = true` trước khi tiến hành spawn, giữ nguyên logic tức thời không độ trễ cho Host/Solo/Tutorial.
4. **Kiểm thử tự động EditMode & PlayMode**:
   - Unit test `HostModeSpawner_TryExtractIntProperty_And_SessionDifficultyReadyGate` kiểm thử toàn diện việc parse an toàn, clamping, và chứng minh local override không kích hoạt readiness trên Client.
   - **EditMode Tests**: `133/133 Passed (100%, 3.88s)`.
   - **PlayMode Tests**: `10/10 Passed (100%, 91.78s)`.

## Entry 2026-08-29 — Cài đặt bộ skill Unity cho Antigravity và Codex

### Yêu cầu

- Người dùng yêu cầu cài bộ `antigravity-awesome-skills` từ repository `benjaminasterA` cho cả Antigravity và Codex để hỗ trợ toàn bộ quy trình phát triển Unity, không chỉ multiplayer.

### Đã triển khai

- Tải nguồn tham khảo tại `C:\Users\triti\Downloads\antigravity-awesome-skills-benjaminasterA-clean`.
- Cài các skill Unity/workflow đã chọn vào Antigravity global hiện tại: `C:\Users\triti\.gemini\config\skills`.
- Cài cùng nhóm skill vào Codex global: `C:\Users\triti\.codex\skills`.
- Nhóm đã cài gồm Unity/game development, planning, debugging, testing, UI validation, localization, production audit và Git workflow.
- Sửa frontmatter bị xuống dòng không hợp lệ trong bản sao active của `unity-developer` và `ui-visual-validator` để Antigravity/Codex có thể phát hiện chúng.
- Bổ sung safety override cho các skill có nguy cơ tự động refactor toàn bộ project hoặc stage/push Git quá rộng; các skill này phải tuân thủ quy tắc project và chỉ thao tác trong phạm vi được duyệt.

### Xác minh

- Antigravity active skill files: `24`; Codex active skill files: `35` (bao gồm skill hệ thống hiện có và các module game lồng nhau).
- Basic frontmatter check: đạt cho toàn bộ skill files active.
- Unity repository `main` vẫn sạch, không bị sửa code/scene/prefab bởi phiên cài đặt này.
- Chưa xác minh bằng giao diện Antigravity vì máy không có lệnh `agy` trong PATH; cần reload/restart Antigravity rồi hỏi danh sách skill để xác nhận discovery.

### Trạng thái

- `Đã triển khai`: cài đặt global cho Antigravity và Codex.
- `Đã kiểm tra file`: đạt.
- `Đã test tay trong Antigravity`: chưa thực hiện.
- Không commit/push thay đổi project trong phiên này.

## Vòng Loot Zombie và Nâng Cấp Balo — 2026-08-29

### Đã triển khai

- Xác chết zombie giờ được State Authority roll loot một lần khi chết, chỉ cho phép một lượt lục thành công, kiểm tra khoảng cách/túi đầy trên Host và gửi thông báo kết quả vào system chat.
- Thông báo nhận loot từ xác zombie và Loot Container dùng màu vàng của system chat; thông báo hiển thị tên vật phẩm và số lượng. Balo hiển thị thêm số ô kho được tăng.
- Loot đạn ngẫu nhiên trong corpse/container bị giới hạn 5–10 viên mỗi stack roll. Các phần thưởng quân sự có chủ đích (ví dụ bundle quest) vẫn giữ số lượng authored riêng.
- Đã thêm công thức xác suất loot và audit 20 lần tìm: Easy có 45%, Normal 30%, Hardcore 12% cho mỗi xác; xác suất không có loot sau 20 lần lần lượt xấp xỉ 0.000642%, 0.079792% và 7.756279%.
- Đã thêm 5 cấp balo runtime ổn định: Level 1–5 có lần lượt 20/25/30/40/50 ô kho; mọi item balo dùng stable ID, được resolve ở Host/client/late join, có thể xuất hiện trong reward văn phòng quân sự và Loot Container theo trọng số 50/30/15/4/1% (sau khi container đã roll được balo).
- Sức chứa giữ nguyên 5 ô hotbar và nâng phần kho từ 15 lên tối đa 50: tổng 20 → 25 → 30 → 35 → 45 → 55 ô. Inventory dùng slot cố định đủ 55 phần tử, UI dựng sẵn 50 ô kho và bật ScrollRect khi nâng cấp.
- Thêm các nút/dev item để kiểm tra từng mức sức chứa và sửa snapshot respawn quân sự để giữ cả item, số ô và cấp balo.

### Xác minh độc lập

- Unity Bee compile sau thay đổi: `ExitCode: 0`, không có lỗi compile mới; log chỉ còn các warning obsolete/pre-existing của A* và field cũ trong `MainMenuManager.cs`.
- EditMode Test Runner: **136/136 passed**, 0 failed.
- PlayMode Test Runner: **10/10 passed**, 0 failed; bao gồm luồng Main Menu → Main, UI 50 ô kho có thể scroll sau khi mô phỏng nâng lên 55 tổng ô, container 20 slot và các regression test Fusion/quest hiện có.
- `git diff --check`: không phát hiện whitespace error. Chưa commit/push/merge vì yêu cầu hiện tại chỉ triển khai và kiểm tra tính năng.

### Bổ sung skill điều phối project-local

- Tạo `.agents/skills/unity-project-workflow/SKILL.md` cho Antigravity và `.codex/skills/unity-project-workflow/SKILL.md` cho Codex.
- Skill bao quát gameplay, C#, scene/prefab, UI, audio, asset, performance, localization, testing, Git và Fusion; đồng thời định tuyến sang các skill chuyên môn đã cài.
- Bắt buộc preflight, plan có giới hạn, bảo toàn serialized reference, network gate khi liên quan, Unity compile/EditMode/PlayMode, bằng chứng runtime và handoff tách rõ trạng thái chưa kiểm chứng.
- Không tự động refactor diện rộng, không stage/push mù và không báo thành công khi chưa có kiểm chứng mới.
- Trạng thái: `Đã triển khai` và đã kiểm tra frontmatter; chưa commit/push.

## Post-check Localization Sức Chứa — 2026-08-29

- Đưa nhãn `CapacityText` của Inventory vào `GameLocalization` để hiển thị đúng English/Vietnamese theo locale hiện tại.
- Sau thay đổi nhỏ này đã compile lại và chạy lại toàn bộ: **EditMode 136/136 passed**, **PlayMode 10/10 passed**, 0 failed.

## Entry 2026-08-29 — Gom tài liệu bàn giao thành một nguồn canonical

### Yêu cầu và quyết định

- Người dùng yêu cầu tiếp tục gom các file work/handoff sau khi phiên trước bị ngắt và cần biết chính xác file phải đọc khi tạo phiên chat mới.
- `Đã duyệt/triển khai`: dùng `CODEX_PROJECT_WORK_LOG.md` làm nguồn bàn giao canonical duy nhất. Ba file Route B/MainPlay/Git integration vẫn được giữ để bảo toàn lịch sử chi tiết, nhưng được dán nhãn hồ sơ lịch sử và không còn là danh sách việc hiện tại.
- `Bị loại`: xóa hoặc ghi đè toàn bộ ba handoff cũ. Lý do: sẽ làm mất quyết định thiết kế, nguyên nhân loại phương án và bằng chứng QA cũ; đồng thời các link lịch sử trong repository sẽ hỏng.
- `Bị loại`: bắt phiên mới luôn đọc cả bốn file. Lý do: trùng lặp lớn, có snapshot cũ và dễ khiến agent lấy state 2026-08-25/26 ghi đè state mới hơn.

### Đã triển khai

- Thêm mục `Bắt đầu một phiên Codex mới` và snapshot 2026-08-29 ở đầu work log.
- Cập nhật `graduation-gameplay-workflow`: chỉ work log là bắt buộc; ba handoff cũ chỉ đọc khi cần lịch sử chuyên sâu.
- Sửa project root cũ trong hai bản `unity-project-workflow` của Codex/Antigravity và thống nhất work log là canonical.
- Dán nhãn historical cho `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md`, `NEXT_SESSION_MAINPLAY_PLAN.md` và `NEXT_SESSION_ROUTE_B_GIT_INTEGRATION_HANDOFF.md`; bổ sung đính chính Route B/MainPlay mới nhất.
- Sửa lỗi cấu trúc nghiêm trọng trong work log: loại một bản lặp của ba entry multiplayer/loading và đoạn Verification bị ghép dở. Bản đầy đủ của từng entry vẫn được giữ nguyên; không xóa lịch sử duy nhất.
- Bổ sung trạng thái private targeted RPC corpse loot: static/codegen và automated tests đã pass; transport/live global state trên nhiều peer vẫn chưa được xác minh đầy đủ.

### Trạng thái Git và kiểm chứng

- Baseline lúc bắt đầu: `main@7bcfb935c`, khớp `origin/main`.
- Giữ nguyên prefab user-owned `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab`; không stage/restore/chỉnh sửa.
- Đây là thay đổi tài liệu/skill, không thay đổi gameplay hoặc network runtime; không cần suy diễn lại kết quả Unity test cũ thành test mới.
- Chưa commit và chưa push trong entry này; yêu cầu gom tài liệu không tự tạo quyền push.

## Entry 2026-08-29 — P0 fresh verification sau pull và checkpoint tài liệu

### Phạm vi

- Người dùng duyệt thực hiện P0: xác minh fresh `main@7bcfb935c` trong chính workspace hiện tại, kiểm tra Scene/luồng Solo và tạo checkpoint riêng cho nhóm tài liệu đã gom.
- Không sửa gameplay, code production, Scene hoặc prefab. Không đưa prefab nhà user-owned vào checkpoint và không push.

### Xác minh fresh trong workspace hiện tại

- Unity instance xác nhận đúng project `E:\\Lap_trinh\\HocTap\\DuAnTotNghiepFIx\\My project`, Unity `6000.0.69f1`; `HEAD` và `origin/main` cùng tại `7bcfb935c` trước khi tạo branch checkpoint.
- Force refresh/compile hoàn tất; Console ngay sau compile: `0 error`, `0 warning`.
- Full EditMode job `319d726da2b9416dbf31df1655b91fb4`: **145/145 passed**, 0 failed, 0 skipped, `4.48s`.
- Full PlayMode job `ff94b3beb1884d49ba5f8366f426ab6a`: **10/10 passed**, 0 failed, 0 skipped, `140.55s`.
- PlayMode có chạy và pass `SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap`: `MainMenu → Fusion Single → Main → spawn Player → loading release`; full Route B production-path cũng pass.
- Unity Scene Validator trên `Assets/Scenes/Main.unity`: **0 missing script, 0 broken prefab, 0 issue**; Scene không dirty sau kiểm tra.
- Các mốc `EndB1`, `EndB2`, `EndB3`, `EndBFinal`, `EndBFinal2`, `EndBToCinemachine`, `Save-Respawn` và `Save-Respawn 2` đều tồn tại trong Scene.

### Phân loại cảnh báo và giới hạn

- Test Runner ghi một log loại `Exception` chỉ để báo đường lưu `TestResults.xml`; không có stack trace và cả 10 PlayMode test vẫn đạt.
- Cảnh báo Voice `TransmitEnabled=false` là trạng thái expected khi chưa bấm Push-to-Talk. Các cảnh báo inventory-full/item-mismatch là nhánh từ chối được test chủ động.
- `ViTriXeChetMay` không tồn tại trong Scene. Code ghi rõ marker này đã mất từ PR #305 và dùng tọa độ fallback được bảo toàn; flow Solo/Route B hiện pass, nhưng việc phục hồi marker author thật nên được theo dõi như một task riêng thay vì âm thầm sửa Scene trong P0.
- MCP WebSocket từng ngắt trong lúc suite chạy; job tiếp tục, kết nối phục hồi và trả kết quả `10/10 passed`. Đây là lỗi cầu nối quan sát, không phải test failure.
- Chưa test tay gameplay hoặc Host/Client thật trong P0; automated pass không được coi là nghiệm thu tay.

### Git

- Tạo branch local `codex/p0-verification-docs-20260829` từ `main@7bcfb935c` để checkpoint bảy file tài liệu/skill đã review.
- `Assets/Khoa/House/cannhatotamhoanchinh_FIXED.prefab` tiếp tục dirty user-owned, không stage/restore/chỉnh sửa; hai trailing-whitespace warning hiện chỉ nằm trong prefab này.
- Chưa push; cần quyền push rõ ràng sau khi người dùng xem kết quả.

## Entry 2026-08-29 — P1/P2 continuation: live two-peer loading và timeout regression

### Đã triển khai

- Chạy Unity Editor gốc làm Host và ParrelSync clone làm Client trong phòng `P1QA3`.
- Tái hiện lỗi thật với cấu hình cũ `ConnectionTimeout=30`: hai Editor cùng tải Scene Main
  khiến Client timeout trước khi gửi readiness; Host dừng ở 1/2.
- Tăng `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` từ 30 lên 120 giây.
  Đây là mitigation cho tải Scene nặng đồng thời; không được hiểu là Scene đã được tối ưu.
- Thêm regression `FusionConnectionTimeout_CoversConcurrentMainSceneLoading` để merge/pull
  sau này không âm thầm đưa timeout về dưới 120 giây.

### Đã xác minh thực tế

- Sau sửa, Host và Client cùng vào Main trong room `P1QA3`; session có 2 Player, khóa đúng
  khi bắt đầu, Host ghi readiness 2/2 và cả hai peer giải phóng loading sang gameplay.
- Cấu hình runtime trên cả hai peer ghi `ConnectionTimeout=120.0`; không tái hiện Fusion
  timeout trong phiên P1QA3 trước khi chủ động dừng hai Editor.
- Fresh batch EditMode: **146/146 passed**, 0 failed, 0 skipped, 3.6665s.
- Fresh batch PlayMode: **10/10 passed**, 0 failed, 0 skipped, 105.8037s.
- Artifacts: `QA_Artifacts/P1P2Continuation_20260829/`.

### Rủi ro và phần chưa xác minh

- Timeout dài hơn làm phát hiện peer chết thật chậm hơn; nguyên nhân hiệu năng nền vẫn là
  Scene Main nặng khi hai Editor cùng deserialize/integrate.
- Chưa có ba peer thật để chốt transport privacy/race của corpse loot. Static/codegen và
  automated contracts vẫn pass nhưng không được suy diễn thành live multi-peer pass.
- Chưa có 5–10 human peers cho readiness/disconnect/death/latency boundary.
- Chưa chạy horde 80–112 zombie bằng Profiler hoặc soak 60 phút; đây vẫn là P2 manual gate.
- NRE Photon Voice chỉ xuất hiện khi hot-reload asset giữa Play; clean Play restart không
  tái hiện nên chưa sửa vendor source. Nếu clean flow tái hiện thì mở defect riêng.

### Git

- Branch local: `codex/p0-verification-docs-20260829`.
- Không push. Prefab nhà user-owned tiếp tục được bảo toàn, không stage/restore/chỉnh sửa.

## Entry 2026-08-30 — Collider vô hình, Player visibility và firearm point-blank

### Quyết định và phạm vi

- Người dùng cho phép assistant tự test các luồng multiplayer cần nhiều peer, tự đánh giá và push Git;
  đồng thời yêu cầu đưa prefab nhà user-owned đang dirty vào cùng push.
- `Đã triển khai`: sửa collider vô hình mẫu, X-Ray local Player, vùng cảm nhận zombie 360 độ có
  fade, tăng damage/độ chính xác AK47 + S12K và fix hụt ở point blank.
- `Chưa triển khai toàn map`: shadow blocker đèn pin. Nguyên nhân đã xác nhận là scene/prefab không
  có `ShadowCaster2D` dù `Light2D.shadowsEnabled=true`; layer `Obstacle` lại gộp cả tường và hàng rào.
  Auto-convert toàn layer sẽ làm hàng rào thấp chặn sáng sai thiết kế. Task được tách thành vertical
  slice một căn nhà + một hàng rào trong `NEXT_SESSION_VISIBILITY_COMBAT_PLAN.md`.

### Collider vô hình

- Marker user-authored `Collider_A*TangHinh` tại `(-0.604, -2.844)` bị chồng bởi
  `==========Wall===========/HangRao (3)`: `PolygonCollider2D` solid layer `Obstacle`, không có
  renderer. Node A* tại marker trước sửa là unwalkable.
- Xóa đúng `HangRao (3)` và giữ marker làm regression. Ba collider-only `HangRao` còn lại được giữ
  vì Scene view xác nhận chúng khớp hàng rào/biên map có hình thật.
- Chạy `Tools/Environment/Scan A* Current Scene` sau sửa: hoàn tất **485.805 node / 1 graph**.

### Visibility và combat

- Player X-Ray dùng silhouette SpriteRenderer unlit local-only trên sorting layer trước nhất; không
  thêm replicated state.
- Vùng cảm nhận zombie chuẩn hóa 2m cho Player/Player2, hoạt động 360 độ cả trong/ngoài nhà và fade
  0,25 giây. Alpha chỉ được xử lý local theo camera target; RGB của hit-flash được bảo toàn.
- AK47: damage 34 → 42, spread asset 2° → 0,75°. S12K: damage/pellet 15 → 24, spread 12° → 8°,
  giữ 9 pellet. State Authority thêm point-blank target assist 1,2m trong aim cone và CircleCast
  radius 0,08m; client không tự áp damage.
- Thêm regression cho marker collider, cấu hình vision hai prefab và balance floor của hai súng.

### Xác minh fresh

- Unity script validation: `PlayerVision.cs` và `PlayerCombat.cs` không có compile error.
- Unity Console sau refresh: 0 error.
- EditMode job `6bd95fe56df74182ad6a7af7a29f8dc3`: **150/150 passed**, 0 failed,
  0 skipped, 4,2848s.
- PlayMode job `f8149af0c62947fda22da4b9489049ac`: **10/10 passed**, 0 failed,
  0 skipped, 106,2297s.
- Live Host+Client `P1QA3` ở entry trước vẫn là bằng chứng fresh cho timeout/readiness 2/2.
  Một vòng two-peer mới đã khởi chạy nhưng ParrelSync Client đóng khi phiên Codex bị ngắt, nên không
  được báo là pass cho X-Ray/fade/combat mới.

### Rủi ro và QA còn lại

- X-Ray đang là silhouette luôn-hiện nhẹ, không phải occluded-pixels-only stencil; cần user đánh giá
  thẩm mỹ trong gameplay.
- Chưa nghiệm thu cảm giác fade, bán kính 2m và balance súng bằng test tay của người dùng.
- Cần chạy lại Host+Client sau pull để xác nhận mỗi peer có visibility riêng và Host chỉ áp damage
  một lần. Checklist/expected result nằm trong `NEXT_SESSION_VISIBILITY_COMBAT_PLAN.md`.
- ShadowCaster2D cho tường/cửa chưa được nhân rộng; không tuyên bố đã fix đèn pin xuyên tường.

## Entry 2026-08-30 — Indoor occlusion, starter loadout retry và Zombie wall sweep

### Phạm vi và quyết định

- Người dùng đã test nhanh bản visibility/combat trước và xác nhận: Fog + đèn pin còn xuyên tường
  trong nhà, một lần Solo không nhận vũ khí đầu game, vùng fade cần giảm còn 1,5m và Zombie đôi lúc
  chìm/tàng hình/xuyên tường trong nhà.
- `Đã chọn`: occlusion local bằng fan raycast chỉ nhận collider thuộc đúng căn nhà đang chứa Player.
  Fog shader giữ cover alpha 0,98 sau structural hit, vì vậy che luôn Light2D rò qua tường nhưng khe
  cửa không collider vẫn mở; hàng rào ngoài map dù gần hơn vẫn bị bỏ qua.
- `Đã loại`: gắn ShadowCaster2D/tái phân layer toàn map trong lượt này. Project chưa author shadow
  geometry và `Obstacle` trộn tường với hàng rào; auto-convert sẽ tạo nhiều blocker sai và khó review.
- `Đã chọn`: giữ Zombie Kinematic/Fusion, thêm Rigidbody2D body cast trước MovePosition.
- `Đã loại`: đổi Zombie sang Dynamic. Rủi ro là Zombie đẩy Player, rung contact và tạo sai lệch
  host/client; body cast giải quyết tunnelling/cắt góc mà không đổi mô hình authority.

### Đã triển khai

- `FogVisionController` dựng 180 tia ở 15Hz khi indoor, cache structure root từ `RoofVisibility` hoặc
  `IndoorVisionArea`, và chỉ dùng hit `Obstacle` là descendant của root đó. Shader nội suy khoảng
  cách theo góc và không mở Fog/flashlight/exit awareness sau tường.
- `PlayerVision` và cả hai prefab Player giảm passive awareness từ 2m xuống 1,5m. Vùng 360 độ vẫn
  thấy Zombie sau lưng nhưng nay dùng cùng LOS check nên không xuyên tường kín.
- `InventorySystem` thêm trạng thái `StartingLoadoutResolved`; hotbar/flashlight placement trả bool,
  State Authority retry mỗi 0,5 giây đến khi toàn bộ item được xác nhận và retry chỉ thêm lượng thiếu.
  Military snapshot được đánh dấu resolved trước restore. Luật canonical không đổi: Easy có AK47,
  Normal không có súng, Hard không có starter gear.
- `ZOmbieAI_Khoa` và `ZombieAIKhoaRebuilt` cast toàn bộ Rigidbody2D theo obstacle mask trước mỗi bước,
  cắt quãng đường ở wall skin 0,02m và dùng quãng đường thực cho NetSpeed/stuck-repath.
- Cập nhật `NEXT_SESSION_VISIBILITY_COMBAT_PLAN.md`; thêm Editor contract và PlayMode physics tests.

### Xác minh fresh

- Unity 6000.0.69f1 force refresh/compile: 0 compile error; shader import không sinh error và runtime
  regression xác nhận `ProjectZomboid/FogVisionOverlay.isSupported=true`.
- A* Main scan bằng tool project: **485.805 node / 1 graph**. Spawn zone vẫn chỉ spawn ở node Walkable;
  prefab Zombie ở layer `Enemy`, obstacle mask `Obstacle`, collision matrix Enemy–Obstacle không ignore.
- Targeted EditMode: **5/5 passed** cho 1,5m, indoor building scope, wall sweep và loadout retry contract.
- Targeted PlayMode: **2/2 passed**: tia bỏ qua hàng rào ngoài root và dừng ở tường nhà; cả hai Zombie
  brain dừng trước static wall. Test placement AK47 idempotent bổ sung: **1/1 passed**.
- Full EditMode job `7990782b0ca64bc9a04f6c818bc7ca37`: **153/153 passed**, 0 failed.
- Full PlayMode job `da152253e299455cacf69657137d4923`: **12/12 passed**, 0 failed, 92,136s.
- A* scan làm Unity bận và MCP WebSocket ngắt một lần khi đổi scene; Editor hồi phục, compile/test sau
  đó pass đầy đủ. Đây là lỗi cầu nối quan sát, không được tính là gameplay pass/fail.

### Chưa xác minh và Git

- Chưa có user visual QA mới tại đúng căn nhà trong ảnh sau bản sửa này; automated ray test xác nhận
  geometry/semantic nhưng không thay thế đánh giá độ tối, feather và cảm giác đèn pin thực tế.
- Chưa soak horde qua nhiều căn nhà hoặc live Host+Client cho visibility client-local; đây vẫn là P2.
- Branch: `codex/indoor-vision-zombie-reliability-20260830`, tạo từ `main@d433d58e2`.

## Entry 2026-08-31 — Phục hồi PlayerVision/Fog của task QA cũ

### Yêu cầu và căn cứ

- Người dùng yêu cầu bỏ cơ chế vision/FOW mới của Tín vì mất Fog cũ, fan che tường bị áp ra ngoài map gây mảng đen lớn. Đã cho phép điều khiển Unity để kiểm tra kỹ, không cho phép push.
- Task đúng đã đọc: `01a05047-5995-79b1-984a-f38ca91e1466`, **QA Fog, starter loadout và zombie indoor**. Không dùng task nhầm `01a050a2-ea70-7bb3-953e-f21ba5c9905a` làm nguồn phục hồi.
- Trước sửa: `main == origin/main == a23c33247323ed2d7670869c11298bc67aa0832e`, working tree sạch.
- Nguồn cũ: `7987af306` (checkpoint gameplay `01455503e`) cộng patch sửa mái trường chưa commit ở cuối task cũ. Không bỏ sót patch chỉ vì nó chưa nằm trong Git commit.

### Backup, phạm vi và quyết định

- Tạo nhánh an toàn `codex/backup-vision-before-restore-20260831` tại HEAD ban đầu; giữ nguyên main và lịch sử của Tín.
- Làm việc trên `codex/restore-indoor-vision-20260831`, HEAD vẫn `a23c33247`, thay đổi chưa commit.
- ZIP nguồn trước sửa, nguồn cũ, patch lịch sử và snapshot ba file đã phục hồi nằm trong `QA_Artifacts/VisionRollback_20260831_091549/`; hash, chi tiết và cách test ở `FINAL_REPORT.md` trong thư mục đó.
- Chọn phục hồi có giới hạn cho vision. Loại phương án reset toàn repository/cherry-pick cả nhánh vì sẽ kéo theo inventory, loading và các thay đổi khác ngoài yêu cầu.

### Đã sửa và cố ý giữ nguyên

- `Assets/Khoa/Code/FogVisionController.cs` và `Assets/Shader/FogVisionOverlay.shader` khớp nội dung Git tại `7987af306`.
- `Assets/Khoa/Code/PlayerVision.cs` khớp đúng bản cuối task cũ gồm fallback mái trường nhiều polygon; Git blob `0f9415ea35d81e5498b4f2f82e54555613f2ad0d`.
- Khôi phục Fog/thời tiết, cone, fade và đèn pin cũ; fan occlusion theo tường chỉ chạy khi có indoor trigger. Giữ fallback hình học cho tường bệnh viện/trường nằm ngoài hierarchy mái.
- Loại `VisionLineOfSight.cs`, `FowLosArtifactRunner.cs` cùng meta thuộc triển khai mới đã bỏ; không còn code reference hoặc GUID reference trong scene/prefab/asset.
- Khôi phục tests indoor/mái trường; bỏ tests riêng của thiết kế FOW ngoài trời đã bị yêu cầu hoàn tác. Giữ test AK47/S12K placement hiện tại và các tests chat/readiness khác.
- Bổ sung runtime regression đi qua sáu vị trí thực, kiểm tra indoor mask và bật/tắt đèn pin; UnityTearDown unload map để không nhiễu test hình học riêng.
- Không sửa scene, prefab, Inventory/DifficultyRules, loading/readiness, chat, combat, AI, quest hay multiplayer. Hai prefab Player cũng không khác checkpoint nguồn.
- Network: phục hồi presentation local theo camera/Player hiện hữu, không thêm State Authority/RPC/replicated state và không thay đổi spawn, damage hay inventory authority.

### Xác minh mới

- Unity 6000.0.69f1 compile thành công, không ghi nhận C#/shader compile error. Runtime xác nhận shader supported.
- Full EditMode: **173/173 passed**, 0 failed/skip, 2.8449938s; job `10a89167a5254d1fba4ce7ed46e8dd8f`.
- Full PlayMode: **15/15 passed**, 0 failed/skip, 106.6487751s; job `daef476b9fb9434d804963f92c56e6eb`.
- Có lượt runtime Solo Easy và Medium. Medium dùng đèn pin starter thật qua đường equip/toggle production. Sau menu Button events, fixture dùng teleport Fusion production để đặt Player chính xác, không giả là đã đi bộ hoàn thành quest.
- Sáu điểm: ngoài trời `(-62.29,30.77)` → bệnh viện lớn `(-46.646,16.413)` → ngoài trời → bệnh viện nhỏ `(-49.584,37.427)` → trường `(11.36,49.93)` → ngoài trường `(11.36,37.50)`. Indoor mask lần lượt **0/1/0/1/1/0**; cả sáu điểm đèn **off/on/off** đúng, phạm vi mask không đổi.
- Đã xem ảnh cover trong nhà và ngoài trời; không còn fan đen lớn ở các điểm outdoor lấy mẫu. 12 ảnh cùng log tọa độ/mask: `QA_Artifacts/VisionRollback_Runtime/`. XML và kết quả tool: thư mục backup nêu trên.
- Các fail trung gian được phân loại: quest đưa Player lại trường khi chưa đủ manh mối; reflection sai assembly/property của clue mask; ca map thật chưa unload collider làm hai test dựng riêng fail. Đã sửa fixture/teardown, không sửa production để né assertions. Nhóm vision sau teardown **5/5**, full suite sau đó **15/15**.
- Kiểm tra ảnh cuối phát hiện full suite còn VictorySummaryUI từ test quest che gameplay; lượt standalone ngay sau đó cũng còn nền menu. Không dùng ảnh bị che làm bằng chứng. Force compile và xác nhận domain reload xong, chạy riêng ca vision **1/1 passed**, 22.5454773s, job `98b547a20c5a4c5a8d56104f426a7ff7`; đã mở xem ảnh gameplay mới, lưu bản cố định tại `QA_Artifacts/VisionRollback_20260831_091549/verified-runtime/`. Full suite có assertions state/physics, không phải pixel assertions cho menu/quest overlay.
- `git diff --check` pass; xác nhận không có diff production ngoài vision. Unity kết thúc Play và trở về MainMenu.

### Test thực tế, rủi ro và bàn giao

- Test tay: MainMenu → Solo → Medium → trang bị đèn từ túi lên hotbar, chọn slot và click trái. Quan sát ngoài đường/sát hàng rào khi đổi hướng; vào bệnh viện và trường, bật/tắt đèn; ra ngoài sau khi đủ điều kiện quest. Mong đợi tường kín trong nhà che phía sau, cửa mở còn nhìn được, không kéo indoor mask ra ngoài map, zombie gần trong mái trường không tàng hình vì polygon chia mảnh.
- Đã thử thao tác desktop Solo Medium nhưng nền menu còn che map sau tải; Console có `ShowLoadingScreen ignored: Scene is already loaded or gameplay is released`. Một lượt sau reload cũng chưa vào được gameplay qua click. **Không báo manual flow này pass**. Domain Reload bị tắt là khả năng gây trạng thái tĩnh sót, chưa kết luận nguyên nhân; không sửa loading trong task vision.
- Trước sửa đã có loading timeout 36.4s. Sau test còn hai thông báo EventSystem trùng và log `Saving results to...` do runner phân loại Exception. Không coi Console hoàn toàn sạch hoặc đã sửa các lỗi này.
- Chưa QA Host+Client, build player, soak horde toàn map, nhiều góc cửa/giờ ngày đêm hoặc nghiệm thu cảm giác Fog của người dùng. Solo/test tự động không thay thế các mục đó.
- Git: nhánh `codex/restore-indoor-vision-20260831`, HEAD `a23c33247`; thay đổi local chưa stage/commit, không push/merge. Main/backup vẫn giữ nguyên HEAD trước sửa. Không có yêu cầu push trong lượt này.

## Entry 2026-08-31 — Hotfix Play lại bị kẹt nền MainMenu

- **Đã duyệt:** user chưa test Fog được, yêu cầu sửa nhanh lỗi Main đã tải/nhạc và HUD đã chạy nhưng nền menu còn che; kèm ảnh Hotbar sinh lại lúc đóng scene. Xác nhận đã nắm implementation Fog cũ; chưa tự nhận biết mọi bug chưa được mô tả.
- **Nguyên nhân:** coordinator static ReleasedToGameplay sót khi Domain Reload tắt; guard ShowLoadingScreen return trước khi coroutine ẩn menu được chạy. New runner attempt chưa reset trạng thái lượt trước. Menu OnDestroy còn bật lại UI, trong khi Hotbar getter có thể tạo object ngay cả ngoài Play/đang shutdown.
- **Đã triển khai:** GameplayReadinessCoordinator reset state/events/Canvas registry ở SubsystemRegistration; MainMenuManager reset loading attempt sau cleanup và trước StartGame, giữ nguyên guard cho callback trễ cùng trận; OnDestroy không activate HUD. HotbarHUDManager chặn tạo instance ngoài Play/quitting và reset cờ mỗi runtime; AutoUIManager khởi tạo Hotbar bằng null-safe access.
- Không đổi scene/prefab/ProjectSettings, readiness release criteria, Authority/RPC, inventory hay Fog. Không chọn bật Domain Reload hoặc bỏ guard Client để chữa triệu chứng. PlayerVision vẫn có blob `0f9415ea35d81e5498b4f2f82e54555613f2ad0d`; Fog controller/shader vẫn khớp nguồn đã phục hồi.
- **Xác minh:** compile không lỗi, diff check pass. Integration `MenuReplayLifecycleEditorTests` **1/1 pass** (job `5ee7bb969f064115b86973ccafcb234f`, 25.8672943s): ba EnterPlayMode/ExitPlayMode liên tiếp không Domain Reload; mỗi vòng vào Solo Medium, assert Player/readiness, menu Canvas inactive, sau Exit không còn Hotbar. Đã mở xem cả ba ảnh map thật. `ReadinessAndChatEditorTests` **43/43 pass** (job `c076cdddbcbc4b67bec057f2a0f4655d`), giữ các gate Client/late join/Failed/local-ready.
- **Thao tác trực tiếp:** click Solo Medium vào map thành công lượt đầu; Stop không error Hotbar sót. Lượt click tiếp theo công cụ gặp focus (EventSystem isFocused=false); không báo ba lượt test tay pass. Ba vòng xác minh là automation qua Button events trong Play thật. Không còn gameplay Error sau ca vòng lặp; vẫn có log TestRunner Saving results được tool phân loại Exception.
- **Test tay đề nghị:** MainMenu → Play → Solo → chọn độ khó → vào Main; nền menu phải biến mất và map/Player/HUD hiện. Stop rồi Play lại ít nhất hai lần, không compile/restart Unity; không còn Hotbar ngoài Play. User chưa nghiệm thu.
- **Giới hạn:** chưa Host+Client, build hoặc full suite sau hotfix; không dùng số 173/15 của lượt phục hồi trước làm bằng chứng mới cho hotfix.
- Báo cáo, backup bốn file trước sửa, XML và ảnh: `QA_Artifacts/MenuReplayFix_20260831/`. Git vẫn `codex/restore-indoor-vision-20260831@a23c33247`, toàn bộ thay đổi local chưa commit/push/merge.

## Entry 2026-08-31 — User xác nhận checkpoint, thảo luận chi tiết Fog indoor

- **Đã test tay/xác nhận:** user nói "được rồi", yêu cầu xem trạng thái sau phục hồi vision và hotfix menu là checkpoint an toàn để quay về nếu các cải tiến tiếp theo sai.
- Lưu checkpoint Git local trên nhánh `codex/checkpoint-vision-menu-stable-20260831`; không push/merge. Các ZIP và ảnh QA trước đó vẫn giữ local trong QA_Artifacts.
- **Chỉ thảo luận, chưa duyệt triển khai:** ảnh indoor có vùng sàn sáng nhưng phần trên của tường/decor vẫn bị Fog che. User muốn biết giải pháp và mức phức tạp; nếu đòi tái cấu trúc lớn thì cân nhắc bỏ tính năng. Không sửa gameplay/shader/scene trong lượt này.
- Đọc code xác nhận fan raycast 2D đo tới collider Obstacle của nhà; full-screen Fog shader lấy XY từ vị trí pixel, chưa có thông tin pixel thuộc mặt tường/decor nào hoặc chân/chiều cao của đối tượng. Đây là nguyên nhân có cơ sở cho sai lệch giữa visibility trên sàn và sprite cao; chưa xác minh từng collider/material của vật trong ảnh.
- Hướng đang cân nhắc: giữ logic chặn sau tường, bổ sung cách hiển thị mặt tường/decor đang nhìn thấy dựa trên vị trí chân/đoạn tường. Không chọn nới mask toàn cục vì có nguy cơ lộ sàn/đồ/zombie phòng sau; chưa hứa phạm vi nhỏ cho toàn map khi chưa kiểm kê cách ghép tilemap và sprite.
- Cần xác nhận: mặt tường/decor nào được sáng và chỉ phần nằm trong vùng nhìn hay cả vật; áp dụng với/không đèn pin; nếu bỏ thì bỏ riêng che theo tường hay toàn bộ Fog indoor. Chưa coi các lựa chọn này là yêu cầu đã chốt.

### Bổ sung thảo luận — yêu cầu mặt tường/decor và giới hạn chi phí

- User xác nhận mặt tường và tủ phía trước Player (khoanh đỏ trong ảnh) phải hiện sáng theo vùng nhìn; chuyển sáng–tối giảm nhẹ tự nhiên; không có đèn pin vẫn hiện mờ, yếu hơn nhiều và chịu ảnh hưởng ánh sáng môi trường/ngày đêm. Phạm vi vẫn chỉ cải tiến trong nhà. Chưa xác nhận quy tắc nhớ vùng đã khám phá hoặc việc sáng cả vật khi chỉ một phần nằm trong vùng nhìn.
- User lo logic khó mở rộng, hiệu năng/sự mượt mà khi Editor hiện khó giữ 60 FPS và lỗi phát sinh. Tiếp tục **chỉ thảo luận**, chưa có phép triển khai thử nghiệm hay thay đổi gameplay/shader/scene.
- Đề xuất để thảo luận: giữ một nguồn kiểm tra vật cản; tách quyền nhìn thấy bề mặt khỏi độ sáng; cần dữ liệu liên hệ mặt tường/sprite cao với vị trí trong nhà thay vì chỉ dùng XY của pixel màn hình. Không nới mask toàn cục hoặc làm sáng cả Tilemap; chuyển mềm phải tránh lộ nội dung phía sau vật cản. Hiện chưa kiểm kê đủ asset để kết luận cách bổ sung dữ liệu này nhẹ cho toàn map.
- Hướng kiểm chứng nếu sau này được duyệt: một nhà mẫu, dùng quy tắc/cấu hình tái sử dụng, đo CPU/GPU trước–sau cùng điều kiện, kiểm tra góc tường/cửa/chuyển hướng/đèn pin/ngày đêm; dừng nếu phải vá riêng nhiều nhà, thay renderer lớn hoặc chi phí/lỗi vượt mức chấp nhận. Không cam kết FPS; hiệu ứng nhìn của từng người nên tính local, không đồng bộ mask từng khung hình qua mạng. Đây là đề xuất, chưa triển khai.
- Đối chiếu nguồn chính thức PZ: bài https://projectzomboid.com/blog/news/2023/02/play-your-cardz-right/ mô tả môi trường tile 2D giả isometric kết hợp đối tượng 3D và dữ liệu depth; không lấy giả định PZ là môi trường hoàn toàn 3D làm cơ sở thiết kế. Không suy diễn đây là toàn bộ thuật toán Fog của PZ.
- Checkpoint local đã lưu tại `ddf440424` / `codex/checkpoint-vision-menu-stable-20260831`, không đổi và không push. Lượt thảo luận này chỉ bổ sung work log, không chạy QA gameplay mới.

## Entry 2026-08-31 — Tạo ảnh Fog indoor trước khi duyệt prototype

- **Yêu cầu mới / đã duyệt:** user yêu cầu lưu đầy đủ trao đổi để đổi phiên khi context dài; tạo hình ảnh mô phỏng tầm nhìn, độ sáng đồ vật, Fog trên tường/decor trước. Chỉ được bắt đầu code sau khi user thấy ảnh ổn và duyệt. Sau triển khai phải đối chiếu, tự đánh giá; nếu vài lần không tái hiện được hoặc logic quá phức tạp thì cân nhắc dừng. Không coi câu "có thể bắt đầu làm thử" là bỏ điều kiện duyệt ảnh.
- **Đã tạo, chờ duyệt:** hai ảnh bằng built-in image_gen: đèn pin ON và OFF, dựa trên ảnh nhà gốc và ảnh khoanh đỏ. Lưu bản sao cả ảnh tham chiếu, ảnh kết quả và prompt tại `QA_Artifacts/IndoorFogConcept_20260831/`; hướng dẫn tiếp nối và tự đánh giá trong `README.md`, prompt nguyên văn trong `PROMPTS.md`.
- Đã xem cả hai ảnh: mặt tường/tranh và tủ phía trước hiện rõ hơn; vùng sáng trên mặt tủ chuyển mềm, OFF mờ hơn ON. ON hiện vùng sáng khá rộng nên cần user duyệt. AI có thể lệch chi tiết sprite/HUD/kích thước; ảnh dùng làm đích cảm giác và quy tắc hiển thị, không thay asset hoặc chuẩn so từng pixel. FPS trong ảnh không phải phép đo runtime.
- **Chưa triển khai / chưa QA Unity mới:** không sửa gameplay, shader, scene, prefab hoặc network. Chưa có phê duyệt ảnh. Chưa có bằng chứng hiệu năng hoặc che khuất khi chuyển động từ concept tĩnh.
- Sau duyệt: một nhà thử, chụp cùng điều kiện so ON/OFF, kiểm tra góc/cửa/quay/vào-ra nhà/ngày đêm và đo CPU/GPU trước–sau; ghi sai lệch qua từng vòng. Đề xuất tối đa ba vòng thử rồi đánh giá dừng nếu không tiến triển hoặc cần tái cấu trúc lớn; số vòng này chưa được user xác nhận.
- Checkpoint `ddf440424` nguyên vẹn; nhánh làm việc `codex/restore-indoor-vision-20260831`. Lượt này chỉ thêm tài liệu/ảnh concept local, không commit/push/merge. Phiên sau đọc entry này và README, chờ hoặc xác minh phản hồi duyệt ảnh mới nhất trước khi code.

## Entry 2026-08-31 — User duyệt concept, bắt đầu thử Fog bề mặt

- **Đã duyệt:** user nói cả hai ảnh rất ổn và cho bắt đầu implementation; không cần vẽ lại. Chỉnh ảnh ON: vùng tường bên phải khoanh đỏ phải tối sớm hơn một chút vì ánh sáng đang lan hơi quá. Ảnh OFF là mức sáng buổi tối; ban ngày sáng nhẹ hơn nhờ môi trường. Ảnh phản hồi lưu `QA_Artifacts/IndoorFogConcept_20260831/approved-v1-narrower-edge.png`.
- Phạm vi đã nhận: thử một nhà trước, giữ checkpoint `ddf440424`, đối chiếu runtime và đo hiệu năng trước–sau. Không tự mở rộng toàn map/tái cấu trúc lớn. Không cần hỏi lại các yêu cầu hình ảnh đã rõ. Chưa có nghiệm thu gameplay mới hoặc quyền push mới.
- Đang kiểm kê dữ liệu nhà mẫu và tạo công cụ QA chỉ chạy Editor. MCP execute_code không dùng được (CodeDom báo lỗi BOM, Roslyn chưa cài); dùng editor helper qua menu để thu bằng chứng, không sửa package/plugin nhằm né lỗi công cụ.

### Kết quả prototype Fog bề mặt — bàn giao cùng ngày

- **Đã triển khai thử, chưa test tay/nghiệm thu:** component `IndoorFogSurfaceMap` opt-in, atlas alpha/footprint cache từ sprite physics shape, shader `Hidden/IndoorFogSurfaceAtlas`; controller/Fog shader đọc projection chỉ cho active collider đúng cấu hình. Không đổi `PlayerVision`, scene/prefab, collider, quest, menu, inventory hoặc Fusion state/RPC. Không gắn mặc định trong Main; QA chỉ thêm component runtime tại `nhachinhxaydautien (12)`.
- Nhà mẫu cùng kiểu ảnh: root `==========Map========== /Map/nhachinhxaydautien (12)`, Player `(-39.2,44.3)`, nhìn `(0,1)`, zoom 3, camera offset QA y=1.2. Hai Tilemap Individual/Gameplay `tuongnha (1)` và `Trangtri`, sorting Y, 67 sprite surface. Physics shape chân tường đã có; không tạo height/depth data cho toàn map.
- Ba lượt hiệu chỉnh hình ảnh: (1) hiện được tường/tủ nhưng xuất hiện vệt tối phía trước; (2) giữ pixel đã visible theo mask cũ để tránh vệt mới; (3) cân lại nền ngày/đêm và cone thu nhẹ. DayAmbientOpacity=0.86/night=0.15/lit=0.08/coneInset=0.06. Đây là opacity trên Global Light đã có, không phải đèn ngày yếu hơn đèn đêm. Có giới hạn xấp xỉ cần giữ, không coi đây là visibility 3D hoàn chỉnh.
- **Tự đánh giá:** tường/tranh/thân tủ hiện đúng hơn, vùng bên phải tối sớm hơn concept đầu. Ranh ở một số góc khuất còn cứng, mức tối buổi tối cần user xem thật; chưa khẳng định khớp hoàn toàn mẫu. Giữ bản thử để đánh giá, không mở rộng mặc định hoặc tái cấu trúc lớn. Atlas tĩnh chưa xử lý invalidation cho đồ/cửa thay đổi; dynamic objects và multiplayer visual overlap chưa QA đầy đủ.
- **Xác minh:** compile/Console không lỗi cuối. Integration real Solo với 11 trạng thái (ON/OFF/day/night/left/right/outdoor/other-house/return/disable), GPU atlas có vùng surface và vùng trong suốt, cache giữ nguyên qua thay đổi, release khi disable/ExitPlay. Lượt đầu phát hiện shader thiếu texture property khi truy vấn material; đã sửa property, không bỏ qua log lỗi.
- EditMode cuối **44/44 pass**, job `c94909a3845941ada8511fc15c5c3b51`, 29.053822s (43 readiness/chat + 1 integration). PlayMode visibility/zombie **5/5 pass**, job `81f024de3c7249bda21a3f067bce126a`, 21.9985892s. Không gọi là full suite; PlayMode chạy trước chỉnh cuối opacity ngày và guard/default QA DTO. Sau chỉnh DTO đã compile, mở Solo và dùng QA helper đo cuối. Chưa Host+Client/late join/reconnect/build hoặc soak.
- **Hiệu năng:** A/B/A cùng Solo/vị trí/hướng/13:30/đèn bật, Game View 987×568, bỏ 30 frame đầu rồi ghi 240 frame/lượt. Median GPU A=3.380224/B=3.456/A2=3.227648 ms; CPU main A=17.6134/B=16.3789/A2=19.0526 ms; Fog marker A=.0528/B=.0568/A2=.0680 ms. GPU tăng khoảng .08–.23ms, CPU nền dao động mạnh nên không kết luận cải thiện CPU hay hứa 60FPS. Atlas build một lần khoảng 2.17–10.43ms trong các lượt, không nằm trong steady-state benchmark. Chưa đo bản build hoặc độ phân giải lớn.
- **Bằng chứng/cách test:** `QA_Artifacts/IndoorFogPrototype_20260831/README.md`, ảnh `v3-*.png` + state, test JSON, CSV và `final-profile-summary.json`. Concept/ảnh đánh dấu duyệt đã giữ ngoài Temp. Bằng chứng là screenshot Play thật, không AI. Đọc README để dùng menu `Tools > QA > Indoor Fog`: Start Solo Automation, Apply Runtime Pose, Return Manual Control; pose.json cấu hình nhà mẫu. Component bị bỏ khi Stop, phải apply lại nếu Play lại.
- **Git/an toàn:** HEAD vẫn `ddf440424`, nhánh `codex/restore-indoor-vision-20260831`; checkpoint branch nguyên. Hai file runtime tracked thay đổi, bốn file mới component/shader/QA helper/test cùng meta, work log và QA artifacts local; không commit/push/merge. Không có diff scene/prefab/animation thực tế. Không làm mất thay đổi Tín hoặc user.
- **Việc tiếp:** user test cảm giác trên nhà mẫu. Nếu ranh góc/đồ động cần thêm nhiều ngoại lệ hoặc đổi renderer lớn thì cân nhắc dừng và phục hồi có kiểm soát về checkpoint. Không tự coi test tự động hoặc việc duyệt concept là nghiệm thu implementation này.
- **Trạng thái Unity khi bàn giao:** đang Play Solo ở nhà mẫu, prototype bật, 13:30 lúc đặt pose; đã gọi Return Manual Control và xác nhận log trả điều khiển. Movement, tốc độ clock và camera offset trả về giá trị trước fixture. Ảnh trước trả điều khiển: `manual-ready.png` + state. Chưa có input/test tay của user sau bàn giao. Stop Play sẽ gỡ component thử, không lưu vào Main.

### Cập nhật sau khi phiên Codex bị ngắt context

- Đã kiểm tra lại thay vì dựa vào log cũ: Unity lúc nối lại đang Stop ở `MainMenu`, Console không có compile/runtime error. Sau đó tạo **một phiên Play sạch mới**, chạy `Start Solo Automation`, nhận log `Solo ready`, áp `pose.json` hai lần cách nhau 0.8 giây để hoàn tất equip/toggle đèn pin thật.
- Trạng thái runtime mới đã chụp tại `manual-ready.png`/`manual-ready-state.txt`: Player `(-39.20,44.30)`, indoor đúng `nhachinhxaydautien (12)/nocnha (1)`, `mask=1`, `surface=1`, `flashlight=1`, 67 surface, atlas build 2.5469 ms. Console 0 error.
- Đã gọi `Return Manual Control` và nhận log xác nhận: `Manual controls restored; prototype remains in this runtime house only.` Unity vẫn đang Play scene `Main` để user test tay ngay; prototype chỉ tồn tại runtime và sẽ tự mất khi Stop. Không sửa/lưu scene hoặc prefab trong bước nối lại này.

## Entry 2026-08-31 — User review V1 và bàn giao Indoor Fog V2 sang task mới

- **Chỉ thảo luận/tài liệu, không sửa code:** user test tay V1 và đánh giá “khá ổn” cho bản thử đầu, đồng thời muốn tiếp tục hoàn thiện. Hai ảnh phản hồi được sao lưu tại `QA_Artifacts/IndoorFogPrototype_20260831/V1_UserReview_20260831/`.
- **Đã test tay/phát hiện bởi user:** (1) vùng giao sáng → tối còn cứng, chưa mềm như concept; (2) tiến gần một số tường xuất hiện vệt đen và di chuyển gần tường có thể nhấp nháy liên tục. Đây là lỗi còn mở; V1 không được xem là stable/accepted.
- Ảnh tĩnh xác nhận hình dạng vệt và biên cứng, nhưng không đủ để kết luận nguồn nhấp nháy. Các khả năng như ray fan 15 Hz, collider hit đổi cạnh, atlas foot projection/point sampling, nhánh `max` giữa visibility gốc và projected, hoặc camera sub-pixel chỉ là giả thuyết cần instrument/reproduce.
- **Đã duyệt hướng tiếp:** tiếp tục tính năng, xử lý ổn định tín hiệu/vệt nhấp nháy trước rồi mới feather biên; sau mỗi bước phải chụp Game View thật cùng pose, đối chiếu concept và ảnh lỗi, tự đánh giá mức hoàn thiện. Giữ một nhà mẫu, không mở rộng toàn map hay tái cấu trúc lớn khi chưa chứng minh cần thiết.
- Đã tạo `NEXT_SESSION_HANDOFF.md` và `NEXT_SESSION_PROMPT.md` trong thư mục QA prototype, chứa yêu cầu, thứ tự điều tra, quality/performance gates, Git safety và prompt tự đủ để task mới tiếp tục. Checkpoint gốc vẫn `ddf440424`; không commit/push/merge.

### Bổ sung V2 milestone gate

- User đã cho phép bắt đầu V2 ngay. V2 chỉ gồm hai mục tiêu đã chốt tại nhà mẫu: loại vệt đen/nhấp nháy gần tường và làm mềm biên sáng–tối, đồng thời không leak qua tường hoặc regression rõ.
- Khi đủ compile/Console, test, chuỗi frame/chuyển động, ảnh fixed-pose và performance thì phải dừng ở trạng thái Play cho user test tay; không tự chuyển V3. Ý tưởng ngoài acceptance ghi `Đề xuất V3`.
- Nếu tối đa 2–3 vòng sửa có bằng chứng vẫn còn lỗi đáng kể hoặc cần ngoại lệ theo nhà/tái cấu trúc lớn, dừng tại checkpoint test được và hỏi user. Chủ động handoff theo ranh giới phase khi context dài; không giả vờ biết chính xác phần trăm context.

## Entry 2026-08-31 — Indoor Fog Surface V2 candidate, chờ user test tay

- **Yêu cầu đã chốt:** tiếp tục V2 trong một nhà mẫu; sửa biên sáng/tối cứng cùng vệt đen/nhấp nháy gần tường. Sau mỗi bước dùng ảnh Game View thật để đối chiếu; dừng ở V2 để user nghiệm thu, không tự làm V3.
- **Chẩn đoán có bằng chứng:** probe 180 bước tiến vào và chạy song song tường ghi nhận ray kề nhau chênh khoảng 15–17 world unit tại góc/cửa. Nội suy khoảng cách trực tiếp tạo tam giác tối dài. Shader cũng từng đo mảng khoảng cách cache từ vị trí Player mới thay vì gốc physics scan tương ứng. Dump atlas liên tục và không có sọc tương ứng, nên không sửa atlas/collider theo giả thuyết sai.
- **Ba vòng cô lập:** vòng 1 ghép đúng scan origin nhưng nội suy visibility hai ray tạo spoke banding; vòng 2 cho phép một ray kề vẫn còn sọc; cả hai bị loại và lưu ảnh. Vòng 3 gửi `_IndoorOcclusionOrigin` và dùng cubic B-spline bốn ray không âm chỉ trên pixel surface opt-in. Sàn/actor/phòng sau tường giữ occlusion khoảng cách cũ; không blur toàn màn hình, không vá tọa độ nhà.
- **Đối chiếu thật:** `V2_Diagnostic_FinalBurst/` giữ 30 frame liên tiếp và CSV 180 bước; đường tái hiện không còn nan đen bật liên tục như ảnh user V1. Bộ `v2-final-day/night-on/off.png` cho thấy tường/tranh/tủ sáng theo nhìn, ban ngày OFF sáng nền hơn ban đêm OFF, phòng sau vẫn tối và tường phải tối sớm hơn concept đầu. Cạnh tại vật cản thật còn rõ có chủ ý để tránh leak; user chưa nghiệm thu cảm giác chạy thật.
- **Xác minh:** Console cuối 0 error. Integration **1/1 pass** job `75cc0c4b5a0a421b932d50822efda2c0` (31.772s); EditMode **44/44 pass** job `6c0247d57ee948328ad7372d81e14a50` (30.268s); PlayMode visibility/zombie **5/5 pass** job `8d10a50c13f74cb7b47e9ccd55a0d14b` (21.854s). Chưa full suite, Host+Client, build hoặc soak; automated pass không thay test tay.
- **Hiệu năng A/B/A:** 240 frame/lượt cùng pose, median GPU 3.244032/3.488768/3.227648 ms, V2 khoảng +0.245 đến +0.261 ms; CPU main 16.9778/16.8433/16.9434 ms nhưng không gọi là cải thiện; draw/batch không đổi 454. Dữ liệu `v2-perf-*.csv` và `v2-final-profile-summary.json`; chưa suy rộng sang build/độ phân giải khác.
- **Phạm vi/Git:** không đổi scene/prefab/animation, không thêm Fusion state/RPC, không mở rộng nhà khác. Checkpoint `ddf440424` và nhánh checkpoint nguyên vẹn; toàn bộ V1/V2 vẫn local chưa commit/push/merge. Chi tiết test tay và kết quả mong đợi trong README QA.

## Entry 2026-08-31 — User nghiệm thu một phần V2, yêu cầu push save point trước cải tiến đèn pin

- **Đã test tay:** user đánh giá ổn hơn; viền đen khi sát tường còn nhẹ nhưng không nhấp nháy liên tục, tạm pass và để backlog. Decor sáng theo đèn pin pass; ca đi ra ngoài/quay lại pass. Đây là save point khá an toàn theo xác nhận trực tiếp của user.
- **Đính chính đánh giá trước:** chuyển sáng sang tối vẫn CHƯA pass. User cần giảm cường độ từ sáng mạnh → vừa → yếu → tối dần trên sàn/tường/decor; không yêu cầu làm mềm hình học cạnh tường. Không được gọi biên ánh sáng gắt trong ảnh user là bóng tường có chủ ý để bỏ qua yêu cầu này. Ảnh review giữ tại `QA_Artifacts/IndoorFogPrototype_20260831/V2_UserReview_20260831/`.
- **Đã duyệt:** chỉ chỉnh dải chuyển của đèn pin trong bước tới; ánh sáng/tầm nhìn thường giữ nguyên. User muốn nếu khả thi thì ghi kế hoạch sau cho tầm nhìn thường, chưa triển khai cùng lượt. Không mở rộng nhà/map, không làm lại cơ chế blocker đã pass.
- **Quyền Git:** user yêu cầu rõ kiểm tra GitHub Desktop và push toàn bộ thay đổi hiện tại trước khi làm tiếp. Đã đối chiếu Desktop đúng repo DuAnTotNghiep/nhánh `codex/restore-indoor-vision-20260831`; fetch origin không thấy main mới. Save point gồm code prototype V2, QA, backup và tài liệu đang local; không merge/push main. Các thay đổi đèn pin sau save point cần test tay mới, không mặc nhiên thuộc quyền push snapshot hiện tại.
- **Kế hoạch đã chốt:** kiểm tra nguồn cắt cường độ (Fog opacity và Light2D), giữ visibility/occlusion; giảm cường độ dần trong vùng chiếu hiện có, không mở lộ phía sau tường. Chụp trước/sau cùng pose, ngày/đêm ON/OFF, quay và đi sát tường, kiểm tra Console/test và hiệu năng.
- **Backlog:** (1) viền đen nhỏ sát tường chỉ xử lý khi có lợi hơn chi phí; (2) cân nhắc chuyển mềm cho tầm nhìn thường sau khi đèn pin đạt nghiệm thu; (3) atlas động/build/multiplayer còn theo các giới hạn cũ.

## Entry 2026-08-31 — Push save point V2 và thử gradient chỉ đèn pin

- **Save point đã hoàn tất:** commit/push `ee0554d149a15fd89b3a370fab974058f235e668`, đối chiếu hash remote và GitHub Desktop No local changes trước sửa mới. Trong khi làm, remote đổi: PR #326 được merge vào main `5aa09d71c`, nhánh remote bị xóa. Xác minh ee0554d14 là ancestor và tree không đổi; agent không thực hiện merge này. Branch local giữ nguyên, upstream hiện gone. Không push lại hoặc tự chuyển branch.
- **Đã triển khai local, chưa nghiệm thu:** tăng dải cường độ Fog inward bằng `flashlightConeFeather=.5`, đồng bộ lõi Light2D ~61.21° chỉ trong active house opt-in + flashlight. Giữ góc ngoài/CurrentVisionAngle145°, normal100/140°, nhà khác torch105/145°. Bước shader-only chưa đủ rõ vì lõi đèn thật còn rộng, nên sửa đồng bộ cả hai. Không thay occlusion, atlas, scene/prefab/animation hoặc Fusion. Diagnostic màu đã gỡ.
- **Đối chiếu thật:** bộ ảnh gradient-before/step1/step2/final và state, đặc biệt close-before so step2-day-edge. Dải trung gian rộng hơn, ngày cải thiện vừa phải, đêm rõ hơn; vẫn thấy ranh vùng chiếu, chưa tuyên bố giống hoàn toàn concept. OFF giữ nền cũ. Motion mới ở FlashlightGradient_Motion đủ180 bước mask/surface1/1, 41 ảnh; xem các frame090/095/100/110/119 không thấy vệt lớn bật lại. Đây là teleport QA, không thay test WASD/soak.
- **Kiểm thử mới:** EditMode44/44 pass29.5664321s (XML gradient-editmode-results.xml); PlayMode5/5 pass21.7041253s, job6bb84f08f8874923af1306c0d5377f9a. Integration11 trạng thái kiểm tra profile đèn và góc gameplay, ngoài/nhà khác/disable/return. Console0error, diff-check pass, không diff scene/prefab/anim/controller. Đầu lượt có một Solo timeout trước sửa, sau chuyển về Game View và mở lại thì các Solo/integration thành công; chưa kết luận nguyên nhân, không sửa loading.
- **Hiệu năng:** A/B/A240frame mỗi lượt, GPU median3.474432/3.473408/3.472384ms, CPU main19.81955/18.97965/18.93215ms, draws454. Chưa thấy GPU tăng đáng kể. Cả A/B dùng mã PlayerVision mới nên không gọi đây là phép đo toàn bộ overhead so binary V2; không cam kết60FPS/build.
- **Bàn giao:** pose nhà mẫu(-39.2,44.3), up,13:30, torchON, HP100,67surfaces; chụp gradient-manual-ready rồi Return Manual Control. Cách test + kết quả mong đợi, rủi ro và kế hoạch trong QA_Artifacts/IndoorFogPrototype_20260831/FLASHLIGHT_GRADIENT_REVIEW.md. Đã cập nhật README/handoff/prompt; không quên normal-vision gradient là backlog, viền nhỏ tạm pass.
- **Git/phần chưa xác minh:** gradient và QA/docs mới vẫn local chưa commit/push, chờ user test. Chưa full suite/build/HostClient/soak, atlas động và mở rộng nhà khác chưa làm. Nếu user bác kết quả, lưu patch/file mới rồi hoàn tác có chọn lọc về ee0554d14, không reset/clean toàn repo.

## Entry 2026-08-31 — User bác kết quả gradient, thảo luận lại đúng ranh

- User đã test xong, yêu cầu Stop; Unity xác nhận đã Stop. User nhận xét không thấy cải thiện rõ so concept; khoanh cụ thể ranh sáng/tối bên phải. Agent thừa nhận đã đối chiếu ảnh nhưng đánh giá quá cao và chưa xác minh đúng vùng này. Trạng thái gradient: CHƯA ĐẠT, không chỉ chờ test nữa. 3 ảnh lưu Gradient_UserReview_20260831.
- Đọc lại shader: cone feather sửa theo angleDot, nhưng visible/occlusion và lớp max-opacity0.98 ở cuối có thể cắt dải. Cơ chế tồn tại được code xác nhận; nguồn đóng góp chính xác Fog/ShadowCaster tại pixel user khoanh chưa tách lớp runtime. Không tăng tham số cone tiếp mà gọi là đã xử lý.
- Hướng đang thảo luận: fade theo khoảng cách tới ranh hiển thị cuối, tối sớm về phía sáng, khớp tối tại ranh và giữ vùng khuất không sáng thêm. Nếu thử, giữ một nhà/torch và chụp crop đúng vùng/cùng zoom, motion/leak/perf; chưa triển khai hoặc chốt kỹ thuật mask/SDF. Không refactor lớn hoặc mở rộng normal vision.
- Quy tắc bàn giao user bổ sung: bật bất tử, tắt đèn trước giao để không hao pin, đảm bảo movement đã trả. Lần setup vừa rồi God Mode chưa bật được; đã sửa việc QA khóa movement bằng Return Manual Control. Không ghi bất tử thành công. Lượt này chỉ lưu ảnh/tài liệu, không sửa code, không chạy test/Play, không commit/push/rollback.

## Entry 2026-08-31 — User duyệt vòng fade ranh, đóng gói chuyển task

- User chấp nhận tối sớm phía sáng, yêu cầu test/so concept khắt khe và logic ở nhiều vị trí/hướng/ảnh hưởng nhà khác. User báo32% context và cho agent tự quyết định chuyển; chọn chuyển trước vòng triển khai mới để tránh lặp chẩn đoán/đánh giá sai.
- Kế hoạch/cổng nghiệm thu mới: QA_Artifacts/IndoorFogPrototype_20260831/BOUNDARY_FADE_NEXT_ITERATION.md. Phê duyệt này thay trạng thái chỉ thảo luận ở entry trước. Chưa triển khai code mới trong lượt đóng gói; cone-only vẫn bị bác, V2ee0554d14 là baseline user đã test.
- Cần phân biệt và chẩn đoán Fog/Light2D tại ranh cuối; fade phía sáng bằng dữ liệu Player/blocker, không vá tọa độ. Cổng hình ảnh: crop đúng ranh/cùng tỷ lệ so concept, khác biệt rõ, nhiều vị trí/hướng, motion, ngày/đêmON/OFF, ra/vào/nhà lân cận giữ nguyên, đo perf đúng baseline. Không làm mất decor pass hoặc thêm leak. Một candidate có giới hạn rồi quyết định dừng nếu không tiến triển/đòi refactor lớn.
- Chuyển task tiếp tục cùng project Unity và bảo toàn toàn bộ dirty code/ảnh/docs, không push/merge hoặc reset/clean. Quy tắc handoff test: GodModeON, flashlightOFF, movementON xác minh thật. Lần trước GodMode chưa bật vì tool DDOL/UI, không lặp tuyên bố đã bật.

## Entry 2026-08-31 — Sol-High loại radial regression và làm lại theo silhouette

- **Model/handoff đã xác nhận:** lượt này metadata thực là `gpt-5.6-sol/high`. Đã ghi quy tắc lâu dài trong handoff/prompt: task tiếp theo phải truyền rõ `model="gpt-5.6-sol"`, `thinking="high"`; không để app mặc định Luna, chỉ đổi khi user yêu cầu.
- **User bác bản trước:** ảnh radial candidate làm tối cả tường có đèn, thụt lùi so Photo 1/concept. Đã thừa nhận đánh giá visual trước sai; giữ source/pose và ảnh bản bị loại tại `RejectedRadialFade_20260831/`, không reset/clean.
- **Nguyên nhân và implementation mới:** radial `wallDistance-testedDistance` coi mọi mặt tường là ranh. Candidate mới tái dùng 180 ray cache, chỉ tạo tối đa16 shadow edge tại độ nhảy near→far giữa hai ray kề; mặt tường liên tục không fade. Shader chỉ tăng opacity trong dải0.65m quanh silhouette, không sửa visibility/ray, không reveal vùng khuất. Khôi phục lõi Light2D105°, cone feather.20 như V2. Không đổi scene/prefab/animation/Fusion hoặc normal vision; chỉ nhà mẫu runtime opt-in.
- **Đối chiếu ảnh:** bộ `shadow-verified-{center,edge,nearwall,corner,down,night,dayoff,nightoff}-{v2,fade}`. ROI center: tường sáng84.25→84.25 và kệ91.39→91.38, trong khi radial chỉ40.96/39.13; mép phải46.60→25.31. OFF day A/B giống pixel, night chênh tối đa1 mức. Kết quả đúng hướng và không còn tối toàn tường, nhưng dải ở vài góc vẫn hẹp/tối nhanh hơn concept; không gọi là match hoàn toàn hay user PASS.
- **Kiểm tra:** compile0error. EditMode integration+classifier4/4 pass job`30ee6c8119524f059708ee51ae08f739`28.4382258s. PlayMode visibility/zombie5/5 pass job`a3401bbc67904de0918c59bf0dbc987d`20.8843838s. Motion fixture180mẫu/41ảnh trong `ShadowBoundary_Motion`; chưa thay test WASD của user. A/B/A240frame: GPU3.495936/3.586048/3.494912ms, Fog marker.05310/.05425/.05185ms, draw/batch458; khoảng+.09ms GPU trong Editor, chưa build.
- **Trạng thái nghiệm thu/phạm vi:** đã triển khai và self-review; user CHƯA test tay candidate. Chưa full suite/build/HostClient/late join/reconnect/soak/dynamic atlas. Review và cách test tại `SHADOW_BOUNDARY_REVIEW.md`.
- **Git:** branch`codex/restore-indoor-vision-20260831`, HEAD/base`ee0554d14`, upstream gone. Candidate/QA/docs dirty local, không commit/push/merge; giữ các artifact regression PlayMode và bản backup trước test.
- **Bàn giao runtime cuối:** Play Solo/Main tại nhà mẫu, prototype runtime active. `shadow-final-review-ready-state.txt` xác nhận HP100, GodMode=true, flashlight=0, movementEnabled=true, cheatMenuOpen=false. Một input `W` thật làm Y44.30→44.32 (`shadow-final-input-after-w-state.txt`), sau đó đã đặt lại đúng pose và chuẩn bị review state lần nữa. Đây chỉ xác minh input hoạt động, không phải user nghiệm thu hình ảnh.
## Entry 2026-08-31 — Triển khai starter loadout và cân bằng loot

- **Đề xuất:** dùng bảng loot container independent-roll đã cân lại: Ammo762 20%, Bandage 35%, EnergyWater 20%, Meat 40%, PainKiller 15%, Water 45%, Ammo12Gauge 15%; backpack giữ base 10% và tier 50/30/15/4/1; bonus weapon dùng base 15% nhân multiplier Easy/Normal/Hard = 22.5%/15%/6%. Loot xác giữ gate Easy/Normal/Hard = 45%/30%/12%, trong xác ưu tiên Bandage: Water 25%, Bandage 45%, PainKiller 15%, Ammo762 10%, Ammo12Gauge 5%.
- **Đã duyệt:** user xác nhận triển khai; Easy/Normal dùng 1 súng random trong pool hiện có + đúng 1 băng theo asset vũ khí; Ammo762 random 15–30; Ammo12Gauge cố định 5; loot xác có Ammo12Gauge và tỉ lệ Bandage cao.
- **Đã triển khai:** `DifficultyRules` có pool AK47/S12K và loadout Easy = random weapon, Water x3, Meat x3, Flashlight x1, Bandage x5, PainKiller x1; Normal = random weapon, Flashlight x1, Bandage x3; Hard = Flashlight x1. `InventorySystem` chọn súng một lần trên State Authority, replicate `StartingWeaponId`, cấp ammo theo `ammoTypeRequired`/`magazineCapacity` và retry không roll lại/không nhân bản. `LootQuantityRules` áp Ammo762 15–30 và Ammo12Gauge 5; bảng mặc định/3 zombie prefab đã cập nhật. 29 file scene/prefab thường với 40 field serialized đã chuyển bonus weapon 25% → 15%; 4 prefab Route B quân sự vẫn giữ 25% vì bypass random loot.
- **Bảo toàn:** Route B military manifest/authority flow không đổi; tutorial kitchen vẫn giữ exception Ammo12Gauge authored 12 viên; corpse search/RPC và inventory sync vẫn canonical theo State Authority; backup dưới `EnvironmentFixerBackups` không đụng tới.
- **Đã test tự động:** test RED trước code job `3d553c9604524b5388a650aae0490c68` ghi nhận 7 failure đúng các contract cũ. Sau triển khai: `InventoryAndLootCapacityTests` **15/15 pass** (`a468734ec2054602aba1126ac1d10ed2`), `ReadinessAndChatEditorTests` **43/43 pass** (`464fdab949a74dfab2b9938d69c2a0fd`), asset loot table **1/1 pass** (`396d3f4016bf4e48ab4d69ebbcf408cb`), PlayMode **15/15 pass** (`b37015d539ad418c8916d6b9253350fd`). Unity refresh/compile xong, Console snapshot có **0 error**. Full EditMode job `a694e977d654456c987e5239d16c43f3` chạy 177 test nhưng còn 1 failure cũ, tái hiện độc lập ở `IndoorFogSurfacePrototypeEditorTests.SurfaceProjection_RealSolo_CachesAtlasAndStaysInsideOptInHouse`: Win32 IO 1224 khi QA helper xử lý `QA_Artifacts/IndoorFogPrototype_20260831/pose.json`; không chạm vào file gameplay này.
- **Build kiểm tra thêm:** `dotnet build Assembly-CSharp.csproj --no-restore` chưa pass vì generated csproj hiện thiếu nhiều source/assembly-boundary type có sẵn trong Unity compile (10 CS0246, gồm `SleepInteractable`, `FlashlightController`, `ZombieCorpseLoot` và các Route type); csproj không nằm trong diff. Unity compiler và Unity Test Framework là bằng chứng build/test chính của task này.
- **Đã test tay:** chưa test tay bởi user và chưa gọi đây là nghiệm thu cảm giác loot. PlayMode đã chạy flow Solo/Fusion hiện có, nhưng chưa mở hai client thật để quan sát inventory replication của starter ammo.
- **Chưa xác minh:** phân bố thống kê dài hạn trong gameplay, host+client/late-join riêng cho starter loadout mới, build executable và tuning cảm giác qua nhiều trận. Cần test manual trên Easy/Normal/Hard trước khi merge/push.
- **Git/handoff:** làm trên branch local `codex/starter-loot-balance-20260831`, chưa commit/push/merge. Kế hoạch lưu tại `docs/plans/2026-08-31-starter-loot-balance.md`. Phiên này không có connector Antigravity trực tiếp; worker nội bộ chỉ hỗ trợ checkpoint test, phần triển khai đã hoàn tất trên branch hiện tại và cần được Antigravity/user review trước khi tích hợp.

## Entry 2026-08-31 — User nghiệm thu shadow-boundary checkpoint và yêu cầu push

- **Đã test tay/đã duyệt:** user test nhanh candidate Sol-High và đánh giá chung “rất tốt”, đủ làm checkpoint an toàn. Ảnh 1 xác nhận vùng sáng–tối rõ và chuyển mềm; chưa hoàn hảo nhưng đạt mức chấp nhận hiện tại. Bằng chứng lưu tại `QA_Artifacts/IndoorFogPrototype_20260831/ShadowBoundary_UserReview_20260831/user-approved-gradient.png`.
- **Backlog đã xác nhận:** ảnh 2 còn viền/vệt đen khi soi sát tường có nhiều decor. Nhận định kỹ thuật hiện tại: nhiều khả năng do surface projection/footpoint/pivot của các sprite decor giao với occlusion mask ở các góc khác nhau, không phải phản xạ ánh sáng vật lý. Có thể cải tiến nhưng rủi ro chạm lại blocker đã ổn; chưa sửa trong lượt chỉ thảo luận và không chặn checkpoint. Ảnh lưu tại `user-decor-black-rims.png` cùng thư mục.
- **Kế hoạch mở rộng:** chưa bật ngay toàn bộ nhà trong Main. Bước an toàn tiếp theo là pilot 2–3 công trình đại diện (nhà ít decor, nhà nhiều decor, công trình lớn/cửa phức tạp), author/validate dữ liệu surface, kiểm tra transition/atlas lifecycle/memory/perf và Solo + Host/Client. Chỉ sau pilot mới batch mở rộng khu dân cư và các khu lớn. Lỗi decor được đo trong pilot rồi quyết định sửa trước hay hoãn.
- **Git:** theo yêu cầu user, tạo checkpoint implementation/evidence `439bfeadd`, fetch/prune thấy `origin/main` có commit mới `8970a8cf6` về starter loadout/zombie loot và `Main.unity`. Đã merge có kiểm soát thành `d4fc7b472`; conflict duy nhất ở work log, đã giữ nguyên cả hai entry. Không có conflict code/Scene/prefab. Tiếp tục commit hai ảnh user/review rồi push lại đúng nhánh `codex/restore-indoor-vision-20260831`; không push trực tiếp main.
- **Xác minh sau merge:** Unity compile 0 error (còn hai warning cũ CS0618/CS0414). EditMode Indoor Fog + starter loadout **19/19 pass**, job `6ac8c18d9a344f8ebcf29416f3d542fd`, 49.1750489s. PlayMode visibility/zombie **5/5 pass**, job `e46a99552eea46f58cd1bd031e622f1c`, 26.1860518s. Đây là cổng hậu-merge phù hợp, chưa phải full suite/build/Host+Client.
