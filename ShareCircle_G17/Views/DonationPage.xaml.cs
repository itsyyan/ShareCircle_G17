using System;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;

namespace ShareCircle_G17.Views;

public partial class DonationPage : ContentPage
{
    // track current category selection
    private string _selectedCategory = null;

    public DonationPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        // Navigate back to previous page
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPostDonationClicked(object sender, EventArgs e)
    {
        // Placeholder action for posting a donation
        await DisplayAlert("Donation", "Donation posted (placeholder).", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private async void OnUploadPhotoTapped(object sender, EventArgs e)
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
            var imageData = ms.ToArray();

            // Use FindByName to avoid relying on generated fields
            var selectedImage = this.FindByName<Image>("SelectedImage");
            var placeholder = this.FindByName<Label>("UploadPlaceholderLabel");

            if (selectedImage != null)
            {
                selectedImage.Source = ImageSource.FromStream(() => new MemoryStream(imageData));
                selectedImage.IsVisible = true;
            }

            if (placeholder != null)
            {
                placeholder.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to pick image: {ex.Message}", "OK");
        }
    }

    private void OnFoodSelected(object sender, EventArgs e)
    {
        SetCategory("Food");
    }

    private void OnItemSelected(object sender, EventArgs e)
    {
        SetCategory("Item");
    }

    private void SetCategory(string category)
    {
        _selectedCategory = category;

        var foodFrame = this.FindByName<Frame>("FoodButton");
        var itemFrame = this.FindByName<Frame>("ItemButton");
        var foodLabel = this.FindByName<Label>("FoodButtonLabel");
        var itemLabel = this.FindByName<Label>("ItemButtonLabel");

        if (foodFrame == null || itemFrame == null || foodLabel == null || itemLabel == null)
            return;

        // reset both to white
        foodFrame.BackgroundColor = Colors.White;
        foodLabel.TextColor = Colors.Black;
        itemFrame.BackgroundColor = Colors.White;
        itemLabel.TextColor = Colors.Black;

        // highlight selected
        if (category == "Food")
        {
            foodFrame.BackgroundColor = Color.FromArgb("#5B2EFF");
            foodLabel.TextColor = Colors.White;
        }
        else if (category == "Item")
        {
            itemFrame.BackgroundColor = Color.FromArgb("#5B2EFF");
            itemLabel.TextColor = Colors.White;
        }
    }
}