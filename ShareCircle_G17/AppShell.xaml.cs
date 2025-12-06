using ShareCircle_G17.Views;

namespace ShareCircle_G17;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes used by Shell navigation
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(SignUpPage), typeof(SignUpPage));
        Routing.RegisterRoute(nameof(DonationPage), typeof(DonationPage));
        Routing.RegisterRoute(nameof(UserDonationPage), typeof(UserDonationPage));
        Routing.RegisterRoute(nameof(DonationEditPage), typeof(DonationEditPage));
        Routing.RegisterRoute(nameof(CommunityPage), typeof(CommunityPage));
        Routing.RegisterRoute(nameof(DonationDetailPage), typeof(DonationDetailPage));
    }
}
