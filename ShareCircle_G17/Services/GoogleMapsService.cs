using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShareCircle_G17.Services
{
    public interface IGoogleMapsService
    {
        Task<string?> GetAddressFromCoordinatesAsync(double latitude, double longitude, string? language = null);
        Task<(double Latitude, double Longitude, string? Address)?> GetCoordinatesFromAddressAsync(string address, string? language = null);
    }

    public class GoogleMapsService : IGoogleMapsService
    {
        private const string GoogleMapsApiKey = "AIzaSyDuBQk_oNkjHq2jXwMzhk_FBiZ7aQzY9_c";
        private readonly HttpClient _httpClient;

        public GoogleMapsService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string?> GetAddressFromCoordinatesAsync(double latitude, double longitude, string? language = null)
        {
            try
            {
                var languageParam = string.IsNullOrWhiteSpace(language) ? "" : $"&language={language}";
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}{languageParam}&key={GoogleMapsApiKey}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                // Check if we got results
                if (jsonDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    // Iterate through results to find one with City/Locality info
                    foreach (var result in results.EnumerateArray())
                    {
                        string? city = null;
                        string? region = null;

                        if (result.TryGetProperty("address_components", out var components))
                        {
                            foreach (var component in components.EnumerateArray())
                            {
                                if (!component.TryGetProperty("types", out var types))
                                    continue;

                                foreach (var type in types.EnumerateArray())
                                {
                                    var typeStr = type.GetString();
                                    if (typeStr == "locality" || typeStr == "sublocality" || typeStr == "administrative_area_level_2")
                                    {
                                        city ??= component.GetProperty("long_name").GetString();
                                    }
                                    else if (typeStr == "administrative_area_level_1")
                                    {
                                        region ??= component.GetProperty("long_name").GetString();
                                    }
                                    else if (typeStr == "country" && region == null)
                                    {
                                        region = component.GetProperty("long_name").GetString();
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(city))
                        {
                            return !string.IsNullOrWhiteSpace(region) ? $"{city}, {region}" : city;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting address from coordinates: {ex.Message}");
                return null;
            }
        }

        public async Task<(double Latitude, double Longitude, string? Address)?> GetCoordinatesFromAddressAsync(string address, string? language = null)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            try
            {
                string encodedAddress = Uri.EscapeDataString(address);
                var languageParam = string.IsNullOrWhiteSpace(language) ? "" : $"&language={language}";
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}{languageParam}&key={GoogleMapsApiKey}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var firstResult = results[0];
                    double lat = 0;
                    double lng = 0;
                    string? formattedAddress = null;

                    if (firstResult.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("location", out var location))
                    {
                        lat = location.GetProperty("lat").GetDouble();
                        lng = location.GetProperty("lng").GetDouble();
                    }

                    if (firstResult.TryGetProperty("formatted_address", out var addressElement))
                    {
                        formattedAddress = addressElement.GetString();
                    }

                    return (lat, lng, formattedAddress);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting coordinates from address: {ex.Message}");
                return null;
            }
        }
    }
}
