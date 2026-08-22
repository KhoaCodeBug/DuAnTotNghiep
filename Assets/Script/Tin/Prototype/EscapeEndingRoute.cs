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

    public static string GetDisplayName(EscapeEndingRoute route) => GetDisplayName(route, true);

    public static string GetDisplayName(EscapeEndingRoute route, bool vietnamese)
    {
        return route switch
        {
            EscapeEndingRoute.CivilianCar => vietnamese ? "TUYẾN A — CHIẾC XE DÂN SỰ" : "ROUTE A — CIVILIAN CAR",
            EscapeEndingRoute.MilitaryEvacuation => vietnamese ? "TUYẾN B — SƠ TÁN QUÂN SỰ" : "ROUTE B — MILITARY EVACUATION",
            _ => vietnamese ? "CHƯA CHỌN TUYẾN THOÁT" : "NO ESCAPE ROUTE SELECTED"
        };
    }
}
