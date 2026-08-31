using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace winapp
{
    public partial class FavoriteAppsView : UserControl
    {
        public ObservableCollection<FavoriteTabItem> Tabs { get; set; } = new ObservableCollection<FavoriteTabItem>();
        
        private FavoriteTabItem? _currentTab;
        public FavoriteTabItem? CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;

                foreach (var t in Tabs)
                {
                    t.IsSelected = (t == _currentTab);
                }

                if (_currentTab != null)
                {
                    _appList = _currentTab.Items;
                    FavoriteListBox.ItemsSource = _appList;
                }
            }
        }

        private ObservableCollection<FavoriteAppItem> _appList = new ObservableCollection<FavoriteAppItem>();
        private bool _isDeleteModeActive = false;

        public ICommand SelectTabCommand { get; }
        public ICommand AddTabCommand { get; }
        public ICommand DeleteTabCommand { get; }

        public FavoriteAppsView()
        {
            InitializeComponent();
            DataContext = this;

            // 탭 선택 커맨드
            SelectTabCommand = new RelayCommand(param =>
            {
                if (param is FavoriteTabItem tab && !tab.IsAddTabButton)
                {
                    CurrentTab = tab;
                }
            });

            // 새 탭 추가 커맨드
            AddTabCommand = new RelayCommand(_ =>
            {
                int realInsertIndex = Tabs.Count;
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].IsAddTabButton)
                    {
                        realInsertIndex = i;
                        break;
                    }
                }

                int actualTabCount = 0;
                foreach (var t in Tabs) if (!t.IsAddTabButton) actualTabCount++;

                var newTab = new FavoriteTabItem 
                { 
                    TabName = $"탭 {actualTabCount + 1}", 
                    IsAddTabButton = false, 
                    TextBlockVisibility = _isDeleteModeActive ? Visibility.Collapsed : Visibility.Visible,
                    TextBoxVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed
                };
                
                EnsureAddButtonForTab(newTab);
                
                if (_isDeleteModeActive)
                {
                    newTab.DeleteButtonVisibility = Visibility.Visible;
                }

                Tabs.Insert(realInsertIndex, newTab);
                CurrentTab = newTab;
                SaveAllTabs();
            });

            // 탭 삭제 커맨드
            DeleteTabCommand = new RelayCommand(param =>
            {
                if (param is FavoriteTabItem tabToDelete && !tabToDelete.IsAddTabButton)
                {
                    int actualTabCount = 0;
                    foreach (var t in Tabs) if (!t.IsAddTabButton) actualTabCount++;

                    if (actualTabCount <= 1)
                    {
                        MessageBox.Show("최소 1개의 탭은 유지되어야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    Tabs.Remove(tabToDelete);

                    if (CurrentTab == tabToDelete)
                    {
                        foreach (var t in Tabs)
                        {
                            if (!t.IsAddTabButton)
                            {
                                CurrentTab = t;
                                break;
                            }
                        }
                    }
                    SaveAllTabs();
                }
            });

            LoadAllTabs();

            // 저장된 탭이 없다면 기본 탭 생성
            if (Tabs.Count == 0)
            {
                var defaultTab = new FavoriteTabItem 
                { 
                    TabName = "기본 탭",
                    IsAddTabButton = false 
                };
                EnsureAddButtonForTab(defaultTab);
                Tabs.Add(defaultTab);
                CurrentTab = defaultTab;
            }
        }

        // 수정 모드 토글 (연필 / 체크 버튼)
        private void BtnToggleDeleteMode_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteModeActive = !_isDeleteModeActive;
            
            BtnToggleDeleteMode.Content = _isDeleteModeActive ? "✓" : "✏";
            BtnToggleDeleteMode.ToolTip = _isDeleteModeActive ? "완료" : "수정 모드";

            foreach (var tab in Tabs)
            {
                if (!tab.IsAddTabButton)
                {
                    tab.DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;

                    tab.TextBlockVisibility = _isDeleteModeActive ? Visibility.Collapsed : Visibility.Visible;
                    tab.TextBoxVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;
                }

                foreach (var item in tab.Items)
                {
                    if (!item.IsAddButton)
                    {
                        item.DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        item.AddButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

            UpdateTabAddButtonVisibility();

            // ★ 수정 모드가 끝날 때(체크 버튼을 눌러 완료할 때) 변경된 탭 이름들을 파일에 저장
            if (!_isDeleteModeActive)
            {
                SaveAllTabs();
            }
        }

        // 수정 모드 시 상단에 [+ 탭 추가] 카드 표시 및 일반 탭 삭제 버튼 토글 관리
        private void UpdateTabAddButtonVisibility()
        {
            for (int i = Tabs.Count - 1; i >= 0; i--)
            {
                if (Tabs[i].IsAddTabButton)
                {
                    Tabs.RemoveAt(i);
                }
            }

            if (_isDeleteModeActive)
            {
                var addTabItem = new FavoriteTabItem
                {
                    TabName = "탭 추가",
                    IsAddTabButton = true,
                    DeleteButtonVisibility = Visibility.Collapsed
                };
                Tabs.Add(addTabItem);
            }
            else
            {
                if (CurrentTab != null && CurrentTab.IsAddTabButton)
                {
                    foreach (var t in Tabs)
                    {
                        if (!t.IsAddTabButton) { CurrentTab = t; break; }
                    }
                }
            }

            foreach (var tab in Tabs)
            {
                if (!tab.IsAddTabButton)
                {
                    tab.DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FavoriteAppItem itemToRemove)
            {
                e.Handled = true;
                if (CurrentTab != null)
                {
                    CurrentTab.Items.Remove(itemToRemove);
                    SaveAllTabs();
                }
            }
        }

        private void EnsureAddButtonForTab(FavoriteTabItem tab)
        {
            bool hasAddBtn = false;
            foreach (var item in tab.Items)
            {
                if (item.IsAddButton) { hasAddBtn = true; break; }
            }

            if (!hasAddBtn)
            {
                tab.Items.Add(new FavoriteAppItem 
                { 
                    AppName = "추가",
                    FilePath = string.Empty,
                    AppIcon = null,
                    IsAddButton = true,
                    NormalCardVisibility = Visibility.Collapsed,
                    AddButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed,
                    DeleteButtonVisibility = Visibility.Collapsed
                });
            }
        }

        private void SaveAllTabs()
        {
            FileIO.SaveTabs(Tabs);
        }

        private void LoadAllTabs()
        {
            var loadedTabs = FileIO.LoadTabs();
            if (loadedTabs != null && loadedTabs.Count > 0)
            {
                foreach (var tab in loadedTabs)
                {
                    tab.IsAddTabButton = false;

                    if (string.IsNullOrWhiteSpace(tab.TabName))
                    {
                        tab.TabName = "새 탭";
                    }

                    if (tab.Items == null)
                    {
                        tab.Items = new ObservableCollection<FavoriteAppItem>();
                    }
                    else
                    {
                        // 저장된 URL을 바탕으로 아이콘을 안전하게 로드
                        foreach (var item in tab.Items)
                        {
                            if (!item.IsAddButton && item.AppIcon == null && !string.IsNullOrEmpty(item.FilePath))
                            {
                                if (item.FilePath.StartsWith("http://") || item.FilePath.StartsWith("https://"))
                                {
                                    // 데드락 없이 백그라운드에서 아이콘 로드 실행
                                    _ = LoadWebIconSafelyAsync(item);
                                }
                                else if (System.IO.File.Exists(item.FilePath))
                                {
                                    try
                                    {
                                        item.AppIcon = IconService.GetFileIcon(item.FilePath);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    
                    EnsureAddButtonForTab(tab);
                    Tabs.Add(tab);
                }
            }

            if (Tabs.Count == 0)
            {
                var defaultTab = new FavoriteTabItem 
                { 
                    TabName = "기본 탭",
                    IsAddTabButton = false 
                };
                EnsureAddButtonForTab(defaultTab);
                Tabs.Add(defaultTab);
            }

            foreach (var t in Tabs)
            {
                if (!t.IsAddTabButton)
                {
                    CurrentTab = t;
                    break;
                }
            }

            SaveAllTabs();
        }

        private async Task LoadWebIconSafelyAsync(FavoriteAppItem item)
        {
            try
            {
                var webInfo = await IconService.GetWebSiteInfoAsync(item.FilePath);
                if (webInfo.icon != null)
                {
                    item.AppIcon = webInfo.icon;
                }
            }
            catch
            {
                // 로딩 실패 시 예외 무시
            }
        }

        // 드래그 앤 드롭 및 시각적 트리 탐색을 위한 메서드
        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // 아이템 클릭 시 실행/추가 로직 처리 메서드
        private async void HandleItemClick()
        {
            if (_isDeleteModeActive)
            {
                if (FavoriteListBox.SelectedItem is FavoriteAppItem currentItem && !currentItem.IsAddButton)
                {
                    FavoriteListBox.SelectedItem = null;
                    return;
                }
                // 만약 선택된 아이템이 없거나 추가 버튼이라면 아래 로직(추가 창 띄우기)으로 진입 가능
            }

            if (FavoriteListBox.SelectedItem is FavoriteAppItem selectedItem)
            {
                if (selectedItem.IsAddButton)
                {
                    var choiceDialog = new AddChoiceDialog { Owner = Window.GetWindow(this) };
                    if (choiceDialog.ShowDialog() == true)
                    {
                        if (choiceDialog.IsSiteSelected)
                        {
                            var urlDialog = new AddSiteDialog { Owner = Window.GetWindow(this) };
                            if (urlDialog.ShowDialog() == true)
                            {
                                string? inputUrl = urlDialog.InputUrl;
                                if (!string.IsNullOrWhiteSpace(inputUrl) && inputUrl != "https://")
                                {
                                    if (!inputUrl.StartsWith("http://") && !inputUrl.StartsWith("https://"))
                                        inputUrl = "https://" + inputUrl;

                                    var webInfo = await IconService.GetWebSiteInfoAsync(inputUrl);
                                    string siteName = string.IsNullOrWhiteSpace(webInfo.title) ? new Uri(inputUrl).Host : webInfo.title;

                                    var newItem = new FavoriteAppItem
                                    {
                                        AppName = siteName,
                                        FilePath = inputUrl,
                                        IconUrl = inputUrl,
                                        AppIcon = webInfo.icon,
                                        IsAddButton = false,
                                        NormalCardVisibility = Visibility.Visible,
                                        AddButtonVisibility = Visibility.Collapsed,
                                        DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed
                                    };

                                    if (CurrentTab != null)
                                    {
                                        CurrentTab.Items.Insert(CurrentTab.Items.Count - 1, newItem);
                                        SaveAllTabs();
                                    }
                                }
                            }
                        }
                        else
                        {
                            OpenFileDialog openFileDialog = new OpenFileDialog
                            {
                                Filter = "응용 프로그램 및 링크 (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|모든 파일 (*.*)|*.*",
                                Title = "앱 추가"
                            };

                            if (openFileDialog.ShowDialog() == true)
                            {
                                string filePath = openFileDialog.FileName;
                                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                                var iconSource = IconService.GetFileIcon(filePath);

                                var newItem = new FavoriteAppItem
                                {
                                    AppName = fileName,
                                    FilePath = filePath,
                                    IconUrl = string.Empty,
                                    AppIcon = iconSource,
                                    IsAddButton = false,
                                    NormalCardVisibility = Visibility.Visible,
                                    AddButtonVisibility = Visibility.Collapsed,
                                    DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed
                                };

                                if (CurrentTab != null)
                                {
                                    CurrentTab.Items.Insert(CurrentTab.Items.Count - 1, newItem);
                                    SaveAllTabs();
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(selectedItem.FilePath))
                    {
                        try
                        {
                            selectedItem.CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                            Process.Start(new ProcessStartInfo { FileName = selectedItem.FilePath, UseShellExecute = true });

                            await Task.Delay(1000);
                            selectedItem.CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"실행 실패: {ex.Message}");
                            selectedItem.CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                        }
                    }
                }
                FavoriteListBox.SelectedItem = null;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }

    public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b && b) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}