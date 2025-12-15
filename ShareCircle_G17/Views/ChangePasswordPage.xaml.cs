using Microsoft.Maui.Controls;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class ChangePasswordPage : ContentPage
    {
        private readonly IFirebaseAuthService? _authService;
        private readonly IFirebaseDatabaseService? _databaseService;

        // Constructor with dependency injection
        public ChangePasswordPage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            LoadUserInfo();
        }

        // Parameterless constructor for design time
        public ChangePasswordPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserInfoAsync();
        }

        private async void LoadUserInfo()
        {
            await LoadUserInfoAsync();
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                if (_authService == null || _databaseService == null)
                    return;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    var userProfile = await _databaseService.GetUserProfileAsync(currentUser.UserId ?? "");
                    UserEmailLabel.Text = $"Email: {userProfile?.Email ?? currentUser.Email ?? "Not available"}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadUserInfo error: {ex.Message}");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            try
            {
                if (_authService == null)
                {
                    await DisplayAlert("Error", "Authentication service not available.", "OK");
                    return;
                }

                // Validate inputs
                if (string.IsNullOrWhiteSpace(CurrentPasswordEntry.Text))
                {
                    await DisplayAlert("Error", "Please enter your current password.", "OK");
                    CurrentPasswordEntry.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewPasswordEntry.Text))
                {
                    await DisplayAlert("Error", "Please enter a new password.", "OK");
                    NewPasswordEntry.Focus();
                    return;
                }

                if (NewPasswordEntry.Text.Length < 6)
                {
                    await DisplayAlert("Error", "New password must be at least 6 characters.", "OK");
                    NewPasswordEntry.Focus();
                    return;
                }

                if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
                {
                    await DisplayAlert("Error", "New passwords do not match.", "OK");
                    ConfirmPasswordEntry.Focus();
                    return;
                }

                if (CurrentPasswordEntry.Text == NewPasswordEntry.Text)
                {
                    await DisplayAlert("Error", "New password must be different from current password.", "OK");
                    NewPasswordEntry.Focus();
                    return;
                }

                // Disable button to prevent double-click
                ChangePasswordButton.IsEnabled = false;
                ChangePasswordButton.Text = "Changing Password...";

                // Get current user
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.Email))
                {
                    await DisplayAlert("Error", "Please log in to change password.", "OK");
                    return;
                }

                // Verify current password by re-authenticating
                var reauthResult = await _authService.SignInAsync(currentUser.Email, CurrentPasswordEntry.Text);
                if (!reauthResult.Success)
                {
                    await DisplayAlert("Error", "Current password is incorrect.", "OK");
                    CurrentPasswordEntry.Text = string.Empty;
                    CurrentPasswordEntry.Focus();
                    return;
                }

                // Change password
                var result = await _authService.ChangePasswordAsync(NewPasswordEntry.Text);

                if (result.Success)
                {
                    await DisplayAlert(
                        "Success",
                        "Your password has been changed successfully!",
                        "OK"
                    );

                    // Clear all fields
                    CurrentPasswordEntry.Text = string.Empty;
                    NewPasswordEntry.Text = string.Empty;
                    ConfirmPasswordEntry.Text = string.Empty;

                    // Navigate back
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to change password: {ex.Message}", "OK");
            }
            finally
            {
                // Re-enable button
                ChangePasswordButton.IsEnabled = true;
                ChangePasswordButton.Text = "Change Password";
            }
        }
    }
}
