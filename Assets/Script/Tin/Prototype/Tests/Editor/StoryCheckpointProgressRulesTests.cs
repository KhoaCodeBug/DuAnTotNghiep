using NUnit.Framework;

public sealed class StoryCheckpointProgressRulesTests
{
    [Test]
    public void SharedMilestoneAloneDoesNotAdvanceTeamCheckpoint()
    {
        var team = new StoryCheckpointProgressRecord();

        Assert.That(team.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));
        Assert.That(team.ArrivedOfficeHospital, Is.False);
    }

    [Test]
    public void CheckpointOneRequiresVerifiedMilestoneAndTeamMemberArrival()
    {
        var team = new StoryCheckpointProgressRecord();

        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, false), Is.False);
        Assert.That(team.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));

        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.True);
        Assert.That(team.ClueMilestoneEligible, Is.True);
        Assert.That(team.ArrivedOfficeHospital, Is.True);
        Assert.That(team.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.OfficeHospital));
    }

    [Test]
    public void CheckpointTwoCannotSkipCheckpointOne()
    {
        var team = new StoryCheckpointProgressRecord();

        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.SchoolMilitary, true), Is.False);
        Assert.That(team.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.Start));
        Assert.That(team.RadioMilestoneEligible, Is.False);
        Assert.That(team.ArrivedSchoolMilitary, Is.False);
    }

    [Test]
    public void CheckpointsAdvanceMonotonicallyAndDuplicateArrivalsAreIdempotent()
    {
        var team = new StoryCheckpointProgressRecord();

        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.True);
        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.False);
        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.SchoolMilitary, true), Is.True);
        Assert.That(team.TryRecordVerifiedArrival(StoryCheckpoint.OfficeHospital, true), Is.False);
        Assert.That(team.HighestCheckpoint, Is.EqualTo(StoryCheckpoint.SchoolMilitary));
    }
}
