using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;
using MauiApp2.Services;

namespace MauiApp2.Pages
{
    public partial class MyEventsPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ObservableCollection<Event> _userEvents;

        public ObservableCollection<Event> UserEvents
        {
            get => _userEvents;
            set
            {
                _userEvents = value;
                OnPropertyChanged();
                HasEvents = _userEvents.Any();
                HasNoEvents = !_userEvents.Any();
                OnPropertyChanged(nameof(HasEvents));
                OnPropertyChanged(nameof(HasNoEvents));
            }
        }

        public bool HasEvents { get; set; }
        public bool HasNoEvents { get; set; }

        public MyEventsPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _userEvents = new ObservableCollection<Event>();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserEvents();
        }

        private async Task LoadUserEvents()
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, jelentkezz be!", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                
                var response = await _supabaseClient
                    .From<EventInsert>()
                    .Filter("user_id", Constants.Operator.Equals, user.Id)
                    .Get();

                _userEvents.Clear();
                if (response.Models != null)
                {
                    foreach (var evtInsert in response.Models)
                    {
                        
                        var evt = new Event
                        {
                            Id = evtInsert.Id,
                            UserId = evtInsert.UserId,
                            EventName = evtInsert.EventName,
                            Description = evtInsert.Description,
                            AgeRestriction = evtInsert.AgeRestriction,
                            StartDate = evtInsert.StartDate,
                            StartTime = evtInsert.StartTime,
                            EndDate = evtInsert.EndDate,
                            EndTime = evtInsert.EndTime,
                            Location = evtInsert.Location,
                            Latitude = evtInsert.Latitude,
                            Longitude = evtInsert.Longitude,
                            CreatedAt = evtInsert.CreatedAt,
                            Category = evtInsert.Category,
                            MusicGenre = evtInsert.MusicGenre
                        };

                       
                        var creatorNameResponse = await _supabaseClient.Rpc("get_user_name", new { user_id = evt.UserId });
                        evt.CreatorName = creatorNameResponse?.Content ?? "Ismeretlen";
                        evt.ShowCreatorDetailsCommand = new Command(async () => await ShowCreatorDetails(evt.UserId, evt.CreatorName));
                        evt.EditCommand = new Command<long>(async (eventId) => await EditEvent(eventId));
                        evt.DeleteCommand = new Command<long>(async (eventId) => await DeleteEvent(eventId));
                        _userEvents.Add(evt);
                    }
                }

                UserEvents = _userEvents;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba az események betöltése közben: {ex.Message}", "OK");
            }
        }

        private async Task EditEvent(long eventId)
        {
            try
            {
                var evt = UserEvents.FirstOrDefault(e => e.Id == eventId);
                if (evt == null) return;

                
                Console.WriteLine($"Szerkesztés kezdése: Id={evt.Id}, EventName={evt.EventName}");

               
                await Navigation.PushAsync(new EditEventPage(_supabaseClient, new PlaceService(), evt));

                
                await LoadUserEvents();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba az esemény módosítása közben: {ex.Message}", "OK");
            }
        }

        private async Task DeleteEvent(long eventId)
        {
            try
            {
                var confirm = await DisplayAlert("Megerõsítés", "Biztosan törölni szeretnéd ezt az eseményt?", "Igen", "Nem");
                if (!confirm) return;

              
                Console.WriteLine($"Törlés kezdése: Id={eventId}");

                
                await _supabaseClient
                    .From<EventInsert>()
                    .Filter("id", Constants.Operator.Equals, eventId)
                    .Delete();

                await DisplayAlert("Siker", "Esemény sikeresen törölve!", "OK");
                await LoadUserEvents(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a törlés során: {ex.Message}");
                await DisplayAlert("Hiba", $"Hiba az esemény törlése közben: {ex.Message}", "OK");
            }
        }

        private async Task ShowCreatorDetails(string creatorId, string creatorName)
        {
            try
            {
                
                var createdEventsResponse = await _supabaseClient
                    .From<EventInsert>()
                    .Where(e => e.UserId == creatorId)
                    .Get();

                int createdEventsCount = createdEventsResponse.Models?.Count ?? 0;

                
                double averageRating = 0;
                if (createdEventsCount > 0)
                {
                    var createdEventIds = createdEventsResponse.Models.Select(e => e.Id).ToList();
                    var ratingsResponse = await _supabaseClient
                        .From<EventRatings>()
                        .Filter("event_id", Constants.Operator.In, createdEventIds)
                        .Get();

                    if (ratingsResponse.Models != null && ratingsResponse.Models.Any())
                    {
                        averageRating = ratingsResponse.Models.Average(r => r.Rating);
                    }
                }

                
                string message = $"{creatorName} adatai:\n" +
                                $"Készített események száma: {createdEventsCount}\n" +
                                $"Átlagos értékelés: {(averageRating > 0 ? averageRating.ToString("F1") : "Nincs értékelés")}";

                await DisplayAlert("Készítõ adatai", message, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a készítõ adatainak lekérdezése közben: {ex.Message}", "OK");
            }
        }
    }
}