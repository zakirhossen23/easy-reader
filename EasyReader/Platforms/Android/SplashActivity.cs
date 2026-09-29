using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.AppCompat.App;

namespace EasyReader.Droid
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AppCompatActivity
    {
        private static readonly string TAG = "X:" + nameof(SplashActivity);

        protected override void OnCreate(Bundle savedInstanceState)
        {
            // call on create
            base.OnCreate(savedInstanceState);
            Log.Debug(TAG, "SplashActivity.OnCreate");

            // try to present a full-bleed splash (hide status/navigation where possible)
            try
            {
                // request fullscreen flags on the window
                Window?.AddFlags(WindowManagerFlags.Fullscreen);

                // also set immersive system ui flags so nav/status are hidden on supported devices
                var uiOptions = (int)SystemUiFlags.LayoutStable | (int)SystemUiFlags.LayoutFullscreen |
                                (int)SystemUiFlags.HideNavigation | (int)SystemUiFlags.ImmersiveSticky;
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
            }
            catch
            {
                // ignore if not supported on platform/version
            }
        }

        protected override async void OnResume()
        {
            // set system bars
            //Window.InsetsController.SetSystemBarsAppearance((int)WindowInsetsControllerAppearance.None, (int)WindowInsetsControllerAppearance.None);

            //int uiOptions = (int)Window.DecorView.SystemUiVisibility;
            //uiOptions |= (int)SystemUiFlags.Fullscreen;
            //uiOptions |= (int)SystemUiFlags.HideNavigation;
            //uiOptions |= (int)SystemUiFlags.LayoutStable;
            //Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;

            // base
            base.OnResume();

            // check for Play Core immediate update; if none, continue startup
            AppUpdateHelper.TryStartImmediateUpdate(this, () =>
            {
                Task startupWork = new Task(() => { SimulateStartup(); });
                startupWork.Start();
            });
        }

        // handle the Play Core update activity result
        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode == AppUpdateHelper.UPDATE_REQUEST_CODE)
            {
                if (resultCode != Result.Ok)
                {
                    Log.Debug(TAG, "Immediate update canceled or failed - closing app.");
                    FinishAffinity();
                }
                else
                {
                    // update installed, proceed to main
                    StartActivity(new Intent(this, typeof(MainActivity)));
                }
            }
        }

        // prevent the back button from canceling the startup process
        public override void OnBackPressed() { }

        // simulate background work that happens behind the splash screen
        async void SimulateStartup()
        {
            Log.Debug(TAG, "Performing some startup work that takes a bit of time.");
            // small visible delay so the splash is perceptible on fast devices
            await Task.Delay(800);
            Log.Debug(TAG, "Startup work is finished - starting MainActivity.");
            // Use the Activity instance as the context when starting the MainActivity
            // to avoid Application.Context-related issues on MAUI/Android.
            StartActivity(new Intent(this, typeof(MainActivity)));
        }
    }
}