using System.Windows;

namespace NWOS.DesktopShell.Ui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Kein lokaler Zustand mehr: Icons, Boxen und Widgets werden von Cardinal
        // gehalten und dort sofort nach jeder Änderung gespeichert. Die UI
        // verbindet sich nur (siehe MainWindow.InitializeAsync).
        var mainWindow = new MainWindow(new CardinalClient());
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }
}
