using System;
using System.Collections.Generic;
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

        // 아이콘 중복 생성 및 메모리 폭발을 막기 위한 캐시 딕셔너리
        private readonly Dictionary<string, ImageSource?> _iconCache = new Dictionary<string, ImageSource?>(StringComparer.OrdinalIgnoreCase);

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
                    bool isMuted = _defaultDevice.AudioEndpointVolume.Mute; // 마스터 음소거 상태 가져오기
                    
                    SliderMasterVolume.Value = currentMasterVolume;
                    if (currentMasterVolume > 0) _lastMasterVolume = currentMasterVolume;
                    UpdateMasterIcon(isMuted, currentMasterVolume);
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
            // CPU 갈굼과 메모리 누수를 막기 위해 주기를 1초로 완화
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
            _refreshTimer.Tick += (s, e) => 
            {
                try
                {
                    if (_defaultDevice != null && SliderMasterVolume != null)
                    {
                        float currentMaster = _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                        bool currentMute = _defaultDevice.AudioEndpointVolume.Mute;

                        if (Math.Abs(SliderMasterVolume.Value - currentMaster) > 0.5f)
                        {
                            if (currentMaster > 0) _lastMasterVolume = currentMaster;
                            SliderMasterVolume.Value = currentMaster;
                        }
                        UpdateMasterIcon(currentMute, currentMaster);
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
                _refreshTimer = null;
            }
            catch { }
        }

        private void UpdateMasterIcon(bool isMuted, double volume)
        {
            if (TxtMasterMuteIcon != null)
            {
                TxtMasterMuteIcon.Text = (isMuted || volume <= 0) ? "🔇" : "🔊";
            }
        }

        private void MasterMute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_defaultDevice != null)
                {
                    bool currentMute = _defaultDevice.AudioEndpointVolume.Mute;
                    _defaultDevice.AudioEndpointVolume.Mute = !currentMute; // 윈도우 마스터 음소거 토글
                    
                    float currentVolume = _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                    UpdateMasterIcon(!currentMute, currentVolume);
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

                        var existingItem = AppVolumes.FirstOrDefault(x => x.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
                        if (existingItem != null)
                        {
                            existingItem.UpdateVolumeFromSession();
                        }
                        else
                        {
                            string appName = process.MainWindowTitle;
                            if (string.IsNullOrEmpty(appName))
                            {
                                appName = processName;
                            }

                            // 아이콘 캐싱 적용: 이미 추출한 적이 있다면 캐시된 아이콘 재사용
                            ImageSource? iconSource = null;
                            if (_iconCache.ContainsKey(processName))
                            {
                                iconSource = _iconCache[processName];
                            }
                            else
                            {
                                try
                                {
                                    iconSource = ExtractIcon(process);
                                }
                                catch { }
                                _iconCache[processName] = iconSource;
                            }

                            Dispatcher.Invoke(() =>
                            {
                                if (!AppVolumes.Any(x => x.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    AppVolumes.Add(new Models.AppVolumeModel(processName, appName, iconSource, session));
                                }
                            });
                        }
                    }
                    catch { }
                }

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
                                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                    sysIcon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                bitmapSource.Freeze(); // 메모리 최적화 및 크로스 스레드 안전성 확보
                                return bitmapSource;
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
                        // 볼륨을 올리면 자동으로 음소거 해제되도록 처리할 수도 있음
                        if (newVolume > 0 && _defaultDevice.AudioEndpointVolume.Mute)
                        {
                            _defaultDevice.AudioEndpointVolume.Mute = false;
                        }
                    }
                    UpdateMasterIcon(_defaultDevice.AudioEndpointVolume.Mute, SliderMasterVolume.Value);
                }
            }
            catch { }
        }
    }
}