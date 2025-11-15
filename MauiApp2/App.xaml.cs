using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp2
{
    public partial class App : Application
    {
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            MainPage = serviceProvider.GetRequiredService<AppShell>();
        }
    }
}