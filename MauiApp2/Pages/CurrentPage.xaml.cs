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

        public int UserAge { get; set; } = 0;  

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

            
            Task.Run(async () =>
            {
                await LoadUserAgeAsync();
                LoadTopEventsAsync();
            });
        }

       
        private async Task LoadUserAgeAsync()
        {
            try
            {
                var authUser = _supabaseClient.Auth.CurrentUser;

                if (authUser == null)
                {
                    UserAge = 0;
                    return;
                }

                
                var response = await _supabaseClient
                    .From<MauiApp2.Models.User>()
                    .Where(u => u.Id == authUser.Id)
                    .Single();

                if (response != null)
                    UserAge = response.Age;
                else
                    UserAge = 0;

                Console.WriteLine($"CurrentPage → UserAge = {UserAge}");
            }
            catch
            {
                UserAge = 0;
            }
        }

       
        private async void LoadTopEventsAsync()
        {
            try
            {
               
                var eventsResponse = await _supabaseClient.From<EventInsert>().Get();
                var allEvents = eventsResponse.Models;

                var now = DateTime.Now;

              
                var ongoingEvents = allEvents.Where(e =>
                {
                    var startDateTime = e.StartDate.Date.Add(e.StartTime);
                    var endDateTime = e.EndDate.Date.Add(e.EndTime);

                    return startDateTime <= now && now <= endDateTime;
                }).ToList();


                
                if (UserAge > 0 && UserAge < 18)
                {
                    ongoingEvents = ongoingEvents
                        .Where(e => e.AgeRestriction == "Nincs korhatár" ||
                                    string.IsNullOrEmpty(e.AgeRestriction))
                        .ToList();
                }


               
                var ratingsResponse = await _supabaseClient.From<EventRating>().Get();
                var ratings = ratingsResponse.Models;


                
                var eventsWithRatings = new List<EventWithRating>();

                foreach (var evt in ongoingEvents)
                {
                    var eventRatings = ratings.Where(r => r.EventId == evt.Id).ToList();
                    double averageRating = eventRatings.Any() ? eventRatings.Average(r => r.Rating) : 0.0;

                    var userProfile = await _supabaseClient
                        .From<MauiApp2.Models.User>()
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
                        CreatorUsername = creatorUsername,
                        AgeRestriction = evt.AgeRestriction   
                    });
                }

               
                TopEvents = eventsWithRatings
                    .OrderByDescending(e => e.AverageRating)
                    .ToList();
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
                EventsCollectionView.SelectedItem = null;
            }
        }

       
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EventWithRating : EventInsert
    {
        public double AverageRating { get; set; }
        public string CreatorUsername { get; set; }
    }

    [Table("event_ratings")]
    public class EventRating : BaseModel
    {
        [PrimaryKey("event_id", false)]
        public long EventId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("rating")]
        public int Rating { get; set; }
    }
}
