using Microsoft.Maui.Devices.Sensors;
using Maui.GoogleMaps;
using MauiApp2.Services;
using Supabase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MauiApp2.Models;
using System.ComponentModel;

namespace MauiApp2.Pages
{
    public partial class EditEventPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly PlaceService _placeService;
        private Event _event;
        private List<PlaceSuggestion> _currentSuggestions;

        // Binding tulajdonságok
        public string EventName { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan EndTime { get; set; }
        public string AgeRestriction { get; set; }
        public string Category { get; set; }
        public string MusicGenre { get; set; }
        public List<string> AgeRestrictions { get; } = new List<string> { "Nincs Korhatár", "Korhatáros (18+)" };
        public List<string> Categories { get; } = new List<string> { "Buli/Házibuli", "Hivatalos esemény", "Koncert" };
        public List<string> MusicGenres { get; } = new List<string> { "Rock", "Pop", "Jazz", "Klasszikus", "Hip-Hop", "Elektronikus", "Egyéb" };
        public List<PlaceSuggestion> CurrentSuggestions
        {
            get => _currentSuggestions;
            set
            {
                _currentSuggestions = value;
                OnPropertyChanged(nameof(CurrentSuggestions));
            }
        }

        private double? _selectedLatitude;
        private double? _selectedLongitude;
        private bool _isMusicGenreVisible;

        public bool IsMusicGenreVisible
        {
            get => _isMusicGenreVisible;
            set
            {
                _isMusicGenreVisible = value;
                OnPropertyChanged(nameof(IsMusicGenreVisible));
                if (!_isMusicGenreVisible)
                {
                    MusicGenre = null;
                    OnPropertyChanged(nameof(MusicGenre));
                }
            }
        }

        public Command SaveCommand { get; }

        public EditEventPage(Supabase.Client supabaseClient, PlaceService placeService, Event evt)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
            _placeService = placeService;
            _event = evt;
            _currentSuggestions = new List<PlaceSuggestion>();

            // Az esemény meglévõ adataival töltjük fel az ûrlapot
            EventName = evt.EventName;
            Location = evt.Location;
            StartDate = evt.StartDate;
            StartTime = evt.StartTime;
            EndDate = evt.EndDate;
            EndTime = evt.EndTime;
            AgeRestriction = evt.AgeRestriction;
            Category = evt.Category;
            MusicGenre = evt.MusicGenre;
            _selectedLatitude = evt.Latitude;
            _selectedLongitude = evt.Longitude;

            SaveCommand = new Command(async () => await SaveEvent());

            // Zenei mûfaj láthatóságának beállítása
            IsMusicGenreVisible = Category == "Koncert";

            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RequestLocationPermission();
            await ShowCurrentLocation();
        }

        private async Task RequestLocationPermission()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Hiba", "A helymeghatározási engedély szükséges a jelenlegi pozíció használatához!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba az engedélykérés során: {ex.Message}", "OK");
            }
        }

        private async Task ShowCurrentLocation()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Hiba", "Kérjük, engedélyezze a helymeghatározást a Beállításokban!", "OK");
                    return;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                if (location != null)
                {
                    var position = new Position(location.Latitude, location.Longitude);
                    EventMap.MoveToRegion(MapSpan.FromCenterAndRadius(position, Distance.FromMeters(500)));

                    EventMap.Pins.Clear();
                    EventMap.Pins.Add(new Pin
                    {
                        Position = position,
                        Label = "Jelenlegi pozíció"
                    });

                    LocationEntry.Text = "Jelenlegi pozíció";
                    _selectedLatitude = location.Latitude;
                    _selectedLongitude = location.Longitude;

                    var address = await _placeService.GetPlaceAddressAsync(location.Latitude, location.Longitude);
                    if (!string.IsNullOrEmpty(address))
                    {
                        LocationEntry.Text = address;
                    }
                }
                else
                {
                    await DisplayAlert("Hiba", "Nem sikerült lekérni a jelenlegi pozíciót. Kérjük, ellenõrizze a helymeghatározási beállításokat!", "OK");
                }
            }
            catch (FeatureNotEnabledException)
            {
                await DisplayAlert("Hiba", "A helymeghatározás ki van kapcsolva. Kérjük, engedélyezze a Beállításokban!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Nem sikerült lekérni a pozíciót: {ex.Message}", "OK");
            }
        }

        private async void OnUseCurrentLocationClicked(object sender, EventArgs e)
        {
            await ShowCurrentLocation();
        }

        private async void OnLocationTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue;

            if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 3)
            {
                SuggestionsList.IsVisible = false;
                return;
            }

            _currentSuggestions = await _placeService.GetPlaceSuggestionsAsync(searchText);
            SuggestionsList.ItemsSource = _currentSuggestions;
            SuggestionsList.IsVisible = _currentSuggestions.Count > 0;
        }

        private async void OnSuggestionSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is PlaceSuggestion selectedSuggestion)
            {
                var coordinates = await _placeService.GetPlaceCoordinatesAsync(selectedSuggestion.PlaceId);

                if (coordinates.HasValue)
                {
                    var (latitude, longitude) = coordinates.Value;
                    var position = new Position(latitude, longitude);
                    EventMap.MoveToRegion(MapSpan.FromCenterAndRadius(position, Distance.FromKilometers(1)));

                    EventMap.Pins.Clear();
                    EventMap.Pins.Add(new Pin
                    {
                        Position = position,
                        Label = selectedSuggestion.Description
                    });

                    SuggestionsList.IsVisible = false;
                    LocationEntry.Text = selectedSuggestion.Description;

                    _selectedLatitude = latitude;
                    _selectedLongitude = longitude;
                }
            }
        }

        private void OnCategoryPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedCategory = CategoryPicker.SelectedItem?.ToString();
            Category = selectedCategory; // Frissítjük a Category tulajdonságot
            IsMusicGenreVisible = selectedCategory == "Koncert";
        }

        private async Task SaveEvent()
        {
            try
            {
                // Ellenõrizzük, hogy minden kötelezõ mezõ ki van-e töltve
                if (string.IsNullOrWhiteSpace(EventName) ||
                    string.IsNullOrWhiteSpace(Location) ||
                    string.IsNullOrWhiteSpace(AgeRestriction) ||
                    string.IsNullOrWhiteSpace(Category))
                {
                    await DisplayAlert("Hiba", "Kérlek, tölts ki minden mezõt!", "OK");
                    return;
                }

                if (Category == "Koncert" && string.IsNullOrWhiteSpace(MusicGenre))
                {
                    await DisplayAlert("Hiba", "Kérlek, válassz egy zenei mûfajt a koncerthez!", "OK");
                    return;
                }

                // Létrehozunk egy EventInsert objektumot az Event adataiból
                var eventInsert = new EventInsert
                {
                    Id = _event.Id,
                    UserId = _event.UserId,
                    EventName = EventName,
                    Description = _event.Description,
                    AgeRestriction = AgeRestriction,
                    StartDate = StartDate,
                    StartTime = StartTime,
                    EndDate = EndDate,
                    EndTime = EndTime,
                    Location = Location,
                    Latitude = _selectedLatitude,
                    Longitude = _selectedLongitude,
                    CreatedAt = _event.CreatedAt,
                    Category = Category,
                    MusicGenre = Category == "Koncert" ? MusicGenre : null
                };

                // Naplózzuk az eventInsert objektumot hibakeresés céljából
                Console.WriteLine($"Frissítendõ esemény: Id={eventInsert.Id}, EventName={eventInsert.EventName}, Category={eventInsert.Category}, UserId={eventInsert.UserId}");

                // Frissítjük a Supabase táblában az EventInsert osztály használatával
                var response = await _supabaseClient
                    .From<EventInsert>()
                    .Where(e => e.Id == eventInsert.Id)
                    .Set(e => e.EventName, eventInsert.EventName)
                    .Set(e => e.Location, eventInsert.Location)
                    .Set(e => e.StartDate, eventInsert.StartDate)
                    .Set(e => e.StartTime, eventInsert.StartTime)
                    .Set(e => e.EndDate, eventInsert.EndDate)
                    .Set(e => e.EndTime, eventInsert.EndTime)
                    .Set(e => e.AgeRestriction, eventInsert.AgeRestriction)
                    .Set(e => e.Category, eventInsert.Category)
                    .Set(e => e.MusicGenre, eventInsert.MusicGenre)
                    .Set(e => e.Latitude, eventInsert.Latitude)
                    .Set(e => e.Longitude, eventInsert.Longitude)
                    .Update();

                // Ellenõrizzük, hogy a frissítés sikeres volt-e
                if (response.Models == null || !response.Models.Any())
                {
                    throw new Exception("A frissítés nem sikerült, a Supabase nem adott vissza frissített rekordot.");
                }

                // Frissítjük az Event objektumot is, hogy az UI-ban is frissüljenek az adatok
                _event.EventName = eventInsert.EventName;
                _event.Location = eventInsert.Location;
                _event.StartDate = eventInsert.StartDate;
                _event.StartTime = eventInsert.StartTime;
                _event.EndDate = eventInsert.EndDate;
                _event.EndTime = eventInsert.EndTime;
                _event.AgeRestriction = eventInsert.AgeRestriction;
                _event.Category = eventInsert.Category;
                _event.MusicGenre = eventInsert.MusicGenre;
                _event.Latitude = eventInsert.Latitude;
                _event.Longitude = eventInsert.Longitude;

                // Értesítjük az eseményt a változásról, hogy a státusz frissüljön
                _event.OnPropertyChanged(nameof(_event.StatusText));
                _event.OnPropertyChanged(nameof(_event.StatusColor));

                await DisplayAlert("Siker", "Esemény sikeresen módosítva!", "OK");
                await Navigation.PopAsync(); // Visszatérünk az elõzõ oldalra
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a mentés során: {ex.Message}");
                await DisplayAlert("Hiba", $"Hiba az esemény módosítása közben: {ex.Message}", "OK");
            }
        }

        // INotifyPropertyChanged implementáció
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}