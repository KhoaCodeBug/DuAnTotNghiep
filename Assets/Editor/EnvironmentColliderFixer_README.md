# Environment Collider & Sorting Fixer

Mở bằng `Tools > Environment > Collider & Sorting Fixer`.

## Quy trình khuyến nghị

1. Mở prefab bằng **Prefab Mode** hoặc chọn prefab instance trong Scene.
2. Chọn root, nhấn **Dùng Selection**, rồi **Quét lại Collider + Sorting**.
3. Với Tilemap đã có Physics Shape tốt:
   - Nhấn nút đánh dấu đề xuất.
   - Kiểm tra lại các dòng được tick.
   - Thêm `TilemapCollider2D` và đặt layer `Obstacle`.
4. Với hàng rào, đèn, cây hoặc Sprite rời:
   - Tool chỉ đề xuất object có Physics Shape nhưng thiếu `Collider2D`.
   - Thêm `PolygonCollider2D`; Unity sẽ lấy shape đã vẽ trong Sprite Editor.
5. Với đoạn Tilemap bị lỗi:
   - Chọn Tilemap trong phần Polygon Patch.
   - Bật hiển thị/click cell cảnh báo.
   - Tạo collision proxy: tool sao chép collider tốt, bỏ riêng các cell đã xác nhận sai và tắt `TilemapCollider2D` nguồn.
   - Vẽ Polygon path bám chân tường hoặc tạo diamond patch thô.

Nếu cell bị cảnh báo có một tile cùng hướng (`_E`, `_N`...) với collider chuẩn ở gần đó, dùng `Tools > Environment > Repair Broad Collider Cells Using Donor`. Tool tạo collision proxy và thay riêng collider cell lỗi bằng donor; visual Tilemap không đổi. Đây là lựa chọn ưu tiên trước Polygon.

Sau khi sửa, chạy `Tools > Environment > Validate All Collider Proxies In Scene` để kiểm tra source/proxy collider, renderer ẩn, số cell và broad shape còn sót.
6. Chạy Player và scan A* để kiểm tra cửa, góc và hành lang.

Tool có nút **Scan A* trong Scene hiện tại**. Nút này chỉ scan dữ liệu hiện tại; hãy kiểm tra gizmo rồi tự quyết định có lưu Scene hay không.

## Màu cảnh báo trong Scene

- Đỏ: `ColliderType.None` hoặc Sprite collider không có Physics Shape.
- Cam: `ColliderType.Grid` tạo full-cell collider.
- Tím: Physics Shape phủ ít nhất 85% diện tích hoặc 55% chiều cao sprite. Đây chỉ là cảnh báo hình học, có thể là collider hợp lệ.
- Vàng: cell đang được chọn để xử lý.

## Vẽ Polygon Patch

- Click trái: thêm đỉnh.
- Enter hoặc double-click: chốt path.
- Backspace: xóa đỉnh cuối.
- Escape: hủy path đang vẽ.
- `Snap` giúp các đỉnh thẳng hàng; có thể đặt `0` để tắt.

## Nguyên tắc an toàn

- Tool không tự tick bất kỳ object nào sau khi quét.
- Tool không tự sửa sorting toàn căn nhà.
- Các nút ghi bị chặn nếu đang chọn prefab asset trực tiếp trong Project; phải dùng Prefab Mode hoặc instance trong Scene.
- Tất cả thao tác chính hỗ trợ Undo.
- Collision proxy nằm trong `__ColliderProxy_<TilemapName>` và không render hình ảnh.
- Polygon patch nằm trong object con `__ColliderPatches`, tách biệt với collider tốt có sẵn.

## Audit nhanh

Chọn root và chạy `Tools > Environment > Audit Selected Root To Console` để xem thống kê mà không thay đổi dữ liệu.
Audit tự tìm prefab root khi đang chọn một object con, đồng thời báo property override, GameObject/component được thêm hoặc bị xóa trên scene instance.

`Tools > Environment > Apply Selected Instance To Prefab (Auto Backup)` sẽ tạo một prefab backup có timestamp trước khi Apply toàn bộ override của instance vào asset. Chỉ dùng sau khi đã đọc override report.

## Quick House Pipeline

Nhóm `Tools > Environment > Quick House Pipeline` hỗ trợ lượt sửa nhanh cho nhà lớn:

1. `Backup Active Scene Snapshot`: lưu một bản copy Scene trước khi sửa mà không đổi Scene đang mở.
2. `Audit + Save Report`: ghi báo cáo sorting, component trùng, collider chồng, tile/cell và sprite khả nghi vào `Assets/EnvironmentFixerReports`.
3. `Normalize Safe Structure`: chuẩn hóa floor (`Default/-15`), wall/decor (`Gameplay`) và roof (`Foreground/11`), chuyển renderer cần Y-sort sang `Individual`, đồng thời bỏ component trùng nhưng giữ component có dữ liệu. Không tự di chuyển tile hay xóa collider.
4. `Decor Collider Review (No Auto Generation)`: chỉ liệt kê collider decor cần kiểm tra. Tool không tự sinh Tilemap/Grid collider cho nội thất vì sprite isometric thường có Physics Shape ôm cả thân hình; collider gameplay phải là footprint nhỏ tại chân vật thể.
5. Nếu còn proxy decor từ phiên bản tool cũ, chạy `Small Houses/Remove Unsafe Decor Proxies (Preserve Manual Polygons)`. Tool bảo toàn Polygon footprint vẽ tay sang `__ManualDecorFootColliders`, rồi gỡ proxy full-body. Giường, tủ loot và object có script tương tác không bị xử lý.
6. `Remove Verified Redundant Solid Polygons`: chỉ xóa Polygon khi phép lấy mẫu hình học đạt đồng thời Jaccard >= 94% và coverage hai chiều >= 96% so với TilemapCollider. Bounds tương tự không còn đủ điều kiện để xóa.
7. Các thao tác tách cell/collider đặc thù phải chạy sau audit và có Undo.

Với decor thường, nếu chưa thể xác định footprint đáng tin cậy thì ưu tiên không có collider thay vì collider ôm toàn sprite. Ngoại lệ là prefab có gameplay riêng như giường/tủ loot: giữ collider do prefab/script đó quản lý.

`Create Conservative Decor Footprints` đã ngừng sinh collider. Lệnh này hiện chỉ gỡ nhóm `__AutoDecorFootColliders` cũ. Không đưa collider decor tự động vào flow sửa nhà; chỉ giữ collider riêng của prefab gameplay như loot/bed.

`Rebuild Authored Wall Collider Network` chỉ áp dụng cho Tilemap tường. Tool xóa các nhóm collider sinh kiểu cũ và gắn `TilemapCollider2D` trực tiếp khi mọi tile tường đã có Physics Shape authored. Không dùng convex hull, không nối các đoạn tường qua khoảng trống và không suy đoán footprint. Nếu tile tường thiếu Physics Shape thì dừng để vẽ Polygon patch tay, không tự bù bằng hình học đoán.

Nếu một số sprite tường vỡ có Physics Shape ôm toàn thân hoặc có rectangle lơ lửng, lệnh rebuild tự chuyển sang collision proxy: proxy chỉ giữ tile tốt; cell lỗi bị loại hoàn toàn và nhận một Polygon mỏng trích từ dải thấp nhất của sprite tường chuẩn cùng hướng. Donor ưu tiên mẫu tường phổ biến nhất (`E1`) thay vì mảnh đặc biệt gần nhất. Validator bắt buộc source collider tắt, proxy không còn broad shape và mỗi cell bị loại có một foot patch thấp.

Pipeline hiện còn có ba kiểm tra dành cho công trình lớn:

- Audit tạo `structural envelope` từ cluster tường lớn nhất và đánh dấu từng cluster decor nằm ngoài phạm vi. `Extract Decor Clusters Outside Wall Envelope` chuyển các cluster đó sang `__ExternalEnvironment`, giữ nguyên world transform, tile transform, material và sorting.
- `Normalize Safe Structure` nén stale Tilemap bounds, bỏ TilemapCollider rỗng 0 shape và tự đặt Tilemap tường có collider vào layer `Obstacle`.
- Audit cũ vẫn có thể gợi ý PolygonCollider network ở ngoài root theo bounds, nhưng đây chỉ là candidate. Không dùng riêng kết quả đó để xóa TilemapCollider; phải chạy kiểm tra coverage hình học hoặc giữ collider nguồn.

Không được kết luận collider hoàn chỉnh chỉ vì object đã có `PolygonCollider2D`. Trước khi bỏ collider nguồn phải kiểm tra coverage theo hình học/cụm, bốn cạnh ngoài, các vách trong, từng nhóm kệ/quầy lớn và chạy lại A* scan. Bounds tổng và vài ray thưa không đủ; đây là bài học từ `cuahang` và `SieuThi_FIX`.

Riêng `cuahang`, pipeline có thể tách các cell `Object2_E_7` thành `__ExternalEnvironment/cuahang_hangrao_visual`, giữ collider chân hàng rào authored trong nhóm vật cản toàn map. `tuongnha` hiện có đủ Physics Shape cho toàn bộ tile nên dùng trực tiếp `TilemapCollider2D`; không chuyển sang Polygon nhiều path. Công cụ loot hiện chỉ tạo marker ứng viên `Store*`; không tự biến toàn bộ quầy/kệ thành loot vì cùng tiền tố sprite có nhiều loại đồ vật khác nhau.
