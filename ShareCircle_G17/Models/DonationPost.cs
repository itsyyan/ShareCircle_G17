using SQLite;

namespace ShareCircle_G17.Models
{
    [Table("Donations")]
    public class DonationPost
    {
        [PrimaryKey]
        public string? PostId { get; set; }

        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? UserImageUrl { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        // Category: "food" or "item"
        public string? Category { get; set; }

        // SubCategory: "fruit", "bread", "packaged" (for food) or "cloths", "toys", "books" (for items)
        public string? SubCategory { get; set; }

        public string? ImageUrl { get; set; }
        public string? DropOffLocation { get; set; }
        public string? ContactEmail { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Location { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? Status { get; set; } // Available, Reserved, Completed
        public int ViewCount { get; set; }

        // Sync flag for offline/online mode
        public bool IsSynced { get; set; }

        // Computed property for display (not stored in DB)
        [Ignore]
        public string DisplayImageUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImageUrl))
                    return "https://via.placeholder.com/150?text=No+Image";

                var trimmed = ImageUrl.Trim();

                if (trimmed.Contains("|"))
                {
                    var first = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (first.Length > 0 && !string.IsNullOrWhiteSpace(first[0]))
                        return first[0];
                }
                else if (trimmed.Contains(";") && !trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var first = trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (first.Length > 0 && !string.IsNullOrWhiteSpace(first[0]))
                        return first[0];
                }

                return trimmed;
            }
        }
    }
}
