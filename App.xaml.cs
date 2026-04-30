using AppTimeTracker.Core.Trackers;
using AppTimeTracker.Data.Repositories;
using AppTimeTracker.Data.Services;
using AppTimeTracker.UI.Converters;
using AppTimeTracker.UI.ViewModels;
using AppTimeTracker.UI.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace AppTimeTracker
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;
        private System.Threading.Mutex _mutex;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfigRepository, ConfigJsonRepository>();
            services.AddSingleton<IStatsRepository, StatsJsonRepository>();

            services.AddSingleton<IIconCacheService, IconCacheService>();
            services.AddSingleton<ConfigManager>(provider =>
                new ConfigManager(
                    provider.GetRequiredService<IConfigRepository>(),
                    provider.GetRequiredService<IIconCacheService>()
                ));

            services.AddSingleton<StatsManager>();
            services.AddSingleton<ActiveProcessTracker>();

            services.AddTransient<StatsViewModel>();
            services.AddTransient<MainWindow>();
            services.AddTransient<StatsWindow>(provider =>
                new StatsWindow(
                    provider.GetRequiredService<StatsManager>(),
                    provider.GetRequiredService<ConfigManager>(),
                    provider.GetRequiredService<IIconCacheService>(),
                    provider.GetRequiredService<ActiveProcessTracker>()
                ));
            services.AddTransient<SettingsWindow>();
            services.AddTransient<ProcessNameToIconConverter>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Global\\AppTimeTracker-SingleInstanceMutex";
            bool createdNew;

            try
            {
                _mutex = new System.Threading.Mutex(true, mutexName, out createdNew);
            }
            catch (Exception)
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                MessageBox.Show("Приложение уже запущено!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                var iconCacheService = _serviceProvider.GetRequiredService<IIconCacheService>();
                var configManager = _serviceProvider.GetRequiredService<ConfigManager>();

                ProcessNameToIconConverter.SetIconCacheService(iconCacheService);
                ProcessNameToIconConverter.SetConfigManager(configManager);

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.SetResourceReference(Window.StyleProperty, typeof(Window));
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска приложения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var iconCacheService = _serviceProvider.GetService<IIconCacheService>();
            iconCacheService?.SaveCache();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}