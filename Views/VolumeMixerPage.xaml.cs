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

namespace winapp
{
    public class AppVolumeModel : INotifyPropertyChanged
    {
        private readonly AudioSessionControl _session;
        private float _volume;
        
        public float PreviousVolume { get; set; } = 50f;

        public string ProcessName { get; set; }
        public string AppName { get; set; }
        public ImageSource AppIcon { get; set; }

        // 음소거 상태에 따라 아이콘 변경 (0이면 음소거 🔇, 아니면 🔊)
        public string MuteIcon => _volume <= 0 ? "🔇" : "🔊";

        public float Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    if (_volume > 0)
                    {
                        PreviousVolume = _volume;
                    }

                    _volume = value;
                    OnPropertyChanged(nameof(Volume));
                    OnPropertyChanged(nameof(MuteIcon)); // 아이콘 갱신 알림

                    if (_session != null)
                    {
                        try
                        {
                            _session.SimpleAudioVolume.Volume = _volume / 100f;
                        }
                        catch { }
                    }
                }
            }
        }

        public AppVolumeModel(string processName, string appName, ImageSource appIcon, AudioSessionControl session)
        {
            ProcessName = processName;
            AppName = appName;
            AppIcon = appIcon;
            _session = session;

            if (_session != null)
            {
                _volume = _session.SimpleAudioVolume.Volume * 100f;
                if (_volume > 0) PreviousVolume = _volume;
            }
        }

        public void UpdateVolumeFromSession()
        {
            if (_session != null)
            {
                try
                {
                    float actualVol = _session.SimpleAudioVolume.Volume * 100f;
                    if (Math.Abs(_volume - actualVol) > 0.1f)
                    {
                        if (_volume > 0) PreviousVolume = _volume;
                        _volume = actualVol;
                        OnPropertyChanged(nameof(Volume));
                        OnPropertyChanged(nameof(MuteIcon));
                    }
                }
                catch { }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class VolumeMixerPage : Page
    {
        private MMDeviceEnumerator _deviceEnumerator;
        private MMDevice _defaultDevice;
        private DispatcherTimer _refreshTimer;
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
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
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

        // 마스터 아이콘 텍스트 업데이트 헬퍼 메서드
        private void UpdateMasterIcon(double volume)
        {
            if (TxtMasterMuteIcon != null)
            {
                TxtMasterMuteIcon.Text = volume <= 0 ? "🔇" : "🔊";
            }
        }

        // 마스터 스피커 아이콘 클릭 시 음소거 토글
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

        // 개별 앱 스피커 아이콘 클릭 시 음소거 토글
        private void AppMute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var appModel = button?.DataContext as AppVolumeModel;
                if (appModel != null)
                {
                    if (appModel.Volume > 0)
                    {
                        appModel.PreviousVolume = appModel.Volume;
                        appModel.Volume = 0;
                    }
                    else
                    {
                        appModel.Volume = appModel.PreviousVolume > 0 ? appModel.PreviousVolume : 50;
                    }
                }
            }
            catch { }
        }

        private void LoadActiveAudioSessions()
        {
            try
            {
                if (_defaultDevice == null) return;

                var sessions = _defaultDevice.AudioSessionManager.Sessions;
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

                        Process process = null;
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
                        if (string.IsNullOrEmpty(processName) || processName.ToLower() == "system" || processName.ToLower() == "svchost")
                            continue;

                        currentProcessNames.Add(processName);

                        var existingItem = AppVolumes.FirstOrDefault(x => x.ProcessName == processName);
                        if (existingItem != null)
                        {
                            existingItem.UpdateVolumeFromSession();
                        }
                        else
                        {
                            string appName = processName;
                            ImageSource iconSource = null;
                            
                            try
                            {
                                iconSource = ExtractIcon(process);
                            }
                            catch { }

                            AppVolumes.Add(new AppVolumeModel(processName, appName, iconSource, session));
                        }
                    }
                    catch { }
                }

                var closedItems = AppVolumes.Where(x => !currentProcessNames.Contains(x.ProcessName)).ToList();
                foreach (var item in closedItems)
                {
                    AppVolumes.Remove(item);
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

        private ImageSource ExtractIcon(Process process)
        {
            try
            {
                string exePath = null;

                // 1차 시도: 일반적인 MainModule 접근
                try
                {
                    exePath = process.MainModule?.FileName;
                }
                catch { }

                // 2차 시도: 권한 문제 등으로 실패한 경우 OpenProcess API를 이용해 안전하게 경로 획득
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

                // 3차 시도: StartInfo 경로 확인
                if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                {
                    try { exePath = process.StartInfo?.FileName; } catch { }
                }

                // 유효한 경로를 찾았을 경우 아이콘 추출
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