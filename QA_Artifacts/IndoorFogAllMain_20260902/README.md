# Indoor Fog — mép trên tường và toàn Main (02/09/2026)

## Phạm vi và nguyên nhân

Yêu cầu: sửa các ô đen trên **mép trên tường**, tự đánh giá bằng Play Mode và ảnh cận cảnh, mở rộng sang các công trình còn lại trong Main, đồng bộ Git rồi push nhánh riêng. Không dùng việc test tự động xanh làm bằng chứng duy nhất cho chất lượng hình ảnh.

Atlas cũ lấy một mẫu alpha nhị phân cho mỗi texel. Ở School, một texel của atlas 1024 tương ứng nhiều pixel màn hình, nên đường viền chéo bị lượng tử hóa thành ô lớn. Chỉ đổi sang bilinear và tăng mật độ chưa đủ: ảnh cận cảnh vẫn lộ gợn. Bản cuối lấy 4×4 mẫu alpha ngay lúc bake, lưu coverage và tọa độ đã nhân coverage, rồi chuẩn hóa lại khi đọc. Xử lý độ che phủ không sửa collider hay tăng số tia nhìn.

- Atlas RGHalf: mật độ mục tiêu 0,02 world unit/texel, mỗi chiều tối đa 3072; bilinear, Clamp, không mipmap.
- School: 2555×1386, 14.164.920 byte (~13,51 MiB), chỉ giữ atlas công trình đang dùng; ra ngoài phải giải phóng.
- 77 công trình / 78 bản đồ bề mặt trong Main, gồm 12 nhóm kiểm tra (hai khu bệnh viện tính riêng). Thêm 62 cấu hình còn thiếu; không áp dụng hàng loạt ra scene khác.
- Cửa hàng tiện lợi có hai trigger mái chồng nhau. Đăng ký cả hai bằng tham chiếu rõ ràng cho cùng surface map; vẫn dùng chính collider do gameplay chọn để tính polygon và occlusion. Không đổi RoofDetector, layer, AI, mạng hay quyền hiển thị zombie.
- Giữ phân loại/làm ổn định biên bóng từ checkpoint trước. Không tắt bóng để che lỗi.

## Quy trình tự nghiệm thu

1. Mở MainMenu → Solo → Medium → Enter the Dead Zone, chờ gameplay thực sự sẵn sàng.
2. School, tọa độ (11,36; 49,93), hướng lên: ngày 13:30 và đêm 21:00, đèn bật/tắt. Chụp sát mép trên tường với zoom 1,5, camera ngang 0 / 2,4 / 4,8; đối chiếu composite và ảnh world không Fog cùng kích thước/camera. Mong đợi: không có ô đen atlas nhô/cắt bậc lớn trên mép, bóng đèn vẫn che đúng phía khuất.
3. Dịch chuyển nhỏ ±0,08 quanh vị trí School để kiểm biên bóng không nhảy danh tính; giữ kiểm tra phân loại tối đa 32 biên.
4. Đi qua đủ 78 map: đèn bật/tắt, map/trigger đúng, atlas tạo một lần và tái sử dụng, thoát nhà phải thu hồi. Với 12 nhóm: chụp cả ngày/đêm × bật/tắt đèn.
5. Chạy kiểm tra GPU bằng texture alpha chéo tổng hợp: phải giữ coverage phân số và giải mã đúng tọa độ chân bề mặt; chạy hồi quy visibility/zombie.

Đây là các bước agent tự chạy; không yêu cầu người dùng test lại để chốt lần sửa này.

## Bằng chứng và kết quả

**Agent tự đánh giá: PASS cho lỗi ô đen mép trên tường và rollout Main trên máy này.** Đối chiếu ảnh world/composite cùng camera, không chỉ dựa vào kết quả tự động. Không còn các khối đen bậc lớn như ảnh báo lỗi; vẫn giữ pixel-art và bóng khuất vốn có, không hứa mọi đường chéo trở thành vector trơn.

- Vòng cuối `4c891d4dc6d34652bc6bd25335d1dc59`: **11/11**, 194,34 giây. Có kiểm tra GPU alpha chéo, authoring, sparse scan, biên bóng/temporal, 78 map trong Play Mode và 4 địa điểm stress.
- Hồi quy `c61e1f084f51437190e9e2f054bed719`: **5/5**, 21,76 giây. Kết quả XML nằm cạnh báo cáo; Console không có lỗi biên dịch/runtime, chỉ thông báo lưu kết quả/cleanup của Test Runner.
- `allmain-20260902-172949-runtime.csv`: **78/78**; 12 nhóm × 4 trạng thái được chụp trong `final/`. State của cửa hàng chứng minh `Trigger` thứ hai vẫn có `surface=1`.
- CPU tạo atlas cao nhất trong vòng này 12,39 ms; đây không phải phép đo GPU của frame đầu vào nhà. School 13,51 MiB; không còn atlas local sau Exit Play Mode.
- Profile School 240 frame: `FogVision.UpdateMaterial` trung bình **0,229 ms**, P95 **0,728 ms**; toàn GPU frame trung bình **3,098 ms**, P95 **5,327 ms**. Đo trong Editor trên máy này, không phải cam kết FPS cho mọi máy.
- Bộ chụp đã sửa lỗi camera theo visual child bị trễ sau teleport và lỗi khác tỷ lệ khung hình giữa raw/composite. Ảnh zoom 1,5 có mật độ pixel/world cao hơn ảnh người dùng zoom 3 ban đầu.

Ảnh cận cảnh cuối: [Fog ở mép tường](final/rollout-20260902-173208-336-school-close-0-composite.png), [world cùng khung](final/rollout-20260902-173208-336-school-close-0-raw.png), [mép bên phải](final/rollout-20260902-173208-336-school-close-2.4-composite.png). Các file mang hậu tố `0/1/2/3` lần lượt là ngày bật/tắt, đêm bật/tắt.

Các lần trung gian không được coi là bản đạt:

- `0c29df2498b542f3bcb8179192a27338`: 10/10 nhưng hình cận cảnh chưa đạt, đã tiếp tục sửa alpha bake.
- `1670f29264f54912a45fa10902b03e01`: bắt được trigger thứ hai ở cửa hàng; đã bổ sung alias, không bỏ qua assert để che lỗi.

## Git và giới hạn

- Nhánh: `codex/indoor-fog-all-main-20260902`, đã merge `origin/main` tại `a14953daa` không có xung đột.
- Ba PR đang mở đã được mô phỏng merge, không nhập nội dung chưa duyệt: #276 `sualaicannha` và #277 `pushlai2cailoiqq` xung đột Main.unity; #135 `xaymaplan17` xung đột Nhi.unity. Cùng xung đột đã tồn tại khi so riêng từng PR với origin/main, trước rollout.
- 13 ảnh/log QA local trùng đường dẫn với main mới đã được giữ ở `.codex-artifacts/pre-main-merge-20260902/QA_Artifacts/VisionRollback_Runtime/`. Không xóa các bằng chứng cũ/untracked khác.
- Nghiệm thu hình ảnh trên Unity 6000.0.69f1, DX11 của máy này. Không tuyên bố đã test GPU khác hoặc phiên multiplayer hai máy. Không thay đổi network/gameplay visibility trong rollout.
- Giữ nguyên layer/collider của từng nhà, bao gồm khác biệt cũ giữa nhóm tường Default và Obstacle. Rollout này không phải một đợt sửa hình học, đường đạn hay mọi hiện tượng chiếu sáng khác của map.
- Chỉ đóng Unity và shutdown sau khi test/ảnh đạt và push được xác nhận. Không ép tắt hoặc bỏ qua hộp thoại dữ liệu chưa lưu.
