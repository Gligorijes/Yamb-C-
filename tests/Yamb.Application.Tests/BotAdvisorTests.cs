using FluentAssertions;
using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Application.Services;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;

namespace Yamb.Application.Tests;

public class BotAdvisorTests
{
    private readonly BotAdvisor _advisor = new(GameRuleset.Default);

    [Fact]
    public void ChooseMove_AvoidsZeroWhenPositiveMoveExists()
    {
        var columns = Enum.GetValues<Column>().Where(column => column != Column.Maximum).ToList();
        var game = CreateGame(3, columns, null, (1, false), (2, false), (3, false), (4, false), (5, false), (6, false));
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
        var game = CreateGame(3, null, null, (6, false), (6, false), (6, false), (6, false), (6, false), (1, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Sixes, 30),
            new AvailableMoveDto(Column.Maximum, Row.Max, 30)
        };

        var move = _advisor.ChooseMove(game, moves);

        move.Should().NotBeNull();
        move!.Column.Should().Be(Column.Maximum);
    }

    [Fact]
    public void ChooseMove_PrefersBonusCompletingMove()
    {
        var filled = new Dictionary<(Column, Row), int>
        {
            [(Column.Free, Row.Ones)] = 5,
            [(Column.Free, Row.Twos)] = 10,
            [(Column.Free, Row.Threes)] = 15,
            [(Column.Free, Row.Fours)] = 20,
            [(Column.Free, Row.Fives)] = 25,
            [(Column.Maximum, Row.FourOfAKind)] = 64,
            [(Column.Maximum, Row.Yamb)] = 80
        };
        var game = CreateGame(
            3,
            null,
            null,
            filled,
            (6, false), (6, false), (6, false), (6, false), (6, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Sixes, 30),
            new AvailableMoveDto(Column.Maximum, Row.Max, 30)
        };

        var move = _advisor.ChooseMove(game, moves);

        move.Should().BeEquivalentTo(new AvailableMoveDto(Column.Free, Row.Sixes, 30));
    }

    [Fact]
    public void ChooseDiceToHold_HoldsSixesForMaximumScore()
    {
        var game = CreateGame(2, null, null, (6, false), (6, false), (6, false), (6, false), (1, false), (2, false));
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
    public void ChooseDiceToHold_IgnoresMaximumWhenRowIsAnnounced()
    {
        var game = CreateGame(
            1,
            null,
            Row.Yamb,
            (6, false), (6, false), (6, false), (6, false), (6, false), (1, false));
        var moves = new[] { new AvailableMoveDto(Column.Announcement, Row.Yamb, 80) };

        var held = _advisor.ChooseDiceToHold(game, moves);

        held.Should().NotBeEmpty();
        held.Count(index => game.Dice.Dice[index].Value == 6).Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void ChooseAnnouncement_SelectsStrongCombination()
    {
        var game = CreateGame(1, null, null, (1, false), (1, false), (1, false), (1, false), (1, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Ones, 5),
            new AvailableMoveDto(Column.Free, Row.Yamb, 55)
        };
        var candidates = new[]
        {
            new AvailableMoveDto(Column.Announcement, Row.Ones, 5),
            new AvailableMoveDto(Column.Announcement, Row.Yamb, 55)
        };

        var row = _advisor.ChooseAnnouncement(game, moves, candidates);

        row.Should().Be(Row.Yamb);
    }

    [Fact]
    public void ChooseAnnouncement_ReturnsNullWithoutCandidates()
    {
        var game = CreateGame(1, null, null, (1, false), (2, false), (3, false), (4, false), (5, false), (6, false));

        var row = _advisor.ChooseAnnouncement(game, Array.Empty<AvailableMoveDto>(), Array.Empty<AvailableMoveDto>());

        row.Should().BeNull();
    }

    [Fact]
    public void ChooseMove_OpensConstrainedColumnInsteadOfFree()
    {
        var columns = Enum.GetValues<Column>().Where(column => column != Column.Maximum).ToList();
        var game = CreateGame(3, columns, null, (6, false), (6, false), (6, false), (6, false), (6, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Sixes, 30),
            new AvailableMoveDto(Column.Downward, Row.Sixes, 30)
        };

        var move = _advisor.ChooseMove(game, moves);

        move!.Column.Should().Be(Column.Downward);
    }

    [Fact]
    public void ChooseMove_ValuesMaxMinSpread()
    {
        var columns = new[] { Column.Downward, Column.Free };
        var filled = new Dictionary<(Column, Row), int>
        {
            [(Column.Downward, Row.Ones)] = 5,
            [(Column.Free, Row.Ones)] = 5
        };
        var game = CreateGame(
            3,
            columns,
            null,
            filled,
            (1, false), (1, false), (1, false), (1, false), (1, false), (1, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Downward, Row.Max, 5),
            new AvailableMoveDto(Column.Free, Row.Min, 5)
        };

        var move = _advisor.ChooseMove(game, moves);

        move.Should().BeEquivalentTo(new AvailableMoveDto(Column.Free, Row.Min, 5));
    }

    [Fact]
    public void ShouldFinishTurn_LocksFirstRollStraight()
    {
        var game = CreateGame(1, null, null, (1, false), (2, false), (3, false), (4, false), (5, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Ones, 1),
            new AvailableMoveDto(Column.Downward, Row.Straight, 66)
        };

        bool finish = _advisor.ShouldFinishTurn(game, moves);

        finish.Should().BeTrue();
    }

    [Fact]
    public void ShouldFinishTurn_DoesNotLockWeakEarlyMove()
    {
        var game = CreateGame(1, null, null, (1, false), (1, false), (2, false), (2, false), (3, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Threes, 3),
            new AvailableMoveDto(Column.Downward, Row.Ones, 2)
        };

        bool finish = _advisor.ShouldFinishTurn(game, moves);

        finish.Should().BeFalse();
    }

    [Fact]
    public void ChooseMove_AvoidsMinimumAboveNine()
    {
        var game = CreateGame(3, null, null, (4, false), (4, false), (4, false), (5, false), (6, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Min, 23),
            new AvailableMoveDto(Column.Downward, Row.Max, 25)
        };

        var move = _advisor.ChooseMove(game, moves);

        move!.Row.Should().Be(Row.Max);
    }

    [Fact]
    public void ChooseMove_ValuesSolidMaximum()
    {
        var columns = Enum.GetValues<Column>().Where(column => column != Column.Maximum).ToList();
        var game = CreateGame(3, columns, null, (5, false), (5, false), (5, false), (6, false), (6, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Min, 27),
            new AvailableMoveDto(Column.Downward, Row.Max, 28)
        };

        var move = _advisor.ChooseMove(game, moves);

        move!.Row.Should().Be(Row.Max);
    }

    [Fact]
    public void ChooseMove_AvoidsWeakFullHouseEarly()
    {
        var game = CreateGame(3, null, null, (4, false), (4, false), (4, false), (2, false), (2, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.FullHouse, 46),
            new AvailableMoveDto(Column.Downward, Row.ThreeOfAKind, 32)
        };

        var move = _advisor.ChooseMove(game, moves);

        move!.Row.Should().Be(Row.ThreeOfAKind);
    }

    [Fact]
    public void ChooseMove_AvoidsWeakThreeOfAKindEarly()
    {
        var game = CreateGame(3, null, null, (3, false), (3, false), (3, false), (5, false), (5, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.ThreeOfAKind, 29),
            new AvailableMoveDto(Column.Downward, Row.Fives, 10)
        };

        var move = _advisor.ChooseMove(game, moves);

        move!.Row.Should().Be(Row.Fives);
    }

    [Fact]
    public void ChooseMove_UsesLearnedExperienceWhenHeuristicIsTied()
    {
        var favored = new AvailableMoveDto(Column.Upward, Row.Sixes, 30);
        var advisor = new BotAdvisor(GameRuleset.Default, new FavoringExperienceStore(BotExperienceKeys.Move(favored)));
        var columns = Enum.GetValues<Column>().Where(column => column != Column.Maximum).ToList();
        var game = CreateGame(3, columns, null, (6, false), (6, false), (6, false), (6, false), (6, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Downward, Row.Sixes, 30),
            favored
        };

        var move = advisor.ChooseMove(game, moves);

        move.Should().BeEquivalentTo(favored);
    }

    [Fact]
    public void BotExperienceKeys_State_GeneralizesAcrossEquivalentDiceShapes()
    {
        var first = CreateGame(3, null, null, (1, false), (1, false), (2, false), (3, false), (4, false), (4, false));
        var second = CreateGame(3, null, null, (1, false), (1, false), (2, false), (5, false), (6, false), (6, false));

        BotExperienceKeys.State(first).Should().Be(BotExperienceKeys.State(second));
    }

    [Fact]
    public void ShouldFinishTurn_UsesLearnedStopExperience()
    {
        var advisor = new BotAdvisor(
            GameRuleset.Default,
            new FavoringExperienceStore(BotExperienceKeys.Stop(true), BotDecisionKind.StopRolling));
        var game = CreateGame(1, null, null, (1, false), (1, false), (2, false), (2, false), (3, false), (6, false));
        var moves = new[]
        {
            new AvailableMoveDto(Column.Free, Row.Threes, 3),
            new AvailableMoveDto(Column.Downward, Row.Ones, 2)
        };

        advisor.ShouldFinishTurn(game, moves).Should().BeTrue();
    }

    private static GameDto CreateGame(
        int rollNumber,
        IReadOnlyList<Column>? columns,
        Row? announcedRow,
        params (int Value, bool Held)[] dice) =>
        CreateGame(rollNumber, columns, announcedRow, null, dice);

    private static GameDto CreateGame(
        int rollNumber,
        IReadOnlyList<Column>? columns,
        Row? announcedRow,
        IReadOnlyDictionary<(Column, Row), int>? filled,
        params (int Value, bool Held)[] dice)
    {
        columns ??= Enum.GetValues<Column>().ToList();
        var cells = columns.ToDictionary(
            column => column,
            currentColumn => (IReadOnlyDictionary<Row, int?>)Enum.GetValues<Row>()
                .ToDictionary(
                    row => row,
                    row => filled is not null && filled.TryGetValue((currentColumn, row), out int score)
                        ? (int?)score
                        : null));
        var bonuses = columns.ToDictionary(column => column, _ => new BonusesDto(null, null, null));
        var sheet = new ScoresheetDto(
            cells,
            bonuses,
            Enum.GetValues<Row>().ToDictionary(row => row, _ => (int?)null),
            new BonusesDto(null, null, null),
            columns.ToDictionary(column => column, _ => 0),
            filled?.Values.Sum() ?? 0,
            filled?.Count ?? 0,
            columns.Count * Enum.GetValues<Row>().Length,
            null);
        var player = new PlayerDto("Bot", sheet);

        return new GameDto(
            Guid.NewGuid(),
            GameMode.SinglePlayer,
            columns,
            new[] { player },
            0,
            new DiceSetDto(dice.Select(die => new DieDto(die.Value, die.Held)).ToList()),
            rollNumber,
            announcedRow,
            !dice.Any(die => die.Held),
            false,
            GameResult.InProgress);
    }

    private sealed class FavoringExperienceStore : IBotExperienceStore
    {
        private readonly string _favoredKey;
        private readonly BotDecisionKind _favoredKind;

        public FavoringExperienceStore(string favoredKey, BotDecisionKind favoredKind = BotDecisionKind.Move)
        {
            _favoredKey = favoredKey;
            _favoredKind = favoredKind;
        }

        public int GamesCompleted => 50;

        public BotExperienceValue? Get(BotDecisionKind kind, string stateKey, string moveKey) =>
            kind == _favoredKind && moveKey == _favoredKey
                ? new BotExperienceValue(40, 1, DateTime.UtcNow, 12)
                : null;

        public void RememberDecision(BotDecisionRecord decision)
        {
        }

        public Task CompleteGameAsync(Guid gameId, double reward) => Task.CompletedTask;
    }
}
