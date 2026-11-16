using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Rules;
using Yamb.Domain.Snapshots;

namespace Yamb.Infrastructure.Persistence;

public sealed class EfGameRepository : IGameRepository
{
    private readonly YambDbContext _db;
    private readonly GameRuleset _rules;

    public EfGameRepository(YambDbContext db, GameRuleset rules)
    {
        _db = db;
        _rules = rules;
    }

    public async Task<YambGame?> GetByIdAsync(Guid id)
    {
        var entity = await _db.SavedGames.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (entity is null) return null;

        var snapshot = JsonSerializer.Deserialize<GameSnapshot>(entity.SnapshotJson);
        return snapshot is null ? null : YambGame.FromSnapshot(snapshot, _rules);
    }

    public async Task SaveAsync(YambGame game)
    {
        var json = JsonSerializer.Serialize(game.ToSnapshot());

        var entity = await _db.SavedGames.FirstOrDefaultAsync(g => g.Id == game.Id);
        if (entity is null)
        {
            _db.SavedGames.Add(new SavedGameEntity
            {
                Id = game.Id,
                Mode = game.Mode,
                SnapshotJson = json,
                PlayerNames = string.Join(" vs ", game.Players.Select(p => p.Name)),
                SavedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.Mode = game.Mode;
            entity.SnapshotJson = json;
            entity.PlayerNames = string.Join(" vs ", game.Players.Select(p => p.Name));
            entity.SavedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _db.SavedGames.FirstOrDefaultAsync(g => g.Id == id);
        if (entity is null) return;

        _db.SavedGames.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<GameSummaryDto>> ListSummariesAsync() =>
        await _db.SavedGames
            .AsNoTracking()
            .OrderByDescending(g => g.SavedAt)
            .Select(g => new GameSummaryDto(g.Id, g.Mode, g.PlayerNames, g.SavedAt))
            .ToListAsync();
}
