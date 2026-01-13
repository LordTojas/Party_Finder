using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;
using System.Linq; // Ez kell a .FirstOrDefault() és .Select() miatt

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
                UpdateVisibility(); // Lista frissítésekor ellenõrizzük, kell-e az üres üzenet
            }
        }

        // Ezek vezérlik, hogy látszik-e a lista vagy az "üres" felirat
        public bool HasLikedEvents { get; set; }
        public bool HasNoLikedEvents { get; set; }

        public PopularPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _likedEvents = new ObservableCollection<Event>();
            BindingContext = this;

            // Elsõ betöltés
            LoadLikedEventsAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Minden megjelenéskor frissítünk, hátha máshol kedveltünk valamit
            LoadLikedEventsAsync();
        }

        private void UpdateVisibility()
        {
            HasLikedEvents = _likedEvents != null && _likedEvents.Any();
            HasNoLikedEvents = !HasLikedEvents;

            // Értesítjük a felületet a változásról
            OnPropertyChanged(nameof(HasLikedEvents));
            OnPropertyChanged(nameof(HasNoLikedEvents));
        }

        private async void LoadLikedEventsAsync()
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    // Ha nincs bejelentkezve, üres lista
                    LikedEvents = new ObservableCollection<Event>();
                    return;
                }

                // 1. Lekérjük a LIKED táblából, hogy miket kedvelt a user
                var likedResponse = await _supabaseClient
                    .From<Liked>()
                    .Where(l => l.UserId == user.Id)
                    .Get();

                if (likedResponse.Models != null && likedResponse.Models.Any())
                {
                    // Kigyûjtjük az Event ID-kat
                    var likedEventIds = likedResponse.Models.Select(l => l.EventId).ToList();

                    // 2. Lekérjük a konkrét eseményeket az EVENT táblából az ID-k alapján
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

        // === EZ A FÜGGVÉNY TÖRLI A KEDVELÉST ===
        private async void OnRemoveClicked(object sender, EventArgs e)
        {
            // Biztonságos ID konverzió (string/int/long kezelése)
            if (sender is Button button && long.TryParse(button.CommandParameter?.ToString(), out long eventIdToRemove))
            {
                // Megerõsítés kérése
                bool answer = await DisplayAlert("Eltávolítás", "Biztosan kiveszed a kedvencek közül?", "Igen", "Nem");
                if (!answer) return;

                try
                {
                    var user = _supabaseClient.Auth.CurrentUser;
                    if (user == null) return;

                    // 1. LÉPÉS: Törlés a 'Liked' táblából (ez a kapcsolat, nem az esemény!)
                    await _supabaseClient
                        .From<Liked>()
                        .Where(x => x.UserId == user.Id && x.EventId == eventIdToRemove)
                        .Delete();

                    // 2. LÉPÉS: Frissítjük a helyi listát (hogy eltûnjön a képernyõrõl)
                    var itemToRemove = LikedEvents.FirstOrDefault(x => x.Id == eventIdToRemove);
                    if (itemToRemove != null)
                    {
                        LikedEvents.Remove(itemToRemove);
                        UpdateVisibility(); // Ha ez volt az utolsó, jelenjen meg az üres üzenet
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Hiba", $"Nem sikerült a mûvelet: {ex.Message}", "OK");
                }
            }
        }
    }
}