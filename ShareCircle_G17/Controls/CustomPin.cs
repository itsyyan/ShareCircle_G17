using Microsoft.Maui.Controls.Maps;

namespace ShareCircle_G17.Controls
{
    public class CustomPin : Pin
    {
        public string? ImageUrl { get; set; }
        public string? PostId { get; set; }
        public int Count { get; set; } = 1;
        public Microsoft.Maui.Controls.ImageSource? Image { get; set; }
    }
}
