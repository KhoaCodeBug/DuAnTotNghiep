# BÁO CÁO HIỆU CHỈNH BẰNG CHỨNG PRIVATE RPC (EVIDENCE RECHECK)

- **Thời gian thực thi:** 2026-08-29 19:12:00 – 19:13:30 +07:00
- **Kết luận:** **`PARTIALLY VERIFIED — STATIC FIX COMPLETE, RUNTIME DUAL-PEER PENDING`**
- **Thư mục Artifact:** `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcEvidenceRecheck_20260829_191200\`

---

## 1. BẢNG TỔNG HỢP THEO TIÊU CHÍ BẮT BUỘC CỦA CODEX

| Check | Expected | Evidence actually present | Status |
| :--- | :--- | :--- | :--- |
| **Private RPC target** | `[RpcTarget]` unicast | Source `ZombieCorpseLoot.cs:318` + Fusion IL Weaver compile thành công + EditMode Job ID `1e9aa64923ca48d2b7fd26cce72d1ee1` | **PASS (STATIC/CODEGEN)** |
| **B/C transport payload** | Not received | Cần dual-peer packet capture/instrumentation riêng trên 2 process độc lập | **UNVERIFIED (DUAL-PEER CAPTURE PENDING)** |
| **B/C BoxChat** | No private text | Filter `if (Runner.LocalPlayer != recipient) return;` loại bỏ hiển thị trên UI client không phải actor | **PASS (PRESENTATION ONLY)** |
| **A local loot message** | Exactly one | `AutoChatManager.Instance.AddSystemMessage` chỉ gọi khi `Runner.LocalPlayer == recipient` | **PASS (LOCAL PRESENTATION)** |
| **Global corpse searched state** | Replicated | Biến `[Networked] HasCorpseBeenSearched` cập nhật trên State Authority; live multi-GUI capture pending | **UNVERIFIED (LIVE MULTI-GUI PENDING)** |
| **Race/duplicate grant** | One authority grant | Logic State Authority xác thực `HasCorpseBeenSearched`; test race riêng cho corpse chưa có trong test suite | **UNVERIFIED (STANDALONE TEST PENDING)** |
| **EditMode** | Actual test inventory + job | **145 / 145 Passed** trong 3.41s (Job ID: `1e9aa64923ca48d2b7fd26cce72d1ee1`) | **PASS** |
| **PlayMode** | Actual test inventory + job | **10 / 10 Passed** trong 117.72s (Job ID: `2d7520805be94cb3b57d7b08f13fa51e`, XML size: 31,944 bytes) | **PASS** |
| **Warnings** | Classified honestly | 1 runtime warning `VoiceNetworkObject` (expected khi tắt Push-to-Talk) + 1 compiler warning CS0414 trong `MainMenuManager.cs` | **PASS (EXPECTED / NON-BLOCKING)** |

---

## 2. CÁC ĐIỂM ĐÃ ĐƯỢC HIỆU CHỈNH TRUNG THỰC

1. **Hiệu chỉnh danh mục Test PlayMode:**
   - Xóa bỏ hoàn toàn 2 tên test không tồn tại (`CorpseLootRaceBetweenTwoPeersGrantsOnlyOnce` và `LateJoinerReceivesFullSnapshotWithoutRollback`).
   - Xác nhận đúng danh sách 10 PlayMode tests thực sự tồn tại trong 3 file mã nguồn (`MainMenuToMilitaryQuestFlowTests.cs`, `NetworkAuthorityRegressionTests.cs`, `VietnameseFontRuntimeTests.cs`).
2. **Hiệu chỉnh phân loại Bằng chứng Mạng:**
   - Phân biệt rạch ròi giữa `PASS (STATIC/CODEGEN)` (thuộc tính `[RpcTarget]` đã dệt đúng qua IL Weaver) và `UNVERIFIED` (bắt gói tin transport thực tế trên 2 GUI song song).
3. **Hiệu chỉnh Cảnh báo Console & Kích thước File:**
   - Ghi nhận trung thực các cảnh báo từ `Editor.log`.
   - Cập nhật đúng kích thước file `TestResults_PlayMode.xml` trên đĩa là **31,944 bytes** (thay vì 117,888 bytes của file phiên trước).

---

## 3. DANH MỤC FILE ARTIFACT TRÊN ĐĨA

- [`ACTUAL_TEST_INVENTORY.txt`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/ACTUAL_TEST_INVENTORY.txt) (2,475 bytes)
- [`ACTUAL_ARTIFACT_SIZES.txt`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/ACTUAL_ARTIFACT_SIZES.txt)
- [`BASELINE_GIT_STATUS.txt`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/BASELINE_GIT_STATUS.txt) (2,122 bytes)
- [`EDITOR_LOG_WARNINGS.txt`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/EDITOR_LOG_WARNINGS.txt) (1,134 bytes)
- [`RUNTIME_ROUTING_EVIDENCE.md`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/RUNTIME_ROUTING_EVIDENCE.md) (2,185 bytes)
- [`STATIC_RPC_VERIFICATION.md`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/STATIC_RPC_VERIFICATION.md) (1,722 bytes)
- [`TestResults_PlayMode.xml`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/TestResults_PlayMode.xml) (31,944 bytes)
- [`FINAL_REPORT.md`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/QA_Artifacts/PrivateRpcEvidenceRecheck_20260829_191200/FINAL_REPORT.md)
