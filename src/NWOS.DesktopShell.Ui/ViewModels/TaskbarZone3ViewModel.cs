using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using NWOS.DesktopShell;

namespace NWOS.DesktopShell.Ui
{
    public class AppItem
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Icon { get; set; } = "📦";
    }

    public class TaskbarZone3ViewModel : INotifyPropertyChanged
    {
        private readonly CardinalIpcClient _ipcClient;
        public ObservableCollection<AppItem> PinnedApps { get; set; }

        public TaskbarZone3ViewModel(CardinalIpcClient ipcClient)
        {
            _ipcClient = ipcClient;
            PinnedApps = new ObservableCollection<AppItem>();
            LoadDefaultApps();
        }

        private void LoadDefaultApps()
        {
            // Standard-Systemanwendungen für den Hub initialisieren
            PinnedApps.Add(new AppItem { Name = "Files", ExecutablePath = "nwos.marc", Icon = "📁" });
            PinnedApps.Add(new AppItem { Name = "Settings", ExecutablePath = "nwos.settings", Icon = "⚙️" });
            PinnedApps.Add(new AppItem { Name = "Terminal", ExecutablePath = "nwos.systemcenter", Icon = "💻" });
        }

        /// <summary>
        /// Startet eine angeheftete Anwendung über den Cardinal-Kernel.
        /// </summary>
        public async Task LaunchAppAsync(AppItem app)
        {
            if (app == null) return;

            // Startbefehl über das IPC-Netzwerk an den Marc-Prozessmanager senden
            await _ipcClient.SendRequestAsync(app.ExecutablePath, "app.launch", "start");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}