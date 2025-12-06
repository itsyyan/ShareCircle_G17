using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Networking;
using ShareCircle_G17.Models;
using ShareCircle_G17.Services;

namespace ShareCircle_G17.ViewModels
{
    public class DonationViewModel : INotifyPropertyChanged
    {
        private readonly DonationSyncService _donationSyncService;
        private byte[] _imageData;
        private string _selectedCategory;
        private ImageSource _selectedImageSource;
        private bool _isImageVisible;
        private string _title;
        private string _description;
        private string _contactEmail;
        private string _location;

        public DonationViewModel(DonationSyncService donationSyncService)
        {
            _donationSyncService = donationSyncService;
            InitializeCommands();
        }

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
        public ICommand PostDonationCommand { get; private set; }
        public ICommand UploadPhotoCommand { get; private set; }
        public ICommand SelectFoodCommand { get; private set; }
        public ICommand SelectItemCommand { get; private set; }

        private void InitializeCommands()
        {
            BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
            PostDonationCommand = new Command(async () => await PostDonationAsync());
            UploadPhotoCommand = new Command(async () => await UploadPhotoAsync());
            SelectFoodCommand = new Command(() => SelectedCategory = "Food");
            SelectItemCommand = new Command(() => SelectedCategory = "Item");
        }

        private async Task PostDonationAsync()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Title))
            {
                await Shell.Current.DisplayAlert("Error", "Please describe what the item is.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedCategory))
            {
                await Shell.Current.DisplayAlert("Error", "Please select a category.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(ContactEmail))
            {
                await Shell.Current.DisplayAlert("Error", "Please enter your contact email.", "OK");
                return;
            }

            try
            {
                var donationItem = new DonationItem
                {
                    Title = Title,
                    Category = SelectedCategory,
                    Description = Description,
                    ContactEmail = ContactEmail,
                    Location = Location,
                    ImageData = _imageData,
                    CreatedDate = DateTime.Now
                };

                var canSyncImmediately = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
                var result = await _donationSyncService.SaveDonationAsync(donationItem, canSyncImmediately);

                if (result > 0)
                {
                    var message = canSyncImmediately
                        ? "Donation posted successfully!"
                        : "Donation saved offline. It will sync when you're back online.";

                    await Shell.Current.DisplayAlert("Success", message, "OK");
                    await ClearForm();
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to save donation: {ex.Message}", "OK");
            }
        }

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

        private async Task ClearForm()
        {
            Title = string.Empty;
            Description = string.Empty;
            ContactEmail = string.Empty;
            Location = string.Empty;
            SelectedCategory = string.Empty;
            _imageData = null;
            IsImageVisible = false;
            SelectedImageSource = null;

            // Update all properties
            UpdateCategoryProperties();
            OnPropertyChanged(nameof(ShowPlaceHolder));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
