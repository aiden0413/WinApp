using System.Windows;

namespace winapp
{
    public partial class AddChoiceDialog : Window
    {
        public bool IsSiteSelected { get; private set; } = false;

        public AddChoiceDialog()
        {
            InitializeComponent();
        }

        private void AppBtn_Click(object sender, RoutedEventArgs e)
        {
            IsSiteSelected = false;
            DialogResult = true;
            Close();
        }

        private void SiteBtn_Click(object sender, RoutedEventArgs e)
        {
            IsSiteSelected = true;
            DialogResult = true;
            Close();
        }
    }
}