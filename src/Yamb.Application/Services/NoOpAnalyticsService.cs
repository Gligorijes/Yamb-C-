using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Domain.Enums;

namespace Yamb.Application.Services;

public sealed class NoOpAnalyticsService : IAnalyticsService
{
    public Task TrackGameStartedAsync(GameMode mode) => Task.CompletedTask;

    public Task TrackGameCompletedAsync(GameMode mode, IReadOnlyList<PlayerScoreDto> finalScores) => Task.CompletedTask;

    public Task TrackHighScoreAchievedAsync(int score, string playerName) => Task.CompletedTask;

    public Task TrackColumnUsageAsync(Column column, Row row, int score) => Task.CompletedTask;
}
