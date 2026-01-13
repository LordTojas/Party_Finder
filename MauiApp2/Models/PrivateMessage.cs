using System;
using Microsoft.Maui.Controls;    
using Microsoft.Maui.Graphics;    


using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


using Newtonsoft.Json;

namespace MauiApp2.Models
{
    [Table("private_messages")]
    public class PrivateMessage: BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("sender_id")] public Guid SenderId { get; set; }
        [Column("receiver_id")] public Guid ReceiverId { get; set; }
        [Column("message_text")] public string MessageText { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; }

        // Segédmező a UI-hoz (saját üzenet vagy sem)
        [Newtonsoft.Json.JsonIgnore]
        public bool IsMine { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public Color BubbleColor => IsMine ? Color.FromArgb("#4CAF50") : Color.FromArgb("#33334D");

        [Newtonsoft.Json.JsonIgnore]
        public LayoutOptions Alignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;
    }
}
