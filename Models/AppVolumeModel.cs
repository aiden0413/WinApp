using System;
using System.ComponentModel;
using System.Windows.Media;
using NAudio.CoreAudioApi;

namespace winapp.Models
{
    public class AppVolumeModel : INotifyPropertyChanged
    {
        private readonly AudioSessionControl? _session;
        private float _volume;
        private bool _isMuted;
        private float _previousVolume = 50f;

        public string ProcessName { get; set; }
        public string AppName { get; set; }
        public ImageSource? AppIcon { get; set; }

        public string MuteIcon => _isMuted ? "🔇" : "🔊";

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged(nameof(IsMuted));
                    OnPropertyChanged(nameof(MuteIcon));
                    
                    // 볼륨 바는 건드리지 않고 윈도우 세션의 Mute 속성만 토글
                    ApplyMuteToSession();
                }
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                if (Math.Abs(_volume - value) > 0.01f)
                {
                    _volume = value;
                    if (_volume > 0)
                    {
                        _previousVolume = _volume;
                    }
                    OnPropertyChanged(nameof(Volume));
                    
                    // 음소거 상태가 아닐 때만 실제 세션 볼륨 조절
                    if (!_isMuted)
                    {
                        ApplyVolumeToSession();
                    }
                }
            }
        }

        public AppVolumeModel(string processName, string appName, ImageSource? appIcon, AudioSessionControl session)
        {
            ProcessName = processName;
            AppName = appName;
            AppIcon = appIcon;
            _session = session;

            if (_session != null)
            {
                _volume = _session.SimpleAudioVolume.Volume * 100f;
                _isMuted = _session.SimpleAudioVolume.Mute; // 윈도우 실제 음소거 상태 동기화

                if (_volume > 0)
                {
                    _previousVolume = _volume;
                }
            }
        }

        public void UpdateVolumeFromSession()
        {
            if (_session != null)
            {
                try
                {
                    float sessionVol = _session.SimpleAudioVolume.Volume * 100f;
                    bool sessionMute = _session.SimpleAudioVolume.Mute;

                    if (_isMuted != sessionMute)
                    {
                        _isMuted = sessionMute;
                        OnPropertyChanged(nameof(IsMuted));
                        OnPropertyChanged(nameof(MuteIcon));
                    }

                    if (!_isMuted && sessionVol > 0 && Math.Abs(_volume - sessionVol) > 0.1f)
                    {
                        _volume = sessionVol;
                        _previousVolume = sessionVol;
                        OnPropertyChanged(nameof(Volume));
                    }
                }
                catch { }
            }
        }

        private void ApplyVolumeToSession()
        {
            if (_session != null)
            {
                try
                {
                    _session.SimpleAudioVolume.Volume = _volume / 100f;
                }
                catch { }
            }
        }

        private void ApplyMuteToSession()
        {
            if (_session != null)
            {
                try
                {
                    // 볼륨바 값은 유지하고 Mute 플래그만 변경
                    _session.SimpleAudioVolume.Mute = _isMuted;
                }
                catch { }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}