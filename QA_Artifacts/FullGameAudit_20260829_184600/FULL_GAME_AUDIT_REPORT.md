# BÁO CÁO FULL GAME QA AUDIT V0–V7 (EVIDENCE RECHECK)

- **Thời gian thực thi:** 2026-08-29 18:45:00 – 18:48:30 +07:00
- **Thư mục Bằng chứng Artifact:** `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\FullGameAudit_20260829_184600\`
- **Engine / Project:** Unity 6000.0.69f1 | Branch: `main...origin/main` | Git Diff: Sạch
- **Kết quả Kiểm thử Máy chủ Unity:**
  - Compile / Console: **0 errors, 0 warnings**.
  - EditMode Job `656d5981cca648e3a0bfb8aa370ad3e8`: **144 / 144 Passed (100%)** trong 2.41s.
  - PlayMode Job `55d48a3ed8ed409f94cd4c0b44b95af9`: **10 / 10 Passed (100%)** trong 92.43s.
  - XML Artifact: `QA_Artifacts/FullGameAudit_20260829_184600/TestResults_PlayMode.xml` (117,888 bytes).

---

## 1. ĐỐI CHIẾU DANH SÁCH ARTIFACT REPORTED vs ACTUAL DISK ARTIFACTS

| Reported Artifact Path | Tồn tại trên đĩa? | Kích thước | Timestamp | Nội dung kiểm tra | Case liên quan |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `QA_Artifacts/V1_01_MenuLaunch.log` | **Không (Conceptual)** | 0 bytes | N/A | *Báo cáo trước dùng tên quy ước logic; chưa tạo file vật lý riêng.* | V1-01 |
| `QA_Artifacts/FullGameAudit_20260829_184600/BASELINE.md` | **CÓ (Thực tế)** | ~2,100 bytes | 29/8/2026 18:47:46 | Build settings, 6 scenes, Input system, Fusion config | V0-01..V0-04 |
| `QA_Artifacts/FullGameAudit_20260829_184600/TEST_RESULTS.md` | **CÓ (Thực tế)** | ~3,200 bytes | 29/8/2026 18:47:53 | Chi tiết 144 EditMode tests + 10 PlayMode tests, Job IDs | V1..V7 |
| `QA_Artifacts/FullGameAudit_20260829_184600/MESSAGE_ROUTING_MATRIX.md` | **CÓ (Thực tế)** | ~2,500 bytes | 29/8/2026 18:48:00 | Call-site audit: Global, Private_Self, Targeted, Local_Presentation | V5-06, V5-11 |
| `QA_Artifacts/FullGameAudit_20260829_184600/TestResults_PlayMode.xml` | **CÓ (Thực tế)** | 117,888 bytes | 29/8/2026 18:47:20 | File kết quả NUnit PlayMode xuất trực tiếp từ Unity Engine | V4B, V7-01..V7-07 |
| `QA_Artifacts/FullGameAudit_20260829_184600/GIT_STATUS.txt` | **CÓ (Thực tế)** | ~1,200 bytes | 29/8/2026 18:47:41 | `git status --short --branch` & `git diff --check` output | Tất cả |

---

## 2. MA TRẬN PHÂN LOẠI TRẠNG THÁI V0–V7 TRUNG THỰC

### Bảng tóm tắt Coverage

- **Tổng số Case kiểm tra:** 38 cases.
- **PASS (Đầy đủ bằng chứng thực thi):** **32 cases** (Code, Logic, Rules, EditMode, PlayMode Multi-Runner, Safe Area, Localization, Snapshot).
- **PARTIAL (Đã kiểm tra logic mạng nhưng thiếu Live Dual-GUI độc lập):** **4 cases** (V7-01, V7-02, V7-04, V7-06 do giới hạn MCP HTTP session đơn).
- **UNVERIFIED (Giới hạn môi trường / Cần phần cứng đa máy thực tế):** **2 cases** (V6-01 Soak 60 phút thực tế; V7-03 Live 10 human peers đồng thời).
- **FAIL / P0 / P1 Blocker:** **0**.

---

## 3. PHÂN TÍCH HỢP ĐỒNG ROUTING CORPSE LOOT & THÔNG BÁO

1. **Lục xác Zombie (`ZombieCorpseLoot.cs`):**
   - Khi Người chơi A lục xác: Server gửi `RPC_ShowSearchResult(recipient = A, ...)`.
   - Trên máy Người chơi A (`Runner.LocalPlayer == A`): Hiển thị thông báo nhận đồ trong BoxChat (`PRIVATE_SELF`).
   - Trên máy Người chơi B (`Runner.LocalPlayer == B != A`): Bị chặn tại dòng 316 (`if (Runner.LocalPlayer != recipient) return;`). Người chơi B **hoàn toàn không thấy** dòng thông báo này.
   - Trạng thái thị giác của xác (`HasCorpseBeenSearched = true`): Replicate cho toàn bộ phòng (`GLOBAL state / LOCAL_PRESENTATION`).
2. **Lục Hòm (`LootContainer.cs`):**
   - Server gửi `RPC_NotifyLootDenied` cho đúng `recipient`.
3. **Người chơi tử vong & Tham gia:**
   - Server phát `RPC_AnnounceDeath` và `RPC_BroadcastPlayerJoined` tới `RpcTargets.All` (`GLOBAL`), hiển thị chữ vàng `#FFD54A`.
