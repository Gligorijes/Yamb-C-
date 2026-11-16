using FluentAssertions;
using Yamb.Domain.Enums;
using Yamb.Domain.Services;

namespace Yamb.Domain.Tests;

public class ColumnValidatorTests
{
    private static TurnContext Context(
        Row? announced = null,
        bool allThrown = true,
        Row? directed = null,
        bool obligatoryUnlocked = false) =>
        new(RollPhase.AfterFirstRoll, announced, allThrown, directed, obligatoryUnlocked);

    private static IReadOnlyDictionary<Row, int?> Cells(params (Row Row, int Value)[] filled)
    {
        var cells = Enum.GetValues<Row>().ToDictionary(r => r, _ => (int?)null);
        foreach (var (row, value) in filled)
        {
            cells[row] = value;
        }
        return cells;
    }

    [Fact]
    public void Downward_FirstEntry_MustBeOnes()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Downward, Cells(), Context());
        available.Should().ContainSingle().Which.Should().Be(Row.Ones);
    }

    [Fact]
    public void Downward_Sequence_AdvancesAfterWrite()
    {
        var cells = Cells((Row.Ones, 5));
        var available = ColumnValidator.GetAvailableRows(Column.Downward, cells, Context());
        available.Should().ContainSingle().Which.Should().Be(Row.Twos);
    }

    [Fact]
    public void Downward_Complete_ReturnsNothing()
    {
        var cells = Enum.GetValues<Row>().ToDictionary(r => r, _ => (int?)0);
        var available = ColumnValidator.GetAvailableRows(Column.Downward, cells, Context());
        available.Should().BeEmpty();
    }

    [Fact]
    public void Upward_FirstEntry_MustBeYamb()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Upward, Cells(), Context());
        available.Should().ContainSingle().Which.Should().Be(Row.Yamb);
    }

    [Fact]
    public void Upward_Sequence_AdvancesAfterWrite()
    {
        var cells = Cells((Row.Yamb, 0));
        var available = ColumnValidator.GetAvailableRows(Column.Upward, cells, Context());
        available.Should().ContainSingle().Which.Should().Be(Row.FourOfAKind);
    }

    [Fact]
    public void Free_AllowsAnyEmptyCell()
    {
        var cells = Cells((Row.Ones, 5), (Row.Yamb, 80));
        var available = ColumnValidator.GetAvailableRows(Column.Free, cells, Context());
        available.Should().HaveCount(11);
        available.Should().NotContain(Row.Ones);
        available.Should().NotContain(Row.Yamb);
    }

    [Fact]
    public void Announcement_WithoutAnnouncement_ReturnsNothing()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Announcement, Cells(), Context());
        available.Should().BeEmpty();
    }

    [Fact]
    public void Announcement_WithAnnouncement_ReturnsAnnouncedRow()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Announcement, Cells(), Context(Row.Yamb));
        available.Should().ContainSingle().Which.Should().Be(Row.Yamb);
    }

    [Fact]
    public void Announcement_WithAlreadyFilledAnnouncedRow_ReturnsNothing()
    {
        var cells = Cells((Row.Yamb, 80));
        var available = ColumnValidator.GetAvailableRows(Column.Announcement, cells, Context(Row.Yamb));
        available.Should().BeEmpty();
    }

    [Fact]
    public void Hand_WhenDiceWereHeld_ReturnsNothing()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Hand, Cells(), Context(allThrown: false));
        available.Should().BeEmpty();
    }

    [Fact]
    public void Hand_WhenAllDiceThrown_AllowsAnyEmptyCell()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Hand, Cells(), Context(allThrown: true));
        available.Should().HaveCount(13);
    }

    [Fact]
    public void BothEnds_Initially_OffersTopAndBottom()
    {
        var available = ColumnValidator.GetAvailableRows(Column.BothEnds, Cells(), Context());
        available.Should().HaveCount(2);
        available.Should().Contain(Row.Ones);
        available.Should().Contain(Row.Yamb);
    }

    [Fact]
    public void BothEnds_SingleRemainingCell_ServedFromEitherDirection()
    {
        var filled = Enum.GetValues<Row>().Where(r => r != Row.Max).ToArray();
        var cells = filled.ToDictionary(r => r, _ => (int?)0);
        var available = ColumnValidator.GetAvailableRows(Column.BothEnds, cells, Context());
        available.Should().ContainSingle().Which.Should().Be(Row.Max);
    }

    [Fact]
    public void MiddleOut_Initially_OffersMaxAndMin()
    {
        var available = ColumnValidator.GetAvailableRows(Column.MiddleOut, Cells(), Context());
        available.Should().HaveCount(2);
        available.Should().Contain(Row.Max);
        available.Should().Contain(Row.Min);
    }

    [Fact]
    public void MiddleOut_SequenceAdvancesIndependently()
    {
        var cells = Cells((Row.Max, 30), (Row.Min, 5));
        var available = ColumnValidator.GetAvailableRows(Column.MiddleOut, cells, Context());
        available.Should().HaveCount(2);
        available.Should().Contain(Row.Sixes);
        available.Should().Contain(Row.Straight);
    }

    [Fact]
    public void Directed_WithoutTarget_ReturnsNothing()
    {
        var available = ColumnValidator.GetAvailableRows(Column.Directed, Cells(), Context());
        available.Should().BeEmpty();
    }

    [Fact]
    public void Directed_WithTarget_ReturnsTargetRow()
    {
        var available = ColumnValidator.GetAvailableRows(
            Column.Directed, Cells(), Context(directed: Row.FullHouse));
        available.Should().ContainSingle().Which.Should().Be(Row.FullHouse);
    }

    [Fact]
    public void Directed_WithFilledTarget_ReturnsNothing()
    {
        var cells = Cells((Row.FullHouse, 58));
        var available = ColumnValidator.GetAvailableRows(
            Column.Directed, cells, Context(directed: Row.FullHouse));
        available.Should().BeEmpty();
    }

    [Fact]
    public void Obligatory_Locked_ReturnsNothing()
    {
        var available = ColumnValidator.GetAvailableRows(
            Column.Obligatory, Cells(), Context(obligatoryUnlocked: false));
        available.Should().BeEmpty();
    }

    [Fact]
    public void Obligatory_Unlocked_FillsTopDown()
    {
        var available = ColumnValidator.GetAvailableRows(
            Column.Obligatory, Cells(), Context(obligatoryUnlocked: true));
        available.Should().ContainSingle().Which.Should().Be(Row.Ones);

        var next = ColumnValidator.GetAvailableRows(
            Column.Obligatory, Cells((Row.Ones, 5)), Context(obligatoryUnlocked: true));
        next.Should().ContainSingle().Which.Should().Be(Row.Twos);
    }

    [Fact]
    public void Maximum_AllowsAnyEmptyCell()
    {
        var available = ColumnValidator.GetAvailableRows(
            Column.Maximum, Cells((Row.Ones, 5)), Context());
        available.Should().HaveCount(12);
        available.Should().NotContain(Row.Ones);
    }

    [Fact]
    public void MiddleOut_UpFrontExhausted_OnlyDownRemains()
    {
        var upRows = new[] { Row.Max, Row.Sixes, Row.Fives, Row.Fours, Row.Threes, Row.Twos, Row.Ones };
        var cells = upRows.ToDictionary(r => r, _ => (int?)0);
        var available = ColumnValidator.GetAvailableRows(Column.MiddleOut, cells, Context());
        available.Should().ContainSingle().Which.Should().Be(Row.Min);
    }
}
