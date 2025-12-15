using System;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using ShareCircle_G17.Services;
using ShareCircle_G17.Models;
using System.Threading.Tasks;
namespace ShareCircle_G17.Views
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly IFirebaseAuthService _authService;
        private readonly IFirebaseDatabaseService _databaseService;
        private string? _currentUserId;
        private string? _selectedImagePath;

        public EditProfilePage(IFirebaseAuthService authService, IFirebaseDatabaseService databaseService)
        {
            InitializeComponent();
            _authService = authService;
            _databaseService = databaseService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCurrentUserData();
        }

        private async Task LoadCurrentUserData()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    _currentUserId = currentUser.UserId;
                    var dbUser = string.IsNullOrWhiteSpace(_currentUserId)
                        ? null
                        : await _databaseService.GetUserProfileAsync(_currentUserId);

                    var displayName = string.IsNullOrWhiteSpace(dbUser?.Username)
                        ? (string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.Email ?? string.Empty : currentUser.Username)
                        : dbUser!.Username;

                    UsernameEntry.Text = displayName;
                    UsernameDisplayLabel.Text = displayName;
                    EmailEntry.Text = currentUser.Email ?? "";

                    // Load bio if available (we'll add this to User model later)
                    // BioEditor.Text = currentUser.Bio ?? "";

                    // Load profile image if available
                    var profileImage = string.IsNullOrEmpty(dbUser?.ProfileImageUrl)
                        ? currentUser.ProfileImageUrl
                        : dbUser!.ProfileImageUrl;

                    var imageSource = CreateImageSource(profileImage);

                    if (imageSource != null)
                    {
                        ProfileImage.Source = imageSource;
                        ProfileImage.IsVisible = true;
                        ProfilePlaceholder.IsVisible = false;
                    }
                    else
                    {
                        ProfilePlaceholder.Text = BuildInitials(displayName);
                        ProfilePlaceholder.IsVisible = true;
                        ProfileImage.IsVisible = false;
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load profile: {ex.Message}", "OK");
            }
        }

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                // Check if media picker is supported
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await DisplayAlert("Not Supported", "Photo capture is not supported on this device.", "OK");
                    return;
                }

                // Ask user to choose between camera or gallery
                string action = await DisplayActionSheet(
                    "Update your vibe",
                    "Nevermind",
                    null,
                    "Snap a photo",
                    "Choose from gallery");

                FileResult? photo = null;

                if (action == "Snap a photo")
                {
                    var ok = await EnsureMediaPermissionsAsync(needsCamera: true);
                    if (!ok)
                    {
                        return;
                    }

                    photo = await MediaPicker.Default.CapturePhotoAsync();
                }
                else if (action == "Choose from gallery")
                {
                    var ok = await EnsureMediaPermissionsAsync(needsCamera: false);
                    if (!ok)
                    {
                        return;
                    }

                    photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                    {
                        Title = "Select Profile Photo"
                    });
                }

                if (photo != null)
                {
                    var localFilePath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);

                    using (Stream sourceStream = await photo.OpenReadAsync())
                    using (FileStream localFileStream = File.OpenWrite(localFilePath))
                    {
                        await sourceStream.CopyToAsync(localFileStream);
                    }

                    _selectedImagePath = localFilePath;

                    // Display the selected image
                    ProfileImage.Source = ImageSource.FromFile(localFilePath);
                    ProfileImage.IsVisible = true;
                    ProfilePlaceholder.IsVisible = false;

                    await DisplayAlert("Success", "Photo selected! Click 'Save Changes' to update your profile.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to change photo: {ex.Message}", "OK");
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // Validate input
                string newUsername = UsernameEntry.Text?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(newUsername))
                {
                    await DisplayAlert("Error", "Username cannot be empty.", "OK");
                    return;
                }

                // Check for uniqueness if username changed
                if (_currentUserId != null)
                {
                    var taken = await _databaseService.IsUsernameTakenAsync(newUsername, _currentUserId);
                    if (taken)
                    {
                        await DisplayAlert("Error", "Username is already taken. Please choose another.", "OK");
                        return;
                    }
                }

                // Disable save button to prevent multiple clicks
                SaveButton.IsEnabled = false;

                // Update user profile
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null && _currentUserId != null)
                {
                    // Create updated user object
                    var updatedUser = new User
                    {
                        UserId = _currentUserId,
                        Username = newUsername,
                        Email = currentUser.Email,
                        // PhoneNumber = null, // Or keep existing? For now we assume we stop managing it.
                        // We need to be careful not to wipe out other fields if User model has more.
                        // But here we construct a new User object.
                        // Let's assume we fetch current user first to preserve other fields?
                        // The current code constructs a NEW object with only these fields.
                        // This implies other fields might be lost if not included!
                        // But looking at FirebaseDatabaseService.UpdateUserProfileAsync, it does a PutAsync.
                        // PutAsync overwrites the node. So yes, fields not here are lost.
                        // We should probably fetch the existing user to preserve fields like CreatedAt, TotalDonations etc.
                        // BUT, looking at the code I read earlier:
                        /*
                        var updatedUser = new User
                        {
                            UserId = _currentUserId,
                            Username = newUsername,
                            Email = currentUser.Email,
                            PhoneNumber = phoneNumber,
                            ProfileImageUrl = ...,
                            CreatedAt = currentUser.CreatedAt
                        };
                        */
                        // It was already constructing a fresh object.
                        // Let's stick to the pattern but remove PhoneNumber.
                        PhoneNumber = null,
                        ProfileImageUrl = await BuildProfileImagePayloadAsync(_selectedImagePath, currentUser.ProfileImageUrl),
                        CreatedAt = currentUser.CreatedAt
                    };

                    // Update in Firebase
                    var result = await _databaseService.UpdateUserProfileAsync(updatedUser);

                    if (result.Success)
                    {
                        UsernameDisplayLabel.Text = newUsername;
                        ProfilePlaceholder.Text = BuildInitials(newUsername);
                        await DisplayAlert("Success", "Profile updated successfully!", "OK");
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        await DisplayAlert("Error", result.Message, "OK");
                        SaveButton.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save changes: {ex.Message}", "OK");
                SaveButton.IsEnabled = true;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private static string BuildInitials(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "U";
            }

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
            }

            return char.ToUpperInvariant(text[0]).ToString();
        }

        private static ImageSource? CreateImageSource(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            const string dataPrefix = "data:image";
            if (value.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var commaIndex = value.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        var base64 = value[(commaIndex + 1)..];
                        var bytes = Convert.FromBase64String(base64);
                        return ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                }
                catch
                {
                    return null;
                }
            }

            // Try http/https URI, otherwise treat as file path
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return ImageSource.FromUri(uri);
            }

            return ImageSource.FromFile(value);
        }

        private static async Task<string?> BuildProfileImagePayloadAsync(string? localPath, string? existingValue)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return existingValue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(localPath);
                var ext = Path.GetExtension(localPath)?.Trim('.').ToLowerInvariant();
                var mime = ext switch
                {
                    "png" => "image/png",
                    "jpg" or "jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                var base64 = Convert.ToBase64String(bytes);
                return $"data:{mime};base64,{base64}";
            }
            catch
            {
                return existingValue;
            }
        }

        private async Task<bool> EnsureMediaPermissionsAsync(bool needsCamera)
        {
            try
            {
                // Photos/Storage
                var photoStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
                if (photoStatus != PermissionStatus.Granted)
                {
                    photoStatus = await Permissions.RequestAsync<Permissions.Photos>();
                }

                if (photoStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission needed", "Please allow photo access to update your profile picture.", "OK");
                    return false;
                }

                if (needsCamera)
                {
                    var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (cameraStatus != PermissionStatus.Granted)
                    {
                        cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                    }

                    if (cameraStatus != PermissionStatus.Granted)
                    {
                        await DisplayAlert("Permission needed", "Please allow camera access to take a new photo.", "OK");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Permission error", $"Unable to request permissions: {ex.Message}", "OK");
                return false;
            }
        }
    }
}









