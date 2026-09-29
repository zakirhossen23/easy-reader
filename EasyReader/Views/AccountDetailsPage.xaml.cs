using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.ApplicationModel;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AccountDetailsPage : ContentPage
    {
        public AccountDetailsPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            // do not change the binding context while taking a photo
            var vm = (AccountDetailsViewModel)BindingContext;
            if (!vm.IsTakingPhoto)
                BindingContext = new AccountDetailsViewModel(Navigation);
            base.OnAppearing();
        }

        // SET GRID BASED ON SCREEN ORIENTATION
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            // set grid lengths based on device idiom
            var idiom = DeviceInfo.Idiom;
            int labelsGridLength;
            int filesGridLength;
            int botButtonsGridLength;
            if (idiom == DeviceIdiom.Tablet)
            {
                labelsGridLength = 4;
                filesGridLength = 3;
                botButtonsGridLength = 110;
            }
            else // phone
            {
                labelsGridLength = 2;
                filesGridLength = 1;
                // increase bottom buttons area on phones so two rows of icon buttons are visible
                botButtonsGridLength = 100;
            }

            // VERTICAL
            if (height > width)
            {
                //    //// clear outer grid definitions
                //    //outerGrid.ColumnDefinitions.Clear();
                //    //outerGrid.RowDefinitions.Clear();

                //    //// add definitions
                //    //outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                //    //outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(labelsGridLength, GridUnitType.Star) });
                //    //outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(filesGridLength, GridUnitType.Star) });
                //    //outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(botButtonsGridLength) });

                //    //// place inner grids
                //    //outerGrid.Children.Remove(filesGrid);
                //    //outerGrid.Children.Remove(botButtonsGrid);
                //    //outerGrid.Children.Add(filesGrid, 0, 1); // column, row
                //    //outerGrid.Children.Add(botButtonsGrid, 0, 2);
                //    //Grid.SetRowSpan(accountDetailsGrid, 1);
            }
            // HORIZONTAL
            else
            {
                // clear outer grid definitions
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.RowDefinitions.Clear();

                // add definitions
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(botButtonsGridLength) });

                // place inner grids
                outerGrid.Children.Remove(filesGrid);
                outerGrid.Children.Remove(botButtonsGrid);
                outerGrid.Children.Add(filesGrid, 1, 0);
                outerGrid.Children.Add(botButtonsGrid, 1, 1);
                Grid.SetRowSpan(accountDetailsGrid, 2);
            }
        }

        // DO NOT CHANGE LISTVIEW BACKGROUND COLOR WHEN TAPPED
        private void FileViewCellTapped(object obj, EventArgs e)
        {
            var viewCell = (ViewCell)obj;
            if (viewCell.View != null)
                viewCell.View.BackgroundColor = Colors.White;
        }
    }
}