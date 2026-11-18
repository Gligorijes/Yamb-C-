using FluentAssertions;
using Yamb.Application.DTOs;
using Yamb.Application.Services;
using Yamb.Domain.Enums;

namespace Yamb.Application.Tests;

public class BotAdvisorTests
{
    private readonly BotAdvisor _advisor = new();

    [Fact]
    public void ChooseMove_AvoidsZeroWhenPositiveMoveExists()
    {
        var game = CreateGame(3, (1, false), (2, false), (3, false), (4, false), (5, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Yamb, 0),
            new AvailableMoveDto(Column.Downward, Row.Ones, 1)
        };

        var move = _advisor.ChooseMove(game, moves);

        move.Should().BeEquivalentTo(new AvailableMoveDto(Column.Downward, Row.Ones, 1));
    }

    [Fact]
    public void ChooseMove_PrefersMaximumColumnQualifier()
    {
        var game = CreateGame(3, (6, false), (6, false), (6, false), (6, false), (6, false), (1, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Sixes, 30),
            new AvailableMoveDto(Column.Maximum, Row.Max, 30)
        };

        var move = _advisor.ChooseMove(game, moves);

        move.Should().BeEquivalentTo(new AvailableMoveDto(Column.Maximum, Row.Max, 30));
    }

    [Fact]
    public void ChooseDiceToHold_HoldsSixesForMaximumScore()
    {
        var game = CreateGame(2, (6, false), (6, false), (6, false), (6, false), (1, false), (2, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Sixes, 24),
            new AvailableMoveDto(Column.Maximum, Row.Max, 24)
        };

        var held = _advisor.ChooseDiceToHold(game, moves);

        held.Should().NotBeEmpty();
        held.Should().OnlyContain(index => game.Dice.Dice[index].Value == 6);
    }

    [Fact]
    public void ChooseAnnouncement_SelectsStrongCombination()
    {
        var game = CreateGame(1, (1, false), (1, false), (1, false), (1, false), (1, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Announcement, Row.Ones, 5),
            new AvailableMoveDto(Column.Announcement, Row.Yamb, 80)
        };

        var row = _advisor.ChooseAnnouncement(game, moves);

        row.Should().Be(Row.Yamb);
    }

    private static GameDto CreateGame(int rollNumber, params (int Value, bool Held)[] dice)
    {
        var columns = Enum.GetValues<Column>().ToList();
        var cells = columns.ToDictionary(
            column => column,
            _ => (IReadOnlyDictionary<Row, int?>)Enum.GetValues<Row>()
                .ToDictionary(row => row, _ => (int?)null));
        var bonuses = columns.ToDictionary(column => column, _ => new BonusesDto(null, null, null));
        var sheet = new ScoresheetDto(
            cells,
            bonuses,
            Enum.GetValues<Row>().ToDictionary(row => row, _ => (int?)null),
            new BonusesDto(null, null, null),
            columns.ToDictionary(column => column, _ => 0),
            0,
            0,
            columns.Count * Enum.GetValues<Row>().Length,
            null);
        var player = new PlayerDto("Bot", sheet);

        return new GameDto(
            Guid.NewGuid(),
            GameMode.VersusComputer,
            columns,
            new[] { player },
            0,
            new DiceSetDto(dice.Select(die => new DieDto(die.Value, die.Held)).ToList()),
            rollNumber,
            null,
            true,
            false,
            GameResult.InProgress);
    }
}
