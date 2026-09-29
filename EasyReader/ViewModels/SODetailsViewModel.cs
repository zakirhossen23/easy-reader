using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using EasyReader.Views;
using EasyReader.Models;
using EasyReader.Services;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class SODetailsViewModel : BaseViewModel
    {
        public SODetailsViewModel() 
        {
            // PAGE COMMANDS
            // -- view customer details
            OpenCloseCustomerDetails_Command = new Command(execute: () => OpenCloseCustomerDetails());

            // -- change so
            FirstSO_Command = new Command(execute: () => FirstServiceOrder());
            PreviousSO_Command = new Command(execute: () => PreviousServiceOrder());
            NextSO_Command = new Command(execute: () => NextServiceOrder());
            LastSO_Command = new Command(execute: () => LastServiceOrder());

            // -- other options
            ContactCustomer_Command = new Command(async () => await ContactCustomerAsync());
            TakeSavePhoto_Command = new Command(async () => await TakeSavePhotoAsync_("SO"));
            OpenMaps_Command = new Command(async () => await OpenMapsAsync());
            SaveCurrentLocation_Command = new Command(async () => await SaveCurrentLocationAsync_("SO"));
            SelectOption_Command = new Command(async () => await SelectOptionAsync());
        }
        public SODetailsViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SOFilesPage_Command = new Command(async () => await GoTo_SOFilesPageAsync());
            GoTo_SOClosePage_Command = new Command(async () => await GoTo_SOClosePageAsync());
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));
            GoTo_NotesPage_Command = new Command(async () => await GoTo_NotesPageAsync_(Navigation, "SO"));

            // GET PROPERTIES + SO LIST
            GetProperties();
            GetServiceOrdersList();

            // IF NO SOs, TURN OFF OPEN ONLY
            if (ServiceOrdersList.Count == 0)
            {
                Properties.IsOpenOnly = false;
                GetServiceOrdersList();
            }

            // NAVIGATE SOs
            Properties.SOInc = GetSOInc();
            NavigateServiceOrders();
        }

        // NAVIGATE SOs
        private void NavigateServiceOrders()
        {
            // get service order
            Properties.SOIncID = ServiceOrdersList[Properties.SOInc].ID;
            ServiceOrder = GetServiceOrder_FromID(Properties.SOIncID);
            UpdateProperties();

            // set index label + view bool
            IndexLabel = (Properties.SOInc + 1).ToString() + " out of " + ServiceOrdersList.Count.ToString();
            ViewCustomerDetails = false;

            // get work array
            if (ServiceOrder.Work != null)
                WorkItemsArray = ServiceOrder.Work.Select(item => item.Name).ToArray();

            // set misc names
            Misc1Name = ServiceOrder.Customer.Misc1Name != null ? ServiceOrder.Customer.Misc1Name + ":" : "Misc1:";
            Misc2Name = ServiceOrder.Customer.Misc2Name != null ? ServiceOrder.Customer.Misc2Name + ":" : "Misc2:";
            Misc3Name = ServiceOrder.Customer.Misc3Name != null ? ServiceOrder.Customer.Misc3Name + ":" : "Misc3:";
            Misc4Name = ServiceOrder.Customer.Misc4Name != null ? ServiceOrder.Customer.Misc4Name + ":" : "Misc4:";
            Misc5Name = ServiceOrder.Customer.Misc5Name != null ? ServiceOrder.Customer.Misc5Name + ":" : "Misc5:";
            Misc6Name = ServiceOrder.Customer.Misc6Name != null ? ServiceOrder.Customer.Misc6Name + ":" : "Misc6:";
        }

        // CUSTOMER DETAILS
        private void OpenCloseCustomerDetails()
        {
            if (Properties.IsSODownloaded)
                ViewCustomerDetails = !ViewCustomerDetails;
        }

        // CHANGE SO
        private void FirstServiceOrder()
        {
            Properties.SOInc = 0;
            NavigateServiceOrders();
        }
        private void PreviousServiceOrder()
        {
            if (Properties.SOInc != 0)
                Properties.SOInc--;
            NavigateServiceOrders();
        }
        private void NextServiceOrder()
        {
            if (Properties.SOInc != (ServiceOrdersList.Count - 1))
                Properties.SOInc++;
            NavigateServiceOrders();
        }
        private void LastServiceOrder()
        {
            Properties.SOInc = ServiceOrdersList.Count - 1;
            NavigateServiceOrders();
        }

        // CONTACT CUSTOMER
        private async Task ContactCustomerAsync()
        {
            try
            {
                if (Properties.IsSODownloaded)
                {
                    // display contact info if not null
                    string call = "Call";
                    string text = "Text";
                    if (ServiceOrder.Customer.Phone1 != null)
                    {
                        call += ":  " + ServiceOrder.Customer.Phone1;
                        text += ":  " + ServiceOrder.Customer.Phone1;
                    }
                    else if (ServiceOrder.Customer.Phone2 != null)
                    {
                        call += ":  " + ServiceOrder.Customer.Phone2;
                        text += ":  " + ServiceOrder.Customer.Phone2;
                    }
                    string email = "Email";
                    if (ServiceOrder.Customer.Email != null)
                        email += ":  " + ServiceOrder.Customer.Email;

                    // get contact method, return if canceled/clicked out
                    string[] actionButtons = { call, text, email };
                    string contactMethod = await AppServices.UserDialogs.ShowActionSheetAsync(
                        "Select Method to Contact Customer", "Cancel", actionButtons);
                    if (contactMethod == "Cancel" || contactMethod == null)
                        return;

                    // contact customer via selected method
                    if (contactMethod == call)
                    {
                        if (ServiceOrder.Customer.Phone1 == null && ServiceOrder.Customer.Phone2 == null)
                            await AppServices.UserDialogs.ShowAlertAsync("Number Not Found", "There is no number saved for this customer.", "OK");
                        else if (ServiceOrder.Customer.Phone1 != null)
                            AppServices.OpenPhoneDialer(ServiceOrder.Customer.Phone1);
                        else if (ServiceOrder.Customer.Phone2 != null)
                            AppServices.OpenPhoneDialer(ServiceOrder.Customer.Phone2);
                    }
                    else if (contactMethod == text)
                    {
                        if (ServiceOrder.Customer.Phone1 == null && ServiceOrder.Customer.Phone2 == null)
                            await AppServices.UserDialogs.ShowAlertAsync("Number Not Found", "There is no number saved for this customer.", "OK");
                        else if (ServiceOrder.Customer.Phone1 != null)
                            await AppServices.ComposeSmsAsync(new SmsMessage(null, ServiceOrder.Customer.Phone1));
                        else if (ServiceOrder.Customer.Phone2 != null)
                            await AppServices.ComposeSmsAsync(new SmsMessage(null, ServiceOrder.Customer.Phone2));
                    }
                    else if (contactMethod == email)
                    {
                        var emailMsg = new EmailMessage()
                        {
                            Subject = "Service Order #" + ServiceOrder.SONum
                        };
                        if (ServiceOrder.Customer.Email != null)
                            emailMsg.To.Add(ServiceOrder.Customer.Email);
                        await AppServices.ComposeEmailAsync(emailMsg);
                    }
                }
            }
            catch (ArgumentNullException anEx)
            {
                    await AppServices.UserDialogs.ShowAlertAsync("Invalid Number", anEx.Message, "OK");
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                    await AppServices.UserDialogs.ShowAlertAsync("Feature Not Supported", fnsEx.Message, "OK");
            }
            catch (Exception ex)
            {
                    await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // OPEN MAPS
        private async Task OpenMapsAsync()
        {
            // nav accounts and delay to update Account before opening maps
            NavigateServiceOrders();
            await Task.Delay(1);
            await OpenMapsAsync_(ServiceOrder.Meter.Latitude, ServiceOrder.Meter.Longitude, "SO");
        }

        // OPTIONS (PHONE ONLY)
        private async Task SelectOptionAsync()
        {
            try
            {
                // get option selected, return if canceled/clicked out
                string[] options = { "Contact", "Files", "Notes", "Take Photo", "Open Maps", "Save Location" };
                    string optionSelected = await AppServices.UserDialogs.ShowActionSheetAsync("Options", "Cancel", options);
                if (optionSelected == "Cancel" || optionSelected == null)
                    return;

                // perform selected option
                switch (optionSelected)
                {
                    case "Contact":
                        await ContactCustomerAsync();
                        break;
                    case "Files":
                        await GoTo_SOFilesPageAsync();
                        break;
                    case "Notes":
                        await GoTo_NotesPageAsync_(Navigation, "SO");
                        break;
                    case "Take Photo":
                        await TakeSavePhotoAsync_("SO");
                        break;
                    case "Open Maps":
                        await OpenMapsAsync();
                        break;
                    case "Save Location":
                        await SaveCurrentLocationAsync_("SO");
                        break;
                }
            }
            catch (Exception ex)
            {
                    await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // NAVIGATION
        private async Task GoTo_SOFilesPageAsync()
        {
            try
            {
                if (Properties.IsSODownloaded)
                {
                    await Navigation.PushAsync(new SOFilesPage());
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task GoTo_SOClosePageAsync()
        {
            try
            {
                if (Properties.IsSODownloaded)
                {
                    await Navigation.PushAsync(new SOClosePage());
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // BINDING COLLECTIONS
        string[] workItemsArray;
        public string[] WorkItemsArray
        {
            get => workItemsArray;
            set
            {
                workItemsArray = value;
                OnPropertyChanged();
            }
        }

        // BINDING VARIABLES
        // -- order
        string indexLabel;
        public string IndexLabel
        {
            get => indexLabel;
            set
            {
                if (indexLabel == value)
                    return;
                indexLabel = value;
                OnPropertyChanged();
            }
        }

        // -- view bool
        bool viewCustomerDetails;
        public bool ViewCustomerDetails
        {
            get => viewCustomerDetails;
            set
            {
                viewCustomerDetails = value;
                OnPropertyChanged();
            }
        }
        
        // -- misc names
        string misc1Name;
        public string Misc1Name
        {
            get => misc1Name;
            set
            {
                misc1Name = value;
                OnPropertyChanged();
            }
        }
        string misc2Name;
        public string Misc2Name
        {
            get => misc2Name;
            set
            {
                misc2Name = value;
                OnPropertyChanged();
            }
        }
        string misc3Name;
        public string Misc3Name
        {
            get => misc3Name;
            set
            {
                misc3Name = value;
                OnPropertyChanged();
            }
        }
        string misc4Name;
        public string Misc4Name
        {
            get => misc4Name;
            set
            {
                misc4Name = value;
                OnPropertyChanged();
            }
        }
        string misc5Name;
        public string Misc5Name
        {
            get => misc5Name;
            set
            {
                misc5Name = value;
                OnPropertyChanged();
            }
        }
        string misc6Name;
        public string Misc6Name
        {
            get => misc6Name;
            set
            {
                misc6Name = value;
                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command OpenCloseCustomerDetails_Command { get; }
        public Command FirstSO_Command { get; }
        public Command PreviousSO_Command { get; }
        public Command NextSO_Command { get; }
        public Command LastSO_Command { get; }
        public Command ContactCustomer_Command { get; }
        public Command TakeSavePhoto_Command { get; }
        public Command OpenMaps_Command { get; }
        public Command SaveCurrentLocation_Command { get; }
        public Command SelectOption_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_SOFilesPage_Command { get; }
        public Command GoTo_SOClosePage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
        public Command GoTo_NotesPage_Command { get; }
    }
}
