using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Enums;
using Yamb.Domain.Exceptions;

namespace Yamb.Presentation;

public sealed class GameShell
{
    private readonly IGameService _gameService;
    private readonly ILeaderboardService _leaderboardService;

    public GameShell(IGameService gameService, ILeaderboardService leaderboardService)
    {
        _gameService = gameService;
        _leaderboardService = leaderboardService;
    }

    public async Task RunAsync()
    {
        Info("YAMB - the dice game of strategy and luck");
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("====== MAIN MENU ======");
            Console.WriteLine(" 1) New game");
            Console.WriteLine(" 2) Resume saved game");
            Console.WriteLine(" 3) Leaderboard");
            Console.WriteLine(" 4) Rules");
            Console.WriteLine(" 0) Quit");
            var choice = Prompt("> ");

            try
            {
                switch (choice)
                {
                    case "1": await StartGameFlowAsync(); break;
                    case "2": await ResumeGameFlowAsync(); break;
                    case "3": await ShowLeaderboardAsync(); break;
                    case "4": ConsoleRenderer.RenderRules(); WaitForAnyKey(); break;
                    case null:
                    case "0":
                    case "q":
                        return;
                    default:
                        Info("Unknown option.");
                        break;
                }
            }
            catch (Exception ex) when (ex is DomainException or GameException)
            {
                Error(ex.Message);
            }
        }
    }

    private async Task StartGameFlowAsync()
    {
        Console.WriteLine("Mode: 1) Single player  2) Two players");
        var modeInput = Prompt("> ");
        var mode = modeInput == "2" ? GameMode.TwoPlayer : GameMode.SinglePlayer;
        var playerCount = mode == GameMode.TwoPlayer ? 2 : 1;

        var names = new List<string>();
        for (int i = 1; i <= playerCount; i++)
        {
            var name = Prompt($"Player {i} name (default 'Player {i}'): ");
            names.Add(string.IsNullOrWhiteSpace(name) ? $"Player {i}" : name);
        }

        var game = await _gameService.StartGameAsync(new StartGameCommand(mode, names));
        Info($"Game started: {game.Id}");
        await PlayGameLoopAsync(game.Id);
    }

    private async Task ResumeGameFlowAsync()
    {
        var games = await _gameService.ListGamesAsync();
        if (games.Count == 0)
        {
            Info("No saved games found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Saved games:");
        for (int i = 0; i < games.Count; i++)
        {
            Console.WriteLine($" {i + 1,2}) {games[i].Mode,-12} {games[i].PlayerNames,-25} saved {games[i].SavedAt:yyyy-MM-dd HH:mm}");
        }

        var pick = Prompt("Game # (blank = cancel): ");
        if (!int.TryParse(pick, out int index) || index < 1 || index > games.Count) return;

        var game = await _gameService.GetGameAsync(games[index - 1].Id);
        await PlayGameLoopAsync(game.Id);
    }

    private async Task ShowLeaderboardAsync(int count = 10)
    {
        var entries = await _leaderboardService.GetTopScoresAsync(count);
        Console.WriteLine();
        Console.WriteLine($"TOP {entries.Count} SCORES");
        if (entries.Count == 0)
        {
            Info("The leaderboard is empty - go set a score!");
            return;
        }
        foreach (var entry in entries)
        {
            Console.WriteLine($" {entry.Rank,3}. {entry.Participants,-20} {entry.Score,5}   {entry.AchievedAt:yyyy-MM-dd}");
        }
    }

    private async Task PlayGameLoopAsync(Guid gameId)
    {
        while (true)
        {
            var game = await _gameService.GetGameAsync(gameId);

            if (game.IsGameOver)
            {
                await RenderGameOverAsync(game);
                return;
            }

            ConsoleRenderer.Render(game);
            var command = Prompt($"{game.Players[game.CurrentPlayerIndex].Name}> ");
            if (command is null) return;

            try
            {
                if (await HandleCommandAsync(gameId, game, command.Trim().ToLowerInvariant())) return;
            }
            catch (Exception ex) when (ex is DomainException or GameException)
            {
                Error(ex.Message);
            }
        }
    }

    private async Task<bool> HandleCommandAsync(Guid gameId, GameDto game, string command)
    {
        if (command.Length == 0) return false;

        if (command is "r" or "roll")
        {
            await _gameService.RollDiceAsync(gameId, game.CurrentPlayerIndex);
            return false;
        }

        if (command is "w" or "write")
        {
            await WriteScoreFlowAsync(gameId, game);
            return false;
        }

        if (command is "a" or "announce")
        {
            await AnnounceFlowAsync(gameId, game);
            return false;
        }

        if (command is "?" or "help")
        {
            ConsoleRenderer.RenderRules();
            WaitForAnyKey();
            return false;
        }

        if (command is "q" or "quit" or "x" or "exit")
        {
            Info("Game saved. Returning to menu.");
            return true;
        }

        if ((command.StartsWith('h') || command.StartsWith("hold")) && command.Length > 1)
        {
            var numberPart = command.StartsWith("hold") ? command[4..] : command[1..];
            if (int.TryParse(numberPart.Trim(), out int dieIndex) && dieIndex >= 1 && dieIndex <= game.Dice.Dice.Count)
            {
                await _gameService.ToggleHoldAsync(gameId, game.CurrentPlayerIndex, dieIndex - 1);
                return false;
            }
            Error($"Hold expects a die number 1..{game.Dice.Dice.Count}, e.g. h3.");
            return false;
        }

        Error("Unknown command. Type ? for help.");
        return false;
    }

    private async Task WriteScoreFlowAsync(Guid gameId, GameDto game)
    {
        var moves = await _gameService.GetAvailableMovesAsync(gameId, game.CurrentPlayerIndex);
        if (moves.Count == 0)
        {
            Info("No writable cells - roll first.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Writable cells:");
        for (int i = 0; i < moves.Count; i++)
        {
            Console.WriteLine($" {i + 1,2}) {ConsoleRenderer.ColumnLabel(moves[i].Column)} {ConsoleRenderer.RowLabel(moves[i].Row),-3} -> {moves[i].PreviewScore}");
        }

        var pick = Prompt("Cell # (blank = cancel): ");
        if (!int.TryParse(pick, out int index) || index < 1 || index > moves.Count) return;

        var move = moves[index - 1];
        await _gameService.WriteScoreAsync(gameId, game.CurrentPlayerIndex, move.Column, move.Row);
    }

    private async Task AnnounceFlowAsync(Guid gameId, GameDto game)
    {
        if (game.RollNumber != 1)
        {
            Info("You can only announce right after the first roll of your turn.");
            return;
        }

        var cells = game.Players[game.CurrentPlayerIndex].Scoresheet.Cells[Column.Announcement];
        var emptyRows = Enum.GetValues<Row>()
            .Where(r => !cells.TryGetValue(r, out var v) || !v.HasValue)
            .ToList();

        if (emptyRows.Count == 0)
        {
            Info("The Announcement column is full.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Announce a row (you must write it in column A this turn):");
        for (int i = 0; i < emptyRows.Count; i++)
        {
            Console.WriteLine($" {i + 1,2}) {ConsoleRenderer.RowLabel(emptyRows[i])}");
        }

        var pick = Prompt("Row # (blank = cancel): ");
        if (!int.TryParse(pick, out int index) || index < 1 || index > emptyRows.Count) return;

        await _gameService.AnnounceAsync(gameId, game.CurrentPlayerIndex, emptyRows[index - 1]);
        Info($"Announced {ConsoleRenderer.RowLabel(emptyRows[index - 1])}. Good luck!");
    }

    private async Task RenderGameOverAsync(GameDto game)
    {
        ConsoleRenderer.SafeClear();
        Console.WriteLine("========== GAME OVER ==========");
        foreach (var player in game.Players)
        {
            Console.WriteLine($"  {player.Name}: {player.Scoresheet.FinalScore ?? 0} points");
        }

        var resultText = game.Result switch
        {
            GameResult.PlayerOneWins => $"{game.Players[0].Name} wins!",
            GameResult.PlayerTwoWins => $"{game.Players[1].Name} wins!",
            GameResult.Draw => "It's a draw!",
            _ => $"Final score: {game.Players[0].Scoresheet.FinalScore ?? 0} points"
        };
        Console.WriteLine($"  >>> {resultText} <<<");
        Console.WriteLine();

        await _gameService.DeleteGameAsync(game.Id);
        await ShowLeaderboardAsync();
    }

    private static string? Prompt(string label)
    {
        Console.Write(label);
        return Console.ReadLine();
    }

    private static void WaitForAnyKey()
    {
        Console.WriteLine();
        Console.Write("Press Enter to continue...");
        Console.ReadLine();
    }

    private static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
