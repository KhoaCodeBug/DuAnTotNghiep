# MainPlay — Kế hoạch phiên tiếp theo

> **TRẠNG THÁI TÀI LIỆU:** kế hoạch lịch sử của chương bệnh viện/finale. Không còn là danh sách task hiện tại. Khi mở phiên mới, đọc `CODEX_PROJECT_WORK_LOG.md` trước; chỉ quay lại file này khi cần lý do thiết kế H1–H5 hoặc lịch sử Route B.

> Cập nhật: 2026-08-25
> H1–H5 bệnh viện đã chốt implementation. Ưu tiên tiếp theo chuyển sang thảo luận finale căn cứ trong `NEXT_SESSION_MILITARY_FINALE_PLAN.md`.
> Không bắt đầu bằng flow cũ Bàn Điều phối → Radio → Tủ hồ sơ và không chờ LootContainer bệnh viện.

## Tài liệu bắt buộc đọc

1. `HOSPITAL_ROUTE_DESIGN_LOCK.md` — nguồn canonical cho thiết kế bệnh viện mới.
2. `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` — trạng thái toàn Tuyến B và ranh giới với căn cứ.
3. `README_MAINPLAY_CODEX_HANDOFF.md` — những gameplay đã thực sự triển khai.

## Trạng thái bàn giao

- Chủ dự án đã đặt trong scene `Main`:
  - `HospitalQuest_ShiftLog` tại quầy tiếp tân.
  - `HospitalQuest_ShiftLog2` tại văn phòng ngay sau tiếp tân.
  - `HospitalQuest_RadioRoom/DoorInteraction`.
  - `HospitalQuest_RadioRoom/RadioInteraction`.
  - `HospitalQuest_ZombieEntry_A/B`.
- Chủ dự án xác nhận đường từ văn phòng ra thẳng phòng Radio không bị collider/tilemap chặn.
- Phòng Radio nhỏ; Door/Radio phải dùng vùng nhỏ, state gating và xác thực server để không tương tác xuyên tường.
- H1–H4 đã có runtime authoritative và scene wiring; H4 còn cần test tay cảm giác âm thanh, vị trí xác và cân bằng chiến đấu.
- Working tree có thay đổi scene/map và công cụ môi trường của người dùng/thành viên khác. Không restore, không gom vào commit bệnh viện nếu không thuộc phạm vi được xác nhận.

## H1 — Cửa và vùng tương tác

**Trạng thái 2026-08-25: ĐÃ TRIỂN KHAI.** Cửa dùng `Door13_W` khi đóng và `Door14_W` khi mở; blocker `Obstacle` riêng được bật/tắt theo network state. Door/Radio có vùng nhỏ, hold interaction, kiểm tra khoảng cách/line-of-sight phía State Authority và gating loại trừ nhau. Sau H2, cả Editor/Development/Release đều phải có shared key mới mở được cửa; không còn bypass keyless.

Kết quả tự động: `89/89` EditMode pass, smoke test PlayMode H1 `1/1` pass, regression Tuyến B `1/1` pass.

Kết quả test tay Solo ngày 2026-08-25 — **PASS**: Player spawn → dùng cheat tới nhiệm vụ bệnh viện → đi thẳng tới phòng Radio phía sau → giữ E mở cửa → tương tác thiết bị Radio thành công. Chưa xác nhận Host/Client/late join.

Mục tiêu: tạo nền cửa Radio đúng và kiểm chứng căn phòng nhỏ trước khi nối quest.

- Kiểm kê tile/sprite cửa đóng và cửa mở có sẵn; ưu tiên asset hiện có.
- Cửa bắt đầu đóng, có collider chặn thật.
- `DoorInteraction` chỉ hoạt động khi cửa đóng.
- `RadioInteraction` vô hiệu tuyệt đối khi cửa chưa mở.
- Sau khi mở: đổi hình cửa, tắt collider, tắt Door và bật Radio.
- Vùng cửa khuyến nghị `0,55–0,7`; vùng Radio `0,45–0,6`, điều chỉnh theo PlayMode thật.
- State Authority kiểm tra state và khoảng cách; client không tự mở cửa.
- Không thay đổi flow quest ở checkpoint này ngoài test harness tối thiểu nếu cần.

**Bài test bàn giao H1:**

- Solo/Host không thể bấm Radio qua cửa.
- Không xuất hiện hai prompt cùng lúc.
- Mở cửa xong mới tiếp cận được Radio.
- Client/late join nhìn đúng trạng thái cửa.

## H2 — Manh mối và chìa khóa shared

**Trạng thái 2026-08-25: ĐÃ TRIỂN KHAI.** State machine authoritative chạy `FindShiftLog → FindShiftLog2 → FindRadioKey → UnlockRadioRoom → RadioReady`. Host chọn một trong 6 KeyLoot bằng stable ID replicated; chỉ nhặt tại điểm được chọn mới trao shared key. Journal/waypoint/F6/F12 và late-join snapshot theo flow này.

Kết quả tự động: compile sạch; `87/87` EditMode pass; PlayMode H2 + regression Tuyến B `2/2` pass; Console sau khi clear có `0` error/warning. Test tay Solo/Host/Client/late join H2 vẫn chờ chủ dự án xác nhận.

Cập nhật test tay 2026-08-25: chủ dự án đã chạy flow H2 thực tế và xác nhận toàn bộ logic đạt. Hai polish phát hiện sau test đã được sửa trong source/scene: `HospitalQuest_ShiftLog` tại quầy tiếp tân tăng riêng tầm tương tác từ `0.85` lên `1.5` world-unit; nhãn waypoint bệnh viện dùng chữ trắng-xanh tương phản cao thay vì bị `GUI.color` nhân thành màu đen. Sau sửa, compile sạch, `4/4` rule test và `1/1` PlayMode scene/range test pass, Console `0` error/warning. Cần chủ dự án re-test trực quan đúng hai polish; chưa suy diễn thành multiplayer pass.

Mục tiêu: chạy được chuỗi bệnh viện tới lúc mở phòng Radio.

- Nối đủ ba tài liệu khu dân cư tới objective bệnh viện mới.
- `HospitalQuest_ShiftLog` cho biết trạm liên lạc nằm phía sau bệnh viện và chìa ở văn phòng trưởng ca.
- `HospitalQuest_ShiftLog2` tiết lộ lệnh phong tỏa và kích hoạt một KeyLoot ngẫu nhiên.
- Nhặt key tại Polygon được đánh dấu mới trao shared quest key; các KeyLoot khác không hoạt động.
- Chìa khóa không chiếm inventory, không mất khi chết/disconnect.
- Journal + waypoint dẫn tới `HospitalQuest_RadioRoom`.
- Nếu Player tìm cửa Radio trước, prompt chỉ dẫn về văn phòng trưởng ca; không soft-lock.
- Mở cửa cập nhật toàn đội authoritative.

**Bài test bàn giao H2:**

- Chạy cả đường canonical ShiftLog → ShiftLog2 → cửa.
- Thử tìm cửa trước ShiftLog.
- Hai client tách đội; một người lấy chìa, người còn lại mở được cửa.
- Disconnect người lấy chìa và late join không làm mất state.

## H3 — Radio, lời thoại và map reveal

**Trạng thái 2026-08-25: ĐÃ TRIỂN KHAI CODE + UNITY QA TỰ ĐỘNG; CHỜ TEST TAY.** Tổng tiến độ 14 giây, chia 3 chặng, là state dùng chung do State Authority giữ; thả E/rời vùng chỉ nhả operator, không mất tiến độ. Một người khác có thể tiếp tục. Người gần Radio nhận chuỗi bản ghi; người xa không bị ép UI/audio và vẫn nhận transcript/map state qua Journal. Radio trao trực tiếp Mảnh 2, map reveal, cinematic, Cue09 và bảng chọn lần hai.

Kết quả tự động trước H4: compile sạch; `HospitalRadioRoomRulesTests + QuestFlowUIPrototypeTests` đạt `49/49`; PlayMode scene H1–H3 `1/1`; regression `MainMenu → Ending B` `1/1`.

Mục tiêu: hoàn tất logic cốt truyện bệnh viện chưa có zombie cao trào.

- Radio cần tổng cộng khoảng 14 giây để khôi phục, chia đều thành 3 chặng.
- Chỉ một người vận hành; thả/rời vùng giữ tiến độ; người khác tiếp tục được.
- Không khóa UI/gameplay của đồng đội ở xa.
- Viết lại Cue 05–09 theo nội dung canonical trong design lock.
- Người gần Radio nghe audio; transcript lưu Journal cho người ở xa/late join.
- Radio trao trực tiếp tọa độ/Mảnh bản đồ 2; không tạo Records Cabinet.
- Giữ thứ tự: reward → map mở/reveal riêng căn cứ → map đóng → cinematic → bảng chọn tuyến lần hai.
- Bảng chọn lần hai vẫn chỉ đổi waypoint; chưa khóa ending.
- Cập nhật F6/F12 và PlayMode test theo flow mới, bỏ giả lập Tủ hồ sơ.

**Audit audio point cũ 2026-08-25 (đã chép lời trực tiếp từ MP3 và đối chiếu source):**

- Cue 07 nói bản đồ nằm trong `tủ hồ sơ` và chìa ở cạnh Radio, mâu thuẫn flow mới nên không dùng nguyên file trong bệnh viện. Chỉ câu cuối “bản liên lạc cuối cùng chưa được phát hết” còn hợp ngữ cảnh, nhưng cắt riêng sẽ làm thừa thoại trước đoạn Radio canonical.
- Cue 08 khẳng định căn cứ “vẫn hoạt động”, mâu thuẫn bí ẩn beacon tự động. Hai câu cảnh báo cổng/báo động vẫn đúng gameplay căn cứ nhưng đã trùng Cue 10–11; không ghép vào bệnh viện.
- Cue 09 đã được cắt thành `09_MilitaryRouteRevealed_Clean.mp3` dài `6,65s`; file gốc `9,884s` được giữ nguyên. Faster Whisper chỉ nhận phần thoại cốt truyện trong bản clean, không còn watermark. Cue này phát **sau** reward/map reveal/cinematic và trước bảng chọn lần hai, không chen giữa bản ghi Radio.
- Cue 05–08 cũ không được phát. Bốn resource path mới `05_HospitalRadioLead`, `06_HospitalEmergencyCall`, `07_MilitaryQuarantineReply`, `08_HospitalOperatorFinal` hiện cố ý chưa có clip. Cue05 hiện subtitle; Cue06–08 phát radio static + subtitle canonical. Đây là bốn voice cần thành viên thu lại đúng nguyên văn trong `RouteBAudioContent.cs`.

**Bài test bàn giao H3:**

- Đổi người vận hành giữa chừng không mất tiến độ.
- Cue không phát trùng và không ép người ở xa dừng gameplay.
- Late join có đúng transcript, map fragment và stage.
- Marker bệnh viện bị gỡ, marker căn cứ xuất hiện, minimap vẫn tắt.

## H4 — Cao trào và kể chuyện môi trường

**Trạng thái 2026-08-25: ĐÃ TRIỂN KHAI CODE + SCENE + UNITY QA TỰ ĐỘNG; CHỜ TEST TAY.**

Mục tiêu: thêm căng thẳng mà không biến bệnh viện thành horde bắt buộc.

- Đã bố trí bốn xác kể chuyện làm breadcrumb tới Trạm Radio. Chúng chỉ có Transform + SpriteRenderer, không collider, AI, network, interaction hoặc loot.
- UI Radio có ba vạch vàng. Mốc 1 và 2 tự dừng thao tác, phát nhiễu và nhả operator.
- Tại mỗi mốc, State Authority sinh tại cả A/B theo độ khó: Dễ 3, Thường 4, Hardcore 5 zombie mỗi điểm; từng cặp A/B cách nhau `0,25 giây`, trải đều trái/phải `0,8` world-unit.
- Nhiễu Radio lặp hai chu kỳ, dài khoảng `2,7 giây`. Mốc 3 hoàn tất bản ghi/reward và không tạo thêm zombie; tổng H4 là `12/16/20`.
- Không khóa cửa, không ép tập hợp toàn đội và không kill gate: Player có thể giữ E lại ngay để tiếp tục dù zombie còn sống.
- F6 tại `RadioReady` nay tiến từng chặng, nên cần ba lần để hoàn tất H3/H4 debug flow.

Kết quả H5 chốt phiên: compile sạch; toàn bộ EditMode `96/96`; hai PlayMode trọng tâm scene/regression `2/2`. Scene test xác nhận 6 stable KeyLoot ID và đúng 10 Polygon riêng; regression Easy xác nhận key chưa được cấp sau ShiftLog2, selected ID hợp lệ, chỉ bước loot mới có shared key, spawn counter `6 → 12` và chặng 3 mới hoàn tất Radio. Ghi chú finale mới: test xe cảnh sát đã được viết lại để dùng `Car` tại đúng vị trí author, không còn phụ thuộc `ViTriXeTest`/`VungKiemTraXeCanhSat`; xem `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` để biết trạng thái QA hiện tại.

**Bài test bàn giao H4:**

- Solo thấy thao tác tự dừng ở đúng hai mốc và có thể ngừng để chiến đấu rồi tiếp tục.
- Mỗi mốc đầu sinh đúng số theo độ khó tại A và B, nhịp 0,25 giây; mốc cuối không sinh thêm.
- Có thể lập tức tiếp tục sửa khi zombie còn sống; không có objective/door/interaction nào đợi kill.
- Co-op cho phép một người vận hành và đồng đội phòng thủ.
- Người ở xa không làm tăng sai số zombie.
- Kết thúc Radio không để lại spawner/event chạy lặp.

## H5 — QA toàn tuyến và tài liệu

**Trạng thái: IMPLEMENTATION COMPLETE; cần test tay hai máy theo checklist bàn giao.**

- Polygon: 10 vùng độc lập, cùng một phép kiểm tra cho client và State Authority.
- Multiplayer state: selected KeyLoot ID, shared key, hospital stage, Radio checkpoint và threat counter đều là Fusion `[Networked]`; client chỉ gửi RPC request, Host tái xác thực stable ID + Polygon + Player sống.
- Late join nhận cùng selected ID/stage/progress qua replicated state và bridge snapshot; wave chỉ do State Authority kích hoạt một lần/checkpoint.
- Regression tự động đi MainMenu → random key → Radio → Ending B không cần LootContainer.
- Test tay Host/Client hai máy vẫn là acceptance cuối cho cảm giác waypoint, disconnect thật và âm lượng không gian.

## Backlog sau bệnh viện — cập nhật hồi sinh quân sự 2026-08-26

1. Finale đã chốt và tích hợp minigame năm hạng mục trực tiếp trên `Car`; ba vật phẩm/Generator prototype đã loại.
2. Hệ thống hồi sinh đội quân sự đã triển khai: checkpoint quanh `Car` lưu khi vote chốt; Multi khóa mode lúc siege bắt đầu, dùng chung 3 lượt tự động sau 10 giây và giữ nguyên inventory/hotbar; Solo chết một lần là thua. Cổng Solo chỉ bắt đầu DPS 3 phút khi zombie đánh cổng lần đầu. Xem addendum trong `README_MAINPLAY_CODEX_HANDOFF.md`.
3. QA tay Solo toàn flow: 3 manh mối, roof-exit, vote, cinematic, chờ zombie chạm cổng rồi đo 3 phút DPS, chết một lần → Failed ngay.
4. QA tay Host + Client: vote nhất trí/từ chối/tương tác lại, disconnect, cinematic đồng bộ; riêng hồi sinh: chết 10s tự sống tại xe với đúng inventory/hotbar, đủ 3 lượt thì hết, cả đội chết cùng lúc → Failed, disconnect người đang chờ hồi sinh không kẹt state.
5. QA tải horde thực tế ở ngưỡng Solo `24` và Multiplayer `50`, rồi tinh chỉnh nếu FPS tụt.
6. Animation đi/chạy của bản sao Host đã dùng Animator và tốc độ thật; cần QA tay cảm giác chuyển động, camera và nhịp dựng cảnh.
7. Regression cũ còn fail tự động: assertion trả Player từ ranh giới khu dân cư (`SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap`); chưa thuộc phạm vi finale.

## Loot sửa `Car` cảnh sát — ĐÃ TRIỂN KHAI 2026-08-26

Implementation thử nghiệm của Ox Alpha vẫn bị loại hoàn toàn; bản hiện tại được làm sạch từ checkpoint an toàn `f2551d1cb`. Bản chính thức giữ ba nguyên tắc: luôn bảo đảm đủ năm vật phẩm, State Authority quyết định loot, và tái sử dụng UI/giao dịch của `LootContainer` hiện có.

1. Tạo prefab loot Route B chính thức, được Fusion đăng ký và hoạt động trong cả Editor lẫn standalone build; không dùng `UnityEditor.AssetDatabase` làm fallback runtime.
2. Vị trí thùng dùng marker được author trong `Main.unity` và kiểm tra collider/lối đi rõ ràng; không tự đoán bằng vòng tròn quanh `Car`/cổng.
3. Thùng chỉ khả dụng từ `SiegeAndRepair`, dùng hình/UX loot hiện có và đỏ toàn bộ sprite khi Player hợp lệ đến gần; Host và Client phải thấy giống nhau.
4. State Authority random phân phối nhưng tổng loot của match luôn có đúng bộ tối thiểu: Toolbox, Hammer, FuelCan, Battery và Tire từ `PoliceCarItemCatalog`. Không dùng item Tuyến A và không để RNG tạo soft-lock.
5. Súng/đạn là bonus riêng, chỉ chọn ID thật đang tồn tại (`AK47`, `S12K`, `Ammo762`, `Ammo12Gauge`); bonus không được chiếm chỗ hoặc thay thế năm item bắt buộc.
6. Server xác thực PlayerRef, phase, khoảng cách, vật cản, inventory và trạng thái slot ở cả open/take/store; hai Player tranh cùng item không được duplicate. Late join phải nhận đúng nội dung còn lại.
7. Nếu không tìm đủ marker/prefab/slot hợp lệ, hệ thống phải fail có kiểm soát và retry/ghi lỗi rõ ràng; không throw giữa flow, không đánh dấu setup hoàn tất trước khi spawn đủ năm món.
8. Quality gate trước khi giao test tay: build các assembly `0 error`; EditMode rules; PlayMode spawn prefab + đủ năm item + inventory đầy + double-claim; smoke MainMenu → cinematic → siege; cuối cùng mới QA Host/Client hai máy.

### Kết quả triển khai và QA tự động

- Có prefab chính thức `Resources/NetworkPrefabs/MilitaryRepairLootContainer.prefab`, mang `NetworkObject`, nhãn `FusionPrefab`, không có fallback `AssetDatabase` runtime.
- `Main.unity` có đúng năm `MilitaryRepairLootMarker` ID `1..5`; authority sinh đúng năm tủ khi chuyển sang `SiegeAndRepair`. Thiếu/sai marker hoặc prefab sẽ log lỗi, rollback phần đã sinh và retry sau 2 giây.
- Mỗi match xáo trộn có seed nhưng luôn đủ Toolbox, Hammer, FuelCan, Battery, Tire; mỗi tủ có thêm một cặp AK47 + Ammo762 hoặc S12K + Ammo12Gauge. Đạn có thể tách nhiều stack theo `maxStack` của ItemData.
- Open/take/store dùng validation authority hiện có cho PlayerRef, phase, khoảng cách, vật cản, inventory và slot. Túi đầy không làm mất món; request cũ sau lần lấy đầu bị từ chối nên không duplicate.
- `Assembly-CSharp.csproj`: build `0 error` (6 warning hiện hữu). EditMode loot rules: `4/4` pass; bộ EditMode liên quan đã chạy trước đó: `12/12` pass. PlayMode full Route B: `1/1` pass trong `35,34s`; authored police car preservation: `1/1` pass trong `7,39s`.
- Chưa chốt acceptance cuối: QA tay vị trí/collider/lối đi của năm marker và QA Host + Client hai máy (đồng bộ highlight, tranh cùng item, late join) vẫn phải thực hiện trong Unity/build thật.
- Hotfix UI sau cinematic: cue `SiegeStarted` không còn khởi động giữa lúc cinematic đang disable `AutoCanvas`; cue được phát sau khi camera/UI gameplay đã restore để radio không snapshot rồi khôi phục canvas về trạng thái tắt. Regression full flow đã bổ sung assertion mở inventory và mở tủ qua local interaction path sau cinematic.
- Hotfix hoàn chỉnh UI: chuỗi reward/map/chọn tuyến bị trễ nay tự hủy nếu một ending đã khóa; radio reconcile `AutoCanvas` theo modal state hiện tại. Không còn map cũ âm thầm giữ `questOverlayOpen=true` sau cinematic.
- Hotfix zombie công thành: `SiegeZombieObjective` đọc trạng thái chết của cả ba implementation zombie, dừng vận tốc/attack, không trừ máu cổng nữa, không bật lại AI khi thả horde và rời danh sách objective. `ZombieCorpseLoot` vẫn giữ nguyên quyền quản lý xác/loot/despawn.
- Unity PlayMode full Route B sau hai hotfix: `1/1` pass trong `41,40s`; test hạ thật một zombie siege và xác nhận dead state bền, objective tắt, xác đứng yên/có corpse loot, sau đó xác nhận Tab và tủ loot mở được sau cinematic.

### Hotfix finale sau test tay — 2026-08-26

- Zombie áo vàng (`ZombieKhoaRebuilt`) không còn đứng dậy: gate objective retire nhưng giữ AI component ở trạng thái dead-only để `Render()` tiếp tục khóa collider và Animator `IsDead`. Mỗi batch nay luân phiên đủ hai prefab, tránh bỏ sót một biến thể.
- Ngay lúc vote chốt và cinematic bắt đầu, authority đặt giờ `16:00`, hủy sleep transition, khóa cứng thời gian, ép mọi Player luôn rested và ẩn cả panel đồng hồ góc màn hình cho tới hết finale.
- Vỡ cổng không dừng horde: zombie đang có được bật native AI và nhận Player sống gần nhất làm target; các batch sau vẫn spawn từ bốn marker và sinh ra ở trạng thái truy đuổi Player ngay.
- Sửa xe chỉ bị hủy bởi `isZombieAttack=true`; đói, khát, bleeding và các nguồn damage-over-time khác không còn hủy phiên sửa.
- Còi xe tiếp tục loop sau cinematic ở `20%` âm lượng ban đầu và chỉ dừng khi đủ `5/5` hạng mục sửa.
- QA: `Assembly-CSharp` build `0 error`; assembly PlayMode/EditMode build `0 error`; rule ngắt sửa EditMode `1/1`; full Route B PlayMode `1/1` pass trong `37,27s`, có assertion riêng cho 16:00/time lock/clock, còi 20%, zombie áo vàng chết bền, chuyển target khi cổng vỡ, spawn hậu-vỡ-cổng và còi dừng sau sửa xong.

## Prompt mở chat triển khai

> Đọc `ROUTE_B_COMPLETE_FLOW_CODEX_HANDOFF.md` B4–B7 và các file finale trong mục 10. Finale đã triển khai theo flow 3 manh mối → rời mái trường → vote nhất trí tại `Car` → cinematic đóng cổng → horde + sửa 5 hạng mục; hồi sinh đội theo luật 10s/3 lượt dùng chung/Solo chết là thua. Không khôi phục Generator/150% HP/electric stun.

## Đính chính kế hoạch hiện tại — 2026-08-29

- H1–H5 và Route B finale không còn là task triển khai tiếp theo; final polish đã nằm trong `main` qua PR #323.
- Các mô tả năm prefab loot runtime, extraction bằng `E`, 13 lane zombie và cutscene 2,25 giây ở phần lịch sử phía trên đã bị implementation mới thay thế; xem snapshot đầu `CODEX_PROJECT_WORK_LOG.md`.
- Ưu tiên QA còn mở hiện nay là multiplayer thật: lobby 5–10 người, readiness 6m, disconnect/death race lúc khởi động xe, virtual follower, horde 80–112 bằng Profiler và corpse-loot targeted RPC trên nhiều peer.
- Các feature mới sau Route B gồm loading/readiness tập trung, difficulty Host-authoritative, localization song ngữ, corpse loot, balo/capacity và private RPC audit. Chúng được ghi ở work log, không nhân bản đầy đủ vào kế hoạch lịch sử này.
