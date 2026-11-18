using Yamb.Application.DTOs;
using Yamb.Domain.Enums;

namespace Yamb.Application.Interfaces;

public interface IBotAdvisor
{
    AvailableMoveDto? ChooseMove(GameDto game, IReadOnlyList<AvailableMoveDto> moves);

    IReadOnlyList<int> ChooseDiceToHold(GameDto game, IReadOnlyList<AvailableMoveDto> moves);

    Row? ChooseAnnouncement(GameDto game, IReadOnlyList<AvailableMoveDto> moves);
}
