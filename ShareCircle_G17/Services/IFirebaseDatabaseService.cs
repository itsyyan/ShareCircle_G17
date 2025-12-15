using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public interface IFirebaseDatabaseService
    {
        // Donation Posts CRUD operations
        Task<(bool Success, string Message, string? PostId)> CreateDonationPostAsync(DonationPost post);
        Task<(bool Success, string Message)> UpdateDonationPostAsync(string postId, DonationPost post);
        Task<(bool Success, string Message)> DeleteDonationPostAsync(string postId);
        Task<DonationPost?> GetDonationPostAsync(string postId);
        Task<List<DonationPost>?> GetAllDonationPostsAsync();
        Task<List<DonationPost>> GetUserDonationPostsAsync(string userId);

        // User Profile CRUD operations
        Task<(bool Success, string Message)> SaveUserProfileAsync(User user);
        Task<User?> GetUserProfileAsync(string userId);
        Task<User?> GetUserProfileByUsernameAsync(string username);
        Task<bool> IsUsernameTakenAsync(string username, string? excludeUserId = null);
        Task<(bool Success, string Message)> UpdateUserProfileAsync(User user);

        // Address book operations
        Task<List<Address>> GetAddressesAsync(string userId);
        Task<(bool Success, string Message, string? AddressId)> SaveAddressAsync(string userId, Address address);
        Task<(bool Success, string Message)> DeleteAddressAsync(string userId, string addressId);

        // Saved Items operations
        Task<List<DonationPost>> GetSavedItemsAsync(string userId);
        Task<(bool Success, string Message)> SaveItemAsync(string userId, string postId);
        Task<(bool Success, string Message)> UnsaveItemAsync(string userId, string postId);

        // Donation Request operations
        Task<(bool Success, string Message, string? RequestId)> CreateDonationRequestAsync(DonationRequest request);
        Task<(bool Success, string Message)> UpdateRequestStatusAsync(string requestId, string status);
        Task<(bool Success, string Message)> CancelRequestAsync(string requestId);
        Task<DonationRequest?> GetRequestAsync(string requestId);
        Task<List<DonationRequest>> GetUserRequestsAsync(string userId);
        Task<List<DonationRequest>> GetDonationRequestsForPostAsync(string postId);

        // Notifications
        Task<List<UserNotification>> GetNotificationsAsync(string userId);
        Task<(bool Success, string Message)> DeleteNotificationAsync(string userId, string notificationId);
        Task<(bool Success, string Message)> MarkNotificationAsReadAsync(string userId, string notificationId);
        Task<(bool Success, string Message)> MarkAllNotificationsAsReadAsync(string userId);
        void ListenForNotifications(string userId);

        // Developer / Testing
        Task<(bool Success, string Message)> ResetDatabaseAsync();
        
        // New method to monitor if user is deleted
        IObservable<bool> ListenForUserDeletion(string userId);
    }
}
