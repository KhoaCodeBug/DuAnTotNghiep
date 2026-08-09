using UnityEngine;

/// <summary>
/// All player-facing copy for tutorial phase one. Kept as data so designers
/// can tune the tone without opening the tutorial controller script.
/// </summary>
[CreateAssetMenu(fileName = "TutorialPhaseOneText", menuName = "Intro/Tutorial Phase One Text")]
public sealed class TutorialPhaseOneText : ScriptableObject
{
    [Header("Movement")]
    public string moveTitle = "DI CHUYỂN";
    [TextArea(3, 7)] public string moveBrief = "Dùng [W] [A] [S] [D] để di chuyển.\n\nKhu vực này hiện an toàn. Hãy làm quen với cảm giác điều khiển trước khi đi tiếp.\n\n[Chuột trái] Bắt đầu";
    public string moveObjective = "DI CHUYỂN";

    [Header("Camera zoom")]
    public string zoomTitle = "TẦM NHÌN";
    [TextArea(3, 7)] public string zoomBrief = "Lăn con lăn chuột để thay đổi khoảng cách quan sát.\n\nTầm nhìn rộng giúp bạn đọc tình huống tốt hơn; zoom gần giúp chú ý chi tiết.\n\n[Chuột trái] Thử ngay";
    public string zoomObjective = "LĂN CHUỘT ĐỂ ZOOM IN / OUT";

    [Header("Looking around")]
    public string aimTitle = "QUAN SÁT";
    [TextArea(3, 7)] public string aimBrief = "Giữ [Chuột phải] và hướng con trỏ tới nơi bạn muốn quan sát.\n\nBạn vẫn có thể đổi hướng ngay cả khi đang đứng yên.\n\n[Chuột trái] Tiếp tục";
    public string aimObjective = "GIỮ CHUỘT PHẢI VÀ QUAN SÁT XUNG QUANH";

    [Header("Needs")]
    public string needsTitle = "THỂ TRẠNG";
    [TextArea(3, 7)] public string needsBrief = "Sau sự cố, cơ thể bắt đầu lên tiếng.\n\nHãy để ý hai biểu tượng ở góc trên bên phải.\n\n[Chuột trái] Xem chỉ số";
    public string needsFocusTitle = "ĐÓI VÀ KHÁT";
    [TextArea(3, 7)] public string needsFocusBody = "Hai biểu tượng này cho biết thể trạng hiện tại của bạn.\nTrong tutorial, chúng sẽ giữ nguyên để bạn có thời gian học.\n\n[Chuột trái] Tiếp tục";

    [Header("House and loot")]
    public string houseTitle = "TÌM NƠI TIẾP TẾ";
    [TextArea(3, 7)] public string houseBrief = "Đi vào căn nhà gần nhất theo điểm đánh dấu vàng để tìm đồ tiếp tế.\n\n[Chuột trái] Lên đường";
    public string houseObjective = "ĐI VÀO NHÀ THEO ĐIỂM ĐÁNH DẤU VÀNG";
    public string houseMarker = "VÀO NHÀ";
    public string cabinetTitle = "LỤC SOÁT";
    [TextArea(3, 7)] public string cabinetBrief = "Đây là tủ bếp được đánh dấu. Đến gần và nhấp vào nó để mở kho đồ.\n\nLấy toàn bộ vật dụng bên trong trước khi rời đi, kể cả khẩu S12K.\n\n[Chuột trái] Mở tủ";
    public string lootObjective = "LẤY TOÀN BỘ ĐỒ TRONG TỦ";
    public string cabinetMarker = "TỦ BẾP";

    [Header("Using supplies")]
    public string consumeTitle = "DÙNG ĐỒ TIẾP TẾ";
    [TextArea(3, 7)] public string consumeBrief = "Mở túi đồ bằng [TAB] hoặc [I].\n\nDùng thịt và nước uống để hồi lại hai chỉ số vừa học.\n\n[Chuột trái] Tiếp tục";
    public string consumeObjective = "DÙNG THỊT VÀ NƯỚC UỐNG TRONG TÚI ĐỒ";

    [Header("Phase complete")]
    public string completeTitle = "GIAI ĐOẠN 1 HOÀN THÀNH";
    [TextArea(3, 7)] public string completeBrief = "Bạn đã biết cách di chuyển, quan sát, nhận biết nhu cầu và lục soát đồ.\n\nPhần chiến đấu sẽ được mở ở chặng tiếp theo.\n\n[Chuột trái] Tự do khám phá";
}
