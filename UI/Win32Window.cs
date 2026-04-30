using System;
using System.Windows.Interop;

namespace AppTimeTracker.UI
{
    public class Win32Window : System.Windows.Forms.IWin32Window
    {
        private readonly IntPtr _handle;

        public Win32Window(System.Windows.Window window)
        {
            _handle = new WindowInteropHelper(window).Handle;
        }

        public IntPtr Handle => _handle;
    }
}