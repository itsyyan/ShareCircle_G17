using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using ShareCircle_G17.Services;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Views
{
    public partial class MyRequestsPage : ContentPage
    {
        private readonly IFirebaseAuthService? _authService;
        private readonly IFirebaseDatabaseService? _databaseService;
        private readonly ISQLiteDatabaseService? _sqliteService;
        private ObservableCollection<RequestItemViewModel> _requests;
        private List<RequestItemViewModel> _allRequests = new();
        private string _currentFilter = "Pending";
        private int _pendingCount;
        private int _completedCount;

        // Constructor with dependency injection
        public MyRequestsPage(
            IFirebaseAuthService authService, 
            IFirebaseDatabaseService databaseService,
            ISQLiteDatabaseService sqliteService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteService;
            _requests = new ObservableCollection<RequestItemViewModel>();
            RequestsCollectionView.ItemsSource = _requests;
        }

        // Parameterless constructor for design time
        public MyRequestsPage()
        {
            InitializeComponent();
            _requests = new ObservableCollection<RequestItemViewModel>();
            RequestsCollectionView.ItemsSource = _requests;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadMyRequestsAsync();
        }

        private async Task LoadMyRequestsAsync()
        {
            try
            {
                if (_authService == null)
                {
                    ShowEmptyState();
                    return;
                }

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    await DisplayAlert("Error", "You must be logged in to view your requests.", "OK");
                    ShowEmptyState();
                    return;
                }

                // 1. Load from SQLite Cache
                if (_sqliteService != null)
                {
                    var allAssociatedRequests = await _sqliteService.GetRequestsByUserAsync(currentUser.UserId);
                    if (allAssociatedRequests != null && allAssociatedRequests.Count > 0)
                    {
                        // Filter to show only requests MADE by the user (not received)
                        var myOutgoingRequests = allAssociatedRequests
                            .Where(r => r.RequesterId == currentUser.UserId)
                            .ToList();

                        if (myOutgoingRequests.Count > 0)
                        {
                            await PopulateRequests(myOutgoingRequests);
                        }
                    }
                }

                // 2. Sync from Firebase (if online)
                if (_databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var userRequests = await _databaseService.GetUserRequestsAsync(currentUser.UserId);

                    System.Diagnostics.Debug.WriteLine($"MyRequests - Found {userRequests?.Count ?? 0} requests from Firebase");

                    if (userRequests != null && userRequests.Count > 0)
                    {
                        await PopulateRequests(userRequests);

                        // Cache to SQLite
                        if (_sqliteService != null)
                        {
                            foreach (var req in userRequests)
                            {
                                await _sqliteService.SaveRequestAsync(req);
                            }
                        }
                    }
                    else if (_allRequests.Count == 0)
                    {
                        ShowEmptyState();
                    }
                }
                else if (_allRequests.Count == 0)
                {
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMyRequestsAsync - Error: {ex.Message}");
                // Only show alert if list is empty
                if (_requests.Count == 0)
                {
                    await DisplayAlert("Error", $"Failed to load requests: {ex.Message}", "OK");
                    ShowEmptyState();
                }
            }
        }

        private async Task PopulateRequests(List<DonationRequest> newRequests)
        {
            // Update _allRequests logic (similar to smart merge but for the master list)
            var newIds = new HashSet<string>(newRequests.Select(r => r.RequestId));
            var toRemove = _allRequests.Where(vm => !newIds.Contains(vm.RequestId)).ToList();
            foreach (var item in toRemove) _allRequests.Remove(item);

            var pendingRequestsToCheck = new List<RequestItemViewModel>();

            foreach (var req in newRequests)
            {
                var existing = _allRequests.FirstOrDefault(vm => vm.RequestId == req.RequestId);
                
                string donorImage = "https://img.icons8.com/color/48/user-male-circle--v1.png";
                if (!string.IsNullOrEmpty(req.DonorId) && _databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    try
                    {
                        var donorProfile = await _databaseService.GetUserProfileAsync(req.DonorId);
                        if (donorProfile != null && !string.IsNullOrEmpty(donorProfile.ProfileImageUrl))
                        {
                            donorImage = donorProfile.ProfileImageUrl;
                        }
                    }
                    catch { /* Ignore */ }
                }

                if (existing != null)
                {
                    // Update properties
                    if (existing.Status != req.Status) existing.Status = req.Status ?? "Pending";
                    if (existing.ItemTitle != req.ItemTitle) existing.ItemTitle = req.ItemTitle ?? "Untitled";
                    if (existing.ItemImageUrl != req.ItemImageUrl) existing.ItemImageUrl = req.ItemImageUrl ?? string.Empty;
                    if (existing.DonorName != req.DonorName) existing.DonorName = req.DonorName ?? "Unknown";
                    if (existing.DonorImageUrl != donorImage) existing.DonorImageUrl = donorImage;
                    
                    if (string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        pendingRequestsToCheck.Add(existing);
                    }
                }
                else
                {
                    var newItem = new RequestItemViewModel
                    {
                        RequestId = req.RequestId ?? string.Empty,
                        PostId = req.PostId ?? string.Empty,
                        ItemTitle = req.ItemTitle ?? "Untitled",
                        ItemDescription = req.ItemDescription ?? string.Empty,
                        ItemImageUrl = req.ItemImageUrl ?? string.Empty,
                        DonorName = req.DonorName ?? "Unknown",
                        DonorImageUrl = donorImage,
                        Status = req.Status ?? "Pending",
                        RequestedAt = req.RequestedAt,
                        Message = req.Message ?? string.Empty
                    };
                    _allRequests.Add(newItem);

                    if (string.Equals(newItem.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        pendingRequestsToCheck.Add(newItem);
                    }
                }
            }

            // Recount
            _pendingCount = _allRequests.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            _completedCount = _allRequests.Count - _pendingCount;

            ApplyFilter(_currentFilter);

            // 3. Background check for deleted posts (orphaned requests)
            if (pendingRequestsToCheck.Count > 0 && _databaseService != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                _ = Task.Run(async () =>
                {
                    foreach (var item in pendingRequestsToCheck)
                    {
                        try
                        {
                            var post = await _databaseService.GetDonationPostAsync(item.PostId);
                            if (post == null)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    item.Status = "Item Deleted";
                                    // Refresh counts and filter if needed
                                    _pendingCount--;
                                    _completedCount++;
                                    UpdateFilterUI();
                                    if (_currentFilter == "Pending") _requests.Remove(item);
                                    else if (_currentFilter == "Completed") _requests.Add(item); // Simple add, sorting might be off but acceptable
                                });
                                await _databaseService.UpdateRequestStatusAsync(item.RequestId, "Item Deleted");
                            }
                        }
                        catch { }
                    }
                });
            }
        }

        private void ApplyFilter(string filter)
        {
            _currentFilter = filter;
            UpdateFilterUI();

            // Filter items
            IEnumerable<RequestItemViewModel> filteredItems;
            if (filter == "Pending")
            {
                filteredItems = _allRequests.Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            }
            else // Completed
            {
                filteredItems = _allRequests.Where(r => !string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            }

            // Smart Merge into _requests (ObservableCollection)
            var newSet = new HashSet<string>(filteredItems.Select(r => r.RequestId));
            
            // Remove items not in new set
            var toRemove = _requests.Where(r => !newSet.Contains(r.RequestId)).ToList();
            foreach(var item in toRemove) _requests.Remove(item);

            // Add items not in current list
            foreach(var item in filteredItems)
            {
                if (!_requests.Any(r => r.RequestId == item.RequestId))
                {
                    _requests.Add(item);
                }
            }

            // Show/Hide list
            if (_requests.Count == 0 && _allRequests.Count == 0)
            {
                ShowEmptyState();
            }
            else
            {
                EmptyStateLayout.IsVisible = false;
                RequestsCollectionView.IsVisible = true;
            }
        }

        private void UpdateFilterUI()
        {
            FilterPendingLabel.Text = $"Pending ({_pendingCount})";
            FilterCompletedLabel.Text = $"Completed ({_completedCount})";

            if (_currentFilter == "Pending")
            {
                FilterPending.BackgroundColor = Colors.White;
                FilterPendingLabel.TextColor = Color.FromArgb("#5B2EFF");
                
                FilterCompleted.BackgroundColor = Color.FromArgb("#33FFFFFF");
                FilterCompletedLabel.TextColor = Colors.White;
            }
            else
            {
                FilterPending.BackgroundColor = Color.FromArgb("#33FFFFFF");
                FilterPendingLabel.TextColor = Colors.White;
                
                FilterCompleted.BackgroundColor = Colors.White;
                FilterCompletedLabel.TextColor = Color.FromArgb("#5B2EFF");
            }
        }

        private void OnFilterPendingTapped(object sender, EventArgs e) => ApplyFilter("Pending");
        private void OnFilterCompletedTapped(object sender, EventArgs e) => ApplyFilter("Completed");

        private void ShowEmptyState()
        {
            EmptyStateLayout.IsVisible = true;
            RequestsCollectionView.IsVisible = false;
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnBrowseHomeClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//HomePage");
        }

        private async void OnCancelRequestClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is RequestItemViewModel request)
            {
                bool confirm = await DisplayAlert(
                    "Cancel Request",
                    $"Are you sure you want to cancel your request for '{request.ItemTitle}'?",
                    "Yes",
                    "No"
                );

                if (!confirm || _databaseService == null)
                    return;

                try
                {
                    var result = await _databaseService.CancelRequestAsync(request.RequestId);

                    if (result.Success)
                    {
                        await DisplayAlert("Success", "Request cancelled successfully.", "OK");
                        await LoadMyRequestsAsync(); // Reload the list
                    }
                    else
                    {
                        await DisplayAlert("Error", result.Message, "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to cancel request: {ex.Message}", "OK");
                }
            }
        }

        private async void OnViewDetailsClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is RequestItemViewModel request)
            {
                // Navigate to ProductDetailsPage with the PostId
                await Shell.Current.GoToAsync($"{nameof(ProductDetailsPage)}?PostId={request.PostId}");
            }
        }
    }

    // ViewModel for displaying request items
    public class RequestItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string RequestId { get; set; } = string.Empty;
        public string PostId { get; set; } = string.Empty;
        
        private string _itemTitle = string.Empty;
        public string ItemTitle 
        { 
            get => _itemTitle; 
            set { if(_itemTitle != value) { _itemTitle = value; OnPropertyChanged(); } } 
        }

        public string ItemDescription { get; set; } = string.Empty;
        
        private string _itemImageUrl = string.Empty;
        public string ItemImageUrl 
        { 
            get => _itemImageUrl; 
            set { if(_itemImageUrl != value) { _itemImageUrl = value; OnPropertyChanged(); } } 
        }

        private string _donorName = string.Empty;
        public string DonorName 
        { 
            get => _donorName; 
            set { if(_donorName != value) { _donorName = value; OnPropertyChanged(); } } 
        }

        private string _donorImageUrl = string.Empty;
        public string DonorImageUrl 
        { 
            get => _donorImageUrl; 
            set { if(_donorImageUrl != value) { _donorImageUrl = value; OnPropertyChanged(); } } 
        }

        private string _status = string.Empty;
        public string Status 
        { 
            get => _status; 
            set 
            { 
                if(_status != value) 
                { 
                    _status = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(StatusBackgroundColor));
                    OnPropertyChanged(nameof(StatusTextColor));
                    OnPropertyChanged(nameof(CanCancel));
                } 
            } 
        }

        public DateTime RequestedAt { get; set; }
        public string Message { get; set; } = string.Empty;

        public string StatusBackgroundColor
        {
            get
            {
                return Status?.ToLower() switch
                {
                    "pending" => "#FEF3C7",     // Yellow
                    "approved" => "#D1FAE5",    // Green
                    "rejected" => "#FEE2E2",    // Red
                    "completed" => "#DBEAFE",   // Blue
                    "cancelled" => "#F3F4F6",   // Gray
                    _ => "#F3F4F6"
                };
            }
        }

        public string StatusTextColor
        {
            get
            {
                return Status?.ToLower() switch
                {
                    "pending" => "#92400E",     // Dark yellow
                    "approved" => "#065F46",    // Dark green
                    "rejected" => "#991B1B",    // Dark red
                    "completed" => "#1E40AF",   // Dark blue
                    "cancelled" => "#6B7280",   // Gray
                    _ => "#6B7280"
                };
            }
        }

        public bool CanCancel
        {
            get
            {
                var status = Status?.ToLower();
                return status == "pending" || status == "approved";
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
