using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views;

public partial class RequestDetailsPage : ContentPage, IQueryAttributable
{
    private readonly IFirebaseDatabaseService? _databaseService;
    private readonly IFirebaseAuthService? _authService;
    private readonly ISQLiteDatabaseService? _sqliteService;
    private DonationRequest? _request;
    private bool _isOwner;

    public RequestDetailsPage()
    {
        InitializeComponent();
        _databaseService = Resolve<IFirebaseDatabaseService>() ?? new FirebaseDatabaseService();
        _authService = Resolve<IFirebaseAuthService>() ?? new FirebaseAuthService();
        _sqliteService = Resolve<ISQLiteDatabaseService>();

        // Hide buttons by default until we know the status
        AcceptButton.IsVisible = false;
        RejectButton.IsVisible = false;
    }

    public RequestDetailsPage(
        IFirebaseDatabaseService databaseService,
        IFirebaseAuthService authService,
        ISQLiteDatabaseService sqliteService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _authService = authService;
        _sqliteService = sqliteService;

        // Hide buttons by default until we know the status
        AcceptButton.IsVisible = false;
        RejectButton.IsVisible = false;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("RequestId", out var idObj) && idObj is string requestId && !string.IsNullOrWhiteSpace(requestId))
        {
            await LoadRequestAsync(requestId);
        }
    }

    private async Task LoadRequestAsync(string requestId)
    {
        try
        {
            var currentUser = _authService != null ? await _authService.GetCurrentUserAsync() : null;

            // Step 1: Load from local cache first (instant, no flicker)
            if (_sqliteService != null)
            {
                var cachedRequest = await _sqliteService.GetRequestAsync(requestId);
                if (cachedRequest != null)
                {
                    _request = cachedRequest;
                    _isOwner = currentUser != null && !string.IsNullOrWhiteSpace(cachedRequest.DonorId) &&
                               string.Equals(cachedRequest.DonorId, currentUser.UserId, StringComparison.OrdinalIgnoreCase);

                    // Display cached data immediately
                    DisplayRequest(cachedRequest);
                    System.Diagnostics.Debug.WriteLine($"RequestDetailsPage: Loaded from cache, status={cachedRequest.Status}");
                }
            }

            // Step 2: Sync from Firebase if online
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet && _databaseService != null)
            {
                var firebaseRequest = await _databaseService.GetRequestAsync(requestId);
                if (firebaseRequest != null)
                {
                    _request = firebaseRequest;
                    _isOwner = currentUser != null && !string.IsNullOrWhiteSpace(firebaseRequest.DonorId) &&
                               string.Equals(firebaseRequest.DonorId, currentUser.UserId, StringComparison.OrdinalIgnoreCase);

                    // Update UI with latest data
                    DisplayRequest(firebaseRequest);

                    // Cache to SQLite for next time
                    if (_sqliteService != null)
                    {
                        await _sqliteService.SaveRequestAsync(firebaseRequest);
                    }

                    System.Diagnostics.Debug.WriteLine($"RequestDetailsPage: Synced from Firebase, status={firebaseRequest.Status}");
                }
            }

            // If still no request found
            if (_request == null)
            {
                await DisplayAlert("Request", "Request not found.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load request: {ex.Message}", "OK");
        }
    }

    private void DisplayRequest(DonationRequest req)
    {
        TitleLabel.Text = string.IsNullOrWhiteSpace(req.ItemTitle) ? "Item" : req.ItemTitle;
        RequesterLabel.Text = string.IsNullOrWhiteSpace(req.RequesterName)
            ? "Unknown User"
            : req.RequesterName;
        StatusLabel.Text = string.IsNullOrWhiteSpace(req.Status) ? "Pending" : req.Status;
        DescriptionLabel.Text = string.IsNullOrWhiteSpace(req.ItemDescription) ? "No description provided." : req.ItemDescription;
        RequestedAtLabel.Text = req.RequestedAt == default
            ? "Requested recently"
            : $"Requested {req.RequestedAt.ToLocalTime():MMM dd, yyyy h:mm tt}";

        var image = string.IsNullOrWhiteSpace(req.ItemImageUrl)
            ? "https://via.placeholder.com/400x300/EEF2FF/9CA3AF?text=Item"
            : req.ItemImageUrl;
        ItemImage.Source = image;

        // Requester avatar
        UpdateRequesterAvatar(req);

        // Show/hide action buttons based on status
        AcceptButton.IsVisible = RejectButton.IsVisible = _isOwner && !IsFinalStatus(req.Status);
    }

    private async void UpdateRequesterAvatar(DonationRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.RequesterId) && _databaseService != null)
        {
            try
            {
                var profile = await _databaseService.GetUserProfileAsync(req.RequesterId);
                if (profile != null)
                {
                    // 1. Update Username if available (fix for missing name)
                    if (!string.IsNullOrWhiteSpace(profile.Username))
                    {
                        RequesterLabel.Text = profile.Username;
                    }

                    // 2. Update Avatar if available
                    if (!string.IsNullOrWhiteSpace(profile.ProfileImageUrl))
                    {
                        RequesterImage.Source = profile.ProfileImageUrl;
                        RequesterImage.IsVisible = true;
                        RequesterInitialLabel.IsVisible = false;
                        return;
                    }
                }
            }
            catch
            {
                // Ignore and fall through to show initial
            }
        }

        RequesterImage.IsVisible = false;
        RequesterInitialLabel.IsVisible = true;
        var initial = string.IsNullOrWhiteSpace(RequesterLabel.Text) || RequesterLabel.Text == "Unknown User"
            ? "R" 
            : RequesterLabel.Text.Substring(0, 1).ToUpper();
        RequesterInitialLabel.Text = initial;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        await UpdateStatusAsync("approved");
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        await UpdateStatusAsync("rejected");
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        if (_request == null)
            return;

        try
        {
            bool isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            if (isOnline && _databaseService != null)
            {
                var result = await _databaseService.UpdateRequestStatusAsync(_request.RequestId!, newStatus);
                if (result.Success)
                {
                    _request.Status = newStatus;
                    _request.RespondedAt = DateTime.UtcNow;
                    StatusLabel.Text = newStatus;
                    AcceptButton.IsVisible = RejectButton.IsVisible = false;

                    // Save to local cache
                    if (_sqliteService != null)
                    {
                        await _sqliteService.SaveRequestAsync(_request);
                    }

                    await DisplayAlert("Success", $"Request {newStatus}.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", result.Message, "OK");
                }
            }
            else
            {
                // Offline: save locally only
                _request.Status = newStatus;
                _request.RespondedAt = DateTime.UtcNow;
                _request.IsSynced = false;
                StatusLabel.Text = newStatus;
                AcceptButton.IsVisible = RejectButton.IsVisible = false;

                if (_sqliteService != null)
                {
                    await _sqliteService.SaveRequestAsync(_request);
                }

                await DisplayAlert("Success", $"Request {newStatus}. Will sync when online.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to update: {ex.Message}", "OK");
        }
    }

    private bool IsFinalStatus(string? status)
    {
        var s = status?.ToLowerInvariant();
        return s == "approved" || s == "rejected" || s == "completed" || s == "cancelled";
    }

    private T? Resolve<T>() where T : class
    {
        return Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(T)) as T;
    }
}
