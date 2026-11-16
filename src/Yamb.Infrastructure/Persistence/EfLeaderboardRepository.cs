using Microsoft.EntityFrameworkCore;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;

namespace Yamb.Infrastructure.Persistence;

public sealed class EfLeaderboardRepository : ILeaderboardRepository
{
    private readonly YambDbContext _db;

    public EfLeaderboardRepository(YambDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int count) =>
        await _db.LeaderboardEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.AchievedAt)
            .Take(count)
            .Select(e => new LeaderboardEntry(e.Id, e.Participants, e.Score, e.AchievedAt))
            .ToListAsync();

    public async Task<bool> QualifiesAsync(int score, int topN)
    {
        int count = await _db.LeaderboardEntries.CountAsync();
        if (count < topN) return true;

        int cutoff = await _db.LeaderboardEntries
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.AchievedAt)
            .Skip(topN - 1)
            .Select(e => e.Score)
            .FirstOrDefaultAsync();

        return score > cutoff;
    }

    public async Task AddAsync(LeaderboardEntry entry)
    {
        _db.LeaderboardEntries.Add(new LeaderboardEntryEntity
        {
            Id = entry.Id,
            Participants = entry.Participants,
            Score = entry.Score,
            AchievedAt = entry.AchievedAt
        });
        await _db.SaveChangesAsync();
    }

    public async Task TrimToAsync(int max)
    {
        var toRemove = await _db.LeaderboardEntries
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.AchievedAt)
            .Skip(max)
            .ToListAsync();

        if (toRemove.Count == 0) return;

        _db.LeaderboardEntries.RemoveRange(toRemove);
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        await _db.LeaderboardEntries.ExecuteDeleteAsync();
    }
}
