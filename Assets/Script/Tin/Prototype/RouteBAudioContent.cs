using System.Collections.Generic;

public enum RouteBAudioCueId
{
    OpeningEmergencyBroadcast,
    PlayerRouteReaction,
    FirstSupplyDocument,
    SecondEvacuationDocument,
    ThirdCoordinationDocument,
    OfficeLocated,
    DispatchDeskLog,
    OfficeRadioRecording,
    MilitaryRouteRevealed,
    MilitaryBaseApproach,
    AlarmPointOfNoReturn,
    SiegeStarted,
    GeneratorOnline,
    EscapeVehicleReady,
    MilitaryEvacuationComplete
}

/// <summary>
/// Recording-ready source copy for every Route B story voice cue. Runtime code may
/// load an optional clip from Resources; subtitles remain the canonical fallback.
/// </summary>
public readonly struct RouteBAudioCue
{
    public RouteBAudioCue(RouteBAudioCueId id, string speaker, string title, string vietnamese,
        string english, string audioResourcePath, float fallbackDuration, bool radioTransmission)
    {
        Id = id;
        Speaker = speaker;
        Title = title;
        Vietnamese = vietnamese;
        English = english;
        AudioResourcePath = audioResourcePath;
        FallbackDuration = fallbackDuration;
        IsRadioTransmission = radioTransmission;
    }

    public RouteBAudioCueId Id { get; }
    public string Speaker { get; }
    public string Title { get; }
    public string Vietnamese { get; }
    public string English { get; }
    public string AudioResourcePath { get; }
    public float FallbackDuration { get; }
    public bool IsRadioTransmission { get; }
}

public static class RouteBAudioContent
{
    private const string Root = "Story/RouteB/";

    private static readonly RouteBAudioCue[] Cues =
    {
        Cue(RouteBAudioCueId.OpeningEmergencyBroadcast, "PHÁT THANH KHẨN CẤP", "TÍN HIỆU KHẨN CẤP",
            "Đây là thông báo khẩn cấp. Tuyến sơ tán dân sự phía bắc đã bị phong tỏa. Người sống sót hãy tìm hồ sơ các chuyến tiếp tế tại khu dân cư phía đông. Văn phòng Điều phối vẫn lưu bản đồ dẫn tới điểm tập kết quân sự.",
            "Emergency broadcast. The northern civilian evacuation route is blocked. Survivors should search the eastern neighborhood for supply records. The Coordination Office still holds a map to the military rally point.",
            "01_OpeningEmergencyBroadcast", 8.5f, true),
        Cue(RouteBAudioCueId.PlayerRouteReaction, "NGƯỜI SỐNG SÓT", "HAI HƯỚNG THOÁT",
            "Xe này vẫn sửa được. Nhưng tín hiệu vừa rồi có thể dẫn tới một con đường an toàn hơn. Mình có thể chuẩn bị cả hai hướng trước khi quyết định.",
            "The car can still be repaired. But that signal may lead to a safer route. I can prepare both options before deciding.",
            "02_PlayerRouteReaction", 5.5f, false),
        Cue(RouteBAudioCueId.FirstSupplyDocument, "NGƯỜI SỐNG SÓT", "HỒ SƠ TIẾP TẾ",
            "Phiếu điều chuyển xác nhận nhiên liệu và dụng cụ từng được đưa về Văn phòng Điều phối. Đây là dấu vết đầu tiên của tuyến quân sự.",
            "The transfer invoice confirms fuel and tools were sent to the Coordination Office. This is the first trace of the military route.",
            "03_FirstSupplyDocument", 5.5f, false),
        Cue(RouteBAudioCueId.SecondEvacuationDocument, "NGƯỜI SỐNG SÓT", "LỊCH TRÌNH SƠ TÁN",
            "Lịch xe buýt đã bị hủy, nhưng mã tuyến vẫn trùng với hồ sơ tiếp tế. Tất cả đều dẫn về cùng một văn phòng.",
            "The bus schedule was cancelled, but its route code matches the supply records. Everything leads to the same office.",
            "04_SecondEvacuationDocument", 5.2f, false),
        Cue(RouteBAudioCueId.ThirdCoordinationDocument, "NGƯỜI SỐNG SÓT", "ĐỊA CHỈ ĐIỀU PHỐI",
            "Ghi chú này có địa chỉ cổng tím và tần số liên lạc dự phòng. Đủ ba tài liệu rồi; mình có thể xác định chính xác Văn phòng Điều phối.",
            "This note contains the purple-gate address and a backup frequency. With all three records, I can pinpoint the Coordination Office.",
            "05_ThirdCoordinationDocument", 5.8f, false),
        Cue(RouteBAudioCueId.OfficeLocated, "NGƯỜI SỐNG SÓT", "VĂN PHÒNG ĐIỀU PHỐI",
            "Đúng địa chỉ rồi. Trước khi lục tủ hồ sơ, mình nên kiểm tra bàn trực và tìm xem ai đã rời khỏi đây cuối cùng.",
            "This is the address. Before searching the cabinets, I should inspect the dispatch desk and find out who left last.",
            "06_OfficeLocated", 5.2f, false),
        Cue(RouteBAudioCueId.DispatchDeskLog, "NHẬT KÝ ĐIỀU PHỐI", "BẢN GHI BÀN TRỰC",
            "Ca trực cuối đã khóa bản đồ trong tủ hồ sơ. Chìa khóa được để cạnh máy vô tuyến; bản liên lạc cuối cùng chưa được phát hết.",
            "The final shift locked the map in the records cabinet. The key was left beside the radio; the last transmission was never fully played.",
            "07_DispatchDeskLog", 6.2f, true),
        Cue(RouteBAudioCueId.OfficeRadioRecording, "BẢN GHI VÔ TUYẾN", "LIÊN LẠC CUỐI CÙNG",
            "Điểm tập kết quân sự vẫn hoạt động. Cổng ngoài sẽ không mở nếu chưa kích hoạt báo động sơ tán. Khi còi vang lên, mọi tiếng động trong khu vực sẽ bị thu hút về căn cứ.",
            "The military rally point is still operational. The outer gate will not open until the evacuation alarm is activated. Once the siren sounds, every threat nearby will be drawn to the base.",
            "08_OfficeRadioRecording", 7.2f, true),
        Cue(RouteBAudioCueId.MilitaryRouteRevealed, "NGƯỜI SỐNG SÓT", "ĐÃ XÁC ĐỊNH TUYẾN QUÂN SỰ",
            "Bản đồ chỉ rõ đường tới căn cứ. Báo động sẽ là điểm không thể quay lại; trước lúc đó mình vẫn có thể chọn chiếc xe dân sự.",
            "The map shows the road to the base. Activating the alarm will be the point of no return; until then, the civilian car remains an option.",
            "09_MilitaryRouteRevealed", 5.8f, false),
        Cue(RouteBAudioCueId.MilitaryBaseApproach, "HỆ THỐNG CĂN CỨ", "KHU QUÂN SỰ",
            "Nguồn điện dự phòng đang ngoại tuyến. Xe sơ tán thiếu ắc quy, nhiên liệu và bộ sửa chữa. Kích hoạt báo động để bắt đầu quy trình di tản.",
            "Backup power is offline. The evacuation vehicle requires a battery, fuel and a repair kit. Activate the alarm to begin evacuation protocol.",
            "10_MilitaryBaseApproach", 6.5f, true),
        Cue(RouteBAudioCueId.AlarmPointOfNoReturn, "HỆ THỐNG CẢNH BÁO", "ĐIỂM KHÔNG THỂ QUAY LẠI",
            "Xác nhận kích hoạt báo động sẽ khóa tuyến thoát bằng chiếc xe dân sự cho toàn đội. Cuộc phòng thủ bắt đầu ngay khi còi báo động vang lên.",
            "Confirming the alarm will lock the civilian-car escape route for the entire team. The defense begins as soon as the siren sounds.",
            "11_AlarmPointOfNoReturn", 6.0f, true),
        Cue(RouteBAudioCueId.SiegeStarted, "HỆ THỐNG CĂN CỨ", "BÁO ĐỘNG SƠ TÁN",
            "Báo động đã kích hoạt. Bảo vệ cổng, khôi phục máy phát và chuẩn bị xe sơ tán. Không còn đường quay lại.",
            "Evacuation alarm activated. Defend the gate, restore the generator and prepare the evacuation vehicle. There is no turning back.",
            "12_SiegeStarted", 5.8f, true),
        Cue(RouteBAudioCueId.GeneratorOnline, "HỆ THỐNG CĂN CỨ", "NGUỒN ĐIỆN ĐÃ KHÔI PHỤC",
            "Máy phát đã hoạt động. Cổng điện được gia cố và kho vật tư đã mở. Hãy hoàn tất việc lắp linh kiện cho xe sơ tán.",
            "Generator online. The powered gate is reinforced and the supply storage is open. Finish installing the evacuation vehicle parts.",
            "13_GeneratorOnline", 5.8f, true),
        Cue(RouteBAudioCueId.EscapeVehicleReady, "HỆ THỐNG CĂN CỨ", "XE SƠ TÁN ĐÃ SẴN SÀNG",
            "Phương tiện đã hoạt động. Tập hợp những người còn sống tại xe và rời căn cứ trước khi cổng phòng thủ sụp đổ.",
            "Vehicle operational. Gather the remaining survivors at the vehicle and leave the base before the defensive gate collapses.",
            "14_EscapeVehicleReady", 5.5f, true),
        Cue(RouteBAudioCueId.MilitaryEvacuationComplete, "BẢN GHI HỆ THỐNG", "SƠ TÁN HOÀN TẤT",
            "Tín hiệu của đội sống sót đã rời khỏi vùng phong tỏa. Tuyến quân sự đóng lại sau lần sơ tán cuối cùng.",
            "The survivors' signal has left the quarantine zone. The military route closed after the final evacuation.",
            "15_MilitaryEvacuationComplete", 5.5f, true)
    };

    private static readonly RouteBAudioCue[] Opening = { Cues[0], Cues[1] };

    public static IReadOnlyList<RouteBAudioCue> All => Cues;
    public static IReadOnlyList<RouteBAudioCue> OpeningSequence => Opening;

    public static RouteBAudioCue Get(RouteBAudioCueId id)
    {
        for (int i = 0; i < Cues.Length; i++)
            if (Cues[i].Id == id) return Cues[i];
        return Cues[0];
    }

    private static RouteBAudioCue Cue(RouteBAudioCueId id, string speaker, string title,
        string vietnamese, string english, string resourceName, float duration, bool radio) =>
        new RouteBAudioCue(id, speaker, title, vietnamese, english, Root + resourceName, duration, radio);
}
