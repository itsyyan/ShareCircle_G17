using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.ViewModels
{
    public class CommunityViewModel : INotifyPropertyChanged
    {
        private readonly IDonationService _donationService;
        private readonly FirebaseService _firebaseService;
        private readonly DonationSyncService _donationSyncService;
        private bool _isBusy;
        private string? _activeCategoryType;
        private string? _activeSubCategory;

        public ObservableCollection<DonationPost> Donations { get; } = new();

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        public CommunityViewModel(IDonationService donationService, FirebaseService firebaseService, DonationSyncService donationSyncService)
        {
            _donationService = donationService;
            _firebaseService = firebaseService;
            _donationSyncService = donationSyncService;
        }

        public void SetFilters(string? categoryType, string? subCategory)
        {
            _activeCategoryType = Normalize(categoryType);
            _activeSubCategory = Normalize(subCategory);
        }

        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var combined = new List<DonationPost>();
                var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

                if (isOnline)
                {
                    await _donationSyncService.SyncPendingDonationsAsync();
                }

                var local = await _donationService.GetDonationsAsync();
                combined.AddRange(local.Select(MapLocalDonation));

                if (isOnline)
                {
                    try
                    {
                        var cloud = await _firebaseService.GetDonationsAsync();
                        combined.AddRange(cloud
                            .Where(HasContent)
                            .Select(MapCloudDonation));
                    }
                    catch
                    {
                        // Ignore remote errors and fall back to local cache.
                    }
                }

                var filtered = combined
                    .Where(MatchesFilter)
                    .OrderByDescending(post => post.CreatedDate);

                Donations.Clear();
                foreach (var post in filtered)
                {
                    Donations.Add(post);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool MatchesFilter(DonationPost post)
        {
            if (_activeCategoryType != null &&
                !string.Equals(Normalize(post.CategoryType), _activeCategoryType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_activeSubCategory != null &&
                !string.Equals(Normalize(post.SubCategory), _activeSubCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private DonationPost MapLocalDonation(DonationItem item)
        {
            var imageSource = item.ImageData != null && item.ImageData.Length > 0
                ? ImageSource.FromStream(() => new MemoryStream(item.ImageData))
                : ImageSource.FromFile("comm.png");

            return new DonationPost
            {
                Id = $"local-{item.Id}",
                Username = "Community Donor",
                ProfileImage = ImageSource.FromFile("face.png"),
                Location = item.Location ?? "Unknown",
                Category = item.Category ?? "General",
                CategoryType = item.Category,
                SubCategory = item.Category, // SQLite currently stores only top-level category
                ItemTitle = item.Title ?? "Donation",
                Description = item.Description ?? string.Empty,
                DonationImage = imageSource,
                ContactEmail = item.ContactEmail ?? string.Empty,
                DropOffLocation = item.Location ?? string.Empty,
                CreatedDate = item.CreatedDate
            };
        }

        private DonationPost MapCloudDonation(FirebaseDonation donation)
        {
            ImageSource donationImage = ImageSource.FromFile("comm.png");

            if (!string.IsNullOrWhiteSpace(donation.ProductImageUrl) &&
                Uri.TryCreate(donation.ProductImageUrl, UriKind.Absolute, out var imageUri))
            {
                donationImage = ImageSource.FromUri(imageUri);
            }

            return new DonationPost
            {
                Id = donation.FirebaseId ?? Guid.NewGuid().ToString(),
                Username = string.IsNullOrWhiteSpace(donation.UserId) ? "Donor" : donation.UserId,
                ProfileImage = ImageSource.FromFile("face.png"),
                Location = donation.DropOffLocation ?? "Unknown",
                Category = FormatCategoryLabel(donation.CategoryType, donation.SubCategory),
                CategoryType = donation.CategoryType,
                SubCategory = donation.SubCategory,
                ItemTitle = donation.ItemName ?? "Donation",
                Description = donation.Description ?? string.Empty,
                DonationImage = donationImage,
                ContactEmail = donation.ContactEmail ?? string.Empty,
                DropOffLocation = donation.DropOffLocation ?? string.Empty,
                CreatedDate = donation.CreatedAt == default ? DateTime.UtcNow : donation.CreatedAt
            };
        }

        private static string FormatCategoryLabel(string? categoryType, string? subCategory)
        {
            if (!string.IsNullOrWhiteSpace(subCategory))
                return Capitalize(subCategory);

            if (!string.IsNullOrWhiteSpace(categoryType))
                return Capitalize(categoryType);

            return "General";
        }

        private static string Capitalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var lower = value.ToLowerInvariant();
            return char.ToUpper(lower[0]) + lower[1..];
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        }

        private static bool HasContent(FirebaseDonation donation)
        {
            return !(string.IsNullOrWhiteSpace(donation.ItemName)
                     && string.IsNullOrWhiteSpace(donation.Description)
                     && string.IsNullOrWhiteSpace(donation.CategoryType)
                     && string.IsNullOrWhiteSpace(donation.SubCategory));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DonationPost
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = "Donor";
        public ImageSource ProfileImage { get; set; } = ImageSource.FromFile("face.png");
        public string Location { get; set; } = "Unknown";
        public string Category { get; set; } = "General";
        public string? CategoryType { get; set; }
        public string? SubCategory { get; set; }
        public string ItemTitle { get; set; } = "Donation";
        public string Description { get; set; } = string.Empty;
        public ImageSource DonationImage { get; set; } = ImageSource.FromFile("comm.png");
        public string ContactEmail { get; set; } = string.Empty;
        public string DropOffLocation { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}


