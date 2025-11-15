using MauiApp2.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MauiApp2.Pages
{
    public partial class SupriseMePage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private List<Event> _events = new List<Event>();
        private Event _selectedEvent;

        public Event SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                _selectedEvent = value;
                OnPropertyChanged();
                HasEvent = _selectedEvent != null;
                HasNoEvent = !HasEvent;
                OnPropertyChanged(nameof(HasEvent));
                OnPropertyChanged(nameof(HasNoEvent));
            }
        }

        public bool HasEvent { get; set; }
        public bool HasNoEvent { get; set; }

        public List<string> AgeGroups { get; set; } = new List<string> { "Nincs Korhatár", "Korhatáros (18+)" };
        public string SelectedAgeGroup { get; set; } = "Nincs Korhatár";

        public SupriseMePage(Supabase.Client supabaseClient)
        {
            Console.WriteLine("SupriseMePage konstruktor: Kezdés");
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            Console.WriteLine("SupriseMePage konstruktor: SupabaseClient beállítva");

            try
            {
                InitializeComponent();
                Console.WriteLine("SupriseMePage konstruktor: InitializeComponent sikeres");
                BindingContext = this;
                Task.Run(LoadEventsAsync).GetAwaiter().OnCompleted(() => { });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SupriseMePage konstruktor: Hiba az inicializálás során: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task LoadEventsAsync()
        {
            try
            {
                Console.WriteLine("LoadEventsAsync: Kezdés");
                var response = await _supabaseClient.From<Event>().Get();
                _events = response.Models?.ToList() ?? new List<Event>();

                foreach (var evt in _events)
                {
                    // Lekérdezzük a készítõ nevét az SQL függvény segítségével
                    var creatorNameResponse = await _supabaseClient.Rpc("get_user_name", new { user_id = evt.UserId });
                    evt.CreatorName = creatorNameResponse?.Content ?? "Ismeretlen";
                    evt.ShowCreatorDetailsCommand = new Command(async () => await ShowCreatorDetails(evt.UserId, evt.CreatorName));
                    evt.LikeCommand = new Command<long>(async (eventId) => await OnLikeClicked(eventId));
                    evt.BeThereCommand = new Command<long>(async (eventId) => await OnBeThereClicked(eventId));
                }

                Console.WriteLine($"LoadEventsAsync: Események betöltve, összesen: {_events.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadEventsAsync: Hiba az események betöltése során: {ex.Message}\nStackTrace: {ex.StackTrace}");
                await DisplayAlert("Hiba", "Nem sikerült betölteni az eseményeket. Kérlek, próbáld újra késõbb!", "OK");
            }
        }

        private async void OnSurpriseButtonClicked(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("OnSurpriseButtonClicked: Kezdés");

                // Ellenõrzés: Körzet és dátum megadása
                if (string.IsNullOrWhiteSpace(RadiusEntry.Text) || !double.TryParse(RadiusEntry.Text, out double radius) || radius <= 0)
                {
                    await DisplayAlert("Hiba", "Kérlek, adj meg egy érvényes körzetet (pozitív szám km-ben)!", "OK");
                    return;
                }

                if (DatePicker.Date == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, válassz egy dátumot!", "OK");
                    return;
                }

                // Korhatár ellenõrzése
                if (AgeLimitPicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, válassz egy korhatárt!", "OK");
                    return;
                }

                string selectedAgeRestriction = AgeLimitPicker.SelectedItem.ToString();
                bool allow18Plus = selectedAgeRestriction == "Korhatáros (18+)";
                Console.WriteLine($"OnSurpriseButtonClicked: Korhatár beállítás: 18+ események engedélyezve: {allow18Plus}");

                // Jelenlegi pozíció lekérdezése
                var location = await GetCurrentLocation();
                if (location == null)
                {
                    await DisplayAlert("Hiba", "Nem sikerült meghatározni a pozíciódat!", "OK");
                    return;
                }

                double userLatitude = location.Latitude;
                double userLongitude = location.Longitude;
                Console.WriteLine($"OnSurpriseButtonClicked: Pozíció: Lat={userLatitude}, Lon={userLongitude}");

                // A keresett nap kezdõ és befejezõ idõpontja
                var searchDateStart = DatePicker.Date.Date; // Keresett nap 00:00
                var searchDateEnd = searchDateStart.AddDays(1).AddTicks(-1); // Keresett nap 23:59:59.9999999

                // Szûrési feltételek
                var filteredEvents = _events.Where(evt =>
                {
                    // Esemény idõtartamának meghatározása
                    var eventStart = evt.StartDate.Date.Add(evt.StartTime);
                    var eventEnd = evt.EndDate.Date.Add(evt.EndTime);

                    // Ellenõrizzük, hogy a keresett nap átfedésben van-e az esemény idõtartamával
                    // (A keresett nap kezdete <= esemény vége ÉS a keresett nap vége >= esemény kezdete)
                    bool isDateInRange = searchDateStart <= eventEnd && searchDateEnd >= eventStart;

                    // További szûrési feltételek
                    return evt.Latitude.HasValue && evt.Longitude.HasValue &&
                           CalculateDistance(userLatitude, userLongitude, evt.Latitude.Value, evt.Longitude.Value) <= radius &&
                           isDateInRange &&
                           (allow18Plus || evt.AgeRestriction == "Nincs korhatár");
                }).ToList();

                Console.WriteLine($"OnSurpriseButtonClicked: Szûrt események száma: {filteredEvents.Count}");

                if (filteredEvents.Count == 0)
                {
                    SelectedEvent = null;
                    HasEvent = false;
                    HasNoEvent = true;
                    OnPropertyChanged(nameof(HasEvent));
                    OnPropertyChanged(nameof(HasNoEvent));
                    await DisplayAlert("Nincs találat", "Nincs találat a megadott körzetben, dátumon és korhatáron belül.", "OK");
                    return;
                }

                // Véletlenszerû esemény kiválasztása
                Random random = new Random();
                SelectedEvent = filteredEvents[random.Next(filteredEvents.Count)];
                HasEvent = true;
                HasNoEvent = false;
                OnPropertyChanged(nameof(HasEvent));
                OnPropertyChanged(nameof(HasNoEvent));

                Console.WriteLine($"OnSurpriseButtonClicked: Kiválasztott esemény: {SelectedEvent.EventName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnSurpriseButtonClicked: Hiba történt: {ex.Message}\nStackTrace: {ex.StackTrace}");
                await DisplayAlert("Hiba", $"Hiba történt: {ex.Message}", "OK");
            }
        }

        private async Task<Location> GetCurrentLocation()
        {
            try
            {
                Console.WriteLine("GetCurrentLocation: Kezdés");
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        Console.WriteLine("GetCurrentLocation: Helymeghatározási engedély megtagadva");
                        await DisplayAlert("Hiba", "A helymeghatározási engedély szükséges!", "OK");
                        return null;
                    }
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);
                Console.WriteLine($"GetCurrentLocation: Sikerült: Lat={location?.Latitude}, Lon={location?.Longitude}");
                return location;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentLocation: Hiba: {ex.Message}\nStackTrace: {ex.StackTrace}");
                await DisplayAlert("Hiba", $"Hiba a pozíció lekérdezése közben: {ex.Message}", "OK");
                return null;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Föld sugara km-ben
            var dLat = ToRadian(lat2 - lat1);
            var dLon = ToRadian(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadian(lat1)) * Math.Cos(ToRadian(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = R * c;
            return distance;
        }

        private double ToRadian(double degree)
        {
            return degree * Math.PI / 180;
        }

        private async Task OnLikeClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, jelentkezz be a kedveléshez!", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                // Ellenõrizzük, hogy a felhasználó már kedvelte-e az eseményt
                var existingLike = await _supabaseClient
                    .From<Liked>()
                    .Filter("user_id", Constants.Operator.Equals, user.Id)
                    .Filter("event_id", Constants.Operator.Equals, eventId)
                    .Get();

                if (existingLike.Models.Any())
                {
                    // Ha már kedvelte, töröljük a kedvelést
                    await _supabaseClient
                        .From<Liked>()
                        .Filter("user_id", Constants.Operator.Equals, user.Id)
                        .Filter("event_id", Constants.Operator.Equals, eventId)
                        .Delete();
                    await DisplayAlert("Siker", "Kedvelés törölve!", "OK");
                }
                else
                {
                    // Ha még nem kedvelte, hozzáadjuk a kedvelést
                    var like = new Liked
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _supabaseClient.From<Liked>().Insert(like);
                    await DisplayAlert("Siker", "Esemény kedvelve!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a kedvelés közben: {ex.Message}", "OK");
            }
        }

        private async Task OnBeThereClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, jelentkezz be a részvétel jelzéséhez!", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                // Ellenõrizzük, hogy a felhasználó már jelezte-e, hogy ott lesz
                var existingBeThere = await _supabaseClient
                    .From<BeThere>()
                    .Filter("user_id", Constants.Operator.Equals, user.Id)
                    .Filter("event_id", Constants.Operator.Equals, eventId)
                    .Get();

                if (existingBeThere.Models.Any())
                {
                    // Ha már jelezte, töröljük a jelzést
                    await _supabaseClient
                        .From<BeThere>()
                        .Filter("user_id", Constants.Operator.Equals, user.Id)
                        .Filter("event_id", Constants.Operator.Equals, eventId)
                        .Delete();
                    await DisplayAlert("Siker", "Részvétel törölve!", "OK");
                }
                else
                {
                    // Ha még nem jelezte, hozzáadjuk a jelzést
                    var beThere = new BeThere
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _supabaseClient.From<BeThere>().Insert(beThere);
                    await DisplayAlert("Siker", "Ott leszek jelzés hozzáadva!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a részvétel jelzése közben: {ex.Message}", "OK");
            }
        }

        private async Task ShowCreatorDetails(string creatorId, string creatorName)
        {
            try
            {
                // Lekérdezzük, hány eseményt készített a felhasználó
                var createdEventsResponse = await _supabaseClient
                    .From<Event>()
                    .Where(e => e.UserId == creatorId)
                    .Get();

                int createdEventsCount = createdEventsResponse.Models?.Count ?? 0;

                // Lekérdezzük a készítõ eseményeinek értékeléseit
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

                // Megjelenítjük az adatokat egy felugró ablakban
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