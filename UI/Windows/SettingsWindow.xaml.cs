using AppTimeTracker.Core.Common;
using AppTimeTracker.Core.Helpers;
using AppTimeTracker.Data.Services;
using AppTimeTracker.UI.Windows.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AppTimeTracker.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigManager _configManager;
        private readonly StatsManager _statsManager;

        public SettingsWindow(ConfigManager configManager, StatsManager statsManager)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));

            InitializeComponent();
            StateChanged += Window_StateChanged;
            LoadSettings();
            LoadProcesses();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeRestoreButton.Content = "□";
                MaximizeRestoreButton.ToolTip = "Развернуть";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeRestoreButton.Content = "❐";
                MaximizeRestoreButton.ToolTip = "Восстановить";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    MaximizeRestoreButton_Click(sender, e);
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeRestoreButton.Content = "❐";
                MaximizeRestoreButton.ToolTip = "Восстановить";
            }
            else
            {
                MaximizeRestoreButton.Content = "□";
                MaximizeRestoreButton.ToolTip = "Развернуть";
            }
        }

        private void LoadSettings()
        {
            try
            {
                var config = _configManager.GetConfig();
                StartupCheckBox.IsChecked = config.RunAtStartup;

                StartupStatus.Text = RegistryHelper.IsStartupEnabled(Constants.AppRegistryKey)
                    ? "Включено"
                    : "Отключено";

                BlacklistBox.ItemsSource = config.BlacklistedApps;
            }
            catch (Exception ex)
            {
                var errorDialog = new InfoDialogWindow(this, "Ошибка",
                    $"Ошибка загрузки настроек: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        private void LoadProcesses()
        {
            try
            {
                var processes = ProcessHelper.GetRunningProcesses()
                    .Where(p => !_configManager.IsBlacklisted(p))
                    .OrderBy(p => p)
                    .ToList();

                ProcessCombo.ItemsSource = processes;
                if (processes.Any())
                    ProcessCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                var errorDialog = new InfoDialogWindow(this, "Ошибка",
                    $"Ошибка загрузки процессов: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _configManager.UpdateConfig(config =>
                {
                    config.RunAtStartup = StartupCheckBox.IsChecked ?? false;

                    var executablePath = ShortcutHelper.GetExecutablePath();

                    if (config.RunAtStartup)
                    {
                        try
                        {
                            RegistryHelper.SetStartup(Constants.AppRegistryKey, executablePath);
                        }
                        catch (Exception ex)
                        {
                            var warningDialog = new InfoDialogWindow(this, "Предупреждение",
                                $"Не удалось настроить автозагрузку: {ex.Message}");
                            warningDialog.ShowDialog();
                        }
                    }
                    else
                    {
                        try
                        {
                            RegistryHelper.RemoveStartup(Constants.AppRegistryKey);
                        }
                        catch { }
                    }
                });

                var successDialog = new InfoDialogWindow(this, "Успех", "Настройки сохранены");
                successDialog.ShowDialog();
                Close();
            }
            catch (Exception ex)
            {
                var errorDialog = new InfoDialogWindow(this, "Ошибка",
                    $"Ошибка сохранения настроек: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        private void AddToBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessCombo.SelectedItem is string processName)
            {
                try
                {
                    _configManager.ToggleBlacklist(processName);
                    BlacklistBox.ItemsSource = _configManager.GetConfig().BlacklistedApps;
                    LoadProcesses();

                    IconExtractor.ClearCacheForProcess(processName);
                }
                catch (Exception ex)
                {
                    var errorDialog = new InfoDialogWindow(this, "Ошибка",
                        $"Ошибка добавления в черный список: {ex.Message}");
                    errorDialog.ShowDialog();
                }
            }
        }

        private void RemoveFromBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (BlacklistBox.SelectedItem is string processName)
            {
                try
                {
                    _configManager.ToggleBlacklist(processName);
                    BlacklistBox.ItemsSource = _configManager.GetConfig().BlacklistedApps;
                    LoadProcesses();

                    IconExtractor.ClearCacheForProcess(processName);
                }
                catch (Exception ex)
                {
                    var errorDialog = new InfoDialogWindow(this, "Ошибка",
                        $"Ошибка удаления из черного списка: {ex.Message}");
                    errorDialog.ShowDialog();
                }
            }
        }

        private void CreateShortcutNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var executablePath = ShortcutHelper.GetExecutablePath();
                var iconPath = Path.Combine(
                    Path.GetDirectoryName(executablePath) ?? string.Empty,
                    "Resources", "Icons", "app.ico");

                if (!File.Exists(iconPath))
                {
                    iconPath = null;
                }

                ShortcutHelper.CreateDesktopShortcut(Constants.AppName, executablePath, iconPath);

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, $"{Constants.AppName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    var successDialog = new InfoDialogWindow(this, "Успех",
                        $"Ярлык создан: {shortcutPath}");
                    successDialog.ShowDialog();
                }
                else
                {
                    var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    var publicShortcutPath = Path.Combine(publicDesktop, $"{Constants.AppName}.lnk");

                    if (File.Exists(publicShortcutPath))
                    {
                        var successDialog = new InfoDialogWindow(this, "Успех",
                            $"Ярлык создан в общем рабочем столе: {publicShortcutPath}");
                        successDialog.ShowDialog();
                    }
                    else
                    {
                        var warningDialog = new InfoDialogWindow(this, "Предупреждение",
                            "Ярлык не найден. Проверьте:\n1. Права доступа к папке Рабочий стол\n2. Настройки антивируса\n3. Попробуйте запустить программу от имени администратора");
                        warningDialog.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                var warningDialog = new InfoDialogWindow(this, "Ошибка",
                    $"Не удалось создать ярлык:\n{ex.Message}");
                warningDialog.ShowDialog();
            }
        }

        private void ClearAllStats_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfirmDialogWindow(this, "Очистка статистики",
                "Вы уверены, что хотите полностью очистить всю статистику? Это действие нельзя отменить.");

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _statsManager.ClearAllStats();
                    IconExtractor.ClearCache();

                    var infoDialog = new InfoDialogWindow(this, "Успех", "Вся статистика очищена");
                    infoDialog.ShowDialog();
                }
                catch (Exception ex)
                {
                    var errorDialog = new InfoDialogWindow(this, "Ошибка",
                        $"Ошибка очистки статистики: {ex.Message}");
                    errorDialog.ShowDialog();
                }
            }
        }

        private void ShowInfoDialog(string title, string message)
        {
            var infoDialog = new InfoDialogWindow(this, title, message);
            infoDialog.ShowDialog();
        }
    }
}