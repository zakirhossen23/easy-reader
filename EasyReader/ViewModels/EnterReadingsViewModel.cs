using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using EasyReader.Models;
using EasyReader.Services;
using EasyReader.Views;

namespace EasyReader.ViewModels
{
    public class EnterReadingsViewModel : BaseViewModel
    {
        public EnterReadingsViewModel() 
        {
            // PAGE COMMANDS
            // -- save + clear new reading
            OpenNumPad_Command = new Command(execute: () => OpenNumPad());
            SaveNewReading_Command = new Command(async () => await SaveNewReadingAsync());
            CloseNumPad_Command = new Command(async () => await CloseNumPadAsync());
            ClearNewReading_Command = new Command(async () => await ClearNewReadingAsync());

            // -- num pad
            ClearNumPad_Command = new Command(execute: () => ClearNumPad());
            Btn1_Command = new Command(execute: () => BtnNumPad("1"));
            Btn2_Command = new Command(execute: () => BtnNumPad("2"));
            Btn3_Command = new Command(execute: () => BtnNumPad("3"));
            Btn4_Command = new Command(execute: () => BtnNumPad("4"));
            Btn5_Command = new Command(execute: () => BtnNumPad("5"));
            Btn6_Command = new Command(execute: () => BtnNumPad("6"));
            Btn7_Command = new Command(execute: () => BtnNumPad("7"));
            Btn8_Command = new Command(execute: () => BtnNumPad("8"));
            Btn9_Command = new Command(execute: () => BtnNumPad("9"));
            Btn0_Command = new Command(execute: () => BtnNumPad("0"));

            // -- change account
            LastEnteredAccount_Command = new Command(execute: () => LastEnteredAccount());
            FirstAccount_Command = new Command(execute: () => FirstAccount());
            PreviousAccount_Command = new Command(execute: () => PreviousAccount());
            NextAccount_Command = new Command(execute: () => NextAccount());
            LastAccount_Command = new Command(execute: () => LastAccount());

            // -- special functions
            TakeSavePhoto_Command = new Command(async () => await TakeSavePhotoAsync_("Account"));
            OpenMaps_Command = new Command(async () => await OpenMapsAsync());
            SaveCurrentLocation_Command = new Command(async () => await SaveCurrentLocationAsync_("Account"));
        }
        public EnterReadingsViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SearchPage_Command = new Command(async () => await GoTo_SearchPageAsync_(Navigation));
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));
            GoTo_NotesPage_Command = new Command(async () => await GoTo_NotesPageAsync_(Navigation, "Account"));

            // GET PROPERTIES + ACCOUNTS LIST
            GetProperties();
            GetSortAccountsList();
            Properties.AccountChangesMade = true;

            // SET BINDINGS
            if (!Properties.IsMissingOnly)
            {
                MissingOnlyLabel = "Off";
                MissingOnlySwitch = false;
            }
            else if (Properties.IsMissingOnly && AccountsList.Count != 0)
            {
                MissingOnlyLabel = "On";
                MissingOnlySwitch = true;
            }
            else // if IsMissingOnly but no accounts are missing readings, turn off missing only + resort
            {
                // update properties, resort accounts list
                Properties.IsMissingOnly = false; // => NavigateAccounts calls UpdateProperties
                GetSortAccountsList();

                // set bindings
                MissingOnlyLabel = "Off";
                MissingOnlySwitch = false;
            }
            Misc1Label = Properties.Misc1 + ":";
            GetAccountStatuses();

            // GET READING INC, NAV ACCOUNTS
            NavigateAccounts(GetReadingInc());
        }

        // NAVIGATE ACCOUNTS
        private void NavigateAccounts(int i)
        {
            // get account
            Properties.ReadingInc = i;
            Properties.ReadingIncID = AccountsList[i].ID;
            Account = GetAccount_FromID(Properties.ReadingIncID);
            UpdateProperties();

            // set view bool to false
            ViewNumPad = false;

            // clear num pad -> calls CheckVariance
            ClearNumPad();
        }

        // CHECK VARIANCE
        private void CheckVariance()
        {
            // check variance, get save button color
            if (Properties.IsVarianceEnabled)
            {
                // get variance double
                if (Properties.Variance == "" || Properties.Variance == null)
                    Properties.Variance = "0";
                double variancePercent = Convert.ToDouble(Properties.Variance);

                // get last + current reading doubles
                double lastReading = Convert.ToDouble(Account.LastReading);
                double currentReading;
                if (NewReadingLabel != "")
                    currentReading = Convert.ToDouble(NewReadingLabel);
                else
                    currentReading = 0;

                // get usage + labels
                double currentUsage = currentReading - lastReading;
                NewUsageLabel = currentUsage.ToString();
                double avgUsage = Convert.ToDouble(Account.AvgUsage);
                double currentVsAvgUsagePercent = (currentUsage - avgUsage) / avgUsage * 100;
                if (currentVsAvgUsagePercent > 0)
                    VsAvgUsageLabel = "+" + Math.Round(currentVsAvgUsagePercent, 1).ToString() + " %";
                else
                    VsAvgUsageLabel = Math.Round(currentVsAvgUsagePercent, 1).ToString() + " %";

                // no reading entered
                if (NewReadingLabel == "")
                {
                    NewUsageLabel = "";
                    VsAvgUsageLabel = "";
                    IsInVariance = false;
                    SaveReadingBtnColor = Color.FromHex("#ffcccb");
                }
                // reading in variance
                else if (Math.Abs(currentVsAvgUsagePercent) <= variancePercent)
                {
                    IsInVariance = true;
                    SaveReadingBtnColor = Color.FromHex("#008000");
                }
                // reading out of variance
                else
                {
                    IsInVariance = false;
                    SaveReadingBtnColor = Color.FromHex("ffda03");
                }
            }
            // always in variance if disabled
            else
            {
                IsInVariance = true;
                if (Convert.ToDouble(NewReadingLabel) == 0)
                    SaveReadingBtnColor = Color.FromHex("#ffcccb");
                else
                    SaveReadingBtnColor = Color.FromHex("#008000");
            }
        }

        // ENTER + CLEAR NEW READING
        private void OpenNumPad()
        {
            if (Properties.IsAccountDownloaded)
                ViewNumPad = true;
        }
        private async Task SaveNewReadingAsync()
        {
            try
            {
                // check if a reading is entered + if accounts are downloaded
                if (NewReadingLabel == "" || !Properties.IsAccountDownloaded)
                    return;

                // check if user wants to replace saved reading
                if (Account.EnteredReading)
                {
                    if (!await Application.Current.MainPage.DisplayAlert("Warning!",
                        "Are you sure you wish to overwrite the previously entered reading of " + Account.NewReading + "?", "Yes, overwrite reading", "Cancel"))
                        return;
                }
                // check if user wants to save out of variance reading
                if (!IsInVariance)
                {
                    if (!await Application.Current.MainPage.DisplayAlert("Warning!",
                        "The entered reading of " + NewReadingLabel + " is not within the set variance of +/- " + Properties.Variance + "%\nWould you like to continue?",
                        "Yes, enter reading", "Cancel"))
                        return;
                }

                // get usage
                double lastReading = Convert.ToDouble(Account.LastReading);
                double newReading = Convert.ToDouble(NewReadingLabel);
                if (newReading < lastReading)
                    newReading += Math.Pow(10, Account.LastReading.Length);
                string newUsage = (newReading - lastReading).ToString();

                // save new reading
                Account.NewReadDate = DateTime.Now.ToString("d");
                Account.NewReadTime = DateTime.Now.ToString("h:mm tt");
                Account.NewReading = NewReadingLabel;
                Account.NewUsage = newUsage;
                Account.EnteredReading = true;
                UpdateAccount(Account);

                // update last entered
                Properties.ReadingLastEnteredID = Account.ID;

                // update account statuses
                GetAccountStatuses();

                // alert success
                await Application.Current.MainPage.DisplayAlert("Reading Successfully Saved",
                    "Entered usage: " + newUsage + "\nHistorical avg. usage: " + Account.AvgUsage, "OK");

                // input demand reading
                if (Account.DemandNeeded)
                {
                    Account.NewDemand = await Application.Current.MainPage.DisplayPromptAsync("Enter Demand",
                        "Enter demand reading.", "OK");
                    UpdateAccount(Account);
                }

                // update reading inc
                if (Properties.IsMissingOnly)
                {
                    // get + sort accounts list
                    GetSortAccountsList();

                    // if on last but not only account, go back 1
                    if (Properties.ReadingInc == AccountsList.Count && Properties.ReadingInc != 0)
                        Properties.ReadingInc--;
                    // if all readings entered, turn off missing only
                    else if (AccountsList.Count == 0)
                        MissingOnlySwitch = false; // -> updates Properties.IsMissingOnly, calls GetSortAccountsList + GetReadingInc
                }
                else if (Properties.IsAutoAdvance && Properties.ReadingInc != (AccountsList.Count - 1))
                    Properties.ReadingInc++;
                UpdateProperties();

                

                // nav accounts -> calls UpdateProperties
                NavigateAccounts(Properties.ReadingInc);

                // send readings file if last missing reading
                if (Properties.MissingAccounts == 0)
                {
                    if (await Application.Current.MainPage.DisplayAlert("Send Readings File",
                        "There are no more missing readings. Would you like to upload the readings file now?", "Yes, upload file", "No"))
                        await Navigation.PushAsync(new FileManagerPage(true));
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task CloseNumPadAsync()
        {
            // check if there is an unsaved reading
            if (Convert.ToDouble(NewReadingLabel) != 0 && NewReadingLabel != Account.NewReading)
            {
                if (!await Application.Current.MainPage.DisplayAlert("Warning!",
                    "The entered reading has not been saved. Are you sure you wish to continue?", "Yes, continue", "Cancel"))
                    return;
            }

            // reset view
            ViewNumPad = false;
        }
        private async Task ClearNewReadingAsync()
        {
            try
            {
                if (Account.EnteredReading && Properties.IsAccountDownloaded)
                {
                    if (await Application.Current.MainPage.DisplayAlert("Warning!",
                        "Are you sure you wish to clear the current reading?\nThis cannot be undone.", "Yes, clear reading", "Cancel"))
                    {
                        // update account
                        Account.NewReadDate = "";
                        Account.NewReadTime = "";
                        Account.NewReading = "";
                        Account.NewUsage = "";
                        Account.EnteredReading = false;
                        UpdateAccount(Account);

                        // update account statuses
                        GetAccountStatuses();

                        // nav accounts
                        NavigateAccounts(Properties.ReadingInc);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // NUM PAD
        private void ClearNumPad()
        {
            // reset numpad
            if (Properties.Decimals == 0)
            {
                NewReadingLabel = "0";
            }
            else
            {
                string zeros = new string('0', Properties.Decimals);
                NewReadingLabel = "0." + zeros;
            }

            // reset other variables
            DigitsEntered = 0;
            CheckVariance();
        }
        private void BtnNumPad(string number)
        {
            // add new number + increment digits entered
            NewReadingLabel += number;
            DigitsEntered++;

            // check if reading has decimals
            if (Properties.Decimals != 0)
            {
                // move decimal
                int decIndex = NewReadingLabel.IndexOf('.');
                NewReadingLabel = string.Format("{0}{1}.{2}", NewReadingLabel.Substring(0, decIndex), NewReadingLabel[decIndex + 1], NewReadingLabel.Substring(decIndex + 2, Properties.Decimals));                
            }

            // remove placeholder zeros
            if (DigitsEntered <= (Properties.Decimals + 1))
                NewReadingLabel = NewReadingLabel.Substring(1);

            // recheck variance
            CheckVariance();
        }

        // CHANGE ACCOUNT
        private void LastEnteredAccount()
        {
            int inc = GetReadingIncFromID(Properties.ReadingLastEnteredID);
            if (inc != -1)
                NavigateAccounts(inc);
        }
        private void FirstAccount()
        {
            NavigateAccounts(0);
        }
        private void PreviousAccount()
        {
            if (Properties.ReadingInc != 0)
                Properties.ReadingInc--;
            NavigateAccounts(Properties.ReadingInc);
        }
        private void NextAccount()
        {
            if (Properties.ReadingInc != (AccountsList.Count - 1))
                Properties.ReadingInc++;
            NavigateAccounts(Properties.ReadingInc);
        }
        private void LastAccount()
        {
            NavigateAccounts(AccountsList.Count - 1);
        }

        // OPEN MAPS
        private async Task OpenMapsAsync()
        {
            // nav accounts and delay to update Account before opening maps
            NavigateAccounts(Properties.ReadingInc);
            await Task.Delay(1);
            await OpenMapsAsync_(Account.Latitude, Account.Longitude, "Account");
        }

        // NON BINDING VARIABLES
        private bool IsInVariance { get; set; }
        private int DigitsEntered { get; set; }

        // BINDING VARIABLES
        // -- misc 1
        public string Misc1Label { get; set; }

        // -- missing only
        string missingOnlyLabel;
        public string MissingOnlyLabel
        {
            get => missingOnlyLabel;
            set
            {
                if (missingOnlyLabel == value)
                    return;
                missingOnlyLabel = value;
                OnPropertyChanged();
            }
        }
        bool missingOnlySwitch;
        public bool MissingOnlySwitch
        {
            get => missingOnlySwitch;
            set
            {
                // set switch
                missingOnlySwitch = value;

                // update label + IsMissingOnly
                if (!value)
                {
                    MissingOnlyLabel = "Off";
                    Properties.IsMissingOnly = false;
                }
                else if (value && AccountsList.Count != 0)
                {
                    MissingOnlyLabel = "On";
                    Properties.IsMissingOnly = true;
                }
                else
                {
                    // reset switch to off if no accounts
                    missingOnlySwitch = false;

                    MissingOnlyLabel = "Off";
                    Properties.IsMissingOnly = false;
                }

                // resort accounts list, nav accounts
                GetSortAccountsList();
                NavigateAccounts(GetReadingInc()); // -> calls UpdateProperties

                OnPropertyChanged();
            }
        }

        // -- view bool
        bool viewNumPad;
        public bool ViewNumPad
        {
            get => viewNumPad;
            set
            {
                viewNumPad = value;
                OnPropertyChanged();
            }
        }

        // -- num pad
        string newUsageLabel;
        public string NewUsageLabel
        {
            get => newUsageLabel;
            set
            {
                if (newUsageLabel == value)
                    return;
                newUsageLabel = value;
                OnPropertyChanged();
            }
        }
        string vsAvgUsageLabel;
        public string VsAvgUsageLabel
        {
            get => vsAvgUsageLabel;
            set
            {
                if (vsAvgUsageLabel == value)
                    return;
                vsAvgUsageLabel = value;
                OnPropertyChanged();
            }
        }
        string newReadingLabel;
        public string NewReadingLabel
        {
            get => newReadingLabel;
            set
            {
                if (newReadingLabel == value)
                    return;
                newReadingLabel = value;
                OnPropertyChanged();
            }
        }
        Color saveReadingBtnColor;
        public Color SaveReadingBtnColor
        {
            get => saveReadingBtnColor;
            set
            {
                if (saveReadingBtnColor == value)
                    return;
                saveReadingBtnColor = value;
                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command OpenNumPad_Command { get; }
        public Command SaveNewReading_Command { get; }
        public Command CloseNumPad_Command { get; }
        public Command ClearNewReading_Command { get; }
        public Command ClearNumPad_Command { get; }
        public Command Btn1_Command { get; }
        public Command Btn2_Command { get; }
        public Command Btn3_Command { get; }
        public Command Btn4_Command { get; }
        public Command Btn5_Command { get; }
        public Command Btn6_Command { get; }
        public Command Btn7_Command { get; }
        public Command Btn8_Command { get; }
        public Command Btn9_Command { get; }
        public Command Btn0_Command { get; }
        public Command LastEnteredAccount_Command { get; }
        public Command FirstAccount_Command { get; }
        public Command PreviousAccount_Command { get; }
        public Command NextAccount_Command { get; }
        public Command LastAccount_Command { get; }
        public Command TakeSavePhoto_Command { get; }
        public Command OpenMaps_Command { get; }
        public Command SaveCurrentLocation_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_SearchPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
        public Command GoTo_NotesPage_Command { get; }
    }
}