using Fusion;
using TMPro;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("Kéo cục NameText vào đây")]
    public TextMeshProUGUI nameText;

    // Biến mạng đồng bộ tên: Tối đa 32 ký tự
    [Networked]
    public NetworkString<_32> PlayerName { get; set; }

    // Biến để bật/tắt cái bảng nhập tên
    private bool isEnteringName = false;
    private string inputName = "";

    // Hàm này chạy ngay sau khi bạn bấm Start Host / Start Client và nhân vật hiện ra
    public override void Spawned()
    {
        // Chỉ hiện bảng nhập tên cho MÁY CỦA MÌNH
        if (HasInputAuthority)
        {
            isEnteringName = true; // Bật bảng lên
            // Lấy lại cái tên cũ bạn từng nhập (hoặc tạo random nếu mới chơi lần đầu)
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
        // Khóa không cho chữ bị lộn ngược khi nhân vật xoay vòng tròn
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

    // ==========================================
    // 🔥 CHIÊU THỨC ONGUI: VẼ UI TẠM THỜI CỰC NHANH
    // ==========================================
    private void OnGUI()
    {
        // Nếu đang trong chế độ nhập tên và đây là nhân vật của mình
        if (isEnteringName && HasInputAuthority)
        {
            // Thông số hộp UI
            float width = 300;
            float height = 150;
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;

            // Vẽ cái khung nền mờ
            GUI.Box(new Rect(x, y, width, height), "");
            GUI.Box(new Rect(x, y, width, height), ""); // Vẽ đúp 2 lần cho nó đậm đen lại

            // Vẽ Tiêu đề
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x, y + 10, width, 40), "NHẬP TÊN CỦA BẠN", titleStyle);

            // Vẽ Khung nhập chữ (Chỉ cho nhập tối đa 15 ký tự)
            GUIStyle inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.fontSize = 22;
            inputStyle.alignment = TextAnchor.MiddleCenter;
            inputName = GUI.TextField(new Rect(x + 20, y + 55, width - 40, 40), inputName, 15, inputStyle);

            // Vẽ nút Xác nhận
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 18;
            btnStyle.fontStyle = FontStyle.Bold;
            if (GUI.Button(new Rect(x + 50, y + 105, width - 100, 35), "VÀO GAME", btnStyle))
            {
                // 1. Báo lên Server tên mới
                RPC_SetName(inputName);

                // 2. Lưu tên vào máy tính để lần sau vào game khỏi gõ lại
                PlayerPrefs.SetString("MyPlayerName", inputName);
                PlayerPrefs.Save();

                // 3. Tắt cái bảng này đi
                isEnteringName = false;
            }
        }
    }
}