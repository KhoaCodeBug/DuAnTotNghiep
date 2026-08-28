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
        if (Input.GetKeyDown(KeyCode.T) && !IsTrading)
        {
            // Block initiating trade if Inventory, Loot, or Health is open
            bool isInvOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen();
            bool isHealthOpen = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;
            if (isInvOpen || isHealthOpen) return;

            SendTradeRequest();
        }
    }

    private void SendTradeRequest()
    {
        PlayerTrade[] allPlayers = FindObjectsByType<PlayerTrade>(FindObjectsSortMode.None);
        foreach (PlayerTrade otherPlayer in allPlayers)
        {
            if (otherPlayer == this) continue;
            if (Vector2.Distance(transform.position, otherPlayer.transform.position) <= tradeRadius && !otherPlayer.IsTrading)
            {
                RPC_SendRequest(otherPlayer.Object.InputAuthority);
                break;
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendRequest(PlayerRef target, RpcInfo info = default)
    {
        PlayerTrade senderTrade = GetPlayerTrade(info.Source);
        PlayerTrade targetTrade = GetPlayerTrade(target);
        if (!CanStartTrade(senderTrade, targetTrade)) return;
        RPC_ShowTradeRequest(info.Source, target);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowTradeRequest(PlayerRef sender, PlayerRef target)
    {
        if (Runner.LocalPlayer == target && AutoUIManager.Instance != null)
            AutoUIManager.Instance.ShowTradeRequestPopup(sender, target);
    }

    public void AcceptTradeRequest(PlayerRef sender) { RPC_AcceptTrade(sender); }
    public void DeclineTradeRequest(PlayerRef sender) { RPC_DeclineTrade(sender); }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_AcceptTrade(PlayerRef sender, RpcInfo info = default)
    {
        PlayerRef receiver = info.Source;
        PlayerTrade p1 = GetPlayerTrade(sender);
        PlayerTrade p2 = GetPlayerTrade(receiver);

        if (p2 != this || !CanStartTrade(p1, p2)) return;

        p1.IsTrading = true; p1.TradePartner = receiver;
        p2.IsTrading = true; p2.TradePartner = sender;
        p1.ResetTradeData(); p2.ResetTradeData();

        RPC_OpenTradeWindow(sender);
        RPC_OpenTradeWindow(receiver);
    }

    private bool CanStartTrade(PlayerTrade sender, PlayerTrade receiver)
    {
        if (!HasStateAuthority || sender == null || receiver == null || sender == receiver ||
            sender.Object == null || receiver.Object == null || !sender.Object.IsValid ||
            !receiver.Object.IsValid || sender.IsTrading || receiver.IsTrading)
            return false;

        return Vector2.Distance(sender.transform.position, receiver.transform.position) <=
               Mathf.Min(sender.tradeRadius, receiver.tradeRadius);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_DeclineTrade(PlayerRef sender, RpcInfo info = default)
    {
        PlayerTrade receiver = GetPlayerTrade(info.Source);
        PlayerTrade senderTrade = GetPlayerTrade(sender);
        if (receiver != this || senderTrade == null) return;
        RPC_NotifyTradeDeclined(sender);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyTradeDeclined(PlayerRef sender)
    {
        if (Runner.LocalPlayer == sender)
        {
            Debug.Log("❌ Bị từ chối!");
        }
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
        InventorySystem inventory = GetComponent<InventorySystem>();
        ItemData data = amount > 0 ? ItemDataLoader.LoadItem(itemName.ToString()) : null;
        bool validOffer = IsTrading && inventory != null && data != null &&
                          inventory.GetItemCount(data) >= amount;

        OfferItemName = validOffer ? data.name : string.Empty;
        OfferAmount = validOffer ? amount : 0;
        IsReady = false; IsConfirmed = false;

        PlayerTrade partner = GetPlayerTrade(TradePartner);
        if (partner != null) { partner.IsReady = false; partner.IsConfirmed = false; }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ToggleReady()
    {
        if (!IsTrading) return;
        if (!IsReady && !TryResolveOffer(this, out _, out _, out _)) return;
        IsReady = !IsReady;
        IsConfirmed = false;
    }

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
        if (!HasStateAuthority || p1 == null || p2 == null || !p1.IsTrading || !p2.IsTrading ||
            p1.TradePartner != p2.Object.InputAuthority || p2.TradePartner != p1.Object.InputAuthority ||
            !TryResolveOffer(p1, out InventorySystem p1Inv, out ItemData data1, out int amount1) ||
            !TryResolveOffer(p2, out InventorySystem p2Inv, out ItemData data2, out int amount2))
        {
            CloseTradeSafely(p1, p2, "offer validation failed");
            return;
        }

        int removed1 = amount1 > 0 ? p1Inv.ConsumeItem(data1, amount1) : 0;
        if (removed1 != amount1)
        {
            if (removed1 > 0) p1Inv.AddItem(data1, removed1);
            CloseTradeSafely(p1, p2, "first authoritative removal failed");
            return;
        }

        int removed2 = amount2 > 0 ? p2Inv.ConsumeItem(data2, amount2) : 0;
        if (removed2 != amount2)
        {
            if (removed2 > 0) p2Inv.AddItem(data2, removed2);
            if (removed1 > 0) p1Inv.AddItem(data1, removed1);
            CloseTradeSafely(p1, p2, "second authoritative removal failed");
            return;
        }

        int p2Before = amount1 > 0 ? p2Inv.GetItemCount(data1) : 0;
        bool addedToP2 = amount1 == 0 || p2Inv.AddItem(data1, amount1);
        int added1 = amount1 > 0 ? Mathf.Max(0, p2Inv.GetItemCount(data1) - p2Before) : 0;

        int p1Before = amount2 > 0 ? p1Inv.GetItemCount(data2) : 0;
        bool addedToP1 = amount2 == 0 || p1Inv.AddItem(data2, amount2);
        int added2 = amount2 > 0 ? Mathf.Max(0, p1Inv.GetItemCount(data2) - p1Before) : 0;

        if (!addedToP2 || added1 != amount1 || !addedToP1 || added2 != amount2)
        {
            if (added1 > 0) p2Inv.ConsumeItem(data1, added1);
            if (added2 > 0) p1Inv.ConsumeItem(data2, added2);
            if (removed1 > 0) p1Inv.AddItem(data1, removed1);
            if (removed2 > 0) p2Inv.AddItem(data2, removed2);
            CloseTradeSafely(p1, p2, "recipient inventory could not accept the complete trade");
            return;
        }

        Debug.Log($"[TRADE] Server committed {amount1} {data1?.itemName ?? "item(s)"} and " +
                  $"{amount2} {data2?.itemName ?? "item(s)"} atomically.");
        CloseTradeSafely(p1, p2, null);
    }

    private bool TryResolveOffer(PlayerTrade trade, out InventorySystem inventory, out ItemData data,
        out int amount)
    {
        inventory = trade != null ? trade.GetComponent<InventorySystem>() : null;
        amount = trade != null ? trade.OfferAmount : 0;
        string itemId = trade != null ? trade.OfferItemName.ToString() : string.Empty;
        data = amount > 0 && !string.IsNullOrWhiteSpace(itemId)
            ? ItemDataLoader.LoadItem(itemId)
            : null;

        if (amount == 0 && string.IsNullOrEmpty(itemId)) return inventory != null;
        return amount > 0 && inventory != null && data != null &&
               inventory.GetItemCount(data) >= amount;
    }

    private void CloseTradeSafely(PlayerTrade p1, PlayerTrade p2, string reason)
    {
        if (!string.IsNullOrEmpty(reason))
            Debug.LogWarning($"[TRADE] Cancelled safely: {reason}.");

        if (p1 != null)
        {
            p1.IsTrading = false;
            p1.ResetTradeData();
            RPC_CloseTradeWindow(p1.Object.InputAuthority);
        }
        if (p2 != null)
        {
            p2.IsTrading = false;
            p2.ResetTradeData();
            RPC_CloseTradeWindow(p2.Object.InputAuthority);
        }
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
