using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using EasyReader.Services;

namespace EasyReader.Services
{
    public static class AppServices
    {
        public static IUserDialogs UserDialogs
        {
            get
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                {
                    var svc = services.GetService(typeof(IUserDialogs)) as IUserDialogs;
                    if (svc != null)
                        return svc;
                }
                throw new InvalidOperationException("`IUserDialogs` is not registered. Register an `IUserDialogs` implementation in `MauiProgram`.");
            }
        }

        public static IMediaService Media
        {
            get
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                {
                    var svc = services.GetService(typeof(IMediaService)) as IMediaService;
                    if (svc != null)
                        return svc;
                }
                throw new InvalidOperationException("`IMediaService` is not registered. Register an `IMediaService` implementation in `MauiProgram`.");
            }
        }

        public static IExternalMaps ExternalMaps
        {
            get
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                {
                    var svc = services.GetService(typeof(IExternalMaps)) as IExternalMaps;
                    if (svc != null)
                        return svc;
                }
                throw new InvalidOperationException("`IExternalMaps` is not registered. Register an `IExternalMaps` implementation in `MauiProgram`.");
            }
        }

        public static void OpenPhoneDialer(string number)
        {
            try { PhoneDialer.Open(number); } catch { }
        }

        public static Task ComposeEmailAsync(EmailMessage message)
        {
            try { return Email.ComposeAsync(message); } catch { return Task.CompletedTask; }
        }

        public static Task ComposeSmsAsync(SmsMessage message)
        {
            try { return Sms.ComposeAsync(message); } catch { return Task.CompletedTask; }
        }
    }
}
