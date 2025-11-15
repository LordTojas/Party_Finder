using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace MauiApp2.Models
{
    [Table("events")]
    public class Event : BaseModel, INotifyPropertyChanged
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
        public string Category { get; set; }

        [Column("music_genre")]
        public string MusicGenre { get; set; }

        // Státusz tulajdonságok
        public string StatusText
        {
            get
            {
                var currentDateTime = DateTime.UtcNow;
                var startDateTime = StartDate.Add(StartTime);
                var endDateTime = EndDate.Add(EndTime);

                if (currentDateTime < startDateTime)
                    return "KÖVETKEZŐ";
                else if (currentDateTime >= startDateTime && currentDateTime <= endDateTime)
                    return "MOST";
                else
                    return "LEJÁRT";
            }
        }

        public Color StatusColor
        {
            get
            {
                var currentDateTime = DateTime.UtcNow;
                var startDateTime = StartDate.Add(StartTime);
                var endDateTime = EndDate.Add(EndTime);

                if (currentDateTime < startDateTime)
                    return Color.FromArgb("#FF5733"); // Narancssárga (KÖVETKEZŐ)
                else if (currentDateTime >= startDateTime && currentDateTime <= endDateTime)
                    return Color.FromArgb("#33FF57"); // Zöld (MOST)
                else
                    return Color.FromArgb("#FF3333"); // Piros (LEJÁRT)
            }
        }

        // Parancsok a gombokhoz
        public Command<long> LikeCommand { get; set; }
        public Command<long> BeThereCommand { get; set; }
        public Command<long> SaveRatingCommand { get; set; }
        public Command ShowCreatorDetailsCommand { get; set; }
        public Command<long> EditCommand { get; set; }
        public Command<long> DeleteCommand { get; set; }

        // Értékeléshez szükséges tulajdonságok
        public bool IsExpired
        {
            get
            {
                var currentDateTime = DateTime.UtcNow;
                var endDateTime = EndDate.Add(EndTime);
                return currentDateTime > endDateTime;
            }
        }

        private int _rating;
        public int Rating
        {
            get => _rating;
            set
            {
                _rating = value;
                OnPropertyChanged(nameof(Rating));
            }
        }

        // Készítő neve
        private string _creatorName;
        public string CreatorName
        {
            get => _creatorName;
            set
            {
                _creatorName = value;
                OnPropertyChanged(nameof(CreatorName));
            }
        }

        // INotifyPropertyChanged implementáció
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName) // Public-ra változtatva
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}