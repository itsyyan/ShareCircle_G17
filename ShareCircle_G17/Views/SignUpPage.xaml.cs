using System;
using Microsoft.Maui.Controls;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class SignUpPage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;
        private readonly IFirebaseDatabaseService _databaseService;

        public SignUpPage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
        }

        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            try
            {
                // Get input values
                string username = UsernameEntry.Text?.Trim() ?? string.Empty;
                string email = EmailEntry.Text?.Trim() ?? string.Empty;
                string password = PasswordEntry.Text ?? string.Empty;
                string confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

                // Validation
                if (string.IsNullOrWhiteSpace(username))
                {
                    await DisplayAlert("Error", "Please enter a username.", "OK");
                    return;
                }

                // Check username uniqueness
                var taken = await _databaseService.IsUsernameTakenAsync(username);
                if (taken)
                {
                    await DisplayAlert("Error", "Username is already taken. Please choose another.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    await DisplayAlert("Error", "Please enter your email.", "OK");
                    return;
                }

                // Email format validation
                if (!email.Contains("@") || !email.Contains("."))
                {
                    await DisplayAlert("Error", "Please enter a valid email address.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    await DisplayAlert("Error", "Please enter a password.", "OK");
                    return;
                }

                if (password.Length < 6)
                {
                    await DisplayAlert("Error", "Password must be at least 6 characters long.", "OK");
                    return;
                }

                if (password != confirmPassword)
                {
                    await DisplayAlert("Error", "Passwords do not match.", "OK");
                    return;
                }

                // Disable button to prevent multiple clicks
                var button = (Button)sender;
                button.IsEnabled = false;

                // Attempt to sign up
                var (success, message, user) = await _authService.SignUpAsync(username, email, password);

                if (success && user != null)
                {
                    // Save user profile to database
                    var (dbSuccess, dbMessage) = await _databaseService.SaveUserProfileAsync(user);

                    if (!dbSuccess)
                    {
                        button.IsEnabled = true;
                        await DisplayAlert("Profile Save Failed", $"Could not save username profile: {dbMessage}", "OK");
                        return;
                    }

                    // Navigate directly to HomePage without showing success alert
                    await Shell.Current.GoToAsync("//HomePage");
                }
                else
                {
                    // Re-enable button on failure
                    button.IsEnabled = true;

                    // Show error message
                    await DisplayAlert("Sign Up Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                // Re-enable button on error
                if (sender is Button btn)
                    btn.IsEnabled = true;

                await DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
            }
        }

        private async void OnSignInClicked(object sender, EventArgs e)
        {
            // Navigate to LoginPage
            await Shell.Current.GoToAsync(nameof(LoginPage));
        }
    }
}

