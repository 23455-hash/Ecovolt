using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EcoVolt
{
    public partial class LoginWindow : Window
    {
        public LoginWindow() { InitializeComponent(); }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { try { DragMove(); } catch { } }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        { Application.Current.Shutdown(); }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string pwd = txtPwd.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pwd))
            { ShowMsg("Por favor completa todos los campos.", true); return; }

            var user = UserStore.FindUser(email, pwd);
            if (user == null)
            { ShowMsg("Correo o contraseña incorrectos.", true); return; }

            UserStore.CurrentUser = user;
            new DashboardWindow().Show();
            Close();
        }

        private void LnkRegister_Click(object sender, MouseButtonEventArgs e)
        { new RegisterWindow().Show(); Close(); }

        private void ShowMsg(string msg, bool isError)
        {
            lblMsg.Text = msg;
            msgBox.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isError ? "#2A0A0A" : "#0A2A15"));
            msgBox.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isError ? "#7A2020" : "#1A5A35"));
            msgBox.BorderThickness = new System.Windows.Thickness(1);
            lblMsg.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isError ? "#FF7070" : "#60DD90"));
            msgBox.Visibility = Visibility.Visible;
        }
    }
}