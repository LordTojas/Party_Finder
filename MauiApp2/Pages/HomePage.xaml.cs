using System.ComponentModel;
using Microsoft.Maui.Controls;
using Supabase;

namespace MauiApp2.Pages;

public partial class HomePage : ContentPage, INotifyPropertyChanged
{
    private readonly Supabase.Client _supabaseClient;
    private Color _backgroundColor;
    private readonly Color[] _colors = new[] { Colors.LightBlue, Colors.LightGreen, Colors.LightPink, Colors.LightYellow };
    private int _colorIndex = 0;

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    public HomePage(Supabase.Client supabaseClient)
    {
        _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));

        InitializeComponent();
        BindingContext = this;
        BackgroundColor = _colors[_colorIndex];
        StartBackgroundColorCycle();

        CheckUserSession();
    }

    private async void CheckUserSession()
    {
        try
        {
            var session = _supabaseClient.Auth.CurrentSession;
            if (session != null && session.User != null)
            {
                Console.WriteLine($"Bejelentkezett felhasználó: {session.User.Email}");
            }
            else
            {
                Console.WriteLine("Nincs bejelentkezve felhasználó");
                await Shell.Current.GoToAsync("WelcomePage");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hiba a felhasználói munkamenet ellenõrzésekor: {ex.Message}");
        }
    }

    private async void StartBackgroundColorCycle()
    {
        while (true)
        {
            await Task.Delay(3000);
            _colorIndex = (_colorIndex + 1) % _colors.Length;
            BackgroundColor = _colors[_colorIndex];
        }
    }

    private async void OnButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await button.ScaleTo(0.95, 100, Easing.SinOut);
            await button.ScaleTo(1.0, 100, Easing.SinIn);

            try
            {
                switch (button.Text)
                {
                    case "Beállítások":
                        await Shell.Current.GoToAsync("SettingsPage");
                        break;
                    case "Keresés":
                        await Shell.Current.GoToAsync("EventSearchPage");
                        break;
                    case "Meglepetés":
                        await Shell.Current.GoToAsync("SupriseMePage");
                        break;
                    case "Kedvencek":
                        await Shell.Current.GoToAsync("PopularPage");
                        break;
                    case "Közösség":
                        await Shell.Current.GoToAsync("CommunityPage");
                        break;
                    case "Csak Neked":
                        await Shell.Current.GoToAsync("RecommendedPage");
                        break;
                    case "Mentett Események":
                        await Shell.Current.GoToAsync("SavedPage");
                        break;
                    case "Aktuális események":
                        await Shell.Current.GoToAsync("CurrentPage");
                        break;
                    case "Eseményeim":
                        await Shell.Current.GoToAsync("MyEventsPage");
                        break;
                    case "Esemény létrehozása":
                        await Shell.Current.GoToAsync("EventCreationPage");
                        break;
                    case "Kijelentkezés":
                        await _supabaseClient.Auth.SignOut();
                        await Shell.Current.GoToAsync("///WelcomePage");
                        break;
                    default:
                        await DisplayAlert("Hiba", "Nem ismert gomb!", "OK");
                        break;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Navigációs hiba: {ex.Message}", "OK");
            }
        }
    }
}