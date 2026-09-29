using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;
using EasyReader.Views;

namespace EasyReader
{
    public partial class App : Application
    {
        public App()
        {
            Application.Current.UserAppTheme = AppTheme.Light;
            InitializeComponent();
           
        }

        // small device resolution specs
        const int smallWidthResolution = 768;
        const int smallHeightResolution = 1280;

        public static bool IsSmallDevice()
        {
            // get device metrics
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            double width = mainDisplayInfo.Width;
            double height = mainDisplayInfo.Height;

            // check if small device
            return (width <= smallWidthResolution && height <= smallHeightResolution);
        }
        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}