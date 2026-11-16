namespace Yamb.Domain.Rules;

public sealed record GameRuleset
{
    public static GameRuleset Default { get; } = new();

    public int DiceCount { get; init; } = 6;

    public int ScoringDiceCount { get; init; } = 5;

    public int MaxRollsPerTurn { get; init; } = 3;

    public int StraightFirstRoll { get; init; } = 66;

    public int StraightSecondRoll { get; init; } = 56;

    public int StraightThirdRoll { get; init; } = 46;

    public int ThreeOfAKindBonus { get; init; } = 20;

    public int FullHouseBonus { get; init; } = 30;

    public int FourOfAKindBonus { get; init; } = 40;

    public int YambBonus { get; init; } = 50;

    public int NumbersBonusThreshold { get; init; } = 60;

    public int NumbersBonusAmount { get; init; } = 30;
}
