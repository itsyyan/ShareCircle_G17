using System;

namespace website.Data
{
    public class User
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        // Helper for sorting if needed
        public string? UsernameLower { get; set; }
    }
}
