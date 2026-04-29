namespace SeerAssetTool.Win;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var service = new SeerToolService();
        if (e.Args is { Length: > 0 })
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            var cliRunner = new CliRunner(service);
            var exitCode = await cliRunner.RunAsync(e.Args);
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow(service);
        MainWindow = window;
        window.Show();
    }
}
