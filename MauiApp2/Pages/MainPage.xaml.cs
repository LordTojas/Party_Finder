using Supabase;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using MauiApp2.Models;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Supabase.Gotrue;

namespace MauiApp2.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;

        public MainPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient;

            var ages = Enumerable.Range(18, 83).ToList(); // 18-tól 100-ig
            AgePicker.ItemsSource = ages;
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;

            if (!ValidateInputs()) return;

            try
            {
                // 1. Felhasználó regisztrálása metaadatokkal
                Supabase.Gotrue.Session signUpResponse = null;
                try
                {
                    signUpResponse = await _supabaseClient.Auth.SignUp(
                        EmailEntry.Text, // Email közvetlenül a SignUp paramétereként
                        PasswordEntry.Text, // Jelszó közvetlenül a SignUp paramétereként
                        new Supabase.Gotrue.SignUpOptions
                        {
                            Data = new Dictionary<string, object>
                            {
                                { "username", UsernameEntry.Text.Trim() },
                                { "gender", GenderPicker.SelectedItem.ToString() },
                                { "age", (int)AgePicker.SelectedItem }
                            }
                        }
                    );
                }
                catch (Exception ex)
                {
                    ShowError("Hiba történt a regisztráció során: " + ex.Message);
                    return;
                }

                if (signUpResponse == null || signUpResponse.User == null)
                {
                    ShowError("Hiba történt a regisztráció során: Ismeretlen hiba");
                    return;
                }

                Console.WriteLine($"Felhasználó regisztrálva: {signUpResponse.User.Email}");
                await DisplayAlert("Debug", $"auth.uid(): {signUpResponse.User.Id}", "OK");

                // 2. Bejelentkeztetjük a felhasználót
                Supabase.Gotrue.Session signInResponse = null;
                try
                {
                    signInResponse = await _supabaseClient.Auth.SignIn(EmailEntry.Text, PasswordEntry.Text);
                }
                catch (Exception ex)
                {
                    ShowError("Nem sikerült bejelentkeztetni a felhasználót: " + ex.Message);
                    return;
                }

                if (signInResponse == null || signInResponse.User == null)
                {
                    ShowError("Nem sikerült bejelentkeztetni a felhasználót: Ismeretlen hiba");
                    return;
                }

                // A trigger automatikusan beszúrja a rekordot a users táblába,
                // így nem kell manuálisan Insert hívást végezni

                Console.WriteLine("Felhasználó mentve a saját Users táblába is (trigger által).");
                await DisplayAlert("Siker", "Fiók létrehozva!", "OK");
                await Shell.Current.GoToAsync("HomePage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba: {ex.Message}");
                ShowError($"Hiba: {ex.Message}");
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
            {
                ShowError("A felhasználónév nem lehet üres.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailEntry.Text) || !IsValidEmail(EmailEntry.Text))
            {
                ShowError("Kérlek adj meg egy érvényes email címet.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text) || !IsValidPassword(PasswordEntry.Text))
            {
                ShowError("A jelszónak legalább 8 karakterből kell állnia, és tartalmaznia kell legalább 1 számot.");
                return false;
            }

            if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ShowError("A jelszavak nem egyeznek.");
                return false;
            }

            if (GenderPicker.SelectedItem == null)
            {
                ShowError("Kérlek válassz nemet.");
                return false;
            }

            if (AgePicker.SelectedItem == null)
            {
                ShowError("Kérlek válassz kort.");
                return false;
            }

            if (!AcceptCheckBox.IsChecked)
            {
                ShowError("Kérlek fogadd el a feltételeket.");
                return false;
            }

            return true;
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        }

        private bool IsValidEmail(string email)
        {
            var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, emailPattern);
        }

        private bool IsValidPassword(string password)
        {
            var passwordPattern = @"^(?=.*\d).{8,}$";
            return Regex.IsMatch(password, passwordPattern);
        }
    }
}