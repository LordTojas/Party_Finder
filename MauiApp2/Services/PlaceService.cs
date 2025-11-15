using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MauiApp2.Services
{
    public class PlaceSuggestion
    {
        public string Description { get; set; }
        public string PlaceId { get; set; }
    }

    public class PlaceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "AIzaSyAD-3jn6Tw-cuiLDeXd4SpeuogH7KhFIuI"; // Az API kulcsod

        public PlaceService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<PlaceSuggestion>> GetPlaceSuggestionsAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<PlaceSuggestion>();

            // Google Places Autocomplete API URL
            var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(input)}&key={_apiKey}&types=geocode";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                var suggestions = new List<PlaceSuggestion>();

                if (root.GetProperty("status").GetString() == "OK")
                {
                    var predictions = root.GetProperty("predictions");
                    foreach (var prediction in predictions.EnumerateArray())
                    {
                        suggestions.Add(new PlaceSuggestion
                        {
                            Description = prediction.GetProperty("description").GetString(),
                            PlaceId = prediction.GetProperty("place_id").GetString()
                        });
                    }
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a helyjavaslatok lekérése közben: {ex.Message}");
                return new List<PlaceSuggestion>();
            }
        }

        public async Task<(double Latitude, double Longitude)?> GetPlaceCoordinatesAsync(string placeId)
        {
            if (string.IsNullOrWhiteSpace(placeId))
                return null;

            // Google Places Details API URL
            var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&fields=geometry&key={_apiKey}";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                if (root.GetProperty("status").GetString() == "OK")
                {
                    var location = root.GetProperty("result").GetProperty("geometry").GetProperty("location");
                    var lat = location.GetProperty("lat").GetDouble();
                    var lng = location.GetProperty("lng").GetDouble();
                    return (lat, lng);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a hely koordinátáinak lekérése közben: {ex.Message}");
                return null;
            }
        }

        // Új metódus: Koordinátákból cím lekérdezése
        public async Task<string> GetPlaceAddressAsync(double latitude, double longitude)
        {
            // Google Maps Geocoding API URL
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={_apiKey}";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                Console.WriteLine($"GetPlaceAddressAsync: Válasz a Google Maps API-tól: {response}");

                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                var status = root.GetProperty("status").GetString();
                Console.WriteLine($"GetPlaceAddressAsync: API status: {status}");

                if (status == "OK")
                {
                    var results = root.GetProperty("results");
                    if (results.EnumerateArray().MoveNext()) // Ellenőrizzük, hogy van-e legalább egy eredmény
                    {
                        var firstResult = results.EnumerateArray().First();
                        var address = firstResult.GetProperty("formatted_address").GetString();
                        Console.WriteLine($"GetPlaceAddressAsync: Lekért cím: {address}");
                        return address;
                    }
                    else
                    {
                        Console.WriteLine("GetPlaceAddressAsync: Nem található cím az adott koordinátákhoz.");
                        return "Ismeretlen cím";
                    }
                }
                else
                {
                    Console.WriteLine($"GetPlaceAddressAsync: API hiba, status: {status}");
                    return "Ismeretlen cím";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPlaceAddressAsync: Hiba a cím lekérdezése közben: {ex.Message}");
                return "Ismeretlen cím";
            }
        }
    }
}