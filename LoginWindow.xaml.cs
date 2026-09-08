using System.Windows;

namespace DMS.Desktop;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        PasswordBox.Password = "password";
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var userId = UserIdBox.Text.Trim();
        var password = PasswordBox.Password;

        // V1 UI login only. Authentication/database will be connected later.
        if (userId == "admin" && password == "password")
        {
            var app = (App)Application.Current;
            app.OpenMainWindow();
            Close();
        }
        else
        {
            ErrorText.Text = "Invalid User ID or Password.";
            PasswordBox.Focus();
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
