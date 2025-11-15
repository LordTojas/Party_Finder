using MauiApp2.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Supabase;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MauiApp2.Pages
{
    public partial class SettingsPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;
        private ProfileData _profileData;
        private Guid _userId; // Guid típusú, hogy illeszkedjen a ProfileData.UserId-hez
        private string _email;

        public SettingsPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            LoadUserDataAsync();
        }

        private async void LoadUserDataAsync()
        {
            try
            {
                // Bejelentkezett felhasználó adatainak lekérdezése
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Nincs bejelentkezett felhasználó!", "OK");
                    return;
                }

                // A user.Id-t Guid típusra konvertáljuk
                if (!Guid.TryParse(user.Id, out _userId))
                {
                    await DisplayAlert("Hiba", "A felhasználói azonosító formátuma érvénytelen!", "OK");
                    return;
                }

                _email = user.Email;
                EmailLabel.Text = _email;

                // A jelszót nem tudjuk lekérdezni a Supabase Auth-ból, ezért csak placeholder-t használunk
                PasswordLabel.Text = "********";

                // ProfileData lekérdezése
                var profileResponse = await _supabaseClient.From<ProfileData>().Where(p => p.UserId == _userId).Single();
                if (profileResponse != null)
                {
                    _profileData = profileResponse;
                    DescriptionEditor.Text = _profileData.Description;
                    FriendCodeLabel.Text = _profileData.FriendCode;
                }
                else
                {
                    // Ha nincs még profil adat, létrehozunk egy újat egyedi barátkóddal
                    string uniqueFriendCode = await GenerateUniqueFriendCode();
                    _profileData = new ProfileData { UserId = _userId, FriendCode = uniqueFriendCode };
                    FriendCodeLabel.Text = _profileData.FriendCode;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a felhasználói adatok betöltése közben: {ex.Message}", "OK");
            }
        }

        private async Task<string> GenerateUniqueFriendCode()
        {
            Random random = new Random();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";
            string friendCode;

            bool isUnique = false;
            int maxAttempts = 100; // Biztonsági korlát az ismétlésekre

            do
            {
                // Barátkód generálása: 3 nagybetû + 3 szám
                friendCode = new string(Enumerable.Repeat(letters, 3).Select(s => s[random.Next(s.Length)]).ToArray()) +
                             new string(Enumerable.Repeat(numbers, 3).Select(s => s[random.Next(s.Length)]).ToArray());

                // Ellenõrizzük, hogy létezik-e már ilyen barátkód
                var existingProfile = await _supabaseClient.From<ProfileData>()
                    .Where(p => p.FriendCode == friendCode)
                    .Single();

                if (existingProfile == null)
                {
                    isUnique = true; // Ha nem létezik, egyedi a kód
                }

                maxAttempts--;
                if (maxAttempts <= 0)
                {
                    throw new Exception("Nem sikerült egyedi barátkódot generálni. Túl sok próbálkozás.");
                }
            } while (!isUnique);

            return friendCode; // Pl. "ABC123"
        }

        private async void OnUploadImageButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var file = await FilePicker.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images });
                if (file == null) return;

                // Stream konvertálása byte[]-ra
                byte[] fileBytes;
                using (var stream = await file.OpenReadAsync())
                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                // A fájlnevet most már Guid alapján generáljuk
                var fileName = $"{_userId.ToString()}_{file.FileName}";
                var uploadResponse = await _supabaseClient.Storage.From("profile-images").Upload(fileBytes, fileName);

                if (uploadResponse != null)
                {
                    _profileData.ProfileImageUrl = _supabaseClient.Storage.From("profile-images").GetPublicUrl(fileName);
                    await DisplayAlert("Siker", "Kép sikeresen feltöltve!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a kép feltöltése közben: {ex.Message}", "OK");
            }
        }

        private async void OnChangePasswordButtonClicked(object sender, EventArgs e)
        {
            // Navigáció egy jelszó módosító oldalra (feltételezem, hogy van ilyen)
            await Shell.Current.GoToAsync("ChangePasswordPage");
        }

        private async void OnSaveButtonClicked(object sender, EventArgs e)
        {
            try
            {
                // Leírás mentése
                _profileData.Description = DescriptionEditor.Text;

                // Biztosítjuk, hogy a UserId helyesen legyen beállítva
                _profileData.UserId = _userId;

                // ProfileData mentése vagy frissítése
                if (_profileData.Id == Guid.Empty) // Új rekord
                {
                    await _supabaseClient.From<ProfileData>().Insert(_profileData);
                }
                else // Meglévõ rekord frissítése
                {
                    await _supabaseClient.From<ProfileData>().Where(p => p.Id == _profileData.Id).Update(_profileData);
                }

                await DisplayAlert("Siker", "Adatok sikeresen mentve!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a mentés közben: {ex.Message}", "OK");
            }
        }

        private async void OnDeleteAccountButtonClicked(object sender, EventArgs e)
        {
            // Megerõsítõ párbeszédpanel megjelenítése
            var confirm = await DisplayAlert("Megerõsítés", "Biztosan törölni szeretnéd a fiókodat? Ez a mûvelet nem vonható vissza! Kérlek, a fiók végleges törléséhez lépj kapcsolatba az adminisztrátorral.", "Igen", "Nem");
            if (!confirm) return;

            try
            {
                // Soft delete: töröljük a profile_data rekordot
                await _supabaseClient.From<ProfileData>().Where(p => p.UserId == _userId).Delete();

                // Kijelentkezés
                await _supabaseClient.Auth.SignOut();

                await DisplayAlert("Siker", "A fiók adatai törölve lettek. A fiók végleges törléséhez lépj kapcsolatba az adminisztrátorral.", "OK");
                // Navigáció a bejelentkezõ oldalra (feltételezem, hogy van ilyen)
                await Shell.Current.GoToAsync("LoginPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba a fiók törlése közben: {ex.Message}", "OK");
            }
        }
    }
}