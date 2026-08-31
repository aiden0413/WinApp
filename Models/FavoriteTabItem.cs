using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace winapp
{
    public class FavoriteTabItem : INotifyPropertyChanged
    {
        private string _tabName = "새 탭";
        public string TabName
        {
            get => _tabName;
            set { _tabName = value; OnPropertyChanged(nameof(TabName)); }
        }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public bool IsAddTabButton { get; set; } = false;

        private Visibility _deleteButtonVisibility = Visibility.Collapsed;
        public Visibility DeleteButtonVisibility
        {
            get => _deleteButtonVisibility;
            set { _deleteButtonVisibility = value; OnPropertyChanged(nameof(DeleteButtonVisibility)); }
        }

        // ★ 핵심: 텍스트블록과 텍스트박스 전환용 가시성 프로퍼티 추가
        private Visibility _textBlockVisibility = Visibility.Visible;
        public Visibility TextBlockVisibility
        {
            get => _textBlockVisibility;
            set { _textBlockVisibility = value; OnPropertyChanged(nameof(TextBlockVisibility)); }
        }

        private Visibility _textBoxVisibility = Visibility.Collapsed;
        public Visibility TextBoxVisibility
        {
            get => _textBoxVisibility;
            set { _textBoxVisibility = value; OnPropertyChanged(nameof(TextBoxVisibility)); }
        }

        public ObservableCollection<FavoriteAppItem> Items { get; set; } = new ObservableCollection<FavoriteAppItem>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}