using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

using ShareCircle_G17.Controls;

namespace ShareCircle_G17.Views;

public partial class ExploreMapPage : ContentPage
{
    private readonly IFirebaseDatabaseService _databaseService;
    private readonly IFirebaseAuthService _authService;
    private DonationPost? _selectedPost;
    private Location? _myLocation;
    private string? _currentCity;

    // All posts in current city (for refresh rotation)
    private List<DonationPost> _allCityPosts = new();
    // Currently displayed posts (max 15)
    private List<DonationPost> _displayedPosts = new();
    // Dictionary to group posts by location
    private Dictionary<string, List<DonationPost>> _postsByLocation = new();

    // Settings
    private const int MaxDisplayPosts = 15;
    private const double CityRadiusKm = 15; // Posts within 15km considered same city

    private readonly Random _random = new();
    private bool _isFirstLoad = true;

    public static readonly BindableProperty HasUnreadNotificationsProperty =
        BindableProperty.Create(nameof(HasUnreadNotifications), typeof(bool), typeof(ExploreMapPage), defaultValue: false);

    public bool HasUnreadNotifications
    {
        get => (bool)GetValue(HasUnreadNotificationsProperty);
        set => SetValue(HasUnreadNotificationsProperty, value);
    }

    public ExploreMapPage(
        IFirebaseDatabaseService databaseService,
        IFirebaseAuthService authService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _authService = authService;
        BindingContext = this;
    }

    public ExploreMapPage()
    {
        InitializeComponent();
        _databaseService = new FirebaseDatabaseService();
        _authService = new FirebaseAuthService();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isFirstLoad)
        {
            await LoadMapDataAsync(isRefresh: false);
            _isFirstLoad = false;
        }
        await CheckUnreadNotificationsAsync();
    }

    private async Task CheckUnreadNotificationsAsync()
    {
        try
        {
            if (_authService == null || _databaseService == null) return;

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null && !string.IsNullOrEmpty(currentUser.UserId))
            {
                // Only check online for now
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var notifications = await _databaseService.GetNotificationsAsync(currentUser.UserId);
                    HasUnreadNotifications = notifications != null && notifications.Any(n => !n.IsRead);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckUnreadNotificationsAsync error: {ex.Message}");
        }
    }

    private async void OnRefreshTapped(object sender, EventArgs e)
    {
        // Animate refresh button
        await RefreshButton.RotateTo(360, 500, Easing.CubicOut);
        RefreshButton.Rotation = 0;

        await LoadMapDataAsync(isRefresh: true);
    }

    private async Task LoadMapDataAsync(bool isRefresh)
    {
        try
        {
            LoadingIndicator.IsVisible = true;

            // 1. Get User Location
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                _myLocation = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(5)
                });

                if (_myLocation != null)
                {
                    // Get current city name
                    await UpdateCurrentCityAsync();

                    var mapSpan = MapSpan.FromCenterAndRadius(_myLocation, Distance.FromKilometers(8));
                    ExploreMap.MoveToRegion(mapSpan);
                }
            }

            if (_myLocation == null)
            {
                CurrentCityLabel.Text = "Location unavailable";
                PostCountLabel.Text = "";
                return;
            }

            // 2. Load all posts
            var allPosts = await _databaseService.GetAllDonationPostsAsync();
            var availablePosts = allPosts
                .Where(p => p.Status == "Available" && (p.Latitude != 0 || p.Longitude != 0))
                .ToList();

            // 3. Filter to current city (within radius)
            _allCityPosts = availablePosts
                .Where(p => IsWithinCity(p.Latitude, p.Longitude))
                .OrderBy(p => Location.CalculateDistance(_myLocation, new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers))
                .ToList();

            // 4. Select posts to display (max 15)
            if (isRefresh && _allCityPosts.Count > MaxDisplayPosts)
            {
                // Shuffle and take 15 different posts
                _displayedPosts = _allCityPosts
                    .OrderBy(_ => _random.Next())
                    .Take(MaxDisplayPosts)
                    .ToList();
            }
            else
            {
                // First load or not enough posts - take closest 15
                _displayedPosts = _allCityPosts.Take(MaxDisplayPosts).ToList();
            }

            // Update UI
            var totalInCity = _allCityPosts.Count;
            var showing = _displayedPosts.Count;
            PostCountLabel.Text = totalInCity > showing
                ? $"· {showing}/{totalInCity}"
                : $"· {showing}";

            // 5. Group displayed posts by location
            _postsByLocation.Clear();
            foreach (var post in _displayedPosts)
            {
                var locationKey = GetLocationKey(post.Latitude, post.Longitude);
                if (!_postsByLocation.ContainsKey(locationKey))
                {
                    _postsByLocation[locationKey] = new List<DonationPost>();
                }
                _postsByLocation[locationKey].Add(post);
            }

            // 6. Create pins
            ExploreMap.Pins.Clear();

            foreach (var kvp in _postsByLocation)
            {
                var postsAtLocation = kvp.Value;
                var firstPost = postsAtLocation[0];
                var postCount = postsAtLocation.Count;

                var pin = new CustomPin
                {
                    Label = postCount > 1 ? $"{postCount} items here" : firstPost.Title,
                    Address = firstPost.Location ?? firstPost.DropOffLocation,
                    Type = PinType.Place,
                    Location = new Location(firstPost.Latitude, firstPost.Longitude),
                    ImageUrl = GetPrimaryImageUrl(firstPost.ImageUrl),
                    PostId = firstPost.PostId,
                    Count = postCount
                };

                var locationKey = kvp.Key;
                pin.MarkerClicked += (s, e) =>
                {
                    OnPinClicked(locationKey);
                    e.HideInfoWindow = true;
                };

                ExploreMap.Pins.Add(pin);
            }

            // Show message if no posts nearby
            if (_displayedPosts.Count == 0)
            {
                PostCountLabel.Text = "· No items nearby";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading map: {ex.Message}");
            CurrentCityLabel.Text = "Error loading";
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    private bool IsWithinCity(double lat, double lng)
    {
        if (_myLocation == null) return false;
        var distance = Location.CalculateDistance(_myLocation, new Location(lat, lng), DistanceUnits.Kilometers);
        return distance <= CityRadiusKm;
    }

    private async Task UpdateCurrentCityAsync()
    {
        if (_myLocation == null)
        {
            CurrentCityLabel.Text = "Unknown location";
            return;
        }

        try
        {
            var placemarks = await Geocoding.GetPlacemarksAsync(_myLocation);
            var placemark = placemarks?.FirstOrDefault();

            if (placemark != null)
            {
                // Prefer Locality (city), then SubAdminArea (district), then AdminArea (state)
                _currentCity = placemark.Locality
                    ?? placemark.SubAdminArea
                    ?? placemark.AdminArea
                    ?? "Unknown";
                CurrentCityLabel.Text = _currentCity;
            }
            else
            {
                CurrentCityLabel.Text = "Unknown location";
            }
        }
        catch
        {
            CurrentCityLabel.Text = "Unknown location";
        }
    }

    private string GetLocationKey(double lat, double lng)
    {
        // Round to 4 decimal places (~11 meter precision)
        return $"{Math.Round(lat, 4)},{Math.Round(lng, 4)}";
    }

    private void OnPinClicked(string locationKey)
    {
        if (!_postsByLocation.TryGetValue(locationKey, out var postsAtLocation) || postsAtLocation.Count == 0)
            return;

        if (postsAtLocation.Count == 1)
        {
            // Single post - show preview directly
            _selectedPost = postsAtLocation[0];
            ShowPostPreview(postsAtLocation[0]);
        }
        else
        {
            // Multiple posts - show selection dialog
            ShowMultiPostSelection(postsAtLocation);
        }
    }

    private void ShowMultiPostSelection(List<DonationPost> posts)
    {
        MultiPostCountLabel.Text = $"{posts.Count} items at this location";
        MultiPostList.ItemsSource = posts;
        MultiPostOverlay.IsVisible = true;
        MultiPostOverlay.Opacity = 0;
        MultiPostOverlay.FadeTo(1, 200);
    }

    private async void OnMultiPostItemTapped(object sender, EventArgs e)
    {
        if (sender is VisualElement element && element.BindingContext is DonationPost post)
        {
            await MultiPostOverlay.FadeTo(0, 150);
            MultiPostOverlay.IsVisible = false;

            _selectedPost = post;
            ShowPostPreview(post);
        }
    }

    private async void OnCloseMultiPostClicked(object sender, EventArgs e)
    {
        await MultiPostOverlay.FadeTo(0, 150);
        MultiPostOverlay.IsVisible = false;
    }

    private async void OnMultiPostOverlayTapped(object sender, EventArgs e)
    {
        await MultiPostOverlay.FadeTo(0, 150);
        MultiPostOverlay.IsVisible = false;
    }

    private void ShowPostPreview(DonationPost post)
    {
        PostTitle.Text = post.Title;
        PostDescription.Text = post.Description;
        PostImage.Source = GetPrimaryImageUrl(post.ImageUrl);

        if (_myLocation != null)
        {
            var postLoc = new Location(post.Latitude, post.Longitude);
            var dist = Location.CalculateDistance(_myLocation, postLoc, DistanceUnits.Kilometers);
            PostDistance.Text = $"{dist:F1} km away";
        }
        else
        {
            PostDistance.Text = "Nearby";
        }

        PostPreviewCard.IsVisible = true;
        PostPreviewCard.TranslateTo(0, 0, 250, Easing.SinOut);
    }

    private string GetPrimaryImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "https://via.placeholder.com/150?text=No+Image";
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

    private async void OnClosePreviewClicked(object sender, EventArgs e)
    {
        await PostPreviewCard.TranslateTo(0, 200, 250, Easing.SinIn);
        PostPreviewCard.IsVisible = false;
        _selectedPost = null;
    }

    private async void OnPreviewCardTapped(object sender, EventArgs e)
    {
        if (_selectedPost != null)
        {
            if (!string.IsNullOrWhiteSpace(_selectedPost.PostId))
            {
                var navParams = new Dictionary<string, object>
                {
                    { "PostId", _selectedPost.PostId }
                };
                await Shell.Current.GoToAsync(nameof(ProductDetailsPage), navParams);
            }
            else
            {
                await DisplayAlert("Info", "No details available for this item.", "OK");
            }
        }
    }

    // Bottom nav handlers
    private async void OnHomeNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//HomePage");
    private async void OnExploreNavTapped(object? sender, EventArgs e) { } // Already here
    private async void OnDonateNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DonationPage));
    private async void OnNotificationNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//NotificationsSettingsPage");
    private async void OnProfileNavTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//ProfilePage");
}
