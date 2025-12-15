using System;
using System.Threading.Tasks;
using ShareCircle_G17.Views;   // 👈 Add this line

namespace ShareCircle_G17
{
    public partial class SplashPage : ContentPage
    {
        public SplashPage()
        {
            InitializeComponent();
            StartSplashSequence();
        }

        private async void StartSplashSequence()
        {
            try
            {
                // Fade in smoothly
                this.Opacity = 0;
                await this.FadeTo(1, 1500, Easing.CubicIn);

                // Keep splash visible for total ~5 seconds
                await Task.Delay(3500);

                // Optional fade-out before navigation
                await this.FadeTo(0, 800, Easing.CubicOut);

                // Navigate to LoginPage; AppShell handles remember-me redirect
                await Shell.Current.GoToAsync(nameof(LoginPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash navigation error: {ex.Message}");
                // Fallback: go to HomePage if SignUpPage fails
                try
                {
                    await Shell.Current.GoToAsync("//HomePage");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback navigation failed: {fallbackEx.Message}");
                }
            }
        }
    }
}
