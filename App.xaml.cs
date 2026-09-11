using System.Windows;
using DMS.Desktop.Data;
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

    public void OpenMainWindow(UserRecord user)
    {
        var mainWindow = new MainWindow(user);

        MainWindow = mainWindow;

        mainWindow.Show();
    }
}
