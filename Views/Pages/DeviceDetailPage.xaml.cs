using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace winapp
{
    public partial class DeviceDetailPage : Page
    {
        private BluetoothDeviceModel _device;

        public DeviceDetailPage(BluetoothDeviceModel device)
        {
            InitializeComponent();
            _device = device;

            // UI에 기본 데이터 매핑
            TxtName.Text = _device.Name;
            TxtId.Text = _device.Id;
            TxtStatus.Text = _device.StatusText;

            // 모든 속성 불러오기 실행
            LoadAllDeviceProperties();
        }

        private void LoadAllDeviceProperties()
        {
            string tempScriptPath = string.Empty;

            try
            {
                string targetName = _device.Name;
                string deviceId = _device.Id;

                string psCode = @"
param(
    [string]$TargetName,
    [string]$DeviceId
)

$Dev = Get-PnpDevice -PresentOnly | Where-Object { $_.FriendlyName -eq $TargetName -or $_.InstanceId -eq $DeviceId } | Select-Object -First 1

if (-not $Dev) {
    $Dev = Get-PnpDevice -PresentOnly | Where-Object { $_.FriendlyName -like ""*$TargetName*"" } | Select-Object -First 1
}

if ($Dev) {
    $Props = Get-PnpDeviceProperty -InstanceId $Dev.InstanceId -ErrorAction SilentlyContinue
    foreach ($p in $Props) {
        $val = ""[Null]""
        if ($p.Data -ne $null) {
            if ($p.Data -is [byte[]]) {
                $val = ""Byte[]: "" + ($p.Data -join ', ')
            } else {
                $val = $p.Data.ToString()
            }
        }
        ""$($p.KeyName)=$val""
    }
}
";

                tempScriptPath = Path.Combine(Path.GetTempPath(), "get_all_props.ps1");
                File.WriteAllText(tempScriptPath, psCode);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\" -TargetName \"{targetName}\" -DeviceId \"{deviceId}\"",
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

                        using (var reader = new StringReader(output))
                        {
                            string? line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                int eqIndex = line.IndexOf('=');
                                if (eqIndex > 0)
                                {
                                    string key = line.Substring(0, eqIndex);
                                    string val = line.Substring(eqIndex + 1);

                                    // 기존 스타일과 똑같이 동적으로 TextBlock 생성해서 추가
                                    AddPropertyUI(key, val);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddPropertyUI("Error", ex.Message);
            }
            finally
            {
                try { if (File.Exists(tempScriptPath)) File.Delete(tempScriptPath); } catch { }
            }
        }

        private void AddPropertyUI(string key, string value)
        {
            // 키 레이블
            var keyBlock = new TextBlock
            {
                Text = key,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 2)
            };

            // 값 레이블
            var valBlock = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };

            PanelProperties.Children.Add(keyBlock);
            PanelProperties.Children.Add(valBlock);
        }
    }
}