using Microsoft.Extensions.Logging;
using ShareCircle_G17.Services;
using ShareCircle_G17.ViewModels;
using ShareCircle_G17.Views;
using SQLitePCL;

namespace ShareCircle_G17
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Ensure SQLite native binaries are loaded for Microsoft.Data.Sqlite on all targets
            Batteries_V2.Init();

            // Register Services
            builder.Services.AddSingleton<IDonationService, DonationService>();
            // Register FirebaseService with the base URL once here
            builder.Services.AddSingleton(sp =>
                new FirebaseService("https://sharecircle-test-default-rtdb.firebaseio.com/"));
            builder.Services.AddSingleton<DonationSyncService>();

            // Register ViewModels
            builder.Services.AddTransient<DonationViewModel>();
            builder.Services.AddTransient<CommunityViewModel>();
            builder.Services.AddTransient<UserDonationViewModel>();
            builder.Services.AddTransient<DonationEditViewModel>();

            // Register Pages
            builder.Services.AddTransient<DonationPage>();
            builder.Services.AddTransient<CommunityPage>();
            builder.Services.AddTransient<DonationDetailPage>();
            builder.Services.AddTransient<UserDonationPage>();
            builder.Services.AddTransient<DonationEditPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
