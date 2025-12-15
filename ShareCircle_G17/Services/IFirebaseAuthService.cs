using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public interface IFirebaseAuthService
    {
        Task<(bool Success, string Message, User? User)> SignUpAsync(string username, string email, string password);
        Task<(bool Success, string Message, User? User)> SignInAsync(string email, string password);
        Task SignOutAsync();
        void SignOut(); // Synchronous sign out wrapper
        Task<User?> GetCurrentUserAsync();
        bool IsUserSignedIn();
        Task<(bool Success, string Message)> ChangePasswordAsync(string newPassword);
        Task<(bool Success, string Message)> DeleteAccountAsync();
        Task<(bool Success, string Message)> SendPasswordResetEmailAsync(string email);
    }
}
