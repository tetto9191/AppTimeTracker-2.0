using AppTimeTracker.Core.Trackers;
using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Services;
using AppTimeTracker.UI.ViewModels;
using AppTimeTracker.UI.Windows.Dialogs;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AppTimeTracker.UI.Windows
{
    public partial class StatsWindow : Window
    {
        private readonly StatsViewModel _viewModel;
        private readonly ConfigManager _configManager;
        private readonly ActiveProcessTracker _tracker;
        private readonly StatsManager _statsManager;
        private readonly IIconCacheService _iconCacheService;

        public StatsWindow(StatsManager statsManager, ConfigManager configManager,
                          IIconCacheService iconCacheService, ActiveProcessTracker tracker)
        {
            InitializeComponent();

            _configManager = configManager;
            _tracker = tracker;
            _statsManager = statsManager;
            _iconCacheService = iconCacheService;

            _viewModel = new StatsViewModel(statsManager, configManager, iconCacheService);
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Cleanup();
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

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SimpleListBox?.SelectedItem is StatItem selectedItem)
            {
                var dialog = new ConfirmDialogWindow(this, "Удаление записи",
                    $"Вы уверены, что хотите удалить всю статистику для приложения '{selectedItem.AppName}'? Это действие нельзя отменить.");

                if (dialog.ShowDialog() == true)
                {
                    _viewModel.DeleteItemCommand.Execute(selectedItem.AppName);

                    var infoDialog = new InfoDialogWindow(this, "Успех",
                        $"Статистика для приложения '{selectedItem.AppName}' удалена");
                    infoDialog.ShowDialog();
                }
            }
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SimpleListBox?.SelectedItem is StatItem selectedItem)
            {
                var dialog = new AppTimeTracker.UI.Dialogs.SimpleInputDialog(
                    selectedItem.AppName,
                    "Переименовать процесс"
                );

                var win32Window = new AppTimeTracker.UI.Win32Window(this);
                var result = dialog.ShowDialog(win32Window);

                if (result == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(dialog.EnteredText))
                {
                    try
                    {
                        _configManager.SetDisplayName(selectedItem.OriginalAppName, dialog.EnteredText);
                        _ = _viewModel.UpdateStatsAsync();

                        var infoDialog = new InfoDialogWindow(this, "Успех",
                            $"Процесс переименован в '{dialog.EnteredText}'.");
                        infoDialog.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        ShowInfoDialog("Ошибка", $"Ошибка при переименовании: {ex.Message}");
                    }
                }
            }
        }

        private void SelectIconMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SimpleListBox?.SelectedItem is StatItem selectedItem)
            {
                var openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Все поддерживаемые файлы (*.png;*.jpg;*.jpeg;*.ico;*.exe)|*.png;*.jpg;*.jpeg;*.ico;*.exe|" +
                                        "Изображения (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|" +
                                        "Исполняемые файлы (*.exe)|*.exe";
                openFileDialog.Title = "Выберите иконку или исполняемый файл";

                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        var base64 = AppTimeTracker.Core.Helpers.IconExtractor.ExtractIconAsBase64(openFileDialog.FileName);

                        if (base64 != null)
                        {
                            _configManager.SetCustomIcon(selectedItem.OriginalAppName, base64);
                            _ = _viewModel.UpdateStatsAsync();

                            var infoDialog = new InfoDialogWindow(this, "Успех", "Иконка обновлена");
                            infoDialog.ShowDialog();
                        }
                        else
                        {
                            ShowInfoDialog("Ошибка", "Не удалось загрузить или извлечь иконку из файла.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowInfoDialog("Ошибка", $"Не удалось загрузить иконку: {ex.Message}");
                    }
                }
            }
        }

        private void AddToBlacklistMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SimpleListBox?.SelectedItem is StatItem selectedItem)
            {
                var dialog = new ConfirmDialogWindow(this, "Добавить в черный список",
                    $"Вы уверены, что хотите добавить '{selectedItem.AppName}' в черный список? " +
                    "Приложение больше не будет отслеживаться.");

                if (dialog.ShowDialog() == true)
                {
                    _configManager.ToggleBlacklist(selectedItem.OriginalAppName);
                    _ = _viewModel.UpdateStatsAsync();

                    var infoDialog = new InfoDialogWindow(this, "Успех",
                        $"'{selectedItem.AppName}' добавлен в черный список.");
                    infoDialog.ShowDialog();
                }
            }
        }

        private void ExcludeFromTrackingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SimpleListBox?.SelectedItem is StatItem selectedItem)
            {
                var dialog = new ConfirmDialogWindow(this, "Прекратить отслеживание",
                    $"Вы уверены, что хотите прекратить отслеживание '{selectedItem.AppName}'?");

                if (dialog.ShowDialog() == true)
                {
                    _tracker.ExcludeProcess(selectedItem.OriginalAppName);

                    var infoDialog = new InfoDialogWindow(this, "Успех",
                        $"Отслеживание '{selectedItem.AppName}' прекращено.");
                    infoDialog.ShowDialog();
                }
            }
        }

        // Метод MergeMenuItem_Click удалён, так как объединение процессов больше не поддерживается

        private void ShowInfoDialog(string title, string message)
        {
            var infoDialog = new InfoDialogWindow(this, title, message);
            infoDialog.ShowDialog();
        }
    }
}