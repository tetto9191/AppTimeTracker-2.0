using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTimeTracker.Data.Models
{
    public class AppStat : INotifyPropertyChanged
    {
        private string _appName;
        private TimeSpan _totalTime;
        private DateTime _lastTracked;
        private bool _isBlacklisted;
        private string _iconBase64;
        private ImageSource _icon;

        public string AppName
        {
            get => _appName;
            set { _appName = value; OnPropertyChanged(); }
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set { _totalTime = value; OnPropertyChanged(); }
        }

        public DateTime LastTracked
        {
            get => _lastTracked;
            set { _lastTracked = value; OnPropertyChanged(); }
        }

        public bool IsBlacklisted
        {
            get => _isBlacklisted;
            set { _isBlacklisted = value; OnPropertyChanged(); }
        }

        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                _iconBase64 = value;
                OnPropertyChanged();
                // При изменении Base64 обновляем иконку
                UpdateIconFromBase64();
            }
        }

        public ImageSource Icon
        {
            get
            {
                if (_icon == null && !string.IsNullOrEmpty(_iconBase64))
                {
                    UpdateIconFromBase64();
                }
                return _icon;
            }
            set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        private void UpdateIconFromBase64()
        {
            if (string.IsNullOrEmpty(_iconBase64))
            {
                Icon = null;
                return;
            }

            try
            {
                var bytes = Convert.FromBase64String(_iconBase64);
                var bitmap = new BitmapImage();

                using (var stream = new System.IO.MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                Icon = bitmap;
            }
            catch
            {
                Icon = null;
            }
        }

        public void AddTime(TimeSpan time)
        {
            TotalTime = TotalTime.Add(time);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}