using Microsoft.Maui.Controls;
using ShareCircle_G17.ViewModels;
using System.Collections.Generic;

namespace ShareCircle_G17.Views
{
    public partial class CommunityPage : ContentPage, IQueryAttributable
    {
        private readonly CommunityViewModel _viewModel;

        public CommunityPage(CommunityViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadAsync();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            // Navigate back to previous page
            await Shell.Current.GoToAsync("..");
        }

        private async void OnRequestClicked(object sender, TappedEventArgs e)
        {
            if (e.Parameter is DonationPost donation)
            {
                await Shell.Current.GoToAsync(nameof(DonationDetailPage), true, new Dictionary<string, object>
                {
                    { "Donation", donation }
                });
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            query.TryGetValue("categoryType", out var categoryObj);
            query.TryGetValue("subcategory", out var subCategoryObj);

            var category = categoryObj?.ToString();
            var subCategory = subCategoryObj?.ToString();

            _viewModel.SetFilters(category, subCategory);
        }
    }
}

