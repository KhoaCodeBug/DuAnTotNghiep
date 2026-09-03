using NUnit.Framework;

public class SharedQuestPresentationRulesTests
{
    [Test]
    public void Recipient_AcceptsEachEventRevisionOnlyOnce()
    {
        var ledger = new SharedQuestPresentationReceiptLedger();
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.ArrivalCarInspected, 1, true));
        Assert.IsFalse(ledger.TryAccept(SharedQuestPresentationEventId.ArrivalCarInspected, 1, true));
    }

    [Test]
    public void OutOfOrder_OlderRevisionCannotReplaySameEvent()
    {
        var ledger = new SharedQuestPresentationReceiptLedger();
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.RouteClueFound, 7, true));
        Assert.IsFalse(ledger.TryAccept(SharedQuestPresentationEventId.RouteClueFound, 6, true));
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.OfficeSearchStarted, 5, true),
            "A different event remains deliverable even when global RPC order differs.");
    }

    [Test]
    public void LateJoiner_IsRejectedWhenAbsentFromTriggerSnapshot()
    {
        var ledger = new SharedQuestPresentationReceiptLedger();
        Assert.IsFalse(ledger.TryAccept(SharedQuestPresentationEventId.HospitalRadioRecording, 3, false));
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.HospitalRadioRecording, 4, true));
    }

    [Test]
    public void AvatarReplacement_DoesNotResetSessionReceipt()
    {
        var ledger = new SharedQuestPresentationReceiptLedger();
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.AllRouteCluesFound, 9, true));

        // The same ledger is owned by MainQuestManager, not either avatar.
        Assert.IsFalse(ledger.TryAccept(SharedQuestPresentationEventId.AllRouteCluesFound, 9, true));
    }

    [Test]
    public void NewSession_ResetAllowsRevisionSequenceToRestart()
    {
        var ledger = new SharedQuestPresentationReceiptLedger();
        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.ArrivalCarInspected, 8, true));

        ledger.ResetForNewSession();

        Assert.IsTrue(ledger.TryAccept(SharedQuestPresentationEventId.ArrivalCarInspected, 1, true));
    }
}
