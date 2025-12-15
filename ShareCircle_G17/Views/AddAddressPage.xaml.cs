using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views;

[QueryProperty(nameof(AddressId), "AddressId")]
[QueryProperty(nameof(SelectedAddress), "SelectedAddress")]
public partial class AddAddressPage : ContentPage
{
    private readonly IFirebaseAuthService? _authService;
    private readonly IFirebaseDatabaseService? _databaseService;
    private string? _userId;
    private string? _addressId;
    private bool _isEdit;
    private Address? _editingAddress;
    private string? _selectedAddress;
    private double _latitude;
    private double _longitude;

    public AddAddressPage()
    {
        InitializeComponent();
    }

    public AddAddressPage(
        IFirebaseAuthService authService,
        IFirebaseDatabaseService databaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;
    }

    public string? AddressId
    {
        get => _addressId;
        set
        {
            _addressId = value;
            _ = LoadAddressForEditAsync();
        }
    }

    public AddressPrediction? SelectedAddress
    {
        set
        {
            if (value != null)
            {
                _selectedAddress = value.FullAddress ?? value.PrimaryText;
                _latitude = value.Latitude;
                _longitude = value.Longitude;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Update the old select label if it still exists or if needed for compatibility
                    AddressSelectLabel.Text = value.PrimaryText ?? "Select Address";
                    AddressSelectLabel.TextColor = Color.FromArgb("#111827");

                    UpdateMapPreview(value.Latitude, value.Longitude, value.PrimaryText ?? "Selected Location");
                });
            }
        }
    }

    private void UpdateMapPreview(double lat, double lng, string label)
    {
        try
        {
            var location = new Location(lat, lng);
            var mapSpan = MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.2)); // Close zoom
            PreviewMap.MoveToRegion(mapSpan);

            PreviewMap.Pins.Clear();
            PreviewMap.Pins.Add(new Pin
            {
                Label = label,
                Location = location,
                Type = PinType.Place
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating preview map: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureUserAsync();
        if (_isEdit && _editingAddress == null)
        {
            await LoadAddressForEditAsync();
        }
    }

    private async Task EnsureUserAsync()
    {
        if (_userId != null)
            return;

        if (_authService == null)
            throw new InvalidOperationException("Auth service unavailable.");

        var user = await _authService.GetCurrentUserAsync();
        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
            throw new InvalidOperationException("No user logged in.");

        _userId = user.UserId;
    }

    private async Task LoadAddressForEditAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_addressId) || _databaseService == null)
                return;

            await EnsureUserAsync();
            var list = await _databaseService.GetAddressesAsync(_userId!);
            var address = list.FirstOrDefault(a => a.AddressId == _addressId);
            if (address == null)
                return;

            _isEdit = true;
            _editingAddress = address;

            NameEntry.Text = address.FullName;
            PhoneEntry.Text = address.PhoneNumber?.Replace("+60", "").Trim();
            
            // Update UI with loaded address
            AddressSelectLabel.Text = address.Street ?? "Select Address";
            AddressSelectLabel.TextColor = string.IsNullOrWhiteSpace(address.Street)
                ? Color.FromArgb("#9CA3AF")
                : Color.FromArgb("#111827");
            
            UpdateMapPreview(address.Latitude, address.Longitude, address.Street ?? "Home");

            AddressDetailsEntry.Text = address.Unit;
            DefaultSwitch.IsToggled = address.IsDefault;
            _selectedAddress = address.Street;
            _latitude = address.Latitude;
            _longitude = address.Longitude;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load address: {ex.Message}", "OK");
        }
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry)
        {
            var border = GetParentBorder(entry);
            if (border != null)
            {
                border.Stroke = Color.FromArgb("#5B2EFF");
            }
        }
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry)
        {
            var border = GetParentBorder(entry);
            if (border != null)
            {
                border.Stroke = Color.FromArgb("#E5E7EB");
            }
        }
    }

    private Border? GetParentBorder(Entry entry)
    {
        if (entry == NameEntry) return NameBorder;
        if (entry == PhoneEntry) return PhoneBorder;
        return null;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSelectAddressTapped(object sender, EventArgs e)
    {
        // Navigate to address search page
        // Pass current address if available to pre-fill search
        var navigationParameter = new Dictionary<string, object>();
        
        if (!string.IsNullOrWhiteSpace(_selectedAddress))
        {
            navigationParameter.Add("InitialQuery", _selectedAddress);
        }

        await Shell.Current.GoToAsync(nameof(AddressSearchPage), navigationParameter);
    }

    private async void OnMapPreviewTapped(object sender, EventArgs e)
    {
        // Navigate directly to map page for adjustment
        // If we have a selected address, pass its coordinates
        var navigationParameter = new Dictionary<string, object>
        {
            { "InitialAddress", _selectedAddress ?? "Selected Location" },
            { "InitialLatitude", _latitude.ToString() },
            { "InitialLongitude", _longitude.ToString() },
            { "ReturnDepth", "1" } // Only go back 1 step (to this page)
        };

        await Shell.Current.GoToAsync(nameof(AddressMapPage), navigationParameter);
    }

    private void OnPrivacyPolicyTapped(object sender, EventArgs e)
    {
        // Navigate to privacy policy or show dialog
        _ = DisplayAlert("Privacy Policy", "Your privacy is important to us. We only use your address information to facilitate donations and pickups.", "OK");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Reset validation
        NameValidationLabel.IsVisible = false;
        PhoneValidationLabel.IsVisible = false;

        bool isValid = true;

        // Validate name
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            NameValidationLabel.IsVisible = true;
            NameBorder.Stroke = Color.FromArgb("#EF4444");
            isValid = false;
        }

        // Validate phone
        var phoneText = PhoneEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(phoneText) || !Regex.IsMatch(phoneText, @"^\d{9,10}$"))
        {
            PhoneValidationLabel.IsVisible = true;
            PhoneBorder.Stroke = Color.FromArgb("#EF4444");
            isValid = false;
        }

        // Validate address selection
        if (string.IsNullOrWhiteSpace(_selectedAddress))
        {
            await DisplayAlert("Validation", "Please select an address.", "OK");
            isValid = false;
        }

        if (!isValid)
            return;

        try
        {
            if (_databaseService == null)
                return;

            await EnsureUserAsync();

            SaveButton.IsEnabled = false;
            SaveButton.Text = "Saving...";

            var phoneNumber = $"+60 {PhoneEntry.Text?.Trim()}";

            var address = new Address
            {
                AddressId = _editingAddress?.AddressId,
                Label = "Home",
                Street = _selectedAddress,
                Unit = AddressDetailsEntry.Text?.Trim(),
                PostalCode = _editingAddress?.PostalCode,
                City = _editingAddress?.City,
                Country = "Malaysia",
                PhoneNumber = phoneNumber,
                FullName = NameEntry.Text?.Trim(),
                IsDefault = DefaultSwitch.IsToggled,
                Latitude = _latitude,
                Longitude = _longitude,
                FormattedAddress = _selectedAddress
            };

            var result = await _databaseService.SaveAddressAsync(_userId!, address);
            if (result.Success)
            {
                if (address.IsDefault && result.AddressId != null)
                {
                    await ClearOtherDefaultsAsync(result.AddressId);
                }

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save address: {ex.Message}", "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "Save";
        }
    }

    private async Task ClearOtherDefaultsAsync(string keepAddressId)
    {
        if (_databaseService == null || string.IsNullOrWhiteSpace(_userId))
            return;

        var addresses = await _databaseService.GetAddressesAsync(_userId!);
        foreach (var addr in addresses.Where(a => a.IsDefault && a.AddressId != keepAddressId))
        {
            addr.IsDefault = false;
            await _databaseService.SaveAddressAsync(_userId!, addr);
        }
    }
}
