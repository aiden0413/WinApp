using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;
using winapp.Models;

namespace winapp
{
    public partial class VolumeMixerPage : Page
    {
        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _defaultDevice;
        private DispatcherTimer? _refreshTimer;
        private double _lastMasterVolume = 50;

        public ObservableCollection<AppVolumeModel> AppVolumes { get; set; } = new ObservableCollection<AppVolumeModel>();

        public VolumeMixerPage()
        {
            InitializeComponent();
            LstAppVolumes.ItemsSource = AppVolumes;

            InitializeAudioController();
            StartRealtimeTimer();
        }

        private void InitializeAudioController()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                if (_defaultDevice != null)
                {
                    float currentMasterVolume = _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                    SliderMasterVolume.Value = currentMasterVolume;
                    if (currentMasterVolume > 0) _lastMasterVolume = currentMasterVolume;
                    UpdateMasterIcon(currentMasterVolume);
                }

                LoadActiveAudioSessions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio Init Error: {ex.Message}");
            }
        }

        private void StartRealtimeTimer()
        {
            _refreshTimer = new DispatcherTimer();
            // 문제 2번 해결: 반응 속도를 높이기 위해 주기 단축 (1초 -> 0.5초)
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
            _refreshTimer.Tick += (s, e) => 
            {
                try
                {
                    if (_defaultDevice != null && SliderMasterVolume != null)
                    {
                        float currentMaster = _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                        if (Math.Abs(SliderMasterVolume.Value - currentMaster) > 0.5f)
                        {
                            if (SliderMasterVolume.Value > 0) _lastMasterVolume = SliderMasterVolume.Value;
                            SliderMasterVolume.Value = currentMaster;
                            UpdateMasterIcon(currentMaster);
                        }
                    }

                    LoadActiveAudioSessions();
                }
                catch { }
            };
            _refreshTimer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _refreshTimer?.Stop();
            }
            catch { }
        }

        private void UpdateMasterIcon(double volume)
        {
            if (TxtMasterMuteIcon != null)
            {
                TxtMasterMuteIcon.Text = volume <= 0 ? "🔇" : "🔊";
            }
        }

        private void MasterMute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_defaultDevice != null && SliderMasterVolume != null)
                {
                    if (SliderMasterVolume.Value > 0)
                    {
                        _lastMasterVolume = SliderMasterVolume.Value;
                        SliderMasterVolume.Value = 0;
                    }
                    else
                    {
                        SliderMasterVolume.Value = _lastMasterVolume > 0 ? _lastMasterVolume : 50;
                    }
                    UpdateMasterIcon(SliderMasterVolume.Value);
                }
            }
            catch { }
        }

        private void AppMute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var appModel = button?.DataContext as AppVolumeModel;
                if (appModel != null)
                {
                    // 음소거 상태를 반전시킴 (슬라이더 바 위치는 그대로 유지됨)
                    appModel.IsMuted = !appModel.IsMuted;
                }
            }
            catch { }
        }

        private void LoadActiveAudioSessions()
        {
            try
            {
                if (_deviceEnumerator == null) return;
                
                _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_defaultDevice == null) return;

                var sessionManager = _defaultDevice.AudioSessionManager;
                if (sessionManager == null) return;

                var sessions = sessionManager.Sessions;
                if (sessions == null) return;

                var currentProcessNames = new System.Collections.Generic.HashSet<string>();

                for (int i = 0; i < sessions.Count; i++)
                {
                    try
                    {
                        var session = sessions[i];
                        if (session == null) continue;

                        uint processId = session.GetProcessID;
                        if (processId == 0) continue;

                        Process? process = null;
                        try
                        {
                            process = Process.GetProcessById((int)processId);
                        }
                        catch
                        {
                            continue;
                        }

                        if (process == null || process.HasExited) continue;

                        string processName = process.ProcessName;
                        if (string.IsNullOrEmpty(processName) || 
                            processName.Equals("system", StringComparison.OrdinalIgnoreCase) || 
                            processName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                            processName.Equals("audiodg", StringComparison.OrdinalIgnoreCase))
                            continue;

                        currentProcessNames.Add(processName);

                        // 이미 리스트에 존재하는 앱인지 확인
                        var existingItem = AppVolumes.FirstOrDefault(x => x.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
                        if (existingItem != null)
                        {
                            existingItem.UpdateVolumeFromSession();
                        }
                        else
                        {
                            // 새로 켜진 앱 발견 즉시 추가
                            string appName = process.MainWindowTitle;
                            if (string.IsNullOrEmpty(appName))
                            {
                                appName = processName; // 창 제목이 없으면 프로세스 이름 사용
                            }

                            ImageSource? iconSource = null;
                            try
                            {
                                iconSource = ExtractIcon(process);
                            }
                            catch { }

                            // UI 스레드에서 즉시 바인딩 컬렉션에 추가
                            Dispatcher.Invoke(() =>
                            {
                                // 한 번 더블 체크해서 중복 추가 원천 차단
                                if (!AppVolumes.Any(x => x.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    AppVolumes.Add(new Models.AppVolumeModel(processName, appName, iconSource, session));
                                }
                            });
                        }
                    }
                    catch { }
                }

                // 종료된 앱은 목록에서 칼같이 제거
                var closedItems = AppVolumes.Where(x => !currentProcessNames.Contains(x.ProcessName)).ToList();
                if (closedItems.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in closedItems)
                        {
                            AppVolumes.Remove(item);
                        }
                    });
                }
            }
            catch { }
        }

        // 아이콘을 못 가져왔을 때 null 반환
        private ImageSource? CreateDefaultIcon()
        {
            return null; 
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("psapi.dll", CharSet = CharSet.Auto)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        private ImageSource? ExtractIcon(Process process)
        {
            try
            {
                string? exePath = null;

                try
                {
                    exePath = process.MainModule?.FileName;
                }
                catch { }

                if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                {
                    IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, process.Id);
                    if (hProcess != IntPtr.Zero)
                    {
                        try
                        {
                            var sb = new System.Text.StringBuilder(1024);
                            if (GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, (uint)sb.Capacity) > 0)
                            {
                                exePath = sb.ToString();
                            }
                        }
                        finally
                        {
                            CloseHandle(hProcess);
                        }
                    }
                }

                if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                {
                    try { exePath = process.StartInfo?.FileName; } catch { }
                }

                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                        {
                            if (sysIcon != null)
                            {
                                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                    sysIcon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                            }
                        }
                        return null;
                    });
                }
            }
            catch { }
            return null;
        }

        private void SliderMasterVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (_defaultDevice != null && SliderMasterVolume != null)
                {
                    float newVolume = (float)(SliderMasterVolume.Value / 100.0);
                    if (Math.Abs(_defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar - newVolume) > 0.005f)
                    {
                        _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
                    }
                    UpdateMasterIcon(SliderMasterVolume.Value);
                }
            }
            catch { }
        }
    }
}