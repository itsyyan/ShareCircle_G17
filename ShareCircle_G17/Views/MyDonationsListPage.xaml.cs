using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views
{
    public partial class MyDonationsListPage : ContentPage, IQueryAttributable
    {
        private readonly IFirebaseAuthService? _authService;
        private readonly IFirebaseDatabaseService? _databaseService;
        private readonly ISQLiteDatabaseService? _sqliteService;
        private ObservableCollection<MyDonationItem> _donations;
        private List<MyDonationItem> _allDonations = new();
        private string _currentFilter = "Available"; // Default filter
        private int _availableCount;
        private int _completedCount;
        private CancellationTokenSource? _loadingCts;
        private bool _isLoading;

        public MyDonationsListPage(
            ISyncService syncService,
            IFirebaseAuthService authService,
            IFirebaseDatabaseService databaseService,
            ISQLiteDatabaseService sqliteDatabaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteDatabaseService;
            _donations = new ObservableCollection<MyDonationItem>();
            DonationsCollectionView.ItemsSource = _donations;
            BindingContext = this; // Ensure BindingContext is set
        }

        public MyDonationsListPage()
        {
            InitializeComponent();
            _donations = new ObservableCollection<MyDonationItem>();
            DonationsCollectionView.ItemsSource = _donations;
            BindingContext = this; // Ensure BindingContext is set
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Filter", out object filterValue) && filterValue is string filterString)
            {
                _currentFilter = filterString;
                // Since OnAppearing runs after this, the filter will be applied there.
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadMyDonationsAsync();
        }

        private async Task LoadMyDonationsAsync()
        {
            try
            {
                // Show loading animation
                ShowLoadingState();

                _donations.Clear();

                if (_authService == null)
                {
                    HideLoadingState();
                    return;
                }

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    HideLoadingState();
                    await DisplayAlert("Error", "You must be logged in to view your donations.", "OK");
                    return;
                }

                // First, load from SQLite cache
                var loadedFromCache = await LoadFromCacheAsync(currentUser.UserId);
                // The filter (_currentFilter) set by ApplyQueryAttributes will be used by PopulateDonationItems -> ApplyFilter

                if (loadedFromCache)
                {
                    // Show cached counts/cards immediately while syncing
                    HideLoadingState();
                }

                // Then sync from Firebase if online
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet && _databaseService != null)
                {
                    await SyncFromFirebaseAsync(currentUser.UserId);
                }

                HideLoadingState();
            }
            catch (Exception ex)
            {
                HideLoadingState();
                await DisplayAlert("Error", $"Failed to load donations: {ex.Message}", "OK");
            }
        }

        private async Task<bool> LoadFromCacheAsync(string userId)
        {
            if (_sqliteService == null) return false;
            var loaded = false;

            try
            {
                await _sqliteService.InitializeDatabaseAsync();
                var cachedDonations = await _sqliteService.GetAllDonationsAsync();

                if (cachedDonations == null || cachedDonations.Count == 0) return false;

                // Filter to only user's donations
                var userDonations = cachedDonations.Where(d =>
                    !string.IsNullOrEmpty(d.UserId) &&
                    d.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)).ToList();

                if (userDonations.Count == 0) return false;

                PopulateDonationItems(userDonations);

                // Fetch cached request counts immediately
                _ = UpdatePendingRequestCountsAsync(onlyCache: true);

                loaded = true;
                System.Diagnostics.Debug.WriteLine($"MyDonationsListPage: Loaded {userDonations.Count} donations from cache");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadFromCacheAsync error: {ex.Message}");
            }

            return loaded;
        }

        private async Task SyncFromFirebaseAsync(string userId)
        {
            if (_databaseService == null || _sqliteService == null) return;

            try
            {
                var userDonations = await _databaseService.GetUserDonationPostsAsync(userId);

                if (userDonations == null || userDonations.Count == 0) return;

                // Cache to SQLite
                foreach (var donation in userDonations)
                {
                    await _sqliteService.SaveDonationAsync(donation);
                }

                // Update UI
                PopulateDonationItems(userDonations);

                // Fetch pending requests for these donations (full sync)
                _ = UpdatePendingRequestCountsAsync(onlyCache: false);

                System.Diagnostics.Debug.WriteLine($"MyDonationsListPage: Synced {userDonations.Count} donations from Firebase");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncFromFirebaseAsync error: {ex.Message}");
            }
        }

        private async Task UpdatePendingRequestCountsAsync(bool onlyCache = false)
        {
            // Clone list to avoid modification issues during iteration
            var itemsToUpdate = _allDonations.ToList();

            foreach (var item in itemsToUpdate)
            {
                try
                {
                    // 1. Try Local Cache First
                    if (_sqliteService != null)
                    {
                        var cachedRequests = await _sqliteService.GetRequestsForPostAsync(item.PostId);
                        if (cachedRequests != null && cachedRequests.Count > 0)
                        {
                            var cachedPending = cachedRequests.Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)).ToList();
                            
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                item.PendingRequestCount = cachedPending.Count;
                                item.FirstPendingRequestId = cachedPending.FirstOrDefault()?.RequestId;
                            });
                        }
                    }

                    if (onlyCache) continue;

                    // 2. Fetch from Firebase
                    if (_databaseService != null)
                    {
                        var requests = await _databaseService.GetDonationRequestsForPostAsync(item.PostId);
                        var pending = requests.Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)).ToList();

                        // Update UI
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            item.PendingRequestCount = pending.Count;
                            item.FirstPendingRequestId = pending.FirstOrDefault()?.RequestId;
                        });

                        // 3. Update Cache
                        if (_sqliteService != null)
                        {
                            foreach (var req in requests)
                            {
                                await _sqliteService.SaveRequestAsync(req);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load requests for post {item.PostId}: {ex.Message}");
                }
            }
        }

        private void PopulateDonationItems(List<DonationPost> donations)
        {
            _allDonations.Clear();
            _availableCount = 0;
            _completedCount = 0;

            foreach (var donation in donations)
            {
                var status = donation.Status ?? "Available";
                if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    _completedCount++;
                else
                    _availableCount++;

                _allDonations.Add(new MyDonationItem
                {
                    PostId = donation.PostId ?? string.Empty,
                    Title = donation.Title ?? "Untitled",
                    Description = donation.Description ?? "No description",
                    Category = donation.Category ?? string.Empty,
                    SubCategory = donation.SubCategory ?? string.Empty,
                    Status = status,
                    CreatedAt = donation.CreatedAt,
                    ImageUrl = GetPrimaryImageUrl(donation.ImageUrl),
                    SourceDonation = donation
                });
            }

            ApplyFilter(_currentFilter);
        }

        private void ShowEmptyState()
        {
            _availableCount = 0;
            _completedCount = 0;
            UpdateFilterUI();
            EmptyStateLayout.IsVisible = true;
            DonationsCollectionView.IsVisible = false;
        }

        private void ApplyFilter(string filter)
        {
            _currentFilter = filter;
            _donations.Clear();

            var filtered = filter switch
            {
                "Completed" => _allDonations.Where(d => d.Status?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true),
                _ => _allDonations.Where(d => !string.Equals(d.Status, "completed", StringComparison.OrdinalIgnoreCase))
            };

            foreach (var item in filtered)
            {
                _donations.Add(item);
            }

            UpdateFilterUI();

            // Don't change visibility while loading - let ShowLoadingState/HideLoadingState handle it
            if (_isLoading) return;

            if (_donations.Count == 0)
            {
                EmptyStateLayout.IsVisible = true;
                DonationsCollectionView.IsVisible = false;
            }
            else
            {
                EmptyStateLayout.IsVisible = false;
                DonationsCollectionView.IsVisible = true;
            }
        }

        private void UpdateFilterUI()
        {
            // Reset filters to inactive style
            FilterAvailable.BackgroundColor = Color.FromArgb("#33FFFFFF");
            FilterAvailableLabel.TextColor = Colors.White;
            FilterAvailableLabel.FontAttributes = FontAttributes.None;

            FilterCompleted.BackgroundColor = Color.FromArgb("#33FFFFFF");
            FilterCompletedLabel.TextColor = Colors.White;
            FilterCompletedLabel.FontAttributes = FontAttributes.None;

            // Active filter style
            switch (_currentFilter)
            {
                case "Completed":
                    FilterCompleted.BackgroundColor = Colors.White;
                    FilterCompletedLabel.TextColor = Color.FromArgb("#5B2EFF");
                    FilterCompletedLabel.FontAttributes = FontAttributes.Bold;
                    break;
                default:
                    FilterAvailable.BackgroundColor = Colors.White;
                    FilterAvailableLabel.TextColor = Color.FromArgb("#5B2EFF");
                    FilterAvailableLabel.FontAttributes = FontAttributes.Bold;
                    break;
            }

            // Update label text with counts
            FilterAvailableLabel.Text = $"Available ({_availableCount})";
            FilterCompletedLabel.Text = $"Completed ({_completedCount})";
        }

        private void OnFilterAvailableTapped(object? sender, EventArgs e)
        {
            ApplyFilter("Available");
        }

        private void OnFilterCompletedTapped(object? sender, EventArgs e)
        {
            ApplyFilter("Completed");
        }

        private async void OnBackClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnCreateDonationClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(DonationPage));
        }

        private async void OnEditClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is MyDonationItem donation)
            {
                if (!string.IsNullOrWhiteSpace(donation.PostId))
                {
                    await Shell.Current.GoToAsync($"{nameof(DonationPage)}?PostId={donation.PostId}");
                }
                else
                {
                    await DisplayAlert("Error", "Unable to edit: missing post ID.", "OK");
                }
            }
        }

        private async void OnCompleteClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is MyDonationItem donation)
            {
                // Mark as complete - ask for confirmation
                var confirm = await DisplayAlert(
                    "Mark as Completed",
                    $"Are you sure you want to mark \"{donation.Title}\" as completed?\n\nThis action cannot be undone.",
                    "Complete",
                    "Cancel"
                );

                if (confirm)
                {
                    await UpdateDonationStatusAsync(donation, "Completed");
                }
            }
        }

        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is MyDonationItem donation)
            {
                await DeleteDonationAsync(donation);
            }
        }

        private async void OnCardTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is MyDonationItem donation)
            {
                if (donation.HasPendingRequests && !string.IsNullOrWhiteSpace(donation.FirstPendingRequestId))
                {
                    // If requests exist, jump to the request details (first pending)
                    var nav = new Dictionary<string, object>
                    {
                        { "RequestId", donation.FirstPendingRequestId }
                    };
                    await Shell.Current.GoToAsync(nameof(RequestDetailsPage), nav);
                }
                else
                {
                    // Otherwise, just view the product details (or edit)
                    await Shell.Current.GoToAsync($"{nameof(ProductDetailsPage)}?PostId={donation.PostId}");
                }
            }
        }

        private async Task DeleteDonationAsync(MyDonationItem donation)
        {
            try
            {
                bool confirm = await DisplayAlert(
                    "Delete Donation",
                    $"Are you sure you want to delete \"{donation.Title}\"?\n\nThis action cannot be undone.",
                    "Delete",
                    "Cancel"
                );

                if (!confirm || _databaseService == null)
                    return;

                var result = await _databaseService.DeleteDonationPostAsync(donation.PostId);

                if (result.Success)
                {
                    _donations.Remove(donation);
                    _allDonations.Remove(donation);
                    // Adjust counts and refresh
                    if (string.Equals(donation.Status, "completed", StringComparison.OrdinalIgnoreCase))
                        _completedCount = Math.Max(0, _completedCount - 1);
                    else
                        _availableCount = Math.Max(0, _availableCount - 1);

                    if (_sqliteService != null)
                    {
                        var cachedDonation = await _sqliteService.GetDonationAsync(donation.PostId);
                        if (cachedDonation != null)
                        {
                            await _sqliteService.DeleteDonationAsync(cachedDonation);
                        }
                    }

                    ApplyFilter(_currentFilter);

                    if (_donations.Count == 0)
                    {
                        ShowEmptyState();
                    }
                }
                else
                {
                    await DisplayAlert("Error", result.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
            }
        }

        private async Task UpdateDonationStatusAsync(MyDonationItem donation, string newStatus)
        {
            try
            {
                if (_databaseService == null)
                    return;

                var fullDonation = await _databaseService.GetDonationPostAsync(donation.PostId);
                if (fullDonation == null)
                {
                    await DisplayAlert("Error", "Donation not found.", "OK");
                    return;
                }

                fullDonation.Status = newStatus;
                fullDonation.UpdatedAt = DateTime.UtcNow;

                var result = await _databaseService.UpdateDonationPostAsync(donation.PostId, fullDonation);

                if (result.Success)
                {
                    // Update local item and counts
                    donation.Status = newStatus;
                    RecountStatusCounts();
                    ApplyFilter(_currentFilter);
                }
                else
                {
                    await DisplayAlert("Error", result.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update: {ex.Message}", "OK");
            }
        }

        private string GetPrimaryImageUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "https://via.placeholder.com/600x400/EEF2FF/9CA3AF?text=No+Image";

            var trimmed = raw.Trim();

            if (trimmed.Contains("|"))
            {
                var first = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                    return first;
            }
            else if (trimmed.Contains(";") && !trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var first = trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                    return first;
            }

            return trimmed;
        }

        private void RecountStatusCounts()
        {
            _availableCount = 0;
            _completedCount = 0;

            foreach (var d in _allDonations)
            {
                if (string.Equals(d.Status, "completed", StringComparison.OrdinalIgnoreCase))
                    _completedCount++;
                else
                    _availableCount++;
            }
        }

        #region Loading Animation

        private void ShowLoadingState()
        {
            _isLoading = true;
            LoadingDotsGrid.IsVisible = true;
            EmptyStateLayout.IsVisible = false;
            DonationsCollectionView.IsVisible = false;
            StartLoadingDots();
        }

        private void HideLoadingState()
        {
            _isLoading = false;
            StopLoadingDots();
            LoadingDotsGrid.IsVisible = false;

            // Now show appropriate content
            if (_donations.Count == 0)
            {
                EmptyStateLayout.IsVisible = true;
                DonationsCollectionView.IsVisible = false;
            }
            else
            {
                EmptyStateLayout.IsVisible = false;
                DonationsCollectionView.IsVisible = true;
            }
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
                        if (token.IsCancellationRequested) break;
                        await dot.ScaleTo(1.2, 140, Easing.SinInOut);
                        await dot.ScaleTo(1.0, 140, Easing.SinInOut);
                        await Task.Delay(90, token);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        #endregion
    }

    public class MyDonationItem : INotifyPropertyChanged
    {
        public string PostId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public DonationPost? SourceDonation { get; set; }

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusBackgroundColor));
                    OnPropertyChanged(nameof(CompleteButtonText));
                    OnPropertyChanged(nameof(CompleteButtonBackground));
                    OnPropertyChanged(nameof(CompleteButtonTextColor));
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(EditColumnWidth));
                    OnPropertyChanged(nameof(CompleteColumnWidth));
                }
            }
        }

        // Can edit only when not completed
        public bool CanEdit => Status?.ToLower() != "completed";

        // Column width for edit button (collapses when completed)
        public GridLength EditColumnWidth => CanEdit ? GridLength.Star : new GridLength(0);

        // Column width for complete button (collapses when completed)
        public GridLength CompleteColumnWidth => CanEdit ? GridLength.Star : new GridLength(0);

        // Status display text
        public string StatusDisplay => Status?.ToLower() switch
        {
            "available" => "Available",
            "reserved" => "Reserved",
            "completed" => "Completed",
            _ => Status ?? "Unknown"
        };

        // Category display
        public string CategoryDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(SubCategory))
                    return SubCategory;
                if (!string.IsNullOrEmpty(Category))
                    return Category;
                return "Item";
            }
        }

        // Time ago display
        public string TimeAgo
        {
            get
            {
                if (CreatedAt == default || CreatedAt == DateTime.MinValue)
                    return "Recently";

                var local = CreatedAt.ToLocalTime();
                var span = DateTime.Now - local;

                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
                if (span.TotalHours < 24) return $"{Math.Max(1, (int)span.TotalHours)}h ago";
                if (span.TotalDays < 7) return $"{Math.Max(1, (int)span.TotalDays)}d ago";

                return local.ToString("MMM dd");
            }
        }

        // Full date display
        public string DateDisplay
        {
            get
            {
                if (CreatedAt == default || CreatedAt == DateTime.MinValue)
                    return "Recently posted";
                return CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");
            }
        }

        // Status background color
        public Color StatusBackgroundColor => Status?.ToLower() switch
        {
            "available" => Color.FromArgb("#22C55E"),   // Green
            "reserved" => Color.FromArgb("#F59E0B"),    // Amber
            "completed" => Color.FromArgb("#3B82F6"),   // Blue
            _ => Color.FromArgb("#6B7280")              // Gray
        };

        // Complete button - changes based on status
        public string CompleteButtonText => Status?.ToLower() == "completed"
            ? "↩ Reopen"
            : "✓ Complete";

        public Color CompleteButtonBackground => Status?.ToLower() == "completed"
            ? Color.FromArgb("#FEF3C7")  // Light amber
            : Color.FromArgb("#DCFCE7"); // Light green

        public Color CompleteButtonTextColor => Status?.ToLower() == "completed"
            ? Color.FromArgb("#B45309")  // Dark amber
            : Color.FromArgb("#166534"); // Dark green

        // Pending requests logic
        private int _pendingRequestCount;
        public int PendingRequestCount
        {
            get => _pendingRequestCount;
            set
            {
                if (_pendingRequestCount != value)
                {
                    _pendingRequestCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasPendingRequests));
                    OnPropertyChanged(nameof(RequestsButtonText));
                }
            }
        }

        public bool HasPendingRequests => PendingRequestCount > 0;
        
        public string RequestsButtonText => $"Requests ({PendingRequestCount})";

        public string? FirstPendingRequestId { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
