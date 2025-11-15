using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase;

namespace MauiApp2.Services
{
    public class UserService
    {
        private readonly Client _supabase;

        public UserService(DatabaseService databaseService)
        {
            _supabase = databaseService.GetClient();
        }

        // Regisztráció
        public async Task<bool> SignUpAsync(string email, string password)
        {
            var response = await _supabase.Auth.SignUp(email, password);
            return response != null;
        }

        // Bejelentkezés
        public async Task<bool> SignInAsync(string email, string password)
        {
            var response = await _supabase.Auth.SignIn(email, password);
            return response != null;
        }

        // Kijelentkezés
        public async Task SignOutAsync()
        {
            await _supabase.Auth.SignOut();
        }
    }
}
