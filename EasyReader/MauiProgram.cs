using Microsoft.Extensions.Logging;
using EasyReader.Services;
using EasyReader.Services.PlatformDefaults;
using CommunityToolkit.Maui;


#if ANDROID
using EasyReader.Platforms.Android;
#endif
#if IOS
using EasyReader.Platforms.iOS;
#endif
using EasyReader.Views;

namespace EasyReader
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder()
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

    

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register platform-specific implementations for database and directories
#if ANDROID
            builder.Services.AddSingleton<IDatabaseConnection, SQLite_Android>();
            builder.Services.AddSingleton<IDirectoryCreator, Directories_Android>();
#elif IOS
            builder.Services.AddSingleton<IDatabaseConnection, SQLite_iOS>();
            builder.Services.AddSingleton<IDirectoryCreator, Directories_iOS>();
#else
            builder.Services.AddSingleton<IDatabaseConnection, SQLite_Default>();
            builder.Services.AddSingleton<IDirectoryCreator, Directory_Default>();
#endif

            // Register MAUI-friendly adapters for dialogs and media
            builder.Services.AddSingleton<IUserDialogs, UserDialogs_MAUI>();
            builder.Services.AddSingleton<IMediaService, MediaService_MAUI>();
            // register local external maps adapter
            builder.Services.AddSingleton<IExternalMaps, ExternalMaps_Default>();

            return builder.Build();
        }
    }
}
