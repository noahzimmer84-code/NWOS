using System.ComponentModel;
using System.Threading.Tasks;
using NWOS.DesktopShell;

namespace NWOS.DesktopShell.Ui
{
    public class TaskbarZone4ViewModel : INotifyPropertyChanged
    {
        private readonly CardinalIpcClient _ipcClient;
        private bool _isAudioPopupOpen;
        private bool _isBackgroundMonitorOpen;
        private string _systemStatusText = "CPU: --% | RAM: --%";

        public bool IsAudioPopupOpen
        {
            get => _isAudioPopupOpen;
            set { _isAudioPopupOpen = value; OnPropertyChanged(nameof(IsAudioPopupOpen)); }
        }

        public bool IsBackgroundMonitorOpen
        {
            get => _isBackgroundMonitorOpen;
            set { _isBackgroundMonitorOpen = value; OnPropertyChanged(nameof(IsBackgroundMonitorOpen)); }
        }

        public string SystemStatusText
        {
            get => _systemStatusText;
            set { _systemStatusText = value; OnPropertyChanged(nameof(SystemStatusText)); }
        }

        public TaskbarZone4ViewModel(CardinalIpcClient ipcClient)
        {
            _ipcClient = ipcClient;
        }

        public void ToggleAudioPopup()
        {
            IsAudioPopupOpen = !IsAudioPopupOpen;
        }

        public void ToggleBackgroundMonitor()
        {
            IsBackgroundMonitorOpen = !IsBackgroundMonitorOpen;
        }

        public async Task UpdateSystemMetricsAsync()
        {
            string result = await _ipcClient.SendRequestAsync("nwos.systemcenter", "metrics.get", "status");
            if (!string.IsNullOrEmpty(result))
            {
                SystemStatusText = result;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}