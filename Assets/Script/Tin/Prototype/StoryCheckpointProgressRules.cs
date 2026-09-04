using System.Collections.Generic;

public enum StoryCheckpoint
{
    Start = 0,
    OfficeHospital = 1,
    SchoolMilitary = 2
}

/// <summary>
/// Authority-owned progress for one session player. The runtime dictionary is
/// keyed by PlayerRef; this pure record keeps checkpoint advancement explicit
/// and testable without tying durable progress to a replaceable avatar.
/// </summary>
public sealed class StoryCheckpointProgressRecord
{
    public bool ClueMilestoneEligible { get; private set; }
    public bool ArrivedOfficeHospital { get; private set; }
    public bool RadioMilestoneEligible { get; private set; }
    public bool ArrivedSchoolMilitary { get; private set; }
    public StoryCheckpoint HighestCheckpoint { get; private set; }

    /// <summary>
    /// Records an arrival only when the corresponding shared milestone is
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

/// <summary>
/// Session-scoped owner map. Removing a player deletes the complete record so
/// a recycled network identity cannot inherit another participant's progress.
/// </summary>
public sealed class StoryCheckpointProgressLedger<TPlayer>
{
    private readonly Dictionary<TPlayer, StoryCheckpointProgressRecord> records =
        new Dictionary<TPlayer, StoryCheckpointProgressRecord>();

    public bool TryRecordVerifiedArrival(TPlayer player, StoryCheckpoint arrival,
        bool sharedMilestoneEligible, out StoryCheckpointProgressRecord progress)
    {
        if (!records.TryGetValue(player, out progress))
        {
            progress = new StoryCheckpointProgressRecord();
            if (!progress.TryRecordVerifiedArrival(arrival, sharedMilestoneEligible))
            {
                progress = null;
                return false;
            }

            records.Add(player, progress);
            return true;
        }

        return progress.TryRecordVerifiedArrival(arrival, sharedMilestoneEligible);
    }

    public bool TryGet(TPlayer player, out StoryCheckpointProgressRecord progress) =>
        records.TryGetValue(player, out progress);

    public bool Remove(TPlayer player) => records.Remove(player);

    public void ResetForNewSession() => records.Clear();
}
