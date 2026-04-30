using AppTimeTracker.Core.Trackers;
using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Services;
using AppTimeTracker.UI.Controls;
using AppTimeTracker.UI.Windows.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTimeTracker.UI.Windows
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ActiveProcessTracker _tracker;
        private readonly StatsManager _statsManager;
        private readonly ConfigManager _configManager;
        private readonly IIconCacheService _iconCacheService;
        private ObservableCollection<AppStatDisplay> _trackedApps;
        private string _currentStatusText;
        private bool _isTracking;
        private TrayIconManager _trayManager;

        public ObservableCollection<AppStatDisplay> TrackedApps
        {
            get => _trackedApps;
            set
            {
                _trackedApps = value;
                OnPropertyChanged();
            }
        }

        public string CurrentStatusText
        {
            get => _currentStatusText;
            set
            {
                _currentStatusText = value;
                OnPropertyChanged();
            }
        }

        public MainWindow(ActiveProcessTracker tracker, StatsManager statsManager,
                         ConfigManager configManager, IIconCacheService iconCacheService)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _iconCacheService = iconCacheService ?? throw new ArgumentNullException(nameof(iconCacheService));

            InitializeComponent();

            this.DataContext = this;

            TrackedApps = new ObservableCollection<AppStatDisplay>();
            CurrentStatusText = "Отслеживание запущено";
            _isTracking = true;

            _tracker.ProcessTimeUpdated += OnProcessTimeUpdated;
            _tracker.Start();

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => UpdateDisplay();
            timer.Start();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTrayManager();
            await CheckForUpdatesAsync();
        }

        private void InitializeTrayManager()
        {
            try
            {
                _trayManager = new TrayIconManager();

                _trayManager.OnShowRequested += () =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                        this.Topmost = true;
                        this.Topmost = false;
                    });
                };

                _trayManager.OnExitRequested += () =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Exit_Click(null, null);
                    });
                };

                _trayManager.Initialize(this, "AppTimeTracker - Отслеживание активно");
            }
            catch (Exception ex)
            {
                ShowInfoDialog("Ошибка", $"Не удалось инициализировать трей-икон: {ex.Message}");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _iconCacheService.SaveCache();
            _trayManager?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void OnProcessTimeUpdated(string originalProcessName, string displayName, TimeSpan time)
        {
            if (string.IsNullOrEmpty(originalProcessName)) return;

            var existingApp = TrackedApps.FirstOrDefault(a => a.OriginalProcessName == originalProcessName);
            if (existingApp != null)
            {
                existingApp.TotalTime = existingApp.TotalTime.Add(time);
                existingApp.LastTracked = DateTime.Now;
                existingApp.AppName = displayName;
            }
            else
            {
                var newApp = new AppStatDisplay
                {
                    OriginalProcessName = originalProcessName,
                    AppName = displayName,
                    TotalTime = time,
                    LastTracked = DateTime.Now,
                    IsBlacklisted = _configManager.IsBlacklisted(originalProcessName),
                    Icon = _iconCacheService.GetIconImage(originalProcessName)
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TrackedApps.Add(newApp);
                    UpdateProcessesCount();
                });
            }
        }

        private void UpdateDisplay()
        {
            UpdateProcessesCount();

            if (_trayManager != null)
            {
                _trayManager.UpdateTooltip($"AppTimeTracker\nОтслеживается {TrackedApps.Count} приложений\nОбновлено: {DateTime.Now:HH:mm:ss}");
            }

            CurrentStatusText = _isTracking ? "Отслеживание активно" : "Отслеживание на паузе";
        }

        private void UpdateProcessesCount()
        {
            var count = TrackedApps.Count(a => !a.IsBlacklisted);
            ActiveProcessesCount.Text = count.ToString();
        }

        private void RenameProcess_Click(object sender, RoutedEventArgs e)
        {
            if (TrackedAppsList.SelectedItem is AppStatDisplay selectedApp)
            {
                if (selectedApp.IsBlacklisted)
                {
                    ShowInfoDialog("Ошибка", "Невозможно переименовать процесс из черного списка.");
                    return;
                }

                var dialog = new AppTimeTracker.UI.Dialogs.SimpleInputDialog(
                    selectedApp.AppName,
                    "Переименовать процесс"
                );

                var win32Window = new AppTimeTracker.UI.Win32Window(this);
                var result = dialog.ShowDialog(win32Window);

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.EnteredText))
                {
                    try
                    {
                        _configManager.SetDisplayName(selectedApp.OriginalProcessName, dialog.EnteredText);
                        selectedApp.AppName = dialog.EnteredText;
                        var customIcon = _configManager.GetCustomIconBase64(selectedApp.OriginalProcessName);
                        if (!string.IsNullOrEmpty(customIcon))
                        {
                            selectedApp.Icon = Base64ToImageSource(customIcon);
                        }
                        else
                        {
                            selectedApp.Icon = _iconCacheService.GetIconImage(selectedApp.OriginalProcessName);
                        }

                        ShowInfoDialog("Успех", $"Процесс переименован в '{dialog.EnteredText}'.");
                    }
                    catch (Exception ex)
                    {
                        ShowInfoDialog("Ошибка", $"Ошибка при переименовании: {ex.Message}");
                    }
                }
            }
        }

        private void SelectIcon_Click(object sender, RoutedEventArgs e)
        {
            if (TrackedAppsList.SelectedItem is AppStatDisplay selectedApp)
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
                            _configManager.SetCustomIcon(selectedApp.OriginalProcessName, base64);
                            selectedApp.Icon = Base64ToImageSource(base64);
                            ShowInfoDialog("Успех", "Иконка обновлена");
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

        private void ToggleTracking_Click(object sender, RoutedEventArgs e)
        {
            if (_isTracking)
            {
                _tracker.Stop();
                ToggleTrackingBtn.Content = "Продолжить";
                CurrentStatusText = "Отслеживание на паузе";
                _isTracking = false;
            }
            else
            {
                _tracker.Start();
                ToggleTrackingBtn.Content = "Пауза";
                CurrentStatusText = "Отслеживание активно";
                _isTracking = true;
            }
        }

        private void OpenStats_Click(object sender, RoutedEventArgs e)
        {
            var statsWindow = new StatsWindow(_statsManager, _configManager, _iconCacheService, _tracker);
            statsWindow.Owner = this;
            statsWindow.ShowDialog();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_configManager, _statsManager);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void AddToBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (TrackedAppsList.SelectedItem is AppStatDisplay selectedApp)
            {
                _configManager.ToggleBlacklist(selectedApp.OriginalProcessName);
                selectedApp.IsBlacklisted = true;
                OnPropertyChanged(nameof(TrackedApps));
            }
        }

        private void ExcludeFromTracking_Click(object sender, RoutedEventArgs e)
        {
            if (TrackedAppsList.SelectedItem is AppStatDisplay selectedApp)
            {
                _tracker.ExcludeProcess(selectedApp.OriginalProcessName);
                TrackedApps.Remove(selectedApp);
                UpdateProcessesCount();
            }
        }

        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _iconCacheService.SaveCache();
            _tracker.Stop();
            _trayManager?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void ShowInfoDialog(string title, string message)
        {
            var infoDialog = new InfoDialogWindow(this, title, message);
            infoDialog.ShowDialog();
        }

        private ImageSource Base64ToImageSource(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateService = new UpdateService();
                var result = await updateService.CheckForUpdateAsync();

                if (result == null || !result.HasUpdate) return;

                var dialog = new ConfirmDialogWindow(
                    owner: this,
                    title: "Доступно обновление",
                    message: $"Текущая версия: {result.CurrentVersion}\nНовая версия: {result.LatestVersion}\n\nОбновить сейчас?"
                );

                if (dialog.ShowDialog() == true)
                {
                    string tempZip = Path.Combine(Path.GetTempPath(), "AppTimeTracker_update.zip");
                    bool downloaded = await updateService.DownloadUpdateAsync(result.DownloadUrl, tempZip);
                    if (!downloaded)
                    {
                        ShowInfoDialog("Ошибка", "Не удалось скачать обновление.");
                        return;
                    }

                    bool applied = updateService.ApplyUpdate(tempZip);
                    if (applied)
                    {
                        _iconCacheService.SaveCache();
                        _tracker.Stop();
                        _trayManager?.Dispose();
                        System.Windows.Application.Current.Shutdown();
                    }
                    else
                    {
                        ShowInfoDialog("Ошибка", "Не удалось применить обновление.");
                    }
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки обновления просто игнорируем, чтобы не мешать работе
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppStatDisplay : INotifyPropertyChanged
    {
        private string _originalProcessName;
        private string _appName;
        private TimeSpan _totalTime;
        private DateTime _lastTracked;
        private bool _isBlacklisted;
        private ImageSource _icon;

        public string OriginalProcessName
        {
            get => _originalProcessName;
            set
            {
                _originalProcessName = value;
                OnPropertyChanged();
            }
        }

        public string AppName
        {
            get => _appName;
            set
            {
                _appName = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                _totalTime = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastTracked
        {
            get => _lastTracked;
            set
            {
                _lastTracked = value;
                OnPropertyChanged();
            }
        }

        public bool IsBlacklisted
        {
            get => _isBlacklisted;
            set
            {
                _isBlacklisted = value;
                OnPropertyChanged();
            }
        }

        public ImageSource Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}