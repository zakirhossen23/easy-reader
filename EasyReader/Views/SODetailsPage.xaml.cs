using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SODetailsPage : ContentPage
    {
        public SODetailsPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            BindingContext = new SODetailsViewModel(Navigation);
            base.OnAppearing();
        }

        public async void OnBackButtonClicked(object sender, EventArgs e)
        {
            if (Navigation != null && Navigation.NavigationStack.Count > 0)
                await Navigation.PopAsync();
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
                gridOuter.ColumnDefinitions.Clear();
                gridOuter.RowDefinitions.Clear();

                // add definitions
                gridOuter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridOuter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                gridOuter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });


            }
            else
            {
                // clear outer grid definitions
                gridOuter.ColumnDefinitions.Clear();
                gridOuter.RowDefinitions.Clear();

                // add definitions
                gridOuter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridOuter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridOuter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
        }
    }
}
