using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Domain.Aggregates;

namespace Yamb.Application.Tests;

internal sealed class FakeGameRepository : IGameRepository
{
    private readonly Dictionary<Guid, YambGame> _games = new();

    public Task<YambGame?> GetByIdAsync(Guid id) =>
        Task.FromResult(_games.TryGetValue(id, out var game) ? game : null);

    public Task SaveAsync(YambGame game)
    {
        _games[game.Id] = game;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _games.Remove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameSummaryDto>> ListSummariesAsync() =>
        Task.FromResult<IReadOnlyList<GameSummaryDto>>(
            _games.Values
                .Select(g => new GameSummaryDto(g.Id, g.Mode, string.Join(" vs ", g.Players.Select(p => p.Name)), DateTime.UtcNow))
                .ToList());
}

internal sealed class FakeLeaderboardRepository : ILeaderboardRepository
{
    private readonly List<LeaderboardEntry> _entries = new();

    public IReadOnlyList<LeaderboardEntry> Entries => _entries.OrderByDescending(e => e.Score).ToList();

    public Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int count) =>
        Task.FromResult<IReadOnlyList<LeaderboardEntry>>(
            _entries.OrderByDescending(e => e.Score).ThenBy(e => e.AchievedAt).Take(count).ToList());

    public Task AddAsync(LeaderboardEntry entry)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<bool> QualifiesAsync(int score, int topN)
    {
        if (_entries.Count < topN) return Task.FromResult(true);
        int cutoff = _entries.OrderByDescending(e => e.Score).ThenBy(e => e.AchievedAt).Skip(topN - 1).First().Score;
        return Task.FromResult(score > cutoff);
    }

    public Task TrimToAsync(int max)
    {
        var toRemove = _entries.OrderByDescending(e => e.Score).ThenBy(e => e.AchievedAt).Skip(max).ToList();
        foreach (var entry in toRemove)
        {
            _entries.Remove(entry);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _entries.Clear();
        return Task.CompletedTask;
    }
}
