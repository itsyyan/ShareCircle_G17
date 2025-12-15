using Microsoft.Maui.Controls;

#if ANDROID
using Android.Graphics;
using AndroidX.SwipeRefreshLayout.Widget;
#endif

#if IOS || MACCATALYST
using UIKit;
#endif

namespace ShareCircle_G17.Controls;

public class SilentRefreshView : RefreshView
{
#if ANDROID
    private SwipeRefreshLayout? _nativeRefresh;
#elif IOS || MACCATALYST
    private UIRefreshControl? _nativeRefresh;
#endif

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if ANDROID
        if (_nativeRefresh != null)
        {
            _nativeRefresh.Refresh -= OnNativeRefresh;
            _nativeRefresh = null;
        }

        if (Handler?.PlatformView is SwipeRefreshLayout native)
        {
            _nativeRefresh = native;
            native.SetColorSchemeColors(Android.Graphics.Color.Transparent);
            native.SetProgressBackgroundColorSchemeColor(Android.Graphics.Color.Transparent);
            native.Refresh += OnNativeRefresh;
        }
#elif IOS || MACCATALYST
        if (_nativeRefresh != null)
        {
            _nativeRefresh.ValueChanged -= OnNativeRefresh;
            _nativeRefresh = null;
        }

        if (Handler?.PlatformView is UIRefreshControl native)
        {
            _nativeRefresh = native;
            native.TintColor = UIColor.Clear;
            native.BackgroundColor = UIColor.Clear;
            native.ValueChanged += OnNativeRefresh;
        }
#endif
    }

#if ANDROID
    private void OnNativeRefresh(object? sender, System.EventArgs e)
    {
        if (_nativeRefresh != null)
        {
            _nativeRefresh.Refreshing = false; // hide the spinner immediately
        }

        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
#elif IOS || MACCATALYST
    private void OnNativeRefresh(object? sender, System.EventArgs e)
    {
        if (_nativeRefresh != null)
        {
            _nativeRefresh.EndRefreshing(); // hide spinner immediately
        }

        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
#endif
}
