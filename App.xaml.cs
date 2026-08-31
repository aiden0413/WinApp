using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace winapp
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "WinApp_SingleInstance_Mutex_Unique_Key";
        private const string PipeName = "WinApp_SingleInstance_Pipe_Unique_Key";
        private CancellationTokenSource? _cts;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // 이미 실행 중인 경우: 파이프로 신호를 보내고 즉시 종료
                SendActivationSignalToFirstInstance();
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // 첫 번째 인스턴스만 파이프 서버를 열어 신호 대기
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenForActivationSignals(_cts.Token));
        }

        private void SendActivationSignalToFirstInstance()
        {
            try
            {
                using var clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                clientStream.Connect(500); // 0.5초 안에 접속 시도
                using var writer = new StreamWriter(clientStream);
                writer.WriteLine("ACTIVATE");
                writer.Flush();
            }
            catch { }
        }

        private async Task ListenForActivationSignals(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var serverStream = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    
                    // 비동기로 다른 인스턴스의 접속 대기
                    await serverStream.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(serverStream);
                    string? message = await reader.ReadLineAsync();

                    if (message == "ACTIVATE")
                    {
                        // UI 스레드에서 안전하게 창 활성화 수행
                        Current.Dispatcher.Invoke(() =>
                        {
                            if (Current.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.ShowMainWindowCustom();
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 파이프 오류 발생 시 짧게 대기 후 재시작
                    await Task.Delay(500, token);
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _cts?.Cancel();
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch { }

            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}