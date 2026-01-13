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

        
        public int UserAge { get; set; } = 0;

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

        public List<string> AgeGroups { get; set; } =
            new List<string> { "Nincs Korhatár", "Korhatáros (18+)" };

        public string SelectedAgeGroup { get; set; } = "Nincs Korhatár";

        public SupriseMePage(Supabase.Client supabaseClient)
        {
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));

            InitializeComponent();
            BindingContext = this;

            
            Task.Run(async () =>
            {
                await LoadUserAgeAsync();
                await LoadEventsAsync();
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

                Console.WriteLine("UserAge (public.users): " + UserAge);
            }
            catch
            {
                UserAge = 0;
            }
        }

        
        private async Task LoadEventsAsync()
        {
            try
            {
                var response = await _supabaseClient.From<Event>().Get();
                _events = response.Models?.ToList() ?? new List<Event>();

                foreach (var evt in _events)
                {
                    var creatorNameResponse = await _supabaseClient.Rpc(
                        "get_user_name",
                        new { user_id = evt.UserId }
                    );

                    evt.CreatorName = creatorNameResponse?.Content ?? "Ismeretlen";
                    evt.ShowCreatorDetailsCommand =
                        new Command(async () => await ShowCreatorDetails(evt.UserId, evt.CreatorName));
                    evt.LikeCommand =
                        new Command<long>(async (eventId) => await OnLikeClicked(eventId));
                    evt.BeThereCommand =
                        new Command<long>(async (eventId) => await OnBeThereClicked(eventId));
                }

                Console.WriteLine("Loaded events: " + _events.Count);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", "Nem sikerült betölteni az eseményeket.", "OK");
            }
        }


        
        private async void OnSurpriseButtonClicked(object sender, EventArgs e)
        {
            try
            {
               
                if (string.IsNullOrWhiteSpace(RadiusEntry.Text) ||
                    !double.TryParse(RadiusEntry.Text, out double radius) ||
                    radius <= 0)
                {
                    await DisplayAlert("Hiba", "Adj meg egy érvényes körzetet!", "OK");
                    return;
                }

                if (AgeLimitPicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Válassz egy korhatárt!", "OK");
                    return;
                }

                string selectedAge = AgeLimitPicker.SelectedItem.ToString();
                bool allow18PlusFromPicker = selectedAge == "Korhatáros (18+)";

                
                var location = await GetCurrentLocation();
                if (location == null)
                {
                    await DisplayAlert("Hiba", "Nem sikerült meghatározni a pozíciót.", "OK");
                    return;
                }

                double userLatitude = location.Latitude;
                double userLongitude = location.Longitude;

                
                var searchDate = DatePicker.Date.Date;
                var searchStart = searchDate;
                var searchEnd = searchDate.AddDays(1).AddTicks(-1);

               
                var filtered = _events.Where(evt =>
                {
                  
                    var eventStart = evt.StartDate.Date.Add(evt.StartTime);
                    var eventEnd = evt.EndDate.Date.Add(evt.EndTime);
                    bool isDateMatch = searchStart <= eventEnd && searchEnd >= eventStart;

                   
                    bool isRadiusMatch =
                        evt.Latitude.HasValue &&
                        evt.Longitude.HasValue &&
                        CalculateDistance(userLatitude, userLongitude, evt.Latitude.Value, evt.Longitude.Value) <= radius;

                    
                    bool matchesPicker =
                        allow18PlusFromPicker ? true : evt.AgeRestriction == "Nincs korhatár";

                   
                    if (UserAge > 0 && UserAge < 18)
                    {
                        if (!string.IsNullOrEmpty(evt.AgeRestriction) &&
                            evt.AgeRestriction.Contains("18"))
                        {
                            return false; 
                        }
                    }

                    return isDateMatch && isRadiusMatch && matchesPicker;
                }).ToList();

                if (filtered.Count == 0)
                {
                    SelectedEvent = null;
                    HasEvent = false;
                    HasNoEvent = true;
                    await DisplayAlert("Nincs találat", "Nincs esemény a szûrés alapján.", "OK");
                    return;
                }

               
                Random rand = new Random();
                SelectedEvent = filtered[rand.Next(filtered.Count)];
                HasEvent = true;
                HasNoEvent = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", ex.Message, "OK");
            }
        }


        
        private async Task<Location> GetCurrentLocation()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                        return null;
                }

                var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                return await Geolocation.GetLocationAsync(req);
            }
            catch
            {
                return null;
            }
        }


        
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async Task OnLikeClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Jelentkezz be!", "OK");
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

                    await DisplayAlert("Törölve", "Kedvelés törölve!", "OK");
                }
                else
                {
                    await _supabaseClient.From<Liked>().Insert(new Liked
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await DisplayAlert("OK", "Kedvelve!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", ex.Message, "OK");
            }
        }


        private async Task OnBeThereClicked(long eventId)
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Jelentkezz be!", "OK");
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

                    await DisplayAlert("Törölve", "Részvétel törölve!", "OK");
                }
                else
                {
                    await _supabaseClient.From<BeThere>().Insert(new BeThere
                    {
                        UserId = user.Id,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await DisplayAlert("OK", "Rögzítve!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", ex.Message, "OK");
            }
        }


        private async Task ShowCreatorDetails(string creatorId, string creatorName)
        {
            try
            {
                var createdEvents = await _supabaseClient
                    .From<Event>()
                    .Where(e => e.UserId == creatorId)
                    .Get();

                int count = createdEvents.Models?.Count ?? 0;

                double avg = 0;
                if (count > 0)
                {
                    var ids = createdEvents.Models.Select(e => e.Id).ToList();
                    var ratings = await _supabaseClient
                        .From<EventRatings>()
                        .Filter("event_id", Constants.Operator.In, ids)
                        .Get();

                    if (ratings.Models.Any())
                        avg = ratings.Models.Average(r => r.Rating);
                }

                await DisplayAlert(
                    "Készítõ adatai",
                    "Események: " + count +
                    "\nÁtlag: " + (avg > 0 ? avg.ToString("F1") : "Nincs értékelés"),
                    "OK"
                );
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", ex.Message, "OK");
            }
        }
    }
}
