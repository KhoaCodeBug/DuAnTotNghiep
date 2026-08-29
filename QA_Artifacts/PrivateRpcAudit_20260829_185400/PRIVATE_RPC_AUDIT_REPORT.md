# BÁO CÁO KIỂM TOÁN CHUYÊN SÂU: PRIVATE RPC CỦA ZOMBIE CORPSE LOOT

- **Thời gian thực thi:** 2026-08-29 18:54:00 +07:00
- **Phạm vi kiểm toán:** Xác minh tính bảo mật mạng và định tuyến RPC của hàm `RPC_ShowSearchResult` trong `Assets/Script/Tin/ZombieCorpseLoot.cs`.
- **Thư mục Artifact:** `E:\Unity\GameObject\Game3D\ProJectZomboiNhai\QA_Artifacts\PrivateRpcAudit_20260829_185400\`

---

## 1. KẾT LUẬN KIỂM TOÁN TỔNG QUAN

- **Trạng thái:** **`FAIL` (P2 - Network Protocol Privacy Defect)**.
- **Tóm tắt phát hiện:**
  - **Tầng Hiển thị (Presentation Layer):** **PASS**. Người chơi B/C không nhìn thấy text thông báo nhặt đồ hay số lượng của Người chơi A trong BoxChat UI nhờ bộ lọc `if (Runner.LocalPlayer != recipient) return;`.
  - **Tầng Giao thức Mạng (Network Transport Layer):** **FAIL (Lỗ hổng over-broadcast)**. Do khai báo `[Rpc(RpcSources.StateAuthority, RpcTargets.All)]` và **thiếu** thuộc tính `[RpcTarget]` trước tham số `PlayerRef recipient`, Photon Fusion 2 serialize và phát tán toàn bộ gói tin nhặt đồ (bao gồm `itemId` và `amount`) qua mạng tới tất cả các client kết nối trong phòng.
  - Các client khác (B/C) nhận được gói tin, deserialize đầy đủ tên vật phẩm và số lượng vào bộ nhớ trước khi câu lệnh `return` ở dòng 313 loại bỏ chúng khỏi UI.

---

## 2. NGUYÊN NHÂN GỐC RỄ (SINGLE ROOT CAUSE)

- **File & Line:** [`Assets/Script/Tin/ZombieCorpseLoot.cs:304-320`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/Assets/Script/Tin/ZombieCorpseLoot.cs#L304-L320).
- **Phân tích Root Cause:**
  Tác giả đã ghép **hai trách nhiệm mạng khác nhau** vào cùng một hàm RPC:
  1. *Cập nhật trạng thái thị giác của xác zombie trên toàn thế giới* (`locallyKnownSearched = true` $\rightarrow$ cần gửi tới mọi peer).
  2. *Gửi thông báo kết quả loot riêng tư và số lượng vật phẩm* (`itemId`, `amount` $\rightarrow$ chỉ được gửi tới người nhặt).

  Do cần thực hiện (1) trên toàn bộ client, hàm đã dùng `RpcTargets.All` mà không dùng `[RpcTarget]` trên `recipient`. Điều này vô tình biến thông tin riêng tư (2) thành gói tin broadcast trên mạng.

---

## 3. ĐỐI CHIẾU VỚI CÁC RPC CHUẨN TRONG PROJECT

Trong [`Assets/Khoa/Code/LootContainer.cs:621-645`](file:///e:/Unity/GameObject/Game3D/ProJectZomboiNhai/Assets/Khoa/Code/LootContainer.cs#L621-L645), hệ thống Loot Container đã cài đặt chuẩn xác:
```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_NotifyLootGranted([RpcTarget] PlayerRef targetPlayer, string itemId, int amount)
```
- Khi có `[RpcTarget]` trên tham số `PlayerRef targetPlayer`, bộ biên dịch IL/Weaver của Photon Fusion 2 tự động định tuyến gói tin **unicast** chỉ tới socket mạng của `targetPlayer`. Các client khác hoàn toàn không nhận được gói tin này trên đường truyền mạng.

---

## 4. ĐỀ XUẤT SỬA ĐỔI TỐI THIỂU CHO CODEX (PROPOSED MINIMAL FIX)

Tách biệt rõ ràng 2 trách nhiệm:
1. **Phần Thông báo Riêng tư (Private Unicast):** Thêm thuộc tính `[RpcTarget]` vào tham số `recipient` trong `RPC_ShowSearchResult` và bỏ tham số `corpseWasSearched`:
   ```csharp
   [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
   private void RPC_ShowSearchResult(
       [RpcTarget] PlayerRef recipient,
       int resultValue,
       string itemId,
       int amount)
   {
       if (Runner == null || Runner.LocalPlayer != recipient) return;
       isAwaitingSearchResult = false;
       string message = BuildLocalResultMessage((SearchResult)resultValue, itemId, amount);
       AutoChatManager.Instance?.AddSystemMessage(message);
   }
   ```
2. **Phần Thị giác Toàn thể (Global Visual State):**
   Xác zombie đã sở hữu thuộc tính mạng `[Networked] public NetworkBool HasCorpseBeenSearched { get; set; }`. Khi State Authority gán `HasCorpseBeenSearched = true` trong `ConsumeCorpse()`, Fusion tự động replicate biến này tới mọi peer; trong `Render()` hoặc qua OnChanged callback, client tự cập nhật `locallyKnownSearched = true` và đổi sprite mà không cần truyền kèm dữ liệu loot.
