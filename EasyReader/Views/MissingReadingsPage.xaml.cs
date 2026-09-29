using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.ApplicationModel;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MissingReadingsPage : ContentPage
    {
        public MissingReadingsPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            // do not change the binding context while taking a photo
            var vm = (MissingReadingsViewModel)BindingContext;
            if (!vm.IsTakingPhoto)
                BindingContext = new MissingReadingsViewModel(Navigation);
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
                botButtonsGridLength = 70;
            }


            // VERTICAL
            if (height > width)
            {
                // clear outer grid definitions
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.RowDefinitions.Clear();

                // add definitions
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(labelsGridLength, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(filesGridLength, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(botButtonsGridLength) });

                // place inner grids
                outerGrid.Children.Remove(filesGrid);
                outerGrid.Children.Remove(botButtonsGrid);
                outerGrid.Children.Add(filesGrid);
                Grid.SetColumn(filesGrid, 0);
                Grid.SetRow(filesGrid, 1);
                outerGrid.Children.Add(botButtonsGrid);
                Grid.SetColumn(botButtonsGrid, 0);
                Grid.SetRow(botButtonsGrid, 2);
                Grid.SetRowSpan(accountDetailsGrid, 1);
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
                outerGrid.Children.Add(filesGrid);
                Grid.SetColumn(filesGrid, 1);
                Grid.SetRow(filesGrid, 0);
                outerGrid.Children.Add(botButtonsGrid);
                Grid.SetColumn(botButtonsGrid, 1);
                Grid.SetRow(botButtonsGrid, 1);
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