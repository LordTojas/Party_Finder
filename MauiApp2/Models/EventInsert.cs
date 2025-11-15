using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MauiApp2.Models
{
    [Table("events")]
    public class EventInsert : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("event_name")]
        public string EventName { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("age_restriction")]
        public string AgeRestriction { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("start_time")]
        public TimeSpan StartTime { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        [Column("end_time")]
        public TimeSpan EndTime { get; set; }

        [Column("location")]
        public string Location { get; set; }

        [Column("latitude")]
        public double? Latitude { get; set; }

        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("category")]
        public string Category { get; set; } // Új mező: Kategória

        [Column("music_genre")]
        public string MusicGenre { get; set; } // Új mező: Zenei műfaj (csak Koncert esetén)

       
    }
}