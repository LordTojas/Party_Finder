using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace MauiApp2.Models
{
    [Table("profile_data")]
    public class ProfileData : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; } // String helyett Guid, hogy megfeleljen a tábla user_id oszlopának típusának

        [Column("description")]
        public string Description { get; set; }

        [Column("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        [Column("friend_code")]
        public string FriendCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}