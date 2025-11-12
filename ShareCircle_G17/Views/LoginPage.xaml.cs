using System;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            // ... validation logic ...

            // Use the route name "HomePage" defined in AppShell.xaml
            // The '//' prefix clears the stack so the user cannot navigate back to the LoginPage.
            await Shell.Current.GoToAsync("//HomePage");
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            // Navigate to SignUpPage
            await Shell.Current.GoToAsync(nameof(SignUpPage));
        }
    }
}
