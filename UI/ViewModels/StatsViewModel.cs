using AppTimeTracker.Data.Models;
using AppTimeTracker.Data.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AppTimeTracker.UI.ViewModels
{
    public class StatsViewModel : INotifyPropertyChanged
    {
        private readonly StatsManager _statsManager;
        private readonly ConfigManager _configManager;
        private readonly IIconCacheService _iconCacheService;
        private bool _isUpdating;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private const int MIN_UPDATE_INTERVAL_MS = 500;

        public ObservableCollection<StatItem> StatItems { get; }
        public ICollectionView StatItemsView { get; }

        public List<string> PeriodItems { get; } = new List<string>
        {
            "Произвольный",
            "Сегодня",
            "Вчера",
            "Эта неделя",
            "Прошлая неделя",
            "Этот месяц",
            "Прошлый месяц",
            "Все время"
        };

        public List<string> TopCountItems { get; } = new List<string>
        {
            "Все",
            "Топ 3",
            "Топ 5",
            "Топ 10",
            "Топ 20"
        };

        private ICommand _updateStatsCommand;
        public ICommand UpdateStatsCommand => _updateStatsCommand ??= new RelayCommand(async () => await UpdateStatsAsync());

        private ICommand _deleteItemCommand;
        public ICommand DeleteItemCommand => _deleteItemCommand ??= new RelayCommand<string>(DeleteItem);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private DateTime _selectedFromDate = DateTime.Today.AddDays(-7);
        public DateTime SelectedFromDate
        {
            get => _selectedFromDate;
            set { _selectedFromDate = value; OnPropertyChanged(); _ = UpdateStatsAsync(); }
        }

        private DateTime _selectedToDate = DateTime.Today;
        public DateTime SelectedToDate
        {
            get => _selectedToDate;
            set { _selectedToDate = value; OnPropertyChanged(); _ = UpdateStatsAsync(); }
        }

        private int _selectedPeriodIndex = 7;
        public int SelectedPeriodIndex
        {
            get => _selectedPeriodIndex;
            set
            {
                _selectedPeriodIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDateRangeEnabled));
                UpdateDateRange();
                _ = UpdateStatsAsync();
            }
        }

        private int _selectedTopCountIndex;
        public int SelectedTopCountIndex
        {
            get => _selectedTopCountIndex;
            set { _selectedTopCountIndex = value; OnPropertyChanged(); _ = UpdateStatsAsync(); }
        }

        public bool IsDateRangeEnabled => SelectedPeriodIndex != 7 && SelectedPeriodIndex != 0;

        public StatsViewModel(StatsManager statsManager, ConfigManager configManager,
                            IIconCacheService iconCacheService)
        {
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _iconCacheService = iconCacheService ?? throw new ArgumentNullException(nameof(iconCacheService));

            StatItems = new ObservableCollection<StatItem>();
            StatItemsView = CollectionViewSource.GetDefaultView(StatItems);
            StatItemsView.SortDescriptions.Add(new SortDescription("Time", ListSortDirection.Descending));

            _statsManager.StatsUpdated += () => _ = UpdateStatsAsync();
            _configManager.ConfigChanged += OnConfigChanged;
        }

        private void UpdateDateRange()
        {
            switch (SelectedPeriodIndex)
            {
                case 1:
                    SelectedFromDate = DateTime.Today;
                    SelectedToDate = DateTime.Today;
                    break;
                case 2:
                    SelectedFromDate = DateTime.Today.AddDays(-1);
                    SelectedToDate = DateTime.Today.AddDays(-1);
                    break;
                case 3:
                    var today = DateTime.Today;
                    var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                    SelectedFromDate = today.AddDays(-daysSinceMonday);
                    SelectedToDate = today.AddDays(6 - daysSinceMonday);
                    break;
                case 4:
                    var lastWeek = DateTime.Today.AddDays(-7);
                    var daysSinceLastMonday = ((int)lastWeek.DayOfWeek + 6) % 7;
                    SelectedFromDate = lastWeek.AddDays(-daysSinceLastMonday);
                    SelectedToDate = lastWeek.AddDays(6 - daysSinceLastMonday);
                    break;
                case 5:
                    SelectedFromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    SelectedToDate = SelectedFromDate.AddMonths(1).AddDays(-1);
                    break;
                case 6:
                    var lastMonth = DateTime.Today.AddMonths(-1);
                    SelectedFromDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    SelectedToDate = SelectedFromDate.AddMonths(1).AddDays(-1);
                    break;
                case 7:
                    SelectedFromDate = DateTime.MinValue;
                    SelectedToDate = DateTime.MaxValue;
                    break;
            }
        }

        public async Task UpdateStatsAsync()
        {
            if (_isUpdating) return;

            var now = DateTime.Now;
            if ((now - _lastUpdateTime).TotalMilliseconds < MIN_UPDATE_INTERVAL_MS)
            {
                return;
            }

            _isUpdating = true;
            IsLoading = true;
            _lastUpdateTime = now;

            try
            {
                var from = SelectedPeriodIndex == 7 ? DateTime.MinValue : SelectedFromDate.Date;
                var to = SelectedPeriodIndex == 7 ? DateTime.MaxValue : SelectedToDate.Date.AddDays(1).AddSeconds(-1);

                var stats = await Task.Run(() => _statsManager.GetStats(from, to));

                var filteredStats = stats
                    .Where(kvp => !_configManager.IsBlacklisted(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (SelectedTopCountIndex > 0)
                {
                    var topCount = SelectedTopCountIndex switch
                    {
                        1 => 3,
                        2 => 5,
                        3 => 10,
                        4 => 20,
                        _ => 10
                    };
                    filteredStats = filteredStats
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(topCount)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }

                await Application.Current.Dispatcher.InvokeAsync(() => UpdateStatItems(filteredStats));
            }
            finally
            {
                _isUpdating = false;
                IsLoading = false;
            }
        }

        private void UpdateStatItems(Dictionary<string, TimeSpan> stats)
        {
            var orderedStats = stats.OrderByDescending(x => x.Value).ToList();
            var existingItems = StatItems.ToDictionary(item => item.AppName, item => item);

            var itemsToRemove = StatItems
                .Where(item => !stats.ContainsKey(item.AppName))
                .ToList();

            foreach (var item in itemsToRemove)
            {
                StatItems.Remove(item);
            }

            foreach (var kvp in orderedStats)
            {
                if (existingItems.TryGetValue(kvp.Key, out var existingItem))
                {
                    if (existingItem.Time != kvp.Value)
                    {
                        existingItem.Time = kvp.Value;
                    }

                    // OriginalAppName оставляем как отображаемое имя (можно и не трогать)
                    UpdateItemIcon(existingItem, kvp.Key);
                }
                else
                {
                    var statItem = new StatItem
                    {
                        AppName = kvp.Key,
                        OriginalAppName = kvp.Key,
                        Time = kvp.Value
                    };

                    UpdateItemIcon(statItem, kvp.Key);
                    StatItems.Add(statItem);
                }
            }

            StatItemsView.Refresh();
        }

        private void UpdateItemIcon(StatItem item, string displayName)
        {
            var iconBase64 = _configManager.GetCustomIconBase64(displayName)
                           ?? _iconCacheService.GetIconBase64(displayName);

            if (item.IconBase64 != iconBase64)
            {
                item.IconBase64 = iconBase64;
            }
        }

        private void OnConfigChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in StatItems)
                {
                    UpdateItemIcon(item, item.AppName);
                }
                _ = UpdateStatsAsync();
            });
        }

        private void DeleteItem(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return;

            _statsManager.DeleteAppStats(appName);
            _ = UpdateStatsAsync();
        }

        public void Cleanup()
        {
            _statsManager.StatsUpdated -= () => _ = UpdateStatsAsync();
            _configManager.ConfigChanged -= OnConfigChanged;
            _statsManager.Cleanup();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;

        public void Execute(object parameter) => _execute((T)parameter);
    }
}