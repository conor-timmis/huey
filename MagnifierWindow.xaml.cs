using System.Windows;
using System.Windows.Media.Imaging;

namespace Huey
{
    public partial class MagnifierWindow : Window
    {
        public MagnifierWindow()
        {
            InitializeComponent();
        }

        public void UpdateMagnifier(BitmapSource source)
        {
            MagnifierImage.Source = source;
        }
    }
} 