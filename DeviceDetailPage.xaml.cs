using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace winapp
{
    public partial class DeviceDetailPage : Page
    {
        private BluetoothDeviceModel _device;

        // 생성자 (기기 데이터를 전달받음)
        public DeviceDetailPage(BluetoothDeviceModel device)
        {
            InitializeComponent();
            _device = device;

            // UI에 데이터 매핑
            TxtName.Text = _device.Name;
            TxtId.Text = _device.Id;
            TxtStatus.Text = _device.StatusText;
        }
    }
}