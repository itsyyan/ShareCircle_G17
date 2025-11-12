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
        // Navigate to the new DonationPage
        await Shell.Current.GoToAsync(nameof(DonationPage));
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
}