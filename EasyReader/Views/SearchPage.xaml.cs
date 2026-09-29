using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SearchPage : ContentPage
    {
        public SearchPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            searchResults.SelectedItem = null; // clear the selected item so it does not reopen the Account Details page
            BindingContext = new SearchViewModel(Navigation);
            base.OnAppearing();
        }

        // CHANGE LISTVIEW BACKGROUND COLOR WHEN TAPPED
        private void SearchViewCellTapped(object obj, EventArgs e)
        {
            // update background color
            var viewCell = (ViewCell)obj;
            if (viewCell.View != null)
            {
                viewCell.View.BackgroundColor = Color.FromHex("#dcdcdc");
            }

            // reset selected itme
            searchResults.SelectedItem = null;
        }
    }
}