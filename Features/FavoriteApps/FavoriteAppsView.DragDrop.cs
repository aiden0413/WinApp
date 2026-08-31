using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace winapp
{
    public partial class FavoriteAppsView : UserControl
    {
        private System.Windows.Point _dragStartPoint;
        private FavoriteAppItem? _draggedItem;
        private bool _isDragging = false;

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
                System.Windows.Point mousePos = e.GetPosition(this);
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
                    GhostContentPanel.Children.Add(new TextBlock { Text = _draggedItem.AppName, Foreground = System.Windows.Media.Brushes.White, FontSize = 11, TextAlignment = TextAlignment.Center });

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
            System.Windows.Point currentPos = e.GetPosition(RootGrid);
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
                            SaveAllTabs();
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
    }
}