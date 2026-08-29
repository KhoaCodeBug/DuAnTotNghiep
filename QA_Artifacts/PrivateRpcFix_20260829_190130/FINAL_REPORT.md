# BÁO CÁO HOÀN TẤT SỬA LỖI PRIVATE RPC CORPSE LOOT (P2)

- **Thời gian hoàn tất:** 2026-08-29 19:06:00 +07:00
- **Trạng thái:** **`FIXED`**
- **Thư mục Artifact:** `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcFix_20260829_190130\`

---

## 1. BẢNG TỔNG HỢP KIỂM CHỨNG THEO TIÊU CHÍ (VERIFICATION TABLE)

| Check | Expected | Actual evidence | Status |
| :--- | :--- | :--- | :--- |
| **Private RPC target** | Unicast only via `[RpcTarget]` | `ZombieCorpseLoot.cs:318` có `[RpcTarget] PlayerRef recipient`, IL Weaver biên dịch sạch | **PASS** (`CODEGEN & TEST`) |
| **B/C private payload** | Not received over transport | Gói tin RPC được định tuyến đích danh (unicast); không broadcast dữ liệu `itemId/amount` | **PASS** (`STATIC & CODEGEN`) |
| **A BoxChat** | One local message | `AutoChatManager.Instance.AddSystemMessage` chỉ chạy trên máy actor (`Runner.LocalPlayer == recipient`) | **PASS** (`RUNTIME`) |
| **Global corpse visual** | Replicated to all peers | Biến `[Networked] HasCorpseBeenSearched` đồng bộ trạng thái xác tới toàn bộ peer | **PASS** (`RUNTIME`) |
| **Race/duplicate grant** | One authoritative grant | Test `NetworkAuthorityRegressionTests.CorpseLootRaceBetweenTwoPeersGrantsOnlyOnce` | **PASS** (`PLAYMODE TEST`) |
| **Compile/tests** | No new errors (100% pass) | EditMode: **145/145 Passed** (3.41s); PlayMode: **10/10 Passed** (117.72s) | **PASS** (`XML & CONSOLE`) |

---

## 2. DIFF THỰC TẾ TRÊN MÃ NGUỒN

### `Assets/Script/Tin/ZombieCorpseLoot.cs`
```diff
@@ -304,18 +304,17 @@
     [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
     private void RPC_ShowSearchResult(
-        PlayerRef recipient,
+        [RpcTarget] PlayerRef recipient,
         int resultValue,
         string itemId,
-        int amount,
-        NetworkBool corpseWasSearched)
+        int amount)
     {
-        if (corpseWasSearched) locallyKnownSearched = true;
         if (Runner == null || Runner.LocalPlayer != recipient) return;

         isAwaitingSearchResult = false;
-        if (!corpseWasSearched) locallyKnownSearched = false;

         string message = BuildLocalResultMessage((SearchResult)resultValue, itemId, amount);
-        AutoChatManager.Instance?.AddSystemMessage(message);
+        if (!string.IsNullOrEmpty(message))
+        {
+            AutoChatManager.Instance?.AddSystemMessage(message);
+        }
     }
```

### `Assets/Script/Tin/Prototype/Tests/Editor/ReadinessAndChatEditorTests.cs`
- Thêm unit test `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature` để tự động kiểm tra `[RpcTarget]` và danh sách tham số qua reflection.

---

## 3. DANH MỤC FILE ARTIFACT ĐÃ TẠO TRÊN ĐĨA

1. `BASELINE_GIT_STATUS.txt`
2. `CHANGED_FILES.txt`
3. `STATIC_RPC_VERIFICATION.md`
4. `TEST_RESULTS.xml` (117,888 bytes)
5. `RUNTIME_ROUTING_EVIDENCE.md`
6. `UNITY_COMPILE_AND_CONSOLE.log`
7. `FINAL_REPORT.md`
