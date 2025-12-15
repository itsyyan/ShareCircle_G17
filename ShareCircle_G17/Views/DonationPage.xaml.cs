using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using ShareCircle_G17.Services;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Views;

[QueryProperty(nameof(PostId), "PostId")]
public partial class DonationPage : ContentPage
{
    private const int MaxPhotos = 10;

    private readonly ISyncService _syncService;
    private readonly IFirebaseAuthService _authService;
    private readonly IFirebaseDatabaseService _databaseService;
    private readonly ISQLiteDatabaseService _sqliteService;
    private string? _userId;

    // track current category selection
    private string? _selectedCategory = null;
    private string? _selectedSubCategory = null;
    private readonly List<byte[]> _selectedImagesData = new();
    public ObservableCollection<ImageSource> SelectedImages { get; } = new();
    public List<Address> Addresses { get; private set; } = new();
    public Address? SelectedAddress { get; private set; }
    public string? PostId
    {
        get => _postId;
        set => _postId = value;
    }
    private string? _postId;
    private bool _isEdit;
    private DonationPost? _editingDonation;
    private string? _existingImageUrl;

    public DonationPage(
        ISyncService syncService,
        IFirebaseAuthService authService,
        IFirebaseDatabaseService databaseService,
        ISQLiteDatabaseService sqliteService)
    {
        InitializeComponent();
        _syncService = syncService;
        _authService = authService;
        _databaseService = databaseService;
        _sqliteService = sqliteService;
        BindingContext = this;
        _selectedCategory = "Food";
        SetToggleStyle(foodActive: true);
        SelectedImages.CollectionChanged += OnSelectedImagesCollectionChanged;
        UpdatePhotoCountLabel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAddressesAsync();
        if (!_isEdit && !string.IsNullOrWhiteSpace(_postId))
        {
            await LoadDonationForEditAsync(_postId);
        }
    }

    private async Task LoadDonationForEditAsync(string postId)
    {
        try
        {
            // ... (rest of method unchanged, but we'll include context to be safe)
            if (_databaseService == null)
            {
                await DisplayAlert("Error", "Unable to load donation. Database not available.", "OK");
                return;
            }
            
            // Try local first if offline, or fall back to it
            DonationPost? donation = null;
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                 donation = await _databaseService.GetDonationPostAsync(postId);
            }
            
            if (donation == null)
            {
                 // Try local
                 donation = await _sqliteService.GetDonationAsync(postId);
            }

            if (donation == null)
            {
                await DisplayAlert("Error", "Donation not found.", "OK");
                return;
            }

            _isEdit = true;
            _editingDonation = donation;
            _existingImageUrl = donation.ImageUrl;
            _postId = postId;

            PageTitleLabel.Text = "Edit Post";
            PostButton.Text = "Update";

            WhatIsItEntry.Text = donation.Title ?? string.Empty;
            DescriptionEditor.Text = donation.Description ?? string.Empty;

            ApplyCategoryFromDonation(donation.Category, donation.SubCategory);

            // Address selection logic...
            // Note: We need Addresses loaded first. OnAppearing calls LoadAddressesAsync first.
            if (Addresses.Any())
            {
                SelectedAddress = Addresses.FirstOrDefault(a =>
                    (!string.IsNullOrWhiteSpace(a.FormattedAddress) &&
                     string.Equals(a.FormattedAddress, donation.DropOffLocation, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(a.OneLine) &&
                     string.Equals(a.OneLine, donation.DropOffLocation, StringComparison.OrdinalIgnoreCase)));

                if (SelectedAddress == null)
                {
                    SelectedAddress = Addresses.First();
                }

                AddressesCollectionView.SelectedItem = SelectedAddress;
            }

            // Images
            SelectedImages.Clear();
            _selectedImagesData.Clear();
            AddExistingImagePreview(donation.ImageUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to load donation: {ex.Message}", "OK");
        }
    }

    private async Task LoadAddressesAsync()
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await DisplayAlert("Login", "Please log in to create a donation.", "OK");
                return;
            }

            _userId = currentUser.UserId;
            List<Address> list = new();
            bool fetchSuccess = false;

            // 1. Try Remote
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try 
                {
                    list = await _databaseService.GetAddressesAsync(_userId);
                    if (list != null && list.Count > 0)
                    {
                        fetchSuccess = true;
                        // Cache addresses
                        foreach (var addr in list)
                        {
                            await _sqliteService.SaveAddressAsync(addr);
                        }
                    }
                }
                catch { /* Ignore and fallback */ }
            }

            // 2. Fallback to SQLite
            if (!fetchSuccess)
            {
                list = await _sqliteService.GetAddressesAsync(_userId);
            }

            Addresses = list ?? new List<Address>();
            AddressesCollectionView.ItemsSource = Addresses;

            var defaultAddr = Addresses.FirstOrDefault(a => a.IsDefault);
            SelectedAddress = defaultAddr ?? Addresses.FirstOrDefault();
            if (SelectedAddress != null)
            {
                AddressesCollectionView.SelectedItem = SelectedAddress;
            }
            else
            {
                SelectedAddress = null;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load addresses: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        // Navigate back to previous page
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPostDonationClicked(object sender, EventArgs e)
    {
        var buttonLabel = _isEdit ? "Update" : "Post";
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(WhatIsItEntry.Text))
            {
                await DisplayAlert("Validation Error", "Please enter what the item is.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedCategory))
            {
                await DisplayAlert("Validation Error", "Please select a category (Food or Item).", "OK");
                return;
            }

            if (SelectedAddress == null)
            {
                await DisplayAlert("Validation Error", "Please select a pickup address.", "OK");
                return;
            }

            // Show loading indicator
            PostButton.IsEnabled = false;
            PostButton.Text = _isEdit ? "Updating..." : "Posting...";

            string dropOffLocation = SelectedAddress.FormattedAddress ?? SelectedAddress.OneLine;
            double latitude = SelectedAddress.Latitude;
            double longitude = SelectedAddress.Longitude;

            // Get current user
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                await DisplayAlert("Error", "You must be logged in to create a donation.", "OK");
                PostButton.IsEnabled = true;
                PostButton.Text = "Post";
                return;
            }

            var imageUrl = BuildImagePayload();

            // Create or update donation post
            var profile = await _databaseService.GetUserProfileAsync(currentUser.UserId);
            var username = profile?.Username ?? currentUser.Username ?? "Anonymous";
            var userImageUrl = profile?.ProfileImageUrl;

            var donation = _isEdit && _editingDonation != null
                ? _editingDonation
                : new DonationPost { PostId = Guid.NewGuid().ToString(), CreatedAt = DateTime.UtcNow, Status = "Available", ViewCount = 0 };

            donation.UserId = currentUser.UserId;
            donation.Username = username;
            donation.UserImageUrl = userImageUrl;
            donation.Title = WhatIsItEntry.Text.Trim();
            donation.Description = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
                ? string.Empty
                : DescriptionEditor.Text.Trim();
            donation.Category = _selectedCategory?.ToLower() ?? "food";
            donation.SubCategory = DetermineSubCategory(_selectedCategory ?? "food", WhatIsItEntry.Text);
            donation.ImageUrl = imageUrl;
            donation.DropOffLocation = dropOffLocation;
            donation.Location = dropOffLocation;
            donation.Latitude = latitude;
            donation.Longitude = longitude;
            donation.IsSynced = false;

            bool success;
            bool isOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;

            if (_isEdit)
            {
                donation.UpdatedAt = DateTime.UtcNow;
                if (isOffline)
                {
                    // Offline edit: save locally and mark as unsynced
                    donation.IsSynced = false;
                    await _syncService.SaveDonationAsync(donation);
                    success = true;
                }
                else
                {
                    var result = await _databaseService.UpdateDonationPostAsync(donation.PostId!, donation);
                    success = result.Success;
                }
            }
            else
            {
                success = await _syncService.SaveDonationAsync(donation);
            }

            if (success)
            {
                // Debug info
                System.Diagnostics.Debug.WriteLine($"Donation saved successfully! (Offline: {isOffline})");
                System.Diagnostics.Debug.WriteLine($"PostId: {donation.PostId}");
                System.Diagnostics.Debug.WriteLine($"UserId: {donation.UserId}");
                System.Diagnostics.Debug.WriteLine($"Title: {donation.Title}");

                string successMessage;
                if (isOffline)
                {
                    successMessage = _isEdit
                        ? "Changes saved locally. Will sync when you're back online."
                        : "Saved locally! Your donation will be published automatically when you're back online.";
                }
                else
                {
                    successMessage = _isEdit
                        ? "Donation updated successfully!"
                        : "Your donation has been posted successfully!";
                }

                await DisplayAlert("Success", successMessage, "OK");

                if (_isEdit)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    ClearForm();
                    await Shell.Current.GoToAsync("//HomePage");
                }
            }
            else
            {
                await DisplayAlert("Error", "Failed to post donation. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            PostButton.IsEnabled = true;
            PostButton.Text = buttonLabel;
        }
    }

    private string DetermineSubCategory(string category, string itemName)
    {
        // Fallback if no subcategory explicitly chosen
        return _selectedSubCategory ?? (category.ToLower() == "food" ? "fruit" : "cloths");
    }

    private string BuildImagePayload()
    {
        var images = new List<string>();

        if (_selectedImagesData.Count > 0)
        {
            images.AddRange(_selectedImagesData.Select(data => $"data:image/jpeg;base64,{Convert.ToBase64String(data)}"));
        }

        if (!string.IsNullOrWhiteSpace(_existingImageUrl))
        {
            var remaining = Math.Max(0, MaxPhotos - images.Count);
            if (remaining > 0)
            {
                images.AddRange(SplitImageValues(_existingImageUrl).Take(remaining));
            }
        }

        if (images.Count == 0)
        {
            return "https://via.placeholder.com/400x300";
        }

        return string.Join("|", images
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(MaxPhotos));
    }

    private void ApplyCategoryFromDonation(string? category, string? subcategory)
    {
        var isFood = !string.Equals(category, "item", StringComparison.OrdinalIgnoreCase);
        _selectedCategory = isFood ? "Food" : "Item";
        _selectedSubCategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory.ToLowerInvariant();

        FoodSubLayout.IsVisible = isFood;
        ItemSubLayout.IsVisible = !isFood;
        SetToggleStyle(isFood);
        UpdateSubcategoryButtons();
    }

    private void AddExistingImagePreview(string? imageValue)
    {
        if (string.IsNullOrWhiteSpace(imageValue))
            return;

        _existingImageUrl = imageValue;

        try
        {
            foreach (var token in SplitImageValues(imageValue))
            {
                if (SelectedImages.Count >= MaxPhotos)
                    break;

                if (token.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var base64Part = token.Contains(',') ? token[(token.IndexOf(',') + 1)..] : token;
                    var data = Convert.FromBase64String(base64Part);
                    SelectedImages.Add(ImageSource.FromStream(() => new MemoryStream(data)));
                }
                else
                {
                    SelectedImages.Add(ImageSource.FromUri(new Uri(token)));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load existing image: {ex.Message}");
        }
        finally
        {
            UpdatePhotoCountLabel();
        }
    }

    private IEnumerable<string> SplitImageValues(string? imageValue)
    {
        if (string.IsNullOrWhiteSpace(imageValue))
            yield break;

        var trimmed = imageValue.Trim();

        if (trimmed.Contains("|"))
        {
            foreach (var part in trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
            yield break;
        }

        if (trimmed.Contains(";") && !trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var part in trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
            yield break;
        }

        yield return trimmed;
    }

    private void ClearForm()
    {
        WhatIsItEntry.Text = string.Empty;
        DescriptionEditor.Text = string.Empty;
        _selectedCategory = null;
        _selectedSubCategory = null;
        _selectedImagesData.Clear();
        SelectedImages.Clear();
        PhotoOptionsOverlay.IsVisible = false;
        ImagePreviewOverlay.IsVisible = false;

        ResetSubcategoryButtons();
        // Default to Food visible
        FoodSubLayout.IsVisible = true;
        ItemSubLayout.IsVisible = false;
        SetToggleStyle(foodActive: true);
        _selectedCategory = "Food";
        _selectedSubCategory = null;
        _existingImageUrl = null;
        _isEdit = false;
        _editingDonation = null;
        _postId = null;
        PageTitleLabel.Text = "Create Post";
        PostButton.Text = "Post";
        UpdatePhotoCountLabel();
    }

    private void OnSelectedImagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdatePhotoCountLabel();
    }

    private void UpdatePhotoCountLabel()
    {
        if (PhotoCountLabel != null)
        {
            PhotoCountLabel.Text = $"{SelectedImages.Count}/{MaxPhotos}";
        }
        if (UploadPlaceholder != null)
        {
            UploadPlaceholder.IsVisible = SelectedImages.Count == 0;
        }
    }

    private async void OnUploadPhotoTapped(object sender, EventArgs e)
    {
        if (SelectedImages.Count >= MaxPhotos)
        {
            await DisplayAlert("Limit reached", $"You can upload up to {MaxPhotos} photos.", "OK");
            return;
        }

        PhotoOptionsOverlay.IsVisible = true;
    }

    private void OnFoodSubcategoryClicked(object sender, EventArgs e)
    {
        _selectedCategory = "Food";
        if (sender is Button btn && btn.CommandParameter is string sub)
        {
            _selectedSubCategory = sub;
        }
        FoodSubLayout.IsVisible = true;
        ItemSubLayout.IsVisible = false;
        SetToggleStyle(foodActive: true);
        UpdateSubcategoryButtons();
    }

    private void OnItemSubcategoryClicked(object sender, EventArgs e)
    {
        _selectedCategory = "Item";
        if (sender is Button btn && btn.CommandParameter is string sub)
        {
            _selectedSubCategory = sub;
        }
        FoodSubLayout.IsVisible = false;
        ItemSubLayout.IsVisible = true;
        SetToggleStyle(foodActive: false);
        UpdateSubcategoryButtons();
    }

    private void OnFoodToggleClicked(object sender, EventArgs e)
    {
        _selectedCategory = "Food";
        _selectedSubCategory = null;
        FoodSubLayout.IsVisible = true;
        ItemSubLayout.IsVisible = false;
        SetToggleStyle(foodActive: true);
        UpdateSubcategoryButtons();
    }

    private void OnItemToggleClicked(object sender, EventArgs e)
    {
        _selectedCategory = "Item";
        _selectedSubCategory = null;
        FoodSubLayout.IsVisible = false;
        ItemSubLayout.IsVisible = true;
        SetToggleStyle(foodActive: false);
        UpdateSubcategoryButtons();
    }

    private void SetToggleStyle(bool foodActive)
    {
        if (FoodToggle != null && ItemToggle != null)
        {
            FoodToggle.BackgroundColor = foodActive ? Color.FromArgb("#5B2EFF") : Color.FromArgb("#F3F4F6");
            FoodToggle.TextColor = foodActive ? Colors.White : Color.FromArgb("#111827");
            ItemToggle.BackgroundColor = foodActive ? Color.FromArgb("#F3F4F6") : Color.FromArgb("#5B2EFF");
            ItemToggle.TextColor = foodActive ? Color.FromArgb("#111827") : Colors.White;
        }
    }

    private void UpdateSubcategoryButtons()
    {
        // Food group
        SetSubButtonStyle(FruitsVegButton, _selectedCategory == "Food" && _selectedSubCategory == "fruit");
        SetSubButtonStyle(BakeryButton, _selectedCategory == "Food" && _selectedSubCategory == "bread");
        SetSubButtonStyle(PackagedButton, _selectedCategory == "Food" && _selectedSubCategory == "packaged");
        SetSubButtonStyle(CookedButton, _selectedCategory == "Food" && _selectedSubCategory == "cooked");
        SetSubButtonStyle(HalalButton, _selectedCategory == "Food" && _selectedSubCategory == "halal");
        SetSubButtonStyle(FoodOthersButton, _selectedCategory == "Food" && _selectedSubCategory == "others");

        // Item group
        SetSubButtonStyle(ClothesButton, _selectedCategory == "Item" && _selectedSubCategory == "cloths");
        SetSubButtonStyle(BooksButton, _selectedCategory == "Item" && _selectedSubCategory == "books");
        SetSubButtonStyle(FurnitureButton, _selectedCategory == "Item" && _selectedSubCategory == "furniture");
        SetSubButtonStyle(ElectronicsButton, _selectedCategory == "Item" && _selectedSubCategory == "electronics");
        SetSubButtonStyle(ToysButton, _selectedCategory == "Item" && _selectedSubCategory == "toys-household");
        SetSubButtonStyle(ItemOthersButton, _selectedCategory == "Item" && _selectedSubCategory == "others");
    }

    private void ResetSubcategoryButtons()
    {
        SetSubButtonStyle(FruitsVegButton, false);
        SetSubButtonStyle(BakeryButton, false);
        SetSubButtonStyle(PackagedButton, false);
        SetSubButtonStyle(CookedButton, false);
        SetSubButtonStyle(HalalButton, false);
        SetSubButtonStyle(FoodOthersButton, false);
        SetSubButtonStyle(ClothesButton, false);
        SetSubButtonStyle(BooksButton, false);
        SetSubButtonStyle(FurnitureButton, false);
        SetSubButtonStyle(ElectronicsButton, false);
        SetSubButtonStyle(ToysButton, false);
        SetSubButtonStyle(ItemOthersButton, false);
    }

    private void SetSubButtonStyle(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.BackgroundColor = Color.FromArgb("#5B2EFF");
            button.TextColor = Colors.White;
        }
        else
        {
            button.BackgroundColor = Color.FromArgb("#F3F4F6");
            button.TextColor = Color.FromArgb("#111827");
        }
    }

    private async void OnAddAddressClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AddAddressPage));
    }

    private async void OnEditAddressClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is string addressId)
        {
            await Shell.Current.GoToAsync($"{nameof(AddAddressPage)}?AddressId={addressId}");
        }
    }

    private void OnAddressSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Address address)
        {
            SelectedAddress = address;
        }
    }

    private void OnPhotoOptionsBackdropTapped(object sender, EventArgs e)
    {
        PhotoOptionsOverlay.IsVisible = false;
    }

    private async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        await HandlePhotoSelectionAsync(fromCamera: true);
    }

    private async void OnChooseGalleryClicked(object sender, EventArgs e)
    {
        await HandlePhotoSelectionAsync(fromCamera: false);
    }

    private void OnCancelPhotoOptionsClicked(object sender, EventArgs e)
    {
        PhotoOptionsOverlay.IsVisible = false;
    }

    private void OnPreviewImageTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ImageSource source)
        {
            ShowImagePreview(source);
        }
    }

    private void OnCloseImagePreviewTapped(object sender, EventArgs e)
    {
        ImagePreviewOverlay.IsVisible = false;
    }

    private async Task HandlePhotoSelectionAsync(bool fromCamera)
    {
        try
        {
            if (fromCamera)
            {
                if (SelectedImages.Count >= MaxPhotos)
                {
                    await DisplayAlert("Limit reached", $"You can upload up to {MaxPhotos} photos.", "OK");
                    return;
                }

                if (!MediaPicker.IsCaptureSupported)
                {
                    await DisplayAlert("Camera unavailable", "Camera capture not supported. Choose from gallery instead.", "OK");
                    return;
                }

                var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = $"donation_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
                });

                if (result != null)
                {
                    await AddImageAsync(result);
                }
            }
            else
            {
                var results = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select photos",
                    FileTypes = FilePickerFileType.Images
                });

                if (results != null)
                {
                    var files = results.ToList();
                    var allowed = Math.Max(0, MaxPhotos - SelectedImages.Count);

                    foreach (var file in files.Take(allowed))
                    {
                        await AddImageAsync(file);
                    }

                    var skipped = files.Count - allowed;
                    if (skipped > 0)
                    {
                        await DisplayAlert("Limit reached", $"Only {allowed} photos were added. {skipped} were skipped to keep the limit at {MaxPhotos}.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to add photo: {ex.Message}", "OK");
        }
        finally
        {
            PhotoOptionsOverlay.IsVisible = false;
        }
    }

    private async Task AddImageAsync(FileResult file)
    {
        if (SelectedImages.Count >= MaxPhotos)
        {
            await DisplayAlert("Limit reached", $"You can upload up to {MaxPhotos} photos.", "OK");
            return;
        }

        using var stream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var data = ms.ToArray();

        _selectedImagesData.Add(data);
        SelectedImages.Add(ImageSource.FromStream(() => new MemoryStream(data)));
    }

    private void ShowImagePreview(ImageSource source)
    {
        var index = SelectedImages.IndexOf(source);
        if (index < 0)
        {
            index = 0;
        }

        PreviewCarousel.Position = index;
        ImagePreviewOverlay.IsVisible = true;
    }
}
