using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace ShareCircle_G17
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
        DataScheme = "com.companyname.sharecircle",
        DataHost = "auth")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set navigation bar color to light purple to match page gradient bottom
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop && Window != null)
            {
                Window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#EDE7FF"));
            }

            // Set navigation bar to light mode (dark icons)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O && Window != null)
            {
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(SystemUiFlags.LightNavigationBar);
            }
        }
    }
}
