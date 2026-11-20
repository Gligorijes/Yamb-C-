using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;
using Yamb.Domain.Services;

namespace Yamb.Application.Services;

public sealed class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IAnalyticsService _analytics;
    private readonly GameRuleset _ruleset;

    public GameService(
        IGameRepository gameRepository,
        ILeaderboardService leaderboardService,
        IAnalyticsService analytics,
        GameRuleset ruleset)
    {
        _gameRepository = gameRepository;
        _leaderboardService = leaderboardService;
        _analytics = analytics;
        _ruleset = ruleset;
    }

    public async Task<GameDto> StartGameAsync(StartGameCommand command)
    {
        if (command.PlayerNames is null || command.PlayerNames.Count == 0)
            throw new GameException("At least one player name is required.");
        if (command.PlayerNames.Any(string.IsNullOrWhiteSpace))
            throw new GameException("Player names cannot be empty.");

        var columns = SheetDefinitions.GetColumns(command.Variant);
        var diceCount = Math.Clamp(command.DiceCount, 5, 6);
        var ruleset = _ruleset with
        {
            DiceCount = diceCount,
            ScoringDiceCount = Math.Min(_ruleset.ScoringDiceCount, diceCount)
        };
        var players = command.PlayerNames
            .Select(name => new Player(name, ruleset, columns))
            .ToList();
        var game = new YambGame(command.Mode, players, ruleset, columns: columns);

        await _gameRepository.SaveAsync(game);
        await _analytics.TrackGameStartedAsync(command.Mode);

        return ToDto(game);
    }

    public async Task<GameDto> GetGameAsync(Guid gameId) => ToDto(await LoadGameAsync(gameId));

    public Task<IReadOnlyList<GameSummaryDto>> ListGamesAsync() => _gameRepository.ListSummariesAsync();

    public Task DeleteGameAsync(Guid gameId) => _gameRepository.DeleteAsync(gameId);

    public async Task<GameDto> RollDiceAsync(Guid gameId, int playerIndex)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);

        game.Roll();

        await _gameRepository.SaveAsync(game);

        return ToDto(game);
    }

    public async Task<GameDto> ToggleHoldAsync(Guid gameId, int playerIndex, int dieIndex)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);
        game.ToggleHold(dieIndex);
        await _gameRepository.SaveAsync(game);
        return ToDto(game);
    }

    public async Task<GameDto> AnnounceAsync(Guid gameId, int playerIndex, Row row)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);
        game.Announce(row);
        await _gameRepository.SaveAsync(game);
        return ToDto(game);
    }

    public async Task<GameDto> WriteScoreAsync(Guid gameId, int playerIndex, Column column, Row row)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);

        var player = game.Players[playerIndex];
        game.WriteScore(column, row);

        var score = player.Scoresheet.GetCell(column, row) ?? 0;
        await _analytics.TrackColumnUsageAsync(column, row, score);
        await _gameRepository.SaveAsync(game);
        await HandleCompletedGameAsync(game);

        return ToDto(game);
    }

    private async Task HandleCompletedGameAsync(YambGame game)
    {
        if (!game.IsGameOver) return;

        var finalScores = game.Players
            .Select(p => new PlayerScoreDto(p.Name, p.Scoresheet.FinalScore ?? 0))
            .ToList();
        await _analytics.TrackGameCompletedAsync(game.Mode, finalScores);

        var leaderboardScores = game.Mode == GameMode.VersusComputer
            ? finalScores.Take(1).ToList()
            : finalScores;

        bool highScore = false;
        foreach (var player in leaderboardScores)
        {
            if (await _leaderboardService.SubmitScoreAsync(player.Name, player.Score))
                highScore = true;
        }

        if (highScore)
        {
            var bestScore = leaderboardScores.Max(s => s.Score);
            await _analytics.TrackHighScoreAchievedAsync(
                bestScore, string.Join(", ", leaderboardScores.Select(s => s.Name)));
        }
    }

    public async Task<IReadOnlyList<AvailableMoveDto>> GetAvailableMovesAsync(Guid gameId, int playerIndex)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);

        var moves = new List<AvailableMoveDto>();
        if (game.RollNumber == 0) return moves;

        if (game.AnnouncedRow is not null)
        {
            moves.Add(new AvailableMoveDto(
                Column.Announcement, game.AnnouncedRow.Value, game.PreviewScore(game.AnnouncedRow.Value)));
            return moves;
        }

        var context = new TurnContext(
            game.Phase,
            null,
            game.AllDiceThrownThisRoll,
            game.DirectedRow,
            game.ObligatoryUnlocked);
        foreach (Column column in game.Columns)
        {
            var cells = game.CurrentPlayer.Scoresheet.GetColumnCells(column);
            foreach (var row in ColumnValidator.GetAvailableRows(column, cells, context))
            {
                int previewScore = game.PreviewScore(row);
                if (column == Column.Maximum && previewScore != game.GetMaximumScore(row))
                    continue;

                moves.Add(new AvailableMoveDto(column, row, previewScore));
            }
        }

        return moves;
    }

    public async Task<IReadOnlyList<AvailableMoveDto>> GetAnnouncementCandidatesAsync(
        Guid gameId,
        int playerIndex)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);

        if (game.RollNumber == 0 || game.AnnouncedRow.HasValue ||
            !game.Columns.Contains(Column.Announcement))
            return Array.Empty<AvailableMoveDto>();

        var cells = game.CurrentPlayer.Scoresheet.GetColumnCells(Column.Announcement);
        return Enum.GetValues<Row>()
            .Where(row => !cells.TryGetValue(row, out var value) || !value.HasValue)
            .Select(row => new AvailableMoveDto(Column.Announcement, row, game.PreviewScore(row)))
            .ToList();
    }

    public async Task<int> PreviewScoreAsync(Guid gameId, int playerIndex, Row row)
    {
        var game = await LoadGameAsync(gameId);
        EnsureActingPlayer(game, playerIndex);
        return game.PreviewScore(row);
    }

    private async Task<YambGame> LoadGameAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        return game ?? throw new GameNotFoundException(gameId);
    }

    private static void EnsureActingPlayer(YambGame game, int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= game.Players.Count)
            throw new GameException($"Invalid player index {playerIndex}.");
        if (playerIndex != game.CurrentPlayerIndex)
            throw new GameException($"It is {game.CurrentPlayer.Name}'s turn.");
    }

    private static GameDto ToDto(YambGame game) => new(
        game.Id,
        game.Mode,
        game.Columns,
        game.Players.Select(p => new PlayerDto(p.Name, ToSheetDto(p.Scoresheet))).ToList(),
        game.CurrentPlayerIndex,
        new DiceSetDto(game.CurrentDice.Dice.Select(d => new DieDto(d.Value, d.IsHeld)).ToList()),
        game.RollNumber,
        game.AnnouncedRow,
        game.AllDiceThrownThisRoll,
        game.IsGameOver,
        game.GetResult());

    private static ScoresheetDto ToSheetDto(Scoresheet sheet)
    {
        var cells = sheet.GetAllCells();
        var bonuses = sheet.Columns.ToDictionary(
            column => column,
            column => new BonusesDto(
                sheet.GetBonus(column, BonusType.NumbersBonus),
                sheet.GetBonus(column, BonusType.MaxMinBonus),
                sheet.GetBonus(column, BonusType.CombinationsBonus)));
        var rowTotals = Enum.GetValues<Row>().ToDictionary(row => row, sheet.GetRowTotal);
        var bonusTotals = new BonusesDto(
            sheet.GetBonusTotal(BonusType.NumbersBonus),
            sheet.GetBonusTotal(BonusType.MaxMinBonus),
            sheet.GetBonusTotal(BonusType.CombinationsBonus));
        var columnTotals = sheet.Columns.ToDictionary(column => column, sheet.GetColumnTotal);
        return new ScoresheetDto(
            cells,
            bonuses,
            rowTotals,
            bonusTotals,
            columnTotals,
            sheet.CurrentScore,
            sheet.FilledCellCount,
            sheet.TotalCellCount,
            sheet.FinalScore);
    }
}
