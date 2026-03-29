using Fusion;
using TMPro;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    public TextMeshProUGUI nameText;

    [Networked]
    public NetworkString<_32> PlayerName { get; set; }

    public override void Spawned()
    {
        // Chỉ máy của mình mới đi lấy tên và tự kéo màn đen của máy mình lên
        if (HasInputAuthority)
        {
            // 1. Lấy tên từ Menu
            string myName = CharacterSelectionMenu.FinalSelectedName;
            if (string.IsNullOrEmpty(myName)) myName = "Survivor";

            // 2. Báo lên Server
            RPC_SetName(myName);

            // =========================================================
            // 🔥 3. KÉO MÀN LÊN (LÚC NÀY ĐÃ KẾT NỐI VÀ VÀO GAME THÀNH CÔNG)
            // =========================================================
            if (LobbyCurtain.Instance != null)
            {
                LobbyCurtain.Instance.HideCurtain();
            }
        }
    }

    public override void Render()
    {
        if (nameText != null) nameText.text = PlayerName.ToString();
    }

    private void LateUpdate()
    {
        // Luôn giữ chữ thẳng đứng, không xoay theo người
        if (nameText != null) nameText.transform.rotation = Quaternion.identity;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(NetworkString<_32> newName)
    {
        PlayerName = newName;
    }
}