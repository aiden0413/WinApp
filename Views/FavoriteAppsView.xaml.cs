using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace winapp
{
    public class FavoriteAppItem : INotifyPropertyChanged
    {
        public string AppName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public BitmapSource? AppIcon { get; set; }
        public bool IsAddButton { get; set; } = false;

        private bool _isDeleteMode;
        public bool IsDeleteMode
        {
            get => _isDeleteMode;
            set
            {
                _isDeleteMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDeleteMode)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class FavoriteAppsView : UserControl
    {
        private ObservableCollection<FavoriteAppItem> _appList = new ObservableCollection<FavoriteAppItem>();
        private bool _isDeleteModeActive = false;
        private readonly string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "winapp_favorites.txt");

        public FavoriteAppsView()
        {
            InitializeComponent();
            
            LoadFavorites();
            FavoriteListBox.SelectionChanged += FavoriteListBox_SelectionChanged;
        }

        // 저장된 목록 불러오기
        private void LoadFavorites()
        {
            _appList.Clear();

            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(_settingsFilePath);
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && File.Exists(line))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(line);
                            BitmapSource? iconSource = GetFileIcon(line);

                            _appList.Add(new FavoriteAppItem
                            {
                                AppName = fileName,
                                FilePath = line,
                                AppIcon = iconSource,
                                IsAddButton = false
                            });
                        }
                    }
                }
                catch { }
            }

            // 항상 마지막에 [+] 앱 추가 버튼 배치
            _appList.Add(new FavoriteAppItem { AppName = "앱 추가", IsAddButton = true });
            FavoriteListBox.ItemsSource = _appList;
        }

        // 현재 목록 저장하기
        private void SaveFavorites()
        {
            try
            {
                var lines = new List<string>();
                foreach (var item in _appList)
                {
                    if (!item.IsAddButton && !string.IsNullOrEmpty(item.FilePath))
                    {
                        lines.Add(item.FilePath);
                    }
                }
                File.WriteAllLines(_settingsFilePath, lines);
            }
            catch { }
        }

        // [삭제 모드] 버튼 클릭 시 토글
        private void BtnToggleDeleteMode_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteModeActive = !_isDeleteModeActive;

            if (_isDeleteModeActive)
            {
                BtnToggleDeleteMode.Content = "완료";
                BtnToggleDeleteMode.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 50, 50));
            }
            else
            {
                BtnToggleDeleteMode.Content = "삭제 모드";
                BtnToggleDeleteMode.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51));
            }

            foreach (var item in _appList)
            {
                if (!item.IsAddButton)
                {
                    item.IsDeleteMode = _isDeleteModeActive;
                    
                    var container = FavoriteListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                    if (container != null)
                    {
                        var deleteBtn = FindVisualChild<Button>(container, "DeleteButton");
                        if (deleteBtn != null)
                        {
                            deleteBtn.Visibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void FavoriteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavoriteListBox.SelectedItem is FavoriteAppItem selectedItem)
            {
                if (!_isDeleteModeActive)
                {
                    if (selectedItem.IsAddButton)
                    {
                        OpenFileDialog openFileDialog = new OpenFileDialog
                        {
                            Filter = "응용 프로그램 및 바로가기 (*.exe;*.lnk)|*.exe;*.lnk|모든 파일 (*.*)|*.*",
                            Title = "즐겨찾기에 추가할 앱 선택"
                        };

                        if (openFileDialog.ShowDialog() == true)
                        {
                            string filePath = openFileDialog.FileName;
                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            BitmapSource? iconSource = GetFileIcon(filePath);

                            var newItem = new FavoriteAppItem
                            {
                                AppName = fileName,
                                FilePath = filePath,
                                AppIcon = iconSource,
                                IsAddButton = false,
                                IsDeleteMode = _isDeleteModeActive
                            };

                            _appList.Insert(_appList.Count - 1, newItem);
                            SaveFavorites(); // 추가 시 즉시 저장
                        }
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(selectedItem.FilePath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = selectedItem.FilePath,
                                    UseShellExecute = true
                                });
                            }
                            else
                            {
                                MessageBox.Show("원본 파일을 찾을 수 없습니다.", "실행 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"앱을 실행하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                FavoriteListBox.SelectedItem = null;
            }
        }

        // 등록된 앱 삭제 버튼(X) 클릭 이벤트
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FavoriteAppItem itemToRemove)
            {
                e.Handled = true;
                _appList.Remove(itemToRemove);
                SaveFavorites(); // 삭제 시 즉시 저장
            }
        }

        private BitmapSource? GetFileIcon(string filePath)
        {
            try
            {
                using (Icon? sysIcon = Icon.ExtractAssociatedIcon(filePath))
                {
                    if (sysIcon != null)
                    {
                        using (Bitmap bmp = sysIcon.ToBitmap())
                        {
                            IntPtr hBitmap = bmp.GetHbitmap();
                            BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            
                            DeleteObject(hBitmap);
                            return bitmapSource;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (child as FrameworkElement)?.Name == name)
                {
                    return typedChild;
                }
                T? descendant = FindVisualChild<T>(child, name);
                if (descendant != null) return descendant;
            }
            return null;
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}