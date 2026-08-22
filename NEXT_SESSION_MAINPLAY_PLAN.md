# MainPlay — Kế hoạch phiên tiếp theo

File này là bàn giao công việc cho phiên phát triển tiếp theo và không thuộc phần mô tả tính năng trong README.

## 1. Chốt QA trạm sửa xe ven đường

Mục tiêu: xác nhận minigame hiện tại ổn định trước khi đưa vào khu quân sự.

- Test thủ công Solo/Host đủ 5 hạng mục và xác nhận item chỉ bị trừ khi hoàn thành.
- Test Host + một Client: tranh quyền sửa, hiển thị tên người đang sửa, hủy bằng `Esc`, nhận sát thương, chết và disconnect.
- Cân chỉnh cảm giác tốc độ kim, khoảng 4–7 giây và kích thước vùng Success/Perfect nếu cần.
- Xác nhận trượt skill-check không làm Noise Meter tăng và không kéo zombie ở trạm ven đường.

## 2. Gắn gameplay vào khu quân sự khi map hoàn tất

Mục tiêu: thay tương tác sửa theo thời gian cũ bằng minigame đã kiểm chứng.

- Đặt marker xe thoát hiểm, polygon sửa trước mũi xe, cổng phòng thủ, máy phát điện, kho vũ khí và điểm tập hợp.
- Dùng chung luật skill-check và state authoritative; tạo cấu hình/item quân sự riêng nếu asset hoặc yêu cầu loot khác xe cảnh sát test.
- Nối hoàn thành 5 hạng mục với phase `ReadyToEscape` và flow tập hợp người còn sống.
- Chỉ bật tiếng thất bại/thu hút zombie trong giai đoạn quân sự sau khi có vị trí cổng và giới hạn số zombie phản ứng.

## 3. Hoàn thiện flow văn phòng

- Căn lại bàn/tài liệu, radio, tủ hoặc két theo map mới.
- Kiểm tra thứ tự quest không thể bị bỏ qua: tài liệu → radio → tủ/két → mảnh bản đồ.
- Kiểm tra vùng reveal bản đồ và đường dẫn tới căn cứ quân sự.

## 4. Hoàn thiện phòng thủ và kết thúc Tuyến B

- QA cổng bị phá, zombie tràn vào, điều kiện thất bại khi toàn đội chết và phục hồi trạng thái cho late joiner.
- Hoàn thiện tập hợp đội, xe rời căn cứ, victory cutscene và bảng tổng kết.

## 5. Phần mở rộng sau gameplay chính

- Silhouette local Player xuyên qua tường, mái, cây và các vật thể che khuất.
- Tiếp tục bộ sprite xe nhiều hướng và QA hướng/pivot khi lái.

## Dữ liệu cần chuẩn bị trong scene

- GameObject/marker chính xác cho xe quân sự và hướng đầu xe.
- Polygon vùng sửa đã căn theo sprite cuối.
- Marker cổng, máy phát, kho/văn phòng, điểm zombie xuất hiện và điểm tập hợp thoát.
- Xác nhận asset xe quân sự cuối và danh sách vị trí loot vật phẩm tương ứng.
