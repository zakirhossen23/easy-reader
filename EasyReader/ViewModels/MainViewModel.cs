using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.Communication;
using EasyReader.Views;
using EasyReader.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReader.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // guard against rapid multiple navigations
        private int _isNavigatingFlag = 0;

        public MainViewModel() { }
        public MainViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_EnterReadingsPage_Command = new Command(async () => await GoTo_EnterReadingsPageAsync());
            GoTo_MissingReadingsPage_Command = new Command(async () => await GoTo_MissingReadingsPageAsync());
            GoTo_AccountDetailsPage_Command = new Command(async () => await GoTo_AccountDetailsPageAsync());
            GoTo_SearchPage_Command = new Command(async () => await GoTo_SearchPageAsync());
            GoTo_FileManagerPage_Command = new Command(async () => await GoTo_FileManagerPageAsync());
            GoTo_SODetailsPage_Command = new Command(async () => await GoTo_SODetailsPageAsync());
            GoTo_AboutPage_Command = new Command(async () => await GoTo_AboutPageAsync());
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));
            GoTo_EmailPhotosPage_Command = new Command(async () => await GoTo_EmailPhotosPageAsync());

            // GET PROPERTIES
            GetProperties();

            // DELETE NULLS
            DeleteNullAccount();
            DeleteNullSO();

            // GET STATUS LABELS
            GetAccountStatuses();
            GetServiceOrderStatuses();

            // SETUP OLD DIRECTORY PATHS - ACTIVE ONLY IN 2.1.7
            SetupOldDirectoryPaths();

            // CREATE DIRECTORIES + TRANSFER FILES FROM OLD DIRECTORIES - one background
            // task, chained in order so they cannot race each other (the transfer must
            // see the final paths, not mid-update state). Background so constructor /
            // navigation doesn't block the UI.
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await CreateDirectoriesAsync().ConfigureAwait(false);
                await TransferFilesToNewDirectoriesAsync().ConfigureAwait(false);
            });

        }

        public async Task GoTo_EmailPhotosPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                await Shell.Current.GoToAsync(nameof(Views.EmailPhotosPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }

        // CREATE DIRECTORIES
        private async Task CreateDirectoriesAsync()
        {
            try
            {
                // storage permissions
                await CheckRequestStoragePermissionsAsync();

                // Resolve IDirectoryCreator. Prefer DependencyService for backward compatibility,
                // then try MAUI's service provider. If none available, fall back to local creation.
                var dirCreator = DependencyService.Get<IDirectoryCreator>()
                                 ?? Application.Current?.Handler?.MauiContext?.Services?.GetService<IDirectoryCreator>();

                string easyReader = "EasyReader";

                if (dirCreator == null)
                {
                    // Fallback: create directories under LocalApplicationData so app doesn't crash.
                    var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var root = Path.Combine(basePath, easyReader);
                    Directory.CreateDirectory(root);

                    string accounts = Directory.CreateDirectory(Path.Combine(root, "Accounts")).FullName;
                    string accountsBackup = Directory.CreateDirectory(Path.Combine(root, "AccountsBackupFiles")).FullName;
                    string imported = Directory.CreateDirectory(Path.Combine(root, "ImportedFiles")).FullName;
                    string exported = Directory.CreateDirectory(Path.Combine(root, "ExportedFiles")).FullName;
                    string serviceOrders = Directory.CreateDirectory(Path.Combine(root, "ServiceOrders")).FullName;
                    string serviceOrdersBackup = Directory.CreateDirectory(Path.Combine(root, "ServiceOrdersBackupFiles")).FullName;

                    // save directory paths in Properties
                    Properties.AccountsFolderPath = accounts;
                    Properties.AccountsBackupFilesFolderPath = accountsBackup;
                    Properties.ImportedFilesFolderPath = imported;
                    Properties.ExportedFilesFolderPath = exported;
                    Properties.ServiceOrdersFolderPath = serviceOrders;
                    Properties.ServiceOrdersBackupFilesFolderPath = serviceOrdersBackup;
                    UpdateProperties();
                    return;
                }

                // create directories using resolved service
                _ = dirCreator.CreateDirectory(easyReader);
                string accountsPath = dirCreator.CreateDirectory(easyReader + "/Accounts");
                string accountsBackupPath = dirCreator.CreateDirectory(easyReader + "/AccountsBackupFiles");
                string importedPath = dirCreator.CreateDirectory(easyReader + "/ImportedFiles");
                string exportedPath = dirCreator.CreateDirectory(easyReader + "/ExportedFiles");
                string serviceOrdersPath = dirCreator.CreateDirectory(easyReader + "/ServiceOrders");
                string serviceOrdersBackupPath = dirCreator.CreateDirectory(easyReader + "/ServiceOrdersBackupFiles");

                // save directory paths in Properties
                Properties.AccountsFolderPath = accountsPath;
                Properties.AccountsBackupFilesFolderPath = accountsBackupPath;
                Properties.ImportedFilesFolderPath = importedPath;
                Properties.ExportedFilesFolderPath = exportedPath;
                Properties.ServiceOrdersFolderPath = serviceOrdersPath;
                Properties.ServiceOrdersBackupFilesFolderPath = serviceOrdersBackupPath;
                UpdateProperties();
            }
            catch (Exception ex)
            {
                // swallow to preserve original behavior; consider logging the error instead
                // await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // 2.1.7 DIRECTORY LOCATION CHANGES
        private void SetupOldDirectoryPaths()
        {
            Properties.AccountsFolderPath_OLD = Properties.AccountsFolderPath;
            Properties.AccountsBackupFilesFolderPath_OLD = Properties.AccountsBackupFilesFolderPath;
            Properties.ImportedFilesFolderPath_OLD = Properties.ImportedFilesFolderPath;
            Properties.ExportedFilesFolderPath_OLD = Properties.ExportedFilesFolderPath;
            Properties.ServiceOrdersFolderPath_OLD = Properties.ServiceOrdersFolderPath;
            Properties.ServiceOrdersBackupFilesFolderPath_OLD = Properties.ServiceOrdersBackupFilesFolderPath;
            UpdateProperties();
        }
        private async Task TransferFilesToNewDirectoriesAsync()
        {
            try
            {
                // storage permissions
                await CheckRequestStoragePermissionsAsync();

                // transfer files. Skip when either side is unset/missing: on a
                // first install the OLD paths are null and Directory.Move(null, ...)
                // throws ArgumentNullException (this used to pop a stray "Error"
                // alert over the UI from a background thread).
                MoveDirectoryIfChanged(Properties.AccountsFolderPath_OLD, Properties.AccountsFolderPath);
                MoveDirectoryIfChanged(Properties.AccountsBackupFilesFolderPath_OLD, Properties.AccountsBackupFilesFolderPath);
                MoveDirectoryIfChanged(Properties.ImportedFilesFolderPath_OLD, Properties.ImportedFilesFolderPath);
                MoveDirectoryIfChanged(Properties.ExportedFilesFolderPath_OLD, Properties.ExportedFilesFolderPath);
                MoveDirectoryIfChanged(Properties.ServiceOrdersFolderPath_OLD, Properties.ServiceOrdersFolderPath);
                MoveDirectoryIfChanged(Properties.ServiceOrdersBackupFilesFolderPath_OLD, Properties.ServiceOrdersBackupFilesFolderPath);
            }
            catch (Exception ex)
            {
                // Background migration must never surface UI - log instead.
                System.Diagnostics.Debug.WriteLine($"TransferFilesToNewDirectoriesAsync failed: {ex.Message}");
            }
        }
        private static void MoveDirectoryIfChanged(string oldPath, string newPath)
        {
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath) || oldPath == newPath)
                return;
            if (!Directory.Exists(oldPath) || Directory.Exists(newPath))
                return;
            Directory.Move(oldPath, newPath);
        }

        // NAVIGATION
        public async Task GoTo_EnterReadingsPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                if (!Properties.IsAccountDownloaded)
                    CreateNullAccount();
                await Shell.Current.GoToAsync(nameof(EnterReadingsPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_MissingReadingsPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                if (Properties.IsAccountDownloaded && Properties.MissingAccounts != 0)
                    await Shell.Current.GoToAsync(nameof(MissingReadingsPage));
                else
                {
                    if (await Application.Current.MainPage.DisplayAlert("No Missing Readings",
                        "There are no missing readings. Please use the Account Details page to view accounts.",
                        "Go to Account Details", "Cancel"))
                        await GoTo_AccountDetailsPageAsync();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_AccountDetailsPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                if (!Properties.IsAccountDownloaded)
                    CreateNullAccount();
                await Shell.Current.GoToAsync(nameof(AccountDetailsPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_SearchPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                await Shell.Current.GoToAsync(nameof(SearchPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_FileManagerPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                await Shell.Current.GoToAsync(nameof(FileManagerPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_SODetailsPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                if (!Properties.IsSODownloaded)
                    CreateNullSO();
                await Shell.Current.GoToAsync(nameof(SODetailsPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }
        public async Task GoTo_AboutPageAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 1) == 1)
                return;
            try
            {
                await Shell.Current.GoToAsync(nameof(AboutPage));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isNavigatingFlag, 0);
            }
        }

        // COMMANDS
        public INavigation Navigation { get; set; }
        public Command GoTo_EnterReadingsPage_Command { get; }
        public Command GoTo_MissingReadingsPage_Command { get; }
        public Command GoTo_AccountDetailsPage_Command { get; }
        public Command GoTo_SearchPage_Command { get; }
        public Command GoTo_FileManagerPage_Command { get; }
        public Command GoTo_SODetailsPage_Command { get; }
        public Command GoTo_AboutPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
        public Command GoTo_EmailPhotosPage_Command { get; }

      
    }
}