using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.Views;

public partial class MyAddressesPage : ContentPage
{
    private readonly IFirebaseAuthService? _authService;
    private readonly IFirebaseDatabaseService? _databaseService;
    private string? _userId;

    public ObservableCollection<Address> Addresses { get; } = new();

    public MyAddressesPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public MyAddressesPage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAddressesAsync();
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

    private async Task LoadAddressesAsync()
    {
        try
        {
            if (_databaseService == null)
                return;

            await EnsureUserAsync();
            var list = await _databaseService.GetAddressesAsync(_userId!);

            var sortedList = list
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToList();

            Addresses.Clear();
            foreach (var addr in sortedList)
            {
                Addresses.Add(addr);
            }

            bool isEmpty = Addresses.Count == 0;
            EmptyStateLayout.IsVisible = isEmpty;
            AddressesCollectionView.IsVisible = !isEmpty;
            FooterAddButton.IsVisible = !isEmpty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to load addresses: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteAddressClicked(object sender, EventArgs e)
    {
        if (_databaseService == null)
            return;

        if (sender is Button button && button.CommandParameter is string addressId && !string.IsNullOrWhiteSpace(addressId))
        {
            try
            {
                await EnsureUserAsync();
                bool confirm = await DisplayAlert("Delete", "Delete this address?", "Delete", "Cancel");
                if (!confirm)
                    return;

                var result = await _databaseService.DeleteAddressAsync(_userId!, addressId);
                if (result.Success)
                {
                    await LoadAddressesAsync();
                }
                else
                {
                    await DisplayAlert("Error", result.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete address: {ex.Message}", "OK");
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddAddressClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AddAddressPage));
    }

    private async void OnAddressTapped(object sender, EventArgs e)
    {
        if (sender is Element el && el.BindingContext is Address addr && !string.IsNullOrWhiteSpace(addr.AddressId))
        {
            await Shell.Current.GoToAsync($"{nameof(AddAddressPage)}?AddressId={addr.AddressId}");
        }
    }
}
