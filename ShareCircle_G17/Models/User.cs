using SQLite;

namespace ShareCircle_G17.Models
{
    [Table("Users")]
    public class User
    {
        [PrimaryKey]
        public string? UserId { get; set; }

        public string? Username { get; set; }
        public string? UsernameLower { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? LocalPasswordHash { get; set; }
        public string? LocalPasswordSalt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalDonations { get; set; }
    }
}
