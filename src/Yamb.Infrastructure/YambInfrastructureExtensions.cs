using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yamb.Application.Interfaces;
using Yamb.Application.Options;
using Yamb.Infrastructure.Persistence;

namespace Yamb.Infrastructure;

public static class YambInfrastructureExtensions
{
    public static IServiceCollection AddYambInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("YambDb") ?? "Data Source=yamb.db";
        services.AddYambInfrastructure(connectionString);
        services.AddOptions<LeaderboardOptions>()
            .Bind(configuration.GetSection(LeaderboardOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddYambInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<YambDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<ILeaderboardRepository, EfLeaderboardRepository>();
        services.AddOptions<LeaderboardOptions>();

        return services;
    }

    public static async Task InitializeYambDatabaseAsync(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YambDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
