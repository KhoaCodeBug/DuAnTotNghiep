using UnityEngine;

public class CharacterSelectionMenu : MonoBehaviour
{
    [Header("Kéo cục Photon Fusion vào đây!")]
    public GameObject fusionManager;

    private string[] characterNames = { "Player 1", "Player 2" };
    public static int LocalSelectedCharacterID = 0;
    private bool isSelectionDone = false;

    void Start()
    {
        // Chỉ làm đúng 1 việc: Tắt Fusion từ đầu
        if (fusionManager != null)
        {
            fusionManager.SetActive(false);
        }
    }

    void OnGUI()
    {
        if (isSelectionDone) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 26;
        boxStyle.fontStyle = FontStyle.Bold;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 22;
        buttonStyle.fontStyle = FontStyle.Bold;

        float boxWidth = 500;
        float buttonHeight = 60;
        float spacing = 80;
        float boxHeight = 120 + (characterNames.Length * spacing);

        float startX = (Screen.width - boxWidth) / 2;
        float startY = (Screen.height - boxHeight) / 2;

        GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "\nCHỌN NHÂN VẬT", boxStyle);

        for (int i = 0; i < characterNames.Length; i++)
        {
            string buttonText = characterNames[i];

            if (i == LocalSelectedCharacterID)
            {
                buttonText = ">>>" + buttonText + "<<< (Đang chọn)";
            }

            float btnX = startX + 40;
            float btnY = startY + 80 + (i * spacing);
            float btnWidth = boxWidth - 80;

            if (GUI.Button(new Rect(btnX, btnY, btnWidth, buttonHeight), buttonText, buttonStyle))
            {
                LocalSelectedCharacterID = i;
                Debug.Log($"✅ ĐÃ CHỐT ĐƠN: Thay đồ sang {characterNames[i]}");
                isSelectionDone = true;

                if (fusionManager != null)
                {
                    fusionManager.SetActive(true);
                }
            }
        }
    }
}