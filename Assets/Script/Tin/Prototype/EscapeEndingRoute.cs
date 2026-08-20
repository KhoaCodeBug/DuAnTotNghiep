public enum EscapeEndingRoute
{
    None = 0,
    CivilianCar = 1,
    MilitaryEvacuation = 2
}

public static class EscapeEndingRules
{
    public static bool IsValidPlayableRoute(EscapeEndingRoute route) =>
        route == EscapeEndingRoute.CivilianCar || route == EscapeEndingRoute.MilitaryEvacuation;

    public static bool CanLock(EscapeEndingRoute current, EscapeEndingRoute requested) =>
        IsValidPlayableRoute(requested) && (current == EscapeEndingRoute.None || current == requested);

    public static string GetDisplayName(EscapeEndingRoute route) => route switch
    {
        EscapeEndingRoute.CivilianCar => "TUYẾN A — CHIẾC XE DÂN SỰ",
        EscapeEndingRoute.MilitaryEvacuation => "TUYẾN B — SƠ TÁN QUÂN SỰ",
        _ => "CHƯA CHỌN TUYẾN THOÁT"
    };
}
