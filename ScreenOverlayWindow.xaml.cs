using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Forms;

namespace Huey
{
    public partial class ScreenOverlayWindow : Window
    {
        private Bitmap _screenshot;
        private DispatcherTimer _magnifierTimer;
        private const int MAGNIFIER_SIZE = 70;
        private const int ZOOM_FACTOR = 10;
        private const int CAPTURE_SIZE = 7;
        
        public event Action<System.Windows.Point>? ColourPicked;
        public event Action? Cancelled;

        public ScreenOverlayWindow(Bitmap screenshot)
        {
            InitializeComponent();
            _screenshot = screenshot;
            ScreenshotImage.Source = Imaging.CreateBitmapSourceFromHBitmap(
                _screenshot.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            
            // Hide the cursor and show crosshair
            this.Cursor = System.Windows.Input.Cursors.None;
            
            this.MouseMove += Overlay_MouseMove;
            this.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
            this.KeyDown += Overlay_KeyDown;
            this.Focusable = true;
            
            // Setup magnifier timer
            _magnifierTimer = new DispatcherTimer();
            _magnifierTimer.Interval = TimeSpan.FromMilliseconds(16);
            _magnifierTimer.Tick += UpdateMagnifier;
            
            this.Loaded += (s, e) =>
            {
                this.Topmost = true;
                this.Activate();
                this.Focus();
                _magnifierTimer.Start();
            };
            
            this.Closed += (s, e) =>
            {
                _magnifierTimer.Stop();
            };
        }

        private void Overlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Draw crosshair at mouse position
            var pos = e.GetPosition(CrosshairCanvas);
            DrawCrosshair(pos);
        }

        private void DrawCrosshair(System.Windows.Point pos)
        {
            CrosshairCanvas.Children.Clear();
            double length = 30;
            double thickness = 2;
            var colour = System.Windows.Media.Brushes.DeepSkyBlue;
            
            // Horizontal line
            CrosshairCanvas.Children.Add(new Line
            {
                X1 = pos.X - length / 2,
                Y1 = pos.Y,
                X2 = pos.X + length / 2,
                Y2 = pos.Y,
                Stroke = colour,
                StrokeThickness = thickness
            });
            
            // Vertical line
            CrosshairCanvas.Children.Add(new Line
            {
                X1 = pos.X,
                Y1 = pos.Y - length / 2,
                X2 = pos.X,
                Y2 = pos.Y + length / 2,
                Stroke = colour,
                StrokeThickness = thickness
            });
            
            // Add center dot
            var centerDot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = colour
            };
            Canvas.SetLeft(centerDot, pos.X - 2);
            Canvas.SetTop(centerDot, pos.Y - 2);
            CrosshairCanvas.Children.Add(centerDot);
        }

        private void UpdateMagnifier(object sender, EventArgs e)
        {
            var mousePos = Mouse.GetPosition(this);
            // Map mousePos from overlay window coordinates to screenshot coordinates
            int screenshotX = (int)(mousePos.X * _screenshot.Width / this.ActualWidth);
            int screenshotY = (int)(mousePos.Y * _screenshot.Height / this.ActualHeight);

            // Calculate capture area in screenshot
            int captureX = screenshotX - CAPTURE_SIZE / 2;
            int captureY = screenshotY - CAPTURE_SIZE / 2;

            // Ensure capture area is within screenshot bounds
            captureX = Math.Max(0, Math.Min(_screenshot.Width - CAPTURE_SIZE, captureX));
            captureY = Math.Max(0, Math.Min(_screenshot.Height - CAPTURE_SIZE, captureY));

            try
            {
                using (var bmp = new System.Drawing.Bitmap(CAPTURE_SIZE, CAPTURE_SIZE))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.DrawImage(_screenshot, new System.Drawing.Rectangle(0, 0, CAPTURE_SIZE, CAPTURE_SIZE),
                        new System.Drawing.Rectangle(captureX, captureY, CAPTURE_SIZE, CAPTURE_SIZE),
                        System.Drawing.GraphicsUnit.Pixel);
                    var hBitmap = bmp.GetHbitmap();
                    try
                    {
                        var source = Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(MAGNIFIER_SIZE, MAGNIFIER_SIZE));
                        MagnifierImage.Source = source;
                    }
                    finally
                    {
                        Win32.DeleteObject(hBitmap);
                    }
                }

                // Position magnifier offset from cursor (right and below)
                double offsetX = 16;
                double offsetY = 16;

                // Adjust position if magnifier would go off screen
                var screenPos = this.PointToScreen(mousePos);
                if (screenPos.X + offsetX + MAGNIFIER_SIZE > System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width)
                {
                    offsetX = -MAGNIFIER_SIZE - 16;
                }
                if (screenPos.Y + offsetY + MAGNIFIER_SIZE > System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height)
                {
                    offsetY = -MAGNIFIER_SIZE - 16;
                }

                MagnifierBorder.Visibility = Visibility.Visible;
                Canvas.SetLeft(MagnifierBorder, mousePos.X + offsetX);
                Canvas.SetTop(MagnifierBorder, mousePos.Y + offsetY);
            }
            catch (Exception)
            {
                MagnifierBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void Overlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            ColourPicked?.Invoke(pos);
        }

        private void Overlay_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            // Draw crosshair at mouse position
            var pos = Mouse.GetPosition(this);
            var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.DeepSkyBlue, 2);
            dc.DrawLine(pen, new System.Windows.Point(pos.X - 15, pos.Y), new System.Windows.Point(pos.X + 15, pos.Y));
            dc.DrawLine(pen, new System.Windows.Point(pos.X, pos.Y - 15), new System.Windows.Point(pos.X, pos.Y + 15));
        }

        public System.Drawing.Color GetPixelColour(System.Windows.Point pos)
        {
            int x = (int)(pos.X * _screenshot.Width / this.ActualWidth);
            int y = (int)(pos.Y * _screenshot.Height / this.ActualHeight);
            x = Math.Max(0, Math.Min(_screenshot.Width - 1, x));
            y = Math.Max(0, Math.Min(_screenshot.Height - 1, y));
            return _screenshot.GetPixel(x, y);
        }

        public Bitmap GetScreenshot() => _screenshot;
        
        // Win32 helper for cleanup
        private static class Win32
        {
            [System.Runtime.InteropServices.DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
        }
    }
} 