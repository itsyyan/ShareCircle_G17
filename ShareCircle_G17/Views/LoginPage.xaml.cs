using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Services;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace ShareCircle_G17.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;
        private readonly IFirebaseDatabaseService _databaseService;
        private readonly ISQLiteDatabaseService _sqliteService;

        // SecureStorage keys for remember me feature
        private const string SAVED_EMAIL_KEY = "saved_email";
        private const string SAVED_PASSWORD_KEY = "saved_password";
        private const string REMEMBER_ME_KEY = "remember_me";

        public LoginPage(
            IFirebaseAuthService authService,
            IFirebaseDatabaseService databaseService,
            ISQLiteDatabaseService sqliteDatabaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteDatabaseService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Check if user has saved credentials
                string rememberMe = await SecureStorage.GetAsync(REMEMBER_ME_KEY);

                if (rememberMe == "true")
                {
                    RememberMeCheckBox.IsChecked = true;

                    // Load saved credentials
                    string savedEmail = await SecureStorage.GetAsync(SAVED_EMAIL_KEY);
                    string savedPassword = await SecureStorage.GetAsync(SAVED_PASSWORD_KEY);

                    if (!string.IsNullOrEmpty(savedEmail))
                    {
                        UsernameEntry.Text = savedEmail;
                    }

                    if (!string.IsNullOrEmpty(savedPassword))
                    {
                        PasswordEntry.Text = savedPassword;
                    }

                    // If credentials exist, auto sign-in silently
                    if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword))
                    {
                        await AutoLoginAsync(savedEmail, savedPassword);
                    }
                }
            }
            catch (Exception ex)
            {
                // If there's an error loading saved credentials, just continue without them
                System.Diagnostics.Debug.WriteLine($"Error loading saved credentials: {ex.Message}");
            }
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            try
            {
                // Disable button to prevent multiple clicks
                button.IsEnabled = false;

                // Get input values (username or email accepted)
                string usernameOrEmail = UsernameEntry.Text?.Trim() ?? string.Empty;
                string password = PasswordEntry.Text ?? string.Empty;

                // Validation
                if (string.IsNullOrWhiteSpace(usernameOrEmail))
                {
                    await DisplayAlert("Error", "Please enter your username or email.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    await DisplayAlert("Error", "Please enter your password.", "OK");
                    return;
                }

                // Resolve username to email if possible (best effort, falls back to offline lookup)
                string emailToUse = usernameOrEmail;
                Models.User? resolvedProfile = null;
                if (!usernameOrEmail.Contains("@"))
                {
                    try
                    {
                        resolvedProfile = await _databaseService.GetUserProfileByUsernameAsync(usernameOrEmail);
                        if (!string.IsNullOrWhiteSpace(resolvedProfile?.Email))
                        {
                            emailToUse = resolvedProfile.Email!;
                        }
                    }
                    catch
                    {
                        // ignore, fallback to offline path
                    }
                }

                bool loggedIn = false;
                string resultMessage = "Login failed";
                bool accountDeactivated = false;

                // Online-first attempt
                if (IsOnline())
                {
                    var (success, message, user) = await _authService.SignInAsync(emailToUse, password);
                    if (success && user != null)
                    {
                        await PersistLocalUserAsync(user, password, resolvedProfile, emailToUse);
                        loggedIn = true;
                        resultMessage = message;
                    }
                    else
                    {
                        resultMessage = message;
                        // Check if account was deactivated by admin - don't allow offline fallback
                        if (message.Contains("deactivated") || message.Contains("verify account status"))
                        {
                            accountDeactivated = true;
                            // Also remove local cached user data to prevent future offline login
                            await RemoveLocalUserAsync(usernameOrEmail);
                        }
                    }
                }

                // Offline fallback ONLY if not logged in AND account is not deactivated
                if (!loggedIn && !accountDeactivated)
                {
                    var offlineResult = await TryOfflineLoginAsync(usernameOrEmail, password);
                    loggedIn = offlineResult.Success;
                    // Only overwrite message if offline login logic actually ran/failed specifically
                    if (!IsOnline()) 
                    {
                         resultMessage = offlineResult.Message;
                    }
                    // If online failed (wrong password) and offline also failed, keep online message?
                    // Actually TryOfflineLoginAsync returns specific messages.
                    if (!loggedIn)
                    {
                        // If we tried offline and failed, use that message if we are truly offline.
                        // If we are online, the online error is more authoritative (e.g. wrong password).
                        // But if online failed due to connection glitch (IsOnline() check is instantaneous), offline might have tried.
                    }
                }

                if (loggedIn)
                {
                    // Save credentials if "Remember Me" is checked
                    if (RememberMeCheckBox.IsChecked)
                    {
                        await SecureStorage.SetAsync(SAVED_EMAIL_KEY, emailToUse);
                        await SecureStorage.SetAsync(SAVED_PASSWORD_KEY, password);
                        await SecureStorage.SetAsync(REMEMBER_ME_KEY, "true");
                    }
                    else
                    {
                        // Clear saved credentials if unchecked
                        SecureStorage.Remove(SAVED_EMAIL_KEY);
                        SecureStorage.Remove(SAVED_PASSWORD_KEY);
                        SecureStorage.Remove(REMEMBER_ME_KEY);
                    }

                    // Navigate directly without showing success alert
                    await Shell.Current.GoToAsync("//HomePage");
                }
                else
                {
                    await DisplayAlert("Login Failed", resultMessage, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            // Navigate to SignUpPage
            await Shell.Current.GoToAsync(nameof(SignUpPage));
        }

        private void OnRememberMeLabelTapped(object sender, EventArgs e)
        {
            // Toggle the checkbox when the label is tapped
            RememberMeCheckBox.IsChecked = !RememberMeCheckBox.IsChecked;
        }

        private async void OnForgetPasswordClicked(object sender, EventArgs e)
        {
            try
            {
                // Navigate to ForgotPasswordPage
                await Shell.Current.GoToAsync(nameof(ForgotPasswordPage));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not navigate: {ex.Message}", "OK");
            }
        }

        private async Task AutoLoginAsync(string email, string password)
        {
            try
            {
                if (IsOnline())
                {
                    var (success, message, user) = await _authService.SignInAsync(email, password);
                    if (success && user != null)
                    {
                        await Shell.Current.GoToAsync("//HomePage");
                        return;
                    }

                    // If account was deactivated, remove local data and don't try offline login
                    if (message.Contains("deactivated") || message.Contains("verify account status"))
                    {
                        await RemoveLocalUserAsync(email);
                        System.Diagnostics.Debug.WriteLine("Auto-login blocked: account deactivated.");
                        return;
                    }
                }

                var offline = await TryOfflineLoginAsync(email, password);
                if (offline.Success)
                {
                    await Shell.Current.GoToAsync("//HomePage");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Auto-login failed (offline + online).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-login error: {ex.Message}");
            }
        }

        private bool IsOnline()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }

        private async Task PersistLocalUserAsync(Models.User user, string password, Models.User? resolvedProfile, string emailFallback)
        {
            await _sqliteService.InitializeDatabaseAsync();

            var local = await _sqliteService.GetUserAsync(user.UserId!) ?? new Models.User
            {
                UserId = user.UserId,
                CreatedAt = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt
            };

            local.Username = string.IsNullOrWhiteSpace(user.Username)
                ? (resolvedProfile?.Username ?? local.Username ?? "User")
                : user.Username;
            local.UsernameLower = local.Username?.ToLowerInvariant();
            local.Email = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : (resolvedProfile?.Email ?? emailFallback);
            local.ProfileImageUrl = string.IsNullOrWhiteSpace(user.ProfileImageUrl) ? local.ProfileImageUrl : user.ProfileImageUrl;

            var (hash, salt) = CreatePasswordHash(password);
            local.LocalPasswordHash = hash;
            local.LocalPasswordSalt = salt;

            await _sqliteService.SaveUserAsync(local);
        }

        private (string Hash, string Salt) CreatePasswordHash(string password)
        {
            var saltBytes = new byte[16];
            RandomNumberGenerator.Fill(saltBytes);

            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password).Concat(saltBytes).ToArray());

            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        private bool VerifyPassword(string password, string saltBase64, string hashBase64)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(saltBase64);
                using var sha = SHA256.Create();
                var computed = sha.ComputeHash(Encoding.UTF8.GetBytes(password).Concat(saltBytes).ToArray());
                var computedBase64 = Convert.ToBase64String(computed);
                return string.Equals(computedBase64, hashBase64, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private async Task<(bool Success, string Message)> TryOfflineLoginAsync(string usernameOrEmail, string password)
        {
            await _sqliteService.InitializeDatabaseAsync();

            Models.User? localUser;
            if (usernameOrEmail.Contains("@"))
            {
                localUser = await _sqliteService.GetUserByEmailAsync(usernameOrEmail);
            }
            else
            {
                localUser = await _sqliteService.GetUserByUsernameAsync(usernameOrEmail);
                localUser ??= await _sqliteService.GetUserByEmailAsync(usernameOrEmail);
            }

            if (localUser == null)
            {
                return (false, "Offline login failed: account not found locally. Please login online once.");
            }

            if (string.IsNullOrWhiteSpace(localUser.LocalPasswordHash) || string.IsNullOrWhiteSpace(localUser.LocalPasswordSalt))
            {
                return (false, "Offline login unavailable. Please login online once to enable offline access.");
            }

            var ok = VerifyPassword(password, localUser.LocalPasswordSalt!, localUser.LocalPasswordHash!);
            return ok
                ? (true, "Signed in offline.")
                : (false, "Offline login failed: incorrect password.");
        }

        private async Task RemoveLocalUserAsync(string usernameOrEmail)
        {
            try
            {
                await _sqliteService.InitializeDatabaseAsync();

                Models.User? localUser;
                if (usernameOrEmail.Contains("@"))
                {
                    localUser = await _sqliteService.GetUserByEmailAsync(usernameOrEmail);
                }
                else
                {
                    localUser = await _sqliteService.GetUserByUsernameAsync(usernameOrEmail);
                    localUser ??= await _sqliteService.GetUserByEmailAsync(usernameOrEmail);
                }

                if (localUser != null)
                {
                    await _sqliteService.DeleteUserAsync(localUser);
                }

                // Also clear saved credentials
                SecureStorage.Remove(SAVED_EMAIL_KEY);
                SecureStorage.Remove(SAVED_PASSWORD_KEY);
                SecureStorage.Remove(REMEMBER_ME_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing local user: {ex.Message}");
            }
        }
    }
}
