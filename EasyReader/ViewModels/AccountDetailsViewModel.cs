using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.IO.Compression;
using Microsoft.Maui.ApplicationModel.Communication;
using EasyReader.Services;
using EasyReader.Models;
using EasyReader.Views;

namespace EasyReader.ViewModels
{
    public class AccountDetailsViewModel : BaseViewModel
    {
        public AccountDetailsViewModel()
        {
            // PAGE COMMANDS
            // -- account file actions
            OpenAccountFile_Command = new Command(async () => await OpenAccountFileAsync());
            RenameAccountFile_Command = new Command(async () => await RenameAccountFileAsync());
            DeleteAccountFiles_Command = new Command(async () => await DeleteAccountFilesAsync());
            SendAccountFiles_Command = new Command(async () => await SendAccountFilesAsync());

            // -- change account
            FirstAccount_Command = new Command(execute: () => FirstAccount());
            PreviousAccount_Command = new Command(execute: () => PreviousAccount());
            NextAccount_Command = new Command(execute: () => NextAccount());
            LastAccount_Command = new Command(execute: () => LastAccount());

            // -- special functions
            TakeSavePhoto_Command = new Command(async () => await TakeSavePhotoAsync());
            OpenMaps_Command = new Command(async () => await OpenMapsAsync());
            SaveCurrentLocation_Command = new Command(async () => await SaveCurrentLocationAsync_("Account"));
        }
        public AccountDetailsViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));
            GoTo_NotesPage_Command = new Command(async () => await GoTo_NotesPageAsync_(Navigation, "Account"));

            // GET PROPERTIES + ACCOUNTS LIST
            GetProperties();
            GetSortAllAccountsList();
            Properties.AccountChangesMade = true;
            //if (sortAllRoutes)
            //    GetSortAllAccountsAllRoutesList();
            //else
            //    GetSortAllAccountsList();

            // SET BINDINGS + INITIALIZE OC
            Misc1Label = Properties.Misc1 + ":";
            GetAccountStatuses();
            AccountFilesOC = new ObservableCollection<AccountFile>();

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

            // get account files
            SelectAllFiles = false;
            GetAccountFilesOC(false);
        }

        // ACCOUNT FILE ACTIONS
        private void GetAccountFilesOC(bool isSelected)
        {
            try
            {
                // clear OC
                AccountFilesOC.Clear();

                if (Properties.IsAccountDownloaded)
                {
                    // create account files
                    foreach (string file in Directory.GetFiles(GetAccountDirectory(Account)))
                    {
                        AccountFile acntFile = new AccountFile
                        {
                            Path = file,
                            Name = Path.GetFileName(file),
                            IsSelected = isSelected
                        };
                        AccountFilesOC.Add(acntFile);
                    }

                    // reorder OC
                    var ocList = AccountFilesOC.OrderBy(f => f.Name).ToList();
                    for (int i = 0; i < ocList.Count; i++)
                        AccountFilesOC.Move(AccountFilesOC.IndexOf(ocList[i]), i);
                }
            }
            catch (Exception ex)
            {
                Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK").ConfigureAwait(false);
            }
        }
        private int GetTotalFilesSelected()
        {
            return AccountFilesOC.Where(f => f.IsSelected).Count();
        }
        private async Task OpenAccountFileAsync()
        {
            try
            {
                // get total files selected
                int filesSelected = GetTotalFilesSelected();

                // find selected file, go to gallery page
                if (filesSelected == 1)
                {
                    AccountFile file = AccountFilesOC.First(f => f.IsSelected);
                    await Navigation.PushAsync(new GalleryPage(file));
                }
                // alert: too many files selected
                else if (filesSelected > 1)
                    await Application.Current.MainPage.DisplayAlert("Too Many Files Selected", "PLease select only 1 file to open.", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task RenameAccountFileAsync()
        {
            try
            {
                // get total files selected
                int filesSelected = GetTotalFilesSelected();

                // find selected file, rename
                if (filesSelected == 1)
                {
                    // get new name of file from user, return if canceled/clicked out
                    string newFileName = await Application.Current.MainPage.DisplayPromptAsync("Rename File", "What would you like to rename this file?");
                    if (newFileName == "" || newFileName == null)
                        return;

                    // get file + file extension
                    AccountFile file = AccountFilesOC.First(f => f.IsSelected);
                    string fileExt = Path.GetExtension(file.Path);

                    // check if file exists, add version number
                    string newFilePath = GetAccountDirectory(Account) + "/" + newFileName + fileExt;
                    int version = 1;
                    while (File.Exists(newFilePath))
                    {
                        newFilePath = GetAccountDirectory(Account) + "/" + newFileName + "_" + version.ToString() + fileExt;
                        version++;
                    }

                    // create new file, copy bytes, delete old file
                    var fileBytes = File.ReadAllBytes(file.Path);
                    File.WriteAllBytes(newFilePath, fileBytes);
                    File.Delete(file.Path);

                    // refresh OC
                    GetAccountFilesOC(false);
                }
                // alert: too many files selected
                else if (filesSelected > 1)
                    await Application.Current.MainPage.DisplayAlert("Too Many Files Selected", "PLease select only 1 file to rename.", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task DeleteAccountFilesAsync()
        {
            try
            {
                // get total files selected
                int filesSelected = GetTotalFilesSelected();

                if (filesSelected > 0)
                {
                    // confirm user wants to delete files
                    bool answer = false;
                    if (filesSelected == 1)
                        answer = await Application.Current.MainPage.DisplayAlert("Warning!",
                            "Are you sure you would like to delete 1 file?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    else
                        answer = await Application.Current.MainPage.DisplayAlert("Warning!",
                            "Are you sure you would like to delete " + filesSelected.ToString() + " files?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    if (!answer)
                        return;

                    // delete selected files
                    foreach (AccountFile file in AccountFilesOC.Where(f => f.IsSelected))
                        File.Delete(file.Path);

                    // refresh OC
                    GetAccountFilesOC(false);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task SendAccountFilesAsync()
        {
            try
            {
                if (GetTotalFilesSelected() > 0)
                {
                    var selected = AccountFilesOC.Where(f => f.IsSelected).ToList();

                    var email = new EmailMessage
                    {
                        Subject = "Account Number: " + Account.AccountNumber
                    };

                    var missing = selected.Where(f => !File.Exists(f.Path)).ToList();
                    if (missing.Any())
                    {
                        await AppServices.UserDialogs.ShowAlertAsync("File Missing", $"{missing.Count} selected file(s) no longer exist on disk. Please refresh and try again.", "OK");
                        return;
                    }

                    foreach (AccountFile file in selected)
                        email.Attachments.Add(new EmailAttachment(file.Path));

                    try
                    {
                        await Email.ComposeAsync(email);
                    }
                    catch (FeatureNotSupportedException)
                    {
                        try
                        {
                            var baseDir = Properties.ImportedFilesFolderPath;
                            if (string.IsNullOrEmpty(baseDir))
                                baseDir = Path.GetTempPath();
                            var zipDir = Path.Combine(baseDir, "EmailShare");
                            Directory.CreateDirectory(zipDir);
                            var zipPath = Path.Combine(zipDir, "attachments.zip");
                            if (File.Exists(zipPath))
                                File.Delete(zipPath);

                            using (var z = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                            {
                                foreach (var f in selected)
                                {
                                    if (File.Exists(f.Path))
                                        z.CreateEntryFromFile(f.Path, Path.GetFileName(f.Path));
                                }
                            }

                            await Share.RequestAsync(new ShareFileRequest
                            {
                                Title = "Share files",
                                File = new ShareFile(zipPath)
                            });
                        }
                        catch (Exception ex2)
                        {
                            await AppServices.UserDialogs.ShowAlertAsync("Share Error", ex2.Message, "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        await AppServices.UserDialogs.ShowAlertAsync("Error sending email", ex.Message, "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // CHANGE ACCOUNT
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

        // SPECIAL FUNCTIONS
        private async Task TakeSavePhotoAsync()
        {
            // update IsTakingPhoto to prevent the binding context from updating
            IsTakingPhoto = true;
            await TakeSavePhotoAsync_("Account");
            NavigateAccounts(Properties.ReadingInc);
            IsTakingPhoto = false;
        }
        private async Task OpenMapsAsync()
        {
            // nav accounts and delay to update Account before opening maps
            NavigateAccounts(Properties.ReadingInc);
            await Task.Delay(1);
            await OpenMapsAsync_(Account.Latitude, Account.Longitude, "Account");
        }

        // BINDING COLLECTIONS
        public ObservableCollection<AccountFile> AccountFilesOC { get; set; }

        // NON BINDING VARIABLES
        public bool IsTakingPhoto { get; set; }

        // BINDING VARIABLES
        public string Misc1Label { get; set; }
        bool selectAllFiles;
        public bool SelectAllFiles
        {
            get => selectAllFiles;
            set
            {
                if (selectAllFiles == value)
                    return;
                selectAllFiles = value;
                GetAccountFilesOC(value);
                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command OpenAccountFile_Command { get; }
        public Command RenameAccountFile_Command { get; }
        public Command DeleteAccountFiles_Command { get; }
        public Command SendAccountFiles_Command { get; }
        public Command FirstAccount_Command { get; }
        public Command PreviousAccount_Command { get; }
        public Command NextAccount_Command { get; }
        public Command LastAccount_Command { get; }
        public Command TakeSavePhoto_Command { get; }
        public Command OpenMaps_Command { get; }
        public Command SaveCurrentLocation_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
        public Command GoTo_NotesPage_Command { get; }
    }
}