using Microsoft.Maui.Devices.Sensors;
using Maui.GoogleMaps;
using MauiApp2.Services;
using Supabase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MauiApp2.Models;
using System.ComponentModel;

namespace MauiApp2.Pages
{
    public partial class EventCreationPage : BasePage, INotifyPropertyChanged
    {
        private readonly PlaceService _placeService;
        private readonly Supabase.Client _supabaseClient;
        private List<PlaceSuggestion> _currentSuggestions;
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
            }
        }

        public EventCreationPage(PlaceService placeService, Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _placeService = placeService;
            _supabaseClient = supabaseClient;
            _currentSuggestions = new List<PlaceSuggestion>();
            BindingContext = this; // A bindinghoz szükséges
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

                    _selectedLatitude = location.Latitude;
                    _selectedLongitude = location.Longitude;

                    // Lekérjük a pontos címet a koordináták alapján
                    var address = await _placeService.GetPlaceAddressAsync(location.Latitude, location.Longitude);
                    LocationEntry.Text = address; // A pontos címet állítjuk be
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

        private async void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PlaceSuggestion selectedSuggestion)
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
            IsMusicGenreVisible = selectedCategory == "Koncert";
            if (!IsMusicGenreVisible)
            {
                MusicGenrePicker.SelectedItem = null; // Töröljük a kiválasztást, ha nem Koncert
            }
        }

        private async void OnCreateEventClicked(object sender, EventArgs e)
        {
            try
            {
                // Ellenõrizzük, hogy minden kötelezõ mezõ ki van-e töltve
                if (string.IsNullOrWhiteSpace(EventNameEntry.Text) ||
                    StartDatePicker.Date == null ||
                    EndDatePicker.Date == null ||
                    string.IsNullOrWhiteSpace(LocationEntry.Text))
                {
                    await DisplayAlert("Hiba", "Kérlek töltsd ki az összes kötelezõ mezõt!", "OK");
                    return;
                }

                if (CategoryPicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, válassz egy kategóriát!", "OK");
                    return;
                }

                if (CategoryPicker.SelectedItem.ToString() == "Koncert" && MusicGenrePicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Kérlek, válassz egy zenei mûfajt a koncerthez!", "OK");
                    return;
                }

                // Ellenõrizzük, hogy a felhasználó be van-e jelentkezve
                if (_supabaseClient.Auth.CurrentSession == null)
                {
                    await DisplayAlert("Hiba", "Nem vagy bejelentkezve!", "OK");
                    await Shell.Current.GoToAsync("MainPage");
                    return;
                }

                // Bejelentkezett felhasználó lekérése
                var user = await _supabaseClient.Auth.GetUser(_supabaseClient.Auth.CurrentSession.AccessToken);
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Nem sikerült lekérni a felhasználói adatokat!", "OK");
                    await Shell.Current.GoToAsync("MainPage");
                    return;
                }

                // Esemény adatainak összegyûjtése az EventInsert osztállyal
                var eventData = new EventInsert
                {
                    UserId = user.Id,
                    EventName = EventNameEntry.Text,
                    Description = EventDescriptionEditor.Text,
                    AgeRestriction = AgeRestrictionPicker.SelectedItem?.ToString(),
                    StartDate = StartDatePicker.Date,
                    StartTime = StartTimePicker.Time,
                    EndDate = EndDatePicker.Date,
                    EndTime = EndTimePicker.Time,
                    Location = LocationEntry.Text,
                    Latitude = _selectedLatitude,
                    Longitude = _selectedLongitude,
                    CreatedAt = DateTime.UtcNow,
                    Category = CategoryPicker.SelectedItem.ToString(),
                    MusicGenre = CategoryPicker.SelectedItem.ToString() == "Koncert" ? MusicGenrePicker.SelectedItem?.ToString() : null
                };

                // Naplózzuk az esemény adatait hibakeresés céljából
                Console.WriteLine($"Esemény mentése: EventName={eventData.EventName}, Description={eventData.Description}, Location={eventData.Location}");

                // Adatok mentése a Supabase adatbázisba egy egyelemû listában
                var response = await _supabaseClient.From<EventInsert>().Insert(new List<EventInsert> { eventData });

                if (response.Models != null && response.Models.Count > 0)
                {
                    await DisplayAlert("Siker", "Esemény létrehozva!", "OK");
                    await Shell.Current.GoToAsync("HomePage");
                }
                else
                {
                    await DisplayAlert("Hiba", "Nem sikerült létrehozni az eseményt!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba történt az esemény létrehozása közben: {ex.Message}", "OK");
            }
        }

        // INotifyPropertyChanged implementáció a bindinghoz
        public new event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}