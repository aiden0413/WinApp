using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Diagnostics;

namespace winapp
{
    public partial class BottomControlBarView : UserControl
    {
        public BottomControlBarView()
        {
            InitializeComponent();
        }

        private void BtnOpenVolumeMixer_Click(object sender, RoutedEventArgs e)
        {
            var frame = FindParent<Frame>(this);
            frame?.Navigate(new VolumeMixerPage());
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private void OpenBluetoothSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:devices",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings Open Error: {ex.Message}");
            }
        }
    }
}