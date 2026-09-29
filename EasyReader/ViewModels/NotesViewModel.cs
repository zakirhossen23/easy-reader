using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using EasyReader.Models;

namespace EasyReader.ViewModels
{
    public class NotesViewModel : BaseViewModel
    {
        public NotesViewModel() 
        {
            // PAGE COMMANDS
            SaveNote_Command = new Command(async () => await SaveNoteGoBackAsync());
            DeleteNote_Command = new Command(async () => await DeleteNoteAsync());
        }
        public NotesViewModel(INavigation navigation, string senderObject)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoBack_Command = new Command(async () => await GoBackAsync());

            // GET PROPERTIES + SENDER OBJECT ("Account" or "SO")
            GetProperties();
            SenderObject = senderObject;

            // GET ACCOUNT / SO + LABELS
            if (SenderObject == "Account")
            {
                // get account
                Account = GetAccount_FromID(Properties.ReadingIncID);
                if (Account.Notes != null)
                    NoteEntry = Account.Notes;

                // get labels
                NameLabel = Account.Name;
                RouteLabel = Account.Route;
                WalkSeqLabel = Account.WalkSequence;
                AccountNumLabel = Account.AccountNumber;
                MeterOrSOName = "Meter #:";
                MeterOrSOLabel = Account.MeterNumber;
                AddressLabel = Account.Address;
            }
            else if (SenderObject == "SO")
            {
                // get so
                ServiceOrder = GetServiceOrder_FromID(Properties.SOIncID);
                if (ServiceOrder.SOInfo.Notes != null)
                    NoteEntry = ServiceOrder.SOInfo.Notes;

                // get labels
                NameLabel = ServiceOrder.Customer.CustomerName;
                RouteLabel = ServiceOrder.Meter.Route;
                WalkSeqLabel = ServiceOrder.Meter.ReadSequence;
                AccountNumLabel = ServiceOrder.Customer.Accountnum;
                MeterOrSOName = "SO #:";
                MeterOrSOLabel = ServiceOrder.SONum;
                AddressLabel = ServiceOrder.Meter.ServiceAddress;
            }
            GetCharsRemainingLabel();
        }

        // SAVE NOTE
        private async Task SaveNoteAsync()
        {
            try
            {
                if (SenderObject == "Account")
                {
                    Account.Notes = NoteEntry;
                    UpdateAccount(Account);
                }
                else if (SenderObject == "SO")
                {
                    ServiceOrder.SOInfo.Notes = NoteEntry;
                    UpdateServiceOrder(ServiceOrder);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task SaveNoteGoBackAsync()
        {
            try
            {
                await SaveNoteAsync();
                await GoBackAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // DELETE NOTE
        private async Task DeleteNoteAsync()
        {
            try
            {
                if (await Application.Current.MainPage.DisplayAlert("Warning!",
                    "Are you sure you wish to delete the current note?", "Yes, delete note", "Cancel"))
                {
                    NoteEntry = "";
                    await SaveNoteAsync();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // GET CHARS REMAINING IN NOTE
        readonly int maxLength = 255;
        private void GetCharsRemainingLabel()
        {
            // get note length
            int currentLength;
            if (NoteEntry != null)
                currentLength = NoteEntry.Length;
            else
                currentLength = 0;

            // get chars remaining label
            CharsRemainingLabel = "Max Length: 255 characters. " + (maxLength - currentLength).ToString() + " characters remaining.";
        }

        // NAVIGATION
        private async Task GoBackAsync()
        {
            try
            {
                // check if user wants to leave page without saving note
                if ((SenderObject == "Account" && NoteEntry != Account.Notes) || (SenderObject == "SO" && NoteEntry != ServiceOrder.SOInfo.Notes))
                {
                    if (!await Application.Current.MainPage.DisplayAlert("Warning!",
                        "The current note has not been saved. Are you sure you wish to continue?", "Yes, continue", "Cancel"))
                        return;
                }

                // go back
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // NON BINDING VARIABLES
        private string SenderObject { get; set; }

        // BINDING VARIABLES
        // -- note
        string noteEntry;
        public string NoteEntry
        {
            get => noteEntry;
            set
            {
                noteEntry = value;
                GetCharsRemainingLabel();
                OnPropertyChanged();
            }
        }
        string charsRemainingLabel;
        public string CharsRemainingLabel
        {
            get => charsRemainingLabel;
            set
            {
                charsRemainingLabel = value;
                OnPropertyChanged();
            }
        }

        // -- labels
        public string NameLabel { get; set; }
        public string RouteLabel { get; set; }
        public string WalkSeqLabel { get; set; }
        public string AccountNumLabel { get; set; }
        public string MeterOrSOName { get; set; }
        public string MeterOrSOLabel { get; set; }
        public string AddressLabel { get; set; }

        // COMMANDS
        public Command SaveNote_Command { get; }
        public Command DeleteNote_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoBack_Command { get; }
    }
}