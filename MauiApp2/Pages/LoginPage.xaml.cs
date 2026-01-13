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

        
        private async void OnGoogleLoginClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;

            try
            {
                var provider = "google";

               
                var authUrl = new Uri(
                    $"https://lbsqjhnljmlzjdnumjax.supabase.co/auth/v1/authorize" +
                    $"?provider={provider}" +
                    $"&redirect_to=myapp://callback" +
                    $"&scope=openid%20email%20profile" +
                    $"&response_type=token"
                );

                var callbackUrl = new Uri("myapp://callback");

                
                var result = await WebAuthenticator.AuthenticateAsync(authUrl, callbackUrl);

                
                var accessToken =
                    result?.Properties.TryGetValue("access_token", out var at) == true ? at : null;

                var refreshToken =
                    result?.Properties.TryGetValue("refresh_token", out var rt) == true ? rt : null;

                if (accessToken != null)
                {
                    
                    var session = await _supabaseClient.Auth.SetSession(accessToken, refreshToken);

                    if (session?.User != null)
                    {
                        await DisplayAlert("Siker", "Sikeres Google bejelentkezés!", "OK");
                        await Shell.Current.GoToAsync("HomePage");
                    }
                    else
                    {
                        ShowError("Hiba: a Supabase session nem jött létre.");
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
