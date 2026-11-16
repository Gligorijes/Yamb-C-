using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yamb.Application;
using Yamb.Infrastructure;
using Yamb.Presentation;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

var services = new ServiceCollection();
services.AddYambApplication();
services.AddYambInfrastructure(configuration);
services.AddSingleton<GameShell>();

await using var provider = services.BuildServiceProvider();
await provider.InitializeYambDatabaseAsync();
await provider.GetRequiredService<GameShell>().RunAsync();

return 0;
