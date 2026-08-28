using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using winapp.Services; // 만든 서비스 네임스페이스 추가

namespace winapp
{
    public partial class DeviceListPage : Page
    {
        public ObservableCollection<BluetoothDeviceModel> DeviceList { get; set; } = new ObservableCollection<BluetoothDeviceModel>();
        
        private static bool _isPollingStarted = false;

        public DeviceListPage()
        {
            InitializeComponent();
            
            this.DataContext = this;
            LstDevices.ItemsSource = DeviceList;

            _ = LoadDevicesAsync();
        }

        private async Task LoadDevicesAsync()
        {
            DeviceList.Clear();
            try
            {
                var devices = await BluetoothService.GetDevicesAsync();
                foreach (var device in devices)
                {
                    DeviceList.Add(device);
                }

                // 기기 목록을 불러온 직후 백그라운드 실시간 갱신(폴링) 시작 (앱 실행 중 최초 1회만 실행)
                if (!_isPollingStarted)
                {
                    _isPollingStarted = true;
                    _ = BluetoothService.UpdateBatteryLevelsAsync(DeviceList);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
        }

        private async void LstDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstDevices.SelectedItem is BluetoothDeviceModel selectedDevice)
            {
                if (selectedDevice.IsAddButton)
                {
                    await LoadDevicesAsync();
                }
                else
                {
                    NavigationService.Navigate(new DeviceDetailPage(selectedDevice));
                }

                LstDevices.SelectedItem = null;
            }
        }

        private async void BtnToggleConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BluetoothDeviceModel device)
            {
                bool targetState = !device.IsConnected;
                
                // 서비스 호출 (device.Name 대신 고유 식별자인 device.Id 사용)
                bool success = await BluetoothService.SetDeviceConnectionStateAsync(device.Id, targetState);

                if (success)
                {
                    device.IsConnected = targetState;
                    await LoadDevicesAsync();
                }
                else
                {
                    MessageBox.Show($"{device.Name} 연결 상태 변경에 실패했습니다.");
                }
            }
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            AudioService.SetSystemVolume((int)e.NewValue);
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v && v == Visibility.Visible) return false;
            return true;
        }
    }
}