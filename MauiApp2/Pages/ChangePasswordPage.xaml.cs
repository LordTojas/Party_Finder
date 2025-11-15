using Supabase;
using Supabase.Gotrue;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace MauiApp2.Pages
{
    public partial class ChangePasswordPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;

        public ChangePasswordPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        }

        private async void OnSavePasswordButtonClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NewPasswordEntry.Text) || NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
                {
                    await DisplayAlert("Hiba", "A jelszavak nem egyeznek, vagy üresen hagytad!", "OK");
                    return;
                }

                // Jelszó módosítása a Supabase Auth-ban
                var userAttributes = new UserAttributes { Password = NewPasswordEntry.Text };
                await _supabaseClient.Auth.Update(userAttributes);

                await DisplayAlert("Siker", "Jelszó sikeresen módosítva!", "OK");
                await Navigation.PopAsync(); // Visszatérés a SettingsPage-re
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a jelszó módosítása közben: {ex.Message}", "OK");
            }
        }
    }
}