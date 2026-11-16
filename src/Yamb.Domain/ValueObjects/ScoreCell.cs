namespace Yamb.Domain.ValueObjects;

public sealed record ScoreCell(int? Value = null)
{
    public bool IsFilled => Value.HasValue;

    public ScoreCell Fill(int score) => new(score);
}
