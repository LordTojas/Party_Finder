

using Microsoft.Maui.Controls;

namespace MauiApp2.Pages;

public partial class BasePage : ContentPage
{
    public BasePage()
    {
        InitializeComponent();
    }

    public View Content
    {
        get => PageContent.Content;
        set => PageContent.Content = value;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Content != null)
        {
            PageContent.Content = Content;
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//homepage");
    }
}