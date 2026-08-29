using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using winapp.Services;

namespace winapp
{
    public partial class DeviceListView : UserControl
    {
        public ObservableCollection<BluetoothDeviceModel> DeviceList { get; set; } = new ObservableCollection<BluetoothDeviceModel>();
        
        private static bool _isPollingStarted = false;

        // 상세 페이지로 이동할 때 부모에게 알려주기 위한 이벤트 선언
        public event EventHandler<BluetoothDeviceModel>? DeviceSelected;

        public DeviceListView()
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

                if (!_isPollingStarted)
                {
                    _isPollingStarted = true;
                    _ = BluetoothService.UpdateDeviceStatesAsync(DeviceList);
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
                    // UserControl 내부에서 직접 NavigationService를 쓸 수 없으므로 부모에게 이벤트를 발생시킴
                    DeviceSelected?.Invoke(this, selectedDevice);
                }

                LstDevices.SelectedItem = null;
            }
        }

        private async void BtnToggleConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BluetoothDeviceModel device)
            {
                if (device.IsAddButton) return;

                bool targetState = !device.IsConnected;
                element.IsEnabled = false;

                try
                {
                    bool success = await BluetoothService.SetDeviceConnectionStateAsync(device.Id, targetState);

                    if (!success)
                    {
                        MessageBox.Show($"{device.Name} 연결 상태 변경에 실패했습니다.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"오류 발생: {ex.Message}");
                }
                finally
                {
                    element.IsEnabled = true;
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