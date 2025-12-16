using SQLite;
using ShareCircle_G17.Models;
using System;
using System.Linq;

namespace ShareCircle_G17.Services
{
    // Local table for saved post IDs
    public class SavedPostEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string UserId { get; set; } = string.Empty;
        [Indexed]
        public string PostId { get; set; } = string.Empty;
    }

    public class SQLiteDatabaseService : ISQLiteDatabaseService
    {
        private SQLiteAsyncConnection? _database;

        public async Task InitializeDatabaseAsync()
        {
            if (_database != null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "sharecircle.db3");
            _database = new SQLiteAsyncConnection(databasePath);

            await _database.CreateTableAsync<User>();
            await _database.CreateTableAsync<DonationPost>();
            await _database.CreateTableAsync<DonationRequest>();
            await _database.CreateTableAsync<SavedPostEntry>();
            await _database.CreateTableAsync<Address>();
            await _database.CreateTableAsync<UserNotification>();
            await _database.CreateTableAsync<SearchHistory>();
            await EnsureUserColumnAsync("LocalPasswordHash", "TEXT");
            await EnsureUserColumnAsync("LocalPasswordSalt", "TEXT");
        }

        // User operations
        public async Task<User?> GetUserAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<User>()
                .Where(u => u.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<User>()
                .Where(u => u.Email != null && u.Email == email)
                .FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            await InitializeDatabaseAsync();
            var normalized = username.ToLowerInvariant();
            return await _database!.Table<User>()
                .Where(u =>
                    (u.Username != null && u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) ||
                    (u.UsernameLower != null && u.UsernameLower == normalized))
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveUserAsync(User user)
        {
            await InitializeDatabaseAsync();

            var existingUser = await GetUserAsync(user.UserId!);
            if (existingUser != null)
            {
                return await _database!.UpdateAsync(user);
            }
            else
            {
                return await _database!.InsertAsync(user);
            }
        }

        public async Task<int> DeleteUserAsync(User user)
        {
            await InitializeDatabaseAsync();
            return await _database!.DeleteAsync(user);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<User>().ToListAsync();
        }

        // Address operations
        public async Task<List<Address>> GetAddressesAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<Address>()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> SaveAddressAsync(Address address)
        {
            await InitializeDatabaseAsync();
            var existing = await _database!.Table<Address>()
                .Where(a => a.AddressId == address.AddressId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                return await _database.UpdateAsync(address);
            }
            else
            {
                return await _database.InsertAsync(address);
            }
        }

        // Donation operations
        public async Task<DonationPost?> GetDonationAsync(string postId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .Where(d => d.PostId == postId)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveDonationAsync(DonationPost donation)
        {
            await InitializeDatabaseAsync();

            var existingDonation = await GetDonationAsync(donation.PostId!);
            if (existingDonation != null)
            {
                return await _database!.UpdateAsync(donation);
            }
            else
            {
                return await _database!.InsertAsync(donation);
            }
        }

        public async Task<int> DeleteDonationAsync(DonationPost donation)
        {
            await InitializeDatabaseAsync();
            return await _database!.DeleteAsync(donation);
        }

        public async Task<List<DonationPost>> GetAllDonationsAsync()
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DonationPost>> GetDonationsByUserIdAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DonationPost>> GetDonationsByCategoryAsync(string category)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .Where(d => d.Category == category)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DonationPost>> GetDonationsBySubCategoryAsync(string subCategory)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .Where(d => d.SubCategory == subCategory)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DonationPost>> GetUnsyncedDonationsAsync()
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationPost>()
                .Where(d => d.IsSynced == false)
                .ToListAsync();
        }

        public async Task<int> MarkDonationAsSyncedAsync(string postId)
        {
            await InitializeDatabaseAsync();
            var donation = await GetDonationAsync(postId);
            if (donation != null)
            {
                donation.IsSynced = true;
                return await _database!.UpdateAsync(donation);
            }
            return 0;
        }

        public async Task ClearAllDonationsAsync()
        {
            await InitializeDatabaseAsync();
            await _database!.DeleteAllAsync<DonationPost>();
        }

        // Saved items operations
        public async Task<List<string>> GetSavedPostIdsAsync(string userId)
        {
            await InitializeDatabaseAsync();
            var entries = await _database!.Table<SavedPostEntry>()
                .Where(e => e.UserId == userId)
                .ToListAsync();
            return entries.Select(e => e.PostId).ToList();
        }

        public async Task SavePostIdAsync(string userId, string postId)
        {
            await InitializeDatabaseAsync();
            var existing = await _database!.Table<SavedPostEntry>()
                .Where(e => e.UserId == userId && e.PostId == postId)
                .FirstOrDefaultAsync();
            if (existing == null)
            {
                await _database.InsertAsync(new SavedPostEntry { UserId = userId, PostId = postId });
            }
        }

        public async Task RemoveSavedPostIdAsync(string userId, string postId)
        {
            await InitializeDatabaseAsync();
            await _database!.Table<SavedPostEntry>()
                .Where(e => e.UserId == userId && e.PostId == postId)
                .DeleteAsync();
        }

        public async Task SyncSavedPostIdsAsync(string userId, List<string> postIds)
        {
            await InitializeDatabaseAsync();
            // Clear existing saved posts for this user and replace with new list
            await _database!.Table<SavedPostEntry>()
                .Where(e => e.UserId == userId)
                .DeleteAsync();
            foreach (var postId in postIds)
            {
                await _database.InsertAsync(new SavedPostEntry { UserId = userId, PostId = postId });
            }
        }

        // DonationRequest operations
        public async Task<DonationRequest?> GetRequestAsync(string requestId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationRequest>()
                .Where(r => r.RequestId == requestId)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveRequestAsync(DonationRequest request)
        {
            await InitializeDatabaseAsync();

            var existingRequest = await GetRequestAsync(request.RequestId!);
            if (existingRequest != null)
            {
                return await _database!.UpdateAsync(request);
            }
            else
            {
                return await _database!.InsertAsync(request);
            }
        }

        public async Task<List<DonationRequest>> GetRequestsByUserAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationRequest>()
                .Where(r => r.DonorId == userId || r.RequesterId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<List<DonationRequest>> GetRequestsForPostAsync(string postId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<DonationRequest>()
                .Where(r => r.PostId == postId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }

        // Notification operations
        public async Task<List<UserNotification>> GetNotificationsAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<UserNotification>()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> SaveNotificationAsync(UserNotification notification)
        {
            await InitializeDatabaseAsync();
            var existing = await _database!.Table<UserNotification>()
                .Where(n => n.NotificationId == notification.NotificationId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                return await _database.UpdateAsync(notification);
            }
            else
            {
                return await _database.InsertAsync(notification);
            }
        }

        public async Task<int> DeleteNotificationAsync(UserNotification notification)
        {
            await InitializeDatabaseAsync();
            return await _database!.DeleteAsync(notification);
        }

        // Search History operations
        public async Task<List<SearchHistory>> GetSearchHistoryAsync(string userId)
        {
            await InitializeDatabaseAsync();
            return await _database!.Table<SearchHistory>()
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task AddSearchHistoryAsync(string userId, string keyword)
        {
            await InitializeDatabaseAsync();
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var normalized = keyword.Trim();

            // Check if exists
            var existing = await _database!.Table<SearchHistory>()
                .Where(h => h.UserId == userId && h.Keyword == normalized)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.CreatedAt = DateTime.UtcNow;
                await _database.UpdateAsync(existing);
            }
            else
            {
                var history = new SearchHistory
                {
                    UserId = userId,
                    Keyword = normalized,
                    CreatedAt = DateTime.UtcNow
                };
                await _database.InsertAsync(history);
            }
        }

        public async Task ClearSearchHistoryAsync(string userId)
        {
            await InitializeDatabaseAsync();
            var items = await _database!.Table<SearchHistory>()
                .Where(h => h.UserId == userId)
                .ToListAsync();
            
            foreach (var item in items)
            {
                await _database.DeleteAsync(item);
            }
        }

        // Database operations
        public async Task ClearAllDataAsync()
        {
            await InitializeDatabaseAsync();
            await _database!.DeleteAllAsync<User>();
            await _database!.DeleteAllAsync<DonationPost>();
            await _database!.DeleteAllAsync<DonationRequest>();
            await _database!.DeleteAllAsync<SavedPostEntry>();
            await _database!.DeleteAllAsync<Address>();
            await _database!.DeleteAllAsync<UserNotification>();
            await _database!.DeleteAllAsync<SearchHistory>();
        }

        private async Task EnsureUserColumnAsync(string columnName, string sqlType)
        {
            var exists = await _database!.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = ?",
                columnName);

            if (exists == 0)
            {
                await _database.ExecuteAsync($"ALTER TABLE Users ADD COLUMN {columnName} {sqlType}");
            }
        }
    }
}
