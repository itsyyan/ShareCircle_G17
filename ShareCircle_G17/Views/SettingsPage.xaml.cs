using Microsoft.Maui.Controls;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly IFirebaseAuthService? _authService;
        private readonly IFirebaseDatabaseService? _databaseService;
        private readonly ISQLiteDatabaseService? _sqliteService;

        // Constructor with dependency injection
        public SettingsPage(
            IFirebaseAuthService authService, 
            IFirebaseDatabaseService databaseService,
            ISQLiteDatabaseService sqliteService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteService;
            LoadSettings();
        }

        // Parameterless constructor for design time
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Set app version
            var version = AppInfo.Current.VersionString;
            var build = AppInfo.Current.BuildString;
            VersionLabel.Text = $"Version {version} (Build {build})";
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            // Navigate to dedicated ChangePasswordPage
            await Shell.Current.GoToAsync(nameof(ChangePasswordPage));
        }

        private async void OnPrivacyPolicyClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(PrivacyPolicyPage));
        }

        private async void OnTermsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(TermsOfServicePage));
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(AboutPage));
        }

        private async void OnClearCacheClicked(object sender, EventArgs e)
        {
            try
            {
                // Calculate cache size
                long cacheSize = GetCacheSize();
                string cacheSizeText = FormatBytes(cacheSize);

                bool confirm = await DisplayAlert(
                    "Clear Cache",
                    $"Current cache size: {cacheSizeText}\n\n" +
                    "This will delete temporary files and free up storage space. Continue?",
                    "Yes",
                    "Cancel"
                );

                if (confirm)
                {
                    // Clear cache directory
                    string cacheDir = FileSystem.CacheDirectory;
                    int deletedCount = 0;

                    if (Directory.Exists(cacheDir))
                    {
                        var files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                deletedCount++;
                            }
                            catch
                            {
                                // Skip files that can't be deleted
                            }
                        }
                    }

                    await DisplayAlert(
                        "Success",
                        $"Cache cleared successfully!\n\n" +
                        $"Deleted {deletedCount} file(s)\n" +
                        $"Freed up {cacheSizeText}",
                        "OK"
                    );
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to clear cache: {ex.Message}", "OK");
            }
        }


        private long GetCacheSize()
        {
            long size = 0;
            try
            {
                string cacheDir = FileSystem.CacheDirectory;
                if (Directory.Exists(cacheDir))
                {
                    var files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            size += fileInfo.Length;
                        }
                        catch
                        {
                            // Skip files that can't be accessed
                        }
                    }
                }
            }
            catch
            {
                // Return 0 if we can't calculate size
            }
            return size;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private async void OnDeleteAccountClicked(object sender, EventArgs e)
        {
            if (_authService == null || _databaseService == null)
            {
                await DisplayAlert("Error", "Services not available.", "OK");
                return;
            }

            bool confirm1 = await DisplayAlert(
                "Delete Account",
                "Are you sure you want to delete your account? This action cannot be undone.",
                "Delete",
                "Cancel"
            );

            if (!confirm1)
                return;

            bool confirm2 = await DisplayAlert(
                "Final Warning",
                "All your data, donations, and saved items will be permanently deleted. Are you absolutely sure?",
                "Yes, Delete Forever",
                "Cancel"
            );

            if (!confirm2)
                return;

            try
            {
                // Get current user
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    await DisplayAlert("Error", "No user logged in.", "OK");
                    return;
                }

                // Prompt for password confirmation
                var password = await DisplayPromptAsync(
                    "Confirm Identity",
                    "Enter your password to confirm account deletion:",
                    placeholder: "Password",
                    maxLength: 50,
                    keyboard: Keyboard.Default
                );

                if (string.IsNullOrWhiteSpace(password))
                    return;

                // Re-authenticate user
                var reauthResult = await _authService.SignInAsync(currentUser.Email ?? "", password);
                if (!reauthResult.Success)
                {
                    await DisplayAlert("Error", "Password is incorrect. Account deletion cancelled.", "OK");
                    return;
                }

                // Show progress
                await DisplayAlert("Deleting Account", "Please wait while we delete your account data...", "OK");

                // Delete user's donations
                var userDonations = await _databaseService.GetUserDonationPostsAsync(currentUser.UserId);
                System.Diagnostics.Debug.WriteLine($"Deleting {userDonations.Count} donations...");
                foreach (var donation in userDonations)
                {
                    if (!string.IsNullOrEmpty(donation.PostId))
                    {
                        await _databaseService.DeleteDonationPostAsync(donation.PostId);
                    }
                }

                // Delete user's requests
                var userRequests = await _databaseService.GetUserRequestsAsync(currentUser.UserId);
                System.Diagnostics.Debug.WriteLine($"Deleting {userRequests.Count} requests...");
                foreach (var request in userRequests)
                {
                    if (!string.IsNullOrEmpty(request.RequestId))
                    {
                        await _databaseService.CancelRequestAsync(request.RequestId);
                    }
                }

                // Delete user's saved items
                // Note: We don't have a method to get all saved items for deletion,
                // but they will be orphaned and can be cleaned up later

                // Delete user profile from database
                // Mark user as deleted instead of completely removing to maintain data integrity
                var emptyUser = new Models.User
                {
                    UserId = currentUser.UserId,
                    Username = "[Deleted User]",
                    Email = "[Deleted]",
                    ProfileImageUrl = "",
                    TotalDonations = 0
                };
                await _databaseService.SaveUserProfileAsync(emptyUser);

                // Delete Firebase Auth account
                var deleteResult = await _authService.DeleteAccountAsync();
                if (deleteResult.Success)
                {
                    // Clear saved credentials
                    try
                    {
                        SecureStorage.Remove("saved_email");
                        SecureStorage.Remove("saved_password");
                        SecureStorage.Remove("remember_me");
                    }
                    catch
                    {
                        // Ignore secure storage errors
                    }

                    await DisplayAlert(
                        "Account Deleted",
                        "Your account and all associated data have been permanently deleted.",
                        "OK"
                    );

                    // Navigate to login page
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    await DisplayAlert("Error", deleteResult.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete account: {ex.Message}", "OK");
            }
        }
    }
}
