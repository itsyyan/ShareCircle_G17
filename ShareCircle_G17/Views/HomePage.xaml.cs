using System;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Views;

public partial class HomePage : ContentPage
{
    // BindableProperty so the UI updates when UserName changes
    public static readonly BindableProperty UserNameProperty =
        BindableProperty.Create(nameof(UserName), typeof(string), typeof(HomePage), defaultValue: "Yan");

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    // BindableProperty for the total donations (sample placeholder shown on first load)
    public static readonly BindableProperty TotalDonationsProperty =
        BindableProperty.Create(nameof(TotalDonations), typeof(int), typeof(HomePage), defaultValue: 25);

    public int TotalDonations
    {
        get => (int)GetValue(TotalDonationsProperty);
        set => SetValue(TotalDonationsProperty, value);
    }

    public HomePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    // Navigation bar handlers
    private async void OnHomeNavTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HomePage");
    }

    private async void OnDonateNavTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(DonationPage));
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "None";
            await DisplayAlert("Navigation Error",
                $"Exception: {ex.GetType().Name}\nMessage: {ex.Message}\nInner: {inner}",
                "OK");
        }
    }

    private async void OnMyDonationNavTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(UserDonationPage));
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "None";
            await DisplayAlert("Navigation Error",
                $"Exception: {ex.GetType().Name}\nMessage: {ex.Message}\nInner: {inner}",
                "OK");
        }
    }

    private async void OnProfileNavTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("ProfilePage");
        }
        catch (Exception)
        {
            await DisplayAlert("Profile", "Profile page route not found.", "OK");
        }
    }

    private async void OnCommunityNavTapped(object? sender, EventArgs e)
    {
        // Navigate to CommunityPage
        await Shell.Current.GoToAsync(nameof(CommunityPage));
    }

    private async void OnFoodFilterTapped(object? sender, TappedEventArgs e)
    {
        var subCategory = e.Parameter?.ToString() ?? string.Empty;
        await NavigateToCommunityWithFilters("food", subCategory);
    }

    private async void OnItemFilterTapped(object? sender, TappedEventArgs e)
    {
        var subCategory = e.Parameter?.ToString() ?? string.Empty;
        await NavigateToCommunityWithFilters("item", subCategory);
    }

    private static Task NavigateToCommunityWithFilters(string categoryType, string? subCategory)
    {
        var route = $"{nameof(CommunityPage)}?categoryType={Uri.EscapeDataString(categoryType)}";

        if (!string.IsNullOrWhiteSpace(subCategory))
        {
            route += $"&subcategory={Uri.EscapeDataString(subCategory)}";
        }

        return Shell.Current.GoToAsync(route);
    }
}