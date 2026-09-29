using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using EasyReader.Models;
using EasyReader.Views;

namespace EasyReader.ViewModels
{
    public class SearchViewModel : BaseViewModel
    {
        public SearchViewModel()
        {
            // PAGE COMMANDS
            PerformSearch_Command = new Command<string>(execute: (string query) => GetSearchResults(query));
        }
        public SearchViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));

            // GET PROPERTIES + STATUS LABELS
            GetProperties();
            GetAccountStatuses();
            GetServiceOrderStatuses();
        }

        // SEARCH
        private void GetSearchResults(string query)
        {
            string normalizedQuery = query?.ToLower() ?? "";
            SearchResultsList = query != null && normalizedQuery != "" ? GetSearchResultsFromDB(normalizedQuery) : null;
        }

        // OPEN SEARCH RESULT
        private async Task OpenSelectedSearchResultAsync(string query)
        {
            try
            {
                // delay so background color of selected search result changes
                await Task.Delay(5);

                // set reading inc id, get selected account
                Account accountSelected = GetAccountFromSearchResult(query.ToLower());

                // if search result route is not within current selection, update route selected to all routes
                if (Properties.RouteSelected != "ALL ROUTES" && Properties.RouteSelected != accountSelected.Route)
                {
                    // check if user wants to update route filter
                    if (!await Application.Current.MainPage.DisplayAlert("Notice",
                        "The selected account is not within the current route selection.\nContinuing will update the route filter to ALL ROUTES.\nWould you like to continue?",
                        "Continue", "Cancel"))
                    {
                        // reset search bar and result, return
                        SearchBarQuery = "";
                        return;
                    }

                    // update properties
                    Properties.RouteSelected = "ALL ROUTES";
                    UpdateProperties();
                }

                // get page to open
                string[] pageChoices = { "Enter Readings", "Account Details" };
                string pageSelected = await Application.Current.MainPage.DisplayActionSheet(
                    "Select Page to Open", "Cancel", null, pageChoices);
                if (pageSelected == "Cancel" || pageSelected == null)
                    return;

                // open selected page
                if (pageSelected == "Account Details")
                    await Navigation.PushAsync(new AccountDetailsPage());
                else if (pageSelected == "Enter Readings")
                {
                    // if missing only is on but account has an entered reading, turn off missing only
                    if (Properties.IsMissingOnly && accountSelected.EnteredReading)
                    {
                        Properties.IsMissingOnly = false;
                        UpdateProperties();
                    }

                    // open enter readings page
                    await Navigation.PushAsync(new EnterReadingsPage());
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // BINDING COLLECTIONS
        List<string> searchResultsList;
        public List<string> SearchResultsList
        {
            get => searchResultsList;
            set
            {
                searchResultsList = value;
                OnPropertyChanged();
            }
        }

        // BINDING VARIABLES
        string searchBarQuery;
        public string SearchBarQuery
        {
            get => searchBarQuery;
            set
            {
                if (searchBarQuery == value)
                    return;
                searchBarQuery = value;
                GetSearchResults(value);
                OnPropertyChanged();
            }
        }
        string selectedSearchResult;
        public string SelectedSearchResult
        {
            get => selectedSearchResult;
            set
            {
                selectedSearchResult = value;
                OnPropertyChanged();
                if (value != null)
                    OpenSelectedSearchResultAsync(value).ConfigureAwait(true);
            }
        }

        // COMMANDS
        public Command<string> PerformSearch_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
    }
}