using System;
using System.Linq;
using SQLite;

namespace ShareCircle_G17.Models
{
    public class Address
    {
        [PrimaryKey]
        public string? AddressId { get; set; }
        public string? UserId { get; set; }
        public string? Label { get; set; }
        public string? Line1 { get; set; }
        public string? Line2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? PhoneNumber { get; set; }
        public string? DoorNumber { get; set; }
        public string? Building { get; set; }
        public string? Street { get; set; }
        public string? Unit { get; set; }
        public string? FullName { get; set; }
        public bool IsDefault { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? FormattedAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string OneLine => ToOneLine();

        public string ToOneLine()
        {
            // If FormattedAddress exists, use it directly (it's already complete)
            if (!string.IsNullOrWhiteSpace(FormattedAddress))
            {
                return FormattedAddress;
            }

            // Otherwise, build from individual parts (avoid duplicates)
            var parts = new[]
            {
                Street,
                Unit,
                City,
                PostalCode,
                State,
                Country
            };

            var compact = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(compact) ? "Address not set" : compact;
        }
    }
}
