using FluentAssertions;
using Microsoft.Extensions.Options;
using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Application.Options;
using Yamb.Application.Services;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Exceptions;
using Yamb.Domain.Rules;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;

namespace Yamb.Application.Tests;

public class GameServiceTests
{
    private readonly FakeGameRepository _gameRepository = new();
    private readonly FakeLeaderboardRepository _leaderboardRepository = new();
    private readonly GameService _service;

    public GameServiceTests()
    {
        var leaderboard = new LeaderboardService(
            _leaderboardRepository,
            Microsoft.Extensions.Options.Options.Create(new LeaderboardOptions { MaxEntries = 10 }));
        _service = new GameService(_gameRepository, leaderboard, new NoOpAnalyticsService(), GameRuleset.Default);
    }

    private static StartGameCommand SinglePlayerCommand(string name = "Ana") =>
        new(GameMode.SinglePlayer, new[] { name });

    [Fact]
    public async Task StartGame_CreatesPersistsAndReturnsDto()
    {
        var dto = await _service.StartGameAsync(SinglePlayerCommand());

        dto.Mode.Should().Be(GameMode.SinglePlayer);
        dto.Players.Should().ContainSingle().Which.Name.Should().Be("Ana");
        dto.CurrentPlayerIndex.Should().Be(0);
        dto.RollNumber.Should().Be(0);
        dto.IsGameOver.Should().BeFalse();
        dto.Result.Should().Be(GameResult.InProgress);

        (await _gameRepository.GetByIdAsync(dto.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task StartGame_FiveDice_CreatesAndReloadsFiveDice()
    {
        var dto = await _service.StartGameAsync(
            new StartGameCommand(GameMode.SinglePlayer, new[] { "Ana" }, DiceCount: 5));

        dto.Dice.Dice.Should().HaveCount(5);

        var reloaded = await _service.GetGameAsync(dto.Id);
        reloaded.Dice.Dice.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(SheetVariant.Four, 4, 52)]
    [InlineData(SheetVariant.Six, 6, 78)]
    [InlineData(SheetVariant.Seven, 7, 91)]
    [InlineData(SheetVariant.Eight, 8, 104)]
    [InlineData(SheetVariant.Nine, 9, 117)]
    [InlineData(SheetVariant.Ten, 10, 130)]
    public async Task StartGame_UsesSelectedSheetVariant(
        SheetVariant variant, int columnCount, int cellCount)
    {
        var dto = await _service.StartGameAsync(
            new StartGameCommand(GameMode.SinglePlayer, new[] { "Ana" }, variant));

        dto.Columns.Should().HaveCount(columnCount);
        dto.Players[0].Scoresheet.TotalCells.Should().Be(cellCount);
    }

    [Fact]
    public async Task GetAvailableMoves_TenColumns_ExcludesLockedDirectedAndObligatory()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.SinglePlayer, new[] { "Ana" }, SheetVariant.Ten));
        await _service.RollDiceAsync(game.Id, 0);

        var moves = await _service.GetAvailableMovesAsync(game.Id, 0);

        moves.Should().NotBeEmpty();
        moves.Should().NotContain(m => m.Column == Column.Directed);
        moves.Should().NotContain(m => m.Column == Column.Obligatory);
        moves.Should().Contain(m => m.Column == Column.Free && m.Row == Row.Yamb);
        moves.Should().OnlyContain(m => game.Columns.Contains(m.Column));
    }

    [Fact]
    public async Task StartGame_EmptyName_Throws()
    {
        var command = new StartGameCommand(GameMode.SinglePlayer, new[] { " " });
        var act = () => _service.StartGameAsync(command);
        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task StartGame_ModePlayerCountMismatch_Throws()
    {
        var command = new StartGameCommand(GameMode.SinglePlayer, new[] { "A", "B" });
        var act = () => _service.StartGameAsync(command);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetGame_UnknownId_Throws()
    {
        var act = () => _service.GetGameAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<GameNotFoundException>();
    }

    [Fact]
    public async Task RollDice_UpdatesRollNumber_AndPersists()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());

        var dto = await _service.RollDiceAsync(game.Id, 0);
        dto.RollNumber.Should().Be(1);

        var reloaded = await _service.GetGameAsync(game.Id);
        reloaded.RollNumber.Should().Be(1);
    }

    [Fact]
    public async Task RollDice_WrongPlayer_Throws()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.TwoPlayer, new[] { "A", "B" }));

        var act = () => _service.RollDiceAsync(game.Id, 1);
        await act.Should().ThrowAsync<GameException>().WithMessage("*A's turn*");
    }

    [Fact]
    public async Task ToggleHold_WrongPlayer_Throws()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.TwoPlayer, new[] { "A", "B" }));

        var act = () => _service.ToggleHoldAsync(game.Id, 1, 0);
        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task Announce_RequiresFirstRollPhase()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());
        await _service.RollDiceAsync(game.Id, 0);
        await _service.RollDiceAsync(game.Id, 0);

        var act = () => _service.AnnounceAsync(game.Id, 0, Row.Yamb);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RollDice_ThirdRollWithAnnouncement_KeepsAnnouncementForManualWrite()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.TwoPlayer, new[] { "A", "B" }));

        await _service.RollDiceAsync(game.Id, 0);
        await _service.AnnounceAsync(game.Id, 0, Row.Ones);
        await _service.RollDiceAsync(game.Id, 0);
        var dto = await _service.RollDiceAsync(game.Id, 0);

        dto.Players[0].Scoresheet.Cells[Column.Announcement][Row.Ones].Should().BeNull();
        dto.CurrentPlayerIndex.Should().Be(0);
        dto.RollNumber.Should().Be(3);
        dto.AnnouncedRow.Should().Be(Row.Ones);
    }

    [Fact]
    public async Task WriteScore_AfterThirdRollWithAnnouncement_WritesAndAdvances()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.TwoPlayer, new[] { "A", "B" }));

        await _service.RollDiceAsync(game.Id, 0);
        await _service.AnnounceAsync(game.Id, 0, Row.Ones);
        await _service.RollDiceAsync(game.Id, 0);
        await _service.RollDiceAsync(game.Id, 0);

        var dto = await _service.WriteScoreAsync(game.Id, 0, Column.Announcement, Row.Ones);

        dto.Players[0].Scoresheet.Cells[Column.Announcement][Row.Ones].Should().HaveValue();
        dto.CurrentPlayerIndex.Should().Be(1);
        dto.RollNumber.Should().Be(0);
        dto.AnnouncedRow.Should().BeNull();
    }

    [Fact]
    public async Task RollDice_ThirdRollWithoutAnnouncement_DoesNotWrite()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());

        await _service.RollDiceAsync(game.Id, 0);
        await _service.RollDiceAsync(game.Id, 0);
        var dto = await _service.RollDiceAsync(game.Id, 0);

        dto.Players[0].Scoresheet.FilledCells.Should().Be(0);
        dto.RollNumber.Should().Be(3);
    }

    [Fact]
    public async Task WriteScore_AdvancesPlayer()
    {
        var game = await _service.StartGameAsync(
            new StartGameCommand(GameMode.TwoPlayer, new[] { "A", "B" }));
        await _service.RollDiceAsync(game.Id, 0);

        var dto = await _service.WriteScoreAsync(game.Id, 0, Column.Downward, Row.Ones);

        dto.CurrentPlayerIndex.Should().Be(1);
        dto.RollNumber.Should().Be(0);
        dto.Players[0].Scoresheet.Cells[Column.Downward][Row.Ones].Should().HaveValue();
    }

    [Fact]
    public async Task WriteScore_GameOver_SubmitsToLeaderboard()
    {
        var gameId = Guid.NewGuid();
        var cells = AllCellsZero().Where(c => !(c.Column == Column.Downward && c.Row == Row.Ones)).ToList();
        await _gameRepository.SaveAsync(BuildRestoredGame(gameId, GameMode.SinglePlayer, "P1", cells));

        var dto = await _service.WriteScoreAsync(gameId, 0, Column.Downward, Row.Ones);

        dto.IsGameOver.Should().BeTrue();
        _leaderboardRepository.Entries.Should().ContainSingle()
            .Which.Participants.Should().Be("P1");
    }

    [Fact]
    public async Task WriteScore_GameOver_SubmitsEachPlayerSeparately()
    {
        var gameId = Guid.NewGuid();
        var cells = AllCellsZero()
            .Where(c => !(c.Column == Column.Downward && c.Row == Row.Ones))
            .ToList();
        var snapshot = new GameSnapshot(
            gameId, GameMode.TwoPlayer, 0,
            new[]
            {
                new PlayerSnapshot("Ana", cells),
                new PlayerSnapshot("Bane", cells)
            },
            new[] { 6, 6, 6, 6, 6, 1 }.Select(v => new Die(v)).ToList(),
            1, null, true);
        await _gameRepository.SaveAsync(YambGame.FromSnapshot(snapshot));

        var first = await _service.WriteScoreAsync(gameId, 0, Column.Downward, Row.Ones);
        first.CurrentPlayerIndex.Should().Be(1);
        await _service.RollDiceAsync(gameId, 1);
        await _service.WriteScoreAsync(gameId, 1, Column.Downward, Row.Ones);

        _leaderboardRepository.Entries.Select(e => e.Participants)
            .Should().BeEquivalentTo(new[] { "Ana", "Bane" });
    }

    [Fact]
    public async Task WriteScore_BotGameOver_SubmitsOnlyHumanToLeaderboard()
    {
        var gameId = Guid.NewGuid();
        var cells = AllCellsZero()
            .Where(c => !(c.Column == Column.Downward && c.Row == Row.Ones))
            .ToList();
        var snapshot = new GameSnapshot(
            gameId, GameMode.VersusComputer, 0,
            new[]
            {
                new PlayerSnapshot("Human", cells),
                new PlayerSnapshot("Bot", cells)
            },
            new[] { 6, 6, 6, 6, 6, 1 }.Select(v => new Die(v)).ToList(),
            1, null, true);
        await _gameRepository.SaveAsync(YambGame.FromSnapshot(snapshot));

        await _service.WriteScoreAsync(gameId, 0, Column.Downward, Row.Ones);
        await _service.RollDiceAsync(gameId, 1);
        await _service.WriteScoreAsync(gameId, 1, Column.Downward, Row.Ones);

        _leaderboardRepository.Entries.Should().ContainSingle()
            .Which.Participants.Should().Be("Human");
    }

    [Fact]
    public async Task GetAvailableMoves_BeforeAnyRoll_IsEmpty()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());

        var moves = await _service.GetAvailableMovesAsync(game.Id, 0);

        moves.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableMoves_AfterFirstRoll_CoversAllColumns()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());
        await _service.RollDiceAsync(game.Id, 0);

        var moves = await _service.GetAvailableMovesAsync(game.Id, 0);

        moves.Should().HaveCount(32);
        moves.Should().Contain(m => m.Column == Column.Downward && m.Row == Row.Ones);
        moves.Should().Contain(m => m.Column == Column.Hand && m.Row == Row.Yamb);
        moves.Should().NotContain(m => m.Column == Column.Announcement);
    }

    [Fact]
    public async Task GetAvailableMoves_WithHeldDice_ExcludesHandColumn()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());
        await _service.RollDiceAsync(game.Id, 0);
        await _service.ToggleHoldAsync(game.Id, 0, 0);
        await _service.RollDiceAsync(game.Id, 0);

        var moves = await _service.GetAvailableMovesAsync(game.Id, 0);

        moves.Should().HaveCount(19);
        moves.Should().NotContain(m => m.Column == Column.Hand);
    }

    [Fact]
    public async Task GetAvailableMoves_AfterAnnouncement_ReturnsOnlyAnnouncedCell()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());
        await _service.RollDiceAsync(game.Id, 0);
        await _service.AnnounceAsync(game.Id, 0, Row.Yamb);

        var moves = await _service.GetAvailableMovesAsync(game.Id, 0);

        moves.Should().ContainSingle();
        moves[0].Column.Should().Be(Column.Announcement);
        moves[0].Row.Should().Be(Row.Yamb);
    }

    [Fact]
    public async Task DeleteGame_RemovesFromRepository()
    {
        var game = await _service.StartGameAsync(SinglePlayerCommand());

        await _service.DeleteGameAsync(game.Id);

        (await _gameRepository.GetByIdAsync(game.Id)).Should().BeNull();
    }

    private static IEnumerable<CellSnapshot> AllCellsZero() =>
        SheetDefinitions.DefaultColumns.SelectMany(
            column => Enum.GetValues<Row>().Select(row => new CellSnapshot(column, row, 0)));

    private static YambGame BuildRestoredGame(
        Guid id, GameMode mode, string playerName, IReadOnlyList<CellSnapshot> cells)
    {
        var snapshot = new GameSnapshot(
            id, mode, 0,
            new[] { new PlayerSnapshot(playerName, cells) },
            new[] { 6, 6, 6, 6, 6, 1 }.Select(v => new Die(v)).ToList(),
            1, null, true);
        return YambGame.FromSnapshot(snapshot);
    }
}

public class LeaderboardServiceTests
{
    private readonly FakeLeaderboardRepository _repository = new();
    private readonly LeaderboardService _service;

    public LeaderboardServiceTests()
    {
        _service = new LeaderboardService(
            _repository, Microsoft.Extensions.Options.Options.Create(new LeaderboardOptions { MaxEntries = 3 }));
    }

    [Fact]
    public async Task Submit_QualifyingScore_IsAdded()
    {
        var added = await _service.SubmitScoreAsync("Ana", 500);

        added.Should().BeTrue();
        _repository.Entries.Should().ContainSingle().Which.Score.Should().Be(500);
    }

    [Fact]
    public async Task Submit_NonQualifyingScore_IsRejected()
    {
        await _service.SubmitScoreAsync("A", 100);
        await _service.SubmitScoreAsync("B", 90);
        await _service.SubmitScoreAsync("C", 80);

        var added = await _service.SubmitScoreAsync("D", 10);

        added.Should().BeFalse();
        _repository.Entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTop_RanksByScoreDescending()
    {
        await _service.SubmitScoreAsync("A", 30);
        await _service.SubmitScoreAsync("B", 50);
        await _service.SubmitScoreAsync("C", 40);

        var top = await _service.GetTopScoresAsync();

        top.Select(e => e.Score).Should().ContainInOrder(50, 40, 30);
        top.Select(e => e.Rank).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task Submit_TrimsToMaxEntries()
    {
        await _service.SubmitScoreAsync("A", 10);
        await _service.SubmitScoreAsync("B", 20);
        await _service.SubmitScoreAsync("C", 30);
        await _service.SubmitScoreAsync("D", 40);

        _repository.Entries.Should().HaveCount(3);
        _repository.Entries.Select(e => e.Score).Should().ContainInOrder(40, 30, 20);
    }

    [Fact]
    public async Task GetTop_ClampsToMaxEntries()
    {
        for (int i = 0; i < 5; i++)
        {
            await _repository.AddAsync(new Yamb.Application.Models.LeaderboardEntry(Guid.NewGuid(), $"P{i}", i * 100, DateTime.UtcNow));
        }

        var top = await _service.GetTopScoresAsync(count: 100);

        top.Should().HaveCount(3);
    }

    [Fact]
    public async Task ClearScores_RemovesAllEntries()
    {
        await _service.SubmitScoreAsync("A", 10);
        await _service.SubmitScoreAsync("B", 20);

        await _service.ClearScoresAsync();

        _repository.Entries.Should().BeEmpty();
    }
}
