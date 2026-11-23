using Yamb.Application.Models;

namespace Yamb.Application.Interfaces;

public interface IBotExperienceStore
{
    int GamesCompleted { get; }

    BotExperienceValue? Get(BotDecisionKind kind, string stateKey, string moveKey);

    void RememberDecision(BotDecisionRecord decision);

    Task CompleteGameAsync(Guid gameId, double reward);
}
