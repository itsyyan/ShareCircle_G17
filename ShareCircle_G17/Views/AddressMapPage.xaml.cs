using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace ShareCircle_G17.Views;

[QueryProperty(nameof(InitialAddress), "InitialAddress")]
[QueryProperty(nameof(InitialLatitude), "InitialLatitude")]
[QueryProperty(nameof(InitialLongitude), "InitialLongitude")]
[QueryProperty(nameof(ReturnDepth), "ReturnDepth")]
public partial class AddressMapPage : ContentPage
{
    private const string GoogleMapsApiKey = "AIzaSyDuBQk_oNkjHq2jXwMzhk_FBiZ7aQzY9_c";
    private readonly HttpClient _httpClient;

    private double _latitude;
    private double _longitude;
    private string? _address;
    private string? _postalCode;
    private string? _city;
    private string? _state;

    private bool _isMapReady;
    
    // Default return depth is 2 (AddAddress -> Search -> Map)
    private int _returnDepth = 2;

    // Selection step: 0 = State, 1 = City, 2 = Postal
    private int _currentStep = 0;

    // Malaysian States and Cities Data
    private static readonly Dictionary<string, List<string>> StateCities = new()
    {
        { "Johor", new List<string> { "Johor Bahru", "Batu Pahat", "Muar", "Kluang", "Pontian", "Segamat", "Kota Tinggi", "Kulai", "Mersing", "Tangkak" } },
        { "Kedah", new List<string> { "Alor Setar", "Sungai Petani", "Kulim", "Langkawi", "Jitra", "Baling", "Kuala Kedah", "Pendang", "Yan", "Pokok Sena" } },
        { "Kelantan", new List<string> { "Kota Bharu", "Pasir Mas", "Tanah Merah", "Machang", "Kuala Krai", "Gua Musang", "Tumpat", "Bachok", "Pasir Puteh", "Jeli" } },
        { "Kuala Lumpur", new List<string> { "Kuala Lumpur", "Cheras", "Kepong", "Setapak", "Wangsa Maju", "Titiwangsa", "Bukit Bintang", "Lembah Pantai", "Seputeh", "Bandar Tun Razak" } },
        { "Labuan", new List<string> { "Labuan", "Victoria" } },
        { "Melaka", new List<string> { "Melaka City", "Alor Gajah", "Jasin", "Masjid Tanah", "Merlimau", "Ayer Keroh" } },
        { "Negeri Sembilan", new List<string> { "Seremban", "Port Dickson", "Nilai", "Bahau", "Kuala Pilah", "Tampin", "Rembau", "Jelebu", "Gemencheh" } },
        { "Pahang", new List<string> { "Kuantan", "Temerloh", "Bentong", "Raub", "Jerantut", "Pekan", "Cameron Highlands", "Lipis", "Rompin", "Maran" } },
        { "Penang", new List<string> { "George Town", "Butterworth", "Bukit Mertajam", "Nibong Tebal", "Bayan Lepas", "Tanjung Bungah", "Air Itam", "Balik Pulau", "Kepala Batas", "Seberang Jaya" } },
        { "Perak", new List<string> { "Ipoh", "Taiping", "Teluk Intan", "Lumut", "Sitiawan", "Manjung", "Kuala Kangsar", "Batu Gajah", "Kampar", "Tapah" } },
        { "Perlis", new List<string> { "Kangar", "Arau", "Padang Besar", "Kuala Perlis" } },
        { "Putrajaya", new List<string> { "Putrajaya" } },
        { "Sabah", new List<string> { "Kota Kinabalu", "Sandakan", "Tawau", "Lahad Datu", "Keningau", "Beaufort", "Papar", "Ranau", "Semporna", "Kudat" } },
        { "Sarawak", new List<string> { "Kuching", "Miri", "Sibu", "Bintulu", "Limbang", "Sarikei", "Sri Aman", "Kapit", "Samarahan", "Kota Samarahan", "Mukah", "Betong", "Saratok", "Serian", "Simunjan", "Lundu", "Bau", "Dalat", "Daro", "Julau", "Kanowit", "Lawas", "Lubok Antu", "Marudi", "Meradong", "Pakan", "Pusa", "Roban", "Sebuyau", "Sebauh", "Selangau", "Siburan", "Song", "Subis", "Tanjung Manis", "Tatau", "Tebedu" } },
        { "Selangor", new List<string> { "Shah Alam", "Petaling Jaya", "Subang Jaya", "Klang", "Kajang", "Ampang", "Selayang", "Rawang", "Puchong", "Serdang", "Bangi", "Cyberjaya", "Sepang", "Kuala Selangor", "Sabak Bernam" } },
        { "Terengganu", new List<string> { "Kuala Terengganu", "Kemaman", "Dungun", "Marang", "Hulu Terengganu", "Besut", "Setiu" } }
    };

    // Postal codes by city (sample data for Sarawak cities)
    private static readonly Dictionary<string, List<string>> CityPostalCodes = new()
    {
        { "Sibu", new List<string> { "96000", "96007", "96008", "96009", "96010" } },
        { "Kuching", new List<string> { "93000", "93050", "93100", "93150", "93200", "93250", "93300", "93350", "93400", "93450" } },
        { "Miri", new List<string> { "98000", "98007", "98008", "98009", "98010" } },
        { "Bintulu", new List<string> { "97000", "97007", "97008", "97009", "97010" } },
        { "Sarikei", new List<string> { "96100", "96107", "96108" } },
        { "Sri Aman", new List<string> { "95000", "95007", "95008", "95009" } },
        { "Kapit", new List<string> { "96800", "96807", "96808" } },
        { "Limbang", new List<string> { "98700", "98707", "98708" } },
        { "Mukah", new List<string> { "96400", "96407", "96408" } },
        { "Betong", new List<string> { "95700", "95707", "95708" } },
        // Add default postal codes for other cities
    };

    public AddressMapPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    public string? ReturnDepth
    {
        set
        {
            if (int.TryParse(value, out var depth))
            {
                _returnDepth = depth;
            }
        }
    }

    public string? InitialAddress { get; set; }

    public string? InitialLatitude
    {
        set
        {
            if (double.TryParse(value, out var lat))
            {
                _latitude = lat;
            }
        }
    }

    public string? InitialLongitude
    {
        set
        {
            if (double.TryParse(value, out var lng))
            {
                _longitude = lng;
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        InitializeMap();
    }

    private void InitializeMap()
    {
        if (_latitude == 0 && _longitude == 0)
        {
            _latitude = 2.5;
            _longitude = 111.8;
        }

        var location = new Location(_latitude, _longitude);
        var mapSpan = MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.5));

        LocationMap.MoveToRegion(mapSpan);
        _isMapReady = true;

        if (!string.IsNullOrEmpty(InitialAddress))
        {
            _address = InitialAddress;
            AddressLabel.Text = InitialAddress;
            // Only fetch region details, don't overwrite address
            _ = ReverseGeocodeAsync(_latitude, _longitude, false);
        }
        else
        {
            _ = ReverseGeocodeAsync(_latitude, _longitude);
        }

        LocationMap.PropertyChanged += OnMapPropertyChanged;
    }

    private void OnMapPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Microsoft.Maui.Controls.Maps.Map.VisibleRegion) && _isMapReady)
        {
            var region = LocationMap.VisibleRegion;
            if (region != null)
            {
                // Only update coordinates, don't refresh address
                _latitude = region.Center.Latitude;
                _longitude = region.Center.Longitude;
            }
        }
    }

    private async Task ReverseGeocodeAsync(double latitude, double longitude, bool updateMainAddress = true)
    {
        try
        {
            if (updateMainAddress)
            {
                AddressLabel.Text = "Loading address...";
            }
            RegionLabel.Text = "Loading region...";

            string url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={GoogleMapsApiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (updateMainAddress)
                {
                    AddressLabel.Text = "Unable to get address";
                }
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var firstResult = results[0];

                if (updateMainAddress && firstResult.TryGetProperty("formatted_address", out var formattedAddress))
                {
                    _address = formattedAddress.GetString();
                }

                if (firstResult.TryGetProperty("address_components", out var components))
                {
                    string? streetNumber = null;
                    string? route = null;
                    string? sublocality = null;
                    string? locality = null;
                    string? adminArea1 = null;
                    string? postalCode = null;

                    foreach (var component in components.EnumerateArray())
                    {
                        if (!component.TryGetProperty("types", out var types))
                            continue;

                        var longName = component.GetProperty("long_name").GetString();

                        foreach (var type in types.EnumerateArray())
                        {
                            var typeStr = type.GetString();
                            switch (typeStr)
                            {
                                case "street_number":
                                    streetNumber = longName;
                                    break;
                                case "route":
                                    route = longName;
                                    break;
                                case "sublocality":
                                case "sublocality_level_1":
                                    sublocality = longName;
                                    break;
                                case "locality":
                                    locality = longName;
                                    break;
                                case "administrative_area_level_1":
                                    adminArea1 = longName;
                                    break;
                                case "postal_code":
                                    postalCode = longName;
                                    break;
                            }
                        }
                    }

                    if (updateMainAddress)
                    {
                        var streetParts = new List<string>();
                        if (!string.IsNullOrEmpty(streetNumber)) streetParts.Add(streetNumber);
                        if (!string.IsNullOrEmpty(route)) streetParts.Add(route);
                        if (streetParts.Count == 0 && !string.IsNullOrEmpty(sublocality)) streetParts.Add(sublocality);

                        var streetAddress = streetParts.Count > 0 ? string.Join(", ", streetParts) : _address;
                        AddressLabel.Text = streetAddress ?? "Unknown address";
                    }

                    _postalCode = postalCode;
                    _city = locality ?? sublocality;
                    _state = adminArea1;

                    UpdateRegionDisplay();
                }
            }
            else
            {
                if (updateMainAddress)
                {
                    AddressLabel.Text = "Address not found";
                }
                RegionLabel.Text = "Malaysia";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reverse geocode error: {ex.Message}");
            if (updateMainAddress)
            {
                AddressLabel.Text = "Unable to get address";
            }
            RegionLabel.Text = "Malaysia";
        }
    }

    private void UpdateRegionDisplay()
    {
        var regionParts = new List<string>();
        if (!string.IsNullOrEmpty(_postalCode)) regionParts.Add(_postalCode);
        if (!string.IsNullOrEmpty(_city)) regionParts.Add(_city);
        if (!string.IsNullOrEmpty(_state)) regionParts.Add(_state);

        RegionLabel.Text = regionParts.Count > 0 ? string.Join(", ", regionParts) : "Malaysia";
    }

    private void UpdateTabDisplay()
    {
        // Update tab labels with selected values
        TabStateLabel.Text = _state ?? "State";
        TabCityLabel.Text = _city ?? "City";
        TabPostalLabel.Text = _postalCode ?? "Postal";

        // Reset all underlines
        TabStateUnderline.BackgroundColor = Colors.Transparent;
        TabCityUnderline.BackgroundColor = Colors.Transparent;
        TabPostalUnderline.BackgroundColor = Colors.Transparent;

        // Highlight current step
        switch (_currentStep)
        {
            case 0:
                TabStateUnderline.BackgroundColor = Color.FromArgb("#111827");
                break;
            case 1:
                TabCityUnderline.BackgroundColor = Color.FromArgb("#111827");
                break;
            case 2:
                TabPostalUnderline.BackgroundColor = Color.FromArgb("#111827");
                break;
        }

        // Update popup title
        PopupTitleLabel.Text = _currentStep switch
        {
            0 => "Select State",
            1 => "Select City",
            2 => "Select Postal Code",
            _ => "Select State"
        };

        // Show/hide back button
        PopupBackButton.IsVisible = _currentStep > 0;

        // Show/hide alphabet index (only for city selection)
        AlphabetIndex.IsVisible = _currentStep == 1;
    }

    private void OnRegionTapped(object sender, EventArgs e)
    {
        _currentStep = 0;
        UpdateTabDisplay();
        PopulateStatesList();
        RegionPopupOverlay.IsVisible = true;
    }

    private void OnCloseRegionPopup(object sender, EventArgs e)
    {
        RegionPopupOverlay.IsVisible = false;
        UpdateRegionDisplay();
    }

    private void OnPopupBackClicked(object sender, EventArgs e)
    {
        if (_currentStep > 0)
        {
            _currentStep--;
            UpdateTabDisplay();

            if (_currentStep == 0)
                PopulateStatesList();
            else if (_currentStep == 1)
                PopulateCitiesList();
        }
    }

    private void OnStateTabTapped(object sender, EventArgs e)
    {
        _currentStep = 0;
        UpdateTabDisplay();
        PopulateStatesList();
    }

    private void OnCityTabTapped(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_state))
        {
            _currentStep = 1;
            UpdateTabDisplay();
            PopulateCitiesList();
        }
    }

    private void OnPostalTabTapped(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_city))
        {
            _currentStep = 2;
            UpdateTabDisplay();
            PopulatePostalCodesList();
        }
    }

    private void PopulateStatesList()
    {
        SelectionListContainer.Children.Clear();

        foreach (var state in StateCities.Keys.OrderBy(s => s))
        {
            var isSelected = state == _state;
            AddListItem(state, isSelected, () => OnStateItemSelected(state));
        }

        ListScrollView.ScrollToAsync(0, 0, false);
    }

    private void PopulateCitiesList()
    {
        SelectionListContainer.Children.Clear();
        AlphabetIndex.Children.Clear();

        if (string.IsNullOrEmpty(_state) || !StateCities.ContainsKey(_state))
            return;

        var cities = StateCities[_state].OrderBy(c => c).ToList();
        var groupedCities = cities.GroupBy(c => c[0].ToString().ToUpper()).OrderBy(g => g.Key);

        var letters = new HashSet<string>();

        foreach (var group in groupedCities)
        {
            // Add letter header
            var headerLabel = new Label
            {
                Text = group.Key,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#111827"),
                Padding = new Thickness(16, 12, 16, 4)
            };
            SelectionListContainer.Children.Add(headerLabel);
            letters.Add(group.Key);

            // Add cities in group
            foreach (var city in group)
            {
                var isSelected = city == _city;
                AddListItem(city, isSelected, () => OnCityItemSelected(city));
            }
        }

        // Populate alphabet index
        foreach (var letter in letters.OrderBy(l => l))
        {
            var letterLabel = new Label
            {
                Text = letter,
                FontSize = 11,
                TextColor = Color.FromArgb("#5B2EFF"),
                HorizontalOptions = LayoutOptions.Center
            };
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => ScrollToLetter(letter);
            letterLabel.GestureRecognizers.Add(tapGesture);
            AlphabetIndex.Children.Add(letterLabel);
        }

        ListScrollView.ScrollToAsync(0, 0, false);
    }

    private void ScrollToLetter(string letter)
    {
        // Find the header with this letter
        foreach (var child in SelectionListContainer.Children)
        {
            if (child is Label label && label.Text == letter && label.FontAttributes == FontAttributes.Bold)
            {
                ListScrollView.ScrollToAsync(label, ScrollToPosition.Start, true);
                break;
            }
        }
    }

    private void PopulatePostalCodesList()
    {
        SelectionListContainer.Children.Clear();

        if (string.IsNullOrEmpty(_city))
            return;

        List<string> postalCodes;

        if (CityPostalCodes.ContainsKey(_city))
        {
            postalCodes = CityPostalCodes[_city];
        }
        else
        {
            // Generate default postal codes based on state
            postalCodes = GenerateDefaultPostalCodes();
        }

        foreach (var postalCode in postalCodes)
        {
            var isSelected = postalCode == _postalCode;
            AddListItem(postalCode, isSelected, () => OnPostalCodeItemSelected(postalCode));
        }

        ListScrollView.ScrollToAsync(0, 0, false);
    }

    private List<string> GenerateDefaultPostalCodes()
    {
        // Generate 5 postal codes based on state prefix
        var prefix = _state switch
        {
            "Johor" => "80",
            "Kedah" => "05",
            "Kelantan" => "15",
            "Kuala Lumpur" => "50",
            "Labuan" => "87",
            "Melaka" => "75",
            "Negeri Sembilan" => "70",
            "Pahang" => "26",
            "Penang" => "10",
            "Perak" => "30",
            "Perlis" => "01",
            "Putrajaya" => "62",
            "Sabah" => "88",
            "Sarawak" => "93",
            "Selangor" => "40",
            "Terengganu" => "20",
            _ => "00"
        };

        return new List<string>
        {
            $"{prefix}000",
            $"{prefix}100",
            $"{prefix}200",
            $"{prefix}300",
            $"{prefix}400"
        };
    }

    private void AddListItem(string text, bool isSelected, Action onTap)
    {
        var itemGrid = new Grid
        {
            Padding = new Thickness(16, 14),
            BackgroundColor = Colors.Transparent
        };

        var itemLabel = new Label
        {
            Text = text,
            FontSize = 15,
            TextColor = isSelected ? Color.FromArgb("#EF4444") : Color.FromArgb("#111827"),
            VerticalOptions = LayoutOptions.Center
        };

        itemGrid.Children.Add(itemLabel);

        // Add separator
        var separator = new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 0, 0)
        };
        itemGrid.Children.Add(separator);

        // Add tap gesture
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => onTap();
        itemGrid.GestureRecognizers.Add(tapGesture);

        SelectionListContainer.Children.Add(itemGrid);
    }

    private void OnStateItemSelected(string state)
    {
        _state = state;
        _city = null;
        _postalCode = null;
        _currentStep = 1;
        UpdateTabDisplay();
        PopulateCitiesList();
    }

    private void OnCityItemSelected(string city)
    {
        _city = city;
        _postalCode = null;
        _currentStep = 2;
        UpdateTabDisplay();
        PopulatePostalCodesList();
    }

    private void OnPostalCodeItemSelected(string postalCode)
    {
        _postalCode = postalCode;
        UpdateTabDisplay();
        UpdateRegionDisplay();
        RegionPopupOverlay.IsVisible = false;
    }

    private async void OnMyLocationClicked(object sender, EventArgs e)
    {
        try
        {
            MyLocationButton.IsEnabled = false;

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                if (location != null)
                {
                    _latitude = location.Latitude;
                    _longitude = location.Longitude;

                    var mapLocation = new Location(_latitude, _longitude);
                    var mapSpan = MapSpan.FromCenterAndRadius(mapLocation, Distance.FromKilometers(0.5));
                    LocationMap.MoveToRegion(mapSpan);

                    await ReverseGeocodeAsync(_latitude, _longitude);
                }
            }
            else
            {
                await DisplayAlert("Permission Denied", "Location permission is required.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not get location: {ex.Message}", "OK");
        }
        finally
        {
            MyLocationButton.IsEnabled = true;
        }
    }

    private async void OnEditAddressTapped(object sender, EventArgs e)
    {
        // Navigate back to address search page to select a different address
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Text = "Saving...";

            var confirmedAddress = new AddressPrediction
            {
                PrimaryText = AddressLabel.Text,
                SecondaryText = RegionLabel.Text,
                FullAddress = _address ?? AddressLabel.Text,
                Latitude = _latitude,
                Longitude = _longitude
            };

            var navigationParameter = new Dictionary<string, object>
            {
                { "SelectedAddress", confirmedAddress }
            };

            // Navigate back based on depth
            string route = "..";
            if (_returnDepth > 1)
            {
                for (int i = 1; i < _returnDepth; i++)
                {
                    route += "/..";
                }
            }

            await Shell.Current.GoToAsync(route, navigationParameter);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "Save";
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocationMap.PropertyChanged -= OnMapPropertyChanged;
    }
}
