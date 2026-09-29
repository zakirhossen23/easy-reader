using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SOFilesPage : ContentPage
    {
        public SOFilesPage()
        {
            InitializeComponent();
            BindingContext = new SOFilesViewModel(Navigation);
        }

        // DO NOT CHANGE LISTVIEW BACKGROUND COLOR WHEN TAPPED
        private void SOViewCellTapped(object obj, EventArgs e)
        {
            var viewCell = (ViewCell)obj;
            if (viewCell.View != null)
                viewCell.View.BackgroundColor = Colors.White;
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
                gridOuter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });


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