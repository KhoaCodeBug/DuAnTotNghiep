using Fusion;
using TMPro;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("Kéo cục NameText vào đây")]
    public TextMeshProUGUI nameText;

    [Networked]
    public NetworkString<_32> PlayerName { get; set; }

    private bool isEnteringName = false;
    private string inputName = "";

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            isEnteringName = true;
            inputName = PlayerPrefs.GetString("MyPlayerName", "Player_" + Random.Range(100, 999));
        }
    }

    public override void Render()
    {
        if (nameText != null)
        {
            nameText.text = PlayerName.ToString();
        }
    }

    private void LateUpdate()
    {
        if (nameText != null)
        {
            nameText.transform.rotation = Quaternion.identity;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(NetworkString<_32> newName)
    {
        PlayerName = newName;
    }

    private void OnGUI()
    {
        if (isEnteringName && HasInputAuthority)
        {
            float width = 300;
            float height = 150;
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;

            GUI.Box(new Rect(x, y, width, height), "");
            GUI.Box(new Rect(x, y, width, height), "");

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x, y + 10, width, 40), "NHẬP TÊN CỦA BẠN", titleStyle);

            GUIStyle inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.fontSize = 22;
            inputStyle.alignment = TextAnchor.MiddleCenter;
            inputName = GUI.TextField(new Rect(x + 20, y + 55, width - 40, 40), inputName, 15, inputStyle);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 18;
            btnStyle.fontStyle = FontStyle.Bold;

            if (GUI.Button(new Rect(x + 50, y + 105, width - 100, 35), "VÀO GAME", btnStyle))
            {
                RPC_SetName(inputName);
                PlayerPrefs.SetString("MyPlayerName", inputName);
                PlayerPrefs.Save();
                isEnteringName = false;

                // =========================================================
                // 🔥 ĐÂY RỒI: GỌI LỆNH KÉO TẤM MÀN ĐEN LÊN
                // =========================================================
                if (LobbyCurtain.Instance != null)
                {
                    LobbyCurtain.Instance.HideCurtain();
                }
            }
        }
    }
}