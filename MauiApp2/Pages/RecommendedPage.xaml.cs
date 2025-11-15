using Supabase;
using Supabase.Postgrest; // Hozzáadjuk a Postgrest névteret az Operator enum miatt
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;

namespace MauiApp2.Pages
{
    public partial class RecommendedPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ObservableCollection<Event> _recommendedEvents;

        public ObservableCollection<Event> RecommendedEvents
        {
            get => _recommendedEvents;
            set
            {
                _recommendedEvents = value;
                OnPropertyChanged();
                HasRecommendedEvents = _recommendedEvents.Any();
                HasNoRecommendedEvents = !_recommendedEvents.Any();
                OnPropertyChanged(nameof(HasRecommendedEvents));
                OnPropertyChanged(nameof(HasNoRecommendedEvents));
            }
        }

        public bool HasRecommendedEvents { get; set; }
        public bool HasNoRecommendedEvents { get; set; }

        public RecommendedPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _recommendedEvents = new ObservableCollection<Event>();
            BindingContext = this;
        }

        private async void OnRecommendClicked(object sender, EventArgs e)
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

                // Betöltjük a felhasználó kedvelt eseményeinek kategóriáit
                var likedResponse = await _supabaseClient
                    .From<Liked>()
                    .Where(l => l.UserId == user.Id)
                    .Get();

                if (likedResponse.Models == null || !likedResponse.Models.Any())
                {
                    RecommendedEvents = new ObservableCollection<Event>();
                    return;
                }

                var likedEventIds = likedResponse.Models.Select(l => l.EventId).ToList();
                var likedEventsResponse = await _supabaseClient
                    .From<Event>()
                    .Filter("id", Constants.Operator.In, likedEventIds)
                    .Get();

                if (likedEventsResponse.Models == null || !likedEventsResponse.Models.Any())
                {
                    RecommendedEvents = new ObservableCollection<Event>();
                    return;
                }

                var likedCategories = likedEventsResponse.Models
                    .Where(e => !string.IsNullOrEmpty(e.Category))
                    .Select(e => e.Category)
                    .Distinct()
                    .ToList();

                if (!likedCategories.Any())
                {
                    RecommendedEvents = new ObservableCollection<Event>();
                    return;
                }

                // Betöltjük az összes eseményt, és szûrjük a kategóriák alapján
                var allEventsResponse = await _supabaseClient
                    .From<Event>()
                    .Filter("category", Constants.Operator.In, likedCategories)
                    .Get();

                if (allEventsResponse.Models != null)
                {
                    var recommendedEvents = allEventsResponse.Models
                        .Where(e => !likedEventIds.Contains(e.Id)) // Kizárjuk a már kedvelt eseményeket
                        .ToList();

                    foreach (var evt in recommendedEvents)
                    {
                        evt.LikeCommand = new Command<long>((eventId) => OnLikeClickedAsync(eventId));
                        evt.BeThereCommand = new Command<long>((eventId) => OnBeThereClickedAsync(eventId));
                    }

                    RecommendedEvents = new ObservableCollection<Event>(recommendedEvents);
                }
                else
                {
                    RecommendedEvents = new ObservableCollection<Event>();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba az ajánlott események betöltése közben: {ex.Message}", "OK");
            }
        }

        private async void OnLikeClickedAsync(long eventId)
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
                    .Where(l => l.UserId == user.Id && l.EventId == eventId)
                    .Get();

                if (existingLike.Models.Any())
                {
                    // Ha már kedvelte, töröljük a kedvelést
                    await _supabaseClient
                        .From<Liked>()
                        .Where(l => l.UserId == user.Id && l.EventId == eventId)
                        .Delete();
                    await DisplayAlert("Siker", "Kedvelés törölve!", "OK");

                    // Frissítjük az ajánlott események listáját
                    var eventToRemove = RecommendedEvents.FirstOrDefault(e => e.Id == eventId);
                    if (eventToRemove != null)
                    {
                        RecommendedEvents.Remove(eventToRemove);
                    }
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

                    // Frissítjük az ajánlott események listáját
                    var eventToMove = RecommendedEvents.FirstOrDefault(e => e.Id == eventId);
                    if (eventToMove != null)
                    {
                        RecommendedEvents.Remove(eventToMove);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a kedvelés közben: {ex.Message}", "OK");
            }
        }

        private async void OnBeThereClickedAsync(long eventId)
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
                    .Where(b => b.UserId == user.Id && b.EventId == eventId)
                    .Get();

                if (existingBeThere.Models.Any())
                {
                    // Ha már jelezte, töröljük a jelzést
                    await _supabaseClient
                        .From<BeThere>()
                        .Where(b => b.UserId == user.Id && b.EventId == eventId)
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
    }
}