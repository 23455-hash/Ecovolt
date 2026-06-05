using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EcoVolt.Models;

namespace EcoVolt
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow() { InitializeComponent(); }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { try { DragMove(); } catch { } }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        { Application.Current.Shutdown(); }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string pwd = txtPwd.Password;
            string pwd2 = txtPwd2.Password;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(pwd) || string.IsNullOrWhiteSpace(pwd2))
            { ShowMsg("Por favor completa todos los campos.", true); return; }

            if (pwd != pwd2)
            { ShowMsg("Las contraseñas no coinciden.", true); return; }

            if (pwd.Length < 6)
            { ShowMsg("La contraseña debe tener al menos 6 caracteres.", true); return; }

            if (UserStore.EmailExists(email))
            { ShowMsg("Ya existe una cuenta con ese correo.", true); return; }

            UserStore.AddUser(new User { Name = name, Email = email, Password = pwd });
            ShowMsg("¡Cuenta creada exitosamente! Redirigiendo...", false);

            var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = System.TimeSpan.FromSeconds(1.2) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                UserStore.CurrentUser = UserStore.FindUser(email, pwd);
                new DashboardWindow().Show();
                Close();
            };
            timer.Start();
        }

        private void LnkLogin_Click(object sender, MouseButtonEventArgs e)
        { new LoginWindow().Show(); Close(); }

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