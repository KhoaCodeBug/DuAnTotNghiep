using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Client-local language service. Network messages should carry keys/data, not translated text.</summary>
public static class GameLocalization
{
    public enum Language { English = 0, Vietnamese = 1 }

    private const string PreferenceKey = "GameLanguage";
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
        { "loot.title", new[] { "LOOT CONTAINER", "VẬT PHẨM TRONG THÙNG" } },
        { "trade.title", new[] { "TRADE", "BÀN GIAO DỊCH" } },
        { "trade.choosing", new[] { "Choosing...", "Đang chọn..." } },
        { "trade.lock", new[] { "LOCK", "KHÓA LẠI" } },
        { "trade.unlock", new[] { "UNLOCK", "MỞ KHÓA" } },
        { "chat.placeholder", new[] { "Press Enter to chat...", "Nhấn Enter để chat..." } },
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
        { "quest.route_a_ready", new[] { "ROUTE A — REQUIREMENTS MET: Return to the vehicle condition panel and press START CAR.", "TUYẾN A — ĐÃ ĐỦ ĐIỀU KIỆN: Quay lại bảng tình trạng xe và bấm KHỞI ĐỘNG XE." } },
        { "quest.route_a_started", new[] { "ROUTE A — CAR STARTED: The city map and civilian road exit are now available.", "TUYẾN A — XE ĐÃ KHỞI ĐỘNG: Bản đồ thành phố và lối thoát dân sự đã được mở." } },
        { "quest.route_a_regroup", new[] { "ROUTE A — REGROUP: Bring every living survivor into or close to the repaired car.", "TUYẾN A — TẬP KẾT: Đưa mọi người còn sống lên xe hoặc tập trung sát chiếc xe đã sửa." } },
        { "quest.route_a_wait_team", new[] { "Cannot depart yet: a living teammate is still outside the regroup radius.", "Chưa thể xuất phát: vẫn còn đồng đội sống sót ở ngoài phạm vi tập kết." } },
        { "quest.ending_a_locked", new[] { "ENDING LOCKED: The team chose to break through the quarantine in the civilian car.", "ENDING ĐÃ KHÓA: Toàn đội chọn vượt vòng phong tỏa bằng chiếc xe dân sự." } },
        { "quest.office_no_points", new[] { "Cannot begin: this area has no mission search points.", "Chưa thể bắt đầu: khu vực chưa có điểm kiểm tra nhiệm vụ." } },
        { "quest.office_missing_points", new[] { "Cannot begin: the office is missing story investigation points.", "Chưa thể bắt đầu: văn phòng thiếu điểm điều tra cốt truyện." } },
        { "quest.office_new_objective", new[] { "NEW OBJECTIVE: Inspect the dispatch desk for the records-cabinet key.", "MỤC TIÊU MỚI: Kiểm tra bàn điều phối để tìm chìa khóa tủ hồ sơ." } },
        { "quest.route_b_go_military", new[] { "ROUTE B — NEW OBJECTIVE: Follow the discovered road to the military base.", "TUYẾN B — MỤC TIÊU MỚI: Đi đến khu quân sự theo tuyến đường vừa tìm thấy." } },
        { "quest.vehicle_sender", new[] { "VEHICLE", "CHIẾC XE" } },
        { "quest.vehicle_signal", new[] { "The car can still be repaired. The emergency frequency has just picked up a new signal.", "Xe vẫn có thể sửa. Tần số khẩn cấp vừa bắt được một tín hiệu mới." } },
        { "quest.outside_search_title", new[] { "OUTSIDE SEARCH AREA", "NGOÀI VÙNG TÌM KIẾM" } },
        { "quest.outside_office_title", new[] { "OUTSIDE AREA OF INTEREST", "NGOÀI VÙNG NGHI VẤN" } },
        { "quest.outside_search_body", new[] { "Follow the marker back to the objective  •  Map [M].", "Đi theo marker để quay lại mục tiêu  •  Bản đồ [M]." } },
        { "quest.office_area_title", new[] { "HOSPITAL COORDINATION SECTION", "KHU ĐIỀU PHỐI TRONG BỆNH VIỆN" } },
        { "quest.office_area_body", new[] { "HOSPITAL COORDINATION SECTION  •  Start by finding the key at the dispatch desk.", "KHU ĐIỀU PHỐI TRONG BỆNH VIỆN  •  Trước tiên hãy tìm chìa khóa tại bàn điều phối." } },
        { "quest.investigation_sender", new[] { "INVESTIGATION", "ĐIỀU TRA" } },
        { "quest.office_step0_title", new[] { "KEY FOUND", "ĐÃ TÌM THẤY CHÌA KHÓA" } },
        { "quest.office_step0_body", new[] { "The shift log points to the final radio transmission. REWARD: dispatch evidence recovered.", "Sổ trực chỉ tới bản liên lạc cuối trong radio. PHẦN THƯỞNG: đã thu hồi chứng cứ điều phối." } },
        { "quest.office_step1_title", new[] { "RADIO RECORDING RESTORED", "ĐÃ KHÔI PHỤC BẢN GHI RADIO" } },
        { "quest.office_step1_body", new[] { "The transmission says the military route diagram is locked in the records cabinet. REWARD: cabinet location identified.", "Bản ghi cho biết sơ đồ tuyến quân sự nằm trong tủ hồ sơ. PHẦN THƯỞNG: đã xác định vị trí tủ." } },
        { "quest.office_step2_title", new[] { "FINAL ROUTE RECORD FOUND", "ĐÃ TÌM THẤY HỒ SƠ TUYẾN CUỐI" } },
        { "quest.office_step2_body", new[] { "The map confirms the military-base road. REWARD: military route map and base waypoint unlocked.", "Bản đồ xác nhận đường tới căn cứ. PHẦN THƯỞNG: bản đồ tuyến quân sự và waypoint căn cứ đã mở." } },
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
        { "quest.military_debug_parts", new[] { "ROUTE B TEST: All three evacuation-vehicle parts are installed without using loot containers.", "TEST TUYẾN B: Đã mô phỏng lắp đủ ba linh kiện xe sơ tán mà không dùng loot container." } },
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
        { "difficulty.easy.title", new[] { "★ EASY MODE ★", "★ CHẾ ĐỘ DỄ ★" } },
        { "difficulty.easy.stats", new[] {
            "<color=#99FF99>ZOMBIE DENSITY:</color> Low (-50% Spawn Rate)\n<color=#99FF99>RESOURCES:</color> Abundant (Loot rate 150%)\n<color=#99FF99>DAMAGE TAKEN:</color> Reduced (-30% Damage)\n<color=#99FF99>STARTING GEAR:</color> Pistol + Ammo & Canned Food\n<color=#99FF99>SURVIVAL RATE:</color> Very High (90%)",
            "<color=#99FF99>MẬT ĐỘ ZOMBIE:</color> Thấp (-50% tần suất xuất hiện)\n<color=#99FF99>TÀI NGUYÊN:</color> Dồi dào (tỉ lệ loot 150%)\n<color=#99FF99>SÁT THƯƠNG NHẬN:</color> Giảm (-30% sát thương)\n<color=#99FF99>TRANG BỊ ĐẦU:</color> Súng lục + đạn và đồ hộp\n<color=#99FF99>TỈ LỆ SINH TỒN:</color> Rất cao (90%)" } },
        { "difficulty.easy.desc", new[] {
            "<b>OVERVIEW:</b>\nZombie spawn count is reduced. Ideal for exploring, gathering resources, and learning basic survival mechanics without heavy pressure.",
            "<b>TỔNG QUAN:</b>\nSố zombie xuất hiện được giảm. Phù hợp để khám phá, thu thập tài nguyên và làm quen cơ chế sinh tồn mà không chịu quá nhiều áp lực." } },
        { "difficulty.normal.title", new[] { "✦ SURVIVAL MODE ✦", "✦ CHẾ ĐỘ SINH TỒN ✦" } },
        { "difficulty.normal.stats", new[] {
            "<color=#FFFF99>ZOMBIE DENSITY:</color> Standard (100% Spawn Rate)\n<color=#FFFF99>RESOURCES:</color> Balanced distribution\n<color=#FFFF99>DAMAGE TAKEN:</color> Normal (100% Damage)\n<color=#FFFF99>STARTING GEAR:</color> Flashlight & Bandage\n<color=#FFFF99>SURVIVAL RATE:</color> Balanced (50%)",
            "<color=#FFFF99>MẬT ĐỘ ZOMBIE:</color> Tiêu chuẩn (100% tần suất)\n<color=#FFFF99>TÀI NGUYÊN:</color> Phân bố cân bằng\n<color=#FFFF99>SÁT THƯƠNG NHẬN:</color> Bình thường (100%)\n<color=#FFFF99>TRANG BỊ ĐẦU:</color> Đèn pin và băng gạc\n<color=#FFFF99>TỈ LỆ SINH TỒN:</color> Cân bằng (50%)" } },
        { "difficulty.normal.desc", new[] {
            "<b>OVERVIEW:</b>\nThe standard zombie survival experience. Spawn rates and cooldown values use their balanced defaults. Requires strategic thinking.",
            "<b>TỔNG QUAN:</b>\nTrải nghiệm sinh tồn zombie tiêu chuẩn. Tần suất xuất hiện và thời gian hồi dùng các giá trị cân bằng mặc định. Người chơi cần suy nghĩ chiến thuật." } },
        { "difficulty.hard.title", new[] { "☠ HARDCORE MODE ☠", "☠ CHẾ ĐỘ KHẮC NGHIỆT ☠" } },
        { "difficulty.hard.stats", new[] {
            "<color=#FF9999>ZOMBIE DENSITY:</color> Extreme (+150% Spawn Rate)\n<color=#FF9999>RESOURCES:</color> Scarce & Depleted (Loot rate 40%)\n<color=#FF9999>DAMAGE TAKEN:</color> Increased (+50% Damage)\n<color=#FF9999>STARTING GEAR:</color> None (Empty hands)\n<color=#FF9999>SURVIVAL RATE:</color> Near Zero (<10%)",
            "<color=#FF9999>MẬT ĐỘ ZOMBIE:</color> Cực cao (+150% tần suất)\n<color=#FF9999>TÀI NGUYÊN:</color> Khan hiếm (tỉ lệ loot 40%)\n<color=#FF9999>SÁT THƯƠNG NHẬN:</color> Tăng (+50% sát thương)\n<color=#FF9999>TRANG BỊ ĐẦU:</color> Không có (tay không)\n<color=#FF9999>TỈ LỆ SINH TỒN:</color> Gần bằng không (<10%)" } },
        { "difficulty.hard.desc", new[] {
            "<b>OVERVIEW:</b>\nA relentless nightmare. Zombies are extremely numerous and spawn very quickly. Demands maximum skill and tactical planning.",
            "<b>TỔNG QUAN:</b>\nMột cơn ác mộng không ngừng nghỉ. Zombie cực kỳ đông và xuất hiện rất nhanh. Đòi hỏi kỹ năng cao nhất cùng kế hoạch chiến thuật chặt chẽ." } },
    };

    private static readonly Dictionary<string, string[]> LiteralText = CreateLiteralTable();

    public static string Get(string key, string fallback = null)
    {
        if (Text.TryGetValue(key, out string[] values))
            return values[(int)Current];
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

    public static void SetLanguage(Language language, bool save = true)
    {
        if (Current == language)
        {
            QuestUILocalization.SetVietnamese(language == Language.Vietnamese);
            return;
        }
        Current = language;
        if (save)
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
        }
        QuestUILocalization.SetVietnamese(IsVietnamese);
        LanguageChanged?.Invoke();
    }

    public static TMP_FontAsset GetRuntimeFont(TMP_FontAsset preferred = null)
    {
        TMP_FontAsset vietnameseFallback = Resources.Load<TMP_FontAsset>("Fonts/VietnameseDynamic SDF");
        if (preferred == null) return vietnameseFallback;

        // Keep the project's visual font, but let the dynamic fallback supply
        // Vietnamese glyphs that are absent from the static VCR atlas.
        if (vietnameseFallback != null && !preferred.fallbackFontAssetTable.Contains(vietnameseFallback))
            preferred.fallbackFontAssetTable.Add(vietnameseFallback);
        return preferred;
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
            { "confirm_trade", new[] { "CONFIRM TRADE", "XÁC NHẬN TRADE" } },
            { "cancel_trade", new[] { "CANCEL", "HỦY BỎ" } },
        };
    }
}

public sealed class RuntimeLocalizationDriver : MonoBehaviour
{
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RuntimeLocalizationDriver>() != null) return;
        GameObject go = new GameObject("--- RUNTIME LOCALIZATION ---");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeLocalizationDriver>();
    }

    private void OnEnable() => GameLocalization.LanguageChanged += RefreshAll;
    private void OnDisable() => GameLocalization.LanguageChanged -= RefreshAll;

    private void Awake() => RefreshAll();

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.4f;
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.font = GameLocalization.GetRuntimeFont(label.font);
            label.text = GameLocalization.TranslateLiteral(label.text);
        }

        foreach (UnityEngine.UI.Text label in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.text = GameLocalization.TranslateLiteral(label.text);
        }
    }
}
