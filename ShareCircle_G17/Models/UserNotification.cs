using SQLite;

namespace ShareCircle_G17.Models;

[Table("Notifications")]
public class UserNotification
{
    [PrimaryKey]
    public string? NotificationId { get; set; }
    
    [Indexed]
    public string? UserId { get; set; } // The recipient (for local storage)

    public string? Type { get; set; } // e.g., "request"
    public string? RequestId { get; set; }
    public string? PostId { get; set; }
    public string? FromUserId { get; set; }
    public string? ItemTitle { get; set; }
    public string? ItemImageUrl { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
