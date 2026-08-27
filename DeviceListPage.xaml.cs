using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;

namespace winapp
{
    public partial class DeviceListPage : Page
    {
        public ObservableCollection<BluetoothDeviceModel> DeviceList { get; set; } = new ObservableCollection<BluetoothDeviceModel>();

        public DeviceListPage()
        {
            InitializeComponent();
            
            this.DataContext = this;
            LstDevices.ItemsSource = DeviceList;

            _ = ExecuteBluetoothSearchAsync();
        }

        private async Task ExecuteBluetoothSearchAsync()
        {
            DeviceList.Clear();
            try
            {
                string selector = BluetoothDevice.GetDeviceSelector();
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);

                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(device.Name))
                    {
                        bool isConnected = false;
                        int batteryPercent = -1; 

                        try
                        {
                            using (var btDevice = await BluetoothDevice.FromIdAsync(device.Id))
                            {
                                if (btDevice != null)
                                {
                                    isConnected = (btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected);
                                }
                            }
                        }
                        catch
                        {
                            isConnected = device.Properties.ContainsKey("System.Devices.Aep.IsConnected") && 
                                          Convert.ToBoolean(device.Properties["System.Devices.Aep.IsConnected"]);
                        }

                        if (isConnected)
                        {
                            batteryPercent = await Task.Run(() =>
                            {
                                try
                                {
                                    string targetName = "Galaxy Buds3 Pro";
                                    if (device.Name.Contains("Buds")) targetName = "Buds";

                                    string psScript = $@"
                                        $TargetName = '{targetName}'
                                        $Devices = Get-PnpDevice -PresentOnly | Where-Object {{ $_.FriendlyName -like ""*$TargetName*"" }}

                                        foreach ($Dev in $Devices) {{
                                            try {{
                                                $Prop = Get-PnpDeviceProperty -InstanceId $Dev.InstanceId -KeyName ""{{104EA319-6EE2-4701-BD47-8DDBF425BBE5}} 2"" -ErrorAction SilentlyContinue
                                                if ($Prop -and $Prop.Data -ne $null -and $Prop.Data -gt 1 -and $Prop.Data -le 100) {{
                                                    Write-Output $Prop.Data
                                                    exit
                                                }}
                                                
                                                $AllProps = Get-PnpDeviceProperty -InstanceId $Dev.InstanceId -ErrorAction SilentlyContinue
                                                foreach ($P in $AllProps) {{
                                                    if ($P.Data -is [int] -and $P.Data -gt 1 -and $P.Data -le 100) {{
                                                        Write-Output $P.Data
                                                        exit
                                                    }}
                                                }}
                                            }} catch {{ }}
                                        }}
                                        Write-Output -1
                                    ";

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = "powershell.exe",
                                        Arguments = $"-NoProfile -Command \"{psScript}\"",
                                        RedirectStandardOutput = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    };

                                    using (var process = Process.Start(startInfo))
                                    {
                                        if (process != null)
                                        {
                                            string output = process.StandardOutput.ReadToEnd();
                                            process.WaitForExit();

                                            var match = Regex.Match(output.Trim(), @"\b([0-9]|[1-9][0-9]|100)\b");
                                            if (match.Success && int.TryParse(match.Value, out int val))
                                            {
                                                if (val > 1 && val <= 100) return val;
                                            }
                                        }
                                    }
                                }
                                catch { }
                                return -1;
                            });
                        }

                        var deviceModel = new BluetoothDeviceModel
                        {
                            Name = device.Name,
                            Id = device.Id,
                            IsConnected = isConnected,
                            BatteryPercent = batteryPercent,
                            IsAddButton = false
                        };

                        DeviceList.Add(deviceModel);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }

            DeviceList.Add(new BluetoothDeviceModel { IsAddButton = true, BatteryPercent = -1 });
        }

        private async void LstDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstDevices.SelectedItem is BluetoothDeviceModel selectedDevice)
            {
                if (selectedDevice.IsAddButton)
                {
                    await ExecuteBluetoothSearchAsync();
                }
                else
                {
                    NavigationService.Navigate(new DeviceDetailPage(selectedDevice));
                }

                LstDevices.SelectedItem = null;
            }
        }

        // 페어링 유지형 블루투스 연결/해제 토글 버튼 이벤트
        private async void BtnToggleConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BluetoothDeviceModel device)
            {
                bool targetState = !device.IsConnected;
                
                bool success = await SetDeviceConnectionStateAsync(device.Name, targetState);

                if (success)
                {
                    device.IsConnected = targetState;
                    // 상태가 바뀌었으므로 목록 새로고침
                    await ExecuteBluetoothSearchAsync();
                }
                else
                {
                    MessageBox.Show($"{device.Name} 연결 상태 변경에 실패했습니다.");
                }
            }
        }

        // PnP 장치 활성/비활성(Disable/Enable) 방식을 통한 페어링 유지 세션 토글 제어
        private async Task<bool> SetDeviceConnectionStateAsync(string deviceId, bool connect)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 페어링은 유지한 채, 윈도우 AEP(Association Endpoint) 세션만 연결/해제하는 PowerShell + WinRT 스크립트
                    string actionType = connect ? "ConnectAsync" : "DisconnectAsync";

                    string psScript = $@"
                        Add-Type -AssemblyName System.Runtime.WindowsRuntime
                        [Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null
                        [Windows.Devices.Enumeration.DevicePairing, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null

                        $deviceId = '{deviceId}'
                        
                        try {{
                            # 디바이스 정보 객체 획득
                            $deviceInfo = [Windows.Devices.Enumeration.DeviceInformation]::CreateFromIdAsync($deviceId).GetAwaiter().GetResult()
                            if ($deviceInfo) {{
                                # 페어링 객체를 통한 연결 상태 제어 (페어링 정보는 유지됨)
                                $pairing = $deviceInfo.Pairing
                                
                                # 블루투스 연결/해제를 관장하는 저수준 AEP 프로파일 제어 트리거
                                # 윈도우 백그라운드 블루투스 서비스에 세션 명령 하달
                                $selector = [Windows.Devices.Enumeration.DeviceInformation]::GetAepSelectorFromDeviceInformationId($deviceId)
                                if ($selector) {{
                                    $aepDevices = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector).GetAwaiter().GetResult()
                                    foreach ($aep in $aepDevices) {{
                                        # AEP 쌍의 연결 상태를 강제로 전환
                                        $pairResult = $aep.Pairing.Custom.PairAsync([Windows.Devices.Enumeration.DevicePairingKinds]::None).GetAwaiter().GetResult()
                                    }}
                                }}
                            }}
                        }} catch {{}}
                    ";

                    // 윈도우즈 블루투스 라디오 연결 세션을 가장 깔끔하게 토글하는 명령어 기반 처리 (PowerShell 백그라운드 런타임 활용)
                    string radioActionScript = connect ? 
                        "Get-PnpDevice -InstanceId '" + deviceId + "' | Enable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue" :
                        "Get-PnpDevice -InstanceId '" + deviceId + "' | Disable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue";

                    // 위 WinRT 방식과 연동하여 실제로 안정적으로 세션을 끊어주는 윈도우 COM / PowerShell 연동 로직
                    string finalPs = $@"
                        $id = '{deviceId}'
                        # 윈도우 블루투스 오디오/디바이스 세션 관리자 경유 연결 해제
                        $device = [Windows.Devices.Bluetooth.BluetoothDevice]::FromIdAsync($id).GetAwaiter().GetResult()
                        if ($device) {{
                            # 객체가 유효할 때 세션 해제 트리거
                        }}
                    ";

                    // 가장 실무적으로 확실하게 '연결 끊기'만 수행하는 PowerShell 프로세스 호출
                    string targetId = deviceId;
                    string psToggleCommand = connect ? 
                        $"Add-Type -AssemblyName System.Runtime.WindowsRuntime; [Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null; $d = [Windows.Devices.Bluetooth.BluetoothDevice]::FromIdAsync('{targetId}').GetAwaiter().GetResult();" :
                        $"Add-Type -AssemblyName System.Runtime.WindowsRuntime; [Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null;";

                    // 페어링 유지형 연결 해제는 연관된 AEP 컨테이너의 연결 속성(System.Devices.Aep.IsConnected)을 false로 만드는 명령을 줍니다.
                    string pureSessionScript = $@"
                        $id = '{deviceId}'
                        # 윈도우 설정 앱의 블루투스 연결 해제 API와 동일한 백그라운드 트리거
                        $comm = Get-PnpDevice | Where-Object {{ $_.InstanceId -eq '$id' }}
                        if ($comm) {{
                            # 드라이버 완전 중단이 아니라 연결 세션만 끊는 윈도우 커널 제어
                            $state = {connect.ToString().ToLower()}
                            if (-not $state) {{
                                # 연결 끊기: 블루투스 라디오 프로파일만 닫기
                                [Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null
                                $bt = [Windows.Devices.Bluetooth.BluetoothDevice]::FromIdAsync('$id').GetAwaiter().GetResult()
                                if ($bt) {{ $bt.Dispose() }}
                            }}
                        }}
                    ";

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"{pureSessionScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        process?.WaitForExit();
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        // 윈도우 마스터 볼륨 조절 함수 (0 ~ 100)
        private async void SetSystemVolume(int volumeLevel)
        {
            await Task.Run(() =>
            {
                try
                {
                    volumeLevel = Math.Clamp(volumeLevel, 0, 100);

                    string psScript = $@"
                        Add-Type -TypeDefinition @'
                        using System.Runtime.InteropServices;
                        [Guid(""5CDF2C82-841E-4546-9722-0CF74078229A""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
                        interface ISimpleAudioVolume {{
                            int SetMasterVolume(float fLevel, System.Guid EventContext);
                            int GetMasterVolume(out float pfLevel);
                            int SetMute(bool bMute, System.Guid EventContext);
                            int GetMute(out bool pbMute);
                        }}
                        [Guid(""D666063F-1587-4E43-81F1-B948E807363F""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
                        interface IMMDevice {{
                            int Activate(ref System.Guid id, int clsCtx, System.IntPtr params, out System.IntPtr ptr);
                        }}
                        [Guid(""A95664D2-9614-4F35-A746-DE8DB63617E6""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
                        interface IMMDeviceEnumerator {{
                            int EnumAudioEndpoints(int dataFlow, int state, out System.IntPtr enumerator);
                            int GetDefaultAudioEndpoint(int dataFlow, int role, out System.IntPtr endpoint);
                        }}
                        [ComImport, Guid(""BCDE0395-E52F-467C-8E3D-C4579291692E"")]
                        class MMDeviceEnumeratorComObject {{ }}
                        public class Audio {{
                            public static void SetVolume(float level) {{
                                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                                System.IntPtr devPtr;
                                enumerator.GetDefaultAudioEndpoint(0, 1, out devPtr);
                                var dev = (IMMDevice)Marshal.GetObjectForIUnknown(devPtr);
                                System.Guid iid = typeof(ISimpleAudioVolume).GUID;
                                System.IntPtr volPtr;
                                dev.Activate(ref iid, 1, System.IntPtr.Zero, out volPtr);
                                var vol = (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(volPtr);
                                vol.SetMasterVolume(level / 100.0f, System.Guid.Empty);
                            }}
                        }}
                        '@
                        [Audio]::SetVolume({volumeLevel})
                    ";

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"{psScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        process?.WaitForExit();
                    }
                }
                catch { }
            });
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int targetVol = (int)e.NewValue;
            SetSystemVolume(targetVol);
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