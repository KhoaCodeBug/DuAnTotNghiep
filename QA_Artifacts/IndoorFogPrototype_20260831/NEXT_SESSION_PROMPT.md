# Prompt to continue in a new Codex task

MODEL BẮT BUỘC THEO MẶC ĐỊNH USER: Sol - High. Lúc tạo task dùng `model="gpt-5.6-sol"`, `thinking="high"` rõ ràng, không chỉ ghi tên model trong prompt và không để app tự chọn Luna. Chỉ đổi khi user yêu cầu khác.

KẾT QUẢ MỚI NHẤT: radial boundary fade bị bác đã được loại; candidate Sol-High mới chỉ grade silhouette near→far tại góc/cửa. Đọc `SHADOW_BOUNDARY_REVIEW.md` và ảnh `shadow-verified-*` trước phần lịch sử: lõi TV/tranh/tủ/tường giữ sáng như V2, mép phải tối dần, nhưng dải ở vài góc còn hẹp hơn concept. EditMode 4/4 và PlayMode 5/5 pass; user chưa test tay, không được coi automated/self-review là nghiệm thu, chưa push.

CẬP NHẬT CUỐI CÙNG: user ĐÃ DUYỆT làm tối sớm ở phía sáng để fade tới ranh, cho tiếp tục một vòng thử có giới hạn, yêu cầu so ảnh khắt khe và kiểm tra nhiều vị trí/hướng/ảnh hưởng nhà khác. Bắt đầu bằng BOUNDARY_FADE_NEXT_ITERATION.md trong cùng thư mục: đó là kế hoạch/cổng nghiệm thu mới nhất. Các câu phía dưới nói chỉ thảo luận/chưa cho làm đã hết hiệu lực. Cho phép mở Unity để chẩn đoán/test khi tiếp tục; không push. Tầm nhìn thường/ngoài scope giữ nguyên. Trước giao test: GodModeON, flashlightOFF, movementON đã kiểm chứng.

CẬP NHẬT ƯU TIÊN: user đã test và đánh giá gradient chưa tốt/không thấy cải thiện rõ tại ranh phải khoanh đỏ. Đọc mục User review mới nhất trong FLASHLIGHT_GRADIENT_REVIEW.md và ảnh Gradient_UserReview_20260831. Lượt hiện tại chỉ thảo luận tính khả thi/hướng sửa ranh hiển thị cuối cùng, chưa cho bắt đầu hướng triển khai mới. Các dòng bên dưới nói chưa có phản hồi là lịch sử đã hết hiệu lực. Unity đang Stop; không tự mở Play. Khi bàn giao test lần sau phải bật God Mode có sẵn, tắt đèn pin và trả movement; lần trước God Mode chưa bật thành công.

Tiếp nối Indoor Fog trong Unity project hiện tại, xử lý phản hồi test tay tiếp theo cho gradient đèn pin. Không bắt đầu lại V1/V2 hoặc mở rộng phạm vi.

Trước khi sửa, đọc AGENTS.md nếu có, các project skills Unity, phần cuối CODEX_PROJECT_WORK_LOG.md và QA_Artifacts/IndoorFogPrototype_20260831/FLASHLIGHT_GRADIENT_REVIEW.md, NEXT_SESSION_HANDOFF.md, README.md. Kiểm tra Git/diff/Unity state/Console để xác minh; lịch sử dưới mục Current state không thay phản hồi mới nhất của user.

User đã test V2: decor pass, không nhấp nháy liên tục pass, ngoài nhà/quay lại pass, viền đen nhỏ tạm pass/backlog. Phần cần cải thiện là cường độ sáng mạnh → vừa → mờ → tối dần, không phải cạnh hình học của tường. User chỉ cho làm đèn pin; tầm nhìn thường giữ nguyên và ghi ý tưởng mở rộng sau.

Gradient đã triển khai local: feather=.5 trong Fog + lõi Light2D~61.21 độ khi torch bật trong house opt-in; góc ngoài/gameplay145 không đổi. OFF100/140, nhà khác torch105/145 giữ nguyên. Không thay blocker/atlas/scene/prefab/Fusion. Đã có44/44 EditMode và5/5 PlayMode pass,11state Solo,180bước motion, ảnh từng bước và A/B/A cùng binary. Chưa được user nghiệm thu gradient, chưa full suite/build/HostClient/soak. Xem review để không suy diễn bằng chứng quá mức.

Save point V2 ee0554d149a15fd89b3a370fab974058f235e668 đã push theo yêu cầu, sau đó được merge bởi thao tác bên ngoài qua PR#326 vào main5aa09d71c (tree giống hệt). Remote feature branch đã bị xóa. Local branch codex/restore-indoor-vision-20260831 vẫn có gradient/QA/docs chưa commit; không push lại hoặc merge tự động. Checkpoint ddf440424 còn nguyên. Bảo toàn mọi thay đổi, không reset/clean. Nếu cần rollback, lưu patch/file mới trước rồi chỉ hoàn tác đúng diff gradient về ee0554d14.

Tiếp tục dựa trên phản hồi user về bộ gradient-final/step2; nếu chưa có phản hồi, không tự tuyên bố pass hoặc mở sang tầm nhìn thường. Thay đổi từng bước nhỏ, chụp Game View thật cùng pose so concept và ảnh user, nêu rõ cải thiện/còn lệch. Kiểm tra chuyển động, ngày/đêm ON/OFF, ra-vào/nhà khác/disable và hiệu năng khi sửa thêm. Nếu nhiều vòng không tiến triển hoặc cần refactor lớn thì đề xuất dừng, giữ save point.

Nhà mẫu nhachinhxaydautien (12), Player(-39.2,44.3), nhìn lên,13:30,torchON. Tools > QA > Indoor Fog: sau Play MainMenu chạy Start Solo Automation, chờ Solo ready; Apply Runtime Pose hai lần rồi Return Manual Control. Component chỉ runtime nên Play mới phải apply lại. Bàn giao phải cập nhật work log/handoff, trả Player đúng chỗ, nêu test tay và kết quả mong đợi, rủi ro và Git. Không xem test tự động là user nghiệm thu.
