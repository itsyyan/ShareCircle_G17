using System;

namespace website.Data
{
    public class DonationPost
    {
        public string? PostId { get; set; }
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ViewCount { get; set; }
    }
}
