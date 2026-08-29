using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Microsoft.Win32;

namespace winapp
{
    public class FavoriteAppItem : INotifyPropertyChanged
    {
        public string AppName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public BitmapSource? AppIcon { get; set; }
        public bool IsAddButton { get; set; } = false;

        private Brush _cardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
        public Brush CardBackgroundColor
        {
            get => _cardBackgroundColor;
            set
            {
                _cardBackgroundColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardBackgroundColor)));
            }
        }

        private Visibility _deleteButtonVisibility = Visibility.Collapsed;
        public Visibility DeleteButtonVisibility
        {
            get => _deleteButtonVisibility;
            set
            {
                _deleteButtonVisibility = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteButtonVisibility)));
            }
        }

        private Visibility _normalCardVisibility = Visibility.Visible;
        public Visibility NormalCardVisibility
        {
            get => _normalCardVisibility;
            set
            {
                _normalCardVisibility = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NormalCardVisibility)));
            }
        }

        private Visibility _addButtonVisibility = Visibility.Collapsed;
        public Visibility AddButtonVisibility
        {
            get => _addButtonVisibility;
            set
            {
                _addButtonVisibility = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddButtonVisibility)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class FavoriteAppsView : UserControl
    {
        private ObservableCollection<FavoriteAppItem> _appList = new ObservableCollection<FavoriteAppItem>();
        private bool _isDeleteModeActive = false;
        private readonly string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "winapp_favorites.txt");
        
        private Point _dragStartPoint;
        private FavoriteAppItem? _draggedItem;
        private bool _isDragging = false;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        public FavoriteAppsView()
        {
            InitializeComponent();
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            try
            {
                _appList.Clear();

                if (File.Exists(_settingsFilePath))
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
                                IsAddButton = false,
                                NormalCardVisibility = Visibility.Visible,
                                AddButtonVisibility = Visibility.Collapsed,
                                DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed
                            });
                        }
                    }
                }

                // 앱 추가 버튼 항목 (기본은 수정 모드가 아니므로 숨김)
                _appList.Add(new FavoriteAppItem 
                { 
                    AppName = "앱 추가", 
                    IsAddButton = true,
                    NormalCardVisibility = Visibility.Collapsed,
                    AddButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed,
                    DeleteButtonVisibility = Visibility.Collapsed
                });

                FavoriteListBox.ItemsSource = _appList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load Error: {ex.Message}");
            }
        }

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

        private void BtnToggleDeleteMode_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteModeActive = !_isDeleteModeActive;
            
            // 버튼의 Content를 직접 변경하여 아이콘 즉시 전환
            BtnToggleDeleteMode.Content = _isDeleteModeActive ? "✓" : "✏";
            
            // 툴팁 동적 변경
            BtnToggleDeleteMode.ToolTip = _isDeleteModeActive ? "완료" : "수정 모드";

            foreach (var item in _appList)
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

        private void FavoriteListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                _draggedItem = listBoxItem.DataContext as FavoriteAppItem;
            }
        }

        private void FavoriteListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && !_isDragging)
            {
                Point mousePos = e.GetPosition(this);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (_draggedItem.IsAddButton || !_isDeleteModeActive) return;

                    _isDragging = true;
                    Mouse.Capture(FavoriteListBox);

                    GhostContentPanel.Children.Clear();
                    if (_draggedItem.AppIcon != null)
                    {
                        GhostContentPanel.Children.Add(new Image { Source = _draggedItem.AppIcon, Width = 44, Height = 44, Margin = new Thickness(0, 0, 0, 6) });
                    }
                    GhostContentPanel.Children.Add(new TextBlock { Text = _draggedItem.AppName, Foreground = Brushes.White, FontSize = 11, TextAlignment = TextAlignment.Center });

                    GhostBorder.Visibility = Visibility.Visible;
                    UpdateGhostPosition(e);
                }
            }

            if (_isDragging)
            {
                UpdateGhostPosition(e);
            }
        }

        private void UpdateGhostPosition(MouseEventArgs e)
        {
            Point currentPos = e.GetPosition(RootGrid);
            Canvas.SetLeft(GhostBorder, currentPos.X - 60);
            Canvas.SetTop(GhostBorder, currentPos.Y - 60);
        }

        private void FavoriteListBox_Drop(object sender, DragEventArgs e)
        {
            CleanupDrag();
        }

        private void FavoriteListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                var targetListBoxItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (targetListBoxItem != null && targetListBoxItem.DataContext is FavoriteAppItem targetItem)
                {
                    if (!targetItem.IsAddButton && _draggedItem != null && !_draggedItem.IsAddButton)
                    {
                        int oldIndex = _appList.IndexOf(_draggedItem);
                        int newIndex = _appList.IndexOf(targetItem);

                        if (oldIndex != newIndex && oldIndex >= 0 && newIndex >= 0)
                        {
                            _appList.Move(oldIndex, newIndex);
                            SaveFavorites();
                        }
                    }
                }
                CleanupDrag();
            }
            else
            {
                HandleItemClick();
            }
            _draggedItem = null;
        }

        private void CleanupDrag()
        {
            GhostBorder.Visibility = Visibility.Collapsed;
            if (Mouse.Captured == FavoriteListBox)
            {
                Mouse.Capture(null);
            }
            _isDragging = false;
        }

        private async void HandleItemClick()
        {
            if (FavoriteListBox.SelectedItem is FavoriteAppItem selectedItem)
            {
                if (selectedItem.IsAddButton)
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog
                    {
                        Filter = "응용 프로그램 (*.exe;*.lnk)|*.exe;*.lnk",
                        Title = "앱 추가"
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
                            NormalCardVisibility = Visibility.Visible,
                            AddButtonVisibility = Visibility.Collapsed,
                            DeleteButtonVisibility = _isDeleteModeActive ? Visibility.Visible : Visibility.Collapsed
                        };

                        _appList.Insert(_appList.Count - 1, newItem);
                        SaveFavorites();
                    }
                }
                else
                {
                    if (File.Exists(selectedItem.FilePath))
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
                    else
                    {
                        MessageBox.Show("파일을 찾을 수 없습니다.");
                    }
                }
                FavoriteListBox.SelectedItem = null;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FavoriteAppItem itemToRemove)
            {
                e.Handled = true;
                _appList.Remove(itemToRemove);
                SaveFavorites();
            }
        }

        private BitmapSource? GetFileIcon(string filePath)
        {
            try
            {
                using (System.Drawing.Icon? sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(filePath))
                {
                    if (sysIcon != null)
                    {
                        using (System.Drawing.Bitmap bmp = sysIcon.ToBitmap())
                        {
                            IntPtr hBitmap = bmp.GetHbitmap();
                            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            DeleteObject(hBitmap);
                            return bitmapSource;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}