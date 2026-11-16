namespace Yamb.Application.Options;

public sealed class LeaderboardOptions
{
    public const string SectionName = "Leaderboard";

    public int MaxEntries { get; set; } = 50;
}
