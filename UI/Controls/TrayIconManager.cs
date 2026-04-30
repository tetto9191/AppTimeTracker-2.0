using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTimeTracker.UI.Controls
{
    public class TrayIconManager : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1;
        private const int ID_TRAYICON = 1000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private NOTIFYICONDATA _notifyIconData;
        private IntPtr _windowHandle;
        private Window _ownerWindow;
        private IntPtr _iconHandle;
        private System.Drawing.Icon _appIcon;
        private bool _isInitialized;
        private HwndSource _hwndSource;
        private ContextMenu _trayContextMenu;
        private bool _disposed;

        public Action OnShowRequested;
        public Action OnExitRequested;

        public void Initialize(Window ownerWindow, string tooltip)
        {
            if (_isInitialized) return;
            if (ownerWindow == null) throw new ArgumentNullException(nameof(ownerWindow));

            _ownerWindow = ownerWindow;

            // Ждем, пока окно будет инициализировано полностью
            if (!ownerWindow.IsLoaded)
            {
                ownerWindow.Loaded += (s, e) => InitializeTrayIcon(tooltip);
            }
            else
            {
                InitializeTrayIcon(tooltip);
            }
        }

        private void InitializeTrayIcon(string tooltip)
        {
            try
            {
                var wih = new WindowInteropHelper(_ownerWindow);
                _windowHandle = wih.EnsureHandle();

                if (_windowHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Не удалось получить дескриптор окна (HWND).");
                }

                LoadIcon();

                _notifyIconData = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _windowHandle,
                    uID = ID_TRAYICON,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = _iconHandle,
                    szTip = tooltip ?? "AppTimeTracker",
                    dwState = 0,
                    dwStateMask = 0,
                    uVersion = 0
                };

                _hwndSource = HwndSource.FromHwnd(_windowHandle);
                if (_hwndSource == null)
                {
                    throw new InvalidOperationException("Не удалось получить HwndSource для окна.");
                }

                _hwndSource.AddHook(WndProc);

                bool success = Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Не удалось добавить иконку в трей. Код ошибки: {error}");
                }

                CreateTrayContextMenu();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации трей-иконки: {ex.Message}");
                throw;
            }
        }

        private void LoadIcon()
        {
            try
            {
                // Пытаемся загрузить из ресурсов
                var uri = new Uri("pack://application:,,,/Resources/Icons/app.ico", UriKind.Absolute);
                var info = Application.GetResourceStream(uri);
                if (info != null)
                {
                    using (var stream = info.Stream)
                    {
                        _appIcon = new System.Drawing.Icon(stream);
                        _iconHandle = _appIcon.Handle;
                        return;
                    }
                }
            }
            catch
            {
                // Игнорируем ошибку и пробуем следующий способ
            }

            try
            {
                // Пытаемся загрузить из файловой системы
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string appDirectory = Path.GetDirectoryName(appPath);
                string iconPath = Path.Combine(appDirectory, "Resources", "Icons", "app.ico");

                if (File.Exists(iconPath))
                {
                    _appIcon = new System.Drawing.Icon(iconPath);
                    _iconHandle = _appIcon.Handle;
                    return;
                }
            }
            catch
            {
                // Игнорируем ошибку и пробуем следующий способ
            }

            try
            {
                // Используем иконку исполняемого файла
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                _appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                _iconHandle = _appIcon.Handle;
            }
            catch
            {
                // Создаем простую иконку
                CreateDefaultIcon();
            }
        }

        private void CreateDefaultIcon()
        {
            try
            {
                using (var bmp = new System.Drawing.Bitmap(32, 32))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.FromArgb(33, 150, 243)); // Синий цвет
                        using (var font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold))
                        {
                            g.DrawString("AT", font, System.Drawing.Brushes.White, 6, 6);
                        }
                    }
                    _appIcon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                    _iconHandle = _appIcon.Handle;
                }
            }
            catch
            {
                // Если не удалось создать иконку, оставляем нулевой указатель
                _iconHandle = IntPtr.Zero;
            }
        }

        private void CreateTrayContextMenu()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _trayContextMenu = new ContextMenu
                    {
                        Background = (Brush)Application.Current.TryFindResource("DarkBackground3"),
                        BorderBrush = (Brush)Application.Current.TryFindResource("BorderColor"),
                        BorderThickness = new Thickness(1),
                        UseLayoutRounding = true,
                        Placement = PlacementMode.Absolute
                    };

                    var showMenuItem = new MenuItem
                    {
                        Header = "Показать",
                        Style = (Style)Application.Current.TryFindResource(typeof(MenuItem))
                    };
                    showMenuItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(() => OnShowRequested?.Invoke());

                    var exitMenuItem = new MenuItem
                    {
                        Header = "Выйти",
                        Style = (Style)Application.Current.TryFindResource(typeof(MenuItem))
                    };
                    exitMenuItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(() => OnExitRequested?.Invoke());

                    _trayContextMenu.Items.Add(showMenuItem);
                    _trayContextMenu.Items.Add(new Separator());
                    _trayContextMenu.Items.Add(exitMenuItem);
                }
                catch
                {
                    // Создаем простейшее меню
                    _trayContextMenu = new ContextMenu
                    {
                        Background = Brushes.Black,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1)
                    };

                    var showItem = new MenuItem { Header = "Показать", Foreground = Brushes.White };
                    showItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(() => OnShowRequested?.Invoke());

                    var exitItem = new MenuItem { Header = "Выйти", Foreground = Brushes.White };
                    exitItem.Click += (s, e) => Application.Current.Dispatcher.Invoke(() => OnExitRequested?.Invoke());

                    _trayContextMenu.Items.Add(showItem);
                    _trayContextMenu.Items.Add(new Separator { Background = Brushes.Gray });
                    _trayContextMenu.Items.Add(exitItem);
                }
            });
        }

        public void UpdateTooltip(string tooltip)
        {
            if (!_isInitialized || _disposed) return;

            _notifyIconData.szTip = tooltip ?? "AppTimeTracker";
            _notifyIconData.uFlags = NIF_TIP;
            Shell_NotifyIcon(NIM_MODIFY, ref _notifyIconData);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                uint mouseMsg = (uint)lParam.ToInt32();

                switch (mouseMsg)
                {
                    case WM_RBUTTONUP:
                        ShowContextMenuAtCursor();
                        handled = true;
                        break;

                    case WM_LBUTTONDBLCLK:
                        Application.Current.Dispatcher.Invoke(() => OnShowRequested?.Invoke());
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void ShowContextMenuAtCursor()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_trayContextMenu == null) return;

                    // Получаем позицию курсора с учетом DPI
                    GetCursorPos(out POINT cursorPos);

                    // Получаем DPI монитора
                    IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                    uint dpiX = 96, dpiY = 96;
                    if (monitor != IntPtr.Zero)
                    {
                        GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    }

                    double scaleX = dpiX / 96.0;
                    double scaleY = dpiY / 96.0;

                    // Показываем меню
                    _trayContextMenu.Placement = PlacementMode.Absolute;
                    _trayContextMenu.HorizontalOffset = cursorPos.X / scaleX;
                    _trayContextMenu.VerticalOffset = cursorPos.Y / scaleY;
                    _trayContextMenu.IsOpen = true;

                    // Закрываем меню при потере фокуса
                    _trayContextMenu.Closed += (s, e) => _trayContextMenu.IsOpen = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка показа контекстного меню: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_isInitialized)
                {
                    _notifyIconData.uFlags = 0;
                    Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                }

                if (_hwndSource != null && !_hwndSource.IsDisposed)
                {
                    _hwndSource.RemoveHook(WndProc);
                }

                if (_iconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_iconHandle);
                    _iconHandle = IntPtr.Zero;
                }

                if (_appIcon != null)
                {
                    _appIcon.Dispose();
                    _appIcon = null;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_trayContextMenu != null)
                    {
                        _trayContextMenu.IsOpen = false;
                        _trayContextMenu = null;
                    }
                });
            }
            catch
            {
                // Игнорируем ошибки при завершении
            }

            _isInitialized = false;
            GC.SuppressFinalize(this);
        }
    }
}