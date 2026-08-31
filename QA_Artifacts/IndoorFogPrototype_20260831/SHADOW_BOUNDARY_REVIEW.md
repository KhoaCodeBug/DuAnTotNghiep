# Shadow-boundary candidate — review cuối 2026-08-31

## Kết luận trung thực

Candidate radial bị user bác đã được loại và giữ nguyên trong `RejectedRadialFade_20260831/`. Nguyên nhân gốc: lấy khoảng cách tới mọi ray-hit làm ranh fade, nên chính mặt tường liên tục đang được đèn chiếu cũng bị kéo về opacity tối. Cách đó sai với ảnh mẫu và không còn nằm trong implementation hiện tại.

Candidate mới chỉ phân loại **độ nhảy độ sâu giữa hai ray kề nhau** tại góc/cửa (near hit → far hit) thành mép bóng. Mặt tường có hit liên tục không phải ranh, vì vậy tường có TV/tranh, kệ sách và decor trong lõi đèn giữ sáng như V2; chỉ dải sát vùng khuất bị hạ sáng. Candidate chỉ tăng độ che, không thay các ray visibility đã được user chấp nhận và không mở lộ phòng/sàn/actor phía sau tường.

Đối chiếu ảnh mẫu: hướng sáng đúng hơn rõ rệt so radial regression, nhưng **chưa thể gọi là khớp hoàn toàn concept**. Ở một số góc dải chuyển còn hẹp và tối nhanh hơn ảnh mẫu AI; không mở rộng vùng visible để ép hình học runtime giống concept vì việc đó có nguy cơ leak.

## Bằng chứng hình ảnh cùng pose

- Baseline V2: `shadow-verified-center-v2.png`.
- Candidate: `shadow-verified-center-fade.png`.
- Candidate góc edge/zoom gần vùng user chỉ: `shadow-verified-edge-fade.png`.
- Bản radial bị loại: `radial-rejected-center.png`.
- Ma trận A/B: `shadow-verified-{center,edge,nearwall,corner,down,night,dayoff,nightoff}-{v2,fade}.png` cùng state/rays tương ứng.
- Chuyển động gần/song song tường: `ShadowBoundary_Motion/`, 41 ảnh và 180 mẫu CSV. Đây là fixture teleport từng bước, không thay cho test cảm giác WASD của user.

Đo ROI ảnh center (mean luma): tường sáng V2/candidate/radial = 84.25/84.25/40.96; kệ = 91.39/91.38/39.13; mép phải = 46.60/25.31/15.88. Nghĩa là candidate giữ lõi sáng nhưng làm tối dần ở mép; radial làm tối sai cả lõi. Torch OFF day A/B giống pixel trong ROI (max channel diff 0); night OFF chênh tối đa 1 mức do thời điểm capture, không có pixel chênh trên 1.

## Kiểm tra kỹ thuật

- Compile cuối: 0 error. Warning runtime/test cũ gồm `ViTriXeChetMay` fallback, Voice TransmitEnabled=false, duplicate EventSystem trong test, emoji thiếu glyph trong cheat UI; không phát sinh từ shader/candidate.
- EditMode `IndoorFogSurfacePrototypeEditorTests`: 4/4 pass, job `30ee6c8119524f059708ee51ae08f739`, 28.4382258s. Bao gồm continuous wall=0 edge, hai silhouette=2 edge, overflow bảo thủ=0 và integration Solo thực qua day/night, ON/OFF, outdoor, nhà khác, return, disable/atlas release.
- PlayMode `VisibilityAndZombieRegressionPlayModeTests`: 5/5 pass, job `a3401bbc67904de0918c59bf0dbc987d`, 20.8843838s. Đây là regression gameplay liên quan visibility/zombie/Solo transition, không phải full suite.
- Performance A/B/A, cùng runtime/pose/987×568, warmup 30 + 240 frame: GPU median 3.495936 / 3.586048 / 3.494912 ms; `FogVision.UpdateMaterial` 0.05310 / 0.05425 / 0.05185 ms; draw/batch 458 không đổi. Candidate tăng khoảng 0.09ms GPU ở phép đo Editor này; chưa suy rộng sang build/độ phân giải khác.
- Không đổi scene/prefab/animation/Fusion state/RPC. Chỉ nhà mẫu opt-in; flashlight OFF, outdoor và nhà khác gửi edgeCount=0.

## Cách test thực tế trong game

1. Trong runtime đã chuẩn bị, bật/tắt đèn pin tại vị trí giữa phòng, nhìn lên tường TV/tranh/kệ rồi quay về mép tường phải.
   - Mong đợi: lõi tường/decor vẫn sáng rõ; chỉ sát mép khuất chuyển sáng → vừa → tối, không tối toàn tường.
2. Đi sát tường, chạy song song cạnh trước và quay đèn chậm/nhanh.
   - Mong đợi: không xuất hiện lại dải đen lớn/nhấp nháy liên tục; vệt viền nhỏ V2 vẫn là backlog đã được user tạm chấp nhận.
3. Tắt đèn, thử ngày/đêm; ra ngoài, sang nhà lân cận, quay lại.
   - Mong đợi: nền OFF giữ V2, nhà khác không nhận gradient, không giữ nhầm mask khi ra/vào.

User chưa test tay candidate này. Automated pass và self-review ảnh không phải nghiệm thu. Chưa kiểm chứng full suite, build, Host/Client, late join/reconnect, soak hoặc dynamic atlas.

Runtime cuối đang Play tại nhà mẫu. `shadow-final-review-ready-state.txt` xác nhận HP100, GodMode ON, đèn pin OFF, movement ON và cheat menu đóng. Input `W` thật đã dịch Y44.30→44.32 trong `shadow-final-input-after-w-state.txt`, rồi fixture được đặt lại đúng pose và chuẩn bị final state lần nữa.

## Git

Base/HEAD `ee0554d149a15fd89b3a370fab974058f235e668`, branch local `codex/restore-indoor-vision-20260831`, upstream đã bị xóa ngoài phiên. Candidate và QA/docs đang dirty local; không commit/push/merge. Bản radial bị loại và ảnh test được giữ, không reset/clean.
