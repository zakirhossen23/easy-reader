using Microsoft.Maui.Controls;
using EasyReader.Models;

namespace EasyReader.ViewModels
{
    public class GalleryViewModel : BaseViewModel
    {
        public GalleryViewModel() { }
        public GalleryViewModel(INavigation navigation, AccountFile file)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));

            // SET BINDINGS
            //GetProperties();
            FileName = file.Name;
            FileImage = file.Path;
        }
        public GalleryViewModel(INavigation navigation, SOFile file)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));

            // SET BINDINGS
            //GetProperties();
            FileName = file.Name;
            FileImage = file.Path;
        }

        // BINDING VARIABLES
        public string FileName { get; set; }
        public ImageSource FileImage { get; set; }

        // NAVIGATION
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
    }
}
