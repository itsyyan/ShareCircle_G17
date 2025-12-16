using SQLite;
using System;

namespace ShareCircle_G17.Models
{
    [Table("SearchHistory")]
    public class SearchHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string? UserId { get; set; }

        public string? Keyword { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
