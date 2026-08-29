# NEXT SESSION — ROUTE B GIT INTEGRATION HANDOFF

> **ĐÃ HOÀN TẤT / KHÔNG CHẠY LẠI NHƯ CHECKLIST HIỆN TẠI.** Đây là hồ sơ của lần integration 2026-08-26. Route B final polish sau đó đã vào `main` qua PR #323 và `main` hiện đã tiến xa hơn. Phiên mới phải đọc `CODEX_PROJECT_WORK_LOG.md`, kiểm tra Git thực tế rồi lập kế hoạch mới; không tạo safety branch/merge lại chỉ vì hướng dẫn lịch sử bên dưới.

Ngày cập nhật: 2026-08-26

## Prompt dùng để mở phiên Codex mới

Tiếp tục công việc trong project Unity tại `E:\Lap_trinh\HocTap\DuAnTotNghiepFIx\My project`.

Mục tiêu của phiên này là bảo toàn toàn bộ phần Route B đã hoàn thành, tạo một commit/nhánh an toàn, lấy code mới nhất mà thành viên khác đã push, ghép hai phía một cách có chủ đích, tự build và chạy lại test Unity, sau đó push feature branch lên remote để tôi kiểm tra. Không push trực tiếp vào `main`.

Trước khi sửa code hoặc chạy lệnh Git làm thay đổi working tree, hãy đọc đầy đủ các tài liệu theo đúng thứ tự ở mục “Thứ tự tài liệu cần đọc” bên dưới. Sau đó kiểm tra `git status`, branch, HEAD, remote và divergence thực tế. Không được giả định `origin/main` hiện tại đã mới nhất nếu chưa `git fetch`.

Working tree hiện có nhiều thay đổi hợp lệ chưa commit. Tuyệt đối không dùng `git reset --hard`, `git checkout --`, `git restore`, clean, stash mù quáng, hoặc chọn toàn bộ `ours`/`theirs`. Trước khi fetch/merge, hãy tạo feature branch an toàn từ trạng thái hiện tại và commit tường minh các file thuộc Route B. Không đưa bốn ảnh `opencode-screen*.png` vào commit. Các file `.meta`, prefab và thay đổi `Assets/Scenes/Main.unity` thuộc tính năng phải được giữ lại.

Sau khi có safety commit:

1. Chạy `git fetch --prune origin`.
2. Xác định chính xác nhánh/commit mà thành viên khác vừa push và nhánh đích cần tích hợp (thường là `origin/main`, nhưng phải kiểm chứng).
3. So sánh log và diff hai phía trước khi merge.
4. Merge nhánh remote mới nhất vào feature branch hiện tại. Ưu tiên một merge có checkpoint rõ ràng thay vì viết lại lịch sử.
5. Giải quyết từng conflict theo ngữ nghĩa. Đặc biệt cẩn thận với `Assets/Scenes/Main.unity`, các manager của Route B, Day/Night, PlayerSurvival và ba họ Zombie AI. Không được giải quyết conflict Unity YAML bằng cách chấp nhận nguyên một phía nếu chưa đối chiếu object/component/reference.
6. Bảo toàn đầy đủ các hành vi đã hoàn thành ở mục “Kết quả bắt buộc phải giữ”. Đồng thời tiếp nhận thay đổi hợp lệ của thành viên khác.
7. Refresh Unity, build các assembly liên quan, kiểm tra Console, chạy EditMode test và full Route B PlayMode test. Nếu merge đụng scene/prefab, phải mở/kiểm tra scene và reference trước khi kết luận.
8. Chỉ push feature branch sau khi toàn bộ kiểm tra đạt. Báo rõ tên branch, commit, remote branch, conflict đã xử lý, test đã chạy và kết quả mong đợi khi tôi test tay.

Nếu không thể xác định nhánh mà thành viên khác đã push, hoặc conflict chứa thay đổi thiết kế không thể suy ra an toàn, dừng tại safety commit/fetch và hỏi tôi; không tự chọn làm mất một phía.

## Thứ tự tài liệu cần đọc

1. `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md`
   - Đọc toàn bộ để hiểu luồng Route B hoàn chỉnh.
   - Chú ý các phần repair loot chính thức, cinematic/UI recovery, zombie corpse/siege và mục “Finale siege hardening sau test tay 2026-08-26”.
2. `NEXT_SESSION_MAINPLAY_PLAN.md`
   - Đọc toàn bộ kế hoạch MainPlay.
   - Chú ý phần loot sửa xe và mục “Hotfix finale sau test tay — 2026-08-26”.
3. `README_MAINPLAY_CODEX_HANDOFF.md` nếu file còn tồn tại.
4. Các file điều phối gameplay chính:
   - `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
   - `Assets/Script/Tin/MainQuest/SiegeHordeDirector.cs`
   - `Assets/Script/Tin/MainQuest/MilitaryRouteCinematicController.cs`
   - `Assets/Script/Tin/MainQuest/MilitaryRepairLootCoordinator.cs`
   - `Assets/Script/Tin/MainQuest/MilitaryRepairLootMarker.cs`
   - `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
   - `Assets/Script/Tin/MainQuest/EscapeRouteDecisionUI.cs`
   - `Assets/Script/Tin/MainQuest/RouteBRadioBroadcastUI.cs`
5. Các hệ thống được Route B tác động:
   - `Assets/Khoa/Code/DayNightManager.cs`
   - `Assets/Script/Tin/PlayerSurvival.cs`
   - `Assets/Script/Tin/AutoUIManager.cs`
   - `Assets/Khoa/Code/LootContainer.cs`
   - `Assets/Hau/Script/VehicleController.cs`
   - `Assets/Hau/Script/VehicleHornAudioController.cs`
   - `Assets/Thai/script/ZombieAI.cs`
   - `Assets/Khoa/Code/ZombieAI_Khoa.cs`
   - `Assets/Khoa/Code/ZombieAIKhoaRebuilt.cs`
6. Rules và tests:
   - `Assets/Script/Tin/Prototype/MilitaryStoryFlowRules.cs`
   - `Assets/Script/Tin/Prototype/MilitaryRepairLootRules.cs`
   - `Assets/Script/Tin/Prototype/Tests/Editor/MilitaryRepairLootRulesTests.cs`
   - `Assets/Script/Tin/Prototype/Tests/Editor/QuestFlowUIPrototypeTests.cs`
   - `Assets/Script/Tin/Prototype/Tests/PlayMode/MainMenuToMilitaryQuestFlowTests.cs`
7. Asset wiring:
   - `Assets/Resources/NetworkPrefabs/MilitaryRepairLootContainer.prefab`
   - `Assets/Scenes/Main.unity`

## Trạng thái Git trước khi tích hợp

- Branch đang đứng lúc ghi handoff: `codex/military-repair-loot-checkpoint`.
- HEAD: `23412e49c` — `docs: plan stable police repair loot rebuild`.
- Checkpoint cha an toàn: `f2551d1cb` — `chore: checkpoint military repair flow before loot handoff`.
- Local `main` và remote-tracking `origin/main` lần cuối nhìn thấy ở `4696253f5`, nhưng thông tin này có thể cũ vì chưa fetch thay đổi mới của đồng đội.
- Branch hiện tại chưa thấy upstream được cấu hình.
- Remote `origin`: `https://github.com/KhoaCodeBug/DuAnTotNghiep.git`.

Các file thay đổi/untracked thuộc công việc cần giữ đã được liệt kê trong `git status`. Trước khi commit phải rà lại diff và stage tường minh. Không stage các ảnh sau:

- `opencode-screen-final.png`
- `opencode-screen-latest.png`
- `opencode-screen.png`
- `opencode-window.png`

## Kết quả bắt buộc phải giữ sau khi merge

### Repair loot chính thức

- Có prefab network `MilitaryRepairLootContainer` và năm marker được author trong scene.
- Năm vị trí loot bảo đảm đủ năm vật phẩm sửa xe, có thêm weapon/ammo theo thiết kế.
- Việc spawn/claim có authority validation, không double claim và xử lý trường hợp inventory đầy.
- Đây là implementation sạch thay thế nhánh Ox Alpha đã discard; không phục hồi code `PoliceRepairLoot*` cũ.

### UI sau cinematic

- Khi Cinemachine/cinematic kết thúc, UI gameplay được phục hồi đúng.
- Nhấn Tab mở lại túi đồ.
- Tương tác tủ/container loot mở được UI.

### Zombie và siege finale

- Zombie áo trắng và áo vàng đều chết thật; animation chết xong không ngồi dậy đánh tiếp.
- Zombie áo vàng dùng họ `ZombieKhoaRebuilt`; presentation chết vẫn được render nhưng objective/horde đã retire nó.
- Khi cổng còn sống, zombie ưu tiên đánh cổng theo flow siege.
- Khi cổng HP về 0, toàn bộ horde hiện tại chuyển sang truy đuổi Player sống gần nhất.
- Sau khi cổng vỡ, các spawn point vẫn tiếp tục sinh zombie và zombie mới lập tức truy đuổi Player.

### Time, sleep và repair

- Khi finale cinematic bắt đầu, authority đưa thời gian về 16:00 bất kể trước đó sáng/tối.
- Thời gian bị khóa ở 16:00 trong finale, đồng hồ HUD bị ẩn.
- Player luôn tỉnh táo, fatigue về 0 và sleep transition bị hủy để không gục khi sửa xe.
- Repair chỉ bị gián đoạn bởi đòn tấn công trực tiếp từ zombie; hunger, thirst, bleeding hoặc damage-over-time không làm reset/hoãn repair.

### Siren

- Siren xe phát đầy đủ trong cinematic.
- Sau cinematic, siren tiếp tục phát ở 20% âm lượng gốc làm ambience thu hút zombie.
- Siren chỉ dừng khi hoàn thành đủ năm hành động sửa xe.

## Kết quả kiểm thử gần nhất trước Git integration

- Build `Assembly-CSharp`: 0 errors, còn 6 warnings cũ.
- Build EditMode/PlayMode assemblies: 0 errors, 0 warnings.
- EditMode direct-damage rule: 1/1 passed.
- Full Route B PlayMode test `RouteBDebugFlowRunsThroughAuthoritativeRepairLootAndMilitaryExtraction`: 1/1 passed trong khoảng 37.273 giây.
- Test bao phủ time lock/clock/sleepiness, siren 20% rồi stop, yellow-zombie death, gate-break retarget, post-break spawn, UI/inventory/loot và extraction.
- Unity Console sau refresh/clear cuối: 0 errors.

Sau khi merge phải chạy lại vì kết quả trên chỉ chứng minh trạng thái trước khi nhận code mới từ remote.

## Những việc chưa được xác nhận

- Chưa fetch nên chưa biết commit/nhánh mới nhất của thành viên khác và chưa biết conflict thực tế.
- Chưa thực hiện merge trong handoff này.
- Chưa có kết quả test tay Host + Client multiplayer sau lần hardening cuối; đây vẫn là kiểm tra nên làm nếu môi trường cho phép.

## Quy tắc làm việc

- Luôn giữ checkpoint có thể quay lại trước thao tác tích hợp.
- Không làm mất thay đổi của người dùng hoặc đồng đội để “cho hết conflict”.
- Mọi thay đổi gameplay phải được kiểm tra trong Unity, không chỉ compile C#.
- Với scene/prefab, phải kiểm tra reference và hành vi runtime sau merge.
- Không báo hoàn thành nếu chưa có build/test tương xứng.
- Khi bàn giao phải đưa test tay theo thứ tự và kết quả mong đợi cụ thể.
- Không push thẳng `main`; push feature branch và để review/PR xử lý bước hợp nhất cuối.

## Kết quả Git integration — 2026-08-26

- Đã tạo nhánh an toàn `codex/route-b-integration-20260826` và commit toàn bộ Route B tại `197b51371`; bốn ảnh `opencode-screen*.png` không được stage.
- Đã fetch `origin`; `origin/main` mới nhất tại thời điểm tích hợp là `13b1c575e`, gồm Route A repair/outro và nhóm prefab loot mới.
- Merge chỉ conflict tại `Assets/Scenes/Main.unity`: Route B thêm năm `MilitaryRepairLootMarker`, Route A thêm `CivilianRouteCheckpoint`, `CivilianCityExit` và `CivilianOutroEnd`. Resolution giữ đủ cả tám root và toàn bộ serialized reference của hai tuyến.
- Unity 6.0.69f1 refresh/compile thành công. `Main.unity` validate sạch: không missing script hoặc broken prefab; kiểm tra live hierarchy thấy đúng một object cho từng marker/mốc của cả hai tuyến.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q`: 0 error, 8 warning hiện hữu từ project/dependency.
- Build `ProjectZomboiNhai.QuestUI.Tests.Editor.csproj` và `ProjectZomboiNhai.QuestFlow.Tests.PlayMode.csproj`: 0 error, 0 warning.
- Toàn bộ EditMode assembly: `112/112` pass trong `7,34s`.
- Full Route B PlayMode `RouteBDebugFlowRunsThroughAuthoritativeRepairLootAndMilitaryExtraction`: `1/1` pass trong `38,51s`.
- PlayMode giữ nguyên police `Car` author: `1/1` pass trong `10,28s`.
- Sau test, Unity Console đã clear và đọc lại: 0 error.
- Một test transaction được làm ổn định sau merge: trước từng direct authority loot RPC, test đặt lại Player cạnh container vì `NetworkRigidbody2D` sẽ reconcile raw `transform.position` sau network tick. Production validation phase/distance/LOS/inventory/double-claim không thay đổi.
- Acceptance Host + Client hai máy và cảm giác thực tế của Route A outro/Route B finale vẫn là QA tay cuối, không được suy diễn từ PlayMode Solo.
