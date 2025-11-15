using Microsoft.Maui.Controls.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Supabase;
using MauiApp2;
using MauiApp2.Pages;
using MauiApp2.Services;
using Maui.GoogleMaps.Hosting;
using Maui.GoogleMaps;
using Maui.GoogleMaps.Handlers;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
               
                    fonts.AddFont("BubblegumSans-Regular.ttf", "BubblegumSans");
              

                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Szolgáltatások regisztrálása
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<UserService>();
        builder.Services.AddSingleton<PlaceService>();

        // Supabase inicializálása
        var supabaseUrl = "https://lbsqjhnljmlzjdnumjax.supabase.co";
        var supabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imxic3FqaG5sam1sempkbnVtamF4Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDI2NzAzMTAsImV4cCI6MjA1ODI0NjMxMH0.yvC98twsuQs7UnBclnLL1WWN0gtfPD76qg-3-rfm2kU";
        var supabase = new Supabase.Client(supabaseUrl, supabaseKey);
        builder.Services.AddSingleton(supabase);

        // A WelcomePage regisztrálása
        builder.Services.AddSingleton<WelcomePage>();

        // A LoginPage regisztrálása
        builder.Services.AddTransient<LoginPage>(provider =>
        {
            var supabaseClient = provider.GetRequiredService<Supabase.Client>();
            return new LoginPage(supabaseClient);
        });

        // A MainPage regisztrálása (regisztráció)
        builder.Services.AddSingleton<MainPage>(provider =>
        {
            var supabaseClient = provider.GetRequiredService<Supabase.Client>();
            return new MainPage(supabaseClient);
        });

        // A HomePage regisztrálása
        builder.Services.AddSingleton<HomePage>(provider =>
        {
            var supabaseClient = provider.GetRequiredService<Supabase.Client>();
            return new HomePage(supabaseClient);
        });

        // Az EventCreationPage regisztrálása
        builder.Services.AddTransient<EventCreationPage>();
        builder.Services.AddTransient<EventSearchPage>();
        builder.Services.AddTransient<CurrentPage>();
        builder.Services.AddTransient<CommunityPage>();
        builder.Services.AddTransient<PopularPage>();
        builder.Services.AddTransient<RecommendedPage>();
        builder.Services.AddTransient<SupriseMePage>();
        builder.Services.AddTransient<SavedPage>();
        builder.Services.AddTransient<SettingsPage>();
        // Az AppShell regisztrálása
        builder.Services.AddSingleton<AppShell>(provider =>
        {
            var supabaseClient = provider.GetRequiredService<Supabase.Client>();
            return new AppShell(supabaseClient);
        });

        // Google Maps inicializálása
#if ANDROID
        builder.UseGoogleMaps(); // Androidon nem kell API kulcsot megadni itt, az AndroidManifest.xml-ben adjuk meg
#endif

        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(Maui.GoogleMaps.Map), typeof(Maui.GoogleMaps.Handlers.MapHandler));
        });

        return builder.Build();
    }
}