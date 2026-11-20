using Yamb.Application.Interfaces;
using Yamb.Application.Models;

namespace Yamb.Application.Services;

public sealed class NullBotExperienceStore : IBotExperienceStore
{
    public static readonly NullBotExperienceStore Instance = new();

    private NullBotExperienceStore()
    {
    }

    public BotExperienceValue? Get(BotDecisionKind kind, string stateKey, string moveKey) => null;

    public void RememberDecision(BotDecisionRecord decision)
    {
    }

    public Task CompleteGameAsync(Guid gameId, double reward) => Task.CompletedTask;
}
