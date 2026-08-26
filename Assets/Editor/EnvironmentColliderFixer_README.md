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

## Hospital hai khối nhà

`hospital` gốc vẽ 539 cell tường trực tiếp trên Tilemap của prefab root. Unity vẫn cho phép gắn `TilemapCollider2D` vào root; việc tách không phải giới hạn kỹ thuật. Pipeline tách thành `Hospital_Large_FIXED/tuongnha` (455 cell) và `Hospital_Small_FIXED/tuongnha` (84 cell) để mỗi khối có collider, trigger mái và `RoofVisibility` độc lập. Không Apply All instance này về `hospital.prefab` vì scene có hàng trăm override và một mái được thêm riêng.

Các lệnh chuyên dụng nằm tại `Tools > Environment > Quick House Pipeline > hospital`:

- `Rebuild Two-Building Structure`: chỉ dùng cho instance chưa tách; chia cell theo hai bounds mái, giữ tile matrix/color/flags và world position.
- `Rebuild All Broad Wall Foot Patches`: dựng lại proxy và foot patch cho cả hai khối; nhà nhỏ được phép mượn donor cùng hướng từ nhà lớn.
- `Repair Independent Roof Controllers`: đảm bảo mỗi trigger mái chỉ điều khiển đúng Tilemap mái của chính nó.
- `Validate Fixed Structure`: bắt buộc PASS cả số cell, source/proxy state, layer `Obstacle`, broad shape, độ cao và vị trí foot patch, ownership của mái và sorting decor.

Đối với cửa trong map isometric 2.5D, physics shape nằm hoàn toàn ở phần cao của sprite là **nóc cửa**, không phải vật cản gameplay. Hospital có 47 cell `Wall A5_N/W` loại này; pipeline phải loại hẳn chúng khỏi proxy, không được thay bằng foot patch. Vì collider của tile tường lân cận có thể rộng hơn một cell và chiếu chéo vào lòng cửa, pipeline còn cắt các path cạnh cửa tại biên cell và khoét đúng vùng giao trên vách chiếu chéo. Validator bắt buộc kiểm tra overlap tại tâm physics shape cũ của cả 47 nóc cửa và chỉ PASS khi `blocked=0`.

Lưu ý tọa độ collider: vertex từ `Collider2D.GetShapes(PhysicsShapeGroup2D)` phải đổi sang world bằng `PhysicsShapeGroup2D.localToWorldMatrix`, sau đó mới đổi về local của Polygon output. Không dùng `temporary.transform.TransformPoint` cho Tilemap nằm dưới Grid; cách đó có thể áp offset công trình hai lần và đặt polygon chân tường lệch xa dù số path/độ cao vẫn đúng. Validator vì vậy phải kiểm tra cả `patch.bounds` nằm trong bounds collider công trình, không chỉ đếm path.

## School

`school` là một công trình lớn duy nhất: root Tilemap rỗng, `tuong1` có 617 cell tường và `nocnha` có 1197 cell mái; `noctruong` hiện rỗng. Không tách thành hai building và không Apply All scene instance về `school.prefab`.

Các lệnh chuyên dụng nằm tại `Tools > Environment > Quick House Pipeline > school`:

- `Audit Structure + Physics Bands`: ghi báo cáo cụm tile, sprite, sorting và dải physics shape vào `Assets/EnvironmentFixerReports/school_current_audit.txt`.
- `Rebuild Collider + Roof + Layers`: giữ nguyên visual Tilemap, dựng proxy collider cho `tuong1`, thay 22 broad shape bằng foot patch, loại 9 `Wall A5_S` chỉ nằm trên phần cao của lối mở và cắt collider lân cận chiếu vào cửa. Trigger mái được dựng theo từng dải cell của cluster mái lớn nhất, không dùng convex hull phủ qua khoảng trống.
- `Fix Decor Y-Sorting Only`: chỉ đặt `decordlophoc` và Tilemap con `decord` về `Gameplay/0/Individual`. Không giữ Order 5/7 vì order cao ép toàn bộ bàn, tủ và thiết bị vẽ đè lên tường, làm decor trông như xuyên tường dù renderer đã ở chế độ `Individual`.
- `Fix LootQuanSu Prefab Sorting`: sao lưu rồi chuẩn hóa riêng bốn prefab `LootQuanSu1/2/3/Vjp` và bốn instance trực tiếp dưới `school` về `Gameplay/0/Pivot`. Pivot chân sprite là điểm sort đúng với trục Y của Renderer2D; không đổi Transform/Z, sprite, material, collider, `LootContainer`, loot table hay `NetworkObject`. Dùng validator riêng `Validate LootQuanSu Prefab Sorting` để kiểm tra mà không phụ thuộc trạng thái collider/mái của toàn School.
- `Validate Fixed Structure`: chỉ PASS khi proxy không còn broad/nóc cửa, tâm 9 nóc cửa có overlap bằng 0, mẫu tường thường vẫn bị chặn, patch đúng vị trí/độ cao, trigger mái phủ đủ footprint và không có collider solid ngoài ý muốn trên decor/floor/roof.

`Door11_W` là sprite cửa song sắt có physics shape thấp tại chân và được giữ làm vật cản; không được loại chỉ vì tên chứa `Door`. Quyết định collider phải dựa trên hình học và vai trò visual thực tế. Riêng `Wall A5_S` của school bắt đầu ở 44.5% chiều cao do một hàng padding trong sprite, nên được nhận diện bằng cả family `Wall A5` và dải shape cao; đây là ngoại lệ có chủ đích so với ngưỡng generic 45%.
