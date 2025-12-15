using SQLite;

namespace ShareCircle_G17.Models
{
    [Table("DonationRequests")]
    public class DonationRequest
    {
        [PrimaryKey]
        public string? RequestId { get; set; }

        public string? PostId { get; set; }
        public string? RequesterId { get; set; }
        public string? RequesterName { get; set; }
        public string? RequesterEmail { get; set; }
        public string? DonorId { get; set; }
        public string? DonorName { get; set; }

        // Donation details (cached for display)
        public string? ItemTitle { get; set; }
        public string? ItemDescription { get; set; }
        public string? ItemImageUrl { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }

        public string? Message { get; set; }
        public string? Status { get; set; } // Pending, Approved, Rejected, Completed, Cancelled

        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        public bool IsSynced { get; set; }
    }
}
