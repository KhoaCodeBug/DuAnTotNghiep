# PROMPT GỬI GOOGLE ANTIGRAVITY — SỬA TRIỆT ĐỂ FOW/LOS VÀ TẤM NỀN ĐEN KHI VÀO NHÀ

Bạn đang làm việc trực tiếp trong Unity project `ProJectZomboiNhai` tại:

`E:\Unity\GameObject\Game3D\ProJectZomboiNhai`

Engine: Unity 6 `6000.0.69f1`, game isometric zombie survival, Photon Fusion 2.

## 0. Quy trình và ràng buộc bắt buộc

1. Đọc đầy đủ các file liên quan trước khi sửa, tối thiểu:
   - `Assets/Khoa/Code/FogVisionController.cs`
   - `Assets/Khoa/Code/PlayerVision.cs`
   - `Assets/Shader/FogVisionOverlay.shader`
   - `Assets/Khoa/Code/RoofDetector.cs`
   - các collider/root/door portal dùng cho indoor visibility trong `Main.unity` và prefab liên quan.
2. Kiểm tra cả thay đổi hiện có trong working tree. Không `reset`, `checkout`, `clean`, `stash`, overwrite, commit, push, pull, merge hoặc đổi branch. Không xóa/sửa thay đổi không thuộc phạm vi FOW/LOS.
3. Chỉ sửa FOW/LOS và test/artifact cần thiết. Không chỉnh cân bằng loot, loading, chat, multiplayer, spawn gear hoặc logic gameplay khác.
4. Tạm thời **không làm lại/cân chỉnh đèn pin**: không đổi range, góc, màu, intensity, falloff, Light2D hoặc flashlight gameplay. FOW/LOS phải đúng ngay cả khi flashlight tắt; flashlight không được dùng để che lỗi FOW.
5. Không sửa trong `Library`, `Temp` hoặc `Logs` để che lỗi. Nếu Unity/test runner không chạy, ghi `BLOCKED/UNVERIFIED` với lý do thực tế, không bịa screenshot/log/pass.
6. Khi kết thúc phải báo cáo file/line thực sự thay đổi, test/lệnh thực sự chạy, pass/fail, screenshot và artifact path tuyệt đối. Không commit/push/merge.

## 1. Lỗi cần sửa theo ảnh người dùng

Các ảnh người dùng đính kèm cần được đọc trực tiếp tại:

- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-7863c0a6-0205-481a-8b4d-c645dcfe6ebb.png`
- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-e868ea7a-3d9d-49f3-8bb1-dedaa3247895.png`
- `C:\Users\triti\OneDrive\Pictures\Screenshots\Ảnh chụp màn hình 2026-08-31 001539.png`
- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-939f2c2a-15be-4a17-9695-a00157bb5289.png`
- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-20023589-7b49-4260-885e-543f6f1040d5.png`
- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-5d18b01e-10ef-49dc-87b3-b16fb7e13359.png`
- `C:\Users\triti\AppData\Local\Temp\codex-clipboard-4e29df6f-0bff-4952-9c09-7a10c67795cc.png`

Ba lỗi cốt lõi cần xử lý, không chỉ đổi màu opacity:

### Lỗi A — Tấm nền đen tràn sai vào trong nhà và ngoài vùng mắt nhìn

Khi Player đứng trong nhà, vùng đen/che phủ không bám theo vị trí nhìn thực tế của Player. Nó tràn qua các mép phòng, phủ sai vùng nội thất và có thể để lộ vùng ngoài nhà dù nhà không có cửa sổ. Vùng thấy được phải bắt đầu từ vị trí Player và bị cắt bởi tường thật; tường kín không được xem như vùng nhìn xuyên được.

### Lỗi B — Tấm nền đen giống một panel screen-space đè lên toàn màn hình

Ảnh cho thấy mask có các mảng tam giác/lưỡi lớn cố định theo màn hình/camera, không giống một vùng FOW world-space quanh nhân vật. Khi Player di chuyển hoặc camera zoom/pan, vùng nhìn phải di chuyển theo Player trong thế giới, không để lại panel cố định, không tràn qua tường và không tạo các đường cắt hình học vô lý.

### Lỗi C — Indoor/outdoor và điểm mù chưa theo LOS thực tế

Logic phải gần nguyên lý Project Zomboid trong giới hạn kỹ thuật của project:

- vùng gần Player có độ sáng/visibility mềm;
- càng xa hoặc càng bị tường che thì càng tối;
- tường, mái và vật cản thật tạo điểm mù;
- nhà không có cửa sổ không được nhìn thấy bên ngoài;
- chỉ cửa/portal đang mở mới có thể mở một vùng nhìn ra ngoài;
- đổi hướng nhìn/di chuyển phải làm vùng quan sát cập nhật quanh Player, không phải xoay một panel đen trên camera.

Hai ảnh cuối là tài liệu tham khảo về cảm giác FOW/LOS của Project Zomboid, không phải yêu cầu sao chép y nguyên shader/asset. Ưu tiên đúng nguyên lý world-space, occlusion và điểm mù.

## 2. Hợp đồng FOW/LOS phải thống nhất với PlayerVision

Không tạo hai bộ quy tắc khác nhau cho gameplay và hình ảnh. Hãy kiểm tra và thống nhất:

1. `PlayerVision` và FOW dùng cùng LOS origin của survivor body/eye trong trạng thái đi bộ; vehicle vision dùng vehicle origin khi thật sự ở trong xe. Không lấy child flashlight origin làm origin của FOW mắt thường.
2. Dùng cùng hướng nhìn, obstacle `LayerMask`, trigger policy và quy tắc collider blocking.
3. Collider có marker `MilitaryGateVisionPassThrough` chỉ là pass-through khi đúng design; mọi collider tường/cửa đóng/obstacle hợp lệ phải block.
4. Raycast phải lấy blocker gần nhất trên từng tia, bỏ qua collider pass-through, xử lý wrap giữa tia cuối và tia đầu, nội suy cạnh mềm vừa đủ.
5. Gameplay LOS của zombie và FOW visual phải cho cùng kết luận ở các case: zombie trước tường bị che, zombie sau tường bị che, zombie qua gate pass-through được thấy, đổi hướng nhìn thì cập nhật đúng.
6. Nếu code hiện tại đã có `VisionLineOfSight`, hãy audit lại và tái sử dụng đúng; không coi việc tồn tại class là bằng chứng logic hình ảnh đã đúng.

## 3. Yêu cầu triển khai FOW world-space

### 3.1. Ngoài trời

- Mask phải được tính trên world position của từng pixel/fragment hoặc polygon world-space tương đương, không dùng hình panel screen-space cố định.
- Tạo visibility fan/occlusion fan từ Player body origin với số tia phù hợp hiệu năng (64–180 hoặc giải pháp tốt hơn). Mỗi tia dừng ở collider block gần nhất; ngoài blocker bị che hoàn toàn hoặc gần hoàn toàn.
- Có vùng awareness mềm quanh Player nhưng không được biến thành một hình tròn sáng xuyên qua tường.
- Khi Player đứng sát tường, vùng sáng phải dừng theo mặt tường; không được tạo hình tam giác/lưỡi lớn xuyên qua tường.
- Khi camera zoom/pan, cùng một bức tường world-space vẫn chặn đúng vị trí; không được thay đổi hình học FOW chỉ vì viewport đổi tỉ lệ.

### 3.2. Trong nhà

- Xác định indoor bằng `RoofDetector`/collider authored của Player local, không bằng camera bounds và không bằng room envelope chung.
- Chỉ cho thấy nội thất thuộc building/room hiện tại và các vùng nối qua cửa/portal được authored, đang mở.
- Structural wall occlusion phải dùng collider/root của đúng building; fence hoặc obstacle ngoài building không được làm cắt nhầm nội thất hiện tại.
- Cửa đóng phải block. Cửa mở chỉ mở portal fan tương ứng; không dùng toàn bộ room polygon làm cửa thoát ra ngoài.
- Nhà không có cửa sổ: tuyệt đối không nhìn xuyên tường/mái ra cây, đường hoặc map ngoài nhà.
- Khi Player xoay 180 độ trong phòng, phần tường phía sau vẫn là tường che; chỉ vùng nhìn hợp lệ/awareness hợp lệ thay đổi.
- Opacity phía sau structural wall phải đủ kín để không nhìn thấy hình học/ánh sáng ngoài nhà qua lớp đen.

### 3.3. Không để flashlight che lỗi FOW

- Kiểm tra toàn bộ thứ tự kết hợp giữa base FOW, indoor occlusion, LOS và flashlight mask.
- Một flashlight đang bật cũng không được mở vùng phía sau collider tường kín.
- Trong lượt này chỉ xác minh flashlight on/off; không cân chỉnh thông số hoặc physical Light2D.

## 4. Kiểm tra shader và dữ liệu truyền vào material

Audit kỹ các điểm sau:

- world-to-screen/world-to-fog-plane transform;
- camera orthographic zoom, aspect ratio và viewport corners;
- hướng trục/isometric coordinate, z plane và sign của vector;
- array ray distance, ray count, angle sampling và interpolation;
- default/fallback khi chưa có Player, đang loading, đổi target, vào/ra nhà;
- stale buffer khi đổi indoor/outdoor hoặc đổi building;
- material property không bị giữ giá trị của frame/căn nhà trước;
- không có array index ngoài giới hạn, NaN, zero direction hoặc stale indoor portal.

Nếu cần thay kiến trúc từ overlay polygon hiện tại sang world-space visibility mesh/texture/mask khác, hãy giải thích trade-off và giữ phạm vi tối thiểu. Không chấp nhận patch chỉ đổi `opacity`, `alpha`, màu nền hoặc làm mờ toàn màn hình.

## 5. Test hồi quy và visual QA bắt buộc

Trước khi sửa, ghi baseline git/status và đọc compile state. Sau khi sửa:

### 5.1. Solo flow

Chạy đúng luồng từ MainMenu: `SOLO` → difficulty → `ENTER THE DEAD ZONE` → chờ Player spawn. Khi test Solo không dùng ParrelSync.

Chụp tối thiểu các mốc sau, tắt flashlight trước:

1. `FOW_FLOW_00_start_spawn.png`: đúng vị trí Player vừa spawn.
2. `FOW_FLOW_01_outside_house.png`: đứng ngay ngoài căn nhà gần điểm spawn.
3. `FOW_FLOW_02_inside_house.png`: đã vào trong căn nhà đó.

Sau đó kiểm tra thêm:

- xoay 360 độ trong nhà;
- đứng sát từng mặt tường;
- nhà không có cửa sổ;
- cửa đóng và cửa mở;
- đi từ ngoài vào trong và từ trong ra ngoài;
- zoom gần/xa, camera pan và aspect ratio khác nhau;
- ngày/đêm, flashlight off trước rồi mới xác minh flashlight on không xuyên tường.

### 5.2. Automated tests

Tạo hoặc cập nhật regression test thực sự, không chỉ assert source text:

- outdoor ray dừng tại wall;
- pass-through gate không chặn;
- indoor ray chỉ nhận structural collider của building hiện tại;
- closed portal không lộ exterior;
- open portal lộ đúng hướng/cự ly;
- PlayerVision zombie LOS và FOW LOS cùng kết quả;
- đổi building/indoor state không giữ lại ray/portal cũ.

### 5.3. Artifact

Tạo thư mục mới:

`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\FOW_LOS_Fix_YYYYMMDD_HHMMSS\`

Lưu tối thiểu:

- `BASELINE_GIT_STATUS.txt`
- `CHANGED_FILES.txt`
- `FOW_LOS_BEHAVIOR.md`
- `TEST_RESULTS.xml` nếu runner tạo được
- `UNITY_COMPILE_AND_CONSOLE.log`
- các ảnh flow và ảnh case lỗi trước/sau
- `FINAL_REPORT.md`

Trong report phải phân biệt rõ `PASS`, `PARTIAL`, `UNVERIFIED`; không gọi “đã sửa triệt để” nếu chỉ có static proof hoặc chỉ thấy UI không lỗi.

## 6. Tiêu chí nghiệm thu của Codex

Chỉ báo `FIXED` khi tất cả điều kiện sau có evidence:

1. Mask bám theo Player trong world-space, không phải panel đè màn hình.
2. Không tràn qua tường/mái và không nhìn thấy ngoài nhà qua phòng không có cửa sổ.
3. Cửa đóng block, cửa mở mở đúng portal.
4. Vùng gần/xa có gradient hợp lý: xa hơn/tường che tối hơn, cạnh mềm vừa phải, không có tam giác/lưỡi bất logic.
5. PlayerVision và FOW dùng cùng origin/direction/obstacle semantics.
6. Ảnh trước/sau và log runtime chứng minh hành vi; shader/compile không có lỗi mới.

Kết thúc báo cáo root cause, diff thực tế, test thực tế, ảnh/artifact tuyệt đối và rủi ro còn lại. Không commit/push/merge.
