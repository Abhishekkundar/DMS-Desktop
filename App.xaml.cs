using System.Windows;

namespace DMS.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var login = new LoginWindow();
        MainWindow = login;
        login.Show();
    }

    public void OpenMainWindow()
    {
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
