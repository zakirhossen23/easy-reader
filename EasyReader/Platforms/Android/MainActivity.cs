using Android.App;
using Android.Content.PM;
using Android.OS;

namespace EasyReader
{
    // MainLauncher removed because SplashActivity is the launcher activity.
    [Activity(Theme = "@style/MainTheme", LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
