using NUnit.Framework;

public sealed class StoryCheckpointProgressRulesTests
{
    [Test]
    public void SharedMilestoneAloneDoesNotAdvanceAnyPlayer()
    {
        var player = new StoryCheckpointProgressRecord();

        Assert.That(player.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));
        Assert.That(player.ArrivedOfficeHospital, Is.False);
    }

    [Test]
    public void CheckpointOneRequiresVerifiedMilestoneAndPersonalArrival()
    {
        var player = new StoryCheckpointProgressRecord();

        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, false), Is.False);
        Assert.That(player.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));

        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.True);
        Assert.That(player.ClueMilestoneEligible, Is.True);
        Assert.That(player.ArrivedOfficeHospital, Is.True);
        Assert.That(player.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.OfficeHospital));
    }

    [Test]
    public void OnePlayersArrivalDoesNotAdvanceAnotherPlayersRecord()
    {
        var ledger = new StoryCheckpointProgressLedger<int>();

        Assert.That(ledger.TryRecordVerifiedArrival(10, StoryCheckpoint.OfficeHospital, true,
            out StoryCheckpointProgressRecord playerA), Is.True);

        Assert.That(playerA.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.OfficeHospital));
        Assert.That(ledger.TryGet(20, out _), Is.False);
    }

    [Test]
    public void CheckpointTwoCannotSkipCheckpointOne()
    {
        var player = new StoryCheckpointProgressRecord();

        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.SchoolMilitary, true), Is.False);
        Assert.That(player.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));
        Assert.That(player.RadioMilestoneEligible, Is.False);
        Assert.That(player.ArrivedSchoolMilitary, Is.False);
    }

    [Test]
    public void CheckpointsAdvanceMonotonicallyAndDuplicateArrivalsAreIdempotent()
    {
        var player = new StoryCheckpointProgressRecord();

        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.True);
        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.False);
        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.SchoolMilitary, true), Is.True);
        Assert.That(player.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.False);
        Assert.That(player.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.SchoolMilitary));
    }

    [Test]
    public void RemovingPlayerPreventsRecycledIdentityFromInheritingProgress()
    {
        var ledger = new StoryCheckpointProgressLedger<int>();
        Assert.That(ledger.TryRecordVerifiedArrival(7, StoryCheckpoint.OfficeHospital, true, out _), Is.True);

        Assert.That(ledger.Remove(7), Is.True);
        Assert.That(ledger.TryGet(7, out _), Is.False);
        Assert.That(ledger.TryRecordVerifiedArrival(7, StoryCheckpoint.SchoolMilitary, true,
            out StoryCheckpointProgressRecord recycled), Is.False);
        Assert.That(recycled, Is.Null);
    }
}
