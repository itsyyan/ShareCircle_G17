using Microsoft.Maui.Controls;
using ShareCircle_G17.ViewModels;

namespace ShareCircle_G17.Views;
public partial class UserDonationPage : ContentPage
{
    private readonly UserDonationViewModel _viewModel;

    // Use Dependency Injection (recommended) or pass the VM manually
    public UserDonationPage(UserDonationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // Load data when the page is displayed
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDonationsAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}