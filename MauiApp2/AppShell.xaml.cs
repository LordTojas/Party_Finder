using MauiApp2.Pages;
using Supabase;
using Microsoft.Maui.Controls;

namespace MauiApp2
{
    public partial class AppShell : Shell
    {
        private readonly Supabase.Client _supabaseClient;

        public AppShell(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));

            // Manuális route regisztráció
            Routing.RegisterRoute("LoginPage", typeof(LoginPage));
            Routing.RegisterRoute("MainPage", typeof(MainPage));
            Routing.RegisterRoute("HomePage", typeof(HomePage));
            Routing.RegisterRoute("EventCreationPage", typeof(EventCreationPage));
            Routing.RegisterRoute("EventSearchPage", typeof(EventSearchPage));
            Routing.RegisterRoute("CurrentPage", typeof(CurrentPage));
            Routing.RegisterRoute("CommunityPage", typeof(CommunityPage));
            Routing.RegisterRoute("PopularPage", typeof(PopularPage));
            Routing.RegisterRoute("RecommendedPage", typeof(RecommendedPage));
            Routing.RegisterRoute("SupriseMePage", typeof(SupriseMePage));
            Routing.RegisterRoute("SavedPage", typeof(SavedPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("MyEventsPage", typeof(MyEventsPage));
            Routing.RegisterRoute("ChangePasswordPage", typeof(ChangePasswordPage));

            // Supabase inicializálás és belépés-ellenőrzés
            InitializeSupabaseAsync();
        }

        private async void InitializeSupabaseAsync()
        {
            try
            {
                await _supabaseClient.InitializeAsync();
                Console.WriteLine("Supabase inicializálva");
                await CheckUserLoginStatus();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a Supabase inicializálása során: {ex.Message}");
                await GoToAsync("WelcomePage");
            }
        }

        private async Task CheckUserLoginStatus()
        {
            try
            {
                var session = _supabaseClient.Auth.CurrentSession;
                if (session == null || session.User == null)
                {
                    Console.WriteLine("Nincs bejelentkezve – maradunk WelcomePage-en");
                  
                }
                else
                {
                    Console.WriteLine("Be van jelentkezve – navigálunk a HomePage-re");
                    await GoToAsync("HomePage");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a bejelentkezési állapot ellenőrzésekor: {ex.Message}");
                await GoToAsync("WelcomePage");
            }
        }
    }
}