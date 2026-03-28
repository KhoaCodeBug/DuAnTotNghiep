using UnityEngine;

public class CharacterSelectionMenu : MonoBehaviour
{
    private string[] characterNames = { "Player 1 (Nam)", "Player 2 (Nữ)" };
    private int selectedID = 0;

    // 🔥 CÔNG TẮC: Đánh dấu xem đã chọn xong chưa
    private bool isSelectionDone = false;

    void Start()
    {
        selectedID = PlayerPrefs.GetInt("SelectedCharacterID", 0);
    }

    void OnGUI()
    {
        // Nếu đã chọn xong rồi thì KHÔNG VẼ GÌ NỮA CẢ (Tàng hình)
        if (isSelectionDone) return;

        float boxWidth = 300;
        float boxHeight = 100 + (characterNames.Length * 50);

        float startX = (Screen.width - boxWidth) / 2;
        float startY = (Screen.height - boxHeight) / 2;

        GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "CHỌN NHÂN VẬT (AUTO UI)");

        for (int i = 0; i < characterNames.Length; i++)
        {
            string buttonText = characterNames[i];

            if (i == selectedID)
            {
                buttonText = ">>> " + buttonText + " <<< (Đang chọn)";
            }

            if (GUI.Button(new Rect(startX + 20, startY + 40 + (i * 50), boxWidth - 40, 40), buttonText))
            {
                selectedID = i;
                PlayerPrefs.SetInt("SelectedCharacterID", selectedID);
                PlayerPrefs.Save();

                Debug.Log($"✅ ĐÃ CHỐT ĐƠN: Thay đồ sang {characterNames[i]}");

                // 🔥 ĐÓNG MENU SAU KHI CHỌN
                isSelectionDone = true;
            }
        }
    }
}