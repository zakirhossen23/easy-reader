using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace EasyReader.Services
{
    public class ExternalMaps_Default : IExternalMaps
    {
        public async Task NavigateTo(string name, double latitude, double longitude)
        {
            try
            {
                // Simple cross-platform fallback: open Google Maps URL in launcher
                string url = $"https://www.google.com/maps/search/?api=1&query={latitude},{longitude}";
                await Launcher.OpenAsync(new Uri(url));
            }
            catch
            {
                // swallow exceptions for best-effort fallback
            }
        }
    }
}
