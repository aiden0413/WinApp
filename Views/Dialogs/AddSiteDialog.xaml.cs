using System.Windows;

namespace winapp
{
    public partial class AddSiteDialog : Window
    {
        public string? InputUrl { get; private set; }

        public AddSiteDialog()
        {
            InitializeComponent();
            TxtUrl.Text = "https://";
            TxtUrl.Focus();
            TxtUrl.SelectAll();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            InputUrl = TxtUrl.Text;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}