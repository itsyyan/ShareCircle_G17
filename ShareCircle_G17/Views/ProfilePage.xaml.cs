using System;
using System.Linq;
using System.IO;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;
        private readonly IFirebaseDatabaseService _databaseService;
        private readonly ISQLiteDatabaseService _sqliteService;

        public static readonly BindableProperty HasUnreadNotificationsProperty =
            BindableProperty.Create(nameof(HasUnreadNotifications), typeof(bool), typeof(ProfilePage), defaultValue: false);

        public bool HasUnreadNotifications
        {
            get => (bool)GetValue(HasUnreadNotificationsProperty);
            set => SetValue(HasUnreadNotificationsProperty, value);
        }

        public ProfilePage(
            IFirebaseAuthService authService, 
            IFirebaseDatabaseService databaseService,
            ISQLiteDatabaseService sqliteService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteService;
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LoadSettings();
            await LoadUserProfile();
            await CheckUnreadNotificationsAsync();
        }

        private async Task CheckUnreadNotificationsAsync()
        {
            try
            {
                if (_authService == null || _databaseService == null) return;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.UserId))
                {
                    // Only check online for now
                    if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                    {
                        var notifications = await _databaseService.GetNotificationsAsync(currentUser.UserId);
                        HasUnreadNotifications = notifications != null && notifications.Any(n => !n.IsRead);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckUnreadNotificationsAsync error: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            var version = AppInfo.Current.VersionString;
            var build = AppInfo.Current.BuildString;
            VersionLabel.Text = $"Version {version} (Build {build})";
        }

        private async Task LoadUserProfile()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();

                if (currentUser == null)
                {
                    return;
                }

                var userId = currentUser.UserId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userId)) return;

                var cachedUser = await _sqliteService.GetUserAsync(userId);
                if (cachedUser != null)
                {
                    UpdateProfileUI(cachedUser);
                    await LoadUserStatistics(userId, useCacheOnly: true);
                }
                else
                {
                    UpdateProfileUI(new User
                    {
                        UserId = userId,
                        Username = currentUser.Username ?? currentUser.Email,
                        Email = currentUser.Email,
                        ProfileImageUrl = currentUser.ProfileImageUrl
                    });
                }

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var freshUser = await _databaseService.GetUserProfileAsync(userId);
                    if (freshUser != null)
                    {
                        UpdateProfileUI(freshUser);
                        await _sqliteService.SaveUserAsync(freshUser);
                        await LoadUserStatistics(userId, useCacheOnly: false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading profile: {ex.Message}");
            }
        }

        private void UpdateProfileUI(User user)
        {
            var displayName = string.IsNullOrWhiteSpace(user.Username)
                    ? "User"
                    : user.Username!;

            UserNameLabel.Text = displayName;
            UserEmailLabel.Text = user.Email ?? string.Empty;
            UserInitialLabel.Text = BuildInitials(displayName);

            var imageSource = CreateImageSource(user.ProfileImageUrl);
            if (imageSource != null)
            {
                UserAvatarImage.Source = imageSource;
                UserAvatarImage.IsVisible = true;
                UserInitialLabel.IsVisible = false;
            }
            else
            {
                UserAvatarImage.IsVisible = false;
                UserInitialLabel.IsVisible = true;
            }
        }

        private async Task LoadUserStatistics(string userId, bool useCacheOnly)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                UpdateStatsUI(0, 0, 0);
                return;
            }

            try
            {
                int listingsCount = 0;
                int completedCount = 0;
                int savedCount = 0;

                if (useCacheOnly)
                {
                    var userPosts = await _sqliteService.GetDonationsByUserIdAsync(userId);
                    
                    listingsCount = userPosts.Count;
                    completedCount = userPosts.Count(p => string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase));
                    
                    var savedIds = await _sqliteService.GetSavedPostIdsAsync(userId);
                    savedCount = savedIds.Count;
                }
                else
                {
                    var userPosts = await _databaseService.GetUserDonationPostsAsync(userId);
                    var savedItems = await _databaseService.GetSavedItemsAsync(userId);

                    listingsCount = userPosts.Count;
                    completedCount = userPosts.Count(p => string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase));
                    savedCount = savedItems.Count;

                    // Sync fetched data to SQLite Cache
                    foreach (var post in userPosts)
                    {
                        await _sqliteService.SaveDonationAsync(post);
                    }

                    var savedIds = savedItems.Where(i => !string.IsNullOrEmpty(i.PostId)).Select(i => i.PostId!).ToList();
                    await _sqliteService.SyncSavedPostIdsAsync(userId, savedIds);
                }

                UpdateStatsUI(listingsCount, completedCount, savedCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading statistics (Cache={useCacheOnly}): {ex.Message}");
            }
        }

        private void UpdateStatsUI(int listings, int completed, int saved)
        {
            ListingsCountLabel.Text = listings.ToString();
            CompletedCountLabel.Text = completed.ToString();
            SavedItemsCountLabel.Text = saved.ToString();
        }

        private static string BuildInitials(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "--";
            }

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
            }

            return char.ToUpperInvariant(text[0]).ToString();
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await LoadUserProfile();
            RefreshView.IsRefreshing = false;
        }

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(EditProfilePage));
        }

        private async void OnMyDonationsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(MyDonationsListPage));
        }

        private async void OnMyRequestsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(MyRequestsPage));
        }

        private async void OnSavedItemsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SavedItemsPage));
        }

        private async void OnListingsStatsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(MyDonationsListPage));
        }

        private async void OnCompletedStatsClicked(object sender, EventArgs e)
        {
            // Navigate to MyDonationsListPage with a query parameter to filter by "Completed"
            var navigationParameters = new Dictionary<string, object>
            {
                { "Filter", "Completed" }
            };
            await Shell.Current.GoToAsync(nameof(MyDonationsListPage), navigationParameters);
        }

        private async void OnSavedItemsStatsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SavedItemsPage));
        }

        private async void OnMyAddressesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(MyAddressesPage));
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(AboutPage));
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
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

        private async void OnClearCacheClicked(object sender, EventArgs e)
        {
            try
            {
                long cacheSize = GetCacheSize();
                string cacheSizeText = FormatBytes(cacheSize);

                bool confirm = await DisplayAlert(
                    "Clear Cache",
                    $"Current cache size: {cacheSizeText}\n\nThis will delete temporary files and free up storage space. Continue?",
                    "Yes",
                    "Cancel"
                );

                if (confirm)
                {
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
                            }
                        }
                    }

                    await DisplayAlert(
                        "Success",
                        $"Cache cleared successfully!\n\nDeleted {deletedCount} file(s)\nFreed up {cacheSizeText}",
                        "OK"
                    );

                    // Also clear database cache
                    if (_sqliteService != null)
                    {
                        await _sqliteService.ClearAllDonationsAsync();
                    }
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
                        }
                    }
                }
            }
            catch
            {
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
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    await DisplayAlert("Error", "No user logged in.", "OK");
                    return;
                }

                var password = await DisplayPromptAsync(
                    "Confirm Identity",
                    "Enter your password to confirm account deletion:",
                    placeholder: "Password",
                    maxLength: 50,
                    keyboard: Keyboard.Default
                );

                if (string.IsNullOrWhiteSpace(password))
                    return;

                var reauthResult = await _authService.SignInAsync(currentUser.Email ?? "", password);
                if (!reauthResult.Success)
                {
                    await DisplayAlert("Error", "Password is incorrect. Account deletion cancelled.", "OK");
                    return;
                }

                await DisplayAlert("Deleting Account", "Please wait while we delete your account data...", "OK");

                var userDonations = await _databaseService.GetUserDonationPostsAsync(currentUser.UserId);
                System.Diagnostics.Debug.WriteLine($"Deleting {userDonations.Count} donations...");
                foreach (var donation in userDonations)
                {
                    if (!string.IsNullOrEmpty(donation.PostId))
                    {
                        await _databaseService.DeleteDonationPostAsync(donation.PostId);
                    }
                }

                var userRequests = await _databaseService.GetUserRequestsAsync(currentUser.UserId);
                System.Diagnostics.Debug.WriteLine($"Deleting {userRequests.Count} requests...");
                foreach (var request in userRequests)
                {
                    if (!string.IsNullOrEmpty(request.RequestId))
                    {
                        await _databaseService.CancelRequestAsync(request.RequestId);
                    }
                }

                var emptyUser = new Models.User
                {
                    UserId = currentUser.UserId,
                    Username = "[Deleted User]",
                    Email = "[Deleted]",
                    ProfileImageUrl = "",
                    TotalDonations = 0
                };
                await _databaseService.SaveUserProfileAsync(emptyUser);

                var deleteResult = await _authService.DeleteAccountAsync();
                if (deleteResult.Success)
                {
                    try
                    {
                        SecureStorage.Remove("saved_email");
                        SecureStorage.Remove("saved_password");
                        SecureStorage.Remove("remember_me");
                    }
                    catch
                    {
                    }

                    await DisplayAlert(
                        "Account Deleted",
                        "Your account and all associated data have been permanently deleted.",
                        "OK"
                    );

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

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Logout",
                "Are you sure you want to logout?",
                "Yes",
                "Cancel"
            );

            if (confirm)
            {
                try
                {
                    await _authService.SignOutAsync();

                    SecureStorage.Remove("saved_email");
                    SecureStorage.Remove("saved_password");
                    SecureStorage.Remove("remember_me");

                    await Shell.Current.GoToAsync("//LoginPage");

                    await DisplayAlert("Success", "You have been logged out successfully.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to logout: {ex.Message}", "OK");
                }
            }
        }

        private async void OnHomeNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//HomePage");
        private async void OnExploreNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ExploreMapPage));
        private async void OnDonateNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DonationPage));
        private async void OnNotificationNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//NotificationsSettingsPage");
        private async void OnProfileNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//ProfilePage");

        private static ImageSource? CreateImageSource(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            const string dataPrefix = "data:image";
            if (value.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var commaIndex = value.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        var base64 = value[(commaIndex + 1)..];
                        var bytes = Convert.FromBase64String(base64);
                        return ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                }
                catch
                {
                    return null;
                }
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return ImageSource.FromUri(uri);
            }

            return ImageSource.FromFile(value);
        }
    }
}

