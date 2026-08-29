using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace winapp
{
    public partial class MainDashboardView : UserControl
    {
        public MainDashboardView()
        {
            InitializeComponent();
        }

        private void MyDeviceListView_DeviceSelected(object sender, BluetoothDeviceModel selectedDevice)
        {
            // UserControl은 NavigationService가 없으므로 부모 프레임을 직접 탐색해서 이동
            var frame = FindParent<Frame>(this);
            frame?.Navigate(new DeviceDetailPage(selectedDevice));
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}