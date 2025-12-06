using System.Net.Http.Json;
using System.Text.Json;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public class FirebaseService
    {
        private readonly string _firebaseBaseUrl;

        private readonly HttpClient _httpClient;

        public FirebaseService(string firebaseBaseUrl, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(firebaseBaseUrl))
                throw new ArgumentException("Firebase base URL must be provided.", nameof(firebaseBaseUrl));

            // Ensure trailing slash
            _firebaseBaseUrl = firebaseBaseUrl.EndsWith("/")
                ? firebaseBaseUrl
                : firebaseBaseUrl + "/";

            _httpClient = httpClient ?? new HttpClient();
        }

        // Creates or updates a user entry in Firebase under Users/{userId}.
        public async Task SaveUserAsync(FirebaseUser user)
        {
            if (string.IsNullOrWhiteSpace(user.UserId))
                throw new ArgumentException("UserId is required for FirebaseUser.");

            var url = $"{_firebaseBaseUrl}Users/{user.UserId}.json";
            var response = await _httpClient.PutAsJsonAsync(url, user);
            response.EnsureSuccessStatusCode();
        }

        // Adds a donation under Donations with an auto-generated Firebase key.
        public async Task<string?> AddDonationAsync(FirebaseDonation donation)
        {
            var url = $"{_firebaseBaseUrl}Donations.json";
            var response = await _httpClient.PostAsJsonAsync(url, donation);
            response.EnsureSuccessStatusCode();

            // Firebase Realtime DB returns: { "name": "-Nv123abc..." }
            var json = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("name", out var nameProp))
                {
                    return nameProp.GetString();
                }
            }
            catch
            {
                
            }

            return null;
        }

        /// Gets all donations from Firebase as a flat list.
        public async Task<List<FirebaseDonation>> GetDonationsAsync()
        {
            var url = $"{_firebaseBaseUrl}Donations.json";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<FirebaseDonation>();

            // Firebase returns a dictionary: { "id1": {...}, "id2": {...} }
            var donationsDict = JsonSerializer.Deserialize<Dictionary<string, FirebaseDonation>>(json) 
                                ?? new Dictionary<string, FirebaseDonation>();

            // Attach the Firebase key to each donation for reference (optional).
            foreach (var kvp in donationsDict)
            {
                kvp.Value.FirebaseId = kvp.Key;
            }

            return donationsDict.Values
                .OrderByDescending(d => d.CreatedAt)
                .ToList();
        }

        public async Task UpdateDonationAsync(FirebaseDonation donation)
        {
            if (string.IsNullOrWhiteSpace(donation.FirebaseId))
                throw new ArgumentException("FirebaseId is required for updating a donation.");

            // Uses HTTP PUT to replace the document at the specific key
            var url = $"{_firebaseBaseUrl}Donations/{donation.FirebaseId}.json";
            var response = await _httpClient.PutAsJsonAsync(url, donation);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDonationAsync(string firebaseId)
        {
            if (string.IsNullOrWhiteSpace(firebaseId))
                throw new ArgumentException("FirebaseId is required for deleting a donation.");

            // Uses HTTP DELETE to remove the document at the specific key
            var url = $"{_firebaseBaseUrl}Donations/{firebaseId}.json";
            var response = await _httpClient.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }
}


