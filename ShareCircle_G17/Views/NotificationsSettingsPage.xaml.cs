using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views;

public partial class NotificationsSettingsPage : ContentPage
{
    private readonly IFirebaseAuthService? _authService;
    private readonly IFirebaseDatabaseService? _databaseService;
    private readonly ISQLiteDatabaseService? _sqliteService;
    private NotificationItem? _selected;
    private bool _isManageMode;
    private string? _currentUserId;

    public static readonly BindableProperty HasUnreadNotificationsProperty =
        BindableProperty.Create(nameof(HasUnreadNotifications), typeof(bool), typeof(NotificationsSettingsPage), defaultValue: false);

    public bool HasUnreadNotifications
    {
        get => (bool)GetValue(HasUnreadNotificationsProperty);
        set => SetValue(HasUnreadNotificationsProperty, value);
    }

    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    public NotificationsSettingsPage()
    {
        InitializeComponent();
        _authService = Resolve<IFirebaseAuthService>() ?? new FirebaseAuthService();
        _databaseService = Resolve<IFirebaseDatabaseService>() ?? new FirebaseDatabaseService();
        _sqliteService = Resolve<ISQLiteDatabaseService>();
        BindingContext = this;
    }

    public NotificationsSettingsPage(
        IFirebaseAuthService authService, 
        IFirebaseDatabaseService databaseService,
        ISQLiteDatabaseService sqliteService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;
        _sqliteService = sqliteService;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        EmptyStateLayout.IsVisible = false;
        // Only show spinner if we have no cached data
        if (Notifications.Count == 0)
        {
            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
        }
        
        _selected = null;

        try
        {
            if (_authService == null)
            {
                EmptyStateLayout.IsVisible = true;
                return;
            }

            var user = await _authService.GetCurrentUserAsync();
            if (user == null || string.IsNullOrWhiteSpace(user.UserId))
            {
                EmptyStateLayout.IsVisible = true;
                return;
            }
            _currentUserId = user.UserId;

            // 1. Load from SQLite Cache
                            if (_sqliteService != null && _databaseService != null)
                            {
                                var cached = await _sqliteService.GetNotificationsAsync(user.UserId);
                                if (cached != null && cached.Count > 0)
                                {
                                    Notifications.Clear();
                                    foreach (var n in cached)
                                    {
                                        Notifications.Add(new NotificationItem(n, _databaseService));
                                    }
                                    LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
                                }
                            }
            
                            // 2. Fetch from Firebase (if online)
                            if (_databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                            {
                                var items = await _databaseService.GetNotificationsAsync(user.UserId);
                                
                                // Update Cache & UI
                                Notifications.Clear();
                                foreach (var n in items)
                                {
                                    n.UserId = user.UserId; // Ensure owner is set for local storage
                                    Notifications.Add(new NotificationItem(n, _databaseService));
                                    
                                    if (_sqliteService != null)
                                    {
                                        await _sqliteService.SaveNotificationAsync(n);
                                    }
                                }
                            }
            _selected = null;
            UpdateSelectionCount();
            
            HasUnreadNotifications = Notifications.Any(n => n.IsUnread);

            EmptyStateLayout.IsVisible = Notifications.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading notifications: {ex.Message}");
            // Only show alert if we have absolutely nothing to show
            if (Notifications.Count == 0)
            {
                await DisplayAlert("Error", $"Failed to load notifications: {ex.Message}", "OK");
                EmptyStateLayout.IsVisible = true;
            }
        }
        finally
        {
            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // Bottom nav handlers
    private async void OnHomeNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//HomePage");
    private async void OnExploreNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//HomePage");
    private async void OnDonateNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DonationPage));
    private async void OnNotificationNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//NotificationsSettingsPage");
    private async void OnProfileNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ProfilePage));

    private bool _suppressCheckBoxEvent;

    private void OnNotificationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isManageMode)
        {
            var currentSelection = e.CurrentSelection.OfType<NotificationItem>().ToList();
            SetSelectionFlags(currentSelection);
            
            // Sync CheckBox without triggering logic
            _suppressCheckBoxEvent = true;
            bool allSelected = Notifications.Count > 0 && currentSelection.Count == Notifications.Count;
            if (SelectAllCheckBox.IsChecked != allSelected)
            {
                SelectAllCheckBox.IsChecked = allSelected;
            }
            _suppressCheckBoxEvent = false;

            UpdateSelectionCount();
            return;
        }

        if (e.CurrentSelection.FirstOrDefault() is NotificationItem item)
        {
            _selected = item;
            NavigateToSelected();
            // Optional: Clear selection to allow re-selecting
            NotificationsCollection.SelectedItem = null;
        }
    }

    private async void NavigateToSelected()
    {
        if (_selected == null)
            return;

        // Mark as read locally immediately
        if (!_selected.IsRead)
        {
            _selected.IsRead = true;
            if (_currentUserId != null && _databaseService != null && !string.IsNullOrWhiteSpace(_selected.NotificationId))
            {
                _ = _databaseService.MarkNotificationAsReadAsync(_currentUserId, _selected.NotificationId);
            }
        }

        try
        {
            // If approved/completed, go to ProductDetailsPage to show donor contact info
            bool isApproved = string.Equals(_selected.Status, "approved", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_selected.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_selected.Status, "accepted", StringComparison.OrdinalIgnoreCase);

            if (isApproved && !string.IsNullOrWhiteSpace(_selected.PostId))
            {
                await Shell.Current.GoToAsync($"{nameof(ProductDetailsPage)}?PostId={_selected.PostId}");
                return;
            }

            // Otherwise (e.g., pending request, rejected, or no PostId), go to RequestDetailsPage
            if (!string.IsNullOrWhiteSpace(_selected.RequestId))
            {
                var nav = new Dictionary<string, object>
                {
                    { "RequestId", _selected.RequestId }
                };
                await Shell.Current.GoToAsync(nameof(RequestDetailsPage), nav);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation", $"Unable to open details: {ex.Message}", "OK");
        }
    }

    private void OnManageClicked(object sender, EventArgs e)
    {
        _isManageMode = !_isManageMode;
        ManageButton.IsVisible = !_isManageMode;
        ActionButton.IsVisible = _isManageMode;
        SelectAllCheckBox.IsVisible = _isManageMode;
        TitleLabel.IsVisible = !_isManageMode;
        SelectionCountLabel.IsVisible = _isManageMode;
        
        // Always None for manual control
        NotificationsCollection.SelectionMode = SelectionMode.None;
        
        // Update items selection mode
        foreach (var n in Notifications)
        {
            n.IsSelectionMode = _isManageMode;
        }
        
        if (_isManageMode)
        {
            // Reset checkbox when entering manage mode
            _suppressCheckBoxEvent = true;
            SelectAllCheckBox.IsChecked = false;
            _suppressCheckBoxEvent = false;
        }
        else
        {
            // Clear selection when exiting
            foreach (var n in Notifications) n.IsSelected = false;
        }
        UpdateSelectionCount();
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        var item = e.Parameter as NotificationItem ?? (sender as Element)?.BindingContext as NotificationItem;
        if (item == null) return;

        if (_isManageMode)
        {
            // Toggle selection
            item.IsSelected = !item.IsSelected;
            
            // Sync CheckBox
            _suppressCheckBoxEvent = true;
            bool allSelected = Notifications.Count > 0 && Notifications.All(n => n.IsSelected);
            if (SelectAllCheckBox.IsChecked != allSelected)
            {
                SelectAllCheckBox.IsChecked = allSelected;
            }
            _suppressCheckBoxEvent = false;

            UpdateSelectionCount();
        }
        else
        {
            _selected = item;
            NavigateToSelected();
        }
    }

    private void OnNotificationDetailsTapped(object? sender, EventArgs e)
    {
        if (sender is Element el && el.BindingContext is NotificationItem item)
        {
            // Redirect to general tap handler
            OnCardTapped(sender, new TappedEventArgs(item));
        }
    }

    private void OnSelectAllCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!_isManageMode || _suppressCheckBoxEvent) return;
        
        bool isChecked = e.Value;
        
        foreach (var n in Notifications)
        {
            n.IsSelected = isChecked;
        }
        
        UpdateSelectionCount();
    }

    private async void OnActionButtonClicked(object sender, EventArgs e)
    {
        if (!_isManageMode) return;
        var selected = Notifications.Where(n => n.IsSelected).ToList();
        
        if (selected.Count == 0)
        {
            // "Done" -> exit manage mode
            OnManageClicked(sender, e);
            return;
        }

        if (_currentUserId == null || _databaseService == null) return;

        bool confirm = await DisplayAlert("Delete", $"Delete {selected.Count} notifications?", "Yes", "No");
        if (!confirm) return;

        foreach (var item in selected)
        {
            if (!string.IsNullOrWhiteSpace(item.NotificationId))
            {
                await _databaseService.DeleteNotificationAsync(_currentUserId, item.NotificationId);
                
                // Also delete from local cache to prevent "flash back"
                if (_sqliteService != null)
                {
                    await _sqliteService.DeleteNotificationAsync(item.Source);
                }
            }
            Notifications.Remove(item);
        }

        // Reset state
        _suppressCheckBoxEvent = true;
        SelectAllCheckBox.IsChecked = false;
        _suppressCheckBoxEvent = false;
        
        UpdateSelectionCount();
        EmptyStateLayout.IsVisible = Notifications.Count == 0;
        
        if (Notifications.Count == 0) OnManageClicked(sender, e);
    }

    private void SetSelectionFlags(IEnumerable<NotificationItem> selected)
    {
        var set = new HashSet<string>(selected.Select(s => s.NotificationId), StringComparer.OrdinalIgnoreCase);
        foreach (var n in Notifications)
        {
            n.IsSelected = set.Contains(n.NotificationId);
        }
    }

    private void UpdateSelectionCount()
    {
        var count = Notifications.Count(n => n.IsSelected);
        SelectionCountLabel.Text = $"Selected {count}";
        SelectionCountLabel.IsVisible = _isManageMode;
        if (ActionButton != null)
        {
            if (count > 0)
            {
                ActionButton.Text = "Delete";
                ActionButton.BackgroundColor = Color.FromArgb("#FEE2E2");
                ActionButton.TextColor = Color.FromArgb("#DC2626");
            }
            else
            {
                ActionButton.Text = "Done";
                ActionButton.BackgroundColor = Color.FromArgb("#6D5BFF");
                ActionButton.TextColor = Colors.White;
            }
        }
    }

    private T? Resolve<T>() where T : class
    {
        return Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(T)) as T;
    }
}

public class NotificationItem : INotifyPropertyChanged
{
    private readonly IFirebaseDatabaseService _databaseService;

    public NotificationItem(UserNotification source, IFirebaseDatabaseService databaseService)
    {
        Source = source;
        _databaseService = databaseService;
        NotificationId = source.NotificationId ?? Guid.NewGuid().ToString();
        // Title = BuildTitle(source); // Will be built after username is fetched
        LinkText = BuildLinkText(source);
        Status = BuildStatus(source);
        StatusColor = BuildStatusColor(source);
        TimeAgo = BuildTimeAgo(source.CreatedAt);
        ImageUrl = string.IsNullOrWhiteSpace(source.ItemImageUrl)
            ? "https://via.placeholder.com/120x120/EEF2FF/9CA3AF?text=Item"
            : source.ItemImageUrl;
        PostId = source.PostId ?? string.Empty;
        RequestId = source.RequestId ?? string.Empty;
        _isRead = source.IsRead;

        // Start loading username in background
        _ = LoadFromUsernameDisplayAsync();
    }

    // Properties... (rest of the properties remain)
    public string NotificationId { get; }
    public UserNotification Source { get; }
    private string _title = string.Empty; // Make title settable for async update
    public string Title
    {
        get => _title;
        private set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }
    public string LinkText { get; }
    public string Status { get; }
    public Color StatusColor { get; }
    public string TimeAgo { get; }
    public string ImageUrl { get; }
    public string PostId { get; }
    public string RequestId { get; }

    private bool _isRead;
    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (_isRead != value)
            {
                _isRead = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CardBackgroundColor));
                OnPropertyChanged(nameof(IsUnread));
            }
        }
    }
    public bool IsUnread => !IsRead;

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (_isSelectionMode != value)
            {
                _isSelectionMode = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CardBackgroundColor));
                OnPropertyChanged(nameof(CardBorderColor));
            }
        }
    }

    public Color CardBackgroundColor
    {
        get
        {
            if (IsSelected) return Color.FromArgb("#E5E7EB"); // Darker Gray for Selected
            if (!IsRead) return Colors.White; // White for Unread
            return Color.FromArgb("#F3F4F6"); // Light Gray for Read
        }
    }

    public Color CardBorderColor => IsSelected ? Color.FromArgb("#5B2EFF") : Color.FromArgb("#E5E7EB");

    private string _fromUsernameDisplay = "User"; // Default value
    public string FromUsernameDisplay
    {
        get => _fromUsernameDisplay;
        private set { if (_fromUsernameDisplay != value) { _fromUsernameDisplay = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); } }
    }

    private async Task LoadFromUsernameDisplayAsync()
    {
        if (string.IsNullOrWhiteSpace(Source.FromUserId)) return;

        if (_databaseService != null)
        {
            try
            {
                var profile = await _databaseService.GetUserProfileAsync(Source.FromUserId);
                FromUsernameDisplay = profile?.Username ?? "User";
            }
            catch
            {
                FromUsernameDisplay = "User";
            }
        }
        else
        {
            FromUsernameDisplay = "User";
        }
        // Update the Title property after username is loaded
        Title = BuildTitle(Source, FromUsernameDisplay);
    }

    private string BuildTitle(UserNotification n, string fromUsername)
    {
        var item = string.IsNullOrWhiteSpace(n.ItemTitle) ? "your item" : n.ItemTitle;
        return n.Type switch
        {
            "request" => $"New request from {fromUsername} for \"{item}\"",
            "status_update" => $"Request {n.Status}: {item}",
            _ => item
        };
    }

    private string BuildLinkText(UserNotification n)
    {
        return n.Type switch
        {
            "request" => "View request details  >",
            _ => "View details  >"
        };
    }

    private string BuildStatus(UserNotification n)
    {
        if (string.IsNullOrWhiteSpace(n.Status))
            return "New";
        return n.Status;
    }

    private Color BuildStatusColor(UserNotification n)
    {
        var status = n.Status?.ToLowerInvariant();
        return status switch
        {
            "pending" => Color.FromArgb("#92400E"),
            "approved" => Color.FromArgb("#065F46"),
            "rejected" => Color.FromArgb("#991B1B"),
            _ => Color.FromArgb("#6B7280")
        };
    }

    private string BuildTimeAgo(DateTime createdAt)
    {
        if (createdAt == default || createdAt == DateTime.MinValue)
            return "Just now";

        var local = createdAt.ToLocalTime();
        var span = DateTime.Now - local;

        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalHours < 1) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
        if (span.TotalDays < 1) return $"{Math.Max(1, (int)span.TotalHours)}h ago";
        if (span.TotalDays < 7) return $"{Math.Max(1, (int)span.TotalDays)}d ago";
        return local.ToString("MMM dd");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
