using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace MauiApp2.Platforms.Android
{
    [Activity(
        Exported = true,
        NoHistory = true,
        LaunchMode = LaunchMode.SingleTask)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[]
        {
            Intent.CategoryDefault,
            Intent.CategoryBrowsable
        },
        DataScheme = "myapp",
        DataHost = "callback")]
    public class MyWebAuthCallbackActivity : WebAuthenticatorCallbackActivity
    {
    }
}
