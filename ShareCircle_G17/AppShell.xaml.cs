using ShareCircle_G17.Views;
using ShareCircle_G17.Services;

namespace ShareCircle_G17;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        // Register routes with dependency injection support
        // This ensures pages are created using DI container
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(SignUpPage), typeof(SignUpPage));
        Routing.RegisterRoute(nameof(DonationPage), typeof(DonationPage));
        Routing.RegisterRoute(nameof(ProductDetailsPage), typeof(ProductDetailsPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));
        Routing.RegisterRoute(nameof(MyDonationsListPage), typeof(MyDonationsListPage));
        Routing.RegisterRoute(nameof(SavedItemsPage), typeof(SavedItemsPage));
        Routing.RegisterRoute(nameof(MyRequestsPage), typeof(MyRequestsPage));
        Routing.RegisterRoute(nameof(NotificationsSettingsPage), typeof(NotificationsSettingsPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(ForgotPasswordPage), typeof(ForgotPasswordPage));
        Routing.RegisterRoute(nameof(MyAddressesPage), typeof(MyAddressesPage));
        Routing.RegisterRoute(nameof(AddAddressPage), typeof(AddAddressPage));
        Routing.RegisterRoute(nameof(AddressSearchPage), typeof(AddressSearchPage));
        Routing.RegisterRoute(nameof(AddressMapPage), typeof(AddressMapPage));
        Routing.RegisterRoute(nameof(RequestDetailsPage), typeof(RequestDetailsPage));
        Routing.RegisterRoute(nameof(ExploreMapPage), typeof(ExploreMapPage));
        Routing.RegisterRoute(nameof(PrivacyPolicyPage), typeof(PrivacyPolicyPage));
        Routing.RegisterRoute(nameof(TermsOfServicePage), typeof(TermsOfServicePage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
    }

    private IDisposable? _userDeletionSubscription;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartListeners();
    }

    private async Task StartListeners()
    {
        try
        {
            var authService = Handler?.MauiContext?.Services.GetService<IFirebaseAuthService>();
            var dbService = Handler?.MauiContext?.Services.GetService<IFirebaseDatabaseService>();

            if (authService != null && dbService != null)
            {
                var user = await authService.GetCurrentUserAsync();
                if (user != null && !string.IsNullOrEmpty(user.UserId))
                {
                    // 1. Listen for notifications
                    dbService.ListenForNotifications(user.UserId);

                    // 2. Listen for account deletion (admin action)
                    _userDeletionSubscription?.Dispose();
                    _userDeletionSubscription = dbService.ListenForUserDeletion(user.UserId)
                        .Subscribe(async isDeleted =>
                        {
                            if (isDeleted)
                            {
                                await MainThread.InvokeOnMainThreadAsync(async () =>
                                {
                                    await DisplayAlert("Session Expired", "Your account has been removed by an administrator.", "OK");
                                    authService.SignOut();
                                    await GoToAsync("//LoginPage");
                                });
                            }
                        });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting listeners: {ex.Message}");
        }
    }
}
