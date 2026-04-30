using System.Windows;
using System.Windows.Input;

namespace AppTimeTracker.UI.Windows.Dialogs
{
    public partial class InfoDialogWindow : Window
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(InfoDialogWindow),
                new PropertyMetadata("Информация"));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(InfoDialogWindow),
                new PropertyMetadata(string.Empty));

        public string TitleText
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public InfoDialogWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public InfoDialogWindow(Window owner, string title, string message) : this()
        {
            Owner = owner;
            TitleText = title;
            Message = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}