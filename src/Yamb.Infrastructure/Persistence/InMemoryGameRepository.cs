using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Domain.Aggregates;

namespace Yamb.Infrastructure.Persistence;

public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly Dictionary<Guid, YambGame> _games = new();
    private readonly Dictionary<Guid, DateTime> _savedAt = new();

    public Task<YambGame?> GetByIdAsync(Guid id) =>
        Task.FromResult(_games.TryGetValue(id, out var game) ? game : null);

    public Task SaveAsync(YambGame game)
    {
        _games[game.Id] = game;
        _savedAt[game.Id] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _games.Remove(id);
        _savedAt.Remove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameSummaryDto>> ListSummariesAsync() =>
        Task.FromResult<IReadOnlyList<GameSummaryDto>>(
            _games.Values
                .Select(g => new GameSummaryDto(
                    g.Id,
                    g.Mode,
                    string.Join(" vs ", g.Players.Select(p => p.Name)),
                    _savedAt.GetValueOrDefault(g.Id)))
                .OrderByDescending(s => s.SavedAt)
                .ToList());
}
