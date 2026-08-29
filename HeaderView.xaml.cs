using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace winapp
{
    public partial class HeaderView : UserControl
    {
        public HeaderView()
        {
            InitializeComponent();
        }

        // 부모 창(MainWindow)의 Frame을 제어하기 위한 참조 속성 또는 메서드 연결 필요
        public Frame TargetFrame { get; set; }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (TargetFrame != null && TargetFrame.CanGoBack)
                TargetFrame.GoBack();
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            if (TargetFrame != null && TargetFrame.CanGoForward)
                TargetFrame.GoForward();
        }

        // 네비게이션 발생 시 화살표 상태 업데이트 (MainWindow에서 호출해 줌)
        public void UpdateNavigationButtons()
        {
            if (TargetFrame == null) return;

            BtnBack.Visibility = Visibility.Visible;
            BtnForward.Visibility = Visibility.Visible;

            BtnBack.IsEnabled = TargetFrame.CanGoBack;
            if (BtnBack.Content is System.Windows.Shapes.Path backPath)
            {
                backPath.Stroke = TargetFrame.CanGoBack 
                    ? Brushes.White 
                    : new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
            }

            BtnForward.IsEnabled = TargetFrame.CanGoForward;
            ForwardIcon.Stroke = TargetFrame.CanGoForward 
                ? Brushes.White 
                : new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
        }

        private void BtnSettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnHelpMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

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
    }
}