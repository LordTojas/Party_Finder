using MauiApp2.Models;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MauiApp2.Pages
{
    public partial class CurrentPage : ContentPage, INotifyPropertyChanged
    {
        private readonly Supabase.Client _supabaseClient;
        private List<EventWithRating> _topEvents;

        public List<EventWithRating> TopEvents
        {
            get => _topEvents;
            set
            {
                _topEvents = value;
                OnPropertyChanged(nameof(TopEvents));
            }
        }

        public CurrentPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            BindingContext = this;

            LoadTopEventsAsync();
        }

        private async void LoadTopEventsAsync()
        {
            try
            {
                // 1. Lekérjük az összes eseményt az events táblából
                var eventsResponse = await _supabaseClient.From<EventInsert>().Get();
                var allEvents = eventsResponse.Models;

                // 2. Szûrjük az Ongoing eseményeket
                var now = DateTime.Now;
                var ongoingEvents = allEvents.Where(e =>
                {
                    var startDateTime = e.StartDate.Date.Add(e.StartTime);
                    var endDateTime = e.EndDate.Date.Add(e.EndTime);
                    return startDateTime <= now && now <= endDateTime;
                }).ToList();

                // 3. Lekérjük az értékeléseket az event_ratings táblából
                var ratingsResponse = await _supabaseClient.From<EventRating>().Get();
                var ratings = ratingsResponse.Models;

                // 4. Kiszámítjuk az átlagos értékeléseket
                var eventsWithRatings = new List<EventWithRating>();
                foreach (var evt in ongoingEvents)
                {
                    var eventRatings = ratings.Where(r => r.EventId == evt.Id).ToList();
                    double averageRating = eventRatings.Any() ? eventRatings.Average(r => r.Rating) : 0.0;

                    // 5. Lekérjük a létrehozó felhasználónevet a users táblából
                    // Az evt.UserId string típusú, ahogy a User.Id is, így nem kell konvertálni
                    var userProfile = await _supabaseClient.From<User>()
                        .Where(u => u.Id == evt.UserId)
                        .Single();
                    string creatorUsername = userProfile?.Username ?? "Ismeretlen";

                    eventsWithRatings.Add(new EventWithRating
                    {
                        Id = evt.Id,
                        EventName = evt.EventName,
                        Description = evt.Description,
                        StartDate = evt.StartDate,
                        StartTime = evt.StartTime,
                        EndDate = evt.EndDate,
                        EndTime = evt.EndTime,
                        Location = evt.Location,
                        Category = evt.Category,
                        AverageRating = averageRating,
                        CreatorUsername = creatorUsername
                    });
                }

                // 6. Rendezés az átlagos értékelés szerint csökkenõ sorrendben
                TopEvents = eventsWithRatings.OrderByDescending(e => e.AverageRating).ToList();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba az események betöltése közben: {ex.Message}", "OK");
            }
        }

        private async void OnEventSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is EventWithRating selectedEvent)
            {
                // Navigáció az esemény részleteire (például egy EventDetailsPage-re)
                // await Shell.Current.GoToAsync($"EventDetailsPage?eventId={selectedEvent.Id}");
                EventsCollectionView.SelectedItem = null; // Kiválasztás törlése
            }
        }

        // INotifyPropertyChanged implementáció
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Segédosztály az eseményekhez és értékelésekhez
    public class EventWithRating : EventInsert
    {
        public double AverageRating { get; set; }
        public string CreatorUsername { get; set; }
    }

    // Az event_ratings tábla modellje
    [Table("event_ratings")]
    public class EventRating : BaseModel
    {
        [PrimaryKey("event_id", false)]
        public long EventId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } // Guid helyett string, hogy illeszkedjen a users tábla id oszlopához

        [Column("rating")]
        public int Rating { get; set; }
    }
}