using System;
using System.Threading.Tasks;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    // Coordinates saving donations locally and syncing them to Firebase when online.
    public class DonationSyncService
    {
        private readonly IDonationService _donationService;
        private readonly FirebaseService _firebaseService;

        public DonationSyncService(IDonationService donationService, FirebaseService firebaseService)
        {
            _donationService = donationService;
            _firebaseService = firebaseService;
        }

        // --- OLD METHOD: Saves the donation locally and attempts initial sync ---
        // Retained for compatibility with older code paths, but calls the new unified method.
        public async Task<int> SaveDonationAsync(DonationItem item, bool canSyncImmediately)
        {
            // Note: This method's logic is largely replaced by SaveOrUpdateDonationAndSyncAsync
            // but we must fix it to ensure it uses the FirebaseId correctly if syncing immediately.

            var id = await _donationService.SaveDonationAsync(item);

            if (item.Id == 0)
            {
                item.Id = id;
            }

            if (canSyncImmediately && id > 0)
            {
                // Use the dedicated sync method for new or initial pushes
                await SaveOrUpdateDonationAndSyncAsync(item, isEditingExisting: false);
            }
            // The item returned here might still be marked IsSynced=false if the sync failed
            // during the call above, but we return the local ID.

            return item.Id;
        }

        // --- NEW/UPDATED METHODS FOR CRUD SYNC ---

        /// <summary>
        /// Saves or updates the donation locally and attempts to synchronize the change to Firebase.
        /// </summary>
        /// <param name="item">The donation item to save/update.</param>
        /// <param name="isEditingExisting">True if this is an update to an item that may already exist on Firebase.</param>
        public async Task SaveOrUpdateDonationAndSyncAsync(DonationItem item, bool isEditingExisting = false)
        {
            // 1. Save/Update locally (SQLite) - Ensure local data is newest
            item.IsSynced = true; // Optimistic assumption
            var id = await _donationService.SaveDonationAsync(item);

            if (item.Id == 0)
            {
                item.Id = id; // Update the local ID if it was a new post
            }

            // 2. Try to sync to Firebase
            try
            {
                var firebaseDonation = MapToFirebaseDonation(item);
                bool requiresLocalUpdate = false;

                if (isEditingExisting && !string.IsNullOrWhiteSpace(item.FirebaseId))
                {
                    // Case A: Editing an existing, previously synced post (UPDATE)
                    firebaseDonation.FirebaseId = item.FirebaseId;
                    await _firebaseService.UpdateDonationAsync(firebaseDonation);
                }
                else
                {
                    // Case B: New post or editing an unsynced post (ADD)
                    string? firebaseId = await _firebaseService.AddDonationAsync(firebaseDonation);

                    if (!string.IsNullOrWhiteSpace(firebaseId))
                    {
                        // We successfully added it. Capture the Firebase ID.
                        item.FirebaseId = firebaseId;
                        requiresLocalUpdate = true;
                    }
                }

                // Success
                item.IsSynced = true;
                if (requiresLocalUpdate)
                {
                    // Only save locally if the FirebaseId was newly assigned
                    await _donationService.SaveDonationAsync(item);
                }
            }
            catch (Exception)
            {
                // Failed to sync (e.g., no network). Mark as unsynced for later retry.
                item.IsSynced = false;
                await _donationService.SaveDonationAsync(item);
            }
        }


        /// <summary>
        /// Deletes the donation from SQLite and attempts to synchronize the deletion to Firebase.
        /// </summary>
        public async Task DeleteDonationAndSyncAsync(DonationItem item)
        {
            // 1. Delete locally (SQLite)
            await _donationService.DeleteDonationAsync(item);

            // 2. Try to delete remotely (Firebase)
            if (!string.IsNullOrWhiteSpace(item.FirebaseId))
            {
                try
                {
                    await _firebaseService.DeleteDonationAsync(item.FirebaseId);
                }
                catch (Exception ex)
                {
                    // For a robust system, you might flag this ID for delayed deletion.
                    // For now, we simply log it since the local record is gone.
                    Console.WriteLine($"Firebase delete failed for ID {item.FirebaseId}: {ex.Message}");
                }
            }
        }

        // --- SYNC PENDING LOGIC (uses the new MapToFirebaseDonation definition) ---

        // Attempts to sync all donations that are still marked as offline.
        public async Task<int> SyncPendingDonationsAsync()
        {
            var pending = await _donationService.GetUnsyncedDonationsAsync();
            var syncedCount = 0;

            foreach (var donation in pending)
            {
                // Note: TrySyncDonationAsync currently only calls AddDonationAsync, 
                // which is suitable for initial syncs (IsSynced=0) but not updates.
                // For a proper update flow, pending items should be checked for 
                // existence on Firebase first, but we rely on Add/Post for now.
                if (await TrySyncDonationAsync(donation))
                {
                    syncedCount++;
                }
            }

            return syncedCount;
        }

        // --- INTERNAL UTILITY METHODS ---

        // Retained for SyncPendingDonationsAsync compatibility. 
        // This handles items that need their FIRST sync (they won't have a FirebaseId yet).
        private async Task<bool> TrySyncDonationAsync(DonationItem donation)
        {
            try
            {
                var firebaseDonation = MapToFirebaseDonation(donation);
                string? firebaseId = await _firebaseService.AddDonationAsync(firebaseDonation);

                if (!string.IsNullOrWhiteSpace(firebaseId))
                {
                    donation.FirebaseId = firebaseId; // Capture the new ID
                    donation.IsSynced = true;
                    await _donationService.SaveDonationAsync(donation);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Original MapToFirebaseDonation structure with FirebaseId included
        private static FirebaseDonation MapToFirebaseDonation(DonationItem donation)
        {
            return new FirebaseDonation
            {
                FirebaseId = donation.FirebaseId, // Used for update/delete API calls

                UserId = "anonymous",
                ItemName = donation.Title,
                Description = donation.Description ?? string.Empty,
                CategoryType = donation.Category?.ToLowerInvariant() ?? string.Empty,
                SubCategory = string.Empty,
                ProductImageUrl = string.Empty,
                DropOffLocation = donation.Location ?? string.Empty,
                ContactEmail = donation.ContactEmail ?? string.Empty,
                CreatedAt = donation.CreatedDate == default ? DateTime.UtcNow : donation.CreatedDate.ToUniversalTime()
            };
        }
    }
}