using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using NWOS.DesktopShell;

namespace NWOS.DesktopShell.Ui
{
    public class NotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    public class TaskbarZone5ViewModel : INotifyPropertyChanged
    {
        private readonly CardinalIpcClient _ipcClient;
        private bool _isNotificationFeedOpen;

        public ObservableCollection<NotificationItem> Notifications { get; set; }

        public bool IsNotificationFeedOpen
        {
            get => _isNotificationFeedOpen;
            set { _isNotificationFeedOpen = value; OnPropertyChanged(nameof(IsNotificationFeedOpen)); }
        }

        public TaskbarZone5ViewModel(CardinalIpcClient ipcClient)
        {
            _ipcClient = ipcClient;
            Notifications = new ObservableCollection<NotificationItem>();
            LoadInitialNotifications();
        }

        private void LoadInitialNotifications()
        {
            // Initiale Willkommensmeldung im Feed hinterlegen
            Notifications.Add(new NotificationItem
            {
                Title = "NWOS System",
                Message = "Kernel und Taskleisten-Overlay erfolgreich gestartet.",
                Timestamp = "Jetzt"
            });
        }

        public void ToggleNotificationFeed()
        {
            IsNotificationFeedOpen = !IsNotificationFeedOpen;
        }

        /// <summary>
        /// Ruft neue Benachrichtigungen über den Cardinal-Kernel ab.
        /// </summary>
        public async Task RefreshNotificationsAsync()
        {
            string result = await _ipcClient.SendRequestAsync("nwos.systemcenter", "notifications.get", "all");
            // Hier können bei Bedarf weitere Benachrichtigungen in die Collection geladen werden
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}