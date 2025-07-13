using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;

namespace Huey
{
    public partial class ScreenOverlayWindow : Window
    {
        private Bitmap _screenshot;
        public event Action<System.Windows.Point>? ColourPicked;
        public event Action? Cancelled;

        public ScreenOverlayWindow(Bitmap screenshot)
        {
            InitializeComponent();
            _screenshot = screenshot;
            ScreenshotImage.Source = Imaging.CreateBitmapSourceFromHBitmap(
                _screenshot.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            this.Cursor = Cursors.Cross;
            this.MouseMove += Overlay_MouseMove;
            this.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
            this.KeyDown += Overlay_KeyDown;
            this.Focusable = true;
            this.Loaded += (s, e) =>
            {
                this.Topmost = true;
                this.Activate();
                this.Focus();
            };
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
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
            var color = System.Windows.Media.Brushes.DeepSkyBlue;
            // Horizontal line
            CrosshairCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = pos.X - length / 2,
                Y1 = pos.Y,
                X2 = pos.X + length / 2,
                Y2 = pos.Y,
                Stroke = color,
                StrokeThickness = thickness
            });
            // Vertical line
            CrosshairCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = pos.X,
                Y1 = pos.Y - length / 2,
                X2 = pos.X,
                Y2 = pos.Y + length / 2,
                Stroke = color,
                StrokeThickness = thickness
            });
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            ColourPicked?.Invoke(pos);
        }

        private void Overlay_KeyDown(object sender, KeyEventArgs e)
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
    }
} 