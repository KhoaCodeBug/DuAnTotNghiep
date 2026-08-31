# Flashlight gradient — 2026-08-31

User sau đó đã duyệt hướng tối sớm từ phía sáng và yêu cầu test nhiều góc/đánh giá khắt khe. Kế hoạch được duyệt ở BOUNDARY_FADE_NEXT_ITERATION.md; bản cone-only bên dưới vẫn CHƯA ĐẠT. Chưa triển khai hướng mới trong task đóng gói.

## User review mới nhất — CHƯA ĐẠT

User đã test tay và báo không thấy cải thiện rõ; dải sáng mạnh → vừa → mờ → tối tại ranh phải vẫn chưa có như concept. Ảnh giữ ở `Gradient_UserReview_20260831/`: runtime gốc, runtime khoanh ranh, concept khoanh vùng mong muốn. Đánh giá cải thiện ở các mục cũ bên dưới quá lạc quan và không chứng minh đạt vùng user quan tâm; không dùng 44/44 hoặc 5/5 pass để bác phản hồi này.

Lượt tiếp nhận chỉ đọc code/đánh giá/lưu tài liệu, chưa sửa gameplay hoặc chạy Play. Code xác nhận thay đổi trước làm mềm theo `angleDot`, trong khi cuối nhánh indoor vẫn có `visible = insideIndoor * indoorOcclusionVisibility` và `max(opacity, (1 - indoorOcclusionVisibility) * _IndoorWallOccludedOpacity)` (0.98). Đây là cơ chế có thể cắt lại dải sáng ở ranh occlusion; đóng góp chính xác của Fog so ShadowCaster2D tại pixel khoanh còn cần tách lớp runtime, chưa khẳng định chỉ một nguồn.

Hướng đề xuất để thảo luận: tạo dải giảm sáng theo khoảng cách tới ranh hiển thị cuối cùng, bắt đầu tối từ phía đang sáng và khớp mức tối tại ranh; giữ vùng khuất không sáng thêm. Cần áp dụng sau khi xét blocker/foot projection, không tiếp tục chỉ tăng cone feather. Nếu thử tiếp, chỉ một nhà + torch, có ảnh cắt đúng vùng khoanh trước/sau/cùng zoom và kiểm tra rò sáng/flicker/hiệu năng. Mask độ phân giải thấp hoặc khoảng cách tới biên là hướng kỹ thuật dự kiến, CHƯA triển khai/chốt. Không tự thêm blur toàn màn hình, mở nhà khác, normal-vision gradient hoặc refactor lớn. Cân nhắc dừng và bảo toàn V2 nếu không có cải thiện rõ sau thử có giới hạn.

Quy tắc bàn giao test mới: bật God Mode bằng cơ chế có sẵn, tắt đèn pin để không hao pin, trả movement và xác minh trước khi báo sẵn sàng. Lần chuẩn bị vừa rồi đã mở Solo/teleport/OFF, nhưng thao tác God Mode chưa thành công; user báo không đi được do QA còn khóa, sau đó đã gọi Return Manual Control. Không được ghi God Mode đã bật. User đã test xong và yêu cầu dừng; Unity xác nhận đã Stop. Không tự mở Play trong lượt thảo luận này.

## Trạng thái mới nhất

Đã triển khai và kiểm thử, **chưa được user nghiệm thu bằng test tay**. Chỉ thử trong `nhachinhxaydautien (12)`, component được thêm runtime; chưa bật mặc định trong scene Main. Tầm nhìn thường, ambient ngày/đêm và blocker giữ nguyên. Viền đen nhỏ sát tường đã được user tạm chấp nhận ở V2, không xử lý thêm trong lượt này.

Save point user yêu cầu đã commit/push đầy đủ: `ee0554d149a15fd89b3a370fab974058f235e668`. Đã kiểm tra GitHub Desktop sau push: đúng repo/branch, No local changes. Sau đó remote thay đổi trong lúc làm: nhánh remote bị xóa và `main` đã có merge PR #326 tại `5aa09d71cea1d10852f2b29ca07ef60cd243b0ae` (14:55:34 +07). Đã xác minh save point là ancestor của main và hai tree không khác nhau. Agent không thực hiện merge đó. Branch local vẫn `codex/restore-indoor-vision-20260831`, upstream hiện `[gone]`; không tự tạo lại remote branch hoặc đổi checkout khi Unity đang Play.

**Code gradient, QA và tài liệu sau save point đang local, chưa commit/push.** Checkpoint cũ `ddf440424` còn nguyên. Nếu cần quay lại: lưu patch và toàn bộ file mới trước, rồi chỉ hoàn tác diff sau `ee0554d14` của các file gradient liên quan; không reset/clean toàn repo, không xóa tài liệu/bằng chứng.

## Nguyên nhân và thay đổi có giới hạn

1. Fog có dải cường độ cone hẹp, còn Light2D thật giữ vùng sáng đều rất rộng. Chỉ mở dải Fog chưa đủ làm sáng/tối chuyển mềm trong ảnh.
2. `IndoorFogSurfaceMap.flashlightConeFeather = 0.50` truyền qua `_IndoorSurfaceLighting.w`; shader tăng dải giảm cường độ **vào bên trong** vùng chiếu. Giữ nguyên rìa tối ngoài (`coneInset = 0.06`), không giãn visibility qua tường.
3. Trong `PlayerVision`, chỉ khi đèn pin bật và có surface map đúng active indoor collider, đồng bộ vùng lõi Light2D với dải Fog. Góc lõi đo thực tế khoảng 61.21°, góc ngoài/CurrentVisionAngle vẫn 145°. Khi tắt đèn trở về 100°/140°; nhà khác hoặc disable prototype giữ profile đèn pin 105°/145°.
4. Không thay raycast, scan origin, B-spline V2, atlas projection, scene/prefab/animation, actor visibility, quest, menu hay Fusion state/RPC. Không thêm blur toàn màn hình hoặc số mẫu shader. Mã diagnostic màu dùng khi điều tra đã gỡ.

QA helper nhận feather trong pose, ghi thêm góc Light2D, và lưu motion vào thư mục riêng để không đè bằng chứng V2. Integration kiểm tra cả góc Light2D lẫn CurrentVisionAngle và bật/tắt đúng phạm vi.

## Từng bước và tự đối chiếu ảnh thật

Tất cả ảnh `gradient-*.png` là ScreenCapture trong Unity; mỗi pose có `-state.txt`. Concept tham khảo ở `../IndoorFogConcept_20260831/`, ảnh user chê chuyển gắt ở `V2_UserReview_20260831/`.

| Bước | Bằng chứng | Đánh giá |
|---|---|---|
| Trước sửa | `gradient-before-day/night-on/off.png` | Mức nền và phạm vi V2 để giữ lại. |
| 1: shader | `gradient-step1-*`, `gradient-edge-before/step1`, `gradient-close-before/after` | Dải Fog rộng hơn nhưng đèn thật vẫn sáng đều quá rộng; thay đổi thị giác chưa đủ. Thử rộng 0.6 (`gradient-side-wide`) không giữ làm mặc định. |
| Chẩn đoán | `gradient-diagnostic.png` | RGB tách illumination/occlusion/indoor cho thấy tín hiệu illumination có ramp, cần đồng bộ Light2D. Đây là ảnh chẩn đoán, không phải kết quả cuối. |
| 2: shader + Light2D | `gradient-step2-day-edge.png`, `gradient-step2-night-edge.png`, `gradient-step2-day-up.png` | Chuyển sáng → vừa → mờ rõ hơn trên sàn và mặt đồ; phòng sau tường vẫn bị che. Cải thiện ban đêm dễ thấy hơn. |
| Bộ cuối | `gradient-final-day-on/off.png`, `gradient-final-night-on/off.png` | Tắt đèn giữ mức nền; ngày sáng hơn đêm. Bật đèn giữ mặt tường/tranh/tủ sáng và rìa phải không lan thêm. |

So trực tiếp `gradient-close-before.png` với `gradient-step2-day-edge.png` tại `(-39.8,44.9)`, hướng `(1,0.3)`, zoom 2.3: dải xám trung gian trên sàn rộng hơn; không còn giữ gần nguyên cường độ đến sát rìa. **Mức cải thiện ban ngày vừa phải, vẫn nhìn ra ranh vùng chiếu; không tuyên bố khớp hoàn toàn concept.** Cạnh tại vật cản và biên tầm nhìn thường có thể vẫn rõ. Không lấy lý do blocker để tự coi yêu cầu gradient đã pass; cần user đánh giá cảm giác thật.

Motion `FlashlightGradient_Motion/`: CSV đủ 180 bước, mask/surface = 1/1 toàn bộ, 41 ảnh gồm burst liên tiếp 090–119 (các mốc trùng capture dùng cùng frame). Đã xem các frame 090/095/100/110/119: không thấy nan đen lớn bật lại; bóng thay đổi theo vị trí. Đây là đường teleport QA khi tiến/sang ngang, không thay cho WASD, xoay chuột nhanh hoặc soak. Màu đỏ tủ do highlight gameplay trong phiên chụp; không dùng nó làm bằng chứng màu ánh sáng. FPS lúc chụp liên tục bị ảnh hưởng bởi ScreenCapture, không dùng để đo hiệu năng.

## Xác minh

- Unity compile và Console cuối: không lỗi.
- EditMode readiness/chat + integration: **44/44 pass**, 29.5664321s, 07:55:54–07:56:24 UTC; nguồn XML Unity tại `gradient-editmode-results.xml`. Không dựa vào kết quả V2 cũ.
- PlayMode visibility/zombie: **5/5 pass**, 21.7041253s; job `6bb84f08f8874923af1306c0d5377f9a`, `gradient-playmode-results.json`.
- PlayMode tạo lại 12 ảnh ngoài trời/bệnh viện/trường học vào đường dẫn evidence cũ. Đã cất bộ mới vào `Gradient_WorldRegression/` rồi phục hồi riêng 12 ảnh checkpoint tại `VisionRollback_Runtime/`, tránh đè lịch sử; không phục hồi file gameplay/user khác.
- Integration real Solo có 11 trạng thái: baseline, ngày/đêm ON/OFF, trái/phải, ngoài nhà, nhà khác, quay lại, disable. Góc ngoài và góc gameplay giữ nguyên; lõi mới chỉ trong nhà mẫu + torch. Ảnh/state có prefix `gradient-regression-`.
- Đầu lượt có một Solo load timeout khi Desktop/MCP tab ở foreground, trước khi sửa code. Sau chuyển về Game View và khởi động phiên mới, các lượt Solo/integration sau thành công. Chưa đủ bằng chứng kết luận nguyên nhân timeout; không sửa loading/readiness trong task này.
- `git diff --check` pass; không có diff scene/prefab/anim/controller. Chưa full suite, Host+Client, build, late join/reconnect hoặc soak; atlas động vẫn là giới hạn cũ.

## Hiệu năng

Cùng Solo, nhà mẫu, `(-39.2,44.3)`, hướng lên, 13:30, đèn bật, zoom 3, Game View 987×568. Mỗi lượt bỏ 30 frame rồi đo 240 frame, không screenshot trong phép đo. A/A2 feather 0.2 và lõi105°, B feather0.5 và lõi61.21°. CSV `gradient-perf-*-profile.csv`, tổng hợp `gradient-profile-summary.json`.

| Median ms | A | B | A2 |
|---|---:|---:|---:|
| CPU main | 19.81955 | 18.97965 | 18.93215 |
| GPU frame | 3.474432 | 3.473408 | 3.472384 |
| FogVision.UpdateMaterial | 0.06095 | 0.05915 | 0.05875 |

Draw/batch giữ 454. GPU chênh khoảng ±0.001024ms, chưa thấy chi phí tăng đáng kể trong phép đo này. CPU dao động, không gọi là cải thiện. **A và B đều chạy mã PlayerVision mới**, nên đây là so hai profile trong cùng binary, không đo chính xác tổng CPU overhead so commit V2. Không cam kết 60 FPS hoặc suy rộng sang build/độ phân giải cao.

## Test tay và kết quả mong đợi

Player được đặt lại ở `(-39.2,44.3)`, HP100, sống, 13:30, đèn bật, house `nhachinhxaydautien (12)`, 67 surfaces, mask/surface/torch = 1/1/1. Ảnh `gradient-manual-ready.png`. Sau Capture dùng `Tools > QA > Indoor Fog > Return Manual Control` để trả movement/camera/clock.

1. Đứng trong phòng, xoay đèn từ trái sang phải, nhìn dải đi qua sàn, tranh và tủ: sáng mạnh giảm dần qua mức vừa/mờ rồi tối; không chỉ làm mờ cạnh gạch. Rìa phải không được lan thêm vào phòng khuất.
2. Đi về phía TV rồi nhìn chéo phải để biên đèn cắt qua giữa sàn; so cảm giác với ảnh `gradient-step2-day-edge.png`. Xoay nhanh rồi chậm: không có mảng sáng bật/tắt hoặc bóng kéo đuôi bất thường.
3. Tắt/bật đèn bằng điều khiển đang dùng: OFF trở về sáng nền cũ; ON nổi bật hơn, decor vẫn được chiếu. Không yêu cầu gradient mới trên tầm nhìn thường.
4. Đi sát/dọc tường và ra ngoài rồi quay lại: không tái xuất hiện nhấp nháy liên tục; viền đen nhỏ cũ được phép còn. Ngoài nhà/nhà khác không nhận profile mới.
5. Nếu cần kiểm tra đêm cố định, sửa `pose.json` hour=21, Apply Runtime Pose hai lần, Capture, Return Manual Control; OFF tối hơn ngày, ON vẫn có dải chuyển. Sau đó trả hour=13.5. Khi Stop/Play mới phải Apply lại vì component chỉ runtime.

## Kế hoạch sau, chưa triển khai

- Chỉ sau user duyệt đèn pin: cân nhắc gradient cho tầm nhìn thường bằng cách tách visibility và illumination sẵn có; cần thống nhất mức nền/phạm vi trước, không tự làm cùng lượt.
- Viền đen nhỏ sát tường: backlog, chỉ quay lại nếu lợi ích xứng chi phí.
- Chưa nhân rộng nhà khác; trước nhân rộng cần QA dữ liệu atlas/cửa/đồ động, build và multiplayer.
- Nếu user thấy gradient vẫn quá gắt: lấy đúng vị trí/hướng/giờ/torch và so ảnh, phân biệt phần cường độ còn gắt với occlusion. Không thêm ngoại lệ từng bức tường hoặc blur để lộ phòng sau. Nếu nhiều vòng không tiến triển thì đánh giá dừng, giữ save point.
