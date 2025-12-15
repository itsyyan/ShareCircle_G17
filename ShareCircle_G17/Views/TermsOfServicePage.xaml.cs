namespace ShareCircle_G17.Views;

public partial class TermsOfServicePage : ContentPage
{
    public TermsOfServicePage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
