using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace winapp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            MainFrame.Navigated += MainFrame_Navigated;
            MainFrame.Navigate(new DeviceListPage());

            this.Loaded += (s, e) => {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 1; 
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            };
        }

        // [←] 뒤로 가기 버튼
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.CanGoBack)
            {
                MainFrame.GoBack();
            }
        }

        // [→] 앞으로 가기 버튼
        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.CanGoForward)
            {
                MainFrame.GoForward();
            }
        }

        // 메뉴 클릭 시 마우스 아래로 ContextMenu(드롭다운)가 뜨도록 처리
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // [파일 > 새로고침]
        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Refresh();
        }

        // [파일 > 종료]
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // [편집 > 설정]
        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("설정 기능은 추후 구현될 예정입니다.", "설정");
        }

        // [보기 > 도움말]
        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("블루투스 기기를 검색하고 카드를 눌러 상세 정보를 확인하거나 연결을 관리할 수 있습니다.", "도움말");
        }

        // 페이지 이동 시 화살표(←, →) 활성화/비활성화 상태 제어
        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            BtnBack.Visibility = Visibility.Visible;
            BtnForward.Visibility = Visibility.Visible;

            // 뒤로 가기 활성화 여부 (Path의 Stroke 색상 변경)
            BtnBack.IsEnabled = MainFrame.CanGoBack;
            if (BtnBack.Content is System.Windows.Shapes.Path backPath)
            {
                backPath.Stroke = MainFrame.CanGoBack 
                    ? Brushes.White 
                    : new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
            }

            // 앞으로 가기 활성화 여부 (Path의 Stroke 색상 변경)
            BtnForward.IsEnabled = MainFrame.CanGoForward;
            ForwardIcon.Stroke = MainFrame.CanGoForward 
                ? Brushes.White 
                : new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
        }

        // 설정 버튼 클릭 시 드롭다운 메뉴 바로 아래에 띄우기
        private void BtnSettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // 도움말 버튼 클릭 시 드롭다운 메뉴 바로 아래에 띄우기
        private void BtnHelpMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // 드롭다운 내부 메뉴 아이템 동작들
        private void MenuSettingsAction_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("환경 설정 페이지입니다.", "설정");
        }

        private void MenuResetAction_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("블루투스 장치를 초기화합니다.", "초기화");
        }

        private void MenuUserGuide_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("블루투스 기기 검색 및 연결 관리 가이드입니다.", "도움말");
        }

        private void MenuVersion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bluetooth Manager v1.0", "버전 정보");
        }


        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}