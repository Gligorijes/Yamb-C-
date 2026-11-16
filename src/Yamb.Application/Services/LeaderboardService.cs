using Microsoft.Extensions.Options;
using Yamb.Application.DTOs;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Application.Options;

namespace Yamb.Application.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly ILeaderboardRepository _repository;
    private readonly LeaderboardOptions _options;

    public LeaderboardService(ILeaderboardRepository repository, IOptions<LeaderboardOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetTopScoresAsync(int? count = null)
    {
        int requested = Math.Clamp(count ?? _options.MaxEntries, 1, _options.MaxEntries);
        var entries = await _repository.GetTopAsync(requested);
        return entries
            .Select((entry, index) => new LeaderboardEntryDto(index + 1, entry.Participants, entry.Score, entry.AchievedAt))
            .ToList();
    }

    public Task<bool> IsHighScoreAsync(int score) =>
        _repository.QualifiesAsync(score, _options.MaxEntries);

    public async Task<bool> SubmitScoreAsync(string participants, int score)
    {
        if (string.IsNullOrWhiteSpace(participants))
            throw new ArgumentException("Participants cannot be empty.", nameof(participants));

        if (!await _repository.QualifiesAsync(score, _options.MaxEntries)) return false;

        var trimmed = participants.Trim();
        if (trimmed.Length > 50) trimmed = trimmed[..50];

        await _repository.AddAsync(new LeaderboardEntry(Guid.NewGuid(), trimmed, score, DateTime.UtcNow));
        await _repository.TrimToAsync(_options.MaxEntries);
        return true;
    }

    public Task ClearScoresAsync() => _repository.ClearAsync();
}
