using System;
using Android.App;
using Android.Util;
using Android.Gms.Tasks;
using Xamarin.Google.Android.Play.Core.Tasks;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.Install.Model;

namespace EasyReader.Droid
{
    public static class AppUpdateHelper
    {
        public const int UPDATE_REQUEST_CODE = 12345;
        private const string TAG = "AppUpdateHelper";

        public static void TryStartImmediateUpdate(Activity activity, Action onNoUpdate = null)
        {
            try
            {
                var appUpdateManager = AppUpdateManagerFactory.Create(activity);
                var appUpdateInfoTask = appUpdateManager.AppUpdateInfo;

                appUpdateInfoTask.AddOnSuccessListener(new OnSuccessListener((result) =>
                {
                    try
                    {
                        if (result is AppUpdateInfo appUpdateInfo)
                        {
                            var avail = appUpdateInfo.UpdateAvailability();
                            var availStr = avail.ToString();

                            Log.Debug(TAG, $"AppUpdateInfo received. Availability={availStr}, IsImmediateAllowed={appUpdateInfo.IsUpdateTypeAllowed(AppUpdateType.Immediate)}");

                            if (avail == UpdateAvailability.UpdateAvailable && appUpdateInfo.IsUpdateTypeAllowed(AppUpdateType.Immediate))
                            {
                                Log.Debug(TAG, "Immediate update available, starting update flow.");
                                try
                                {
                                    appUpdateManager.StartUpdateFlowForResult(appUpdateInfo, AppUpdateType.Immediate, activity, UPDATE_REQUEST_CODE);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(TAG, "Failed to start update flow: " + ex);
                                    onNoUpdate?.Invoke();
                                }
                            }
                            else
                            {
                                onNoUpdate?.Invoke();
                            }
                        }
                        else
                        {
                            Log.Debug(TAG, "AppUpdateInfo result is not an AppUpdateInfo instance.");
                            onNoUpdate?.Invoke();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(TAG, ex.ToString());
                        onNoUpdate?.Invoke();
                    }
                }));

                // Also listen for failures to get more diagnostic info in Logcat
                appUpdateInfoTask.AddOnFailureListener(new OnFailureListener((ex) =>
                {
                    try
                    {
                        Log.Error(TAG, "AppUpdateInfo task failed: " + ex?.ToString());
                    }
                    catch { }
                    onNoUpdate?.Invoke();
                }));
            }
            catch (Exception ex)
            {
                Log.Error(TAG, ex.ToString());
                onNoUpdate?.Invoke();
            }
        }

        // small helper to adapt a lambda to Play Core IOnFailureListener
        class OnFailureListener : Java.Lang.Object, Android.Gms.Tasks.IOnFailureListener, Xamarin.Google.Android.Play.Core.Tasks.IOnFailureListener
        {
            readonly Action<Java.Lang.Exception> _action;
            public OnFailureListener(Action<Java.Lang.Exception> action) { _action = action; }
            public void OnFailure(Java.Lang.Exception e) => _action?.Invoke(e);
        }

        // small helper to adapt a lambda to Play Core IOnSuccessListener
        class OnSuccessListener : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener, Xamarin.Google.Android.Play.Core.Tasks.IOnSuccessListener
        {
            readonly Action<Java.Lang.Object> _action;
            public OnSuccessListener(Action<Java.Lang.Object> action) { _action = action; }
            public void OnSuccess(Java.Lang.Object result) => _action?.Invoke(result);
        }
    }
}
