using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Services;

namespace ShareCircle_G17
{
    public partial class App : Application
    {
        private readonly ISyncService? _syncService;
        private bool _wasOffline = false;

        public App(AppShell appShell, ISyncService syncService)
        {
            InitializeComponent();

            _syncService = syncService;

            // Use dependency injection to get AppShell
            MainPage = appShell;

            // Initialize network status
            _wasOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;

            // Subscribe to connectivity changes
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }

        private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            bool isOnline = e.NetworkAccess == NetworkAccess.Internet;

            // If we just came back online from being offline, sync pending donations
            if (isOnline && _wasOffline)
            {
                System.Diagnostics.Debug.WriteLine("Network restored - syncing pending donations...");
                await SyncPendingDonationsAsync();
            }

            _wasOffline = !isOnline;
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Check and sync pending donations when app resumes
            // This is a fallback in case ConnectivityChanged event was missed
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                System.Diagnostics.Debug.WriteLine("App resumed with network - checking for pending donations...");
                _ = SyncPendingDonationsAsync();
            }
        }

        private async Task SyncPendingDonationsAsync()
        {
            try
            {
                if (_syncService != null)
                {
                    await _syncService.SyncUnsyncedDonationsToFirebaseAsync();
                    System.Diagnostics.Debug.WriteLine("Pending donations synced successfully!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing pending donations: {ex.Message}");
            }
        }

        protected override void CleanUp()
        {
            // Unsubscribe from connectivity changes
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            base.CleanUp();
        }
    }
}
