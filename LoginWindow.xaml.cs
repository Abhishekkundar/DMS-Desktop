using System.Windows;
using DMS.Desktop.Data;

namespace DMS.Desktop;

public partial class LoginWindow : Window
{
    private readonly UserStore _userStore;

    public LoginWindow()
    {
        InitializeComponent();

        _userStore = new UserStore();

        PasswordBox.Password = "password";
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var userId = UserIdBox.Text.Trim();
        var password = PasswordBox.Password;

        ErrorText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            ErrorText.Text = "Please enter User ID.";
            UserIdBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "Please enter Password.";
            PasswordBox.Focus();
            return;
        }

        var user = _userStore.Authenticate(userId, password);

        if (user is null)
        {
            ErrorText.Text = "Invalid User ID or Password.";
            PasswordBox.SelectAll();
            PasswordBox.Focus();
            return;
        }

        var app = (App)Application.Current;

        app.OpenMainWindow(user);

        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}