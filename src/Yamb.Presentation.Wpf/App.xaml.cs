using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Yamb.Application;
using Yamb.Infrastructure;

namespace Yamb.Presentation.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _provider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            WpfSettings.Load();
            Localization.Apply(WpfSettings.Language);
            ThemeManager.Apply(WpfSettings.Theme);

            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Yamb");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "yamb.db");

            var services = new ServiceCollection();
            services.AddYambApplication();
            services.AddYambInfrastructure($"Data Source={dbPath}");
            services.AddSingleton<MainWindow>();
            _provider = services.BuildServiceProvider();

            await _provider.InitializeYambDatabaseAsync();

            var mainWindow = _provider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Yamb failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WpfSettings.Save();
        _provider?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Yamb", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
