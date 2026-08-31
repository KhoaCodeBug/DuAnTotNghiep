# Vòng thử được user duyệt — fade ở ranh hiển thị cuối (2026-08-31)

## Phê duyệt và trạng thái

User chấp nhận làm tối sớm một dải ở phía hiện đang sáng để tạo chuyển sáng → vừa → mờ → tối như concept, giữ vùng khuất không sáng thêm. Cho tiếp tục nhưng yêu cầu đánh giá khắt khe, so đúng ảnh mẫu và xem xét nhiều vị trí/hướng/nhà khác. User báo còn32% context và giao agent tự quyết định chuyển task; quyết định đóng gói rồi chuyển trước triển khai mới. Đây là phê duyệt MỚI, thay các dòng lịch sử còn nói chỉ thảo luận/chưa cho triển khai.

Unity đã Stop. Không sửa code gameplay trong lượt đóng gói. Chưa triển khai hướng fade ranh mới. Bản cone-only hiện local đã bị user bác, KHÔNG phải baseline tốt. Baseline user tạm chấp nhận là commit ee0554d14 (V2), đang có trong main qua PR#326, commit5aa09d71c. Local HEAD ee0554d14, branch codex/restore-indoor-vision-20260831, remote branch deleted/upstream gone. Không push/merge hoặc tự tạo lại remote branch. Các thay đổi local và QA cần giữ.

## Mục tiêu đúng

Đọc 3 ảnh Gradient_UserReview_20260831: vùng khoanh phải trên tường và sàn đang bị cắt sáng/tối gắt. Concept có một dải giảm cường độ rộng ở vùng đó. Làm mềm rìa cone bên trái hoặc cạnh gạch không chứng minh đạt yêu cầu. Không tuyên bố đã cải thiện nếu chỉ thấy trong thông số/shader signal mà ảnh cuối không rõ.

Code hiện có fade angleDot, sau đó visible=insideIndoor*indoorOcclusionVisibility và max-opacity bị chặn gần0.98. Lớp sau có thể cắt mất ramp; sự tồn tại được code xác nhận, đóng góp Fog/ShadowCaster2D tại vùng user khoanh chưa tách riêng runtime. Thực hiện chẩn đoán nhỏ trước khi chọn kỹ thuật. URP local Light2D có shadowSoftness và shadowSoftnessFalloffIntensity; không suy ra chỉ chỉnh chúng sẽ sửa final Fog mask.

## Hướng và giới hạn

- Tạo dải giảm cường độ theo ranh hiển thị cuối, chỉ phía đang sáng; giữ che khuất gameplay và vùng sau tường. Cường độ phải khớp mức tối ở ranh, không chỉ giảm về ambient rồi lại nhảy về đen.
- Cân nhắc mask/khoảng cách tới biên hoặc giải pháp trực tiếp phù hợp dữ liệu hiện có sau chẩn đoán. Chưa chốt render texture/SDF/blur; không mặc định thêm renderer lớn. Không blur ảnh màu/sprite toàn màn hình. Không tăng số ray/CPU quét cả map để che vấn đề.
- Chỉ flashlight và nhà opt-in. Không thay normal vision, ambient day/night, multiplayer state/RPC, scene/prefab hoặc tác động toàn map. Không vá tọa độ/nhận diện từng bức tường; vị trí cố định chỉ dành QA.
- Thuật toán cần dùng Player, vật cản và dữ liệu nhà để dùng được ở vị trí/hướng khác. Nhà lân cận không opt-in phải giữ hành vi cũ, không nhận mask/tham số nhà mẫu. Chưa nhân rộng tính năng lên nhà khác chỉ vì một góc pass.
- Nếu cần xác nhận khả năng dùng cho kiểu nhà khác, có thể kiểm kê dữ liệu và regression nhà chưa opt-in trước. Không báo tổng quát hóa đã pass khi chưa có evidence một fixture phù hợp khác; trình bày rõ giới hạn.

## Cổng thị giác bắt buộc — chặt hơn các lượt cũ

1. Giữ bộ before/after đúng vị trí, hướng, giờ, đèn, zoom/độ phân giải. Crop cùng vùng khoanh và cùng tỷ lệ hiển thị; tránh ảnh quá nhỏ khiến lỗi biến mất. So concept về độ rộng/cảm giác chuyển, không đòi trùng pixel hình học AI.
2. Vùng quan trọng phải thấy được khoảng chuyển từ sáng mạnh qua vừa/mờ tới tối bằng mắt ở ảnh cuối. Nếu chỉ thay đổi rất nhẹ như gradient-step2 thì FAIL. Không gọi các dòng test pass là nghiệm thu hình ảnh.
3. Kiểm tra cùng tham số ở giữa phòng, tiến gần tường, góc lõm/lồi, cửa ra vào; hướng lên/xuống/trái/phải và chéo. Ưu tiên ca có ranh thật cắt ngang sàn và mặt tường/decor. Không tinh chỉnh riêng từng pose để tạo ảnh đẹp.
4. Đứng yên quay chậm/quay nhanh; tiến vào, lùi ra, chạy song song tường; kiểm tra không flicker, sọc mới hoặc độ sáng nhảy từng nấc. Chuỗi frame có ích nhưng còn cần kiểm tra input thật; teleport QA không thay WASD.
5. Ngày/đêm với torchON, cùng pose torchOFF phải giữ nền/tầm nhìn cũ. Bật/tắt nhiều lần. Vào/ra nhà, sang nhà khác, đứng gần nhà lân cận và quay lại: không rò sáng/giữ nhầm mask/profile.
6. Mỗi bước ghi rõ PASS/FAIL/CHƯA TEST và chỉ rõ ảnh/chỗ nào. Lưu cả ảnh bị loại. Đánh giá vùng còn gắt trung thực; không gọi yêu cầu user là bóng vật cản có chủ ý để bỏ qua.
7. Không làm tối đến mức mặt tường/tủ trước mặt mất chi tiết từng được user pass. Viền đen nhỏ V2 đã tạm chấp nhận/backlog; không mở task mới chữa viền.

## Cổng kỹ thuật, hiệu năng và dừng

Compile + Console, test theo risk, regression ngoài scope. Các44/44EditMode và5/5PlayMode trước chỉ chứng minh bản thử cũ không lỗi kiểm tra; phải xác minh mới sau sửa.

Đo CPU/GPU/fog marker/draw/batch dưới cùng pose/time/đèn/resolution, warmup và không capture trong sampling. Có phép đo baseline phù hợp thật, ghi rõ nếu chỉ so profile cùng binary. Không hứa60FPS hoặc lấy FPS trong ảnh có ScreenCapture làm benchmark. Nếu thêm mask/pass, kiểm tra chi phí và tài nguyên khi enter/exit/disable, không chỉ steady-state.

Giới hạn một vòng thử có chẩn đoán và một candidate chính để đánh giá. Có thể sửa lỗi kỹ thuật trong vòng đó nhưng không tự kéo dài nhiều phương án/refactor. Nếu vẫn không tạo khác biệt rõ tại vùng cần sửa, hoặc cần dữ liệu từng tường/renderer lớn, dừng và báo user lựa chọn giữ V2. Giữ tất cả bản thử trước rollback; không reset/clean.

## Setup và bàn giao test

Sample nhachinhxaydautien (12), base(-39.2,44.3), nhìn lên,13:30,zoom3; edge pose(-39.8,44.9),dir(1,.3),zoom2.3. pose.json trong thư mục QA. Tools > QA > Indoor Fog: Play MainMenu, Start Solo Automation, xác minh Solo ready, Apply Runtime Pose; component runtime cần apply lại sau mỗi Play. Căn nhà mẫu67surfaces. Capture trước khi Return Manual Control nếu cần fixed shot.

QUY TẮC USER MỚI: trước trả test phải bật bất tử có sẵn, tắt đèn để tránh hao pin, trả movement, đóng menu/đảm bảo input được dùng; xác minh rồi mới nói sẵn sàng. Lần trước thiếu Return Manual Control nên user không đi được; đã gọi sau phản hồi. God Mode chưa bật được lần đó, không ghi thành công.

God Mode hiện có ở DevCheatManager.isGodMode / phímP mở menu. Nó ở DontDestroyOnLoad. MCP FindGameObjects local implementation chỉ duyệt GetActiveScene nên tìm theo name/component không thấy DDOL; tránh lặp truy vấn vô ích. manage_scene get_hierarchy bỏ qua scene_name và cũng trả active scene. Find Player bằng hierarchy được id nhưng id đổi mỗi lượt. Không thêm code chỉ để né công cụ. Computer Use phải dùng skill; nếu user đang điều khiển và tool báo user input detected thì không giành chuột/phím hoặc lặp mãi, báo rõ trở ngại và phối hợp.

Khi dừng vòng: ảnh before/after, tự đánh giá, test đã chạy/kết quả, test tay+kỳ vọng, giới hạn, Git, worklog/handoff. User nghiệm thu riêng, không tự push bản mới.
