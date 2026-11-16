namespace Yamb.Application.Models;

public sealed record LeaderboardEntry(Guid Id, string Participants, int Score, DateTime AchievedAt);
