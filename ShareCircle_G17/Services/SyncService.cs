using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public class SyncService : ISyncService
    {
        private readonly ISQLiteDatabaseService _sqliteService;
        private readonly IFirebaseDatabaseService _firebaseService;
        private readonly IConnectivity _connectivity;

        public SyncService(
            ISQLiteDatabaseService sqliteService,
            IFirebaseDatabaseService firebaseService,
            IConnectivity connectivity)
        {
            _sqliteService = sqliteService;
            _firebaseService = firebaseService;
            _connectivity = connectivity;
        }

        public async Task<bool> IsOnlineAsync()
        {
            return _connectivity.NetworkAccess == NetworkAccess.Internet;
        }

        public async Task SyncDonationsFromFirebaseAsync()
        {
            try
            {
                if (!await IsOnlineAsync())
                    return;

                // Fetch all donations from Firebase
                var firebaseDonations = await _firebaseService.GetAllDonationPostsAsync();

                // Save/refresh Firebase donations into SQLite
                foreach (var donation in firebaseDonations)
                {
                    donation.IsSynced = true;
                    await _sqliteService.SaveDonationAsync(donation);
                }

                // Remove any locally cached donations that were deleted from Firebase
                var localDonations = await _sqliteService.GetAllDonationsAsync();
                var firebaseIds = firebaseDonations
                    .Where(d => !string.IsNullOrWhiteSpace(d.PostId))
                    .Select(d => d.PostId)
                    .ToHashSet();

                foreach (var local in localDonations)
                {
                    // Only purge records that were previously synced and no longer exist in Firebase
                    if (local.IsSynced && !string.IsNullOrWhiteSpace(local.PostId) && !firebaseIds.Contains(local.PostId))
                    {
                        await _sqliteService.DeleteDonationAsync(local);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing from Firebase: {ex.Message}");
            }
        }

        public async Task SyncUnsyncedDonationsToFirebaseAsync()
        {
            try
            {
                if (!await IsOnlineAsync())
                    return;

                // Get all unsynced donations from SQLite
                var unsyncedDonations = await _sqliteService.GetUnsyncedDonationsAsync();
                int successCount = 0;

                foreach (var donation in unsyncedDonations)
                {
                    // Upload to Firebase
                    var result = await _firebaseService.CreateDonationPostAsync(donation);
                    
                    if (result.Success)
                    {
                        // Mark as synced in SQLite
                        await _sqliteService.MarkDonationAsSyncedAsync(donation.PostId!);
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.DisplayAlert("Sync Complete", $"Your {successCount} posts published offline have been successfully uploaded.", "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing to Firebase: {ex.Message}");
            }
        }

        public async Task<bool> SaveDonationAsync(DonationPost donation)
        {
            try
            {
                bool isOnline = await IsOnlineAsync();

                if (isOnline)
                {
                    // Online mode: Save to Firebase first, then SQLite
                    var result = await _firebaseService.CreateDonationPostAsync(donation);

                    if (!result.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save to Firebase: {result.Message}");
                        // Still save to SQLite as unsynced
                        donation.IsSynced = false;
                        await _sqliteService.SaveDonationAsync(donation);
                        return false;
                    }

                    // Firebase save successful, now save to SQLite
                    donation.IsSynced = true;
                    await _sqliteService.SaveDonationAsync(donation);
                }
                else
                {
                    // Offline mode: Save to SQLite only, mark as unsynced
                    donation.IsSynced = false;
                    await _sqliteService.SaveDonationAsync(donation);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving donation: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DonationPost>> GetDonationsAsync(string? subCategory = null)
        {
            try
            {
                bool isOnline = await IsOnlineAsync();

                if (isOnline)
                {
                    // Sync from Firebase first
                    await SyncDonationsFromFirebaseAsync();
                }

                // Always fetch from SQLite (works both online and offline)
                List<DonationPost> donations;

                if (string.IsNullOrEmpty(subCategory))
                {
                    donations = await _sqliteService.GetAllDonationsAsync();
                }
                else
                {
                    donations = await _sqliteService.GetDonationsBySubCategoryAsync(subCategory);
                }

                return donations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting donations: {ex.Message}");
                return new List<DonationPost>();
            }
        }

        public async Task PerformFullSyncAsync()
        {
            try
            {
                if (!await IsOnlineAsync())
                    return;

                // First, upload any unsynced local data
                await SyncUnsyncedDonationsToFirebaseAsync();

                // Then, fetch latest data from Firebase
                await SyncDonationsFromFirebaseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error performing full sync: {ex.Message}");
            }
        }
    }
}
