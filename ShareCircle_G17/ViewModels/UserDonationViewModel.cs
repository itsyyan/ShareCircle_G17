using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ShareCircle_G17.ViewModels;

public partial class UserDonationViewModel : ObservableObject
{
    private readonly IDonationService _donationService;

    [ObservableProperty]
    private ObservableCollection<DonationItem> donations;

    [ObservableProperty]
    private bool isRefreshing;

    // Placeholder for the actual logged-in User ID (should be retrieved from Auth service later)
    private const string CurrentUserId = "default_user";

    public UserDonationViewModel(IDonationService donationService)
    {
        _donationService = (DonationService)donationService; // Assign to the interface
        Donations = new ObservableCollection<DonationItem>();
    }

    [RelayCommand]
    private async Task OpenOptions(DonationItem donation)
    {
        if (donation == null) return;

        string action = await Application.Current.MainPage.DisplayActionSheet(
            "Choose an option",
            "Cancel",
            null,
            "Edit",
            "Delete");

        if (action == "Edit")
        {
            await EditDonation(donation);
        }
        else if (action == "Delete")
        {
            await DeleteDonation(donation);
        }
    }

    [RelayCommand]
    public async Task LoadDonationsAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            var userDonations = await _donationService.GetUserDonationsAsync(CurrentUserId);

            // Update the observable collection on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Donations.Clear();
                foreach (var donation in userDonations)
                {
                    Donations.Add(donation);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading user donations: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to load donations.", "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task EditDonation(DonationItem donation)
    {
        if (donation == null) return;
        // Navigation to an edit page, passing the donation ID
        await Shell.Current.GoToAsync($"DonationEditPage?id={donation.Id}");
    }

    [RelayCommand]
    private async Task DeleteDonation(DonationItem donation)
    {
        if (donation == null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete the donation: {donation.Title}?",
            "Yes",
            "No");

        if (confirm)
        {
            await _donationService.DeleteDonationAsync(donation);
            Donations.Remove(donation); // Remove from the observable collection to update UI
        }
    }
}