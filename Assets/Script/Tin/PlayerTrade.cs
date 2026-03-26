using Fusion;
using UnityEngine;

public class PlayerTrade : NetworkBehaviour
{
    [Header("Tầm xa giao dịch")]
    public float tradeRadius = 2f;

    [Networked] public NetworkBool IsTrading { get; set; }
    [Networked] public PlayerRef TradePartner { get; set; }
    [Networked] public NetworkString<_64> OfferItemName { get; set; }
    [Networked] public int OfferAmount { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsConfirmed { get; set; }

    private void Update()
    {
        if (!HasInputAuthority) return;
        if (Input.GetKeyDown(KeyCode.T) && !IsTrading) SendTradeRequest();
    }

    private void SendTradeRequest()
    {
        PlayerTrade[] allPlayers = FindObjectsByType<PlayerTrade>(FindObjectsSortMode.None);
        foreach (PlayerTrade otherPlayer in allPlayers)
        {
            if (otherPlayer == this) continue;
            if (Vector2.Distance(transform.position, otherPlayer.transform.position) <= tradeRadius && !otherPlayer.IsTrading)
            {
                RPC_SendRequest(Object.InputAuthority, otherPlayer.Object.InputAuthority);
                break;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendRequest(PlayerRef sender, PlayerRef target)
    {
        if (Runner.LocalPlayer == target && AutoUIManager.Instance != null)
            AutoUIManager.Instance.ShowTradeRequestPopup(sender, target);
    }

    public void AcceptTradeRequest(PlayerRef sender) { RPC_AcceptTrade(sender, Runner.LocalPlayer); }
    public void DeclineTradeRequest(PlayerRef sender) { RPC_DeclineTrade(sender); }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AcceptTrade(PlayerRef sender, PlayerRef receiver)
    {
        PlayerTrade p1 = GetPlayerTrade(sender);
        PlayerTrade p2 = GetPlayerTrade(receiver);

        if (p1 != null && p2 != null)
        {
            p1.IsTrading = true; p1.TradePartner = receiver;
            p2.IsTrading = true; p2.TradePartner = sender;
            p1.ResetTradeData(); p2.ResetTradeData();

            RPC_OpenTradeWindow(sender);
            RPC_OpenTradeWindow(receiver);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_DeclineTrade(PlayerRef sender)
    {
        if (Runner.LocalPlayer == sender) Debug.Log("❌ Bị từ chối!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OpenTradeWindow(PlayerRef target)
    {
        if (Runner.LocalPlayer == target && AutoUIManager.Instance != null)
            AutoUIManager.Instance.ShowTradeWindow();
    }

    public void ResetTradeData()
    {
        OfferItemName = ""; OfferAmount = 0;
        IsReady = false; IsConfirmed = false;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetOffer(NetworkString<_64> itemName, int amount)
    {
        OfferItemName = itemName; OfferAmount = amount;
        IsReady = false; IsConfirmed = false;

        PlayerTrade partner = GetPlayerTrade(TradePartner);
        if (partner != null) { partner.IsReady = false; partner.IsConfirmed = false; }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ToggleReady() { IsReady = !IsReady; IsConfirmed = false; }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ConfirmTrade()
    {
        if (!IsReady) return;
        IsConfirmed = true;

        PlayerTrade partner = GetPlayerTrade(TradePartner);
        if (partner != null && partner.IsConfirmed)
        {
            ExecuteTrade(this, partner);
        }
    }

    public void CancelTrade() { RPC_CancelTrade(); }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_CancelTrade()
    {
        PlayerTrade partner = GetPlayerTrade(TradePartner);

        this.IsTrading = false; this.ResetTradeData();
        if (partner != null) { partner.IsTrading = false; partner.ResetTradeData(); }

        RPC_CloseTradeWindow(this.Object.InputAuthority);
        if (partner != null) RPC_CloseTradeWindow(partner.Object.InputAuthority);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CloseTradeWindow(PlayerRef target)
    {
        if (Runner.LocalPlayer == target && AutoUIManager.Instance != null)
            AutoUIManager.Instance.HideTradeWindow();
    }

    private void ExecuteTrade(PlayerTrade p1, PlayerTrade p2)
    {
        string item1Name = p1.OfferItemName.ToString();
        string item2Name = p2.OfferItemName.ToString();
        int amount1 = p1.OfferAmount;
        int amount2 = p2.OfferAmount;

        if (!string.IsNullOrEmpty(item1Name) && amount1 > 0)
        {
            p1.RPC_TargetRemoveTradeItem(p1.Object.InputAuthority, item1Name, amount1);
            p2.RPC_TargetReceiveTradeItem(p2.Object.InputAuthority, item1Name, amount1);
        }

        if (!string.IsNullOrEmpty(item2Name) && amount2 > 0)
        {
            p2.RPC_TargetRemoveTradeItem(p2.Object.InputAuthority, item2Name, amount2);
            p1.RPC_TargetReceiveTradeItem(p1.Object.InputAuthority, item2Name, amount2);
        }

        // 🔥 ĐÃ FIX LỖI SỐ 2: Server tự động dọn dẹp phòng, không dùng lệnh xin phép của Client nữa
        p1.IsTrading = false; p1.ResetTradeData();
        p2.IsTrading = false; p2.ResetTradeData();

        RPC_CloseTradeWindow(p1.Object.InputAuthority);
        RPC_CloseTradeWindow(p2.Object.InputAuthority);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_TargetReceiveTradeItem(PlayerRef target, NetworkString<_64> itemName, int amount)
    {
        ItemData data = Resources.Load<ItemData>("Items/" + itemName.ToString());
        if (data != null)
        {
            InventorySystem inv = GetComponent<InventorySystem>();
            bool success = inv.AddItem(data, amount);
            if (!success) DropFallback(inv, data, amount);
            Debug.Log($"[TRADE] Đã nhận được {amount} {data.itemName}");
        }
        else Debug.LogError($"[TRADE LỖI] Không tìm thấy file 'Items/{itemName}' trong thư mục Resources!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_TargetRemoveTradeItem(PlayerRef target, NetworkString<_64> itemName, int amount)
    {
        ItemData data = Resources.Load<ItemData>("Items/" + itemName.ToString());
        if (data != null)
        {
            GetComponent<InventorySystem>().ConsumeItem(data, amount);
            Debug.Log($"[TRADE] Đã giao đi {amount} {data.itemName}");
        }
    }

    private void DropFallback(InventorySystem inv, ItemData item, int amount)
    {
        inv.slots.Add(new InventorySlot(item, amount));
        inv.DropItem(inv.slots.Count - 1);
    }

    public PlayerTrade GetPlayerTrade(PlayerRef playerRef)
    {
        foreach (var p in FindObjectsByType<PlayerTrade>(FindObjectsSortMode.None))
        {
            // 🔥 ĐÃ FIX LỖI SỐ 1: Thêm khiên bảo vệ `p.Object != null` chống NullReference
            if (p != null && p.Object != null && p.Object.InputAuthority == playerRef) return p;
        }
        return null;
    }
}