using Yamb.Domain.Enums;
using Yamb.Domain.Events;
using Yamb.Domain.Exceptions;
using Yamb.Domain.Rules;
using Yamb.Domain.Services;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;

namespace Yamb.Domain.Aggregates;

public sealed class Scoresheet
{
    private readonly Dictionary<Column, Dictionary<Row, ScoreCell>> _cells = new();
    private readonly ScoringEngine _engine;
    private readonly List<IDomainEvent> _domainEvents = new();

    public Scoresheet(GameRuleset? rules = null, IReadOnlyList<Column>? columns = null)
    {
        _engine = new ScoringEngine(rules);
        Columns = columns is { Count: > 0 }
            ? columns.Distinct().ToList()
            : SheetDefinitions.DefaultColumns.ToList();

        foreach (Column column in Columns)
        {
            _cells[column] = new Dictionary<Row, ScoreCell>();
            foreach (Row row in Enum.GetValues<Row>())
            {
                _cells[column][row] = new ScoreCell();
            }
        }
    }

    public IReadOnlyList<Column> Columns { get; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    public int? GetCell(Column column, Row row) => _cells[column][row].Value;

    public bool IsCellFilled(Column column, Row row) => _cells[column][row].IsFilled;

    public void WriteScore(Column column, Row row, int score)
    {
        if (IsCellFilled(column, row))
            throw new DomainException($"Cell [{column}][{row}] is already filled.");

        _cells[column][row] = _cells[column][row].Fill(score);
        _domainEvents.Add(new CellFilledEvent(column, row, score, DateTime.UtcNow));
    }

    public IReadOnlyDictionary<Row, int?> GetColumnCells(Column column) =>
        _cells[column].ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

    public IReadOnlyDictionary<Column, IReadOnlyDictionary<Row, int?>> GetAllCells() =>
        _cells.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<Row, int?>)kvp.Value.ToDictionary(c => c.Key, c => c.Value.Value));

    public int? GetBonus(Column column, BonusType bonus) => bonus switch
    {
        BonusType.NumbersBonus => _engine.CalculateNumbersBonus(GetColumnCells(column)),
        BonusType.MaxMinBonus => _engine.CalculateMaxMinBonus(GetColumnCells(column)),
        BonusType.CombinationsBonus => _engine.CalculateCombinationsBonus(GetColumnCells(column)),
        _ => throw new ArgumentOutOfRangeException(nameof(bonus))
    };

    public int GetColumnTotal(Column column)
    {
        var cells = GetColumnCells(column);
        int numericSum = 0;
        foreach (var row in new[] { Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes })
        {
            numericSum += cells[row] ?? 0;
        }

        int total = numericSum;

        if (_engine.CalculateNumbersBonus(cells) is int numbersBonus)
            total += numbersBonus - numericSum;

        if (_engine.CalculateMaxMinBonus(cells) is int maxMinBonus)
            total += maxMinBonus;

        foreach (var row in new[] { Row.Straight, Row.ThreeOfAKind, Row.FullHouse, Row.FourOfAKind, Row.Yamb })
        {
            total += cells[row] ?? 0;
        }

        return total;
    }

    public int CurrentScore => Columns.Sum(GetColumnTotal);

    public int? GetRowTotal(Row row)
    {
        var values = Columns.Select(column => _cells[column][row].Value).ToList();
        return values.Any(value => !value.HasValue) ? null : values.Sum(value => value!.Value);
    }

    public int FilledCellCount => _cells.Values.Sum(cells => cells.Values.Count(cell => cell.IsFilled));

    public int TotalCellCount => _cells.Count * _cells.Values.First().Count;

    public int? GetBonusTotal(BonusType bonus)
    {
        var values = Columns.Select(column => GetBonus(column, bonus)).ToList();
        return values.Any(v => !v.HasValue) ? null : values.Sum(v => v!.Value);
    }

    public int? FinalScore
    {
        get
        {
            var numbers = GetBonusTotal(BonusType.NumbersBonus);
            var maxMin = GetBonusTotal(BonusType.MaxMinBonus);
            var combos = GetBonusTotal(BonusType.CombinationsBonus);
            return numbers.HasValue && maxMin.HasValue && combos.HasValue
                ? numbers.Value + maxMin.Value + combos.Value
                : null;
        }
    }

    public bool IsComplete => FinalScore.HasValue;

    public static Scoresheet Restore(
        GameRuleset rules,
        IEnumerable<CellSnapshot> cells,
        IReadOnlyList<Column>? columns = null)
    {
        var snapshotCells = cells.ToList();
        var activeColumns = columns is { Count: > 0 }
            ? columns.Distinct().ToList()
            : snapshotCells.Select(cell => cell.Column).Distinct().ToList();

        if (activeColumns.Count == 0)
            activeColumns = SheetDefinitions.DefaultColumns.ToList();

        var sheet = new Scoresheet(rules, activeColumns);
        var scoring = new ScoringEngine(rules);
        foreach (var cell in snapshotCells)
        {
            if (sheet._cells.TryGetValue(cell.Column, out var rowCells) &&
                rowCells.ContainsKey(cell.Row))
            {
                int? value = cell.Value;
                if (value.HasValue && cell.Row is >= Row.Ones and <= Row.Sixes)
                    value = Math.Clamp(value.Value, 0, scoring.GetMaximum(cell.Row));
                rowCells[cell.Row] = new ScoreCell(value);
            }
        }
        return sheet;
    }
}
