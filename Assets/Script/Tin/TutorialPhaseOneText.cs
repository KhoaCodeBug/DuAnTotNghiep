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

    [Header("Weapon and reload")]
    public string weaponTitle = "TRANG BỊ VŨ KHÍ";
    [TextArea(3, 7)] public string weaponBrief = "Mở túi đồ bằng [TAB] hoặc [I], rồi kéo khẩu S12K vào một trong năm ô Hotbar phía dưới.\n\nĐặt vũ khí vào Hotbar giúp bạn sẵn sàng phản ứng ngay khi nguy hiểm xuất hiện.\n\n[Chuột trái] Tiếp tục";
    public string weaponObjective = "KÉO S12K VÀO MỘT Ô HOTBAR";
    public string reloadTitle = "NẠP ĐẠN";
    [TextArea(3, 7)] public string reloadBrief = "Khẩu S12K đang trống đạn. Đóng túi đồ, chọn khẩu súng trên Hotbar rồi nhấn [R] để nạp đạn.\n\nChưa cần nổ súng vội. Hãy chuẩn bị trước.\n\n[Chuột trái] Tiếp tục";
    public string reloadObjective = "NHẤN [R] ĐỂ NẠP ĐẠN CHO S12K";

    [Header("First zombie")]
    public string leaveHouseTitle = "CÓ GÌ ĐÓ BÊN NGOÀI";
    [TextArea(3, 7)] public string leaveHouseBrief = "Bạn đã có đồ tiếp tế và một khẩu súng đã nạp đạn.\n\nRa khỏi căn nhà thật cẩn thận.\n\n[Chuột trái] Tiếp tục";
    public string leaveHouseObjective = "RỜI KHỎI CĂN NHÀ";
    public string noiseTitle = "TIẾNG ỒN";
    [TextArea(3, 7)] public string noiseBrief = "Mỗi hành động đều có thể tạo tiếng động. Bước chân, chạy nước rút, cận chiến và súng nổ đều có thể kéo zombie tới.\n\nĐể ý thanh Độ ồn ở góc dưới bên trái.\n\n[Chuột trái] Xem thanh Độ ồn";
    public string noiseObjective = "THEO DÕI THANH ĐỘ ỒN";
    public string sneakTitle = "TIẾP CẬN ÂM THẦM";
    [TextArea(3, 7)] public string sneakBrief = "Nhấn [C] để ngồi xuống, rồi tiếp cận zombie từ phía sau.\n\nDi chuyển chậm ít gây chú ý hơn và cho bạn cơ hội ra đòn trước.\n\n[Chuột trái] Tiếp tục";
    public string sneakObjective = "NHẤN [C] VÀ TIẾP CẬN ZOMBIE TỪ PHÍA SAU";
    public string meleeTitle = "ĐÁNH THƯỜNG";
    [TextArea(3, 7)] public string meleeBrief = "Khi đã đủ gần, giữ [Chuột phải] để ngắm hướng ra đòn và nhấn [Space] để đánh thường bằng báng súng.\n\nKết liễu zombie trước khi nó kịp phản ứng.\n\n[Chuột trái] Tiếp tục";
    public string meleeObjective = "GIỮ [CHUỘT PHẢI] + NHẤN [SPACE] ĐỂ HẠ ZOMBIE";

    [Header("Bleeding and bandage")]
    public string bleedingTitle = "BẠN ĐANG CHẢY MÁU";
    [TextArea(3, 7)] public string bleedingBrief = "Một vết rách ở tay đang khiến bạn mất máu. Biểu tượng đỏ này sẽ cảnh báo khi cơ thể còn vết thương hở.\n\nHãy kiểm tra tình trạng cơ thể và xử lý nó trước khi đi tiếp.\n\n[Chuột trái] Tiếp tục";
    public string openHealthObjective = "NHẤN [TAB], SAU ĐÓ CHỌN HEALTH STATUS";
    public string bandageObjective = "NHẤP CHUỘT PHẢI VÀO VẾT THƯƠNG VÀ CHỌN APPLY BANDAGE";

    [Header("Ranged weapon")]
    public string rangedTitle = "MỤC TIÊU Ở XA";
    [TextArea(3, 7)] public string rangedBrief = "Không phải mối nguy nào cũng cho phép bạn tiếp cận an toàn. Với mục tiêu ở xa, khẩu súng giúp bạn tấn công trước khi nó chạm tới mình.\n\n[Chuột trái] Tiếp tục";
    public string rangedNoiseTitle = "CÁI GIÁ CỦA MỘT PHÁT SÚNG";
    [TextArea(3, 7)] public string rangedNoiseBrief = "Súng rất hiệu quả, nhưng tiếng nổ có thể kéo mọi zombie trong khu vực tới vị trí của bạn. Hãy nhìn thanh Độ ồn trước khi bóp cò.\n\n[Chuột trái] Sẵn sàng";
    public string rangedObjective = "GIỮ [CHUỘT PHẢI] VÀ BẮN HẠ ZOMBIE B";

    [Header("Final horde")]
    public string finalTauntTitle = "BẮN KHÁ ĐẤY";
    [TextArea(3, 7)] public string finalTauntBrief = "Bạn đã xử lý được mục tiêu. Nhanh, gọn và chính xác.\n\nChỉ có một vấn đề nhỏ... cả khu phố vừa nghe thấy rồi.\n\nChúc may mắn, kẻ sống sót.\n\n[Chuột trái] Tiếp tục";
    public string hordeObjective = "SỐNG SÓT";

    [Header("Tutorial ending")]
    public string endingTitle = "ĐÂY LÀ CÁCH BẠN CHẾT";
    [TextArea(2, 5)] public string endingBody = "Bạn đã hoàn thành phần hướng dẫn. Trong thế giới này, sống sót không có nghĩa là chiến thắng — chỉ là trì hoãn điều không thể tránh khỏi.";
    public string startMainButton = "BẮT ĐẦU SINH TỒN";
    public string replayButton = "CHƠI LẠI HƯỚNG DẪN";
    public string returnMenuButton = "TRỞ VỀ MENU";
}
