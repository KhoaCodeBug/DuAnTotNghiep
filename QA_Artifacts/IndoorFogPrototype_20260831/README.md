# Fog indoor — prototype một nhà, 2026-08-31

## Trạng thái

User đã duyệt ảnh concept với hai chỉnh sửa: phần tường phải tối sớm hơn ở rìa bên phải, và ảnh không đèn là mức buổi tối, ban ngày chỉ sáng nhẹ hơn. Prototype đã triển khai và kiểm tra Solo. **Chưa nghiệm thu bằng test tay, chưa bật mặc định trong Main, chưa sẵn sàng nhân rộng toàn map.**

- Checkpoint an toàn: `ddf440424`, nhánh `codex/checkpoint-vision-menu-stable-20260831`.
- Nhánh làm việc: `codex/restore-indoor-vision-20260831`; thay đổi local, không commit/push/merge.
- Nhà mẫu: `==========Map========== /Map/nhachinhxaydautien (12)`; điểm chụp Player `(-39.2, 44.3)`, nhìn lên `(0,1)`; zoom 3, camera QA offset lên 1.2.
- Không đổi scene/prefab, PlayerVision, collider, AI, inventory, quest, menu hoặc RPC/state Fusion. Component chỉ được gắn vào nhà mẫu trong Play bởi công cụ QA.

## Cách hoạt động và phạm vi sửa

- `IndoorFogSurfaceMap.cs`: opt-in trên root nhà, dùng sprite alpha và hình chân từ physics shape để tạo atlas liên hệ pixel mặt tường/decor với vị trí dưới chân; giữ thứ tự layer/order/Y như tilemap Individual hiện tại. Tạo một lần, tái sử dụng khi quay, đổi giờ/đèn và đi ra-vào; bỏ GPU resource khi disable/thoát Play.
- `IndoorFogSurfaceAtlas.shader`: chỉ dùng để tạo atlas; không sửa texture hoặc material của asset gốc.
- `FogVisionController.cs` và `FogVisionOverlay.shader`: dùng atlas chỉ khi active indoor collider đúng component cấu hình; giữ nhánh cũ cho ngoài trời và nhà chưa opt-in. Giữ ánh sáng thật từ Light2D và clock Fusion. Không gửi atlas qua mạng.
- Vòng 1 hiện được tường/tủ nhưng có vệt tối ở mặt tường phía trước. Vòng 2 giữ phần đã thấy ở mask cũ để sửa các vệt mới này. Vòng 3 cân lại sáng nền ngày/đêm và giảm lan ở rìa cone. Các thay đổi tham số trong vòng 3 không phải renderer mới.
- Opacity nền hiện tại day=0.86/night=0.15, lit=0.08, coneInset=0.06. Đây là lượng phủ Fog, không phải cường độ đèn; ban ngày Global Light mạnh hơn nhiều nên cần phủ mạnh hơn để giữ sáng nền trong nhà vừa phải.

## Bằng chứng hình ảnh thật

Các file `v3-*.png` do ScreenCapture trong Unity Play tạo, không phải AI. Mỗi ảnh có file `-state.txt` kèm vị trí, collider, flashlight, mask và thông số light tại lúc chụp.

- `v3-baseline-day-on.png`: baseline, prototype tắt.
- `v3-day-on.png`: bật đèn, 13:30.
- `v3-day-off.png`: tắt đèn, 13:30.
- `v3-night-off.png`: tắt đèn, 21:00.
- `v3-night-on.png`: bật đèn, 21:00.
- `v3-look-left.png`, `v3-look-right.png`: hướng khác.
- `v3-outdoor.png`, `v3-other-house.png`, `v3-return.png`, `v3-disabled.png`: kiểm tra phạm vi và vòng đời.

Ảnh concept/reference/ảnh user đánh dấu đã duyệt nằm ở `../IndoorFogConcept_20260831/`. Nhà mẫu cùng kiểu bố cục nhưng không dùng ảnh sinh AI để thay asset hoặc thay chi tiết nhà.

**Tự đánh giá:** mặt tường, tranh và thân tủ phía trước hiện rõ hơn; vùng tường bên phải bị che sớm hơn concept đầu. Chuyển cường độ theo hướng đèn mềm hơn. Tuy nhiên ranh ở một số góc khuất còn cứng, mức tối ban đêm vẫn cần user đánh giá trên màn hình thật. Chưa tuyên bố khớp hoàn toàn ảnh mục tiêu. Không tiếp tục mở rộng kiến trúc chỉ để đuổi theo ảnh AI.

## Kiểm tra đã chạy

- Compile/Console không có lỗi sau sửa. Lượt test đầu phát hiện shader thiếu khai báo texture property cho truy vấn material; đã bổ sung property, không bỏ qua lỗi test.
- EditMode cuối: **44/44 pass**, job `c94909a3845941ada8511fc15c5c3b51`, 29.053822s. Gồm 43 readiness/chat/regression và 1 integration thực sự EnterPlayMode từ MainMenu → Solo Medium → 11 trạng thái.
- Integration kiểm tra active mask/flashlight, GPU atlas có surface và vùng trong suốt, không rebuild khi đổi hướng/giờ/đèn hoặc ra-vào, release khi disable, không sót RenderTexture sau ExitPlayMode. Không suy ra toàn bộ quy tắc che khuất đúng từ các assert này.
- PlayMode visibility/zombie: **5/5 pass**, job `81f024de3c7249bda21a3f067bce126a`, 21.9985892s. Có runtime hospital/school/outdoor và các ca zombie visibility. Không gọi đây là full suite toàn dự án.
- Sau test cuối chỉ chỉnh default của DTO công cụ QA cho đúng nhà mẫu và guard ReturnManualControl; đã compile và sử dụng lại công cụ ở runtime để đo cuối.
- Chưa Host/Client, late join/reconnect, player build, test camera độ phân giải lớn, cửa/đồ di chuyển hoặc soak nhiều zombie.

## Đo hiệu năng cuối

A/B/A trên cùng lượt Solo, vị trí/hướng/13:30/đèn pin bật, Game View 987×568, zoom 3. Mỗi lượt bỏ 30 frame đầu, lấy 240 frame bằng ProfilerRecorder. A/A2 không gắn surface, B gắn surface; đây là so bật/tắt phần mở rộng trong shader mới, không phải binary benchmark cả checkpoint cũ. Dữ liệu gốc `final-*-profile.csv`, tổng hợp `final-profile-summary.json`.

| Median (ms) | A baseline | B prototype | A2 baseline |
|---|---:|---:|---:|
| CPU main thread | 17.6134 | 16.3789 | 19.0526 |
| GPU frame | 3.380224 | 3.456 | 3.227648 |
| FogVision.UpdateMaterial | 0.0528 | 0.0568 | 0.0680 |
| Delta frame | 19.2322 | 18.02611 | 20.3885 |

GPU B cao hơn A/A2 khoảng 0.08–0.23 ms trong mẫu này. Không kết luận CPU được cải thiện: baseline đã dao động mạnh (p95 CPU 38.8/49.3 ms), thế giới vẫn chạy và Editor có hoạt động nền. Không đảm bảo 60 FPS hoặc suy rộng lên độ phân giải ảnh concept. Marker Fog p95 khoảng 0.805–1.236 ms, gồm fan raycast đang có. Atlas 67 surface, build CPU được ghi khoảng 2.17–10.43 ms giữa các lượt; khoản đầu vào nhà này bị loại khỏi cửa sổ steady-state nên không được giấu khi báo chi phí. Chưa đo GPU bake riêng hoặc cold load ở build.

## Giới hạn phải giữ trước khi mở rộng

1. Atlas tĩnh: chưa có cơ chế invalidation cho tile/sprite đổi hình, màu alpha hoặc di chuyển. Không tự áp dụng cho cửa động/vật thể chuyển động.
2. Projection dựa physics shape/cell anchor là xấp xỉ, chưa phải depth/height map chính xác cho mọi asset. Chưa kiểm tra mọi loại tường hoặc Tilemap chunk/atlas packing/rotation.
3. Giữ visibility cũ để tránh tạo vệt mới đồng nghĩa không sửa hết mọi sai lệch hình học có sẵn. Mask nền tĩnh chưa phân biệt vật thể động đè lên từng pixel; PlayerVision/zombie LOS vẫn giữ cơ chế cũ, nhưng chưa kiểm tra multiplayer visual occlusion đầy đủ.
4. Shader được tìm trong Editor; chưa xác nhận inclusion/stripping trong player build. Không ship prototype chỉ dựa vào test Editor.
5. Ảnh tĩnh không chứng minh mượt khi chạy và quay nhanh. Các số FPS trên ảnh không dùng thay cho Profiler.

## Test tay / tiếp nối

Trong lượt bàn giao, nếu Unity đang Play tại nhà mẫu thì chỉ cần tiếp tục điều khiển sau menu **Tools → QA → Indoor Fog → Return Manual Control** (đã gọi trước bàn giao nếu ghi trong work log).

Nếu đã Stop/restart:

1. Mở `MainMenu`, Play, vào Solo Medium bình thường. Hoặc dùng menu **Tools → QA → Indoor Fog → Start Solo Automation** sau khi đã Play.
2. `pose.json` trong thư mục này phải chọn house `nhachinhxaydautien (12)`, x=-39.2/y=44.3, prototype=true; giờ/hướng/đèn có thể thay trong file cho ảnh đối chiếu.
3. Chọn **Apply Runtime Pose**. Đợi một giây, chọn lại một lần nếu vừa mới đưa flashlight lên hotbar. Đây là fixture đứng yên, nên đang khóa movement và clock để chụp.
4. Chọn **Return Manual Control** để trả lại movement, clock speed và camera offset trước fixture. Prototype chỉ còn ở nhà mẫu trong lượt Play này; Stop sẽ bỏ component runtime đó.
5. Đứng nhìn tranh/tường sau và tủ bên phải; bật/tắt đèn, quay trái/phải và chạy gần góc. Mong đợi mặt trên không bị cắt tối ngang chân, không còn vệt dọc mới; xem kỹ chỗ ranh khuất còn cứng.
6. Đi ra ngoài và sang nhà khác: hiệu ứng mới phải tắt ở đó, không mang wall-mask mới ra map. Quay lại nhà mẫu: bật lại, không tạo atlas lần nữa trong cùng lượt Play.
7. So OFF 13:30 với 21:00 bằng `pose.json` + **Apply Runtime Pose**, rồi **Capture Game View**. Ánh sáng nền ban ngày sáng hơn ban đêm, cả hai thấp hơn bật đèn. Duyệt cảm giác bằng mắt, không chỉ dựa test pass.

## Quyết định tiếp theo

Giữ prototype để user test và phản hồi; chưa tích hợp mặc định. Nếu mép góc/đồ động đòi thêm nhiều ngoại lệ hoặc sửa renderer lớn thì dừng thử nghiệm và bảo toàn checkpoint. Không tự mở rộng scope.

Nếu rollback: trước hết lưu bản thử; chỉ phục hồi hai file runtime controller/shader về checkpoint và cất bốn file mới (component, atlas shader, QA helper, integration test) cùng meta ra ngoài Assets. Không reset/clean toàn repo và không làm mất work log/ảnh/user changes.

## V2 — ổn định gần tường và làm mềm bề mặt (2026-08-31)

User review V1 mở hai lỗi: biên sáng/tối trên tường còn cứng và có vệt đen nhấp nháy khi đi sát tường. V2 giữ nguyên phạm vi một nhà mẫu, không sửa scene/prefab và không mở sang nhà khác.

### Chẩn đoán và thay đổi

- Probe 180 bước gồm tiến về tường và đi song song mép tường xác nhận các ray kề nhau có thể chênh khoảng 15–17 world unit tại góc/cửa. Nội suy trực tiếp khoảng cách giữa một ray gần và một ray xa tạo tam giác tối dài.
- Dữ liệu khoảng cách thuộc lần physics scan trước nhưng shader từng đo từ vị trí Player hiện tại giữa hai scan. V2 gửi kèm `lastOcclusionOrigin` để mỗi mảng khoảng cách luôn được dùng với đúng gốc quét.
- Atlas projection đã dump và kiểm tra; atlas liên tục, không tạo các sọc giống ảnh lỗi. Không sửa atlas hoặc collider để chữa sai nguyên nhân.
- Hai thử nghiệm đầu (nội suy visibility hai ray và cho phép một ray kề) vẫn tạo sọc góc trên tường nên bị loại. Candidate cuối chỉ tái dựng visibility **trên bề mặt tường/decor opt-in** bằng cubic B-spline bốn ray không âm. Sàn, actor và logic phòng sau tường vẫn dùng nội suy khoảng cách nghiêm ngặt cũ.

### Bằng chứng thị giác V2

- `V2_Diagnostic_Baseline/`: probe trước sửa, có tam giác/sọc thay đổi theo ray.
- `V2_Diagnostic_Step1/`, `V2_Diagnostic_Step2/`: hai hướng bị loại vì còn sọc.
- `V2_Diagnostic_Step3/`: candidate B-spline theo các mốc 15 frame.
- `V2_Diagnostic_FinalBurst/`: 30 ảnh liên tiếp `burst-090` đến `burst-119`, cộng CSV 180 bước và atlas diagnostic. Chuỗi này không còn nan đen bật liên tục như ảnh V1. FPS trong ảnh bị ảnh hưởng mạnh bởi ScreenCapture và không dùng làm benchmark.
- `v2-final-day-on/off.png`, `v2-final-night-on/off.png` cùng file state: bốn pose cố định. Ban ngày không đèn sáng nền hơn ban đêm; bật đèn làm rõ tường/tranh/tủ; phòng sau tường vẫn tối; tường phải tối sớm hơn concept đầu.

Tự đánh giá: V2 gần concept hơn rõ rệt và loại được artifact V1 trong đường chạy đã tái hiện. Cạnh bóng tại đúng đường che của tường trung tâm vẫn rõ; không blur thêm vì phép blur rộng có thể làm lộ phòng sau. Đây là giới hạn hình học có chủ ý của V2 và cần user test tay khi chạy thật.

### Cổng kiểm thử V2

- Console sau test, capture và profile: **0 error**.
- Integration căn nhà mẫu: **1/1 pass**, job `75cc0c4b5a0a421b932d50822efda2c0`, 31.772s.
- EditMode readiness/chat + integration: **44/44 pass**, job `6c0247d57ee948328ad7372d81e14a50`, 30.268s.
- PlayMode visibility/zombie: **5/5 pass**, job `8d10a50c13f74cb7b47e9ccd55a0d14b`, 21.854s.
- Chưa chạy full suite, Host+Client, build hoặc soak. Automated pass không thay user acceptance.

### Hiệu năng V2

A/B/A cùng Solo, `(-39.2,44.3)`, 13:30, nhìn lên, đèn bật, zoom 3; bỏ 30 frame đầu và lấy 240 frame mỗi lượt. Dữ liệu `v2-perf-*-profile.csv`, tổng hợp `v2-final-profile-summary.json`.

| Median (ms) | A baseline | B V2 | A2 baseline |
|---|---:|---:|---:|
| CPU main | 16.9778 | 16.8433 | 16.9434 |
| GPU frame | 3.244032 | 3.488768 | 3.227648 |
| FogVision.UpdateMaterial | 0.0448 | 0.0519 | 0.04585 |
| Delta frame | 18.444505 | 18.34355 | 18.4639 |

GPU B cao hơn A/A2 khoảng **0.245–0.261 ms**; draw call và batch giữ 454. CPU chênh khoảng -0.10 đến -0.13 ms nhưng không xem là cải thiện vì nền Editor dao động. Chưa suy rộng sang player build hoặc độ phân giải khác.

### Test tay V2 và kết quả mong đợi

1. Từ vị trí nhà mẫu, đi tiến sát tường trước rồi chạy dọc hai hướng của mép tường. Mong đợi không xuất hiện nan/vệt đen bật ra và không nhấp nháy liên tục.
2. Quay trái/phải, bật/tắt đèn. Tường, tranh, kệ và tủ trong hướng nhìn đổi sáng liên tục; sàn/phòng/vật sau tường không bị lộ.
3. So ban ngày và ban đêm khi tắt đèn. Ban ngày có sáng môi trường nhẹ; ban đêm tối hơn rõ. Khi bật đèn, đèn pin vẫn là nguồn nổi bật.
4. Đi ra ngoài hoặc sang nhà khác. Surface V2 phải tắt; cơ chế Fog ngoài map không đổi. Quay lại nhà mẫu phải bật lại mà không rebuild atlas trong cùng lượt Play.
5. Nếu vẫn thấy nhấp nháy, ghi vị trí, hướng nhìn, trạng thái đèn và giờ; chụp hoặc quay đoạn ngắn. Không xem automated test pass là nghiệm thu lỗi thị giác này.

V2 dừng tại đây để user test. Không tự bắt đầu V3 trước phản hồi.
