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
        private ObservableCollection<RequestItemViewModel> _requests;

        // Constructor with dependency injection
        public MyRequestsPage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
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
                _requests.Clear();

                // Check if services are available
                if (_authService == null || _databaseService == null)
                {
                    ShowEmptyState();
                    return;
                }

                // Get current user
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrEmpty(currentUser.UserId))
                {
                    await DisplayAlert("Error", "You must be logged in to view your requests.", "OK");
                    ShowEmptyState();
                    return;
                }

                // Get user's requests from database
                var userRequests = await _databaseService.GetUserRequestsAsync(currentUser.UserId);

                System.Diagnostics.Debug.WriteLine($"MyRequests - Found {userRequests?.Count ?? 0} requests");

                if (userRequests == null || userRequests.Count == 0)
                {
                    ShowEmptyState();
                    return;
                }

                // Convert to view models for display
                foreach (var request in userRequests)
                {
                    string donorImage = "https://img.icons8.com/color/48/user-male-circle--v1.png"; // Default
                    
                    // Try to fetch donor's actual profile image
                    if (!string.IsNullOrEmpty(request.DonorId))
                    {
                        try
                        {
                            var donorProfile = await _databaseService.GetUserProfileAsync(request.DonorId);
                            if (donorProfile != null && !string.IsNullOrEmpty(donorProfile.ProfileImageUrl))
                            {
                                donorImage = donorProfile.ProfileImageUrl;
                            }
                        }
                        catch
                        {
                            // Ignore error, keep default
                        }
                    }

                    _requests.Add(new RequestItemViewModel
                    {
                        RequestId = request.RequestId ?? string.Empty,
                        PostId = request.PostId ?? string.Empty,
                        ItemTitle = request.ItemTitle ?? "Untitled",
                        ItemDescription = request.ItemDescription ?? string.Empty,
                        ItemImageUrl = request.ItemImageUrl ?? string.Empty,
                        DonorName = request.DonorName ?? "Unknown",
                        DonorImageUrl = donorImage,
                        Status = request.Status ?? "Pending",
                        RequestedAt = request.RequestedAt,
                        Message = request.Message ?? string.Empty
                    });
                }

                // Show requests list
                EmptyStateLayout.IsVisible = false;
                RequestsCollectionView.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMyRequestsAsync - Error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to load requests: {ex.Message}", "OK");
                ShowEmptyState();
            }
        }

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
    public class RequestItemViewModel
    {
        public string RequestId { get; set; } = string.Empty;
        public string PostId { get; set; } = string.Empty;
        public string ItemTitle { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string ItemImageUrl { get; set; } = string.Empty;
        public string DonorName { get; set; } = string.Empty;
        public string DonorImageUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
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
    }
}
