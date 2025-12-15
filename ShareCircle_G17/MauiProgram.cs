using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ShareCircle_G17.Services;
using ShareCircle_G17.Views;
#if ANDROID
using Android.Graphics;
using Android.Views;
using AndroidX.Core.View;
using static AndroidX.Core.View.WindowInsetsControllerCompat;
#elif IOS
using UIKit;
using CoreGraphics;
using Microsoft.Maui.Platform;
#endif
namespace ShareCircle_G17
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                {
                    var window = activity.Window;
                    if (window != null)
                    {
                        WindowCompat.SetDecorFitsSystemWindows(window, false);
                        var accent = Android.Graphics.Color.ParseColor("#5B2EFF");
                        window.SetStatusBarColor(accent);
                        window.SetNavigationBarColor(accent);
                        window.ClearFlags(WindowManagerFlags.TranslucentStatus);
                        window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);

                        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
                        if (controller != null)
                        {
                            controller.AppearanceLightStatusBars = false; // use light icons on dark bar
                            controller.AppearanceLightNavigationBars = false;
                        }
                    }
                }));
            });
#endif

            // Register Firebase Services
            builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
            builder.Services.AddSingleton<IFirebaseDatabaseService, FirebaseDatabaseService>();

            // Register SQLite and Sync Services
            builder.Services.AddSingleton<ISQLiteDatabaseService, SQLiteDatabaseService>();
            builder.Services.AddSingleton<ISyncService, SyncService>();
            builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);

            // Register Google Maps Service
            builder.Services.AddSingleton<IGoogleMapsService, GoogleMapsService>();

            // Register Pages for Dependency Injection
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<SignUpPage>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<DonationPage>();
            builder.Services.AddTransient<ProductDetailsPage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<EditProfilePage>();
            builder.Services.AddTransient<MyDonationsListPage>();
            builder.Services.AddTransient<SavedItemsPage>();
            builder.Services.AddTransient<MyRequestsPage>();
            builder.Services.AddTransient<NotificationsSettingsPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<ChangePasswordPage>();
            builder.Services.AddTransient<ForgotPasswordPage>();
            builder.Services.AddTransient<MyAddressesPage>();
            builder.Services.AddTransient<AddAddressPage>();
            builder.Services.AddTransient<AddressSearchPage>();
            builder.Services.AddTransient<AddressMapPage>();
            builder.Services.AddTransient<RequestDetailsPage>();

            // Register AppShell and App
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<App>();

#if ANDROID
            Microsoft.Maui.Maps.Handlers.MapPinHandler.Mapper.AppendToMapping("CustomImage", async (handler, view) =>
            {
                if (view is ShareCircle_G17.Controls.CustomPin customPin)
                {
                    var nativeView = (object)handler.PlatformView;
                    
                    try 
                    {
                        // Pass Count to the drawing method
                        var bitmap = await GetImageBitmapFromUrl(customPin.ImageUrl, customPin.Count);
                        if (bitmap != null)
                        {
                            var descriptor = Android.Gms.Maps.Model.BitmapDescriptorFactory.FromBitmap(bitmap);

                            if (nativeView is Android.Gms.Maps.Model.Marker marker)
                            {
                                marker.SetIcon(descriptor);
                                marker.SetAnchor(0.5f, 0.5f);
                            }
                            else if (nativeView is Android.Gms.Maps.Model.MarkerOptions options)
                            {
                                options.SetIcon(descriptor);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error setting custom marker icon: {ex.Message}");
                    }
                }
            });
#elif IOS
            Microsoft.Maui.Maps.Handlers.MapPinHandler.Mapper.AppendToMapping("CustomImage", async (handler, view) =>
            {
                if (view is ShareCircle_G17.Controls.CustomPin customPin)
                {
                    try
                    {
                        // Pass Count to the drawing method
                        var image = await GetImageFromUrlIOS(customPin.ImageUrl, customPin.Count);
                        if (image != null)
                        {
                            customPin.Image = ImageSource.FromStream(() => image.AsPNG().AsStream());
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error setting custom marker icon (iOS): {ex.Message}");
                    }
                }
            });
#endif

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if IOS
        private static async Task<UIImage?> GetImageFromUrlIOS(string? url, int count)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                byte[] bytes;
                if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) && url.Contains(","))
                {
                    var base64 = url.Substring(url.IndexOf(",") + 1);
                    bytes = Convert.FromBase64String(base64);
                }
                else if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    using var client = new HttpClient();
                    bytes = await client.GetByteArrayAsync(url);
                }
                else
                {
                    return null;
                }

                using var data = Foundation.NSData.FromArray(bytes);
                var original = UIImage.LoadFromData(data);
                if (original == null) return null;

                nfloat width = 64f;  // Main circle diameter
                nfloat height = 84f; // Total height including tail
                nfloat radius = width / 2f;
                nfloat strokeWidth = 3f;
                
                UIGraphics.BeginImageContextWithOptions(new CGSize(width, height), false, 0f);
                var context = UIGraphics.GetCurrentContext();

                // Define Pin Path (Teardrop)
                var pinPath = new UIBezierPath();
                // Center of the circular head
                var center = new CGPoint(width / 2, radius);
                
                // Arc for the top part (approx 30 deg to 150 deg? No, usually -30 to 210)
                // Start angle: radians. 0 is right. PI/2 is down.
                // We want from angle where tail touches (approx 150 deg = 2.61 rad) to (30 deg = 0.52 rad)?
                // Let's use simple trigonometry.
                // Tangent points at approx 45 degrees from bottom center?
                // Let's try Arc + Triangle.
                
                pinPath.AddArc(center, radius - strokeWidth/2, (nfloat)(Math.PI/4), (nfloat)(Math.PI * 3 / 4), false);
                pinPath.AddLineTo(new CGPoint(width/2, height)); // Tip
                pinPath.ClosePath();

                // Fill White
                context.SetFillColor(Colors.White.ToPlatform().CGColor);
                pinPath.Fill();

                // Clip and Draw Image
                context.SaveState();
                var clipPath = UIBezierPath.FromOval(new CGRect(strokeWidth/2, strokeWidth/2, width-strokeWidth, width-strokeWidth));
                clipPath.AddClip();
                original.Draw(new CGRect(0, 0, width, width));
                context.RestoreState();

                // Stroke
                context.SetLineWidth(strokeWidth);
                context.SetStrokeColor(Color.FromArgb("#5B2EFF").ToPlatform().CGColor);
                pinPath.LineWidth = strokeWidth;
                pinPath.Stroke();

                // Badge
                if (count > 1)
                {
                    nfloat badgeSize = 22f;
                    var badgeRect = new CGRect(width - badgeSize, 0, badgeSize, badgeSize);
                    context.SetFillColor(Colors.Red.ToPlatform().CGColor);
                    context.AddEllipseInRect(badgeRect);
                    context.FillPath();

                    var text = $"+{Math.Min(count - 1, 99)}";
                    var nsString = new Foundation.NSString(text);
                    var attributes = new UIStringAttributes { ForegroundColor = UIColor.White, Font = UIFont.BoldSystemFontOfSize(11) };
                    var textSize = nsString.GetSizeUsingAttributes(attributes);
                    nsString.DrawString(new CGRect(badgeRect.X + (badgeRect.Width - textSize.Width)/2, badgeRect.Y + (badgeRect.Height - textSize.Height)/2, textSize.Width, textSize.Height), attributes);
                }

                var result = UIGraphics.GetImageFromCurrentImageContext();
                UIGraphics.EndImageContext();
                return result;
            }
            catch
            {
                return null;
            }
        }
#endif

#if ANDROID
        private static async System.Threading.Tasks.Task<Android.Graphics.Bitmap?> GetImageBitmapFromUrl(string? url, int count)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                byte[] bytes;
                if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) && url.Contains(","))
                {
                    var base64 = url.Substring(url.IndexOf(",") + 1);
                    bytes = Convert.FromBase64String(base64);
                }
                else if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    using var client = new System.Net.Http.HttpClient();
                    bytes = await client.GetByteArrayAsync(url);
                }
                else
                {
                    return null;
                }

                var original = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                if (original == null) return null;

                // Dimensions
                int width = 160;
                int height = 220; // Taller for tail
                int radius = 80;
                int strokeWidth = 8;

                var scaled = Android.Graphics.Bitmap.CreateScaledBitmap(original, width, width, false);
                original.Recycle();
                
                var output = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888!);
                var canvas = new Android.Graphics.Canvas(output);
                var paint = new Android.Graphics.Paint { AntiAlias = true };

                // Define Path
                var path = new Android.Graphics.Path();
                float cx = width / 2f;
                float cy = radius;
                float r = radius - strokeWidth / 2f;

                // Circle part
                path.AddCircle(cx, cy, r, Android.Graphics.Path.Direction.Cw);
                
                // Tail triangle (simple approach: connect tangents approx 45 deg)
                float dx = (float)(r * Math.Sin(Math.PI / 4)); // 45 deg
                float dy = (float)(r * Math.Cos(Math.PI / 4));
                
                var tailPath = new Android.Graphics.Path();
                tailPath.MoveTo(cx - dx, cy + dy);
                tailPath.LineTo(cx, height);
                tailPath.LineTo(cx + dx, cy + dy);
                tailPath.Close();
                
                // Union paths (Fill)
                paint.Color = Android.Graphics.Color.White;
                paint.SetStyle(Android.Graphics.Paint.Style.Fill);
                canvas.DrawPath(path, paint);
                canvas.DrawPath(tailPath, paint);

                // Draw Image (Clipped)
                canvas.Save();
                canvas.ClipPath(path);
                canvas.DrawBitmap(scaled, 0, 0, paint);
                canvas.Restore();

                // Draw Border
                paint.Color = Android.Graphics.Color.ParseColor("#5B2EFF");
                paint.SetStyle(Android.Graphics.Paint.Style.Stroke);
                paint.StrokeWidth = strokeWidth;
                
                // Draw path borders again to overlay
                canvas.DrawPath(path, paint);
                // Draw tail border (needs care to avoid double line inside)
                // Actually simpler: Draw path of the union? 
                // For simplicity, just draw tail border lines
                var borderPath = new Android.Graphics.Path();
                borderPath.MoveTo(cx - dx, cy + dy);
                borderPath.LineTo(cx, height);
                borderPath.LineTo(cx + dx, cy + dy);
                canvas.DrawPath(borderPath, paint);

                // Badge
                if (count > 1)
                {
                    paint.SetStyle(Android.Graphics.Paint.Style.Fill);
                    paint.Color = Android.Graphics.Color.Red;
                    float badgeR = 30f;
                    float badgeX = width - badgeR;
                    float badgeY = badgeR;
                    canvas.DrawCircle(badgeX, badgeY, badgeR, paint);

                    paint.Color = Android.Graphics.Color.White;
                    paint.TextSize = 36;
                    paint.TextAlign = Android.Graphics.Paint.Align.Center;
                    var metrics = paint.GetFontMetrics();
                    canvas.DrawText($"+{Math.Min(count - 1, 99)}", badgeX, badgeY - (metrics.Ascent + metrics.Descent) / 2, paint);
                }

                scaled.Recycle();
                return output;
            }
            catch 
            {
                return null;
            }
        }
#endif

    }
}
