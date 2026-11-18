using Yamb.Domain.Enums;
using Yamb.Domain.Rules;

namespace Yamb.Domain.Services;

public sealed class ScoringEngine
{
    private readonly GameRuleset _rules;

    public ScoringEngine(GameRuleset? rules = null)
    {
        _rules = rules ?? GameRuleset.Default;
    }

    public int Calculate(IReadOnlyList<int> diceValues, Row row, int rollNumber)
    {
        if (diceValues is null)
            throw new ArgumentNullException(nameof(diceValues));
        if (diceValues.Count != _rules.DiceCount)
            throw new ArgumentException($"Exactly {_rules.DiceCount} dice values required.");
        if (rollNumber < 1 || rollNumber > _rules.MaxRollsPerTurn)
            throw new ArgumentOutOfRangeException(nameof(rollNumber), "Roll number must be between 1 and the maximum rolls per turn.");

        var sorted = diceValues.OrderBy(v => v).ToList();

        return row switch
        {
            Row.Ones => CalculateNumbers(sorted, 1),
            Row.Twos => CalculateNumbers(sorted, 2),
            Row.Threes => CalculateNumbers(sorted, 3),
            Row.Fours => CalculateNumbers(sorted, 4),
            Row.Fives => CalculateNumbers(sorted, 5),
            Row.Sixes => CalculateNumbers(sorted, 6),
            Row.Max => sorted.TakeLast(_rules.ScoringDiceCount).Sum(),
            Row.Min => sorted.Take(_rules.ScoringDiceCount).Sum(),
            Row.Straight => CalculateStraight(sorted, rollNumber),
            Row.ThreeOfAKind => CalculateThreeOfAKind(sorted),
            Row.FullHouse => CalculateFullHouse(sorted),
            Row.FourOfAKind => CalculateFourOfAKind(sorted),
            Row.Yamb => CalculateYamb(sorted),
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }

    public int GetMaximum(Row row) => row switch
    {
        Row.Ones => _rules.ScoringDiceCount,
        Row.Twos => 2 * _rules.ScoringDiceCount,
        Row.Threes => 3 * _rules.ScoringDiceCount,
        Row.Fours => 4 * _rules.ScoringDiceCount,
        Row.Fives => 5 * _rules.ScoringDiceCount,
        Row.Sixes => 6 * _rules.ScoringDiceCount,
        Row.Max => 6 * _rules.ScoringDiceCount,
        Row.Min => _rules.ScoringDiceCount,
        Row.Straight => _rules.StraightFirstRoll,
        Row.ThreeOfAKind => 3 * 6 + _rules.ThreeOfAKindBonus,
        Row.FullHouse => 3 * 6 + 2 * 5 + _rules.FullHouseBonus,
        Row.FourOfAKind => 4 * 6 + _rules.FourOfAKindBonus,
        Row.Yamb => _rules.ScoringDiceCount * 6 + _rules.YambBonus,
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };

    private int CalculateNumbers(List<int> sorted, int face)
    {
        int count = Math.Min(sorted.Count(v => v == face), _rules.ScoringDiceCount);
        return count * face;
    }

    private int CalculateStraight(List<int> sorted, int rollNumber)
    {
        var distinct = sorted.Distinct().ToHashSet();
        bool isLowStraight = new[] { 1, 2, 3, 4, 5 }.All(n => distinct.Contains(n));
        bool isHighStraight = new[] { 2, 3, 4, 5, 6 }.All(n => distinct.Contains(n));

        if (!isLowStraight && !isHighStraight) return 0;

        return rollNumber switch
        {
            1 => _rules.StraightFirstRoll,
            2 => _rules.StraightSecondRoll,
            _ => _rules.StraightThirdRoll
        };
    }

    private int CalculateThreeOfAKind(List<int> sorted)
    {
        var triple = sorted.GroupBy(v => v)
                           .Where(g => g.Count() >= 3)
                           .OrderByDescending(g => g.Key)
                           .FirstOrDefault();
        if (triple is null) return 0;
        return 3 * triple.Key + _rules.ThreeOfAKindBonus;
    }

    private int CalculateFullHouse(List<int> sorted)
    {
        var groups = sorted.GroupBy(v => v).ToList();

        var tripleCandidates = groups.Where(g => g.Count() >= 3).ToList();
        var pairCandidates = groups.Where(g => g.Count() >= 2).ToList();

        int bestScore = 0;

        foreach (var triple in tripleCandidates)
        {
            foreach (var pair in pairCandidates)
            {
                if (pair.Key == triple.Key) continue;

                int score = 3 * triple.Key + 2 * pair.Key + _rules.FullHouseBonus;
                if (score > bestScore) bestScore = score;
            }
        }

        return bestScore;
    }

    private int CalculateFourOfAKind(List<int> sorted)
    {
        var quad = sorted.GroupBy(v => v)
                         .Where(g => g.Count() >= 4)
                         .OrderByDescending(g => g.Key)
                         .FirstOrDefault();
        if (quad is null) return 0;
        return 4 * quad.Key + _rules.FourOfAKindBonus;
    }

    private int CalculateYamb(List<int> sorted)
    {
        var five = sorted.GroupBy(v => v)
                         .Where(g => g.Count() >= _rules.ScoringDiceCount)
                         .OrderByDescending(g => g.Key)
                         .FirstOrDefault();
        if (five is null) return 0;
        return _rules.ScoringDiceCount * five.Key + _rules.YambBonus;
    }

    public int? CalculateNumbersBonus(IReadOnlyDictionary<Row, int?> columnCells)
    {
        var numberRows = new[] { Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes };
        if (numberRows.Any(r => !IsFilled(columnCells, r))) return null;

        int sum = numberRows.Sum(r => columnCells[r]!.Value);
        return sum + (sum >= _rules.NumbersBonusThreshold ? _rules.NumbersBonusAmount : 0);
    }

    public int? CalculateMaxMinBonus(IReadOnlyDictionary<Row, int?> columnCells)
    {
        if (!IsFilled(columnCells, Row.Max)) return null;
        if (!IsFilled(columnCells, Row.Min)) return null;
        if (!IsFilled(columnCells, Row.Ones)) return null;

        return (columnCells[Row.Max]!.Value - columnCells[Row.Min]!.Value) * columnCells[Row.Ones]!.Value;
    }

    public int? CalculateCombinationsBonus(IReadOnlyDictionary<Row, int?> columnCells)
    {
        var comboRows = new[] { Row.Straight, Row.ThreeOfAKind, Row.FullHouse, Row.FourOfAKind, Row.Yamb };
        if (comboRows.Any(r => !IsFilled(columnCells, r))) return null;

        return comboRows.Sum(r => columnCells[r]!.Value);
    }

    private static bool IsFilled(IReadOnlyDictionary<Row, int?> cells, Row row) =>
        cells.TryGetValue(row, out var v) && v.HasValue;
}
