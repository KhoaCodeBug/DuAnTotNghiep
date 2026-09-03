using System.Collections.Generic;

public enum SharedQuestPresentationEventId
{
    LocalizedMessage = 1,
    ArrivalCarInspected = 2,
    RouteClueFound = 3,
    AllRouteCluesFound = 4,
    OfficeSearchStarted = 5,
    OfficeInvestigationProgress = 6,
    HospitalRadioRecording = 7
}

/// <summary>
/// Client-session receipt state. It deliberately lives outside player/avatar
/// state so replacing an avatar cannot replay a shared presentation.
/// </summary>
public sealed class SharedQuestPresentationReceiptLedger
{
    private readonly Dictionary<SharedQuestPresentationEventId, int> highestRevisionByEvent =
        new Dictionary<SharedQuestPresentationEventId, int>();

    public bool TryAccept(SharedQuestPresentationEventId eventId, int revision, bool wasRecipientAtTrigger)
    {
        if (!wasRecipientAtTrigger || revision <= 0) return false;
        if (highestRevisionByEvent.TryGetValue(eventId, out int highestRevision) && revision <= highestRevision)
            return false;

        highestRevisionByEvent[eventId] = revision;
        return true;
    }

    public void ResetForNewSession()
    {
        highestRevisionByEvent.Clear();
    }
}
