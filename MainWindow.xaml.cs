using System;
using System.Windows;
using System.Windows.Navigation;

namespace winapp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 헤더와 MainFrame 네비게이션 연동
            MyHeader.TargetFrame = MainFrame;
            MainFrame.Navigated += (s, e) => { MyHeader.UpdateNavigationButtons(); };
            
            // ★ 기존 DeviceListPage 대신 조립형 대시보드 페이지로 첫 화면 진입
            MainFrame.Navigate(new MainDashboardPage());

            this.Loaded += (s, e) => {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 1; 
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            };
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 작업표시줄을 제외한 전체 모니터 작업 영역 가져오기
            var workArea = System.Windows.SystemParameters.WorkArea;

            // 오른쪽 절반 영역의 시작 X 좌표 (전체 너비의 딱 절반 지점)
            double rightHalfStart = workArea.Left + (workArea.Width / 2);
            double rightHalfWidth = workArea.Width / 2;

            // 오른쪽 절반 영역의 중앙에 창이 오도록 X, Y 좌표 계산
            this.Left = rightHalfStart + (rightHalfWidth - this.Width) / 2;
            this.Top = workArea.Top + (workArea.Height - this.Height) / 2;

            // 💡 실행될 때만 잠깐 맨 앞으로 강제 호출 후 즉시 해제
            this.Topmost = true;
            this.Topmost = false;
        }
    }
}