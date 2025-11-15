using Supabase;
using Supabase.Postgrest; // Hozzáadjuk a Postgrest névteret
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;

namespace MauiApp2.Pages
{
    public partial class PopularPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ObservableCollection<Event> _likedEvents;

        public ObservableCollection<Event> LikedEvents
        {
            get => _likedEvents;
            set
            {
                _likedEvents = value;
                OnPropertyChanged();
                HasLikedEvents = _likedEvents.Any();
                HasNoLikedEvents = !_likedEvents.Any();
                OnPropertyChanged(nameof(HasLikedEvents));
                OnPropertyChanged(nameof(HasNoLikedEvents));
            }
        }

        public bool HasLikedEvents { get; set; }
        public bool HasNoLikedEvents { get; set; }

        public PopularPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _likedEvents = new ObservableCollection<Event>();
            BindingContext = this;
            LoadLikedEventsAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadLikedEventsAsync();
        }

        private async void LoadLikedEventsAsync()
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

                // Betöltjük a felhasználó kedvelt eseményeit a Liked táblából
                var likedResponse = await _supabaseClient
                    .From<Liked>()
                    .Where(l => l.UserId == user.Id)
                    .Get();

                if (likedResponse.Models != null && likedResponse.Models.Any())
                {
                    var likedEventIds = likedResponse.Models.Select(l => l.EventId).ToList();
                    var eventsResponse = await _supabaseClient
                        .From<Event>()
                        .Filter("id", Constants.Operator.In, likedEventIds)
                        .Get();

                    if (eventsResponse.Models != null)
                    {
                        LikedEvents = new ObservableCollection<Event>(eventsResponse.Models);
                    }
                    else
                    {
                        LikedEvents = new ObservableCollection<Event>();
                    }
                }
                else
                {
                    LikedEvents = new ObservableCollection<Event>();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a kedvelt események betöltése közben: {ex.Message}", "OK");
            }
        }
    }
}