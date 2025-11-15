using Supabase;
using Microsoft.Maui.Controls;
using MauiApp2.Models;
using Microsoft.Maui.Authentication;
using Supabase.Gotrue;

namespace MauiApp2.Pages
{
    public partial class LoginPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;

        public LoginPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;
        }

        // Klasszikus felhasználónév/jelszó login
        private async void OnLoginClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
            {
                ShowError("A felhasználónév nem lehet üres.");
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                ShowError("A jelszó nem lehet üres.");
                return;
            }

            try
            {
                var userResponse = await _supabaseClient
                    .From<MauiApp2.Models.User>()
                    .Where(x => x.Username == UsernameEntry.Text)
                    .Single();

                if (userResponse == null)
                {
                    ShowError("Hibás felhasználónév vagy jelszó.");
                    return;
                }

                var session = await _supabaseClient.Auth.SignIn(userResponse.Email, PasswordEntry.Text);

                if (session != null && session.User != null)
                {
                    await DisplayAlert("Siker", "Sikeres bejelentkezés!", "OK");
                    await Shell.Current.GoToAsync("HomePage");
                }
                else
                {
                    ShowError("Hibás felhasználónév vagy jelszó.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Hiba: {ex.Message}");
            }
        }

        // Google login WebAuthenticator-rel
        private async void OnGoogleLoginClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;

            try
            {
                var provider = "google";

                // Supabase Google hitelesítési URL
                var authUrl = new Uri($"https://lbsqjhnljmlzjdnumjax.supabase.co/auth/v1/authorize?provider={provider}");
                var callbackUrl = new Uri("myapp://callback");

                var result = await WebAuthenticator.AuthenticateAsync(authUrl, callbackUrl);

                var accessToken = result?.Properties.ContainsKey("access_token") == true
                    ? result.Properties["access_token"]
                    : null;

                var refreshToken = result?.Properties.ContainsKey("refresh_token") == true
                    ? result.Properties["refresh_token"]
                    : null;

                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Bejelentkezés a Supabase-hez az access token alapján
                    var session = await _supabaseClient.Auth.SetSession(accessToken, refreshToken);

                    if (session != null && session.User != null)
                    {
                        await DisplayAlert("Siker", "Sikeres Google bejelentkezés!", "OK");
                        await Shell.Current.GoToAsync("HomePage");
                    }
                    else
                    {
                        ShowError("Hiba a Google hitelesítés során. Ellenõrizd a Supabase konfigurációt.");
                    }
                }
                else
                {
                    ShowError("Nem érkezett access token a Google hitelesítésbõl.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Hiba a Google bejelentkezés során: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        }
    }
}
