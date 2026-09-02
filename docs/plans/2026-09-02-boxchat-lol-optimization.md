# BoxChat LoL-Style Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Làm BoxChat hoạt động theo mô hình LoL: tin nhắn đến chỉ hiện chữ; chỉ hiện khung khi nhập/kéo; thả kéo xong có thể gõ ngay.

**Architecture:** Tách rõ ba trạng thái `Hidden`, `TextOnly` và `Editing/Dragging`. Bỏ hoàn toàn HeaderBar; dùng vùng chat khi đang nhập làm drag handle, nhưng không để thao tác kéo kích hoạt `onEndEdit` hoặc đóng InputField.

**Tech Stack:** Unity uGUI `InputField`, `EventSystem`, `CanvasGroup`, `ScrollRect`, Photon Fusion 2 RPC, Unity Test Framework.

---

### Task 1: Sửa InputField và luồng focus

**Files:**
- Modify: `Assets/Script/Tin/AutoChatManager.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`

**Steps:**

1. Gán lại `chatInput.textComponent = inputText` sau khi tạo text component.
2. Tạo test xác nhận InputField có `textComponent`, mở chat sẽ focus InputField và nhập được text.
3. Chạy test để xác nhận lỗi hiện tại được bắt.
4. Sửa tối thiểu code và chạy lại EditMode test.

### Task 2: Sửa kéo thả không làm đóng chat

**Files:**
- Modify: `Assets/Script/Tin/AutoChatManager.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`

**Steps:**

1. Khi bắt đầu kéo, đánh dấu trạng thái `Dragging` và tạm ngăn `onEndEdit` xử lý như submit/hủy.
2. Khi thả, giữ trạng thái nhập, lưu vị trí và clamp theo Canvas/safe area.
3. Focus lại InputField sau sự kiện kéo, không yêu cầu nhấn Enter lần hai.
4. Tạo test cho chuỗi: `OpenChat → BeginDrag → EndDrag → IsTyping vẫn true → InputField vẫn được chọn`.
5. Chạy EditMode test.

### Task 3: Bỏ Header và tách hiển thị TextOnly/Editing

**Files:**
- Modify: `Assets/Script/Tin/AutoChatManager.cs`
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`

**Steps:**

1. Xóa việc tạo và sử dụng `HeaderBar`; không hiển thị chữ “CHAT” hoặc “Drag to move”.
2. Khi nhận system/player message trong trạng thái đóng: chỉ bật text, nền/frame/input phải tắt và không chặn raycast.
3. Khi nhấn Enter hoặc đang kéo: bật nền/frame/input; vùng chat có thể nhận drag.
4. Sau khi gửi hoặc Escape: tắt editor/frame nhưng giữ lịch sử text theo thời gian fade.
5. Tạo test riêng cho `TextOnly`, `Editing`, `Dragging` và không để một CanvasGroup chung làm sai trạng thái.

### Task 4: Kiểm tra phạm vi Multiplayer

**Files:**
- Review/Modify only if needed: `Assets/Script/Tin/Multiplayer/PlayerInputHandler2D.cs`
- Review/Modify only if needed: `Assets/Script/Tin/Multiplayer/HostModeSpawner.cs`

**Steps:**

1. Xác nhận chỉ InputAuthority gửi chat.
2. Xác nhận StateAuthority nhận, kiểm tra và broadcast đúng một lần.
3. Xác nhận Host và Client đều gọi `AddPlayerMessage` trên BoxChat local.
4. Không thay đổi RPC nếu luồng hiện tại đã đúng; tránh sửa lan sang movement/voice.

### Task 5: Kiểm thử và bằng chứng

**Files:**
- Test: `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`
- Test: các PlayMode test liên quan network/readiness hiện có

**Steps:**

1. Chạy compile check và EditMode tests.
2. Chạy PlayMode tests; ghi rõ số lượng test thực tế, không dùng số liệu cũ trong kế hoạch.
3. Rebuild Windows từ source hiện tại, không dùng `Builds/Playtest/ProJectZomboiNhai.exe` cũ.
4. Test hai instance trong cùng SessionName:
   - Host Create Room.
   - Client Join đúng room.
   - Xác nhận Players `2/4` trước khi chat.
5. Chụp bằng chứng cho bốn trạng thái: text-only, editing, dragging, thả kéo rồi gõ ngay.
6. Kiểm tra Unity Console và ghi lại lỗi còn tồn tại.

**Constraints:** Giữ nguyên thay đổi người dùng, không reset/revert/xóa artifact, không sửa Header theo hướng localization vì Header sẽ bị loại bỏ hoàn toàn, không commit/push nếu chưa được yêu cầu.
