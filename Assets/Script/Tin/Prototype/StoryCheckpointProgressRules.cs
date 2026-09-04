public enum StoryCheckpoint
{
    Start = 0,
    OfficeHospital = 1,
    SchoolMilitary = 2
}

/// <summary>
/// Authority-owned checkpoint progress for the current team/session. This
/// record lives on the scene quest manager, so replacing an avatar does not
/// reset the team's latest verified checkpoint.
/// </summary>
public sealed class StoryCheckpointProgressRecord
{
    public bool ClueMilestoneEligible { get; private set; }
    public bool ArrivedOfficeHospital { get; private set; }
    public bool RadioMilestoneEligible { get; private set; }
    public bool ArrivedSchoolMilitary { get; private set; }
    public StoryCheckpoint HighestCheckpoint { get; private set; }

    /// <summary>
    /// Records a team member's verified arrival only when the corresponding shared milestone is
    /// currently valid. Checkpoint two cannot skip checkpoint one.
    /// Returns true only when the highest checkpoint advances.
    /// </summary>
    public bool TryRecordVerifiedArrival(StoryCheckpoint arrival, bool sharedMilestoneEligible)
    {
        if (!sharedMilestoneEligible) return false;

        StoryCheckpoint previous = HighestCheckpoint;
        switch (arrival)
        {
            case StoryCheckpoint.OfficeHospital:
                ClueMilestoneEligible = true;
                ArrivedOfficeHospital = true;
                if (HighestCheckpoint < StoryCheckpoint.OfficeHospital)
                    HighestCheckpoint = StoryCheckpoint.OfficeHospital;
                break;

            case StoryCheckpoint.SchoolMilitary:
                if (HighestCheckpoint < StoryCheckpoint.OfficeHospital) return false;
                RadioMilestoneEligible = true;
                ArrivedSchoolMilitary = true;
                if (HighestCheckpoint < StoryCheckpoint.SchoolMilitary)
                    HighestCheckpoint = StoryCheckpoint.SchoolMilitary;
                break;

            default:
                return false;
        }

        return HighestCheckpoint > previous;
    }
}
