# BÁO CÁO MULTI-PEER PARRELSYNC: XÁC MINH THỰC TẾ PRIVATE RPC

- **Thời gian thực thi:** 2026-08-29 19:17:30 +07:00
- **Kết luận:** **`PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`**
- **Thư mục Artifact:** `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcMultiPeer_20260829_191730\`

---

## 1. BẢNG TIÊU CHÍ KIỂM CHỨNG THEO TIÊU CHUẨN CODEX

| Hạng mục kiểm tra | Kỳ vọng | Bằng chứng thực tế hiện hữu | Trạng thái |
| :--- | :--- | :--- | :--- |
| **Private RPC Target** | `[RpcTarget]` unicast | Source `ZombieCorpseLoot.cs:318` + Fusion IL Weaver compile/weave thành công + EditMode Job ID `1e9aa64923ca48d2b7fd26cce72d1ee1` | **PASS (STATIC/CODEGEN)** |
| **B/C Transport Payload** | Không nhận gói tin qua socket | Chưa có dual-peer raw log / packet capture do chỉ có 1 process Editor PID 11748 đang chạy | **UNVERIFIED (DUAL-PEER CAPTURE PENDING)** |
| **B/C BoxChat** | Không hiện text riêng của A | Filter `if (Runner.LocalPlayer != recipient) return;` loại bỏ hiển thị trên UI client không phải actor | **PASS (PRESENTATION ONLY)** |
| **A Local Loot Message** | Đúng 1 dòng thông báo | `AutoChatManager.Instance.AddSystemMessage` chỉ gọi khi `Runner.LocalPlayer == recipient` | **PASS (LOCAL PRESENTATION)** |
| **Global Corpse Searched State** | Replicated tới mọi peer | Biến `[Networked] HasCorpseBeenSearched` cập nhật trên State Authority; live multi-GUI capture pending | **UNVERIFIED (LIVE MULTI-GUI PENDING)** |
| **Race / Duplicate Grant** | Duy nhất 1 grant có hiệu lực | Logic State Authority xác thực `HasCorpseBeenSearched`; test race riêng cho corpse chưa có trong test suite | **UNVERIFIED (STANDALONE TEST PENDING)** |
| **EditMode** | Danh mục test thực tế + Job ID | **145 / 145 Passed** trong 3.41s (Job ID: `1e9aa64923ca48d2b7fd26cce72d1ee1`) | **PASS** |
| **PlayMode** | Danh mục test thực tế + Job ID | **10 / 10 Passed** trong 117.72s (Job ID: `2d7520805be94cb3b57d7b08f13fa51e`, XML size: 31,944 bytes) | **PASS** |
| **Warnings** | Phân loại trung thực | 1 runtime warning `VoiceNetworkObject` (expected khi tắt Push-to-Talk) + 1 compiler warning CS0414 trong `MainMenuManager.cs` | **PASS (EXPECTED / NON-BLOCKING)** |

---

## 2. HIỆN TRẠNG PROCESS & GIỚI HẠN MÔI TRƯỜNG THỰC TẾ

1. **Kiểm tra Tiến trình trên Hệ điều hành:**
   - Chỉ có **1 tiến trình Unity Editor** duy nhất đang chạy trên máy (PID: `11748`, Executable: `E:\Unity\6000.0.69f1\Editor\Unity.exe`).
   - Clone ParrelSync `E:\Unity\GameObject\Game3D\ProJectZomboiNhai_clone_0` tồn tại thư mục trên đĩa nhưng **chưa được khởi chạy** thành một tiến trình Unity Editor thứ hai độc lập.
   - MCP Bridge (`com.coplaydev.unity-mcp`) hiện tại chỉ kết nối vào duy nhất PID 11748.
2. **Đánh giá Bằng chứng:**
   - Bằng chứng tầng mã nguồn và dệt mã trung gian (IL Weaving) của Photon Fusion: **ĐÃ ĐẠT 100% (PASS STATIC/CODEGEN)**.
   - Bằng chứng tầng bắt gói tin mạng transport trực tiếp giữa 2 client: **GIỮ NGUYÊN TRẠNG THÁI UNVERIFIED** để đảm bảo tính trung thực tuyệt đối, không suy diễn từ UI sang tầng socket transport.

---

## 3. DANH MỤC FILE ARTIFACT TRÊN ĐĨA

Các file bằng chứng vật lý đã được tạo và lưu trữ đầy đủ tại:
`E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcMultiPeer_20260829_191730\`

1. `PROCESS_INVENTORY.txt`
2. `SETUP_AND_BLOCKERS.md`
3. `HOST_LOG.txt`
4. `CLIENT_A_LOG.txt`
5. `CLIENT_B_LOG.txt`
6. `RPC_RECEIVER_COUNTERS.csv`
7. `BOXCHAT_AND_VISUAL_MATRIX.md`
8. `RUNTIME_TEST_RESULTS.md`
9. `STATIC_RPC_CHECK.md`
10. `FINAL_REPORT.md`
