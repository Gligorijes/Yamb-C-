using Yamb.Application.DTOs;
using Yamb.Domain.Aggregates;
using Yamb.Domain.Enums;

namespace Yamb.Application.Interfaces;

public interface IGameRepository
{
    Task<YambGame?> GetByIdAsync(Guid id);

    Task SaveAsync(YambGame game);

    Task DeleteAsync(Guid id);

    Task<IReadOnlyList<GameSummaryDto>> ListSummariesAsync();
}

public interface ILeaderboardRepository
{
    Task<IReadOnlyList<Models.LeaderboardEntry>> GetTopAsync(int count);

    Task AddAsync(Models.LeaderboardEntry entry);

    Task<bool> QualifiesAsync(int score, int topN);

    Task TrimToAsync(int max);

    Task ClearAsync();
}

public interface IAnalyticsService
{
    Task TrackGameStartedAsync(GameMode mode);

    Task TrackGameCompletedAsync(GameMode mode, IReadOnlyList<PlayerScoreDto> finalScores);

    Task TrackHighScoreAchievedAsync(int score, string playerName);

    Task TrackColumnUsageAsync(Column column, Row row, int score);
}
