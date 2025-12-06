using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;
using System.Collections.Generic; // Required for IQueryAttributable

namespace ShareCircle_G17.ViewModels
{
    // 1. Implement IQueryAttributable to receive the ID from the navigation URI
    [QueryProperty(nameof(DonationId), "id")]
    public class DonationEditViewModel : INotifyPropertyChanged, IQueryAttributable
    {
        private readonly IDonationService _donationService; // Need this to fetch old data
        private readonly DonationSyncService _donationSyncService;

        // Private backing fields for data
        private byte[] _imageData;
        private DonationItem _originalDonation; // Stores the original item (with ID, FirebaseId, etc.)
        private int _donationId;

        // View Model Properties (same as DonationViewModel)
        private string _selectedCategory;
        private ImageSource _selectedImageSource;
        private bool _isImageVisible;
        private string _title;
        private string _description;
        private string _contactEmail;
        private string _location;

        public DonationEditViewModel(IDonationService donationService, DonationSyncService donationSyncService)
        {
            _donationService = donationService;
            _donationSyncService = donationSyncService;
            InitializeCommands();
        }

        // --- Navigation and Data Loading ---

        public int DonationId
        {
            get => _donationId;
            set
            {
                if (_donationId != value)
                {
                    _donationId = value;
                    OnPropertyChanged();
                    // Automatically load data when the ID is set via navigation
                    LoadDonationDataAsync(value);
                }
            }
        }

        // 2. IQueryAttributable Interface Implementation (Mandatory for CS0535 fix)
        public void ApplyQueryAttributes(IDictionary<string, object> queryAttributes)
        {
            // The [QueryProperty] attribute handles assigning the 'id' to the DonationId property.
            // We just need this method here to satisfy the interface contract.
        }

        private async Task LoadDonationDataAsync(int id)
        {
            try
            {
                var item = await _donationService.GetDonationAsync(id);

                if (item == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Donation post not found.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // Store the original item (holds ID, FirebaseId, etc.)
                _originalDonation = item;

                // Populate UI properties with existing data
                Title = item.Title;
                Description = item.Description;
                ContactEmail = item.ContactEmail;
                Location = item.Location;
                SelectedCategory = item.Category;
                _imageData = item.ImageData;

                if (item.ImageData != null && item.ImageData.Length > 0)
                {
                    SelectedImageSource = ImageSource.FromStream(() => new MemoryStream(item.ImageData));
                    IsImageVisible = true;
                }
                else
                {
                    IsImageVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading donation data: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to load data for editing.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        // --- Commands and Actions ---

        // NEW: Handles the Save Changes button
        public ICommand SaveChangesCommand { get; private set; }
        // NEW: Handles the Delete Post button
        public ICommand DeletePostCommand { get; private set; }

        private void InitializeCommands()
        {
            // Existing commands
            BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
            UploadPhotoCommand = new Command(async () => await UploadPhotoAsync());
            SelectFoodCommand = new Command(() => SelectedCategory = "Food");
            SelectItemCommand = new Command(() => SelectedCategory = "Item");

            // NEW: Command implementation for editing/deleting
            SaveChangesCommand = new Command(async () => await SaveChangesAsync());
            DeletePostCommand = new Command(async () => await DeletePostAsync());
        }

        private async Task SaveChangesAsync()
        {
            if (_originalDonation == null) return;

            // 1. Validation (Use existing validation logic if needed)

            // 2. Update the stored DonationItem model with current UI values
            _originalDonation.Title = Title;
            _originalDonation.Category = SelectedCategory;
            _originalDonation.Description = Description;
            _originalDonation.ContactEmail = ContactEmail;
            _originalDonation.Location = Location;
            _originalDonation.ImageData = _imageData;

            try
            {
                // Use the dedicated sync method for updates (isEditingExisting: true)
                await _donationSyncService.SaveOrUpdateDonationAndSyncAsync(_originalDonation, isEditingExisting: true);

                await Shell.Current.DisplayAlert("Success", "Donation updated successfully!", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving donation: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to update donation.", "OK");
            }
        }

        private async Task DeletePostAsync()
        {
            if (_originalDonation == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete the post: {_originalDonation.Title}?",
                "Yes",
                "No");

            if (confirm)
            {
                try
                {
                    // Use the dedicated sync method for deletion
                    await _donationSyncService.DeleteDonationAndSyncAsync(_originalDonation);

                    await Shell.Current.DisplayAlert("Deleted", "Post deleted successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting donation: {ex.Message}");
                    await Shell.Current.DisplayAlert("Error", "Failed to delete donation.", "OK");
                }
            }
        }

        // --- Copied Properties and Methods from DonationViewModel ---

        // ... (Paste all the existing Public Properties here: SelectedCategory, SelectedImageSource, 
        // IsImageVisible, Title, Description, ContactEmail, Location) ...

        // Properties
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    UpdateCategoryProperties();
                }
            }
        }

        public ImageSource SelectedImageSource
        {
            get => _selectedImageSource;
            set
            {
                if (_selectedImageSource != value)
                {
                    _selectedImageSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsImageVisible
        {
            get => _isImageVisible;
            set
            {
                if (_isImageVisible != value)
                {
                    _isImageVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowPlaceHolder));
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ContactEmail
        {
            get => _contactEmail;
            set
            {
                if (_contactEmail != value)
                {
                    _contactEmail = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Location
        {
            get => _location;
            set
            {
                if (_location != value)
                {
                    _location = value;
                    OnPropertyChanged();
                }
            }
        }

        // UI Bindings
        public bool IsFoodSelected => SelectedCategory == "Food";
        public bool IsItemSelected => SelectedCategory == "Item";
        public Color FoodButtonColor => IsFoodSelected ? Color.FromArgb("#5B2EFF") : Colors.White;
        public Color ItemButtonColor => IsItemSelected ? Color.FromArgb("#5B2EFF") : Colors.White;
        public Color FoodTextColor => IsFoodSelected ? Colors.White : Color.FromArgb("#222222");
        public Color ItemTextColor => IsItemSelected ? Colors.White : Color.FromArgb("#222222");
        public bool ShowPlaceHolder => !IsImageVisible;

        // Commands
        public ICommand BackCommand { get; private set; }
        public ICommand UploadPhotoCommand { get; private set; }
        public ICommand SelectFoodCommand { get; private set; }
        public ICommand SelectItemCommand { get; private set; }


        // --- Copied Helper Methods ---
        private async Task UploadPhotoAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a photo",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null)
                    return;

                using var stream = await result.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                _imageData = ms.ToArray();

                SelectedImageSource = ImageSource.FromStream(() => new MemoryStream(_imageData));
                IsImageVisible = true;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Unable to pick image: {ex.Message}", "OK");
            }
        }

        private void UpdateCategoryProperties()
        {
            OnPropertyChanged(nameof(IsFoodSelected));
            OnPropertyChanged(nameof(IsItemSelected));
            OnPropertyChanged(nameof(FoodButtonColor));
            OnPropertyChanged(nameof(ItemButtonColor));
            OnPropertyChanged(nameof(FoodTextColor));
            OnPropertyChanged(nameof(ItemTextColor));
        }


        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}