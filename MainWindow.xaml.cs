using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Point = System.Drawing.Point;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace Huey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DrawingColor? _selectedColor;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void PickColorButton_Click(object sender, RoutedEventArgs e)
        {
            PickColorFromScreen();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColor.HasValue)
            {
                string hexValue = $"#{_selectedColor.Value.R:X2}{_selectedColor.Value.G:X2}{_selectedColor.Value.B:X2}";
                Clipboard.SetText(hexValue);
                
                // Show a brief notification
                MessageBox.Show($"Copied {hexValue} to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PickColorFromScreen()
        {
            // Minimize the window
            this.WindowState = WindowState.Minimized;
            
            // Give the system a moment to minimize
            System.Threading.Thread.Sleep(100);
            
            try
            {
                // Get the cursor position
                Point cursorPosition = GetCursorPosition();
                
                // Capture the screen and get the color at cursor position
                using (Bitmap bitmap = new Bitmap(1, 1))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen((int)cursorPosition.X, (int)cursorPosition.Y, 0, 0, new System.Drawing.Size(1, 1));
                    DrawingColor pixelColor = bitmap.GetPixel(0, 0);
                    
                    // Update the UI with the selected color
                    UpdateColorDisplay(pixelColor);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error picking color: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void UpdateColorDisplay(DrawingColor color)
        {
            _selectedColor = color;
            
            // Update color preview
            ColorPreview.Background = new SolidColorBrush(MediaColor.FromRgb(color.R, color.G, color.B));
            
            // Update RGB value
            RgbValue.Text = $"{color.R}, {color.G}, {color.B}";
            
            // Update HEX value
            HexValue.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            
            // Update preview text
            var textBlock = ColorPreview.Child as TextBlock;
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
    }
} 