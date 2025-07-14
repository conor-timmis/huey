using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Point = System.Drawing.Point;
using DrawingColour = System.Drawing.Color;
using MediaColour = System.Windows.Media.Color;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Huey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DrawingColour? _selectedColour;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private Win32.LowLevelMouseProc _mouseProc;
        private MagnifierWindow? _magnifierWindow;
        private bool _isPickingColour = false;
        private DispatcherTimer? _copyNotificationTimer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void PickColourButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimize the main window
            this.WindowState = WindowState.Minimized;
            System.Threading.Thread.Sleep(150);

            // Capture the entire screen
            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            using (var bmp = new System.Drawing.Bitmap(screenBounds.Width, screenBounds.Height))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, bmp.Size);
                // Show overlay window with screenshot
                var overlay = new ScreenOverlayWindow((System.Drawing.Bitmap)bmp.Clone());
                overlay.ColourPicked += pos =>
                {
                    // Get colour from screenshot
                    var colour = overlay.GetPixelColour(pos);
                    Dispatcher.Invoke(() => UpdateColourDisplay(colour));
                    overlay.Close();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                };
                overlay.Cancelled += () =>
                {
                    overlay.Close();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                };
                overlay.Show();
                overlay.Focus();
            }
        }

        private void UpdateMagnifier(object? sender, EventArgs e)
        {
            if (!_isPickingColour || _magnifierWindow == null) return;

            Win32.POINT pt;
            Win32.GetCursorPos(out pt);
            int zoom = 8;
            int size = 19; // 19x19 area for 8x zoom in 150px window
            int captureSize = size;
            int captureX = pt.X - captureSize / 2;
            int captureY = pt.Y - captureSize / 2;

            using (var bmp = new System.Drawing.Bitmap(captureSize, captureSize))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(captureX, captureY, 0, 0, new System.Drawing.Size(captureSize, captureSize));
                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(150, 150));
                    _magnifierWindow.UpdateMagnifier(source);
                }
                finally
                {
                    Win32.DeleteObject(hBitmap);
                }
            }

            // Position magnifier near cursor (offset so it doesn't cover the pointer)
            _magnifierWindow.Left = pt.X + 20;
            _magnifierWindow.Top = pt.Y + 20;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)Win32.WM_LBUTTONDOWN)
            {
                // Get mouse position
                Win32.POINT pt = (Win32.POINT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(Win32.POINT));
                var cursorPosition = new Point(pt.X, pt.Y);

                // Capture the screen and get the colour at cursor position
                using (Bitmap bitmap = new Bitmap(1, 1))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen((int)cursorPosition.X, (int)cursorPosition.Y, 0, 0, new System.Drawing.Size(1, 1));
                    DrawingColour pixelColour = bitmap.GetPixel(0, 0);
                    Dispatcher.Invoke(() => UpdateColourDisplay(pixelColour));
                }

                // Remove hook and restore window
                Win32.UnhookWindowsHookEx(_mouseHookId);
                Dispatcher.Invoke(() =>
                {
                    _isPickingColour = false;
                    if (_magnifierWindow != null)
                    {
                        _magnifierWindow.Close();
                        _magnifierWindow = null;
                    }
                    CompositionTarget.Rendering -= UpdateMagnifier;
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                });
            }
            return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColour.HasValue)
            {
                string hexValue = $"#{_selectedColour.Value.R:X2}{_selectedColour.Value.G:X2}{_selectedColour.Value.B:X2}";
                System.Windows.Clipboard.SetText(hexValue);
                
                // Show a brief notification
                System.Windows.MessageBox.Show($"Copied {hexValue} to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CopyHexButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColour.HasValue)
            {
                string hexValue = $"#{_selectedColour.Value.R:X2}{_selectedColour.Value.G:X2}{_selectedColour.Value.B:X2}";
                System.Windows.Clipboard.SetText(hexValue);
                ShowCopyNotification();
            }
        }

        private void CopyRgbButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColour.HasValue)
            {
                string rgbValue = $"{_selectedColour.Value.R}, {_selectedColour.Value.G}, {_selectedColour.Value.B}";
                System.Windows.Clipboard.SetText(rgbValue);
                ShowCopyNotification();
            }
        }

        private void ShowCopyNotification()
        {
            CopyNotification.Visibility = Visibility.Visible;
            _copyNotificationTimer?.Stop();
            if (_copyNotificationTimer == null)
            {
                _copyNotificationTimer = new DispatcherTimer();
                _copyNotificationTimer.Interval = TimeSpan.FromSeconds(1.2);
                _copyNotificationTimer.Tick += (s, e) =>
                {
                    CopyNotification.Visibility = Visibility.Collapsed;
                    _copyNotificationTimer.Stop();
                };
            }
            _copyNotificationTimer.Start();
        }

        private void PickColourFromScreen()
        {
            // Minimize the window
            this.WindowState = WindowState.Minimized;
            
            // Give the system a moment to minimize
            System.Threading.Thread.Sleep(100);
            
            try
            {
                // Get the cursor position
                Point cursorPosition = GetCursorPosition();
                
                // Capture the screen and get the colour at cursor position
                using (Bitmap bitmap = new Bitmap(1, 1))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen((int)cursorPosition.X, (int)cursorPosition.Y, 0, 0, new System.Drawing.Size(1, 1));
                    DrawingColour pixelColour = bitmap.GetPixel(0, 0);
                    
                    // Update the UI with the selected colour
                    UpdateColourDisplay(pixelColour);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error picking colour: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore the window
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }

        private Point GetCursorPosition()
        {
            // Get cursor position using Win32 API
            POINT point;
            GetCursorPos(out point);
            return new Point(point.X, point.Y);
        }

        private void UpdateColourDisplay(DrawingColour colour)
        {
            _selectedColour = colour;
            
            // Update colour preview
            ColourPreview.Background = new SolidColorBrush(MediaColour.FromRgb(colour.R, colour.G, colour.B));
            
            // Update RGB value
            RgbValue.Text = $"{colour.R}, {colour.G}, {colour.B}";
            
            // Update HEX value
            HexValue.Text = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
            
            // Update preview text
            var textBlock = ColourPreview.Child as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = "";
            }
        }

        #region Win32 API
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        #endregion

        // Add Win32 helper class for mouse hook
        private static class Win32
        {
            public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            public const int WH_MOUSE_LL = 14;
            public const int WM_LBUTTONDOWN = 0x0201;

            public const int IDC_CROSS = 32515;
            [DllImport("user32.dll")]
            public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
            [DllImport("user32.dll")]
            public static extern IntPtr SetCursor(IntPtr hCursor);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public POINT pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            public static IntPtr SetMouseHook(LowLevelMouseProc proc)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetCursorPos(out POINT lpPoint);
        }
    }
} 