using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    public partial class AdvancedFileManagerPage : ContentPage
    {
        public AdvancedFileManagerPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
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
                // clear feature grid definitions
                featuresGrid.ColumnDefinitions.Clear();
                featuresGrid.RowDefinitions.Clear();
                featuresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                featuresGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                featuresGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                rowExtraSpace.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // clear feature grid definitions
                featuresGrid.ColumnDefinitions.Clear();
                featuresGrid.RowDefinitions.Clear();
                featuresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                featuresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                featuresGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); 
                rowExtraSpace.Height = 0;

            }
        }
    }
}
