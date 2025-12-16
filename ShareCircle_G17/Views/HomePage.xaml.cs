using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Services;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Views;

public partial class HomePage : ContentPage
{
    private readonly IFirebaseAuthService? _authService;
    private readonly IFirebaseDatabaseService? _databaseService;
    private readonly IGoogleMapsService? _googleMapsService;
    private readonly ISQLiteDatabaseService? _sqliteService;
    private readonly List<HomeMasonryItem> _allMasonry = new();
    private string _currentFilter = "All";
    private HashSet<string> _savedPostIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasLoadedCache;
    private string? _currentUserId;
    private CancellationTokenSource? _loadingCts;


    // BindableProperty so the UI updates when UserName changes
    public static readonly BindableProperty UserNameProperty =
        BindableProperty.Create(nameof(UserName), typeof(string), typeof(HomePage), defaultValue: "User");

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    // BindableProperty for the total donations
    public static readonly BindableProperty TotalDonationsProperty =
        BindableProperty.Create(nameof(TotalDonations), typeof(int), typeof(HomePage), defaultValue: 0);

    public int TotalDonations
    {
        get => (int)GetValue(TotalDonationsProperty);
        set => SetValue(TotalDonationsProperty, value);
    }

    // BindableProperty for current location display
    public static readonly BindableProperty LocationDisplayProperty =
        BindableProperty.Create(nameof(LocationDisplay), typeof(string), typeof(HomePage), defaultValue: "Locating...");

    public string LocationDisplay
    {
        get => (string)GetValue(LocationDisplayProperty);
        set => SetValue(LocationDisplayProperty, value);
    }

    private bool _isLocationRefreshing;
    public bool IsLocationRefreshing
    {
        get => _isLocationRefreshing;
        set
        {
            if (_isLocationRefreshing != value)
            {
                _isLocationRefreshing = value;
                OnPropertyChanged();
            }
        }
    }

    public static readonly BindableProperty HasUnreadNotificationsProperty =
        BindableProperty.Create(nameof(HasUnreadNotifications), typeof(bool), typeof(HomePage), defaultValue: false);

    public bool HasUnreadNotifications
    {
        get => (bool)GetValue(HasUnreadNotificationsProperty);
        set => SetValue(HasUnreadNotificationsProperty, value);
    }

    // BindableProperty to track if subcategories are expanded
    public static readonly BindableProperty HasExpandedSubcategoriesProperty =
        BindableProperty.Create(nameof(HasExpandedSubcategories), typeof(bool), typeof(HomePage), defaultValue: false);

    public bool HasExpandedSubcategories
    {
        get => (bool)GetValue(HasExpandedSubcategoriesProperty);
        set
        {
            SetValue(HasExpandedSubcategoriesProperty, value);
            OnPropertyChanged(nameof(FilterAlignment));
        }
    }

    // Computed property for filter alignment
    public LayoutOptions FilterAlignment => HasExpandedSubcategories ? LayoutOptions.Start : LayoutOptions.Center;

    public ICommand RefreshLocationCommand { get; }
    public ICommand RefreshCommand { get; }
    public ObservableCollection<HomeMasonryItem> MasonryItems { get; } = new();
    public ObservableCollection<UnifiedFilterItem> UnifiedFilterItems { get; } = new();
    private bool _isBusy;
    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                UpdateLoadingState();
            }
        }
    }
    public bool ShowLoading => IsBusy;

    // Constructor with dependency injection
    public HomePage(
        IFirebaseAuthService authService,
        IFirebaseDatabaseService databaseService,
        IGoogleMapsService googleMapsService,
        ISQLiteDatabaseService sqliteDatabaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;
        _googleMapsService = googleMapsService;
        _sqliteService = sqliteDatabaseService;
        RefreshLocationCommand = new Command(async () => await RefreshLocationAsync());
        RefreshCommand = new Command(async () => await RefreshAllAsync(true));
        BindingContext = this;
        InitializeUnifiedFilters();
        // SeedMasonryData(); // Removed - will load real data in OnAppearing
    }

    // Parameterless constructor for design time
    public HomePage()
    {
        InitializeComponent();
        _googleMapsService = new GoogleMapsService();
        RefreshLocationCommand = new Command(async () => await RefreshLocationAsync());
        RefreshCommand = new Command(async () => await RefreshAllAsync(true));
        BindingContext = this;
        InitializeUnifiedFilters();
        // SeedMasonryData(); // Removed - will load real data in OnAppearing
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // --- Added: Security Check ---
        await CheckUserStatusAsync();
        // -----------------------------

        // First time: Show loading animation then load data
        if (!_hasLoadedCache)
        {
            MasonryItems.Clear(); // Ensure empty state
            await RefreshAllAsync(true); // Triggers loading dots + network fetch
            _hasLoadedCache = true;
        }
        else
        {
            // Refresh posts and user data, but skip location refresh to save battery/resources
            await RefreshAllAsync(false);
        }

        // Check for notification permission on Android 13+
        await CheckAndRequestNotificationPermission();
    }

    private async Task CheckUserStatusAsync()
    {
        try
        {
            if (_authService == null || _databaseService == null) return;
            
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null && !string.IsNullOrEmpty(currentUser.UserId))
            {
                // Only check if we are online
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var profile = await _databaseService.GetUserProfileAsync(currentUser.UserId);
                    if (profile == null)
                    {
                        // User deleted! Force on Main Thread to avoid context issues
                        MainThread.BeginInvokeOnMainThread(async () => 
                        {
                            await _authService.SignOutAsync(); // Logout immediately
                            
                            // Clear secure storage as well
                            Microsoft.Maui.Storage.SecureStorage.Remove("saved_email");
                            Microsoft.Maui.Storage.SecureStorage.Remove("saved_password");
                            Microsoft.Maui.Storage.SecureStorage.Remove("remember_me");

                            await DisplayAlert("Session Expired", "Your account has been deactivated by an administrator.", "OK");
                            
                            // Force navigation to Login and clear stack
                            await Shell.Current.GoToAsync("//LoginPage");
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"User status check failed: {ex.Message}");
        }
    }

    private async Task CheckAndRequestNotificationPermission()
    {
        try
        {
            // Only required for Android 13+ (API 33+)
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 13)
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
        }
        catch (Exception ex)
        {
            // Just log, don't crash if permission check fails (e.g. on older devices or different platforms)
            System.Diagnostics.Debug.WriteLine($"Notification permission check failed: {ex.Message}");
        }
    }

    private async Task LoadUserDataAsync()
    {
        try
        {
            if (_authService == null || _databaseService == null)
            {
                System.Diagnostics.Debug.WriteLine("HomePage: Services not available");
                return;
            }

            // Get current user
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
            {
                System.Diagnostics.Debug.WriteLine("HomePage: No user logged in");
                UserName = "User";
                TotalDonations = 0;
                UpdateAvatarDisplay("User", null);
                return;
            }

            Models.User? userProfile = null;

            // Try online first if possible
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                userProfile = await _databaseService.GetUserProfileAsync(currentUser.UserId);
                // Cache user profile if found
                if (userProfile != null && _sqliteService != null)
                {
                    await _sqliteService.InitializeDatabaseAsync();
                    await _sqliteService.SaveUserAsync(userProfile);
                }
            }

            // Fallback to local SQLite if offline or online fetch failed
            if (userProfile == null && _sqliteService != null)
            {
                await _sqliteService.InitializeDatabaseAsync();
                userProfile = await _sqliteService.GetUserAsync(currentUser.UserId);
            }

            // Update username
            UserName = userProfile?.Username ?? currentUser.Username ?? "User";

            // Update avatar display
            UpdateAvatarDisplay(UserName, userProfile?.ProfileImageUrl);

            // Get user's total donations count (online preferred, offline fallback)
            var userDonations = (Connectivity.Current.NetworkAccess == NetworkAccess.Internet && _databaseService != null)
                ? await _databaseService.GetUserDonationPostsAsync(currentUser.UserId)
                : null;

            if (userDonations != null)
            {
                TotalDonations = userDonations.Count(d => string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            }
            else if (_sqliteService != null)
            {
                // Use local cached donations count if available
                var localDonations = await _sqliteService.GetAllDonationsAsync();
                TotalDonations = localDonations?.Count(d => d.UserId == currentUser.UserId && string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase)) ?? 0;
            }
            else
            {
                TotalDonations = 0;
            }

            System.Diagnostics.Debug.WriteLine($"HomePage: Loaded {TotalDonations} donations for user {UserName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HomePage: Error loading user data: {ex.Message}");
            UserName = "User";
            TotalDonations = 0;
            UpdateAvatarDisplay("User", null);
        }
    }

    /// <summary>
    /// Load data from SQLite cache instantly (no network, no loading indicator)
    /// </summary>
    private async Task LoadFromCacheAsync()
    {
        try
        {
            if (_sqliteService == null) return;

            await _sqliteService.InitializeDatabaseAsync();

            // Get current user ID
            if (_authService != null)
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                _currentUserId = currentUser?.UserId;

                // Load user profile from cache
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    var cachedUser = await _sqliteService.GetUserAsync(_currentUserId);
                    if (cachedUser != null)
                    {
                        UserName = cachedUser.Username ?? "User";
                        UpdateAvatarDisplay(UserName, cachedUser.ProfileImageUrl);
                    }

                    // Load saved post IDs from local cache
                    var cachedSavedIds = await _sqliteService.GetSavedPostIdsAsync(_currentUserId);
                    _savedPostIds = new HashSet<string>(cachedSavedIds, StringComparer.OrdinalIgnoreCase);
                }
            }

            // Load donations from cache
            var cachedDonations = await _sqliteService.GetAllDonationsAsync();
            if (cachedDonations != null && cachedDonations.Count > 0)
            {
                PopulateMasonryItems(cachedDonations);
                System.Diagnostics.Debug.WriteLine($"HomePage: Loaded {cachedDonations.Count} donations from cache");
            }

            // Load user's donation count from cache
            if (!string.IsNullOrEmpty(_currentUserId))
            {
                var localDonations = await _sqliteService.GetAllDonationsAsync();
                TotalDonations = localDonations?.Count(d => d.UserId == _currentUserId && string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase)) ?? 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadFromCacheAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sync data from Firebase in background and update cache
    /// </summary>
    private async Task SyncFromFirebaseAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            System.Diagnostics.Debug.WriteLine("HomePage: Offline, skipping Firebase sync");
            return;
        }

        if (_databaseService == null || _sqliteService == null) return;

        try
        {
            // Sync user data
            if (_authService != null && !string.IsNullOrEmpty(_currentUserId))
            {
                var userProfile = await _databaseService.GetUserProfileAsync(_currentUserId);
                if (userProfile != null)
                {
                    await _sqliteService.SaveUserAsync(userProfile);
                    UserName = userProfile.Username ?? "User";
                    UpdateAvatarDisplay(UserName, userProfile.ProfileImageUrl);
                }

                // Sync saved items
                var savedPosts = await _databaseService.GetSavedItemsAsync(_currentUserId);
                var savedIds = savedPosts.Where(p => !string.IsNullOrWhiteSpace(p.PostId)).Select(p => p.PostId!).ToList();
                await _sqliteService.SyncSavedPostIdsAsync(_currentUserId, savedIds);
                _savedPostIds = new HashSet<string>(savedIds, StringComparer.OrdinalIgnoreCase);

                // Update saved state for existing items
                foreach (var item in _allMasonry)
                {
                    if (item.SourceDonation != null && !string.IsNullOrEmpty(item.SourceDonation.PostId))
                    {
                        item.IsSaved = _savedPostIds.Contains(item.SourceDonation.PostId);
                    }
                }

                // Sync user's donation count
                var userDonations = await _databaseService.GetUserDonationPostsAsync(_currentUserId);
                if (userDonations != null)
                {
                    TotalDonations = userDonations.Count(d => string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase));
                }
            }

            // Sync all donations
            var donations = await _databaseService.GetAllDonationPostsAsync();
            if (donations != null)
            {
                // Cache to SQLite - Clear old data first to handle deletions
                await _sqliteService.ClearAllDonationsAsync();
                
                foreach (var donation in donations)
                {
                    await _sqliteService.SaveDonationAsync(donation);
                }

                // Update UI if data changed
                PopulateMasonryItems(donations);
                System.Diagnostics.Debug.WriteLine($"HomePage: Synced {donations.Count} donations from Firebase");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncFromFirebaseAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Populate masonry items from donation list
    /// </summary>
    private void PopulateMasonryItems(List<DonationPost> donations)
    {
        _allMasonry.Clear();

        foreach (var d in donations)
        {
            var title = string.IsNullOrWhiteSpace(d.Title) ? "Untitled" : d.Title;
            var desc = string.IsNullOrWhiteSpace(d.Description) ? "No description provided." : d.Description;
            var cat = string.IsNullOrWhiteSpace(d.Category) ? "Item" : ToTitle(d.Category);
            var image = GetPrimaryImageUrl(d.ImageUrl);
            var height = 170 + Math.Min(80, desc.Length / 4);
            var meta = BuildMeta(d);
            var timeAgo = BuildTimeAgo(d.CreatedAt);

            _allMasonry.Add(new HomeMasonryItem
            {
                Title = title,
                Description = desc,
                Category = cat,
                Subcategory = d.SubCategory ?? string.Empty,
                Image = image,
                CardHeight = height,
                Meta = meta,
                TimeAgo = timeAgo,
                DistanceText = "Nearby",
                Username = string.IsNullOrWhiteSpace(d.Username) ? "User" : d.Username,
                UserInitial = string.IsNullOrWhiteSpace(d.Username) ? "U" : d.Username[0].ToString().ToUpperInvariant(),
                SourceDonation = d,
                IsSaved = _savedPostIds.Contains(d.PostId ?? string.Empty)
            });
        }

        ApplyFilter(_currentFilter);
    }

    private async Task RefreshLocationAsync()
    {
        try
        {
            IsLocationRefreshing = true;

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                LocationDisplay = "Location permission needed";
                return;
            }

            // Try last known first, then a fresh request
            var location = await Geolocation.GetLastKnownLocationAsync()
                           ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (location == null)
            {
                LocationDisplay = "Unable to get location";
                return;
            }

            // Prefer Google Maps reverse geocoding with explicit language
            if (_googleMapsService != null)
            {
                var googleAddress = await _googleMapsService.GetAddressFromCoordinatesAsync(location.Latitude, location.Longitude, "en");
                if (!string.IsNullOrWhiteSpace(googleAddress))
                {
                    LocationDisplay = ExtractCity(googleAddress);
                    return;
                }
            }

            var placemark = (await Geocoding.GetPlacemarksAsync(location)).FirstOrDefault();
            LocationDisplay = placemark == null
                ? "Location unavailable"
                : BuildCityOnly(placemark);
        }
        catch (PermissionException)
        {
            LocationDisplay = "Location permission needed";
        }
        catch (FeatureNotSupportedException)
        {
            LocationDisplay = "Location not supported";
        }
        catch (Exception)
        {
            LocationDisplay = "Location failed";
        }
        finally
        {
            IsLocationRefreshing = false;
        }
    }

    private string BuildCityOnly(Placemark placemark)
    {
        var city = PreferAscii(placemark.Locality)
                   ?? PreferAscii(placemark.SubAdminArea)
                   ?? PreferAscii(placemark.SubLocality);

        if (!string.IsNullOrWhiteSpace(city))
            return city;

        return "Unknown location";
    }

    private string ExtractCity(string address)
    {
        var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim())
                           .Where(p => !string.IsNullOrWhiteSpace(p))
                           .ToArray();
        if (parts.Length == 0) return "Unknown location";
        return parts[0];
    }

    private string? PreferAscii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var ascii = new string(value.Where(c => c <= 127 && !char.IsControl(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(ascii) ? null : ascii;
    }

    private void UpdateAvatarDisplay(string username, string? profileImageUrl)
    {
        // If user has a profile image, show it
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            UserAvatarImage.Source = profileImageUrl;
            UserAvatarImage.IsVisible = true;
            UserInitialLabel.IsVisible = false;
        }
        else
        {
            // Show user initial
            var initial = string.IsNullOrEmpty(username) ? "U" : username.Substring(0, 1).ToUpper();
            UserInitialLabel.Text = initial;
            UserInitialLabel.IsVisible = true;
            UserAvatarImage.IsVisible = false;
        }
    }

    // Navigation bar handlers
    private async void OnHomeNavTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HomePage");
    }

    private async void OnPullRefreshing(object? sender, EventArgs e)
    {
        if (sender is RefreshView rv)
        {
            rv.IsRefreshing = false; // hide native spinner immediately
        }
        // User-initiated refresh: sync and refresh location
        await RefreshAllAsync(true);
    }

        private async void OnExploreNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ExploreMapPage));

    private async void OnDonateNavTapped(object? sender, EventArgs e)
    {
        // Navigate to the new DonationPage
        await Shell.Current.GoToAsync(nameof(DonationPage));
    }

    private async void OnNotificationNavTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//NotificationsSettingsPage");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Notifications", $"Unable to open notifications: {ex.Message}", "OK");
        }
    }

    private async void OnProfileNavTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("ProfilePage");
        }
        catch (Exception)
        {
            await DisplayAlert("Profile", "Profile page route not found.", "OK");
        }
    }

    private async void OnAvatarTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("ProfilePage");
        }
        catch (Exception)
        {
            await DisplayAlert("Profile", "Profile page route not found.", "OK");
        }
    }

    private async void OnMyDonationsTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(MyDonationsListPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to navigate: {ex.Message}", "OK");
        }
    }

    private void InitializeUnifiedFilters()
    {
        UnifiedFilterItems.Clear();

        // Initially only show main categories
        UnifiedFilterItems.Add(new UnifiedFilterItem
        {
            DisplayName = "All",
            FilterKey = "All",
            FilterType = "main",
            IsMainCategory = true,
            IsSelected = true
        });

        UnifiedFilterItems.Add(new UnifiedFilterItem
        {
            DisplayName = "Food",
            FilterKey = "Food",
            FilterType = "main",
            IsMainCategory = true
        });

        UnifiedFilterItems.Add(new UnifiedFilterItem
        {
            DisplayName = "Items",
            FilterKey = "Item",
            FilterType = "main",
            IsMainCategory = true
        });
    }

    private List<UnifiedFilterItem> GetFoodSubcategories()
    {
        return new List<UnifiedFilterItem>
        {
            new UnifiedFilterItem { DisplayName = "Fruits & Veg", FilterKey = "fruit", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/salad.png" },
            new UnifiedFilterItem { DisplayName = "Bakery", FilterKey = "bread", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/bread.png" },
            new UnifiedFilterItem { DisplayName = "Packaged", FilterKey = "packaged", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/ingredients.png" },
            new UnifiedFilterItem { DisplayName = "Cooked", FilterKey = "cooked", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/cooking-pot.png" },
            new UnifiedFilterItem { DisplayName = "Halal", FilterKey = "halal", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/mosque.png" },
            new UnifiedFilterItem { DisplayName = "Others", FilterKey = "others", ParentCategory = "Food", Icon = "https://img.icons8.com/color/48/more.png" }
        };
    }

    private List<UnifiedFilterItem> GetItemSubcategories()
    {
        return new List<UnifiedFilterItem>
        {
            new UnifiedFilterItem { DisplayName = "Clothes", FilterKey = "cloths", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/t-shirt.png" },
            new UnifiedFilterItem { DisplayName = "Books", FilterKey = "books", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/books.png" },
            new UnifiedFilterItem { DisplayName = "Furniture", FilterKey = "furniture", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/armchair.png" },
            new UnifiedFilterItem { DisplayName = "Electronics", FilterKey = "electronics", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/laptop.png" },
            new UnifiedFilterItem { DisplayName = "Toys & Household", FilterKey = "toys-household", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/teddy-bear.png" },
            new UnifiedFilterItem { DisplayName = "Others", FilterKey = "others", ParentCategory = "Item", Icon = "https://img.icons8.com/color/48/more.png" }
        };
    }

    private async void OnFilterItemLoaded(object sender, EventArgs e)
    {
        if (sender is Border border)
        {
            // Start with offset and transparent
            border.TranslationX = -20;
            border.Opacity = 0;

            // Animate in: fade + slide
            await Task.WhenAll(
                border.FadeTo(1, 180, Easing.CubicOut),
                border.TranslateTo(0, 0, 180, Easing.CubicOut)
            );
        }
    }

    private void OnUnifiedFilterTapped(object sender, TappedEventArgs e)
    {
        // TapGestureRecognizer sends itself as sender, so rely on the command parameter first
        var item = e?.Parameter as UnifiedFilterItem
                   ?? (sender as BindableObject)?.BindingContext as UnifiedFilterItem;
        if (item == null)
            return;

        // Reset all items selection
        foreach (var filterItem in UnifiedFilterItems)
        {
            filterItem.IsSelected = false;
        }

        // Mark selected
        item.IsSelected = true;

        if (item.IsMainCategory)
        {
            // Main category clicked
            if (item.FilterKey == "All")
            {
                // "All" - collapse all subcategories and reset expand states
                CollapseAllCategories();
                ApplyFilter("All");
            }
            else if (item.FilterKey == "Food")
            {
                // "Food" - toggle expand/collapse
                if (item.IsExpanded)
                {
                    // Already expanded - collapse it
                    item.IsExpanded = false;
                    RemoveAllSubcategories();
                }
                else
                {
                    // Collapsed - expand it and collapse others
                    CollapseAllCategories();
                    item.IsExpanded = true;
                    ExpandFoodSubcategories();
                }
                ApplyFilter("Food");
            }
            else if (item.FilterKey == "Item")
            {
                // "Items" - toggle expand/collapse
                if (item.IsExpanded)
                {
                    // Already expanded - collapse it
                    item.IsExpanded = false;
                    RemoveAllSubcategories();
                }
                else
                {
                    // Collapsed - expand it and collapse others
                    CollapseAllCategories();
                    item.IsExpanded = true;
                    ExpandItemSubcategories();
                }
                ApplyFilter("Item");
            }
        }
        else
        {
            // Subcategory clicked - filter by subcategory
            ApplyFilter(item.ParentCategory, item.FilterKey);
        }
    }

    private void CollapseAllCategories()
    {
        // Collapse all expandable categories
        foreach (var filterItem in UnifiedFilterItems)
        {
            if (filterItem.IsMainCategory)
            {
                filterItem.IsExpanded = false;
            }
        }
        RemoveAllSubcategories();
    }

    private void RemoveAllSubcategories()
    {
        // Remove all subcategory items (keep only main categories)
        var subsToRemove = UnifiedFilterItems.Where(f => !f.IsMainCategory).ToList();
        foreach (var sub in subsToRemove)
        {
            UnifiedFilterItems.Remove(sub);
        }
        // Mark that we no longer have expanded subcategories
        HasExpandedSubcategories = false;
    }

    private async void ExpandFoodSubcategories()
    {
        // Find the index of "Food" main category
        var foodIndex = UnifiedFilterItems.ToList().FindIndex(f => f.FilterKey == "Food");
        if (foodIndex >= 0)
        {
            // Insert food subcategories right after "Food" with animation delay
            var foodSubs = GetFoodSubcategories();
            int insertIndex = foodIndex + 1;
            foreach (var sub in foodSubs)
            {
                UnifiedFilterItems.Insert(insertIndex++, sub);
                await Task.Delay(40); // Small delay for staggered animation effect
            }
            // Mark that we have expanded subcategories
            HasExpandedSubcategories = true;
        }
    }

    private async void ExpandItemSubcategories()
    {
        // Find the index of "Items" main category
        var itemIndex = UnifiedFilterItems.ToList().FindIndex(f => f.FilterKey == "Item");
        if (itemIndex >= 0)
        {
            // Insert item subcategories right after "Items" with animation delay
            var itemSubs = GetItemSubcategories();
            int insertIndex = itemIndex + 1;
            foreach (var sub in itemSubs)
            {
                UnifiedFilterItems.Insert(insertIndex++, sub);
                await Task.Delay(40); // Small delay for staggered animation effect
            }
            // Mark that we have expanded subcategories
            HasExpandedSubcategories = true;
        }
    }

    private void ApplyFilter(string filter, string? subcategory = null)
    {
        _currentFilter = filter;
        MasonryItems.Clear();

        IEnumerable<HomeMasonryItem> query = _allMasonry;

        if (!string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => string.Equals(i.Category, filter, StringComparison.OrdinalIgnoreCase));

            // Apply subcategory filter if specified
            if (!string.IsNullOrEmpty(subcategory))
            {
                query = query.Where(i => !string.IsNullOrEmpty(i.Subcategory) &&
                                         i.Subcategory.Contains(subcategory, StringComparison.OrdinalIgnoreCase));
            }
        }

        foreach (var item in query)
        {
            MasonryItems.Add(item);
        }

    }

    private void SeedMasonryData()
    {
        _allMasonry.Clear();
        _allMasonry.Add(new HomeMasonryItem
        {
            Title = "Fresh Apples",
            Description = "Crisp red apples available for pickup.",
            Category = "Food",
            Image = "fruit.png",
            CardHeight = 200,
            TimeAgo = "1h ago",
            DistanceText = "Nearby"
        });
        _allMasonry.Add(new HomeMasonryItem
        {
            Title = "Children Books",
            Description = "Bundle of kid books in good condition.",
            Category = "Item",
            Image = "book_2.png",
            CardHeight = 230,
            TimeAgo = "2h ago",
            DistanceText = "2 km away"
        });
        _allMasonry.Add(new HomeMasonryItem
        {
            Title = "Bread & Pastries",
            Description = "Freshly baked pastries, pick up today.",
            Category = "Food",
            Image = "bread.png",
            CardHeight = 180,
            TimeAgo = "Just now",
            DistanceText = "Nearby"
        });
        _allMasonry.Add(new HomeMasonryItem
        {
            Title = "Toys Set",
            Description = "Mixed toys, gently used.",
            Category = "Item",
            Image = "toys.png",
            CardHeight = 210,
            TimeAgo = "3h ago",
            DistanceText = "5 km away"
        });
        _allMasonry.Add(new HomeMasonryItem
        {
            Title = "Packaged Meals",
            Description = "Ready-to-eat packaged meals.",
            Category = "Food",
            Image = "dine.png",
            CardHeight = 195,
            TimeAgo = "Yesterday",
            DistanceText = "8 km away"
        });

        ApplyFilter("All");
    }

    private async Task LoadMasonryAsync()
    {
        try
        {
            _allMasonry.Clear();

            List<DonationPost>? donations = null;
            bool fetchSuccess = false;
            _savedPostIds = await GetSavedPostIdsAsync();

                            // 1. Try Remote Fetch
                            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet && _databaseService != null)
                            {
                                donations = await _databaseService.GetAllDonationPostsAsync();
                                if (donations != null)
                                {
                                    fetchSuccess = true;
                                    // Update Cache
                                    if (_sqliteService != null)
                                    {
                                        await _sqliteService.InitializeDatabaseAsync();
                                        await _sqliteService.ClearAllDonationsAsync();
                                        foreach (var d in donations)
                                        {
                                            await _sqliteService.SaveDonationAsync(d);
                                        }
                                    }
                                }
                                else
                                {
                                    // Fetch failed (returned null) even though we are online
                                    // This explains why old data persists (fallback to cache)
                                    // Silent fail or notify user? notify if manual refresh.
                                    System.Diagnostics.Debug.WriteLine("HomePage: Remote fetch returned null");
                                }
                            }
            // 2. Fallback to Local Cache if Remote Failed/Offline
            if (!fetchSuccess && _sqliteService != null)
            {
                await _sqliteService.InitializeDatabaseAsync();
                donations = await _sqliteService.GetAllDonationsAsync();
            }

            // 3. Populate UI
            if (donations == null) donations = new List<DonationPost>();

            foreach (var d in donations)
            {
                var title = string.IsNullOrWhiteSpace(d.Title) ? "Untitled" : d.Title;
                var desc = string.IsNullOrWhiteSpace(d.Description) ? "No description provided." : d.Description;
                var cat = string.IsNullOrWhiteSpace(d.Category) ? "Item" : ToTitle(d.Category);
                var image = GetPrimaryImageUrl(d.ImageUrl);
                var height = 170 + Math.Min(80, desc.Length / 4);
                var meta = BuildMeta(d);
                var timeAgo = BuildTimeAgo(d.CreatedAt);

                _allMasonry.Add(new HomeMasonryItem
                {
                    Title = title,
                    Description = desc,
                    Category = cat,
                    Subcategory = d.SubCategory ?? string.Empty,
                    Image = image,
                    CardHeight = height,
                    Meta = meta,
                    TimeAgo = timeAgo,
                    DistanceText = "Nearby",
                    Username = string.IsNullOrWhiteSpace(d.Username) ? "User" : d.Username,
                    UserInitial = string.IsNullOrWhiteSpace(d.Username) ? "U" : d.Username[0].ToString().ToUpperInvariant(),
                    UserImageUrl = d.UserImageUrl ?? string.Empty,
                    SourceDonation = d,
                    IsSaved = _savedPostIds.Contains(d.PostId ?? string.Empty)
                });
            }

            ApplyFilter(_currentFilter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HomePage Masonry load error: {ex.Message}");
        }
    }

    private async void OnMasonrySaveTapped(object sender, EventArgs e)
    {
        if (sender is not Element el || el.BindingContext is not HomeMasonryItem item)
            return;

        // If source data is missing (design-time/template), just toggle locally
        if (item.SourceDonation == null || string.IsNullOrEmpty(item.SourceDonation.PostId))
        {
            item.IsSaved = !item.IsSaved;
            return;
        }

        var postId = item.SourceDonation.PostId;
        var userId = _currentUserId;

        // Get user ID if not cached
        if (string.IsNullOrEmpty(userId) && _authService != null)
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            userId = currentUser?.UserId;
            _currentUserId = userId;
        }

        if (string.IsNullOrEmpty(userId))
        {
            await DisplayAlert("Login required", "Please sign in before saving.", "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.SourceDonation.UserId) &&
            string.Equals(item.SourceDonation.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Notice", "You cannot save your own post.", "OK");
            return;
        }

        // Toggle immediately for instant UI feedback
        var newSavedState = !item.IsSaved;
        item.IsSaved = newSavedState;

        // Update local cache immediately
        if (newSavedState)
        {
            _savedPostIds.Add(postId);
            if (_sqliteService != null)
            {
                await _sqliteService.SavePostIdAsync(userId, postId);
            }
        }
        else
        {
            _savedPostIds.Remove(postId);
            if (_sqliteService != null)
            {
                await _sqliteService.RemoveSavedPostIdAsync(userId, postId);
            }
        }

        // Sync to Firebase in background (don't wait)
        if (_databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (newSavedState)
                    {
                        await _databaseService.SaveItemAsync(userId, postId);
                    }
                    else
                    {
                        await _databaseService.UnsaveItemAsync(userId, postId);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Firebase save sync error: {ex.Message}");
                }
            });
        }
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        // CommandParameter now passes PostId; fallback to binding context if missing
        var postId = e?.Parameter as string;
        var item = (sender as BindableObject)?.BindingContext as HomeMasonryItem;

        if (string.IsNullOrWhiteSpace(postId))
        {
            postId = item?.SourceDonation?.PostId;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(postId))
            {
                var navParams = new Dictionary<string, object>
                {
                    { "PostId", postId }
                };
                await Shell.Current.GoToAsync(nameof(ProductDetailsPage), navParams);
                return;
            }

            await DisplayAlert("Info", "No details available for this item.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            await DisplayAlert("Error", "Unable to open product details.", "OK");
        }
    }

    private string GetPrimaryImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "https://via.placeholder.com/600x400/EEF2FF/9CA3AF?text=No+Image";
        }

        var trimmed = raw.Trim();

        if (trimmed.Contains("|"))
        {
            var first = trimmed.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first!;
        }
        else if (trimmed.Contains(";") && !trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            var first = trimmed.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first!;
        }

            return trimmed;
    }

    private async Task RefreshAllAsync(bool refreshLocation)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // 1. Load critical content in parallel (User data, Posts, Notifications)
            var loadTasks = new List<Task>
            {
                LoadUserDataAsync(),
                LoadMasonryAsync(),
                CheckUnreadNotificationsAsync()
            };

            await Task.WhenAll(loadTasks);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshAllAsync data error: {ex.Message}");
        }
        finally
        {
            // 2. Hide loading indicator immediately so user sees content
            IsBusy = false;
        }

        // 3. Refresh location in background (doesn't block UI)
        if (refreshLocation)
        {
            try
            {
                await RefreshLocationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location refresh error: {ex.Message}");
            }
        }
    }

    private async Task CheckUnreadNotificationsAsync()
    {
        try
        {
            if (_databaseService == null) return;
            
            // Need current user ID
            if (string.IsNullOrEmpty(_currentUserId) && _authService != null)
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                _currentUserId = currentUser?.UserId;
            }

            if (string.IsNullOrEmpty(_currentUserId))
            {
                HasUnreadNotifications = false;
                return;
            }
            
            // Only check online for now
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var notifications = await _databaseService.GetNotificationsAsync(_currentUserId);
                HasUnreadNotifications = notifications != null && notifications.Any(n => !n.IsRead);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckUnreadNotificationsAsync error: {ex.Message}");
        }
    }

    private async Task<HashSet<string>> GetSavedPostIdsAsync()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool fetchSuccess = false;
        string? userId = _currentUserId;

        try
        {
            if (string.IsNullOrEmpty(userId) && _authService != null)
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                userId = currentUser?.UserId;
            }

            if (string.IsNullOrEmpty(userId)) return result;

            // 1. Try Network
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet && _databaseService != null)
            {
                var savedPosts = await _databaseService.GetSavedItemsAsync(userId);
                if (savedPosts != null)
                {
                    var ids = savedPosts
                        .Where(p => !string.IsNullOrWhiteSpace(p.PostId))
                        .Select(p => p.PostId!)
                        .ToList();

                    foreach (var id in ids) result.Add(id);
                    fetchSuccess = true;

                    // Update Cache
                    if (_sqliteService != null)
                    {
                        await _sqliteService.InitializeDatabaseAsync();
                        await _sqliteService.SyncSavedPostIdsAsync(userId, ids);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSavedPostIdsAsync network error: {ex.Message}");
        }

        // 2. Fallback to Cache
        if (!fetchSuccess && _sqliteService != null && !string.IsNullOrEmpty(userId))
        {
            try
            {
                await _sqliteService.InitializeDatabaseAsync();
                var cachedIds = await _sqliteService.GetSavedPostIdsAsync(userId);
                foreach (var id in cachedIds) result.Add(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSavedPostIdsAsync cache error: {ex.Message}");
            }
        }

        return result;
    }

    private void StartLoadingDots()
    {
        if (_loadingCts != null) return;
        if (!ShowLoading) return;
        _loadingCts = new CancellationTokenSource();
        _ = AnimateDotsAsync(_loadingCts.Token);
    }

    private void StopLoadingDots()
    {
        if (_loadingCts == null) return;
        _loadingCts.Cancel();
        _loadingCts = null;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Dot1.Scale = Dot2.Scale = Dot3.Scale = 1;
            Dot1.Opacity = Dot2.Opacity = Dot3.Opacity = 1;
        });
    }

    private async Task AnimateDotsAsync(CancellationToken token)
    {
        try
        {
            var dots = new[] { Dot1, Dot2, Dot3 };
            while (!token.IsCancellationRequested)
            {
                foreach (var dot in dots)
                {
                    await dot.ScaleTo(1.2, 140, Easing.SinInOut);
                    await dot.ScaleTo(1.0, 140, Easing.SinInOut);
                    await Task.Delay(90, token);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }



    private void UpdateLoadingState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        if (ShowLoading)
        {
            StartLoadingDots();
        }
        else
        {
            StopLoadingDots();
        }
    }

    private string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (value.Length == 1)
            return value.ToUpperInvariant();
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    private string BuildMeta(DonationPost post)
    {
        var date = post.CreatedAt == default || post.CreatedAt == DateTime.MinValue
            ? "Recently"
            : post.CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");
        var location = !string.IsNullOrWhiteSpace(post.Location)
            ? post.Location
            : (!string.IsNullOrWhiteSpace(post.DropOffLocation) ? post.DropOffLocation : "Unknown");
        return $"{location} · {date}";
    }

    private string BuildTimeAgo(DateTime createdAtUtc)
    {
        if (createdAtUtc == default || createdAtUtc == DateTime.MinValue)
        {
            return "Recently";
        }

        var local = createdAtUtc.ToLocalTime();
        var span = DateTime.Now - local;

        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
        if (span.TotalHours < 24) return $"{Math.Max(1, (int)span.TotalHours)}h ago";
        if (span.TotalDays < 7) return $"{Math.Max(1, (int)span.TotalDays)}d ago";

        return local.ToString("MMM dd");
    }

}

public class HomeMasonryItem : INotifyPropertyChanged
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Food";
    public string Subcategory { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public double CardHeight { get; set; } = 200;
    public bool HasImage => !string.IsNullOrWhiteSpace(Image);
    public string Meta { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string DistanceText { get; set; } = string.Empty;
    public string Username { get; set; } = "User";
    public string UserInitial { get; set; } = "U";
    public string UserImageUrl { get; set; } = string.Empty;
    public bool HasUserImage => !string.IsNullOrWhiteSpace(UserImageUrl);
    public bool ShowInitial => !HasUserImage;
    public DonationPost? SourceDonation { get; set; } // Reference to original donation
    private bool _isSaved;
    public bool IsSaved
    {
        get => _isSaved;
        set
        {
            if (_isSaved != value)
            {
                _isSaved = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeartIcon));
            }
        }
    }
    public string HeartIcon => IsSaved
        ? "https://img.icons8.com/fluency-systems-filled/30/fa314a/like.png"
        : "https://img.icons8.com/fluency-systems-regular/30/9CA3AF/like.png";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class UnifiedFilterItem : INotifyPropertyChanged
{
    public string DisplayName { get; set; } = string.Empty;
    public string FilterKey { get; set; } = string.Empty;
    public string FilterType { get; set; } = "sub"; // "main" or "sub"
    public string Icon { get; set; } = string.Empty;
    public string ParentCategory { get; set; } = string.Empty;
    public bool IsMainCategory { get; set; } = false;

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
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(FontAttributes));
            }
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandIcon));
                OnPropertyChanged(nameof(HasExpandIcon));
            }
        }
    }

    // Visual properties
    public bool HasIcon => !string.IsNullOrEmpty(Icon);
    public bool HasExpandIcon => IsMainCategory && FilterKey != "All"; // Show expand icon for Food and Items only
    public string ExpandIcon => IsExpanded ? "▶" : "◀"; // Right when expanded, left when collapsed
    public Color BackgroundColor => IsSelected ? Color.FromArgb("#5B2EFF") : Color.FromArgb("#F3F4F6");
    public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#111827");
    public FontAttributes FontAttributes => IsMainCategory ? Microsoft.Maui.Controls.FontAttributes.Bold : Microsoft.Maui.Controls.FontAttributes.None;
    public int FontSize => IsMainCategory ? 14 : 13;
    public Thickness Padding => IsMainCategory ? new Thickness(14, 8) : new Thickness(12, 8);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
