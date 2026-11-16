using Microsoft.Extensions.DependencyInjection;
using Yamb.Application.Interfaces;
using Yamb.Application.Services;
using Yamb.Domain.Rules;

namespace Yamb.Application;

public static class YambApplicationExtensions
{
    public static IServiceCollection AddYambApplication(this IServiceCollection services)
    {
        services.AddSingleton(GameRuleset.Default);
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddSingleton<IAnalyticsService, NoOpAnalyticsService>();
        return services;
    }
}
