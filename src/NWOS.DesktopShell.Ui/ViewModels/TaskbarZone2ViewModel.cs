using System.ComponentModel;
using System.Threading.Tasks;
using NWOS.DesktopShell;

namespace NWOS.DesktopShell.Ui
{
    public class TaskbarZone2ViewModel : INotifyPropertyChanged
    {
        private readonly CardinalIpcClient _ipcClient;
        private string _searchQuery = string.Empty;
        private bool _isMasterMenuOpen;

        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(nameof(SearchQuery)); }
        }

        public bool IsMasterMenuOpen
        {
            get => _isMasterMenuOpen;
            set { _isMasterMenuOpen = value; OnPropertyChanged(nameof(IsMasterMenuOpen)); }
        }

        public TaskbarZone2ViewModel(CardinalIpcClient ipcClient)
        {
            _ipcClient = ipcClient;
        }

        /// <summary>
        /// Öffnet oder schließt das Master-Systemmenü.
        /// </summary>
        public void ToggleMasterMenu()
        {
            IsMasterMenuOpen = !IsMasterMenuOpen;
        }

        /// <summary>
        /// Schaltet zwischen virtuellen Workspaces um (z.B. WS1, WS2).
        /// </summary>
        public void SwitchWorkspace(string workspaceId)
        {
            // Logik zum Umschalten der Desktop-Ansicht
        }

        /// <summary>
        /// Führt die globale Alice-Suche über das IPC-Netzwerk aus.
        /// </summary>
        public async Task ExecuteGlobalSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            string query = SearchQuery;
            SearchQuery = string.Empty;

            // Anfrage an das Alice-Modul via Cardinal Kernel schicken
            string result = await _ipcClient.SendRequestAsync("nwos.alice", "search.global", query);

            // Hier wird das Suchergebnis an die UI übergeben (z.B. Such-Popup öffnen)
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}