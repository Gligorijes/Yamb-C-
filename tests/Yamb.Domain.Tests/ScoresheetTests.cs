using FluentAssertions;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Events;
using Yamb.Domain.Exceptions;

namespace Yamb.Domain.Tests;

public class ScoresheetTests
{
    private readonly Scoresheet _sheet = new();

    [Fact]
    public void WriteScore_ThenReadCell()
    {
        _sheet.WriteScore(Column.Free, Row.Ones, 5);
        _sheet.GetCell(Column.Free, Row.Ones).Should().Be(5);
        _sheet.IsCellFilled(Column.Free, Row.Ones).Should().BeTrue();
    }

    [Fact]
    public void WriteScore_Twice_Throws()
    {
        _sheet.WriteScore(Column.Free, Row.Ones, 5);
        var act = () => _sheet.WriteScore(Column.Free, Row.Ones, 3);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void WriteScore_RaisesCellFilledEvent()
    {
        _sheet.ClearDomainEvents();
        _sheet.WriteScore(Column.Downward, Row.Sixes, 30);
        _sheet.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CellFilledEvent>();
    }

    [Fact]
    public void NumbersBonus_PendingUntilAllNumberRowsFilled()
    {
        _sheet.WriteScore(Column.Downward, Row.Ones, 5);
        _sheet.WriteScore(Column.Downward, Row.Twos, 10);
        _sheet.WriteScore(Column.Downward, Row.Threes, 15);
        _sheet.WriteScore(Column.Downward, Row.Fours, 20);
        _sheet.WriteScore(Column.Downward, Row.Fives, 25);

        _sheet.GetBonus(Column.Downward, BonusType.NumbersBonus).Should().BeNull();

        _sheet.WriteScore(Column.Downward, Row.Sixes, 30);
        _sheet.GetBonus(Column.Downward, BonusType.NumbersBonus).Should().Be(135);
    }

    [Fact]
    public void FinalScore_IsNull_WhenIncomplete()
    {
        _sheet.WriteScore(Column.Downward, Row.Ones, 5);
        _sheet.FinalScore.Should().BeNull();
        _sheet.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void CompletedSheet_ComputesCorrectFinalScore()
    {
        FillColumn(_sheet, Column.Downward, highValues: true);
        foreach (var column in _sheet.Columns.Where(c => c != Column.Downward))
        {
            FillColumn(_sheet, column, highValues: false);
        }

        _sheet.GetBonus(Column.Downward, BonusType.NumbersBonus).Should().Be(135);
        _sheet.GetBonus(Column.Downward, BonusType.MaxMinBonus).Should().Be(125);
        _sheet.GetBonus(Column.Downward, BonusType.CombinationsBonus).Should().Be(306);

        _sheet.IsComplete.Should().BeTrue();
        _sheet.FinalScore.Should().Be(566);
    }

    [Fact]
    public void GetColumnTotal_SumsPartialColumn()
    {
        _sheet.WriteScore(Column.Downward, Row.Ones, 5);
        _sheet.WriteScore(Column.Downward, Row.Twos, 10);
        _sheet.WriteScore(Column.Downward, Row.Straight, 66);

        _sheet.GetColumnTotal(Column.Downward).Should().Be(81);
        _sheet.CurrentScore.Should().Be(81);
    }

    [Fact]
    public void GetColumnTotal_CompletedColumn_MatchesFinalScore()
    {
        FillColumn(_sheet, Column.Downward, highValues: true);

        _sheet.GetColumnTotal(Column.Downward).Should().Be(566);
        _sheet.CurrentScore.Should().Be(566);
    }

    [Fact]
    public void Restore_RebuildsCells()
    {
        var original = new Scoresheet();
        FillColumn(original, Column.Downward, highValues: true);

        var restored = Scoresheet.Restore(
            Yamb.Domain.Rules.GameRuleset.Default,
            original.GetAllCells()
                .SelectMany(c => c.Value.Select(cell => new Snapshots.CellSnapshot(c.Key, cell.Key, cell.Value)))
                .ToList());

        restored.GetCell(Column.Downward, Row.Ones).Should().Be(5);
        restored.GetCell(Column.Downward, Row.Yamb).Should().Be(80);
        restored.GetCell(Column.Free, Row.Ones).Should().BeNull();
        restored.FinalScore.Should().BeNull();
    }

    [Fact]
    public void Restore_ClampsNumberRowsToScoringDiceLimit()
    {
        var restored = Scoresheet.Restore(
            Yamb.Domain.Rules.GameRuleset.Default,
            new[]
            {
                new Snapshots.CellSnapshot(Column.Downward, Row.Ones, 6),
                new Snapshots.CellSnapshot(Column.Free, Row.Sixes, 36)
            });

        restored.GetCell(Column.Downward, Row.Ones).Should().Be(5);
        restored.GetCell(Column.Free, Row.Sixes).Should().Be(30);
    }

    [Fact]
    public void CellCounts_TrackFilledAndTotal()
    {
        new Scoresheet().TotalCellCount.Should().Be(91);
        _sheet.FilledCellCount.Should().Be(0);

        FillColumn(_sheet, Column.Downward, highValues: true);

        _sheet.FilledCellCount.Should().Be(13);
    }

    [Fact]
    public void GetRowTotal_SumsAcrossColumns_AndIsNullWhenIncomplete()
    {
        _sheet.WriteScore(Column.Downward, Row.Ones, 5);
        _sheet.WriteScore(Column.Free, Row.Ones, 3);
        _sheet.GetRowTotal(Row.Ones).Should().BeNull();

        foreach (var column in _sheet.Columns.Where(c => c != Column.Downward && c != Column.Free))
        {
            _sheet.WriteScore(column, Row.Ones, 1);
        }

        _sheet.GetRowTotal(Row.Ones).Should().Be(13);
    }

    private static void FillColumn(Scoresheet sheet, Column column, bool highValues)
    {
        var values = new Dictionary<Row, int>
        {
            [Row.Ones] = highValues ? 5 : 0,
            [Row.Twos] = highValues ? 10 : 0,
            [Row.Threes] = highValues ? 15 : 0,
            [Row.Fours] = highValues ? 20 : 0,
            [Row.Fives] = highValues ? 25 : 0,
            [Row.Sixes] = highValues ? 30 : 0,
            [Row.Max] = highValues ? 30 : 0,
            [Row.Min] = highValues ? 5 : 0,
            [Row.Straight] = highValues ? 66 : 0,
            [Row.ThreeOfAKind] = highValues ? 38 : 0,
            [Row.FullHouse] = highValues ? 58 : 0,
            [Row.FourOfAKind] = highValues ? 64 : 0,
            [Row.Yamb] = highValues ? 80 : 0
        };

        foreach (var (row, value) in values)
        {
            sheet.WriteScore(column, row, value);
        }
    }
}
