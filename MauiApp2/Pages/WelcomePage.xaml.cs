namespace MauiApp2.Pages
{
    public partial class WelcomePage : ContentPage
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private async void OnLoginButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("LoginPage");
        }

        private async void OnRegisterButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MainPage");
        }
    }
}
