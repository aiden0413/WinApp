using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace winapp.Services
{
    public static class AudioService
    {
        public static void SetSystemVolume(int volumeLevel)
        {
            Task.Run(() =>
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
    }
}