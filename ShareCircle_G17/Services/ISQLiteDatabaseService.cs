using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public interface ISQLiteDatabaseService
    {
        // User operations
        Task<User?> GetUserAsync(string userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<int> SaveUserAsync(User user);
        Task<int> DeleteUserAsync(User user);
        Task<List<User>> GetAllUsersAsync();

        // Donation operations
        Task<DonationPost?> GetDonationAsync(string postId);
        Task<int> SaveDonationAsync(DonationPost donation);
        Task<int> DeleteDonationAsync(DonationPost donation);
        Task<List<DonationPost>> GetAllDonationsAsync();
        Task<List<DonationPost>> GetDonationsByUserIdAsync(string userId);
        Task<List<DonationPost>> GetDonationsByCategoryAsync(string category);
        Task<List<DonationPost>> GetDonationsBySubCategoryAsync(string subCategory);
        Task<List<DonationPost>> GetUnsyncedDonationsAsync();
        Task<int> MarkDonationAsSyncedAsync(string postId);
        Task ClearAllDonationsAsync();

        // Address operations
        Task<List<Address>> GetAddressesAsync(string userId);
        Task<int> SaveAddressAsync(Address address);

        // Saved Items operations
        Task<List<string>> GetSavedPostIdsAsync(string userId);
        Task SavePostIdAsync(string userId, string postId);
        Task RemoveSavedPostIdAsync(string userId, string postId);
        Task SyncSavedPostIdsAsync(string userId, List<string> postIds);

        // DonationRequest operations
        Task<DonationRequest?> GetRequestAsync(string requestId);
        Task<int> SaveRequestAsync(DonationRequest request);
        Task<List<DonationRequest>> GetRequestsByUserAsync(string userId);
        Task<List<DonationRequest>> GetRequestsForPostAsync(string postId);

        // Notification operations
        Task<List<UserNotification>> GetNotificationsAsync(string userId);
        Task<int> SaveNotificationAsync(UserNotification notification);
        Task<int> DeleteNotificationAsync(UserNotification notification);

        // Search History operations
        Task<List<SearchHistory>> GetSearchHistoryAsync(string userId);
        Task AddSearchHistoryAsync(string userId, string keyword);
        Task ClearSearchHistoryAsync(string userId);

        // Database operations
        Task InitializeDatabaseAsync();
        Task ClearAllDataAsync();
    }
}
