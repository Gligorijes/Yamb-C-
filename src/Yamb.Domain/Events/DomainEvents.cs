using Yamb.Domain.Enums;

namespace Yamb.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public sealed record CellFilledEvent(Column Column, Row Row, int Score, DateTime OccurredAt) : IDomainEvent;

public sealed record GameCompletedEvent(GameResult Result, DateTime OccurredAt) : IDomainEvent;
