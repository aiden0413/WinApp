using System.ComponentModel;

namespace winapp
{
    public class BluetoothDeviceModel : INotifyPropertyChanged
    {
        // Null 경고를 방지하기 위해 초기값 설정 또는 nullable 처리
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ButtonText));
            }
        }

        private int _batteryPercent = -1;
        public int BatteryPercent
        {
            get => _batteryPercent;
            set
            {
                _batteryPercent = value;
                OnPropertyChanged(nameof(BatteryPercent));
                OnPropertyChanged(nameof(BatteryText));
                OnPropertyChanged(nameof(HasBattery));
            }
        }

        public string BatteryText => BatteryPercent >= 0 ? $"{BatteryPercent}%" : "";
        public bool HasBattery => BatteryPercent >= 0;

        public bool IsAddButton { get; set; }

        public string StatusText => IsConnected ? "연결됨" : "연결 안 됨";
        public string StatusColor => IsConnected ? "#4CAF50" : "#888888";
        public string ButtonText => IsConnected ? "연결 해제" : "연결";

        // Nullable 경고 해결을 위해 '?' 추가
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}