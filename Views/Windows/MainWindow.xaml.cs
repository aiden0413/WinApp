using System;
using System.ComponentModel; // CancelEventArgs를 위해 추가
using System.Windows;
using System.Windows.Navigation;

namespace winapp
{
    public partial class MainWindow : Window
    {
        private bool _isActuallyClosing = false; // 진짜 종료 여부 플래그

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

        // ==========================================
        // 🚀 시스템 트레이 및 닫기 버튼 가로채기 관련 메서드들
        // ==========================================

        // X(닫기) 버튼을 눌렀을 때 완전히 종료되지 않고 트레이로 숨겨지도록 가로챔
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isActuallyClosing)
            {
                e.Cancel = true; // 창이 꺼지는 걸 막음
                this.Hide();     // 창을 숨겨서 백그라운드 트레이로 상주시킴
            }
        }

        // 트레이 아이콘 더블클릭 시 창 다시 띄우기
        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        // 트레이 메뉴 '열기' 클릭 시
        private void TrayOpen_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        // 트레이 메뉴 '종료' 클릭 시 진짜 종료
        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            _isActuallyClosing = true;
            Application.Current.Shutdown();
        }

        // 창을 화면에 다시 소환하는 공통 메서드
        private void ShowMainWindow()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }

        // 백그라운드(숨겨진 상태)에서 창을 완벽하게 살려내는 안전 복원 메서드
        public void ShowMainWindowCustom()
        {
            // 1. 최소화 상태 해제
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            // 2. 숨겨져 있던 창을 다시 보이게 함
            this.Visibility = Visibility.Visible;
            this.Show();

            // 3. 포커스 강탈 및 최상단 깜빡임으로 렌더링 루프 강제 구동
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;

            // 4. DirectX 렌더링 서피스 강제 새로고침 (빈 화면 방지 핵심)
            this.InvalidateVisual();
        }
    }
}