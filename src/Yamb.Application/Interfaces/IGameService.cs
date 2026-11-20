using Yamb.Application.DTOs;
using Yamb.Domain.Enums;

namespace Yamb.Application.Interfaces;

public interface IGameService
{
    Task<GameDto> StartGameAsync(StartGameCommand command);

    Task<GameDto> GetGameAsync(Guid gameId);

    Task<IReadOnlyList<GameSummaryDto>> ListGamesAsync();

    Task DeleteGameAsync(Guid gameId);

    Task<GameDto> RollDiceAsync(Guid gameId, int playerIndex);

    Task<GameDto> ToggleHoldAsync(Guid gameId, int playerIndex, int dieIndex);

    Task<GameDto> AnnounceAsync(Guid gameId, int playerIndex, Row row);

    Task<GameDto> WriteScoreAsync(Guid gameId, int playerIndex, Column column, Row row);

    Task<IReadOnlyList<AvailableMoveDto>> GetAvailableMovesAsync(Guid gameId, int playerIndex);

    Task<IReadOnlyList<AvailableMoveDto>> GetAnnouncementCandidatesAsync(Guid gameId, int playerIndex);

    Task<int> PreviewScoreAsync(Guid gameId, int playerIndex, Row row);
}
