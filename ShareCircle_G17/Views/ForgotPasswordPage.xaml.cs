using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;

        public ForgotPasswordPage(IFirebaseAuthService authService)
        {
            InitializeComponent();
            _authService = authService;
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            string email = EmailEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email))
            {
                await DisplayAlert("Error", "Please enter your email address.", "OK");
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                await DisplayAlert("Error", "Please enter a valid email address.", "OK");
                return;
            }

            // UI Loading State
            SendButton.IsEnabled = false;
            SendButton.Text = "Sending...";
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            try
            {
                var (success, message) = await _authService.SendPasswordResetEmailAsync(email);

                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                SendButton.Text = "Send Reset Link";
                SendButton.IsEnabled = true;

                if (success)
                {
                    await DisplayAlert("Check your email", message, "OK");
                    // Optionally navigate back
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", message, "OK");
                }
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                SendButton.Text = "Send Reset Link";
                SendButton.IsEnabled = true;

                await DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
            }
        }

        private async void OnBackToLoginClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
