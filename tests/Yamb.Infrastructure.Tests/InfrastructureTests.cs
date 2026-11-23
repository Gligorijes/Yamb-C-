using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;
using Yamb.Domain.Rules;
using Yamb.Domain.Snapshots;
using Yamb.Domain.ValueObjects;
using Yamb.Infrastructure.Learning;
using Yamb.Infrastructure.Persistence;

namespace Yamb.Infrastructure.Tests;

public class EfLeaderboardRepositoryTests
{
    private static YambDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<YambDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new YambDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task AddAndGetTop_OrdersByScoreDescending()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "A", 100, DateTime.UtcNow));
        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "B", 300, DateTime.UtcNow));
        await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), "C", 200, DateTime.UtcNow));

        var top = await repo.GetTopAsync(2);

        top.Select(e => e.Score).Should().ContainInOrder(300, 200);
    }

    [Fact]
    public async Task Qualifies_EmptyLeaderboard_ReturnsTrue()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        (await repo.QualifiesAsync(1, topN: 10)).Should().BeTrue();
    }

    [Fact]
    public async Task Qualifies_BelowCutoff_ReturnsFalse()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        for (int i = 0; i < 10; i++)
        {
            await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), $"P{i}", 100 + i, DateTime.UtcNow));
        }

        (await repo.QualifiesAsync(95, topN: 10)).Should().BeFalse();
        (await repo.QualifiesAsync(999, topN: 10)).Should().BeTrue();
    }

    [Fact]
    public async Task TrimTo_RemovesEntriesBeyondMax()
    {
        using var db = CreateContext();
        var repo = new EfLeaderboardRepository(db);

        for (int i = 0; i < 5; i++)
        {
            await repo.AddAsync(new LeaderboardEntry(Guid.NewGuid(), $"P{i}", i * 10, DateTime.UtcNow));
        }

        await repo.TrimToAsync(3);

        (await repo.GetTopAsync(10)).Should().HaveCount(3);
    }
}

public class EfGameRepositoryTests
{
    private static YambDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<YambDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new YambDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SaveAndLoad_PreservesGameState()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.TwoPlayer,
            new[] { new Player("Ana"), new Player("Bog") });
        game.Roll();
        game.ToggleHold(1);
        game.Announce(Row.FullHouse);

        await repo.SaveAsync(game);
        var loaded = await repo.GetByIdAsync(game.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(game.Id);
        loaded.Mode.Should().Be(GameMode.TwoPlayer);
        loaded.Players.Select(p => p.Name).Should().BeEquivalentTo("Ana", "Bog");
        loaded.RollNumber.Should().Be(1);
        loaded.AnnouncedRow.Should().Be(Row.FullHouse);
        loaded.CurrentDice.Dice.Should().BeEquivalentTo(game.CurrentDice.Dice);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesFiveDiceRules()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var rules = GameRuleset.Default with { DiceCount = 5, ScoringDiceCount = 5 };
        var game = new YambGame(
            GameMode.SinglePlayer,
            new[] { new Player("Ana", rules) },
            rules);
        game.Roll();

        await repo.SaveAsync(game);
        var loaded = await repo.GetByIdAsync(game.Id);

        loaded.Should().NotBeNull();
        loaded!.Rules.DiceCount.Should().Be(5);
        loaded.Rules.ScoringDiceCount.Should().Be(5);
        loaded.CurrentDice.Dice.Should().HaveCount(5);
    }

    [Fact]
    public async Task SaveTwice_UpdatesExistingSnapshot()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });

        await repo.SaveAsync(game);
        game.Roll();
        game.WriteScore(Column.Downward, Row.Ones);
        await repo.SaveAsync(game);

        var loaded = await repo.GetByIdAsync(game.Id);
        loaded!.RollNumber.Should().Be(0);
        loaded.Players[0].Scoresheet.GetCell(Column.Downward, Row.Ones).Should().HaveValue();
    }

    [Fact]
    public async Task WriteOnLoadedGame_PersistsCorrectly()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        game.Roll();
        await repo.SaveAsync(game);

        var loaded = await repo.GetByIdAsync(game.Id);
        loaded!.WriteScore(Column.Downward, Row.Ones);
        await repo.SaveAsync(loaded);

        var reloaded = await repo.GetByIdAsync(game.Id);
        reloaded!.Players[0].Scoresheet.GetCell(Column.Downward, Row.Ones).Should().HaveValue();
    }

    [Fact]
    public async Task ListSummaries_ReturnsSavedGames()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        await repo.SaveAsync(game);

        var summaries = await repo.ListSummariesAsync();

        summaries.Should().ContainSingle();
        summaries[0].Id.Should().Be(game.Id);
        summaries[0].PlayerNames.Should().Be("Ana");
        summaries[0].Mode.Should().Be(GameMode.SinglePlayer);
        summaries[0].ColumnCount.Should().Be(7);
        summaries[0].DiceCount.Should().Be(6);
        summaries[0].TotalCells.Should().Be(91);
        summaries[0].Players.Should().ContainSingle().Which.Name.Should().Be("Ana");
    }

    [Fact]
    public async Task Delete_RemovesSavedGame()
    {
        using var db = CreateContext();
        var repo = new EfGameRepository(db, GameRuleset.Default);
        var game = new YambGame(GameMode.SinglePlayer, new[] { new Player("Ana") });
        await repo.SaveAsync(game);

        await repo.DeleteAsync(game.Id);

        (await repo.GetByIdAsync(game.Id)).Should().BeNull();
        (await repo.ListSummariesAsync()).Should().BeEmpty();
    }
}

public class JsonBotExperienceStoreTests
{
    [Fact]
    public async Task CompleteGameAsync_PersistsDecisionReward()
    {
        string directory = Path.Combine(Path.GetTempPath(), "yamb-bot-experience-tests");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
        Guid gameId = Guid.NewGuid();
        const BotDecisionKind kind = BotDecisionKind.Move;
        const string state = "state";
        const string move = "move";

        try
        {
            var store = new JsonBotExperienceStore(path);
            store.RememberDecision(new BotDecisionRecord(gameId, kind, state, move, DateTime.UtcNow));
            await store.CompleteGameAsync(gameId, 0.8);

            var reloaded = new JsonBotExperienceStore(path);
            var value = reloaded.Get(kind, state, move);

            value.Should().NotBeNull();
            value!.Count.Should().Be(1);
            value.Mean.Should().BeApproximately(0.8, 0.0001);
            value.Games.Should().Be(1);
            reloaded.GamesCompleted.Should().Be(1);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task CompleteGameAsync_GroupsRepeatedDecisionsAndTracksTrainingGrowth()
    {
        string directory = Path.Combine(Path.GetTempPath(), "yamb-bot-experience-tests");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
        Guid firstGame = Guid.NewGuid();
        Guid secondGame = Guid.NewGuid();
        const BotDecisionKind kind = BotDecisionKind.Move;
        const string state = "state";
        const string move = "move";

        try
        {
            var store = new JsonBotExperienceStore(path);
            store.RememberDecision(new BotDecisionRecord(firstGame, kind, state, move, DateTime.UtcNow));
            store.RememberDecision(new BotDecisionRecord(firstGame, kind, state, move, DateTime.UtcNow));
            await store.CompleteGameAsync(firstGame, 1);

            store.RememberDecision(new BotDecisionRecord(secondGame, kind, state, move, DateTime.UtcNow));
            await store.CompleteGameAsync(secondGame, -1);

            var reloaded = new JsonBotExperienceStore(path);
            var value = reloaded.Get(kind, state, move);

            value.Should().NotBeNull();
            value!.Count.Should().Be(3);
            value.Games.Should().Be(2);
            value.Mean.Should().BeApproximately(1.0 / 3.0, 0.0001);
            reloaded.GamesCompleted.Should().Be(2);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
