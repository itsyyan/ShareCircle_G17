using Microsoft.Maui.Controls;
using ShareCircle_G17.ViewModels;
using System.Collections.Generic;

namespace ShareCircle_G17.Views
{
    public partial class DonationDetailPage : ContentPage, IQueryAttributable
    {
        private DonationPost _donation;

        public DonationDetailPage()
        {
            InitializeComponent();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Donation", out var donationObj) && donationObj is DonationPost donation)
            {
                _donation = donation;
                BindingContext = donation;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnRequestClicked(object sender, EventArgs e)
        {
            if (_donation == null)
                return;

            var email = string.IsNullOrWhiteSpace(_donation.ContactEmail) ? "the donor" : _donation.ContactEmail;
            await DisplayAlert("Request sent", $"We’ve notified {email}. They will contact you soon.", "OK");
        }
    }
}

