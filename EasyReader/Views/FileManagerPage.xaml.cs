using EasyReader.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System.Reflection;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class FileManagerPage : ContentPage
    {
        public FileManagerPage()
        {
            InitializeComponent();
        }
        public FileManagerPage(bool uploadReadings)
        {
            InitializeComponent();
            BindingContext = new FileManagerViewModel(Navigation, uploadReadings);
        }
        protected override void OnAppearing()
        {
            // Only create the ViewModel once and reuse it. This prevents overwriting the
            // existing BindingContext which can break bindings (e.g., popup visibility).
            if (BindingContext == null)
                BindingContext = new FileManagerViewModel(Navigation);
            base.OnAppearing();
        }
        // SET AMOUNT OF EXTRA SPACE
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            var idiom = DeviceInfo.Idiom;

            // VERTICAL
            if (height > width)
            {
                // clear outer grid definitions
                fileManagementGrid.ColumnDefinitions.Clear();
                fileManagementGrid.RowDefinitions.Clear();

                // add definitions
                fileManagementGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fileManagementGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                fileManagementGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                fileManagementGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });


            }
            else
            {
                // clear outer grid definitions
                fileManagementGrid.ColumnDefinitions.Clear();
                fileManagementGrid.RowDefinitions.Clear();

                // add definitions
                fileManagementGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fileManagementGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fileManagementGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            }
        }
}