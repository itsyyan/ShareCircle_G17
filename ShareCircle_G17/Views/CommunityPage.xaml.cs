using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;

namespace ShareCircle_G17.Views
{
    public partial class CommunityPage : ContentPage
    {
        public ObservableCollection<DonationPost> Donations { get; set; }

        public CommunityPage()
        {
            InitializeComponent();
            LoadDonations();
            BindingContext = this;
        }

        private void LoadDonations()
        {
            // Initialize with sample data
            // In a real app, this would load from a database or API
            Donations = new ObservableCollection<DonationPost>
            {
                new DonationPost
                {
                    Username = "Sarah",
                    ProfileImageUrl = "https://t4.ftcdn.net/jpg/04/31/64/75/360_F_431647519_usrbQ8Z983hTYe8zgA7t1XVc5fEtqcpa.jpg",
                    Location = "Sibu, Sarawak",
                    Category = "Food",
                    ItemTitle = "Fresh Fruits Basket",
                    Description = "A variety of fresh fruits including apples, oranges, and bananas. All in good condition!",
                    ImageUrl = "https://images.unsplash.com/photo-1610832958506-aa56368176cf?w=400",
                    ContactEmail = "sarah@example.com",
                    DropOffLocation = "Central Market"
                },
                new DonationPost
                {
                    Username = "John",
                    ProfileImageUrl = "https://t4.ftcdn.net/jpg/04/31/64/75/360_F_431647519_usrbQ8Z983hTYe8zgA7t1XVc5fEtqcpa.jpg",
                    Location = "Kuching, Sarawak",
                    Category = "Item",
                    ItemTitle = "Children's Books Collection",
                    Description = "Gently used children's books suitable for ages 5-10. Great for learning and reading!",
                    ImageUrl = "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400",
                    ContactEmail = "john@example.com",
                    DropOffLocation = "Library Main Branch"
                },
                new DonationPost
                {
                    Username = "Maria",
                    ProfileImageUrl = "https://t4.ftcdn.net/jpg/04/31/64/75/360_F_431647519_usrbQ8Z983hTYe8zgA7t1XVc5fEtqcpa.jpg",
                    Location = "Miri, Sarawak",
                    Category = "Food",
                    ItemTitle = "Homemade Bread",
                    Description = "Freshly baked bread and pastries. Made with love and care!",
                    ImageUrl = "https://images.unsplash.com/photo-1509440159596-0249088772ff?w=400",
                    ContactEmail = "maria@example.com",
                    DropOffLocation = "Community Center"
                }
            };
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Navigate back to previous page
            await Shell.Current.GoToAsync("..");
        }

        private async void OnRequestClicked(object sender, TappedEventArgs e)
        {
            // Get the donation post from the CommandParameter
            if (e.Parameter is DonationPost donation)
            {
                // TODO: Implement request functionality
                await DisplayAlert("Request", $"Request sent for {donation.ItemTitle}", "OK");
            }
        }
    }

    // Simple model class for donation posts
    public class DonationPost
    {
        public string Username { get; set; }
        public string ProfileImageUrl { get; set; }
        public string Location { get; set; }
        public string Category { get; set; }
        public string ItemTitle { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string ContactEmail { get; set; }
        public string DropOffLocation { get; set; }
    }
}

