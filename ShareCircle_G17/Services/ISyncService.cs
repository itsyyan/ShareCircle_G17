using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public interface ISyncService
    {
        // Check if device is online
        Task<bool> IsOnlineAsync();

        // Sync donations from Firebase to SQLite
        Task SyncDonationsFromFirebaseAsync();

        // Sync unsynced donations from SQLite to Firebase
        Task SyncUnsyncedDonationsToFirebaseAsync();

        // Save donation (handles both online and offline modes)
        Task<bool> SaveDonationAsync(DonationPost donation);

        // Get donations with sync handling
        Task<List<DonationPost>> GetDonationsAsync(string? subCategory = null);

        // Force full sync
        Task PerformFullSyncAsync();
    }
}
