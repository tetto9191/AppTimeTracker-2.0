using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppTimeTracker.Data.Models
{
    public class StatItem : INotifyPropertyChanged
    {
        private string _appName = string.Empty;
        private string _originalAppName = string.Empty;
        private TimeSpan _time;
        private string _valueFormatted = string.Empty;
        private string _iconBase64;

        public string AppName
        {
            get => _appName;
            set { _appName = value; OnPropertyChanged(); }
        }

        public string OriginalAppName
        {
            get => _originalAppName;
            set { _originalAppName = value; OnPropertyChanged(); }
        }

        public TimeSpan Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); UpdateValueFormatted(); }
        }

        public string ValueFormatted
        {
            get => _valueFormatted;
            set { _valueFormatted = value; OnPropertyChanged(); }
        }

        public string IconBase64
        {
            get => _iconBase64;
            set { _iconBase64 = value; OnPropertyChanged(); }
        }

        private void UpdateValueFormatted()
        {
            var totalHours = (int)_time.TotalHours;
            var hours = totalHours;
            var minutes = _time.Minutes;
            var seconds = _time.Seconds;

            ValueFormatted = $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}