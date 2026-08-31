# Indoor Fog — ảnh mục tiêu đã duyệt kèm hiệu chỉnh

Ngày: 2026-08-31. Nguồn bàn giao canonical: CODEX_PROJECT_WORK_LOG.md.

## Cập nhật phê duyệt 2026-08-31

User đã duyệt hai ảnh và cho triển khai thử. Vùng tường bên phải trong ảnh ON cần tối sớm hơn một chút; ảnh OFF mô tả buổi tối, ban ngày sáng nhẹ hơn. Không cần vẽ lại. Ảnh phản hồi: `approved-v1-narrower-edge.png`. Prototype và tự đánh giá mới nhất ở `../IndoorFogPrototype_20260831/README.md`; work log là nguồn canonical. Chưa có nghiệm thu gameplay prototype.

## Trạng thái và phạm vi lúc tạo ảnh (lịch sử)

- **Đã duyệt:** tạo ảnh mô phỏng trước khi code; chỉ triển khai thử sau khi người dùng duyệt hình ảnh.
- **Đã tạo, chưa duyệt:** hai ảnh mục tiêu bằng built-in image_gen. Không phải ảnh chụp kết quả triển khai Unity. Chưa sửa code, shader, scene, prefab hay network.
- Giữ checkpoint `ddf440424`, nhánh `codex/checkpoint-vision-menu-stable-20260831`. Nhánh làm việc `codex/restore-indoor-vision-20260831`; không push hoặc merge.

## Tệp để xem và tiếp nối

- `reference-original.png`: ảnh game gốc do người dùng cung cấp; bản sao bền vững ngoài Temp.
- `reference-marked.png`: ảnh người dùng khoanh mặt tường và tủ cần sửa.
- `concept-v1-flashlight-on.png`: đề xuất khi bật đèn pin; mặt tường, tranh và thân tủ phía trước hiện đầy đủ hơn, chuyển độ sáng mềm.
- `concept-v1-flashlight-off.png`: đề xuất khi tắt đèn; cùng vùng nhìn hiện mờ và yếu hơn đáng kể.
- `PROMPTS.md`: nguyên văn hai prompt; ảnh ON dùng hai ảnh gốc, OFF chỉnh tiếp ảnh ON để giữ góc nhìn.

## Tự đánh giá ảnh, không phải QA gameplay

- Đã xem trực tiếp cả hai ảnh sinh ra: mặt tường phía sau và mặt tủ bên phải không còn bị cắt tối mạnh ở phần trên; bản OFF tối rõ rệt hơn bản ON nhưng còn đọc được bề mặt.
- Bản ON thể hiện vùng sáng khá rộng; cần người dùng duyệt độ rộng và mức sáng. Đây chưa phải thông số góc đèn hoặc cường độ đã chốt.
- AI có thể vẽ lại chi tiết sprite, HUD và thay đổi kích thước ảnh; không dùng để thay asset hoặc yêu cầu game khớp từng pixel. Đích so sánh là bề mặt nào hiện, mức sáng tương đối và cách chuyển tối.
- Các số FPS/giờ trên ảnh là nội dung ảnh gốc hoặc được mô hình tái tạo, **không phải phép đo hiệu năng**.
- Ảnh tĩnh không chứng minh không xuyên tường, không nhấp nháy, không trễ khi quay hoặc network đúng. Chưa chạy Unity QA mới trong lượt này.

## Sau khi người dùng duyệt

1. Ghi chính xác ảnh/điều chỉnh được duyệt vào work log; chưa tự coi việc tạo ảnh thành phê duyệt triển khai.
2. Làm thử trên một nhà, giữ hành vi ngoài nhà và hệ thống liên quan; kiểm tra dữ liệu bề mặt/Tilemap trước khi chọn cách làm. Không tự tái cấu trúc lớn.
3. Chụp runtime cùng góc nhìn, vị trí, hướng, zoom và giờ để đối chiếu ảnh mục tiêu ON/OFF. Kiểm tra tường, tranh, tủ, sàn và phòng khuất; thử quay nhanh, cửa/góc tường, vào/ra nhà và ngày/đêm. Đánh giá Host/Client theo thay đổi thực tế.
4. Đo CPU/GPU frame time trước–sau trong điều kiện tương đương. Không suy ra FPS từ ảnh concept.
5. Báo sai lệch và nguyên nhân qua từng vòng. User muốn cân nhắc dừng sau vài lần nếu không tái hiện được hoặc logic phức tạp; đề xuất giới hạn tối đa ba vòng prototype, chưa phải số vòng user đã xác nhận. Không mở rộng map khi chưa đủ bằng chứng.
6. Nếu cần dừng, bảo toàn thay đổi ngoài thử nghiệm và dùng checkpoint để phục hồi có kiểm soát; không reset/clean toàn repo.

## Điểm chờ người dùng lúc tạo ảnh (đã được cập nhật ở trên)

- Duyệt hoặc điều chỉnh độ sáng khi bật/tắt đèn, mức hiện của mặt tường/tủ và độ mềm.
- Chưa duyệt ảnh; chưa triển khai prototype; chưa có nghiệm thu gameplay mới.
