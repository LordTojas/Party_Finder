using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MauiApp2.Models
{
    [Table("be_there")]
    public class BeThere : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("event_id")]
        public long EventId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}