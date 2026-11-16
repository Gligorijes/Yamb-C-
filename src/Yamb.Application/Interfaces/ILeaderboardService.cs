using Yamb.Application.DTOs;

namespace Yamb.Application.Interfaces;

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetTopScoresAsync(int? count = null);

    Task<bool> IsHighScoreAsync(int score);

    Task<bool> SubmitScoreAsync(string participants, int score);

    Task ClearScoresAsync();
}
