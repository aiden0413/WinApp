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
        // 블루투스 기기 목록 및 배터리 검색
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

                                    // 핵심: 'Hands-Free AG'가 포함된 PnP 장치를 직접 타겟팅하여 배터리 GUID 속성을 읽어옴
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

        // 블루투스 연결/해제 제어
        public static async Task<bool> SetDeviceConnectionStateAsync(string deviceId, bool connect)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string pureSessionScript = $@"
                        $id = '{deviceId}'
                        $comm = Get-PnpDevice | Where-Object {{ $_.InstanceId -eq '$id' }}
                        if ($comm) {{
                            $state = {connect.ToString().ToLower()}
                            if (-not $state) {{
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
    }
}