using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Supabase;


namespace MauiApp2.Services
{
    public class DatabaseService
    {
        private readonly Client _supabase;


        public DatabaseService()
        {
            
            string url = "https://lbsqjhnljmlzjdnumjax.supabase.co";
            string anonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imxic3FqaG5sam1sempkbnVtamF4Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDI2NzAzMTAsImV4cCI6MjA1ODI0NjMxMH0.yvC98twsuQs7UnBclnLL1WWN0gtfPD76qg-3-rfm2kU";

            _supabase = new Client(url, anonKey);
            _supabase.InitializeAsync();
        }
        public Client GetClient() => _supabase;
    }
}
