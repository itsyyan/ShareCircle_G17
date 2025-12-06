using SQLite;

namespace ShareCircle_G17.Models
{
    public class DonationItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public byte[] ImageData { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string ContactEmail { get; set; }
        public string Location { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsSynced { get; set; }

        public string UserId { get; set; } = "default_user";

        public string? FirebaseId { get; set; }
    }
}
