using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;

namespace MauiApp2.Pages
{
    public partial class SavedPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ObservableCollection<Event> _savedEvents;
        private string _userName;
        private int _attendedEventsCount;
        private int _createdEventsCount;
        private double _averageRating;

        public ObservableCollection<Event> SavedEvents
        {
            get => _savedEvents;
            set
            {
                _savedEvents = value;
                OnPropertyChanged();
                HasSavedEvents = _savedEvents.Any();
                HasNoSavedEvents = !_savedEvents.Any();
                OnPropertyChanged(nameof(HasSavedEvents));
                OnPropertyChanged(nameof(HasNoSavedEvents));
            }
        }

        public bool HasSavedEvents { get; set; }
        public bool HasNoSavedEvents { get; set; }

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged();
            }
        }

        public int AttendedEventsCount
        {
            get => _attendedEventsCount;
            set
            {
                _attendedEventsCount = value;
                OnPropertyChanged();
            }
        }

        public int CreatedEventsCount
        {
            get => _createdEventsCount;
            set
            {
                _createdEventsCount = value;
                OnPropertyChanged();
            }
        }

        public double AverageRating
        {
            get => _averageRating;
            set
            {
                _averageRating = value;
                OnPropertyChanged();
            }
        }

        public SavedPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _savedEvents = new ObservableCollection<Event>();
            BindingContext = this;
            LoadSavedEventsAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadSavedEventsAsync();
        }

        private async void LoadSavedEventsAsync()
        {
            try
            {
                Console.WriteLine("LoadSavedEventsAsync kezdés...");

                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    Console.WriteLine("Nincs bejelentkezett felhasználó, navigálás a LoginPage-re...");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                Console.WriteLine($"Bejelentkezett felhasználó: {user.Id}");

                // Felhasználó nevének lekérése biztonságosan
                string fullName = null;
                if (user.UserMetadata != null && user.UserMetadata.TryGetValue("full_name", out var fullNameObj))
                {
                    fullName = fullNameObj?.ToString();
                }
                UserName = fullName ?? user.Email ?? "Felhasználó";
                Console.WriteLine($"Felhasználó neve: {UserName}");

                // Statisztikák kiszámítása
                // 1. Hány eseményen jelezte, hogy ott lesz (BeThere tábla)
                var beThereResponse = await _supabaseClient
                    .From<BeThere>()
                    .Where(b => b.UserId == user.Id)
                    .Get();

                AttendedEventsCount = beThereResponse.Models?.Count ?? 0;
                Console.WriteLine($"Ott leszek események száma: {AttendedEventsCount}");
                if (AttendedEventsCount == 0)
                {
                    Console.WriteLine("Nincsenek rekordok a BeThere táblában a felhasználóhoz.");
                    Console.WriteLine($"Felhasználó ID (user.Id): {user.Id}");
                }

                // 2. Hány eseményt készített (Events tábla, user_id alapján)
                var createdEventsResponse = await _supabaseClient
                    .From<Event>()
                    .Where(e => e.UserId == user.Id)
                    .Get();

                CreatedEventsCount = createdEventsResponse.Models?.Count ?? 0;
                Console.WriteLine($"Készített események száma: {CreatedEventsCount}");
                if (CreatedEventsCount == 0)
                {
                    Console.WriteLine("Nincsenek rekordok az Events táblában a felhasználóhoz (készített események).");
                    Console.WriteLine($"Felhasználó ID (user.Id): {user.Id}");
                }

                // 3. Átlagos értékelés más felhasználóktól (EventRatings tábla)
                if (CreatedEventsCount > 0)
                {
                    var createdEventIds = createdEventsResponse.Models.Select(e => e.Id).ToList();
                    var ratingsResponse = await _supabaseClient
                        .From<EventRatings>()
                        .Filter("event_id", Constants.Operator.In, createdEventIds)
                        .Get();

                    if (ratingsResponse.Models != null && ratingsResponse.Models.Any())
                    {
                        AverageRating = ratingsResponse.Models.Average(r => r.Rating);
                        Console.WriteLine($"Értékelések száma: {ratingsResponse.Models.Count}, Átlagos értékelés: {AverageRating}");
                    }
                    else
                    {
                        AverageRating = 0;
                        Console.WriteLine("Nincsenek értékelések az EventRatings táblában a felhasználó eseményeire.");
                    }
                }
                else
                {
                    AverageRating = 0;
                    Console.WriteLine("Nincsenek készített események, így nincs átlagos értékelés.");
                }

                // Betöltjük a felhasználó mentett eseményeit a BeThere táblából
                if (beThereResponse.Models != null && beThereResponse.Models.Any())
                {
                    var eventIds = beThereResponse.Models.Select(b => b.EventId).ToList();
                    Console.WriteLine($"Mentett események ID-i: {string.Join(", ", eventIds)}");

                    // Események lekérdezése a Filter metódussal
                    var eventsResponse = await _supabaseClient
                        .From<Event>()
                        .Filter("id", Constants.Operator.In, eventIds)
                        .Get();

                    Console.WriteLine($"Events tábla lekérdezése megtörtént, rekordok száma: {eventsResponse.Models?.Count ?? 0}");

                    if (eventsResponse.Models != null && eventsResponse.Models.Any())
                    {
                        // Betöltjük a felhasználó korábbi értékeléseit
                        var ratingsResponse = await _supabaseClient
                            .From<EventRatings>()
                            .Filter("user_id", Constants.Operator.Equals, user.Id)
                            .Filter("event_id", Constants.Operator.In, eventIds)
                            .Get();

                        Console.WriteLine($"EventRatings tábla lekérdezése megtörtént, rekordok száma: {ratingsResponse.Models?.Count ?? 0}");

                        var ratings = ratingsResponse.Models.ToDictionary(r => r.EventId, r => r.Rating);

                        foreach (var evt in eventsResponse.Models)
                        {
                            evt.SaveRatingCommand = new Command<long>(async (eventId) => await SaveRatingAsync(eventId, evt.Rating));
                            evt.Rating = ratings.ContainsKey(evt.Id) ? ratings[evt.Id] : 0; // Ha van korábbi értékelés, azt használjuk
                            Console.WriteLine($"Esemény betöltve: {evt.EventName}, Rating: {evt.Rating}");
                        }

                        SavedEvents = new ObservableCollection<Event>(eventsResponse.Models);
                    }
                    else
                    {
                        Console.WriteLine("Nincsenek események az Events táblában a megadott ID-k alapján.");
                        SavedEvents = new ObservableCollection<Event>();
                    }
                }
                else
                {
                    Console.WriteLine("Nincsenek mentett események a BeThere táblában.");
                    SavedEvents = new ObservableCollection<Event>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a mentett események betöltése közben: {ex.Message}");
                await DisplayAlert("Hiba", $"Hiba a mentett események betöltése közben: {ex.Message}", "OK");
            }
        }

        private async Task SaveRatingAsync(long eventId, int rating)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    Console.WriteLine("Nincs bejelentkezett felhasználó, navigálás a LoginPage-re...");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                if (rating < 1 || rating > 5)
                {
                    Console.WriteLine($"Érvénytelen értékelés: {rating}");
                    await DisplayAlert("Hiba", "Az értékelés 1 és 5 között kell legyen!", "OK");
                    return;
                }

                // Ellenõrizzük, hogy a felhasználó már értékelte-e az eseményt
                var existingRating = await _supabaseClient
                    .From<EventRatings>()
                    .Where(r => r.UserId == user.Id && r.EventId == eventId)
                    .Get();

                if (existingRating.Models.Any())
                {
                    // Ha már létezik értékelés, frissítjük
                    await _supabaseClient
                        .From<EventRatings>()
                        .Where(r => r.UserId == user.Id && r.EventId == eventId)
                        .Set(r => r.Rating, rating)
                        .Update();
                    Console.WriteLine($"Értékelés frissítve: Esemény ID: {eventId}, Új értékelés: {rating}");
                }
                else
                {
                    // Ha még nem értékelte, új rekordot hozunk létre
                    var eventRating = new EventRatings
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        Rating = rating,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _supabaseClient.From<EventRatings>().Insert(eventRating);
                    Console.WriteLine($"Új értékelés hozzáadva: Esemény ID: {eventId}, Értékelés: {rating}");
                }

                await DisplayAlert("Siker", "Értékelés mentve!", "OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba az értékelés mentése közben: {ex.Message}");
                await DisplayAlert("Hiba", $"Hiba az értékelés mentése közben: {ex.Message}", "OK");
            }
        }
    }
}