using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using ShareCircle_G17.Services;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Views
{
    public partial class SavedItemsPage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;
        private readonly IFirebaseDatabaseService _databaseService;
        private readonly ISQLiteDatabaseService? _sqliteService;
        private ObservableCollection<SavedItemViewModel> _savedItems;
        private CancellationTokenSource? _loadingCts;

        public SavedItemsPage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService, ISQLiteDatabaseService sqliteDatabaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
            _sqliteService = sqliteDatabaseService;
            _savedItems = new ObservableCollection<SavedItemViewModel>();
            SavedItemsCollectionView.ItemsSource = _savedItems;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadSavedItems();
        }

        private async Task LoadSavedItems()
        {
            try
            {
                ShowLoadingState();

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.UserId))
                {
                    EmptyStateLayout.IsVisible = true;
                    SavedItemsCollectionView.IsVisible = false;
                    return;
                }

                var userId = currentUser.UserId;
                var savedItems = await _databaseService.GetSavedItemsAsync(userId);

                // Persist saved items + ids to SQLite for offline access
                if (savedItems != null && savedItems.Count > 0 && _sqliteService != null)
                {
                    await _sqliteService.InitializeDatabaseAsync();
                    var savedIds = savedItems
                        .Where(p => !string.IsNullOrWhiteSpace(p.PostId))
                        .Select(p => p.PostId!)
                        .ToList();

                    foreach (var item in savedItems)
                    {
                        await _sqliteService.SaveDonationAsync(item);
                    }

                    if (savedIds.Count > 0)
                    {
                        await _sqliteService.SyncSavedPostIdsAsync(userId, savedIds);
                    }
                }

                // Fallback to SQLite if none from Firebase
                if ((savedItems == null || savedItems.Count == 0) && _sqliteService != null)
                {
                    await _sqliteService.InitializeDatabaseAsync();
                    var localSavedIds = await _sqliteService.GetSavedPostIdsAsync(userId);
                    var localDonations = await _sqliteService.GetAllDonationsAsync();
                    if (localSavedIds != null && localDonations != null)
                    {
                        savedItems = localDonations.Where(d => d.PostId != null && localSavedIds.Contains(d.PostId))
                                                   .ToList();
                    }
                }

                _savedItems.Clear();

                if (savedItems != null && savedItems.Count > 0)
                {
                    foreach (var item in savedItems)
                    {
                        _savedItems.Add(new SavedItemViewModel(item));
                    }

                    EmptyStateLayout.IsVisible = false;
                    SavedItemsCollectionView.IsVisible = true;
                }
                else
                {
                    EmptyStateLayout.IsVisible = true;
                    SavedItemsCollectionView.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                EmptyStateLayout.IsVisible = true;
                SavedItemsCollectionView.IsVisible = false;
                System.Diagnostics.Debug.WriteLine($"Error loading saved items: {ex.Message}");
            }
            finally
            {
                HideLoadingState();
                RefreshView.IsRefreshing = false;
            }
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await LoadSavedItems();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnBrowseItemsClicked(object sender, EventArgs e)
        {
            // Navigate back to the home feed to browse items
            await Shell.Current.GoToAsync("//HomePage");
        }

        private async void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is SavedItemViewModel item && !string.IsNullOrEmpty(item.PostId))
            {
                await Shell.Current.GoToAsync($"{nameof(ProductDetailsPage)}?PostId={item.PostId}");
            }
        }

        private async void OnUnsaveTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is SavedItemViewModel item)
            {
                bool confirm = await DisplayAlert(
                    "Unsave Item",
                    $"Remove '{item.Title}' from saved items?",
                    "Yes",
                    "Cancel"
                );

                if (confirm)
                {
                    try
                    {
                        var currentUser = await _authService.GetCurrentUserAsync();
                        if (currentUser != null && item.PostId != null)
                        {
                            await _databaseService.UnsaveItemAsync(currentUser.UserId ?? "", item.PostId);
                            if (_sqliteService != null)
                            {
                                await _sqliteService.InitializeDatabaseAsync();
                                await _sqliteService.RemoveSavedPostIdAsync(currentUser.UserId ?? string.Empty, item.PostId);
                            }
                            _savedItems.Remove(item);

                            if (_savedItems.Count == 0)
                            {
                                EmptyStateLayout.IsVisible = true;
                                SavedItemsCollectionView.IsVisible = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"Failed to unsave item: {ex.Message}", "OK");
                    }
                }
            }
        }

        #region Loading Animation

        private void ShowLoadingState()
        {
            LoadingDotsGrid.IsVisible = true;
            EmptyStateLayout.IsVisible = false;
            SavedItemsCollectionView.IsVisible = false;
            StartLoadingDots();
        }

        private void HideLoadingState()
        {
            StopLoadingDots();
            LoadingDotsGrid.IsVisible = false;

            if (_savedItems.Count == 0)
            {
                EmptyStateLayout.IsVisible = true;
                SavedItemsCollectionView.IsVisible = false;
            }
            else
            {
                EmptyStateLayout.IsVisible = false;
                SavedItemsCollectionView.IsVisible = true;
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

    public class SavedItemViewModel
    {
        private readonly Models.DonationPost _post;

        public SavedItemViewModel(Models.DonationPost post)
        {
            _post = post;
            SavedAt = post.CreatedAt;
        }

        public string? PostId => _post.PostId;
        public string? Title => _post.Title;
        public string? Description => _post.Description;
        public string? Category => _post.Category;
        public DateTime SavedAt { get; set; }

        // Image properties
        public string Image
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_post.ImageUrl))
                    return "https://via.placeholder.com/400x300/EEF2FF/9CA3AF?text=No+Image";

                var url = _post.ImageUrl;
                if (url.Contains("|"))
                    url = url.Split('|')[0];

                return url;
            }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(_post.ImageUrl);

        // User properties
        public string Username => _post.Username ?? "Anonymous";
        public string UserInitial => string.IsNullOrWhiteSpace(_post.Username)
            ? "A"
            : _post.Username.Substring(0, 1).ToUpperInvariant();

        // Time ago
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - _post.CreatedAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                return _post.CreatedAt.ToString("MMM dd");
            }
        }

        // Always saved in this page
        public bool IsSaved => true;
    }
}
