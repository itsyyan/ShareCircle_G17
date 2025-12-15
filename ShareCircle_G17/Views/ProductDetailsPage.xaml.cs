using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views;

public partial class ProductDetailsPage : ContentPage, IQueryAttributable
{
    private readonly IGoogleMapsService? _googleMapsService;
    private readonly IFirebaseDatabaseService? _databaseService;
    private readonly IFirebaseAuthService? _authService;
    private readonly ISQLiteDatabaseService? _sqliteService;
    private DonationPost? _donationPost;
    private CancellationTokenSource? _loadingCts;
    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsContentVisible));
                OnPropertyChanged(nameof(ShowActionButtons));
                if (_isLoading)
                    StartLoadingDots();
                else
                    StopLoadingDots();
            }
        }
    }

    public bool IsContentVisible => !IsLoading;

    public string LocationDisplay { get; set; } = "Locating...";
    public ICommand RefreshLocationCommand { get; }

    public ObservableCollection<string> ImageSources { get; } = new();

    private bool _isSaved;
    private bool _showAddress;
    private bool _showActionButtons = true;

    public bool IsSaved
    {
        get => _isSaved;
        set
        {
            if (_isSaved != value)
            {
                _isSaved = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowAddress
    {
        get => _showAddress;
        set
        {
            if (_showAddress != value)
            {
                _showAddress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HideAddressMessage));
            }
        }
    }

    public bool ShowActionButtons
    {
        get => _showActionButtons && !IsLoading;
        set
        {
            if (_showActionButtons != value)
            {
                _showActionButtons = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HideAddressMessage => !ShowAddress;

    public ProductDetailsPage()
    {
        InitializeComponent();
        _googleMapsService = new GoogleMapsService();
        _databaseService = new FirebaseDatabaseService();
        _authService = new FirebaseAuthService();
        _sqliteService = Resolve<ISQLiteDatabaseService>();
        RefreshLocationCommand = new Command(async () => await RefreshLocationAsync());
        BindingContext = this;
    }

    public ProductDetailsPage(
        IGoogleMapsService googleMapsService,
        IFirebaseDatabaseService databaseService,
        IFirebaseAuthService authService,
        ISQLiteDatabaseService sqliteService)
    {
        InitializeComponent();
        _googleMapsService = googleMapsService;
        _databaseService = databaseService;
        _authService = authService;
        _sqliteService = sqliteService;
        RefreshLocationCommand = new Command(async () => await RefreshLocationAsync());
        BindingContext = this;
    }

    private T? Resolve<T>() where T : class
    {
        return Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(T)) as T;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshLocationAsync();
    }

    private async Task RefreshLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                LocationDisplay = "Location permission needed";
                OnPropertyChanged(nameof(LocationDisplay));
                return;
            }

            var location = await Geolocation.GetLastKnownLocationAsync()
                           ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (location == null)
            {
                LocationDisplay = "Unable to get location";
                OnPropertyChanged(nameof(LocationDisplay));
                return;
            }

            // Prefer Google Maps reverse geocoding with explicit language
            if (_googleMapsService != null)
            {
                var googleAddress = await _googleMapsService.GetAddressFromCoordinatesAsync(location.Latitude, location.Longitude, "en");
                if (!string.IsNullOrWhiteSpace(googleAddress))
                {
                    LocationDisplay = ExtractCity(googleAddress);
                    OnPropertyChanged(nameof(LocationDisplay));
                    return;
                }
            }

            var placemark = (await Geocoding.GetPlacemarksAsync(location)).FirstOrDefault();
            LocationDisplay = placemark == null ? "Unknown location" : BuildCityOnly(placemark);

            OnPropertyChanged(nameof(LocationDisplay));
        }
        catch (PermissionException)
        {
            LocationDisplay = "Location permission needed";
            OnPropertyChanged(nameof(LocationDisplay));
        }
        catch (FeatureNotSupportedException)
        {
            LocationDisplay = "Location not supported";
            OnPropertyChanged(nameof(LocationDisplay));
        }
        catch (Exception)
        {
            LocationDisplay = "Location failed";
            OnPropertyChanged(nameof(LocationDisplay));
        }
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;

        // Prefer PostId to ensure we fetch the latest data
        if (query.TryGetValue("PostId", out var idObj))
        {
            var postId = idObj as string ?? (idObj?.ToString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(postId))
            {
                IsLoading = true;
                try
                {
                    await LoadDonationByIdAsync(postId);
                }
                finally
                {
                    IsLoading = false;
                }
                return;
            }
        }

        // Fallback: if a Donation object was passed
        if (query.TryGetValue("Donation", out var donationObj) && donationObj is DonationPost dp)
        {
            _donationPost = dp;
            BindFromDonationPost(dp);
            return;
        }
    }

    private async Task LoadDonationByIdAsync(string postId)
    {
        if (_databaseService == null) return;
        var post = await _databaseService.GetDonationPostAsync(postId);
        if (post != null)
        {
            _donationPost = post;
            BindFromDonationPost(post);
            await EvaluateAddressVisibilityAsync(post);
        }
    }

    private void BindFromDonationPost(DonationPost donation)
    {
        TitleLabel.Text = string.IsNullOrWhiteSpace(donation.Title) ? "Untitled" : donation.Title;
        DescriptionLabel.Text = string.IsNullOrWhiteSpace(donation.Description) ? "No description provided." : donation.Description;
        UsernameLabel.Text = string.IsNullOrWhiteSpace(donation.Username) ? "Anonymous" : donation.Username;
        AddressLabel.Text = string.IsNullOrWhiteSpace(donation.DropOffLocation) ? "Pickup location not provided." : donation.DropOffLocation;
        MetaLabel.Text = BuildMetaText(donation.CreatedAt);

        // Load user avatar or show initial
        LoadUserAvatar(donation.UserImageUrl, donation.Username);

        LoadImagesFromString(donation.ImageUrl);
        _ = EvaluateAddressVisibilityAsync(donation);
        _ = CheckIsSavedAsync();
    }

    private void LoadUserAvatar(string? userImageUrl, string? username)
    {
        // Set fallback initial
        UserInitialLabel.Text = string.IsNullOrWhiteSpace(username) ? "?" : username[0].ToString().ToUpper();

        if (!string.IsNullOrWhiteSpace(userImageUrl))
        {
            try
            {
                UserAvatarImage.Source = userImageUrl;
                UserAvatarImage.IsVisible = true;
                UserInitialLabel.IsVisible = false;
            }
            catch
            {
                // If image loading fails, show initial
                UserAvatarImage.IsVisible = false;
                UserInitialLabel.IsVisible = true;
            }
        }
        else
        {
            UserAvatarImage.IsVisible = false;
            UserInitialLabel.IsVisible = true;
        }
    }

    private async Task CheckIsSavedAsync()
    {
        if (_donationPost == null || string.IsNullOrEmpty(_donationPost.PostId))
            return;

        try
        {
            var currentUser = _authService != null ? await _authService.GetCurrentUserAsync() : null;
            if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
            {
                IsSaved = false;
                return;
            }

            // 1. Check local cache first (faster)
            if (_sqliteService != null)
            {
                var savedIds = await _sqliteService.GetSavedPostIdsAsync(currentUser.UserId);
                if (savedIds.Contains(_donationPost.PostId, StringComparer.OrdinalIgnoreCase))
                {
                    IsSaved = true;
                    return; // Found locally, assume true
                }
            }

            // 2. Check remote if online and not found locally (optional, but good for sync)
            if (_databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var savedItems = await _databaseService.GetSavedItemsAsync(currentUser.UserId);
                IsSaved = savedItems.Any(i => i.PostId == _donationPost.PostId);
                
                // Update local cache if found remote but not local
                if (IsSaved && _sqliteService != null)
                {
                    await _sqliteService.SavePostIdAsync(currentUser.UserId, _donationPost.PostId);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking saved status: {ex.Message}");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var currentUser = _authService != null ? await _authService.GetCurrentUserAsync() : null;
        if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            await DisplayAlert("Login required", "Please sign in before saving.", "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_donationPost?.UserId) &&
            string.Equals(_donationPost.UserId, currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Notice", "You cannot save your own post.", "OK");
            return;
        }

        IsSaved = !IsSaved;

        // Update local cache immediately
        if (_sqliteService != null && _donationPost != null && !string.IsNullOrEmpty(_donationPost.PostId))
        {
            if (IsSaved)
            {
                await _sqliteService.SavePostIdAsync(currentUser.UserId, _donationPost.PostId);
            }
            else
            {
                await _sqliteService.RemoveSavedPostIdAsync(currentUser.UserId, _donationPost.PostId);
            }
        }

        // Sync to Firebase
        if (_databaseService != null && _donationPost != null && !string.IsNullOrEmpty(_donationPost.PostId))
        {
            var postId = _donationPost.PostId;
            var userId = currentUser.UserId;
            var isSavedState = IsSaved;

            // Run in background
            _ = Task.Run(async () =>
            {
                try
                {
                    if (isSavedState)
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
                    System.Diagnostics.Debug.WriteLine($"Error syncing save state: {ex.Message}");
                }
            });
        }

        await DisplayAlert("Save", IsSaved ? "Saved to favorites." : "Removed from favorites.", "OK");
    }

    private async void OnRequestClicked(object sender, EventArgs e)
    {
        var currentUser = _authService != null ? await _authService.GetCurrentUserAsync() : null;
        if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            await DisplayAlert("Login required", "Please sign in before sending a request.", "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_donationPost?.UserId) &&
            string.Equals(_donationPost.UserId, currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Notice", "You cannot request your own post.", "OK");
            return;
        }

        if (_donationPost == null || string.IsNullOrWhiteSpace(_donationPost.PostId))
        {
            await DisplayAlert("Error", "Unable to send request: missing post details.", "OK");
            return;
        }

        if (_databaseService == null)
        {
            await DisplayAlert("Error", "Service unavailable. Please try again later.", "OK");
            return;
        }

        // Prevent duplicate active requests from the same user for the same post
        try
        {
            var existing = await _databaseService.GetDonationRequestsForPostAsync(_donationPost.PostId);
            if (existing.Any(r => string.Equals(r.RequesterId, currentUser.UserId, StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(r.Status, "Rejected", StringComparison.OrdinalIgnoreCase)))
            {
                await DisplayAlert("Notice", "You have already sent a request for this post.", "OK");
                return;
            }
        }
        catch
        {
            // If duplicate check fails, continue to attempt request creation
        }

        var request = new DonationRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            PostId = _donationPost.PostId,
            RequesterId = currentUser.UserId,
            RequesterName = currentUser.Username ?? "User",
            RequesterEmail = currentUser.Email ?? string.Empty,
            DonorId = _donationPost.UserId,
            DonorName = _donationPost.Username,
            ItemTitle = _donationPost.Title,
            ItemDescription = _donationPost.Description,
            ItemImageUrl = _donationPost.ImageUrl,
            Category = _donationPost.Category,
            SubCategory = _donationPost.SubCategory,
            Message = "I'd like to request this item.",
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };

        var result = await _databaseService.CreateDonationRequestAsync(request);
        if (result.Success)
        {
            // Update UI immediately
            if (RequestButton != null)
            {
                RequestButton.Text = "Request Sent";
                RequestButton.IsEnabled = false;
                RequestButton.BackgroundColor = Colors.Gray;
            }
            await DisplayAlert("Request sent", "Your request has been sent to the donor.", "OK");
        }
        else
        {
            await DisplayAlert("Error", result.Message, "OK");
        }
    }

    private async void OnAddressTapped(object sender, EventArgs e)
    {
        var address = AddressLabel?.Text;
        if (string.IsNullOrWhiteSpace(address) || address.Equals("Pickup location not provided.", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Address", "No address available to open.", "OK");
            return;
        }

        try
        {
            var encoded = Uri.EscapeDataString(address);
            var uri = new Uri($"https://www.google.com/maps/search/?api=1&query={encoded}");
            await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to open maps: {ex.Message}", "OK");
        }
    }

    private void LoadImagesFromString(string? imageData)
    {
        const string placeholder = "https://via.placeholder.com/600x400/EEF2FF/9CA3AF?text=No+Image";
        ImageSources.Clear();

        if (string.IsNullOrWhiteSpace(imageData))
        {
            ImageSources.Add(placeholder);
            return;
        }

        imageData = imageData.Trim();

        IEnumerable<string> tokens;
        if (imageData.Contains("|"))
        {
            tokens = imageData.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        else if (imageData.Contains(";") && !imageData.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            tokens = imageData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        else
        {
            tokens = new[] { imageData };
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            var cleaned = token.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            if (seen.Add(cleaned))
            {
                ImageSources.Add(cleaned);
            }
        }

        if (ImageSources.Count == 0)
        {
            ImageSources.Add(placeholder);
        }

        UpdateCarouselLayout();
    }

    private string? _donorPhoneNumber;

    private async Task EvaluateAddressVisibilityAsync(DonationPost donation)
    {
        try
        {
            // Default show buttons
            ShowActionButtons = true;
            
            // Reset button state (default to active)
            if (RequestButton != null)
            {
                RequestButton.Text = "Request";
                RequestButton.IsEnabled = true;
                RequestButton.BackgroundColor = Color.FromArgb("#5B2EFF");
            }

            // Donor always sees the address, but not buttons (optional)
            if (_authService == null)
            {
                ShowAddress = false;
                return;
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser?.UserId == null)
            {
                ShowAddress = false;
                return;
            }

            // Owner Logic - show own contact info
            if (!string.IsNullOrWhiteSpace(donation.UserId) &&
                string.Equals(donation.UserId, currentUser.UserId, StringComparison.OrdinalIgnoreCase))
            {
                ShowAddress = true;
                ShowActionButtons = false; // Hide buttons for owner
                await LoadDonorContactInfoAsync(donation.UserId, donation.Username);
                return;
            }

            // For requesters: check request status
            if (_databaseService == null || string.IsNullOrWhiteSpace(donation.PostId))
            {
                ShowAddress = false;
                return;
            }

            var requests = await _databaseService.GetDonationRequestsForPostAsync(donation.PostId);
            
            // Check for Approved/Completed access
            var hasAccess = requests.Any(r =>
                string.Equals(r.RequesterId, currentUser.UserId, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(r.Status, "approved", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(r.Status, "completed", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(r.Status, "accepted", StringComparison.OrdinalIgnoreCase)));

            ShowAddress = hasAccess;

            // If request is approved/completed, hide buttons and load donor contact info
            if (hasAccess)
            {
                ShowActionButtons = false;
                await LoadDonorContactInfoAsync(donation.UserId, donation.Username);
            }
            else
            {
                // Check for Pending Request
                var pendingRequest = requests.FirstOrDefault(r => 
                    string.Equals(r.RequesterId, currentUser.UserId, StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)));

                if (pendingRequest != null)
                {
                    // Pending: Keep buttons visible but disable Request button
                    if (RequestButton != null)
                    {
                        RequestButton.Text = "Request Sent";
                        RequestButton.IsEnabled = false;
                        RequestButton.BackgroundColor = Colors.Gray;
                    }
                }
            }
        }
        catch
        {
            ShowAddress = false;
        }
    }

    private async Task LoadDonorContactInfoAsync(string? donorUserId, string? donorUsername)
    {
        try
        {
            // Set default values
            DonorNameLabel.Text = string.IsNullOrWhiteSpace(donorUsername) ? "Donor" : donorUsername;
            DonorPhoneLabel.Text = "Not provided";
            _donorPhoneNumber = null;

            if (string.IsNullOrWhiteSpace(donorUserId) || _databaseService == null)
                return;

            // Try to get donor's default address for phone number
            var addresses = await _databaseService.GetAddressesAsync(donorUserId);
            var defaultAddress = addresses?.FirstOrDefault(a => a.IsDefault) ?? addresses?.FirstOrDefault();

            if (defaultAddress != null)
            {
                if (!string.IsNullOrWhiteSpace(defaultAddress.FullName))
                {
                    DonorNameLabel.Text = defaultAddress.FullName;
                }

                if (!string.IsNullOrWhiteSpace(defaultAddress.PhoneNumber))
                {
                    DonorPhoneLabel.Text = defaultAddress.PhoneNumber;
                    _donorPhoneNumber = defaultAddress.PhoneNumber;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading donor contact info: {ex.Message}");
        }
    }

    private async void OnWhatsAppClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_donorPhoneNumber))
        {
            await DisplayAlert("WhatsApp", "No phone number available.", "OK");
            return;
        }

        var waNumber = BuildWhatsAppNumber(_donorPhoneNumber);
        if (string.IsNullOrWhiteSpace(waNumber))
        {
            await DisplayAlert("WhatsApp", "Phone number is not valid for WhatsApp.", "OK");
            return;
        }

        var uri = new Uri($"https://wa.me/{waNumber}");

        try
        {
            if (await Launcher.CanOpenAsync(uri))
            {
                await Launcher.OpenAsync(uri);
            }
            else
            {
                await DisplayAlert("WhatsApp", "Cannot open WhatsApp on this device.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("WhatsApp", $"Unable to open WhatsApp: {ex.Message}", "OK");
        }
    }


    private string BuildCityOnly(Placemark placemark)
    {
        var city = PreferAscii(placemark.Locality)
                   ?? PreferAscii(placemark.SubAdminArea)
                   ?? PreferAscii(placemark.SubLocality);
        if (!string.IsNullOrWhiteSpace(city))
            return city!;
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

    private void UpdateCarouselLayout()
    {
        var count = ImageSources?.Count ?? 0;

        if (ImageCarousel?.ItemsLayout is LinearItemsLayout linear)
        {
            linear.ItemSpacing = count > 1 ? 12 : 0;
        }

        if (ImageCarousel != null)
        {
            ImageCarousel.PeekAreaInsets = count > 1 ? 30 : 0;
            ImageCarousel.IsSwipeEnabled = count > 1;
        }

        if (ImageIndicators != null)
        {
            ImageIndicators.IsVisible = count > 1;
        }
    }

    private string BuildMetaText(DateTime createdAtUtc)
    {
        if (createdAtUtc == default || createdAtUtc == DateTime.MinValue)
        {
            return "Posted recently";
        }

        var local = createdAtUtc.ToLocalTime();
        return $"Posted {local:MMM dd, yyyy}";
    }

    private string BuildWhatsAppNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // WhatsApp expects country code with digits only (no + or separators)
        var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
        return digitsOnly;
    }

    private void StartLoadingDots()
    {
        if (_loadingCts != null) return;
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
            if (Dot1 != null) Dot1.Scale = Dot1.Opacity = 1;
            if (Dot2 != null) Dot2.Scale = Dot2.Opacity = 1;
            if (Dot3 != null) Dot3.Scale = Dot3.Opacity = 1;
        });
    }

    private async Task AnimateDotsAsync(CancellationToken token)
    {
        try
        {
            // Wait for UI to be ready
            while (Dot1 == null || Dot2 == null || Dot3 == null)
            {
                if (token.IsCancellationRequested) return;
                await Task.Delay(50, token);
            }

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
        }
    }
}
