using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace MauiApp2.Models
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } // String típusra váltás

        [Column("username")]
        public string Username { get; set; }

        [Column("gender")]
        public string Gender { get; set; }

        [Column("age")]
        public int Age { get; set; }

        [Column("email")]
        public string Email { get; set; }
    }
}