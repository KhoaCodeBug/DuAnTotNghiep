# Fragments of Survival — UML diagrams

File chính: `Fragments_of_Survival_UseCase_Activity.drawio`

## Danh sách page

1. Use Case tổng quan
2. Main Menu chi tiết
3. Chơi đơn
4. Multiplayer Host và Client
5. Hướng dẫn độc lập
6. Khám phá và thế giới
7. Sinh tồn và sức khỏe
8. Vật phẩm và chiến đấu
9. Tương tác Multiplayer
10. Main Quest phần 1
11. Main Quest finale
12. Options và Pause
13. Activity Flow toàn game
14. Activity Flow Tutorial
15. Activity Flow Main Play

## Quy ước UML

- Đường liền có mũi tên: actor sử dụng chức năng.
- `<<include>>`: bước bắt buộc hoặc chức năng được tái sử dụng.
- `<<extend>>`: hành vi tùy chọn hoặc chỉ xảy ra khi có điều kiện.
- Đường liền với tam giác rỗng: kế thừa actor.
- Note dùng hình ghi chú và nối bằng nét đứt không mũi tên.
- Activity Flow dùng nút hành động, nút quyết định hình thoi và mũi tên chỉ thứ tự.

## Cập nhật file

Chạy `node generate_game_diagrams.js` trong thư mục này sau khi chỉnh dữ liệu trong generator.
