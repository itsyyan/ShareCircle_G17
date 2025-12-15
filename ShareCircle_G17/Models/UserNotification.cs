namespace ShareCircle_G17.Models;

public class UserNotification
{
    public string? NotificationId { get; set; }
    public string? Type { get; set; } // e.g., "request"
    public string? RequestId { get; set; }
    public string? PostId { get; set; }
    public string? FromUserId { get; set; }
    public string? FromUserName { get; set; }
    public string? ItemTitle { get; set; }
    public string? ItemImageUrl { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
