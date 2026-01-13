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

                    _selectedLatitude = location.Latitude;
                    _selectedLongitude = location.Longitude;

                    var address = await _placeService.GetPlaceAddressAsync(location.Latitude, location.Longitude);
                    LocationEntry.Text = address;
                }
                else
                {
                    await DisplayAlert("Hiba", "Nem sikerült lekérni a jelenlegi pozíciót!", "OK");
                }
            }
            catch (FeatureNotEnabledException)
            {
                await DisplayAlert("Hiba", "A helymeghatározás ki van kapcsolva!", "OK");
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
                MusicGenrePicker.SelectedItem = null;
            }
        }

        private async void OnCreateEventClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(EventNameEntry.Text) ||
                    StartDatePicker.Date == null ||
                    EndDatePicker.Date == null ||
                    string.IsNullOrWhiteSpace(LocationEntry.Text))
                {
                    await DisplayAlert("Hiba", "Kérlek töltsd ki az összes mezőt!", "OK");
                    return;
                }

                if (CategoryPicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Kérlek válassz kategóriát!", "OK");
                    return;
                }

                if (CategoryPicker.SelectedItem.ToString() == "Koncert" &&
                    MusicGenrePicker.SelectedItem == null)
                {
                    await DisplayAlert("Hiba", "Koncert esetén zenei műfaj szükséges!", "OK");
                    return;
                }

                if (_supabaseClient.Auth.CurrentSession == null)
                {
                    await DisplayAlert("Hiba", "Nem vagy bejelentkezve!", "OK");
                    await Shell.Current.GoToAsync("MainPage");
                    return;
                }

                var user = await _supabaseClient.Auth.GetUser(_supabaseClient.Auth.CurrentSession.AccessToken);

                //  KOR LEKÉRÉSE
                var profileResponse = await _supabaseClient
                    .From<MauiApp2.Models.User>()
                    .Where(x => x.Id == user.Id)
                    .Single();

                int userAge = profileResponse?.Age ?? 0;

                
                if (userAge < 18 && AgeRestrictionPicker.SelectedItem?.ToString() == "Korhatáros (18+)")
                {
                    await DisplayAlert("Hiba",
                        "18 év alatt nem hozhatsz létre korhatáros (18+) eseményt!",
                        "OK");

                    return;
                }

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
                    MusicGenre = CategoryPicker.SelectedItem.ToString() == "Koncert"
                        ? MusicGenrePicker.SelectedItem?.ToString()
                        : null
                };

                Console.WriteLine($"Esemény mentése: {eventData.EventName}, Loc={eventData.Location}");

                var response = await _supabaseClient.From<EventInsert>()
                    .Insert(new List<EventInsert> { eventData });

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
                await DisplayAlert("Hiba",
                    $"Hiba történt: {ex.Message}",
                    "OK");
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }
    }

    
}
