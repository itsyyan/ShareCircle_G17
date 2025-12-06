using Microsoft.Maui.Controls;
using ShareCircle_G17.ViewModels;

namespace ShareCircle_G17.Views;

// The partial class for the XAML page
public partial class DonationEditPage : ContentPage
{
    // The constructor automatically receives the ViewModel via Dependency Injection (DI).
    public DonationEditPage(DonationEditViewModel viewModel)
    {
        InitializeComponent();

        // The ViewModel is set as the BindingContext, allowing the XAML fields to load and update data.
        BindingContext = viewModel;
    }
}