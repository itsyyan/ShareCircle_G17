namespace ShareCircle_G17.Models
{
    public class FirebaseUser
    {
        // Used as the document key / path segment in Firebase.
        public string UserId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int TotalDonations { get; set; }
    }

    public class FirebaseDonation
    {
        public string? FirebaseId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryType { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public string DropOffLocation { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}


