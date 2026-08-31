using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace winapp
{
    public class FavoriteAppItem : INotifyPropertyChanged
    {
        private string _appName = string.Empty;
        public string AppName
        {
            get => _appName;
            set { _appName = value; OnPropertyChanged(nameof(AppName)); }
        }

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        private string _iconUrl = string.Empty;
        public string IconUrl
        {
            get => _iconUrl;
            set { _iconUrl = value; OnPropertyChanged(nameof(IconUrl)); }
        }

        private ImageSource? _appIcon;
        public ImageSource? AppIcon
        {
            get => _appIcon;
            set { _appIcon = value; OnPropertyChanged(nameof(AppIcon)); }
        }

        public bool IsAddButton { get; set; } = false;

        private Visibility _normalCardVisibility = Visibility.Visible;
        public Visibility NormalCardVisibility
        {
            get => _normalCardVisibility;
            set { _normalCardVisibility = value; OnPropertyChanged(nameof(NormalCardVisibility)); }
        }

        private Visibility _addButtonVisibility = Visibility.Collapsed;
        public Visibility AddButtonVisibility
        {
            get => _addButtonVisibility;
            set { _addButtonVisibility = value; OnPropertyChanged(nameof(AddButtonVisibility)); }
        }

        private Visibility _deleteButtonVisibility = Visibility.Collapsed;
        public Visibility DeleteButtonVisibility
        {
            get => _deleteButtonVisibility;
            set { _deleteButtonVisibility = value; OnPropertyChanged(nameof(DeleteButtonVisibility)); }
        }

        private Brush _cardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
        public Brush CardBackgroundColor
        {
            get => _cardBackgroundColor;
            set { _cardBackgroundColor = value; OnPropertyChanged(nameof(CardBackgroundColor)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}