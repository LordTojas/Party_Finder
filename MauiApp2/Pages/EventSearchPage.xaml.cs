using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using MauiApp2.Models;

namespace MauiApp2.Pages
{
    public partial class EventSearchPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ObservableCollection<Event> _allEvents;
        private ObservableCollection<Event> _filteredEvents;

       
        public int UserAge { get; set; } = 0;

        
        public string SearchName { get; set; } = string.Empty;
        public string SelectedMusicGenre { get; set; } = string.Empty;
        public string SelectedAgeGroup { get; set; } = "Nincs Korhatár";
        public DateTime? StartDateFilter { get; set; }
        public DateTime? EndDateFilter { get; set; }

        public List<string> AgeGroups { get; set; } = new()
        {
            "Nincs Korhatár", "Korhatáros (18+)"
        };

        public List<string> MusicGenres { get; set; } = new()
        {
            "Minden", "Pop", "Rock", "Jazz", "Elektronikus", "Klasszikus", "Egyéb"
        };

        public ObservableCollection<Event> FilteredEvents
        {
            get => _filteredEvents;
            set
            {
                _filteredEvents = value;
                OnPropertyChanged();
                HasResults = _filteredEvents.Any();
                HasNoResults = !_filteredEvents.Any();
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(HasNoResults));
            }
        }

        public bool HasResults { get; set; }
        public bool HasNoResults { get; set; }

        public EventSearchPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _allEvents = new ObservableCollection<Event>();
            _filteredEvents = new ObservableCollection<Event>();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserAge();     
            await RequestLocationPermission();
            await LoadEvents();
        }

        
        private async Task LoadUserAge()
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
            }
            catch
            {
                UserAge = 0;
            }
        }

      

        private async Task RequestLocationPermission()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                    await DisplayAlert("Hiba", "A helymeghatározási engedély szükséges a kereséshez!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Engedélykérés sikertelen: {ex.Message}", "OK");
            }
        }

        private async Task LoadEvents()
        {
            try
            {
                var response = await _supabaseClient.From<Event>().Get();
                _allEvents.Clear();

                if (response.Models != null)
                {
                    foreach (var evt in response.Models)
                    {
                        var creatorNameResponse =
                            await _supabaseClient.Rpc("get_user_name", new { user_id = evt.UserId });

                        evt.CreatorName = creatorNameResponse?.Content ?? "Ismeretlen";

                        evt.ShowCreatorDetailsCommand = new Command(async () =>
                            await ShowCreatorDetails(evt.UserId, evt.CreatorName));

                        evt.LikeCommand = new Command<long>(async (eventId) =>
                            await OnLikeClicked(eventId));

                        evt.BeThereCommand = new Command<long>(async (eventId) =>
                            await OnBeThereClicked(eventId));

                        _allEvents.Add(evt);
                    }
                }

                await FilterEvents();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Nem sikerült betölteni az eseményeket: {ex.Message}", "OK");
            }
        }

        private async void OnSearchClicked(object sender, EventArgs e)
        {
            await FilterEvents();
        }

        private async Task FilterEvents()
        {
            try
            {
                var location = await GetCurrentLocation() ??
                               new Location(47.4979, 19.0402); 

                double radiusKm = RadiusSlider.Value;

                var filtered = _allEvents.Where(evt =>
                {
                    
                    bool isWithinRadius = true;

                    if (evt.Latitude.HasValue && evt.Longitude.HasValue)
                    {
                        double distance = CalculateDistance(
                            location.Latitude,
                            location.Longitude,
                            evt.Latitude.Value,
                            evt.Longitude.Value
                        );
                        isWithinRadius = distance <= radiusKm;
                    }

                   
                    if (UserAge > 0 && UserAge < 18)
                    {
                        if (!string.IsNullOrEmpty(evt.AgeRestriction) &&
                            evt.AgeRestriction.Contains("18"))
                        {
                            return false;
                        }
                    }

                    bool isAgePickerMatch =
                        SelectedAgeGroup == "Minden"
                        || string.IsNullOrEmpty(SelectedAgeGroup)
                        || (evt.AgeRestriction?.Equals(SelectedAgeGroup,
                            StringComparison.OrdinalIgnoreCase) ?? false);

                    bool isNameMatch =
                        string.IsNullOrEmpty(SearchName)
                        || evt.EventName.Contains(SearchName, StringComparison.OrdinalIgnoreCase);

                    bool isMusicMatch =
                        SelectedMusicGenre == "Minden"
                        || string.IsNullOrEmpty(SelectedMusicGenre)
                        || (evt.MusicGenre?.Equals(SelectedMusicGenre,
                            StringComparison.OrdinalIgnoreCase) ?? false);

                    bool isDateMatch = true;
                    if (StartDateFilter.HasValue)
                        isDateMatch &= evt.StartDate.Date >= StartDateFilter.Value.Date;
                    if (EndDateFilter.HasValue)
                        isDateMatch &= evt.EndDate.Date <= EndDateFilter.Value.Date;

                    return isWithinRadius && isAgePickerMatch &&
                           isNameMatch && isMusicMatch && isDateMatch;
                }).ToList();

                FilteredEvents = new ObservableCollection<Event>(filtered);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Szűrés sikertelen: {ex.Message}", "OK");
            }
        }

        private async Task<Location> GetCurrentLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                return await Geolocation.GetLocationAsync(request);
            }
            catch { return null; }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async Task OnLikeClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;

                if (user == null)
                {
                    await DisplayAlert("Bejelentkezés szükséges", "Kérlek, jelentkezz be a kedveléshez.", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                var existing = await _supabaseClient
                    .From<Liked>()
                    .Filter("user_id", Constants.Operator.Equals, user.Id)
                    .Filter("event_id", Constants.Operator.Equals, eventId)
                    .Get();

                if (existing.Models.Any())
                {
                    await _supabaseClient
                        .From<Liked>()
                        .Filter("user_id", Constants.Operator.Equals, user.Id)
                        .Filter("event_id", Constants.Operator.Equals, eventId)
                        .Delete();

                    await DisplayAlert("Eltávolítva", "Kedvelés törölve.", "OK");
                }
                else
                {
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
                await DisplayAlert("Hiba", $"Kedvelés közben hiba történt: {ex.Message}", "OK");
            }
        }

        private async Task OnBeThereClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;

                if (user == null)
                {
                    await DisplayAlert("Bejelentkezés szükséges", "Kérlek, jelentkezz be!", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                var existing = await _supabaseClient
                    .From<BeThere>()
                    .Filter("user_id", Constants.Operator.Equals, user.Id)
                    .Filter("event_id", Constants.Operator.Equals, eventId)
                    .Get();

                if (existing.Models.Any())
                {
                    await _supabaseClient
                        .From<BeThere>()
                        .Filter("user_id", Constants.Operator.Equals, user.Id)
                        .Filter("event_id", Constants.Operator.Equals, eventId)
                        .Delete();

                    await DisplayAlert("Eltávolítva", "Részvétel törölve.", "OK");
                }
                else
                {
                    var beThere = new BeThere
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _supabaseClient.From<BeThere>().Insert(beThere);
                    await DisplayAlert("Siker", "Részvétel rögzítve!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Részvétel mentése sikertelen: {ex.Message}", "OK");
            }
        }

        private async Task ShowCreatorDetails(string creatorId, string creatorName)
        {
            try
            {
                var createdEventsResponse = await _supabaseClient
                    .From<Event>()
                    .Where(e => e.UserId == creatorId)
                    .Get();

                int createdEventsCount = createdEventsResponse.Models?.Count ?? 0;

                double avgRating = 0;

                if (createdEventsCount > 0)
                {
                    var eventIds = createdEventsResponse.Models.Select(e => e.Id).ToList();

                    var ratingsResponse = await _supabaseClient
                        .From<EventRatings>()
                        .Filter("event_id", Constants.Operator.In, eventIds)
                        .Get();

                    if (ratingsResponse.Models != null && ratingsResponse.Models.Any())
                        avgRating = ratingsResponse.Models.Average(r => r.Rating);
                }

                string msg =
                    "Keszito adatai:\n" +
                    "Keszitett esemenyek: " + createdEventsCount + "\n" +
                    "Atlagos ertekeles: " +
                    (avgRating > 0 ? avgRating.ToString("F1") : "Nincs ertekeles");

                await DisplayAlert("Keszito adatai", msg, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Adatok lekérése sikertelen: {ex.Message}", "OK");
            }
        }
    }
}
