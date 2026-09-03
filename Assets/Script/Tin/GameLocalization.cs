using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Client-local language service. Network messages should carry keys/data, not translated text.</summary>
public static class GameLocalization
{
    public enum Language { English = 0, Vietnamese = 1 }

    private const string PreferenceKey = "GameLanguage";
    private const string StaticVietnameseFontResourcePath = "Fonts/Vietnamese Static SDF";
    private static TMP_FontAsset staticVietnameseFont;
    public static event Action LanguageChanged;
    public static Language Current { get; private set; } = ReadInitialLanguage();
    public static bool IsVietnamese => Current == Language.Vietnamese;
    public static string CurrentLabel => IsVietnamese ? "TIẾNG VIỆT" : "ENGLISH";

    private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
    {
        { "spectator.dead", new[] { "YOU DIED. SPECTATING TEAMMATES.\nUse A/D or click to cycle.", "BẠN ĐÃ CHẾT. ĐANG THEO DÕI ĐỒNG ĐỘI.\nDùng A/D hoặc nhấp chuột để chuyển." } },
        { "respawn.action", new[] { "RESPAWN", "HỒI SINH" } },
        { "respawn.waiting", new[] { "RESPAWNING...", "ĐANG HỒI SINH..." } },
        { "respawn.failed", new[] { "RESPAWN FAILED - TRY AGAIN", "HỒI SINH THẤT BẠI - THỬ LẠI" } },
        { "noise.title", new[] { "NOISE", "ĐỘ ỒN" } },
        { "noise.silent", new[] { "SILENT", "YÊN LẶNG" } },
        { "noise.running", new[] { "RUNNING", "CHẠY" } },
        { "noise.footsteps", new[] { "FOOTSTEPS", "BƯỚC CHÂN" } },
        { "noise.voice", new[] { "NEARBY VOICE", "GIỌNG NÓI LÂN CẬN" } },
        { "intro.next", new[] { "[E] Next", "[E] Tiếp" } },
        { "intro.leave", new[] { "[E] Leave vehicle", "[E] Rời xe" } },
        { "intro.fallback", new[] { "The car died. I need to inspect it.", "Xe đã chết máy. Phải xuống kiểm tra thôi." } },
        { "settings.language", new[] { "LANGUAGE:", "NGÔN NGỮ:" } },
        { "settings.english", new[] { "ENGLISH", "TIẾNG ANH" } },
        { "settings.vietnamese", new[] { "VIETNAMESE", "TIẾNG VIỆT" } },
        { "inventory.title", new[] { "INVENTORY", "TÚI ĐỒ" } },
        { "inventory.capacity", new[] { "{0}/{1} STORAGE + {2} HOTBAR", "{0}/{1} Ô KHO + {2} Ô HOTBAR" } },
        { "loot.title", new[] { "LOOT CONTAINER", "VẬT PHẨM TRONG THÙNG" } },
        { "loot.picked_up", new[] { "Looted {0} x{1}.", "Đã lục được {0} x{1}." } },
        { "loot.backpack_found", new[] { "Found {0} (capacity +{1} storage slots).", "Đã lục được {0} (tăng {1} ô kho)." } },
        { "loot.backpack_denied", new[] { "You already have an equal or higher level backpack; leave this for your teammates.", "Bạn đang có balo cấp cao hơn; balo này để đồng đội khác loot." } },
        { "backpack.equipped", new[] { "Backpack equipped: {0} storage slots.", "Đã trang bị balo: {0} ô kho." } },
        { "backpack.quest.scan", new[] { "BACKPACK CONFIGURATION RECEIVED", "ĐÃ NHẬN GÓI TRANG BỊ BALO" } },
        { "backpack.quest.level4.tier", new[] { "CAPACITY UPGRADED", "NÂNG CẤP SỨC CHỨA" } },
        { "backpack.quest.level4.title", new[] { "BACKPACK LEVEL 4 ACQUIRED", "ĐÃ NHẬN BALO CẤP 4" } },
        { "backpack.quest.level4.body", new[] { "", "" } },
        { "backpack.quest.level5.tier", new[] { "CAPACITY UPGRADED", "NÂNG CẤP SỨC CHỨA" } },
        { "backpack.quest.level5.title", new[] { "BACKPACK LEVEL 5 ACQUIRED", "ĐÃ NHẬN BALO CẤP 5" } },
        { "backpack.quest.level5.body", new[] { "", "" } },
        { "backpack.quest.level5.upgraded", new[] { "Upgraded to backpack level 5.", "Đã nâng cấp lên balo cấp 5." } },
        { "backpack.notification.title", new[] { "BACKPACK REWARD RECEIVED", "ĐÃ NHẬN PHẦN THƯỞNG BALO" } },
        { "backpack.notification.title_transition", new[] { "BACKPACK LEVEL {0} → LEVEL {1}", "BALO CẤP {0} → CẤP {1}" } },
        { "backpack.notification.body_format", new[] { "{0}\n{1}  •  {2}", "{0}\n{1}  •  {2}" } },
        { "backpack.notification.reason.level4", new[] { "Hospital milestone completed", "Hoàn thành mốc bệnh viện" } },
        { "backpack.notification.reason.level5", new[] { "Radio restoration milestone completed", "Hoàn thành mốc khôi phục radio" } },
        { "backpack.notification.capacity_transition", new[] { "STORAGE {0} → {1} (+{2} SLOTS)", "SỨC CHỨA {0} → {1} (+{2} Ô)" } },
        { "backpack.notification.level4", new[] { "STORAGE 30 → 40 (+10 SLOTS)", "SỨC CHỨA 30 → 40 (+10 Ô)" } },
        { "backpack.notification.level5", new[] { "STORAGE 40 → 50 (+10 SLOTS)", "SỨC CHỨA 40 → 50 (+10 Ô)" } },
        { "backpack.quest.capacity", new[] { "FIELD STORAGE  {0}/{1}", "SỨC CHỨA KHO  {0}/{1}" } },
        { "corpse.title", new[] { "CORPSE SEARCH", "LỤC XÁC" } },
        { "corpse.prompt", new[] { "PRESS [E] TO SEARCH", "NHẤN [E] ĐỂ LỤC SOÁT" } },
        { "corpse.searching", new[] { "SEARCHING ZOMBIE CORPSE", "ĐANG LỤC XÁC ZOMBIE" } },
        { "corpse.found", new[] { "Found: {0} x{1}.", "Đã nhận được: {0} x{1}." } },
        { "corpse.empty", new[] { "Nothing useful was found.", "Không tìm thấy gì hữu ích." } },
        { "corpse.already_searched", new[] { "This corpse has already been searched.", "Xác này đã được lục soát." } },
        { "corpse.too_far", new[] { "Move closer to the zombie corpse.", "Bạn phải đứng gần xác zombie hơn." } },
        { "corpse.inventory_full", new[] { "Your inventory is full.", "Túi đồ đã đầy." } },
        { "corpse.inventory_missing", new[] { "Your inventory could not be found.", "Không tìm thấy túi đồ của người chơi." } },
        { "corpse.player_missing", new[] { "The player could not be validated.", "Không thể xác thực người chơi." } },
        { "corpse.invalid_loot", new[] { "The corpse loot configuration is invalid.", "Cấu hình vật phẩm trên xác không hợp lệ." } },
        { "trade.title", new[] { "TRADE", "BÀN GIAO DỊCH" } },
        { "trade.choosing", new[] { "Choosing...", "Đang chọn..." } },
        { "trade.lock", new[] { "LOCK", "KHÓA LẠI" } },
        { "trade.unlock", new[] { "UNLOCK", "MỞ KHÓA" } },
        { "chat.placeholder", new[] { "Press Enter to chat...", "Nhấn Enter để chat..." } },
        { "chat.input_placeholder", new[] { "Type a message...", "Nhập tin nhắn..." } },
        { "trade.confirmed", new[] { "DEAL CONFIRMED!", "ĐÃ CHỐT KÈO!" } },
        { "trade.locked", new[] { "LOCKED!", "ĐÃ KHÓA!" } },
        { "trade.incoming", new[] { "INCOMING TRADE!\nAnother player wants to trade items with you.", "GIAO DỊCH ĐANG TỚI!\nMột người chơi khác muốn trao đổi đồ với bạn." } },
        { "item.type", new[] { "Type", "Loại" } },
        { "item.using", new[] { "Using", "Đang dùng" } },
        { "lobby.you", new[] { "YOU", "BẠN" } },
        { "lobby.survivor", new[] { "SURVIVOR {0}", "NGƯỜI SỐNG SÓT {0}" } },
        { "lobby.connected", new[] { "CONNECTED", "ĐÃ KẾT NỐI" } },
        { "lobby.wait_signal", new[] { "Waiting for signal...", "Đang chờ tín hiệu..." } },
        { "lobby.client_wait", new[] { "Device connected. Waiting for Host's orders...", "Đã kết nối. Đang chờ Chủ phòng..." } },
        { "lobby.host_report", new[] { "Outpost report: {0}/{1} personnel in sector.", "Báo cáo tiền đồn: {0}/{1} người trong khu vực." } },
        { "settings.unsaved", new[] { "UNSAVED CHANGES\n\nDo you want to save changes before exiting?", "THAY ĐỔI CHƯA LƯU\n\nBạn có muốn lưu trước khi thoát không?" } },
        { "lobby.host_wait", new[] { "You are the Host. Wait for your team and press START!", "Bạn là Chủ phòng. Hãy chờ đồng đội rồi nhấn BẮT ĐẦU!" } },
        { "lobby.wait_start", new[] { "Waiting for the Host to START...", "Đang chờ Chủ phòng BẮT ĐẦU..." } },
        { "server.waiting", new[] { "WAITING", "ĐANG CHỜ" } },
        { "server.full", new[] { "FULL", "ĐẦY" } },
        { "server.combat", new[] { "IN COMBAT", "ĐANG CHƠI" } },
        { "server.locked", new[] { "LOCKED", "CÓ KHÓA" } },
        { "server.open", new[] { "OPEN", "MỞ" } },
        { "server.row", new[] { "{0} Base: {1} | Players: {2}/{3} | Status: {4}", "{0} Phòng: {1} | Người chơi: {2}/{3} | Trạng thái: {4}" } },
        { "sleep.no_system", new[] { "Day/night system was not found.", "Không tìm thấy hệ thống ngày và đêm." } },
        { "sleep.invalid_hours", new[] { "You can only sleep from 20:00 to 03:00.", "Chỉ có thể ngủ từ 20:00 đến 03:00." } },
        { "sleep.too_far", new[] { "You are too far from the bed.", "Bạn đang đứng quá xa giường." } },
        { "sleep.bed_used", new[] { "This bed is already occupied.", "Giường này đã có người sử dụng." } },
        { "sleep.leave_vehicle", new[] { "Leave the vehicle before using a bed.", "Hãy xuống xe trước khi sử dụng giường." } },
        { "sleep.wait_started", new[] { "Lying down. Waiting for other players...", "Đã nằm xuống. Đang đợi những người chơi khác..." } },
        { "sleep.left", new[] { "You left the bed.", "Bạn đã rời khỏi giường." } },
        { "sleep.wait_count", new[] { "WAITING FOR OTHER PLAYERS  ({0}/{1})", "ĐANG ĐỢI NGƯỜI CHƠI KHÁC  ({0}/{1})" } },
        { "sleep.exhausted", new[] { "YOU ARE EXHAUSTED...", "BẠN ĐÃ KIỆT SỨC..." } },
        { "quest.find_map", new[] { "OBJECTIVE: Find the city map in the office.", "NHIỆM VỤ: Tìm bản đồ thành phố trong văn phòng." } },
        { "quest.reach_military", new[] { "OBJECTIVE: Reach the military zone marked on the map.", "NHIỆM VỤ: Đến khu quân sự được đánh dấu trên bản đồ." } },
        { "quest.sender", new[] { "OBJECTIVE", "NHIỆM VỤ" } },
        { "quest.map_clue_chat", new[] { "IMPORTANT CLUE FOUND! The city map reveals a military position.", "ĐÃ TÌM THẤY MANH MỐI QUAN TRỌNG! Bản đồ thành phố hé lộ vị trí của quân đội." } },
        { "quest.clue_title", new[] { "IMPORTANT CLUE FOUND", "ĐÃ TÌM THẤY MANH MỐI" } },
        { "quest.clue_subtitle", new[] { "CITY MAP  •  MILITARY POSITION IDENTIFIED", "BẢN ĐỒ THÀNH PHỐ  •  ĐÃ XÁC ĐỊNH VỊ TRÍ QUÂN ĐỘI" } },
        { "quest.military_title", new[] { "MILITARY ENCAMPMENT", "KHU ĐÓNG QUÂN CỦA QUÂN ĐỘI" } },
        { "quest.military_subtitle", new[] { "FPT POLYTECHNIC COLLEGE", "TRƯỜNG CAO ĐẲNG FPT POLYTECHNIC" } },
        { "quest.military_marker", new[] { "MILITARY ZONE", "KHU QUÂN SỰ" } },
        { "quest.route_b_new_search", new[] { "ROUTE B — NEW OBJECTIVE: Find {0} supply and evacuation records in the nearby houses.", "TUYẾN B — MỤC TIÊU MỚI: Tìm {0} tài liệu về tuyến tiếp tế và sơ tán trong các ngôi nhà xung quanh." } },
        { "quest.route_clue_count", new[] { "Route clues: {0}/{1}.", "Manh mối tuyến đường: {0}/{1}." } },
        { "quest.route_clue.invoice.name", new[] { "Supply Transfer Invoice", "Phiếu điều chuyển vật tư" } },
        { "quest.route_clue.invoice.short", new[] { "SUPPLIES", "VẬT TƯ" } },
        { "quest.route_clue.invoice.reading", new[] {
            "Emergency dispatch note: “All remaining medicines, reserve fuel, and repair tools must be transferred to the Residential Coordination Office before 18:00. Do not deliver directly to the evacuation point.” The delivery person circled a section of the eastern road and added: “purple gate”.",
            "Phiếu điều chuyển khẩn: “Toàn bộ thuốc, nhiên liệu dự phòng và dụng cụ sửa chữa còn lại được chuyển về Văn phòng Điều phối Khu Dân Cư trước 18:00. Không giao trực tiếp tại điểm sơ tán.” Người giao khoanh một đoạn đường phía đông và ghi thêm: “cổng màu tím”." } },
        { "quest.route_clue.invoice.inference", new[] {
            "INFERENCE: Fuel and car repair tools were once gathered at the purple-gate office.",
            "SUY LUẬN: Nhiên liệu và dụng cụ sửa xe từng được tập kết tại văn phòng cổng tím." } },
        { "quest.route_clue.diagram.name", new[] { "Evacuation Route Change Notice", "Thông báo đổi tuyến sơ tán" } },
        { "quest.route_clue.diagram.short", new[] { "EVACUATION", "SƠ TÁN" } },
        { "quest.route_clue.diagram.reading", new[] {
            "Operations notice: “The civilian evacuation route has been cancelled. Remaining operational vehicles must pass through the military zone checkpoint. The new route diagram is archived in the Coordination Office.”",
            "Thông báo vận hành: “Tuyến sơ tán dân sự đã bị hủy. Các chuyến xe còn hoạt động phải chuyển qua trạm kiểm soát khu quân sự. Sơ đồ tuyến mới được lưu tại Văn phòng Điều phối.”" } },
        { "quest.route_clue.diagram.inference", new[] {
            "INFERENCE: Reaching the evacuation point requires the route diagram located in the office.",
            "SUY LUẬN: Muốn đến điểm sơ tán phải tìm sơ đồ tuyến nằm trong văn phòng." } },
        { "quest.route_clue.note.name", new[] { "Duty Officer's Note", "Ghi chú của nhân viên trực" } },
        { "quest.route_clue.note.short", new[] { "DUTY OFFICER", "NHÂN VIÊN TRỰC" } },
        { "quest.route_clue.note.reading", new[] {
            "Hastily written note: “I locked the final route records in the storage cabinet. The key is still at the dispatch desk; the communication log is in the radio. If no one returns, take the map to the military station.”",
            "Mảnh giấy viết vội: “Tôi đã khóa hồ sơ tuyến cuối trong tủ lưu trữ. Chìa khóa vẫn ở bàn điều phối; bản ghi liên lạc còn trong radio. Nếu không còn ai quay lại, hãy mang bản đồ đến trạm quân sự.”" } },
        { "quest.route_clue.note.inference", new[] {
            "INFERENCE: Inside the office, check the dispatch desk, the radio, and then the storage cabinet.",
            "SUY LUẬN: Trong văn phòng cần kiểm tra bàn điều phối, radio rồi tủ lưu trữ." } },
        { "quest.route_clue.eyebrow", new[] { "ROUTE CLUES  //  ", "MANH MỐI TUYẾN ĐƯỜNG  //  " } },
        { "quest.route_clue.fallback_title", new[] { "UNKNOWN CLUE", "MANH MỐI KHÔNG RÕ" } },
        { "quest.route_clue.close_hint", new[] { "[SPACE / E]  STORE CLUE", "[SPACE / E]  CẤT MANH MỐI" } },
        { "quest.hospital.transcript.eyebrow", new[] { "HOSPITAL ARCHIVE  //  SAVED TRANSCRIPT", "LƯU TRỮ BỆNH VIỆN  //  TRANSCRIPT ĐÃ LƯU" } },
        { "quest.hospital.transcript.title", new[] { "RECOVERED RADIO RECORDING", "BẢN GHI RADIO ĐÃ KHÔI PHỤC" } },
        { "quest.hospital.transcript.conclusion", new[] {
            "Conclusion: the convoy withdrew to North Base; the recording does not confirm anyone is alive there.",
            "Kết luận: đoàn xe đã rút về Căn cứ phía Bắc; bản ghi không xác nhận ở đó còn người sống." } },
        { "quest.route_a_ready", new[] { "ROUTE A — REQUIREMENTS MET: Return to the vehicle condition panel and press START CAR.", "TUYẾN A — ĐÃ ĐỦ ĐIỀU KIỆN: Quay lại bảng tình trạng xe và bấm KHỞI ĐỘNG XE." } },
        { "quest.route_a_started", new[] { "ROUTE A — CAR STARTED: The city map and civilian road exit are now available.", "TUYẾN A — XE ĐÃ KHỞI ĐỘNG: Bản đồ thành phố và lối thoát dân sự đã được mở." } },
        { "quest.route_a_regroup", new[] { "ROUTE A — REGROUP: Bring every living survivor into or close to the repaired car.", "TUYẾN A — TẬP KẾT: Đưa mọi người còn sống lên xe hoặc tập trung sát chiếc xe đã sửa." } },
        { "quest.route_a_wait_team", new[] { "Cannot depart yet: a living teammate is still outside the regroup radius.", "Chưa thể xuất phát: vẫn còn đồng đội sống sót ở ngoài phạm vi tập kết." } },
        { "quest.ending_a_locked", new[] { "ENDING LOCKED: The team chose to break through the quarantine in the civilian car.", "KẾT CỤC ĐÃ KHÓA: Toàn đội chọn vượt vòng phong tỏa bằng chiếc xe dân sự." } },
        { "quest.office_no_points", new[] { "Cannot begin: this area has no mission search points.", "Chưa thể bắt đầu: khu vực chưa có điểm kiểm tra nhiệm vụ." } },
        { "quest.office_missing_points", new[] { "Cannot begin: the office is missing story investigation points.", "Chưa thể bắt đầu: văn phòng thiếu điểm điều tra cốt truyện." } },
        { "quest.office_new_objective", new[] { "NEW OBJECTIVE: Inspect the dispatch desk for the records-cabinet key.", "MỤC TIÊU MỚI: Kiểm tra bàn điều phối để tìm chìa khóa tủ hồ sơ." } },
        { "quest.route_b_go_military", new[] { "ROUTE B — NEW OBJECTIVE: Follow the discovered road to the military base.", "TUYẾN B — MỤC TIÊU MỚI: Đi đến khu quân sự theo tuyến đường vừa tìm thấy." } },
        { "quest.vehicle_sender", new[] { "VEHICLE", "CHIẾC XE" } },
        { "quest.vehicle_signal", new[] { "The car can still be repaired. The emergency frequency has just picked up a new signal.", "Xe vẫn có thể sửa. Tần số khẩn cấp vừa bắt được một tín hiệu mới." } },
        { "quest.outside_search_title", new[] { "OUTSIDE SEARCH AREA", "NGOÀI VÙNG TÌM KIẾM" } },
        { "quest.outside_office_title", new[] { "OUTSIDE AREA OF INTEREST", "NGOÀI VÙNG NGHI VẤN" } },
        { "quest.outside_search_body", new[] { "Follow the marker back to the objective  •  Map [M].", "Đi theo dấu chỉ dẫn để quay lại mục tiêu  •  Bản đồ [M]." } },
        { "quest.office_area_title", new[] { "HOSPITAL COORDINATION SECTION", "KHU ĐIỀU PHỐI TRONG BỆNH VIỆN" } },
        { "quest.office_area_body", new[] { "HOSPITAL COORDINATION SECTION  •  Start by finding the key at the dispatch desk.", "KHU ĐIỀU PHỐI TRONG BỆNH VIỆN  •  Trước tiên hãy tìm chìa khóa tại bàn điều phối." } },
        { "quest.investigation_sender", new[] { "INVESTIGATION", "ĐIỀU TRA" } },
        { "quest.office_step0_title", new[] { "KEY FOUND", "ĐÃ TÌM THẤY CHÌA KHÓA" } },
        { "quest.office_step0_body", new[] { "The shift log points to the final radio transmission. REWARD: dispatch evidence recovered.", "Sổ trực chỉ tới bản liên lạc cuối trong radio. PHẦN THƯỞNG: đã thu hồi chứng cứ điều phối." } },
        { "quest.office_step1_title", new[] { "RADIO RECORDING RESTORED", "ĐÃ KHÔI PHỤC BẢN GHI RADIO" } },
        { "quest.office_step1_body", new[] { "The transmission says the military route diagram is locked in the records cabinet. REWARD: cabinet location identified.", "Bản ghi cho biết sơ đồ tuyến quân sự nằm trong tủ hồ sơ. PHẦN THƯỞNG: đã xác định vị trí tủ." } },
        { "quest.office_step2_title", new[] { "FINAL ROUTE RECORD FOUND", "ĐÃ TÌM THẤY HỒ SƠ TUYẾN CUỐI" } },
        { "quest.office_step2_body", new[] { "The map confirms the military-base road. REWARD: military route map and base waypoint unlocked.", "Bản đồ xác nhận đường tới căn cứ. PHẦN THƯỞNG: bản đồ tuyến quân sự và điểm đến căn cứ đã mở." } },
        { "quest.interaction_hold", new[] { "INTERACTION  •  HOLD", "TƯƠNG TÁC  •  GIỮ PHÍM" } },
        { "quest.office_action_dispatch", new[] { "INSPECT DISPATCH DESK", "KIỂM TRA BÀN ĐIỀU PHỐI" } },
        { "quest.office_action_radio", new[] { "RESTORE RADIO RECORDING", "KHÔI PHỤC BẢN GHI RADIO" } },
        { "quest.office_action_cabinet", new[] { "OPEN RECORDS CABINET", "MỞ TỦ HỒ SƠ" } },
        { "quest.office_action_generic", new[] { "INSPECT", "KIỂM TRA" } },
        { "quest.office_progress_dispatch", new[] { "INSPECTING DISPATCH DESK...", "ĐANG KIỂM TRA BÀN ĐIỀU PHỐI..." } },
        { "quest.office_progress_radio", new[] { "RESTORING RADIO RECORDING...", "ĐANG KHÔI PHỤC BẢN GHI RADIO..." } },
        { "quest.office_progress_cabinet", new[] { "OPENING RECORDS CABINET...", "ĐANG MỞ TỦ HỒ SƠ..." } },
        { "quest.office_progress_generic", new[] { "INSPECTING...", "ĐANG KIỂM TRA..." } },
        { "quest.clues_sender", new[] { "CLUES", "MANH MỐI" } },
        { "quest.all_clues_title", new[] { "ALL CLUES FOUND", "ĐÃ PHÁT HIỆN ĐỦ MANH MỐI" } },
        { "quest.all_clues_body", new[] { "All three records point to the Coordination Section inside the marked hospital. Open the map [M] to inspect it.", "Ba tài liệu cùng dẫn tới Khu Điều phối bên trong bệnh viện được đánh dấu. Mở bản đồ [M] để kiểm tra." } },
        { "quest.cabinet_found_title", new[] { "CLUE FOUND", "ĐÃ TÌM THẤY MANH MỐI" } },
        { "quest.cabinet_found_body", new[] { "An important clue was found.", "Đã tìm thấy manh mối quan trọng." } },
        { "quest.cabinet_empty_title", new[] { "NO CLUE", "KHÔNG CÓ MANH MỐI" } },
        { "quest.cabinet_empty_body", new[] { "There is no clue here. Check another location.", "Chẳng có manh mối gì ở đây cả. Hãy kiểm tra vị trí khác." } },
        { "quest.objective_inspect_car", new[] { "Inspect the stalled car", "Kiểm tra chiếc xe vừa chết máy" } },
        { "quest.objective_search_records", new[] { "Find supply and evacuation records  •  {0}/{1}", "Tìm tài liệu về tuyến tiếp tế và sơ tán  •  {0}/{1}" } },
        { "quest.objective_locate_office", new[] { "Find the Coordination Section inside the marked hospital", "Tìm Khu Điều phối bên trong bệnh viện được đánh dấu" } },
        { "quest.military_arrived", new[] { "Reached the military base. REWARD: evacuation vehicle assessment unlocked.", "Đã tới căn cứ quân sự. PHẦN THƯỞNG: mở đánh giá xe sơ tán." } },
        { "quest.military_failed", new[] { "MISSION FAILED: No survivors remain at the base.", "NHIỆM VỤ THẤT BẠI: Không còn người sống sót tại căn cứ." } },
        { "quest.military_route_blocked", new[] { "Cannot activate: the team already committed to the civilian-car escape.", "Không thể kích hoạt: toàn đội đã cam kết thoát bằng chiếc xe dân sự." } },
        { "quest.military_siege", new[] { "ALARM! The gate is sealed. Collect 3 parts and defend the evacuation vehicle.", "BÁO ĐỘNG! Cổng đã đóng. Thu thập 3 phụ tùng và bảo vệ xe thoát hiểm." } },
        { "quest.military_generator", new[] { "Generator online: the gate now has 150% HP and stuns zombies on contact.", "Máy phát điện đã hoạt động: cổng đạt 150% HP và làm choáng zombie tiếp xúc." } },
        { "quest.military_armory_locked", new[] { "The armory is locked. Find the key in the office safe.", "Kho quân nhu bị khóa. Cần chìa khóa lấy từ két sắt văn phòng." } },
        { "quest.military_armory_open", new[] { "Armory opened: AK47, S12K, ammunition and a level-3 backpack supplied.", "Kho quân nhu đã mở: AK47, S12K, đạn dược và balo cấp 3 đã được cấp." } },
        { "quest.military_safe_open", new[] { "Safe opened: armory key and S12K acquired.", "Két sắt đã mở: nhận chìa khóa kho quân nhu và S12K." } },
        { "quest.military_vehicle_ready", new[] { "The vehicle is repaired. Regroup at the vehicle and press E to leave the area.", "Xe đã sửa xong. Tập hợp tại xe và nhấn E để thoát khỏi khu vực." } },
        { "quest.military_collected", new[] { "Collected: {0}.", "Đã thu thập: {0}." } },
        { "quest.military_installed", new[] { "Installed: {0}.", "Đã lắp: {0}." } },
        { "quest.military_debug_parts", new[] { "ROUTE B TEST: All three evacuation-vehicle parts are installed without using loot containers.", "KIỂM TRA TUYẾN B: Đã mô phỏng lắp đủ ba linh kiện xe sơ tán mà không cần thùng chứa đồ." } },
        { "menu.enter_base_name", new[] { "YOU MUST ENTER THE BASE NAME!", "BẠN PHẢI NHẬP TÊN PHÒNG!" } },
        { "menu.connection_failed", new[] { "CONNECTION FAILED! {0}", "KẾT NỐI THẤT BẠI! {0}" } },
        { "menu.connection_lost", new[] { "Lost connection to server: {0}", "Đã mất kết nối tới máy chủ: {0}" } },
        { "menu.loading_escape_reality", new[] { "ESCAPING FROM REALITY...", "ĐANG THOÁT KHỎI HIỆN THỰC..." } },
        { "menu.loading_wait_players", new[] { "No hope left. Waiting for other doomed souls...", "Hy vọng đã cạn. Đang chờ những linh hồn lạc lối khác..." } },
        { "menu.loading_shelter", new[] { "FINDING A WAY BACK TO SHELTER...", "ĐANG TÌM ĐƯỜNG VỀ NƠI TRÚ ẨN..." } },
        { "menu.loading_escape", new[] { "ESCAPING...{0}%", "ĐANG THOÁT...{0}%" } },
        { "menu.death_loading", new[] { "<color=#990000>THIS IS HOW YOU DIED...</color>", "<color=#990000>ĐÂY LÀ CÁCH BẠN ĐÃ CHẾT...</color>" } },
        { "vehicle.seat.driver", new[] { "DRIVER", "TÀI XẾ" } },
        { "vehicle.seat.front", new[] { "FRONT PASSENGER", "GHẾ PHỤ" } },
        { "vehicle.seat.rear_left", new[] { "REAR LEFT", "GHẾ SAU TRÁI" } },
        { "vehicle.seat.rear_right", new[] { "REAR RIGHT", "GHẾ SAU PHẢI" } },
        { "vehicle.seat.unknown", new[] { "UNKNOWN", "KHÔNG XÁC ĐỊNH" } },
        { "vehicle.seat.status", new[] {
            "SEAT {0} — {1}   |   SHIFT+1-4: SEAT   |   SPACE: BRAKE   |   F: EXIT",
            "GHẾ {0} — {1}   |   SHIFT+1-4: ĐỔI GHẾ   |   SPACE: PHANH   |   F: XUỐNG XE" } },
        { "difficulty.easy.title", new[] { "* EASY MODE *", "* CHẾ ĐỘ DỄ *" } },
        { "difficulty.easy.stats", new[] {
            "<color=#99FF99>ZOMBIE DENSITY:</color> Low (-50% Spawn Rate)\n<color=#99FF99>RESOURCES:</color> Abundant (Loot rate 150%)\n<color=#99FF99>DAMAGE TAKEN:</color> Reduced (-30% Damage)\n<color=#99FF99>STARTING GEAR:</color> Random weapon + 1 magazine, food, bandages & painkiller\n<color=#99FF99>SURVIVAL RATE:</color> Very High (90%)",
            "<color=#99FF99>MẬT ĐỘ ZOMBIE:</color> Thấp (-50% tần suất xuất hiện)\n<color=#99FF99>TÀI NGUYÊN:</color> Dồi dào (tỉ lệ loot 150%)\n<color=#99FF99>SÁT THƯƠNG NHẬN:</color> Giảm (-30% sát thương)\n<color=#99FF99>TRANG BỊ ĐẦU:</color> Súng ngẫu nhiên + 1 băng đạn, thức ăn, băng gạc và thuốc giảm đau\n<color=#99FF99>TỈ LỆ SINH TỒN:</color> Rất cao (90%)" } },
        { "difficulty.easy.desc", new[] {
            "<b>OVERVIEW:</b>\nZombie spawn count is reduced. Ideal for exploring, gathering resources, and learning basic survival mechanics without heavy pressure.",
            "<b>TỔNG QUAN:</b>\nSố zombie xuất hiện được giảm. Phù hợp để khám phá, thu thập tài nguyên và làm quen cơ chế sinh tồn mà không chịu quá nhiều áp lực." } },
        { "difficulty.normal.title", new[] { "• SURVIVAL MODE •", "• CHẾ ĐỘ SINH TỒN •" } },
        { "difficulty.normal.stats", new[] {
            "<color=#FFFF99>ZOMBIE DENSITY:</color> Standard (100% Spawn Rate)\n<color=#FFFF99>RESOURCES:</color> Balanced distribution\n<color=#FFFF99>DAMAGE TAKEN:</color> Normal (100% Damage)\n<color=#FFFF99>STARTING GEAR:</color> Random weapon + 1 magazine & 3 bandages\n<color=#FFFF99>SURVIVAL RATE:</color> Balanced (50%)",
            "<color=#FFFF99>MẬT ĐỘ ZOMBIE:</color> Tiêu chuẩn (100% tần suất)\n<color=#FFFF99>TÀI NGUYÊN:</color> Phân bố cân bằng\n<color=#FFFF99>SÁT THƯƠNG NHẬN:</color> Bình thường (100%)\n<color=#FFFF99>TRANG BỊ ĐẦU:</color> Súng ngẫu nhiên + 1 băng đạn và 3 băng gạc\n<color=#FFFF99>TỈ LỆ SINH TỒN:</color> Cân bằng (50%)" } },
        { "difficulty.normal.desc", new[] {
            "<b>OVERVIEW:</b>\nThe standard zombie survival experience. Spawn rates and cooldown values use their balanced defaults. Requires strategic thinking.",
            "<b>TỔNG QUAN:</b>\nTrải nghiệm sinh tồn zombie tiêu chuẩn. Tần suất xuất hiện và thời gian hồi dùng các giá trị cân bằng mặc định. Người chơi cần suy nghĩ chiến thuật." } },
        { "difficulty.hard.title", new[] { "! HARDCORE MODE !", "! CHẾ ĐỘ KHẮC NGHIỆT !" } },
        { "difficulty.hard.stats", new[] {
            "<color=#FF9999>ZOMBIE DENSITY:</color> Extreme (+150% Spawn Rate)\n<color=#FF9999>RESOURCES:</color> Scarce & Depleted (Loot rate 40%)\n<color=#FF9999>DAMAGE TAKEN:</color> Increased (+50% Damage)\n<color=#FF9999>STARTING GEAR:</color> None\n<color=#FF9999>SURVIVAL RATE:</color> Near Zero (<10%)",
            "<color=#FF9999>MẬT ĐỘ ZOMBIE:</color> Cực cao (+150% tần suất)\n<color=#FF9999>TÀI NGUYÊN:</color> Khan hiếm (tỉ lệ loot 40%)\n<color=#FF9999>SÁT THƯƠNG NHẬN:</color> Tăng (+50% sát thương)\n<color=#FF9999>TRANG BỊ ĐẦU:</color> Không có\n<color=#FF9999>TỈ LỆ SINH TỒN:</color> Gần bằng không (<10%)" } },
        { "difficulty.hard.desc", new[] {
            "<b>OVERVIEW:</b>\nA relentless nightmare. Zombies are extremely numerous and spawn very quickly. Demands maximum skill and tactical planning.",
            "<b>TỔNG QUAN:</b>\nMột cơn ác mộng không ngừng nghỉ. Zombie cực kỳ đông và xuất hiện rất nhanh. Đòi hỏi kỹ năng cao nhất cùng kế hoạch chiến thuật chặt chẽ." } },
        { "loading.connecting", new[] { "Connecting to game session...", "Đang kết nối phiên chơi..." } },
        { "loading.scene_loading", new[] { "Loading map resources...", "Đang nạp tài nguyên bản đồ..." } },
        { "loading.fusion_ready", new[] { "Initializing network session...", "Đang khởi tạo môi trường mạng..." } },
        { "loading.player_spawn_waiting", new[] { "Requesting survivor spawn...", "Đang yêu cầu tạo nhân vật..." } },
        { "loading.avatar_binding", new[] { "Binding character controls...", "Đang liên kết điều khiển..." } },
        { "loading.hud_ready", new[] { "Finalizing interface...", "Đang hoàn tất giao diện..." } },
        { "loading.awaiting_host", new[] { "Awaiting server release...", "Đang chờ máy chủ giải phóng..." } },
        { "loading.ready_complete", new[] { "100% - Ready!", "100% - Sẵn sàng!" } },
        { "loading.failed", new[] { "Loading failed: {0}", "Tải thất bại: {0}" } },
        { "loading.tip.1", new[] { "Tip: Crouch-walking [C] reduces noise, making it easier to sneak past zombies.", "Mẹo: Giữ [C] để ngồi di chuyển lén lút, giảm tiếng động để tránh thu hút zombie." } },
        { "loading.tip.2", new[] { "Tip: Turn off flashlights when in safe areas to save battery and stay hidden.", "Mẹo: Tắt đèn pin khi ở khu vực an toàn để tiết kiệm pin và tránh bị phát hiện." } },
        { "loading.tip.3", new[] { "Tip: Vehicle repair requires specialized tools and parts found across the map.", "Mẹo: Sửa xe cần đầy đủ dụng cụ và phụ tùng nằm rải rác trong bản đồ." } },
        { "loading.tip.4", new[] { "Tip: Balance hunger and thirst to keep stamina and health regeneration high.", "Mẹo: Duy trì mức đói và khát hợp lý để giữ thể lực và hồi máu tốt nhất." } },
        { "loading.tip.5", new[] { "Tip: In multiplayer, stay close to teammates and cover them while performing tasks.", "Mẹo: Khi chơi nhiều người, hãy đi cùng đồng đội và yểm trợ nhau khi sửa chữa." } },
        { "chat.system_prefix", new[] { "SYSTEM", "HỆ THỐNG" } },
        { "chat.player_joined", new[] { "{0} joined the match.", "{0} đã vào trận." } },
        { "chat.player_left", new[] { "{0} has left the match.", "{0} đã rời khỏi trận đấu." } },
        { "chat.death.zombie", new[] { "{0} died to a zombie attack.", "{0} đã chết vì bị zombie tấn công." } },
        { "chat.death.bleeding", new[] { "{0} bled out.", "{0} đã chết vì mất máu." } },
        { "chat.death.infection", new[] { "{0} succumbed to the infection.", "{0} đã chết vì nhiễm trùng." } },
        { "chat.death.starvation", new[] { "{0} starved to death.", "{0} đã chết vì đói." } },
        { "chat.death.dehydration", new[] { "{0} died of thirst.", "{0} đã chết vì khát." } },
        { "chat.death.pvp_killer", new[] { "{0} was killed by {1}.", "{0} đã bị {1} hạ gục." } },
        { "chat.death.pvp_generic", new[] { "{0} was killed by another player.", "{0} đã bị người chơi khác hạ gục." } },
        { "chat.death.unknown", new[] { "{0} has died.", "{0} đã tử vong." } },

        // Route B School & clues
        { "quest.military.clue_dialogue_0", new[] { "So many corpses... Is anyone really left alive in here?", "Nhiều xác chết quá... Liệu trong này thật sự còn ai sống sót không?" } },
        { "quest.military.clue_dialogue_1", new[] { "This looks like a storage depot. The military stockpiled everything here, from ammunition to repair tools.", "Đây có vẻ là một nhà kho. Quân đội đã tích trữ đủ thứ ở đây, từ đạn dược đến dụng cụ sửa chữa." } },
        { "quest.military.clue_dialogue_2", new[] { "Another map fragment... It looks like this base has fallen. The military must have pulled all forces back into the countryside.", "Một mảnh bản đồ mới... Có vẻ căn cứ này đã thất thủ. Quân đội hẳn đã rút toàn bộ lực lượng về vùng nông thôn." } },
        { "quest.military.clue_dialogue_none", new[] { "Nothing unusual here.", "Không có gì khác thường." } },
        { "quest.military.clues_sender", new[] { "MILITARY CLUES", "MANH MỐI QUÂN SỰ" } },
        { "quest.military.clues_progress_complete", new[] { "Team progress: {0}/{1}. Enough clues found to leave the school.", "Tiến độ chung: {0}/{1}. Đã đủ manh mối để rời trường." } },
        { "quest.military.clues_progress", new[] { "Team progress: {0}/{1} clues.", "Tiến độ chung: {0}/{1} manh mối." } },
        { "quest.military.new_clue_title", new[] { "NEW CLUE DISCOVERED", "PHÁT HIỆN MANH MỐI MỚI" } },
        { "quest.military.new_clue_body", new[] { "New clue discovered - press [M] to inspect", "Phát hiện manh mối mới - bấm M để kiểm tra" } },
        { "quest.military.school_exit_blocked", new[] { "Cannot leave the school yet. Inspect all clues first ({0}/{1}).", "Chưa thể rời trường. Hãy kiểm tra đủ manh mối ({0}/{1})." } },
        { "quest.military.police_car_objective", new[] { "The clues all point to the police car in the courtyard. Inspect the car.", "Các manh mối đều nhắc tới chiếc xe cảnh sát trong sân. Hãy tới kiểm tra xe." } },

        // Route B Vote
        { "quest.military.vote_sender", new[] { "ROUTE B VOTE", "BIỂU QUYẾT TUYẾN B" } },
        { "quest.military.vote_cancel_route_locked", new[] { "Cannot commit to Route B because another escape route has already been chosen.", "Không thể khóa Tuyến B vì một tuyến kết thúc khác đã được chọn." } },

        // Route B Repair & Extraction
        { "quest.military.repair_stand_front", new[] { "Stand in front of the vehicle hood to begin repairs.", "Hãy đứng trước mũi xe để sửa chữa." } },
        { "quest.military.repair_state_invalid", new[] { "Cannot repair the vehicle in your current state.", "Không thể sửa xe trong trạng thái hiện tại." } },
        { "quest.military.repair_already_complete", new[] { "This item has already been fully repaired.", "Hạng mục này đã được sửa hoàn tất." } },
        { "quest.military.repair_in_progress_by", new[] { "VEHICLE BEING REPAIRED BY: {0}", "XE ĐANG ĐƯỢC SỬA BỞI: {0}" } },
        { "quest.military.repair_busy_other", new[] { "You are currently repairing another part.", "Bạn đang sửa một hạng mục khác." } },
        { "quest.military.repair_item_required", new[] { "Required item: {0}.", "Cần vật phẩm: {0}." } },
        { "quest.military.escape_sender", new[] { "EVACUATION VEHICLE", "XE SƠ TÁN" } },
        { "quest.military.escape_start_denied_single", new[] { "Cannot depart yet: 1 person is still outside the vehicle.", "Chưa thể khởi động: 1 người còn ở ngoài xe." } },
        { "quest.military.escape_start_denied_multiple", new[] { "Cannot depart yet: {0} people are still outside the vehicle.", "Chưa thể khởi động: {0} người còn ở ngoài xe." } },
        { "quest.military.escape_starting_driver", new[] { "Engine starting... hold formation and prepare to follow directional markers.", "Đang khởi động... giữ đội hình và chuẩn bị lái theo chỉ dẫn." } },
        { "quest.military.escape_starting_team", new[] { "Evacuation vehicle is starting. Prepare to leave the base.", "Xe đang khởi động. Chuẩn bị rời căn cứ." } },
        { "quest.military.escape_unlocked", new[] { "Engine ready — follow the yellow arrows!", "Động cơ đã sẵn sàng — đi theo các mũi tên vàng!" } },
        { "quest.military.gate_broken", new[] { "The gate has fallen! Horde is re-targeting the surviving team.", "Cổng đã vỡ! Đàn zombie chuyển mục tiêu sang đội sống sót." } },
        { "quest.military.repair_interrupted_damage", new[] { "Vehicle repair was interrupted because you took damage.", "Việc sửa xe bị gián đoạn vì bạn vừa nhận sát thương." } },
        { "quest.military.repair_stopped", new[] { "Vehicle repair stopped.", "Đã dừng sửa xe." } },
        { "quest.military.repair_complete_all", new[] { "The police car has completed all 5 repair items.", "Xe cảnh sát đã hoàn tất đủ 5 hạng mục sửa chữa." } },
        { "quest.military.repair_complete_single", new[] { "Completed one repair item on the police car.", "Đã hoàn tất một hạng mục sửa xe cảnh sát." } },

        // Route B OnGUI HUD / Waypoints / Gate
        { "quest.military.school_clues_done", new[] { "ALL 3/3 CLUES FOUND  •  LEAVE THE SCHOOL", "ĐÃ ĐỦ 3/3 MANH MỐI  •  RỜI KHỎI TRƯỜNG HỌC" } },
        { "quest.military.school_clues_progress", new[] { "EXPLORE SCHOOL  •  CLUES {0}/3", "KHÁM PHÁ TRƯỜNG HỌC  •  MANH MỐI {0}/3" } },
        { "quest.military.gate_bar_title", new[] { "MILITARY BASE GATE", "CỔNG KHU QUÂN SỰ" } },
        { "quest.military.police_car_waypoint", new[] { "POLICE CAR  •  {0:0} m\nINSPECT VEHICLE", "XE CẢNH SÁT  •  {0:0} m\nHÃY KIỂM TRA" } },

        // Debug F6 / F10 Chat
        { "quest.debug.route_b_military_unlocked", new[] { "Military route opened. Use F10 or CheatMenu to run base missions.", "Đã mở tuyến quân sự. Dùng F10 hoặc CheatMenu để chạy nhiệm vụ căn cứ." } },
        { "quest.debug.f7_only_neighborhood", new[] { "F7 only works while searching for clues in the residential area.", "F7 chỉ hoạt động khi nhiệm vụ tìm kiếm manh mối trong khu dân cư đang diễn ra." } },

        // Victory Summary UI
        { "victory.title.civilian", new[] { "ESCAPE SUCCESSFUL", "THOÁT HIỂM THÀNH CÔNG" } },
        { "victory.title.military", new[] { "MISSION COMPLETE", "NHIỆM VỤ HOÀN THÀNH" } },
        { "victory.subtitle.civilian", new[] { "THE SURVIVORS BROKE THROUGH THE QUARANTINE IN THE CIVILIAN CAR", "ĐỘI SỐNG SÓT ĐÃ VƯỢT VÒNG PHONG TỎA BẰNG XE DÂN SỰ" } },
        { "victory.subtitle.military", new[] { "THE SURVIVORS ESCAPED THE CITY VIA THE MILITARY ROUTE", "ĐỘI SỐNG SÓT ĐÃ RỜI THÀNH PHỐ QUA TUYẾN QUÂN SỰ" } },
        { "victory.return_menu", new[] { "RETURN TO MAIN MENU", "QUAY VỀ MENU CHÍNH" } },
        { "victory.stat.survival_time", new[] { "SURVIVAL TIME", "THỜI GIAN SINH TỒN" } },
        { "victory.stat.zombies_killed", new[] { "ZOMBIES KILLED", "SỐ ZOMBIE ĐÃ HẠ" } },
        { "victory.stat.difficulty", new[] { "DIFFICULTY", "ĐỘ KHÓ" } },
        { "difficulty.name.easy", new[] { "Easy", "Dễ" } },
        { "difficulty.name.normal", new[] { "Normal", "Thường" } },
        { "difficulty.name.hardcore", new[] { "Hardcore", "Khắc nghiệt" } },
        { "room_placeholder", new[] { "E.g. Refugee Camp...", "VD: Trại tị nạn..." } },

        // Players & Respawns
        { "player.other", new[] { "ANOTHER PLAYER", "NGƯỜI CHƠI KHÁC" } },
        { "quest.respawn_sender", new[] { "TEAM RESPAWN", "HỒI SINH ĐỒNG ĐỘI" } },
        { "quest.respawn_announced", new[] { "{0} used a team respawn. Remaining: {1}/{2}.", "{0} đã sử dụng lượt hồi sinh đồng đội. Còn lại: {1}/{2}." } },
        { "quest.military.respawn_sender", new[] { "MILITARY RESPAWN", "HỒI SINH QUÂN SỰ" } },
        { "quest.military.respawn_body", new[] { "A teammate respawned at the base. {0} team respawns remaining.", "Đồng đội đã hồi sinh tại căn cứ. Còn {0} lượt hồi sinh của đội." } },

        // Route B Vote Cancel
        { "quest.military.vote_cancel_ready", new[] { "The vote was cancelled. You can inspect the vehicle again when the team is ready.", "Biểu quyết đã hủy. Có thể kiểm tra xe lại khi cả đội sẵn sàng." } },
        { "quest.military.vote_cancel_no_players", new[] { "The vote was cancelled because no valid players remain.", "Biểu quyết đã hủy vì không còn người chơi hợp lệ." } },

        // Military Route Intro Cinematic
        { "cinematic.military.broken_car_subtitle", new[] { "Damn it... the vehicle is broken. We must find a way to repair it and escape from here!", "Chết tiệt... xe hỏng rồi. Phải mau chóng tìm cách sửa lại xe và tẩu thoát khỏi đây!" } },
        { "cinematic.military.error_no_avatar", new[] { "[MILITARY CINEMATIC] Could not find live avatar to create visual within 5 seconds.", "[MILITARY CINEMATIC] Không tìm thấy avatar sống để tạo visual sau 5 giây." } },
        { "cinematic.military.log_scene_started", new[] { "[MILITARY CINEMATIC] Starting scene at {0}; vehicle {1}; gate {2}.", "[MILITARY CINEMATIC] Bắt đầu cảnh tại {0}; xe {1}; cổng {2}." } },
        { "cinematic.military.log_walked_to_car", new[] { "[MILITARY CINEMATIC] Host walked to the vehicle.", "[MILITARY CINEMATIC] Host đã đi bộ tới xe." } },
        { "cinematic.military.log_ran_to_gate", new[] { "[MILITARY CINEMATIC] Host ran to gate closing position.", "[MILITARY CINEMATIC] Host đã chạy tới vị trí đóng cổng." } },

        // Route B Repair Interrupts
        { "quest.military.repair_interrupted_disconnect", new[] { "The player repairing the vehicle disconnected.", "Người sửa xe đã ngắt kết nối." } },
        { "quest.military.repair_interrupted_left", new[] { "The player repairing the vehicle left the match.", "Người sửa xe đã rời trận." } },
        { "quest.military.repair_interrupted_generic", new[] { "Vehicle repair was interrupted.", "Việc sửa xe bị gián đoạn." } },
        { "quest.military.repair_interrupted_item_missing", new[] { "The required repair item is no longer in the inventory.", "Vật phẩm sửa chữa không còn trong túi đồ." } },
        { "quest.military.repair_interrupted_zombie", new[] { "Vehicle repair was interrupted by a zombie attack.", "Việc sửa xe bị gián đoạn vì zombie tấn công." } },

        // Debug F6, F8, F9, F12
        { "quest.test_sender", new[] { "QUEST TEST", "KIỂM TRA NHIỆM VỤ" } },
        { "quest.editor_test_sender", new[] { "EDITOR TEST", "KIỂM TRA EDITOR" } },
        { "quest.debug.teleported_to", new[] { "Teleported to {0}.", "Đã dịch chuyển tới {0}." } },
        { "quest.debug.target_school", new[] { "school entrance in the military zone", "lối vào trường học trong khu quân sự" } },
        { "quest.debug.target_inspect_car", new[] { "evacuation vehicle to inspect", "xe sơ tán cần kiểm tra" } },
        { "quest.debug.target_repair_car", new[] { "vehicle needing 5 repair items", "xe cần sửa 5 hạng mục" } },
        { "quest.debug.target_regroup_car", new[] { "evacuation vehicle regroup point", "điểm tập kết xe sơ tán" } },
        { "quest.debug.f8_granted", new[] { "F8 granted {0} missing items. Inventory now has all 5/5 vehicle repair items.", "F8 đã cấp {0} món còn thiếu. Túi đồ hiện đủ 5/5 vật phẩm sửa xe." } },
        { "quest.debug.f8_failed", new[] { "F8 could not grant: {0}. Free up inventory space and try again.", "F8 không thể cấp: {0}. Hãy dọn ô trống trong túi rồi thử lại." } },
        { "quest.debug.f9_granted", new[] { "F9 granted {0} missing items. Inventory now has all 5/5 police car repair items.", "F9 đã cấp {0} món còn thiếu. Túi đồ hiện đủ 5/5 vật phẩm sửa xe cảnh sát." } },
        { "quest.debug.f9_failed", new[] { "F9 could not grant: {0}. Free up inventory space and try again.", "F9 không thể cấp: {0}. Hãy dọn ô trống rồi thử lại." } },
        { "quest.debug.f12_skipped_loot", new[] { "F12 skipped: the objective requires finding records in loot containers.", "F12 bị bỏ qua: nhiệm vụ tìm hồ sơ dùng thùng chứa đồ." } },
        { "quest.debug.map_regions_unlocked", new[] { "All hospital and military map regions have been unlocked.", "Đã mở toàn bộ hai vùng bản đồ Bệnh viện và Quân sự." } },

        // Hospital & Radio Quest Messages
        { "quest.hospital.sender", new[] { "HOSPITAL MISSION", "NHIỆM VỤ BỆNH VIỆN" } },
        { "quest.hospital.door_opened", new[] { "Auxiliary radio station opened. Radio is now ready to inspect.", "Đã mở Trạm liên lạc phụ trợ. Radio hiện sẵn sàng để kiểm tra." } },
        { "quest.hospital.radio_ready", new[] { "Radio is ready. Signal broadcast and restoration will begin at H3.", "Radio đã sẵn sàng. Nội dung phát sóng và phục hồi tín hiệu sẽ bắt đầu ở H3." } },
        { "quest.hospital.door_opened_title", new[] { "RADIO STATION OPENED", "TRẠM RADIO ĐÃ MỞ" } },
        { "quest.hospital.radio_ready_title", new[] { "RADIO READY", "RADIO SẴN SÀNG" } },
        { "quest.radio.restored_sender", new[] { "RADIO RECORDING RESTORED", "BẢN GHI RADIO ĐÃ KHÔI PHỤC" } },
        { "quest.radio.restored_body", new[] { "Transcript saved to journal. The radio memory contains coordinates for the Northern Base; survivors unconfirmed.", "Bản ghi đã lưu trong Nhật ký. Bộ nhớ máy chứa tọa độ Căn cứ phía Bắc; chưa rõ ở đó còn ai sống." } },
        { "quest.radio.map_fragment_title", new[] { "MAP FRAGMENT 2", "MẢNH BẢN ĐỒ 2" } },
        { "quest.radio.map_fragment_body", new[] { "Extracted beacon frequency and coordinates from radio memory.", "Đã trích xuất tần số đèn hiệu và tọa độ từ bộ nhớ Radio." } },
        { "quest.radio.map_fragment_recorded", new[] { "Military base location has been recorded onto the map.", "Vị trí căn cứ quân sự đã được ghi vào bản đồ." } },
        { "quest.hospital.radio_noise_source", new[] { "RADIO NOISE", "RADIO NHIỄU" } },
        { "quest.hospital.radio_threat_title", new[] { "RADIO NOISE BURST  •  STAGE {0}/3", "RADIO BÙNG NHIỄU  •  CHẶNG {0}/3" } },
        { "quest.hospital.radio_threat_body", new[] { "There is movement outside. You may investigate or continue repairing the Radio.", "Có tiếng động bên ngoài. Bạn có thể ra kiểm tra hoặc tiếp tục sửa Radio." } },
        { "quest.hospital.door_sender", new[] { "RADIO STATION DOOR", "CỬA TRẠM RADIO" } },
        { "quest.hospital.need_key_title", new[] { "KEY REQUIRED", "CẦN CHÌA KHÓA" } },
        { "quest.hospital.door_locked_find_key", new[] { "Door locked. Spare key marked in supervisor office.", "Cửa bị khóa. Chìa dự phòng đã được đánh dấu trong khu văn phòng trưởng ca." } },
        { "quest.hospital.door_locked_find_log2", new[] { "Door locked. Check supervisor office behind reception desk.", "Cửa bị khóa. Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân." } },
        { "quest.hospital.door_locked_find_log1", new[] { "Door locked. Check shift log at reception desk for spare key location.", "Cửa bị khóa. Kiểm tra sổ trực tại quầy tiếp tân để tìm nơi cất chìa dự phòng." } },
        { "quest.hospital.shift_log_sender", new[] { "HOSPITAL SHIFT LOG", "SỔ TRỰC BỆNH VIỆN" } },
        { "quest.hospital.shift_log_chat", new[] { "Radio station key kept by supervisor in administrative office.", "Chìa khóa Trạm Radio do trưởng ca giữ tại văn phòng hành chính." } },
        { "quest.hospital.shift_log_body", new[] { "Treatment area closed.\nAll emergency communications redirected to Auxiliary Station behind hospital.\nSpare key kept by supervisor in administrative office.\n\nJournal: Check supervisor office behind reception desk.", "Khu điều trị đã đóng.\nToàn bộ liên lạc khẩn cấp chuyển sang Trạm phụ trợ phía sau bệnh viện.\nChìa khóa dự phòng do trưởng ca giữ tại văn phòng hành chính.\n\nNhật ký: Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân." } },
        { "quest.hospital.supervisor_sender", new[] { "SUPERVISOR OFFICE", "VĂN PHÒNG TRƯỞNG CA" } },
        { "quest.hospital.supervisor_chat", new[] { "Identified possible spare key location. Waypoint updated.", "Đã xác định vị trí có thể cất chìa dự phòng. Điểm đến đã được cập nhật." } },
        { "quest.hospital.red_alert_title", new[] { "RED ALERT PROTOCOL", "LỆNH PHONG TỎA CẤP ĐỎ" } },
        { "quest.hospital.red_alert_body", new[] { "Red alert confirmed.\nConvoys prohibited from stopping at hospital.\nCommunications officer infected, locked themselves in Auxiliary Station to keep radio alive.\n\nJournal: Find radio key at marked location.", "Lệnh phong tỏa cấp đỏ đã được xác nhận.\nĐoàn xe không được dừng tại bệnh viện.\nNhân viên liên lạc có dấu hiệu nhiễm bệnh và đã tự khóa mình tại Trạm phụ trợ để giữ kênh Radio hoạt động.\n\nNhật ký: Tìm chìa khóa Radio tại vị trí đã được đánh dấu." } },
        { "quest.hospital.radio_key_sender", new[] { "RADIO KEY", "CHÌA KHÓA RADIO" } },
        { "quest.hospital.radio_key_chat", new[] { "Team acquired shared key. Waypoint moved to Radio Station.", "Đội đã nhận chìa khóa dùng chung. Điểm đến chuyển tới Trạm Radio." } },
        { "quest.hospital.radio_key_title", new[] { "RADIO KEY ACQUIRED", "ĐÃ NHẶT CHÌA KHÓA RADIO" } },
        { "quest.hospital.radio_key_body", new[] { "Acquired spare radio station key.\nKey is shared among teammates and uses no inventory slot.\n\nJournal: Open Auxiliary Station behind hospital.", "Đã nhặt chìa khóa dự phòng của Trạm Radio.\nChìa khóa là trạng thái dùng chung của toàn đội và không chiếm ô kho đồ.\n\nNhật ký: Mở Trạm liên lạc phụ trợ phía sau bệnh viện." } },

        // Arrival Car Repair & Start Messages
        { "quest.arrival.repair_already_complete", new[] { "This item has already been repaired.", "Hạng mục này đã được sửa hoàn tất." } },
        { "quest.arrival.repair_busy_other", new[] { "You are currently repairing another part.", "Bạn đang sửa một hạng mục khác." } },
        { "quest.arrival.repair_in_progress_other", new[] { "Another player is currently repairing this vehicle.", "Một người chơi khác đang sửa chiếc xe này." } },
        { "quest.arrival.repair_stand_front", new[] { "Stand in front of the hood inspection area to repair.", "Hãy đứng trong vùng kiểm tra trước capo để sửa xe." } },
        { "quest.arrival.repair_state_invalid", new[] { "Cannot repair the vehicle in your current state.", "Không thể sửa xe trong trạng thái hiện tại." } },
        { "quest.arrival.repair_item_missing", new[] { "Missing required items. Open journal [J] for the checklist.", "Thiếu vật phẩm phù hợp. Mở nhật ký [J] để xem danh sách." } },
        { "quest.arrival.repair_invalid_data", new[] { "Invalid vehicle repair item data.", "Dữ liệu hạng mục sửa xe không hợp lệ." } },
        { "quest.arrival.repair_inventory_changed", new[] { "Items changed in inventory. Please check [J] again.", "Vật phẩm vừa thay đổi trong túi đồ. Hãy kiểm tra lại [J]." } },
        { "quest.arrival.start_stand_front", new[] { "Stand in front of the hood inspection area to start.", "Phải đứng trong vùng kiểm tra trước mũi xe để khởi động." } },
        { "quest.arrival.start_parts_incomplete", new[] { "Engine, fuel, battery, and front-left tire not fully resolved.", "Động cơ, nhiên liệu, ắc quy và lốp trước trái chưa được xử lý đầy đủ." } },
        { "quest.arrival.start_failed_prefab", new[] { "Could not activate vehicle. Try again or verify car configuration.", "Không thể kích hoạt phương tiện. Hãy thử lại hoặc kiểm tra cấu hình xe." } },
        { "quest.arrival.start_success", new[] { "Engine started. Civilian car is ready for exploration and escape.", "Động cơ đã nổ máy. Xe dân sự đã sẵn sàng để khám phá và thoát hiểm." } },
        { "quest.arrival.repair_title_success", new[] { "VEHICLE REPAIR", "SỬA XE" } },
        { "quest.arrival.repair_title_fail", new[] { "REPAIR FAILED", "KHÔNG THỂ SỬA" } },
        { "quest.arrival.start_title_success", new[] { "START VEHICLE", "KHỞI ĐỘNG XE" } },
        { "quest.arrival.start_title_fail", new[] { "START FAILED", "KHÔNG THỂ KHỞI ĐỘNG" } },
        { "quest.arrival.repair_core_success", new[] { "Serviced the hood, starter, and engine. Hammer and tool kit preserved.", "Đã xử lý nắp capo, bộ đề và động cơ. Búa và bộ dụng cụ được giữ lại." } },
        { "quest.arrival.add_fuel_success", new[] { "Poured the fuel can into the tank.", "Đã đổ can nhiên liệu vào bình." } },
        { "quest.arrival.replace_battery_success", new[] { "Installed the new battery. Required before starting the car.", "Đã lắp ắc quy mới. Đây là hạng mục bắt buộc trước khi khởi động." } },
        { "quest.arrival.replace_tire_success", new[] { "Replaced damaged front-left tire. Required before driving.", "Đã thay lốp trước trái bị hỏng. Đây là hạng mục bắt buộc trước khi chạy." } },
        { "quest.arrival.status_updated", new[] { "Vehicle status updated.", "Đã cập nhật tình trạng xe." } },

        // Arrival Car Inspection UI
        { "arrival_ui.header_eyebrow_police", new[] { "VEHICLE INSPECTION  //  POLICE CAR", "KIỂM TRA PHƯƠNG TIỆN  //  XE CẢNH SÁT" } },
        { "arrival_ui.header_eyebrow_civilian", new[] { "VEHICLE INSPECTION  //  CIVILIAN CAR", "KIỂM TRA PHƯƠNG TIỆN  //  XE DÂN DỤNG" } },
        { "arrival_ui.vehicle_police", new[] { "PATROL VEHICLE", "XE TUẦN TRA" } },
        { "arrival_ui.vehicle_civilian", new[] { "CHEVALIER NYALA", "CHEVALIER NYALA" } },
        { "arrival_ui.header_title", new[] { "VEHICLE CONDITION", "TÌNH TRẠNG XE" } },
        { "arrival_ui.footer_hint", new[] { "[E] CLOSE     •     [ESC] CLOSE     •     OR CLICK  ×", "[E] ĐÓNG     •     [ESC] ĐÓNG     •     HOẶC BẤM  ×" } },
        { "arrival_ui.diagram_label", new[] { "SELECT A PART TO INSPECT", "CHỌN BỘ PHẬN ĐỂ KIỂM TRA" } },
        { "arrival_ui.diagram_hint", new[] { "CLICK AN ICON OR PART ON THE VEHICLE", "BẤM VÀO BIỂU TƯỢNG HOẶC BỘ PHẬN TRÊN XE" } },
        { "arrival_ui.overall_condition", new[] { "OVERALL CONDITION: {0}%", "TÌNH TRẠNG TỔNG THỂ: {0}%" } },
        { "arrival_ui.repair_clock_label", new[] { "REPAIRING", "ĐANG SỬA" } },
        { "arrival_ui.repair_clock_cancel", new[] { "[E] / [ESC]  CANCEL", "[E] / [ESC]  HỦY" } },
        { "arrival_ui.group_engine", new[] { "ENGINE BAY", "KHOANG ĐỘNG CƠ" } },
        { "arrival_ui.group_fuel", new[] { "FUEL", "NHIÊN LIỆU" } },
        { "arrival_ui.group_wheels", new[] { "WHEELS", "BÁNH XE" } },
        { "arrival_ui.group_body", new[] { "BODYWORK", "THÂN XE" } },
        { "arrival_ui.action_inspect", new[] { "Inspect", "Kiểm tra" } },
        { "arrival_ui.action_repair", new[] { "Repair", "Sửa chữa" } },
        { "arrival_ui.action_replace", new[] { "Replace Part", "Thay linh kiện" } },
        { "arrival_ui.action_refuel", new[] { "Add Fuel", "Đổ nhiên liệu" } },
        { "arrival_ui.repairing_progress", new[] { "PERFORMING REPAIR. MAINTAIN POSITION UNTIL THE DIAL COMPLETES A FULL ROTATION.", "ĐANG THỰC HIỆN SỬA CHỮA. GIỮ NGUYÊN VỊ TRÍ CHO TỚI KHI KIM QUAY ĐỦ MỘT VÒNG." } },
        { "arrival_ui.status_completed_server", new[] { "STATUS: ITEM COMPLETED AND CONFIRMED BY SERVER.", "TRẠNG THÁI: HẠNG MỤC ĐÃ HOÀN THÀNH VÀ ĐƯỢC SERVER XÁC NHẬN." } },
        { "arrival_ui.status_repairing", new[] { "REPAIRING...", "ĐANG SỬA..." } },
        { "arrival_ui.status_completed", new[] { "COMPLETED", "ĐÃ HOÀN THÀNH" } },
        { "arrival_ui.inspect_result_prefix", new[] { "INSPECTION RESULT: ", "KẾT QUẢ KIỂM TRA: " } },
        { "arrival_ui.chat_sender_inspect", new[] { "VEHICLE INSPECTION", "KIỂM TRA XE" } },
        { "arrival_ui.police_not_connected", new[] { "CANNOT CONNECT TO POLICE CAR STATE.", "CHƯA THỂ KẾT NỐI VỚI TRẠNG THÁI XE CẢNH SÁT." } },
        { "arrival_ui.verifying_server", new[] { "CONFIRMING ITEMS WITH SERVER...", "ĐANG XÁC NHẬN VẬT PHẨM VỚI SERVER..." } },
        { "arrival_ui.verifying_button", new[] { "CONFIRMING...", "ĐANG XÁC NHẬN..." } },
        { "arrival_ui.quest_not_connected", new[] { "CANNOT CONNECT TO QUEST STATE. PLEASE RETRY.", "CHƯA THỂ KẾT NỐI VỚI TRẠNG THÁI NHIỆM VỤ. HÃY THỬ LẠI." } },
        { "arrival_ui.start_server_not_connected", new[] { "CANNOT CONNECT TO SERVER TO START VEHICLE.", "CHƯA THỂ KẾT NỐI VỚI SERVER ĐỂ KHỞI ĐỘNG XE." } },
        { "arrival_ui.start_missing_parts_warn", new[] { "CANNOT START: MUST REPAIR ENGINE, ADD FUEL, REPLACE BATTERY AND FRONT-LEFT TIRE.", "CHƯA THỂ KHỞI ĐỘNG: CẦN SỬA ĐỘNG CƠ, ĐỔ NHIÊN LIỆU, THAY ẮC QUY VÀ LỐP TRƯỚC TRÁI." } },
        { "arrival_ui.starting_button", new[] { "STARTING...", "ĐANG KHỞI ĐỘNG..." } },
        { "arrival_ui.police_repair_done", new[] { "REPAIRS COMPLETE", "SỬA XE HOÀN TẤT" } },
        { "arrival_ui.police_repaired_count", new[] { "REPAIRED {0}/5", "ĐÃ SỬA {0}/5" } },
        { "arrival_ui.vehicle_started_btn", new[] { "ENGINE RUNNING", "XE ĐÃ KHỞI ĐỘNG" } },
        { "arrival_ui.start_vehicle_btn", new[] { "START VEHICLE", "KHỞI ĐỘNG XE" } },
        { "arrival_ui.cannot_start_btn", new[] { "CANNOT START", "CHƯA THỂ KHỞI ĐỘNG" } },
        { "arrival_ui.failed_prefix", new[] { "CANNOT PERFORM: ", "KHÔNG THỂ THỰC HIỆN: " } },
        { "arrival_ui.stopped_prefix", new[] { "STOPPED: ", "ĐÃ DỪNG: " } },
        { "arrival_ui.police_all_repaired_diag", new[] { "COMPLETED: VEHICLE REPAIRED ALL 5 ITEMS.", "HOÀN TẤT: XE ĐÃ SỬA ĐỦ 5 HẠNG MỤC." } },
        { "arrival_ui.police_part_repaired_diag", new[] { "REPAIR ITEM COMPLETED.", "HOÀN TẤT HẠNG MỤC SỬA CHỮA." } },
        { "arrival_ui.completed_prefix", new[] { "COMPLETED: ", "HOÀN TẤT: " } },
        { "arrival_ui.start_success_prefix", new[] { "STARTED SUCCESSFULLY: ", "KHỞI ĐỘNG THÀNH CÔNG: " } },
        { "arrival_ui.diagnosis_prefix", new[] { "DIAGNOSIS: ", "CHẨN ĐOÁN: " } },
        { "arrival_ui.track_items_hint", new[] { "  •  TRACK ITEMS IN [J]", "  •  VẬT PHẨM THEO DÕI TRONG [J]" } },
        { "arrival_ui.progress_prefix", new[] { "  •  PROGRESS: ", "  •  TIẾN ĐỘ: " } },
        { "arrival_ui.needs_prefix", new[] { "  •  REQUIRED: ", "  •  CẦN: " } },

        // Arrival UI Parts
        { "arrival_ui.part.engine.name", new[] { "Engine", "Động cơ" } },
        { "arrival_ui.part.engine.desc", new[] { "Engine is overheated and starter motor is stuck.", "Động cơ bị quá nhiệt và bộ đề đang kẹt." } },
        { "arrival_ui.part.engine.rec", new[] { "Cool engine, inspect starter motor and ignition system.", "Làm nguội động cơ, kiểm tra bộ đề và hệ thống đánh lửa." } },
        { "arrival_ui.part.battery.name", new[] { "Battery", "Ắc quy" } },
        { "arrival_ui.part.battery.desc", new[] { "Battery is completely dead and cannot supply power to starter.", "Ắc quy đã chết hoàn toàn và không còn khả năng cấp điện cho bộ đề." } },
        { "arrival_ui.part.battery.rec", new[] { "Must replace battery before attempting to start.", "Bắt buộc thay ắc quy trước khi thử khởi động." } },
        { "arrival_ui.part.exhaust.name", new[] { "Exhaust", "Ống xả" } },
        { "arrival_ui.part.exhaust.desc", new[] { "Exhaust intact, no severe leaks detected.", "Ống xả còn nguyên, chưa phát hiện rò khí nghiêm trọng." } },
        { "arrival_ui.part.exhaust.rec", new[] { "No immediate action required.", "Chưa cần can thiệp ngay." } },
        { "arrival_ui.part.fuel.name", new[] { "Fuel Tank", "Bình xăng" } },
        { "arrival_ui.part.fuel.desc", new[] { "Tank is nearly empty; no reserve fuel on board.", "Bình gần như cạn và không còn nhiên liệu dự phòng trên xe." } },
        { "arrival_ui.part.fuel.rec", new[] { "Add fuel before attempting to start.", "Bổ sung nhiên liệu trước khi thử khởi động." } },
        { "arrival_ui.part.front_left.name", new[] { "Front Left Tire", "Lốp trước trái" } },
        { "arrival_ui.part.front_left.desc", new[] { "Front left tire is punctured and cannot support load.", "Lốp trước trái đã thủng và không thể chịu tải." } },
        { "arrival_ui.part.front_left.rec", new[] { "Must replace front left tire before driving.", "Bắt buộc thay lốp trước trái trước khi cho xe chạy." } },
        { "arrival_ui.part.rear_left.name", new[] { "Rear Left Tire", "Lốp sau trái" } },
        { "arrival_ui.part.rear_left.desc", new[] { "Rear left tire is usable.", "Lốp sau trái còn sử dụng được." } },
        { "arrival_ui.part.rear_left.rec", new[] { "Monitor pressure after vehicle is running.", "Theo dõi áp suất sau khi xe hoạt động." } },
        { "arrival_ui.part.front_right.name", new[] { "Front Right Tire", "Lốp trước phải" } },
        { "arrival_ui.part.front_right.desc", new[] { "Front right tire shows surface aging.", "Lốp trước phải có dấu hiệu chai bề mặt." } },
        { "arrival_ui.part.front_right.rec", new[] { "Usable temporarily; avoid hard acceleration.", "Có thể sử dụng tạm thời, tránh tăng tốc gấp." } },
        { "arrival_ui.part.rear_right.name", new[] { "Rear Right Tire", "Lốp sau phải" } },
        { "arrival_ui.part.rear_right.desc", new[] { "Rear right tire has uneven wear but is intact.", "Lốp sau phải mòn không đều nhưng chưa thủng." } },
        { "arrival_ui.part.rear_right.rec", new[] { "Can continue using for short distance.", "Có thể tiếp tục sử dụng trong quãng đường ngắn." } },
        { "arrival_ui.part.hood.name", new[] { "Hood", "Nắp capo" } },
        { "arrival_ui.part.hood.desc", new[] { "Hood deformed from heat and obstructing starter latch.", "Nắp capo biến dạng do nhiệt và đang che khuất điểm kẹt của bộ đề." } },
        { "arrival_ui.part.hood.rec", new[] { "Pop hood and clear latch before repairing engine.", "Mở nắp và xử lý cơ cấu khóa trước khi sửa động cơ." } },
        { "arrival_ui.part.windshield.name", new[] { "Windshield", "Kính chắn gió" } },
        { "arrival_ui.part.windshield.desc", new[] { "Windshield has several scratches but intact.", "Kính chắn gió có nhiều vết xước nhưng chưa vỡ." } },
        { "arrival_ui.part.windshield.rec", new[] { "Visibility acceptable under illuminated conditions.", "Tầm nhìn vẫn chấp nhận được trong điều kiện sáng." } },
        { "arrival_ui.part.front_door.name", new[] { "Front Door", "Cửa trước" } },
        { "arrival_ui.part.front_door.desc", new[] { "Front door, hinges, and locks operate normally.", "Cửa trước, bản lề và khóa vẫn hoạt động bình thường." } },
        { "arrival_ui.part.front_door.rec", new[] { "No repairs needed.", "Không cần sửa chữa." } },

        // Broken Arrival Car & Roadside Repair Station
        { "quest.arrival.inspecting_engine", new[] { "INSPECTING ENGINE...", "ĐANG KIỂM TRA ĐỘNG CƠ..." } },
        { "quest.arrival.prompt_inspect", new[] { "HOLD [E]\nINSPECT VEHICLE", "GIỮ [E]\nKIỂM TRA XE" } },
        { "quest.police.inspecting", new[] { "INSPECTING POLICE CAR...", "ĐANG KIỂM TRA XE CẢNH SÁT..." } },
        { "quest.police.prompt_inspect", new[] { "INSPECT POLICE CAR\nHOLD [E]", "KIỂM TRA XE CẢNH SÁT\nGIỮ [E]" } },
        { "quest.police.prompt_repair", new[] { "INSPECT / REPAIR VEHICLE\nHOLD [E]", "KIỂM TRA / SỬA XE\nGIỮ [E]" } },

        // Military Escape Vehicle Repair & Skill Check UI
        { "quest.military.prompt_inspect_vehicle", new[] { "[E]  INSPECT MILITARY VEHICLE", "[E]  KIỂM TRA XE QUÂN SỰ" } },
        { "quest.military.prompt_install_parts", new[] { "[E]  INSTALL PARTS  •  {0}", "[E]  LẮP PHỤ TÙNG  •  {0}" } },
        { "quest.military.prompt_escape_base", new[] { "[E]  REGROUP TEAM AND ESCAPE BASE", "[E]  TẬP HỢP ĐỘI VÀ THOÁT KHỎI CĂN CỨ" } },
        { "quest.military.repair_progress_label", new[] { "REPAIR VEHICLE  {0}%", "SỬA XE  {0}%" } },
        { "quest.military.prompt_resume_repair", new[] { "RELEASE [E] THEN HOLD AGAIN TO RESUME REPAIR", "THẢ [E] RỒI GIỮ LẠI ĐỂ TIẾP TỤC SỬA" } },
        { "quest.military.prompt_hold_repair", new[] { "HOLD [E]  REPAIR VEHICLE", "GIỮ [E]  SỬA XE" } },
        { "quest.military.repair_parts_status", new[] { "Battery {0}  Fuel {1}  Repair Kit {2}", "Ắc quy {0}  Nhiên liệu {1}  Bộ sửa {2}" } },
        { "quest.skill_check.start", new[] { "STARTING VEHICLE REPAIR", "BẮT ĐẦU SỬA XE" } },
        { "quest.skill_check.perfect", new[] { "PERFECT  +7%", "HOÀN HẢO  +7%" } },
        { "quest.skill_check.success", new[] { "SUCCESS  +3.5%", "THÀNH CÔNG  +3.5%" } },
        { "quest.skill_check.miss", new[] { "MISS  -2%     1-SEC PAUSE", "TRƯỢT  -2%     TẠM DỪNG 1 GIÂY" } },
        { "quest.skill_check.all_done", new[] { "ALL 5 ITEMS COMPLETED", "ĐÃ HOÀN TẤT ĐỦ 5 HẠNG MỤC" } },
        { "quest.skill_check.item_done", new[] { "REPAIR ITEM COMPLETED", "HẠNG MỤC SỬA CHỮA HOÀN TẤT" } },
        { "quest.skill_check.stopped_saved", new[] { "REPAIR STOPPED. ITEM PROGRESS SAVED.", "ĐÃ DỪNG SỬA. TIẾN ĐỘ HẠNG MỤC ĐƯỢC GIỮ LẠI." } },
        { "quest.skill_check.space_hint", new[] { "SPACE  TIMING CHECK", "SPACE  CANH THỜI ĐIỂM" } },
        { "quest.skill_check.recovering", new[] { "RECOVERING...", "ĐANG KHẮC PHỤC..." } },
        { "quest.skill_check.preparing", new[] { "PREPARING TIMING CHECK", "CHUẨN BỊ CANH THỜI ĐIỂM" } },
        { "quest.skill_check.esc_hint", new[] { "ESC  EXIT REPAIR     PROGRESS SAVED", "ESC  RỜI SỬA     TIẾN ĐỘ ĐƯỢC GIỮ LẠI" } },
        { "quest.skill_check.progress_bar", new[] { "REPAIR PROGRESS  {0:0.0}%", "TIẾN ĐỘ SỬA  {0:0.0}%" } },

        // Military School Clue Point
        { "quest.military.inspecting_clue", new[] { "INSPECTING CLUE...", "ĐANG KIỂM TRA MANH MỐI..." } },
        { "quest.military.prompt_hold_inspect", new[] { "HOLD [E] TO INSPECT", "GIỮ [E] ĐỂ KIỂM TRA" } },
        { "quest.military.clue_label_0", new[] { "CASUALTY AREA", "KHU VỰC TỬ THƯƠNG" } },
        { "quest.military.clue_label_1", new[] { "SUPPLY DEPOT", "NHÀ KHO QUÂN NHU" } },
        { "quest.military.clue_label_2", new[] { "FINAL MAP FRAGMENT", "MẢNH BẢN ĐỒ CUỐI" } },

        // Military Route Vote UI
        { "quest.vote.eyebrow", new[] { "POINT OF NO RETURN  //  TEAM VOTE", "ĐIỂM KHÔNG THỂ QUAY LẠI  //  BIỂU QUYẾT TOÀN ĐỘI" } },
        { "quest.vote.title", new[] { "CONTINUE MILITARY STORYLINE ROUTE?", "TIẾP TỤC TUYẾN CỐT TRUYỆN QUÂN SỰ?" } },
        { "quest.vote.body", new[] { "If the entire team agrees, the main storyline route will begin and you cannot return to the free route in this session. If even one player declines, the vote will be cancelled and the vehicle can be inspected again later.", "Nếu toàn đội đồng ý, tuyến cốt truyện chính sẽ bắt đầu và không thể quay lại tuyến tự do trong phiên chơi này. Chỉ cần một người từ chối, biểu quyết sẽ hủy và xe có thể được kiểm tra lại sau." } },
        { "quest.vote.status_waiting", new[] { "AGREED  •  WAITING FOR {0}/{1}", "ĐÃ ĐỒNG Ý  •  ĐANG CHỜ {0}/{1}" } },
        { "quest.vote.status_counts", new[] { "APPROVAL VOTES: {0}/{1}", "PHIẾU ĐỒNG Ý: {0}/{1}" } },
        { "quest.vote.btn_agree", new[] { "[ENTER / Y]  AGREE", "[ENTER / Y]  ĐỒNG Ý" } },
        { "quest.vote.btn_decline", new[] { "[ESC / N]  DECLINE", "[ESC / N]  TỪ CHỐI" } },

        // Civilian Escape Route & Presentation
        { "quest.civilian.countdown", new[] { "City departure countdown: {0}s...", "Bắt đầu đếm ngược rời thành phố: {0}s..." } },
        { "quest.civilian.prompt_drive", new[] { "[E]  START BREAKING THROUGH BLOCKADE  •  POINT OF NO RETURN", "[E]  BẮT ĐẦU VƯỢT VÒNG PHONG TỎA  •  ĐIỂM KHÔNG THỂ QUAY LẠI" } },
        { "quest.civilian.prompt_wait_team", new[] { "WAIT FOR SURVIVING TEAM MEMBERS TO GATHER NEAR VEHICLE", "CHỜ CÁC THÀNH VIÊN CÒN SỐNG TẬP KẾT GẦN XE" } },
        { "quest.civilian.cinematic_eyebrow", new[] { "ESCAPE ROUTE A  //  VEHICLE READY", "TUYẾN THOÁT HIỂM A  //  PHƯƠNG TIỆN SẴN SÀNG" } },
        { "quest.civilian.cinematic_title", new[] { "VEHICLE OPERATIONAL", "XE ĐÃ HOẠT ĐỘNG" } },
        { "quest.civilian.cinematic_body", new[] { "A route to leave the city has been identified", "Đã xác định một tuyến đường có thể rời khỏi thành phố" } },
        { "quest.civilian.outro_title", new[] { "LEFT THE CITY", "ĐÃ RỜI KHỎI THÀNH PHỐ" } },
        { "quest.civilian.outro_subtitle", new[] { "A new journey awaits ahead...", "Một chặng đường mới đang chờ phía trước..." } },
        { "quest.military.interact_generator", new[] { "START", "KHỞI ĐỘNG" } },
        { "quest.military.interact_armory", new[] { "UNLOCK", "MỞ KHÓA" } },
        { "quest.military.interact_safe", new[] { "OPEN SAFE", "MỞ KÉT" } },
        { "quest.military.interact_collect", new[] { "COLLECT", "THU THẬP" } },
        { "quest.clue_picked_up", new[] { "Picked up: {0}.", "Đã nhặt: {0}." } },
        { "quest.zone_search_configured", new[] { "Designated a search zone with {0} houses.", "Đã khoanh vùng tìm kiếm gồm {0} căn nhà." } },
        { "quest.return_to_objective", new[] { "RETURN TO OBJECTIVE  •  {0:0} m", "QUAY LẠI MỤC TIÊU  •  {0:0} m" } },
        { "quest.new_clue_detected_sender", new[] { "NEW CLUE DETECTED", "PHÁT HIỆN MANH MỐI MỚI" } },
        { "quest.new_clue_detected_body", new[] { "New clue detected - press [M] to inspect", "Phát hiện manh mối mới - bấm M để kiểm tra" } },
        { "quest.office_revealed_chat", new[] { "Purple office location confirmed.", "Đã xác định văn phòng màu tím." } },
        { "quest.boundary.sender", new[] { "AREA BOUNDARY", "GIỚI HẠN KHU VỰC" } },
        { "quest.boundary.warning", new[] { "The outside area is not safe yet. Search for clues within the marked district on your map.", "Phía ngoài chưa an toàn. Hãy tìm manh mối trong khu được đánh dấu trên bản đồ." } },
    };

    private static readonly Dictionary<string, string[]> LiteralText = CreateLiteralTable();

    public static string Get(string key, string fallback = null)
    {
        if (Text.TryGetValue(key, out string[] values))
            return values[(int)Current];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[LOCALIZATION] Missing key '{key}'.");
#endif
        return fallback ?? key;
    }

    public static string TranslateLiteral(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        const string dropdownSuffix = "  ▼";
        bool hasDropdownSuffix = value.EndsWith(dropdownSuffix, StringComparison.Ordinal);
        string coreValue = hasDropdownSuffix
            ? value.Substring(0, value.Length - dropdownSuffix.Length)
            : value;

        // Serialized scene labels sometimes contain a trailing newline or
        // layout spaces. Match the visible content while preserving those
        // characters so localization does not disturb the UI layout.
        int contentStart = 0;
        while (contentStart < coreValue.Length && char.IsWhiteSpace(coreValue[contentStart])) contentStart++;
        int contentEnd = coreValue.Length - 1;
        while (contentEnd >= contentStart && char.IsWhiteSpace(coreValue[contentEnd])) contentEnd--;
        string prefix = coreValue.Substring(0, contentStart);
        string matchValue = contentEnd >= contentStart
            ? coreValue.Substring(contentStart, contentEnd - contentStart + 1)
            : coreValue;
        string suffix = contentEnd + 1 < coreValue.Length
            ? coreValue.Substring(contentEnd + 1)
            : string.Empty;

        foreach (KeyValuePair<string, string[]> pair in LiteralText)
        {
            if (matchValue == pair.Value[0] || matchValue == pair.Value[1])
                return prefix + pair.Value[(int)Current] + suffix + (hasDropdownSuffix ? dropdownSuffix : string.Empty);
        }
        return value;
    }

    static GameLocalization()
    {
        QuestUILocalization.SetStringLookup((k, fb) => Get(k, fb));
    }

    public static void SetLanguage(Language language, bool save = true)
    {
        Current = language;
        if (save)
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
        }
        QuestUILocalization.SetVietnamese(IsVietnamese);
        QuestUILocalization.SetStringLookup((k, fb) => Get(k, fb));
        LanguageChanged?.Invoke();
    }

    public static TMP_FontAsset GetRuntimeFont(TMP_FontAsset preferred = null)
    {
        if (preferred != null) return preferred;

        // Font fallbacks are serialized by VietnameseStaticFontSetup. Runtime
        // code must never mutate shared TMP_FontAsset instances or their atlases.
        if (staticVietnameseFont == null)
            staticVietnameseFont = Resources.Load<TMP_FontAsset>(StaticVietnameseFontResourcePath);
        return staticVietnameseFont != null ? staticVietnameseFont : TMP_Settings.defaultFontAsset;
    }

    private static Language ReadInitialLanguage()
    {
        int fallback = Application.systemLanguage == SystemLanguage.Vietnamese ? 1 : 0;
        return (Language)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, fallback), 0, 1);
    }

    private static Dictionary<string, string[]> CreateLiteralTable()
    {
        return new Dictionary<string, string[]>
        {
            { "new_game", new[] { "NEW GAME", "CHƠI MỚI" } },
            { "select_difficulty", new[] { "SELECT DIFFICULTY", "CHỌN ĐỘ KHÓ" } },
            { "tutorial", new[] { "TUTORIAL", "HƯỚNG DẪN" } },
            { "multiplayer", new[] { "MULTIPLAYER", "NHIỀU NGƯỜI CHƠI" } },
            { "options", new[] { "OPTIONS", "TÙY CHỌN" } },
            { "credits", new[] { "CREDITS", "GIỚI THIỆU" } },
            { "quit", new[] { "QUIT", "THOÁT" } },
            { "back", new[] { "BACK", "QUAY LẠI" } },
            { "save", new[] { "SAVE", "LƯU" } },
            { "display", new[] { "DISPLAY", "HIỂN THỊ" } },
            { "controls", new[] { "CONTROLS", "ĐIỀU KHIỂN" } },
            { "general", new[] { "GENERAL", "CHUNG" } },
            { "audio", new[] { "AUDIO", "ÂM THANH" } },
            { "resolution", new[] { "RESOLUTION:", "ĐỘ PHÂN GIẢI:" } },
            { "display_mode", new[] { "DISPLAY MODE:", "CHẾ ĐỘ MÀN HÌNH:" } },
            { "graphics", new[] { "GRAPHICS QUALITY:", "CHẤT LƯỢNG ĐỒ HỌA:" } },
            { "shadows", new[] { "SHADOW QUALITY:", "CHẤT LƯỢNG BÓNG:" } },
            { "brightness", new[] { "BRIGHTNESS:", "ĐỘ SÁNG:" } },
            { "fps_limit", new[] { "FPS LIMIT:", "GIỚI HẠN FPS:" } },
            { "show_fps", new[] { "SHOW FPS:", "HIỆN FPS:" } },
            { "fps_position", new[] { "FPS POSITION:", "VỊ TRÍ FPS:" } },
            { "aim_sensitivity", new[] { "AIM SENSITIVITY:", "ĐỘ NHẠY NGẮM:" } },
            { "zoom_sensitivity", new[] { "ZOOM SENSITIVITY:", "ĐỘ NHẠY ZOOM:" } },
            { "master_volume", new[] { "MASTER VOLUME:", "ÂM LƯỢNG TỔNG:" } },
            { "music_volume", new[] { "MUSIC VOLUME:", "ÂM LƯỢNG NHẠC:" } },
            { "sfx_volume", new[] { "SFX VOLUME:", "ÂM LƯỢNG HIỆU ỨNG:" } },
            { "inventory", new[] { "INVENTORY", "TÚI ĐỒ" } },
            { "loot", new[] { "LOOT CONTAINER", "VẬT PHẨM TRONG THÙNG" } },
            { "health", new[] { "HEALTH STATUS", "TÌNH TRẠNG SỨC KHỎE" } },
            { "pause", new[] { "PAUSED", "TẠM DỪNG" } },
            { "resume", new[] { "RESUME", "TIẾP TỤC" } },
            { "host", new[] { "HOST", "CHỦ PHÒNG" } },
            { "teammate", new[] { "TEAMMATE", "ĐỒNG ĐỘI" } },
            { "empty", new[] { "EMPTY SLOT", "Ô TRỐNG" } },
            { "start", new[] { "START", "BẮT ĐẦU" } },
            { "create_room", new[] { "CREATE ROOM", "TẠO PHÒNG" } },
            { "join_room", new[] { "JOIN ROOM", "VÀO PHÒNG" } },
            { "refresh", new[] { "REFRESH", "LÀM MỚI" } },
            { "yes", new[] { "[ YES ]", "[ CÓ ]" } },
            { "no", new[] { "[ NO ]", "[ KHÔNG ]" } },
            { "low", new[] { "LOW", "THẤP" } },
            { "medium", new[] { "MEDIUM", "TRUNG BÌNH" } },
            { "high", new[] { "HIGH", "CAO" } },
            { "disabled", new[] { "DISABLED", "TẮT" } },
            { "off", new[] { "OFF", "TẮT" } },
            { "on", new[] { "ON", "BẬT" } },
            { "fullscreen", new[] { "FULLSCREEN", "TOÀN MÀN HÌNH" } },
            { "borderless", new[] { "BORDERLESS", "KHÔNG VIỀN" } },
            { "windowed", new[] { "WINDOWED", "CỬA SỔ" } },
            { "unlimited", new[] { "UNLIMITED", "KHÔNG GIỚI HẠN" } },
            { "solo", new[] { "SOLO", "CHƠI ĐƠN" } },
            { "easy", new[] { "EASY", "DỄ" } },
            { "normal", new[] { "NORMAL", "THƯỜNG" } },
            { "hard", new[] { "HARD", "KHÓ" } },
            { "hardcore", new[] { "HARDCORE", "SINH TỒN" } },
            { "host_game", new[] { "HOST GAME", "TẠO PHÒNG" } },
            { "join_game", new[] { "JOIN GAME", "VÀO PHÒNG" } },
            { "host_settings", new[] { "HOST SETTINGS", "CÀI ĐẶT PHÒNG" } },
            { "room_name", new[] { "ROOM NAME:", "TÊN PHÒNG:" } },
            { "max_players", new[] { "MAX PLAYERS:", "SỐ NGƯỜI TỐI ĐA:" } },
            { "difficulty", new[] { "DIFFICULTY:", "ĐỘ KHÓ:" } },
            { "password", new[] { "PASSWORD:", "MẬT KHẨU:" } },
            { "select_survivor", new[] { "SELECT SURVIVOR", "CHỌN NHÂN VẬT" } },
            { "server_list", new[] { "SERVER LIST", "DANH SÁCH PHÒNG" } },
            { "enter_password", new[] { "ENTER PASSWORD", "NHẬP MẬT KHẨU" } },
            { "close", new[] { "CLOSE", "ĐÓNG" } },
            { "confirm", new[] { "CONFIRM", "XÁC NHẬN" } },
            { "refresh_list", new[] { "REFRESH LIST", "LÀM MỚI DANH SÁCH" } },
            { "survivor_identity", new[] { "SURVIVOR IDENTITY", "HỒ SƠ NHÂN VẬT" } },
            { "enter_dead_zone", new[] { "ENTER THE DEAD ZONE", "TIẾN VÀO VÙNG CHẾT" } },
            { "start_campaign", new[] { "START CAMPAIGN", "BẮT ĐẦU CHIẾN DỊCH" } },
            { "waiting_room", new[] { "WAITING ROOM", "PHÒNG CHỜ" } },
            { "room", new[] { "ROOM:", "PHÒNG:" } },
            { "players", new[] { "PLAYERS", "NGƯỜI CHƠI" } },
            { "ready", new[] { "READY", "SẴN SÀNG" } },
            { "not_ready", new[] { "NOT READY", "CHƯA SẴN SÀNG" } },
            { "connecting", new[] { "CONNECTING...", "ĐANG KẾT NỐI..." } },
            { "entering", new[] { "ENTERING THE DEAD ZONE...", "ĐANG TIẾN VÀO VÙNG CHẾT..." } },
            { "room_placeholder", new[] { "E.g. Refugee Camp...", "VD: Trại tị nạn..." } },
            { "password_placeholder", new[] { "Enter password...", "Nhập mật khẩu..." } },
            { "hard_only", new[] { "HARD ONLY", "CHỈ BÓNG CỨNG" } },
            { "all_shadows", new[] { "ALL SHADOWS", "TẤT CẢ BÓNG" } },
            { "top_right", new[] { "TOP RIGHT", "TRÊN PHẢI" } },
            { "top_left", new[] { "TOP LEFT", "TRÊN TRÁI" } },
            { "bottom_right", new[] { "BOTTOM RIGHT", "DƯỚI PHẢI" } },
            { "bottom_left", new[] { "BOTTOM LEFT", "DƯỚI TRÁI" } },
            { "top_center", new[] { "TOP CENTER", "TRÊN GIỮA" } },
            { "bottom_center", new[] { "BOTTOM CENTER", "DƯỚI GIỮA" } },
            { "anti_aliasing", new[] { "ANTI-ALIASING:", "KHỬ RĂNG CƯA:" } },
            { "credits_team", new[] { "SURVIVAL TEAM", "ĐỘI NGŨ PHÁT TRIỂN" } },
            { "english_option", new[] { "ENGLISH", "TIẾNG ANH" } },
            { "vietnamese_option", new[] { "VIETNAMESE", "TIẾNG VIỆT" } },
            { "drop", new[] { "Drop", "Vứt" } },
            { "use", new[] { "Use", "Dùng" } },
            { "bandage", new[] { "Bandage", "Băng gạc" } },
            { "painkiller", new[] { "PainKiller", "Thuốc giảm đau" } },
            { "water", new[] { "Water", "Nước" } },
            { "energy_water", new[] { "EnergyWater", "Nước tăng lực" } },
            { "meat", new[] { "Meat", "Thịt" } },
            { "ammo_762", new[] { "7.62mm Ammo", "Đạn 7.62mm" } },
            { "ammo_12g", new[] { "12 Gauge Ammo", "Đạn 12 Gauge" } },
            { "ammunition", new[] { "Ammunition", "Đạn dược" } },
            { "medical", new[] { "Medical", "Y tế" } },
            { "consumable", new[] { "Consumable", "Nhu yếu phẩm" } },
            { "weapon", new[] { "Weapon", "Vũ khí" } },
            { "backpack", new[] { "Backpack", "Ba lô" } },
            { "moderate_exertion", new[] { "Moderate Exertion", "Hơi mệt" } },
            { "high_exertion", new[] { "High Exertion", "Khá mệt" } },
            { "excessive_exertion", new[] { "Excessive Exertion", "Rất mệt" } },
            { "exhausted", new[] { "Exhausted", "Kiệt sức" } },
            { "leave_bed", new[] { "LEAVE BED", "RỜI KHỎI GIƯỜNG" } },
            { "sleeping", new[] { "SLEEPING...", "ĐANG NGỦ..." } },
            { "sleep_prompt", new[] { "PRESS [E] TO SLEEP", "NHẤN [E] ĐỂ NGỦ" } },
            { "sleep_hours", new[] { "YOU CAN ONLY SLEEP FROM 20:00 TO 03:00", "CHỈ CÓ THỂ NGỦ TỪ 20:00 ĐẾN 03:00" } },
            { "quest_search", new[] { "PRESS [E] TO SEARCH CABINET", "NHẤN [E] ĐỂ KIỂM TRA TỦ" } },
            { "quest_search_area", new[] { "PRESS [E] TO SEARCH AREA", "NHẤN [E] ĐỂ KIỂM TRA KHU VỰC" } },
            { "survivor_identity_label", new[] { "SURVIVOR IDENTITY:", "ĐỊNH DANH KẺ SỐNG SÓT:" } },
            { "accept", new[] { "ACCEPT", "ĐỒNG Ý" } },
            { "decline", new[] { "DECLINE", "TỪ CHỐI" } },
            { "item", new[] { "Item", "Vật phẩm" } },
            { "campaign_lobby", new[] { "CAMPAIGN LOBBY", "SẢNH CHIẾN DỊCH" } },
            { "you", new[] { "YOU", "BẠN" } },
            { "connected", new[] { "CONNECTED", "ĐÃ KẾT NỐI" } },
            { "dont_save", new[] { "DON'T SAVE", "KHÔNG LƯU" } },
            { "cancel", new[] { "CANCEL", "HỦY" } },
            { "scan_radio", new[] { "SCANNING RADIO FREQUENCIES...", "ĐANG QUÉT TẦN SỐ RADIO..." } },
            { "static_noise", new[] { "ONLY STATIC NOISE REMAINS...", "CHỈ CÒN LẠI TIẾNG NHIỄU..." } },
            { "base_full", new[] { "BASE IS FULL! CANNOT JOIN.", "PHÒNG ĐÃ ĐẦY! KHÔNG THỂ THAM GIA." } },
            { "wrong_password", new[] { "WRONG PASSWORD!", "SAI MẬT KHẨU!" } },
            { "enter_name", new[] { "Enter name...", "Nhập tên..." } },
            { "health_help", new[] { "Right click on injuries: Apply/Remove Bandage | Scroll: View details", "Nhấp chuột phải vào vết thương: Băng/Tháo băng | Cuộn: Xem chi tiết" } },
            { "overall_body_status", new[] { "Overall Body Status", "Tình trạng cơ thể" } },
            { "minor_pain", new[] { "Minor Pain", "Đau nhẹ" } },
            { "slight_damage", new[] { "Slight Damage", "Tổn thương rất nhẹ" } },
            { "minor_damage", new[] { "Minor Damage", "Tổn thương nhẹ" } },
            { "moderate_damage", new[] { "Moderate Damage", "Tổn thương vừa" } },
            { "severe_damage", new[] { "Severe Damage", "Tổn thương nặng" } },
            { "very_severe_damage", new[] { "Very Severe Damage", "Tổn thương rất nặng" } },
            { "critical_damage", new[] { "Critical Damage", "Tổn thương nguy kịch" } },
            { "highly_critical_damage", new[] { "Highly Critical Damage", "Tổn thương cực kỳ nguy kịch" } },
            { "terminal_damage", new[] { "Terminal Damage", "Tổn thương chí mạng" } },
            { "deceased", new[] { "Deceased", "Đã tử vong" } },
            { "bandaged", new[] { "Bandaged", "Đã băng bó" } },
            { "bitten", new[] { "Bitten", "Bị cắn" } },
            { "bleeding", new[] { "Bleeding", "Đang chảy máu" } },
            { "scratched", new[] { "Scratched", "Bị trầy xước" } },
            { "laceration", new[] { "Laceration", "Vết rách" } },
            { "remove_bandage", new[] { "Remove Bandage", "Tháo băng" } },
            { "apply_bandage", new[] { "Apply Bandage", "Băng bó" } },
            { "no_bandages", new[] { "No Bandages", "Không có băng gạc" } },
            { "applying_bandage", new[] { "Applying Bandage...", "Đang băng bó..." } },
            { "removing_bandage", new[] { "Removing Bandage...", "Đang tháo băng..." } },
            { "head", new[] { "Head", "Đầu" } },
            { "neck", new[] { "Neck", "Cổ" } },
            { "upper_torso", new[] { "Upper Torso", "Thân trên" } },
            { "lower_torso", new[] { "Lower Torso", "Thân dưới" } },
            { "left_thigh", new[] { "Left Thigh", "Đùi trái" } },
            { "left_calf", new[] { "Left Calf", "Bắp chân trái" } },
            { "left_foot", new[] { "Left Foot", "Bàn chân trái" } },
            { "right_thigh", new[] { "Right Thigh", "Đùi phải" } },
            { "right_calf", new[] { "Right Calf", "Bắp chân phải" } },
            { "right_foot", new[] { "Right Foot", "Bàn chân phải" } },
            { "left_upper_arm", new[] { "Left Upper Arm", "Bắp tay trái" } },
            { "left_forearm", new[] { "Left Forearm", "Cẳng tay trái" } },
            { "left_hand", new[] { "Left Hand", "Bàn tay trái" } },
            { "right_upper_arm", new[] { "Right Upper Arm", "Bắp tay phải" } },
            { "right_forearm", new[] { "Right Forearm", "Cẳng tay phải" } },
            { "right_hand", new[] { "Right Hand", "Bàn tay phải" } },
            { "bash", new[] { "Bash", "Đập" } },
            { "game_over", new[] { "GAME OVER", "TRÒ CHƠI KẾT THÚC" } },
            { "kill_count", new[] { "KILL COUNT:", "SỐ KẺ ĐỊCH ĐÃ HẠ:" } },
            { "prison", new[] { "Prison", "Nhà tù" } },
            { "survival_fragments", new[] { "FRAGMENTS OF SURVIVAL", "NHỮNG MẢNH VỠ SINH TỒN" } },
            { "your_offer", new[] { "YOUR OFFER", "ĐỒ CỦA BẠN" } },
            { "pick_item", new[] { "PICK ITEM", "CHỌN ĐỒ" } },
            { "partner_offer", new[] { "PARTNER'S OFFER", "ĐỒ ĐỐI TÁC" } },
            { "confirm_trade", new[] { "CONFIRM TRADE", "XÁC NHẬN GIAO DỊCH" } },
            { "cancel_trade", new[] { "CANCEL", "HỦY BỎ" } },
            { "customize_survivor", new[] { "CUSTOMIZE SURVIVOR", "CHỌN NHÂN VẬT" } },
            { "language_label", new[] { "LANGUAGE:", "NGÔN NGỮ:" } },
            { "victory_return", new[] { "RETURN TO MAIN MENU", "QUAY VỀ MENU CHÍNH" } },
            { "victory_mission_complete", new[] { "MISSION COMPLETE", "NHIỆM VỤ HOÀN THÀNH" } },
            { "victory_escape_successful", new[] { "ESCAPE SUCCESSFUL", "THOÁT HIỂM THÀNH CÔNG" } },
            { "difficulty_hardcore", new[] { "Hardcore", "Khắc nghiệt" } },
            { "police_car", new[] { "POLICE CAR", "XE CẢNH SÁT" } },
            { "inspect_vehicle", new[] { "INSPECT VEHICLE", "HÃY KIỂM TRA" } },
            { "military_base_gate", new[] { "MILITARY BASE GATE", "CỔNG KHU QUÂN SỰ" } },
            { "police_toolbox", new[] { "Patrol Car Tool Kit", "Bộ dụng cụ xe tuần tra" } },
            { "police_hammer", new[] { "Police Rescue Hammer", "Búa cứu hộ cảnh sát" } },
            { "police_fuel", new[] { "Police Spare Fuel Can", "Can nhiên liệu dự phòng cảnh sát" } },
            { "police_battery", new[] { "Patrol Car Battery", "Ắc quy xe tuần tra" } },
            { "police_tire", new[] { "Patrol Car Tire", "Lốp xe tuần tra" } },
            { "arrival_toolbox", new[] { "Car Repair Tool Kit", "Bộ dụng cụ sửa xe" } },
            { "arrival_hammer", new[] { "Rescue Hammer", "Búa cứu hộ" } },
            { "arrival_fuel", new[] { "Fuel Can", "Can nhiên liệu" } },
            { "arrival_battery", new[] { "Car Battery", "Ắc quy ô tô" } },
            { "arrival_tire", new[] { "Car Tire", "Lốp ô tô" } },
            { "route_clue_invoice", new[] { "Supply Transfer Invoice", "Phiếu điều chuyển vật tư" } },
            { "route_clue_diagram", new[] { "Evacuation Route Change Notice", "Thông báo đổi tuyến sơ tán" } },
            { "route_clue_note", new[] { "Duty Officer's Note", "Ghi chú của nhân viên trực" } },
        };
    }
}

public sealed class RuntimeLocalizationDriver : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RuntimeLocalizationDriver>() != null) return;
        GameObject go = new GameObject("--- RUNTIME LOCALIZATION ---");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeLocalizationDriver>();
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += RefreshAll;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshAll;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Awake() => RefreshAll();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshAll();

    private void RefreshAll()
    {
        foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.text = GameLocalization.TranslateLiteral(label.text);
        }

        foreach (UnityEngine.UI.Text label in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.text = GameLocalization.TranslateLiteral(label.text);
        }
    }
}
