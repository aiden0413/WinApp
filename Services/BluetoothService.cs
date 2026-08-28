using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using System.IO;

namespace winapp.Services
{
    public static class BluetoothService
    {
        // 블루투스 기기 목록 및 초기 배터리 검색
        public static async Task<ObservableCollection<BluetoothDeviceModel>> GetDevicesAsync()
        {
            var deviceList = new ObservableCollection<BluetoothDeviceModel>();
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
                                string tempScriptPath = string.Empty;
                                try
                                {
                                    string targetName = "Galaxy Buds3 Pro";
                                    if (device.Name.Contains("Buds")) targetName = "Buds";

                                    string psCode = $@"
                                        $TargetName = '{targetName}'
                                        $Dev = Get-PnpDevice -PresentOnly | Where-Object {{ $_.FriendlyName -like ""*$TargetName*"" -and $_.FriendlyName -like ""*Hands-Free AG*"" }} | Select-Object -First 1

                                        if ($Dev) {{
                                            try {{
                                                $Prop = Get-PnpDeviceProperty -InstanceId $Dev.InstanceId -KeyName ""{{104EA319-6EE2-4701-BD47-8DDBF425BBE5}} 2"" -ErrorAction SilentlyContinue
                                                if ($Prop -and $Prop.Data -ne $null) {{
                                                    $val = [int]$Prop.Data
                                                    if ($val -ge 0 -and $val -le 100) {{
                                                        Write-Output $val
                                                        exit
                                                    }}
                                                }}
                                            }} catch {{ }}
                                        }}
                                        Write-Output -1
                                    ";

                                    tempScriptPath = Path.Combine(Path.GetTempPath(), "get_bt_battery.ps1");
                                    File.WriteAllText(tempScriptPath, psCode);

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = "powershell.exe",
                                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
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

                                            if (int.TryParse(output.Trim(), out int val) && val >= 0 && val <= 100)
                                            {
                                                return val;
                                            }
                                        }
                                    }
                                }
                                catch { }
                                finally
                                {
                                    try { if (File.Exists(tempScriptPath)) File.Delete(tempScriptPath); } catch { }
                                }
                                return -1;
                            });
                        }
                        else
                        {
                            batteryPercent = -1;
                        }

                        deviceList.Add(new BluetoothDeviceModel
                        {
                            Name = device.Name,
                            Id = device.Id,
                            IsConnected = isConnected,
                            BatteryPercent = batteryPercent,
                            IsAddButton = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bluetooth Search Error: {ex.Message}");
            }

            deviceList.Add(new BluetoothDeviceModel { IsAddButton = true, BatteryPercent = -1 });
            return deviceList;
        }

        // 백그라운드 실시간 상태 갱신 (0.5초 주기 폴링 - 연결 상태 및 배터리 동시 체크)
        public static async Task UpdateDeviceStatesAsync(ObservableCollection<BluetoothDeviceModel> devices)
        {
            while (true)
            {
                await Task.Delay(500); // 0.5초(500ms) 간격

                foreach (var device in devices)
                {
                    if (device.IsAddButton) continue;

                    bool currentConnected = false;
                    try
                    {
                        // 1. 공식 API로 현재 실제 연결 상태 확인
                        using (var btDevice = await BluetoothDevice.FromIdAsync(device.Id))
                        {
                            if (btDevice != null)
                            {
                                currentConnected = (btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected);
                            }
                        }
                    }
                    catch
                    {
                        // 예외 시 AEP 속성으로 대체 확인
                        // (device 객체에 Properties가 없다면 최신 상태를 위해 장치 검색을 거치거나 캐시된 값 사용)
                    }

                    // 2. 연결 상태가 이전과 달라졌다면 즉시 업데이트 (INotifyPropertyChanged에 의해 UI 자동 갱신)
                    if (device.IsConnected != currentConnected)
                    {
                        device.IsConnected = currentConnected;
                    }

                    // 3. 연결되어 있는 Buds 기기라면 배터리 갱신 수행
                    if (device.IsConnected && device.Name.Contains("Buds"))
                    {
                        int newBattery = await Task.Run(() =>
                        {
                            string tempScriptPath = string.Empty;
                            try
                            {
                                string targetName = "Buds";
                                string psCode = $@"
                                    $TargetName = '{targetName}'
                                    $Dev = Get-PnpDevice -PresentOnly | Where-Object {{ $_.FriendlyName -like ""*$TargetName*"" -and $_.FriendlyName -like ""*Hands-Free AG*"" }} | Select-Object -First 1

                                    if ($Dev) {{
                                        try {{
                                            $Prop = Get-PnpDeviceProperty -InstanceId $Dev.InstanceId -KeyName ""{{104EA319-6EE2-4701-BD47-8DDBF425BBE5}} 2"" -ErrorAction SilentlyContinue
                                            if ($Prop -and $Prop.Data -ne $null) {{
                                                $val = [int]$Prop.Data
                                                if ($val -ge 0 -and $val -le 100) {{
                                                    Write-Output $val
                                                    exit
                                                }}
                                            }}
                                        }} catch {{ }}
                                    }}
                                    Write-Output -1
                                ";

                                tempScriptPath = Path.Combine(Path.GetTempPath(), "get_bt_battery_poll.ps1");
                                File.WriteAllText(tempScriptPath, psCode);

                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
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

                                        if (int.TryParse(output.Trim(), out int val) && val >= 0 && val <= 100)
                                        {
                                            return val;
                                        }
                                    }
                                }
                            }
                            catch { }
                            finally
                            {
                                try { if (File.Exists(tempScriptPath)) File.Delete(tempScriptPath); } catch { }
                            }
                            return -1;
                        });

                        if (newBattery != -1 && device.BatteryPercent != newBattery)
                        {
                            device.BatteryPercent = newBattery;
                        }
                    }
                    else if (!device.IsConnected && device.BatteryPercent != -1)
                    {
                        // 연결이 끊겼다면 배터리 표시 초기화 (-1)
                        device.BatteryPercent = -1;
                    }
                }
            }
        }

        public static async Task<bool> SetDeviceConnectionStateAsync(string deviceId, bool connect)
        {
            try
            {
                // 1. 전달받은 deviceId로 장치 정보 로드
                var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
                if (deviceInfo == null)
                {
                    Debug.WriteLine("블루투스 장치를 찾을 수 없습니다.");
                    return false;
                }

                var customPairing = deviceInfo.Pairing.Custom;
                
                customPairing.PairingRequested += (sender, args) =>
                {
                    args.Accept();
                };

                if (connect)
                {
                    // 연결(페어링) 요청
                    var result = await customPairing.PairAsync(DevicePairingKinds.ConfirmOnly, DevicePairingProtectionLevel.None);
                    return result.Status == DevicePairingResultStatus.Paired || result.Status == DevicePairingResultStatus.AlreadyPaired;
                }
                else
                {
                    // 연결 해제 (언페어링) 처리
                    try
                    {
                        Debug.WriteLine($"[디버그] 연결 해제(언페어링) 시도 시작 - DeviceId: {deviceId}");
                        
                        // UnpairAsync()는 DeviceUnpairingResult를 반환함
                        DeviceUnpairingResult unpairResult = await deviceInfo.Pairing.UnpairAsync();
                        
                        Debug.WriteLine($"[디버그] 언페어링 결과 Status: {unpairResult.Status}");
                        
                        // 올바른 상태 값인 DeviceUnpairingResultStatus.Unpaired 사용
                        return unpairResult.Status == DeviceUnpairingResultStatus.Unpaired;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[디버그] 연결 해제 중 예외 발생: {ex.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BT Connection State Error: {ex.Message}");
                return false;
            }
        }
    }
}