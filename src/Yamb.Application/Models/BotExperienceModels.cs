namespace Yamb.Application.Models;

public enum BotDecisionKind
{
    Move,
    Hold,
    Announcement,
    StopRolling
}

public sealed record BotDecisionRecord(
    Guid GameId,
    BotDecisionKind Kind,
    string StateKey,
    string MoveKey,
    DateTime RecordedAtUtc);

public sealed record BotExperienceValue(
    int Count,
    double Mean,
    DateTime UpdatedAtUtc,
    int Games = 0);
