using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;

namespace ShareCircle_G17.Views;

public class AddressPrediction
{
    public string? PlaceId { get; set; }
    public string? PrimaryText { get; set; }
    public string? SecondaryText { get; set; }
    public string? FullAddress { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

[QueryProperty(nameof(InitialQuery), "InitialQuery")]
public partial class AddressSearchPage : ContentPage
{
    private const string GoogleMapsApiKey = "AIzaSyDuBQk_oNkjHq2jXwMzhk_FBiZ7aQzY9_c";
    private readonly HttpClient _httpClient;
    private readonly ObservableCollection<AddressPrediction> _predictions;
    private CancellationTokenSource? _searchCancellation;

    public AddressSearchPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _predictions = new ObservableCollection<AddressPrediction>();
        ResultsCollectionView.ItemsSource = _predictions;
    }

    public string? InitialQuery { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (!string.IsNullOrEmpty(InitialQuery))
        {
            SearchEntry.Text = InitialQuery;
            // Clear it so it doesn't re-trigger on back navigation
            InitialQuery = null; 
        }
        else if (string.IsNullOrEmpty(SearchEntry.Text))
        {
            SearchEntry.Focus();
            _ = LoadNearbyPlacesAsync();
        }
    }

    private async Task LoadNearbyPlacesAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
                return;

            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(5)
            });

            if (location == null)
                return;

            // Use Google Places Nearby Search API
            string url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={location.Latitude},{location.Longitude}&radius=500&key={GoogleMapsApiKey}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                _predictions.Clear();

                if (jsonDoc.RootElement.TryGetProperty("results", out var results))
                {
                    foreach (var place in results.EnumerateArray())
                    {
                        var name = place.GetProperty("name").GetString();
                        var vicinity = place.TryGetProperty("vicinity", out var v) ? v.GetString() : "";
                        var placeId = place.GetProperty("place_id").GetString();
                        
                        // Extract geometry if needed for instant selection
                        double lat = 0, lng = 0;
                        if (place.TryGetProperty("geometry", out var geo) && 
                            geo.TryGetProperty("location", out var loc))
                        {
                            lat = loc.GetProperty("lat").GetDouble();
                            lng = loc.GetProperty("lng").GetDouble();
                        }

                        _predictions.Add(new AddressPrediction
                        {
                            PlaceId = placeId,
                            PrimaryText = name,
                            SecondaryText = vicinity,
                            FullAddress = $"{name}, {vicinity}",
                            Latitude = lat,
                            Longitude = lng
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading nearby places: {ex.Message}");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            NoResultsLabel.IsVisible = _predictions.Count == 0;
            NearbyLabel.IsVisible = _predictions.Count > 0;
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue;

        // Show/hide clear button
        ClearButton.IsVisible = !string.IsNullOrEmpty(query);

        // Cancel previous search
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(query))
        {
            // If cleared, show nearby again
            NearbyLabel.Text = "Nearby Addresses";
            _ = LoadNearbyPlacesAsync();
            return;
        }
        
        // If typing, change label to "Search Results" or hide it
        NearbyLabel.Text = "Search Results";

        if (query.Length < 2)
        {
            // Don't clear if we are reverting to nearby, but if we are just starting to type...
            // Actually, wait for debounce.
            return;
        }

        // Debounce - wait 300ms before searching
        try
        {
            await Task.Delay(300, _searchCancellation.Token);
            await SearchAddressesAsync(query, _searchCancellation.Token);
        }
        catch (TaskCanceledException)
        {
            // Search was cancelled, ignore
        }
    }

    private async Task SearchAddressesAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            NoResultsLabel.IsVisible = false;

            // Use Google Places Autocomplete API
            string encodedQuery = Uri.EscapeDataString(query);
            string url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={encodedQuery}&components=country:my&key={GoogleMapsApiKey}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            _predictions.Clear();

            if (jsonDoc.RootElement.TryGetProperty("predictions", out var predictions))
            {
                foreach (var prediction in predictions.EnumerateArray())
                {
                    var placeId = prediction.GetProperty("place_id").GetString();
                    var description = prediction.GetProperty("description").GetString();

                    string? primaryText = description;
                    string? secondaryText = "Malaysia";

                    // Try to get structured formatting
                    if (prediction.TryGetProperty("structured_formatting", out var formatting))
                    {
                        if (formatting.TryGetProperty("main_text", out var mainText))
                        {
                            primaryText = mainText.GetString();
                        }
                        if (formatting.TryGetProperty("secondary_text", out var secText))
                        {
                            secondaryText = secText.GetString();
                        }
                    }

                    _predictions.Add(new AddressPrediction
                    {
                        PlaceId = placeId,
                        PrimaryText = primaryText,
                        SecondaryText = secondaryText,
                        FullAddress = description
                    });
                }
            }

            NoResultsLabel.IsVisible = _predictions.Count == 0;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error searching addresses: {ex.Message}");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async Task<(double Lat, double Lng)?> GetPlaceDetailsAsync(string placeId)
    {
        try
        {
            string url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&fields=geometry&key={GoogleMapsApiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("geometry", out var geometry) &&
                geometry.TryGetProperty("location", out var location))
            {
                var lat = location.GetProperty("lat").GetDouble();
                var lng = location.GetProperty("lng").GetDouble();
                return (lat, lng);
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting place details: {ex.Message}");
            return null;
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        // User pressed Enter - select first result if available
        if (_predictions.Count > 0)
        {
            await SelectAddressAsync(_predictions[0]);
        }
    }

    private void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        // Not used - using tap gesture instead
    }

    private async void OnAddressTapped(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is AddressPrediction prediction)
        {
            await SelectAddressAsync(prediction);
        }
    }

    private async Task SelectAddressAsync(AddressPrediction prediction)
    {
        try
        {
            // Get coordinates from place ID
            if (!string.IsNullOrEmpty(prediction.PlaceId))
            {
                var coords = await GetPlaceDetailsAsync(prediction.PlaceId);
                if (coords.HasValue)
                {
                    prediction.Latitude = coords.Value.Lat;
                    prediction.Longitude = coords.Value.Lng;
                }
            }

            // Navigate to map page to confirm location
            var navigationParameter = new Dictionary<string, object>
            {
                { "InitialAddress", prediction.FullAddress ?? prediction.PrimaryText ?? "" },
                { "InitialLatitude", prediction.Latitude.ToString() },
                { "InitialLongitude", prediction.Longitude.ToString() },
                { "ReturnDepth", "2" } // Go back 2 steps (to AddAddressPage)
            };

            await Shell.Current.GoToAsync(nameof(AddressMapPage), navigationParameter);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select address: {ex.Message}", "OK");
        }
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        SearchEntry.Text = string.Empty;
        SearchEntry.Focus();
        _ = LoadNearbyPlacesAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
