using System;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Views
{
    public partial class SignUpPage : ContentPage
    {
        public SignUpPage()
        {
            InitializeComponent();
        }

        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            // ... validation logic ...
            // TODO: Add sign up validation and account creation logic

            // Navigate to HomePage after successful sign up
            // The '//' prefix clears the stack so the user cannot navigate back to the SignUpPage.
            await Shell.Current.GoToAsync("//HomePage");
        }

        private async void OnSignInClicked(object sender, EventArgs e)
        {
            // Navigate back to LoginPage
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}

