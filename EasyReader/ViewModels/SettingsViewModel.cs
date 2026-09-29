using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using EasyReader.Models;

namespace EasyReader.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        public SettingsViewModel() 
        {
            // PAGE COMMANDS
            SelectRoute_Command = new Command(async () => await SelectRouteAsync());
            SelectDecimals_Command = new Command(async () => await SelectDecimalsAsync());
            SelectBackupFileDuraction_Command = new Command(async () => await SelectBackupFileDurationAsync());
        }
        public SettingsViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));

            // GET PROPERTIES + SET BINDINGS
            GetProperties();
            ReaderDescription = Properties.ReaderDescription;
            HostFolder = Properties.HostFolder;
            RouteSelected = Properties.RouteSelected;
            IsMissingOnly = Properties.IsMissingOnly;
            IsOpenOnly = Properties.IsOpenOnly;
            IsAutoAdvance = Properties.IsAutoAdvance;
            IsVarianceEnabled = Properties.IsVarianceEnabled;
            Variance = Properties.Variance;
            Decimals = Properties.Decimals.ToString();
            BackupFilesDuration = Properties.BackupFilesDurationDays.ToString();

            // ADD ALL ROUTES
            Route allRoutes = new Route
            {
                RouteNumber = "ALL ROUTES"
            };
            InsertRoute(allRoutes);

            // INITIALIZE CHECKBOXES (calls OnSortChanged)
            switch (Properties.SortMethod)
            {
                case "RouteSequence":
                    SortRouteSequence = true;
                    break;
                case "AccountMeter":
                    SortAccountMeter = true;
                    break;
                case "NameMeter":
                    SortNameMeter = true;
                    break;
                case "AddressMeter":
                    SortAddressMeter = true;
                    break;
                case "Misc1":
                    SortMisc1 = true;
                    break;
                case "ImportOrder":
                    SortImportOrder = true;
                    break;
            }
        }

        // SORTING
        private void OnSortChanged(string method)
        {
            // update properties
            Properties.SortMethod = method;
            UpdateProperties();

            // set remaining sort methods to false
            if (method != "RouteSequence")
                SortRouteSequence = false;
            if (method != "AccountMeter")
                SortAccountMeter = false;
            if (method != "NameMeter")
                SortNameMeter = false;
            if (method != "AddressMeter")
                SortAddressMeter = false;
            if (method != "Misc1")
                SortMisc1 = false;
            if (method != "ImportOrder")
                SortImportOrder = false;
        }

        // ROUTE SELECTION
        private async Task SelectRouteAsync()
        {
            try
            {
                // get route selected, return if canceled/clicked out
                string routeSelected = await Application.Current.MainPage.DisplayActionSheet(
                    "Select a Route", "Cancel", null, GetRoutesList().Select(rt => rt.RouteNumber).ToArray());
                if (routeSelected == "Cancel" || routeSelected == null)
                    return;

                // save route selected
                RouteSelected = routeSelected;
                Properties.RouteSelected = routeSelected;
                UpdateProperties();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // DECIMALS SELECTION
        private async Task SelectDecimalsAsync()
        {
            try
            {
                // get decimals selected, return if canceled/clicked out
                string[] decimalChoices = { "0", "1", "2", "3", "4" };
                string decimalsSelected = await Application.Current.MainPage.DisplayActionSheet(
                    "Select Number of Decimals", "Cancel", null, decimalChoices);
                if (decimalsSelected == "Cancel" || decimalsSelected == null)
                    return;

                // save decimals selected
                Decimals = decimalsSelected;
                Properties.Decimals = Convert.ToInt32(decimalsSelected);
                UpdateProperties();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // BACKUP FILE DURATION SELECTION
        private async Task SelectBackupFileDurationAsync()
        {
            try
            {
                // get backup file duraction, return if canceled/clicked out
                string duration = await Application.Current.MainPage.DisplayPromptAsync("Input Desired Backup Duration",
                    "Please enter how long you would like backup readings files (.csv's) to be saved on your device before being deleted (in days).",
                    "OK", "Cancel", null, 4, Keyboard.Numeric, "");
                if (duration == "Cancel" || duration == null)
                    return;

                // display error and return if the value entered is not greater than 0
                if (Convert.ToInt32(duration) <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "The entered value is not valid. Please enter a positive value greater than zero.", "OK");
                    return;
                }

                // save duration entered
                BackupFilesDuration = duration;
                Properties.BackupFilesDurationDays = Convert.ToInt32(duration);
                UpdateProperties();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // BINDING VARIABLES
        // -- properties binding strings and switches
        string readerDesciption;
        public string ReaderDescription
        {
            get => readerDesciption;
            set
            {
                readerDesciption = value;
                Properties.ReaderDescription = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        string hostFolder;
        public string HostFolder
        {
            get => hostFolder;
            set
            {
                hostFolder = value;
                Properties.HostFolder = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        string routeSelected;
        public string RouteSelected
        {
            get => routeSelected;
            set
            {
                routeSelected = value;
                OnPropertyChanged();
            }
        }
        bool isMissingOnly;
        public bool IsMissingOnly
        {
            get => isMissingOnly;
            set
            {
                isMissingOnly = value;
                Properties.IsMissingOnly = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        bool isOpenOnly;
        public bool IsOpenOnly
        {
            get => isOpenOnly;
            set
            {
                isOpenOnly = value;
                Properties.IsOpenOnly = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        bool isAutoAdvance;
        public bool IsAutoAdvance
        {
            get => isAutoAdvance;
            set
            {
                isAutoAdvance = value;
                Properties.IsAutoAdvance = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        bool isVarianceEnabled;
        public bool IsVarianceEnabled
        {
            get => isVarianceEnabled;
            set
            {
                isVarianceEnabled = value;
                Properties.IsVarianceEnabled = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        string variance;
        public string Variance
        {
            get => variance;
            set
            {
                variance = value;
                Properties.Variance = value;
                UpdateProperties();
                OnPropertyChanged();
            }
        }
        string decimals;
        public string Decimals
        {
            get => decimals;
            set
            {
                decimals = value;
                OnPropertyChanged();
                // properties saved in method SelectDecimalsAsync()
            }
        }
        string backupFilesDuration;
        public string BackupFilesDuration
        {
            get => backupFilesDuration;
            set
            {
                backupFilesDuration = value;
                OnPropertyChanged();
                // properties saved in method SelectBackupFileDurationAsync()
            }
        }

        // -- sort methods
        bool sortRouteSequence;
        public bool SortRouteSequence
        {
            get => sortRouteSequence;
            set
            {
                if (sortRouteSequence == value)
                    return;
                sortRouteSequence = value;
                if (value == true)
                    OnSortChanged("RouteSequence");
                OnPropertyChanged();
            }
        }
        bool sortAccountMeter;
        public bool SortAccountMeter
        {
            get => sortAccountMeter;
            set
            {
                if (sortAccountMeter == value)
                    return;
                sortAccountMeter = value;
                if (value == true)
                    OnSortChanged("AccountMeter");
                OnPropertyChanged();
            }
        }
        bool sortNameMeter;
        public bool SortNameMeter
        {
            get => sortNameMeter;
            set
            {
                if (sortNameMeter == value)
                    return;
                sortNameMeter = value;
                if (value == true)
                    OnSortChanged("NameMeter");
                OnPropertyChanged();
            }
        }
        bool sortAddressMeter;
        public bool SortAddressMeter
        {
            get => sortAddressMeter;
            set
            {
                if (sortAddressMeter == value)
                    return;
                sortAddressMeter = value;
                if (value == true)
                    OnSortChanged("AddressMeter");
                OnPropertyChanged();
            }
        }
        bool sortMisc1;
        public bool SortMisc1
        {
            get => sortMisc1;
            set
            {
                if (sortMisc1 == value)
                    return;
                sortMisc1 = value;
                if (value == true)
                    OnSortChanged("Misc1");
                OnPropertyChanged();
            }
        }
        bool sortImportOrder;
        public bool SortImportOrder
        {
            get => sortImportOrder;
            set
            {
                if (sortImportOrder == value)
                    return;
                sortImportOrder = value;
                if (value == true)
                    OnSortChanged("ImportOrder");
                OnPropertyChanged();
            }
        }        

        // COMMANDS
        public Command SelectRoute_Command { get; }
        public Command SelectDecimals_Command { get; }
        public Command SelectBackupFileDuraction_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
    }
}