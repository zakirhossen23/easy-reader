using System;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Xml;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FluentFTP;
using FluentFTP.Helpers;
using EasyReader.Models;
using EasyReader.Models.ServiceOrders;
using EasyReader.Services;
using EasyReader.Views;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.IO.Compression;
using Microsoft.Maui.ApplicationModel.Communication;

namespace EasyReader.ViewModels
{
    public class FileManagerViewModel : BaseViewModel
    {
        public readonly IDirectoryCreator _directoryCreator;
        public FileManagerViewModel()
        {
            _directoryCreator = ResolveDirectoryCreator();
            // PAGE COMMANDS
            // -- readings
            DownloadNewReadingsFile_Command = new Command(async () => await DownloadNewReadingsFileAsync());
            DownloadBackupReadingsFile_Command = new Command(async () => await DownloadReadingsFile_BackupFile_Async());
            DownloadBackupReadings_Command = new Command(async () => await DownloadReadingsFile_BackupAccountsAsync());
            ExportReadingsFile_Command = new Command(async () => await ExportReadingsFileAsync());

            // -- so's
            SelectSOsToDownload_Command = new Command(async () => await SelectSOsToDownloadAsync());
            FtpDownload_SOFile_Command = new Command(async () => await FtpDownload_SOFileAsync());
            SelectAllFilesToDownload_Command = new Command(execute: () => SelectAllFilesToDownload());
            CancelDownload_Command = new Command(execute: () => CancelDownload());
            GetFromBackup_SOFile_Command = new Command(async () => await GetFromBackup_SOFileAsync());
            SelectStatusesToUpload_Command = new Command(async () => await SelectStatusesToUploadAsync());
            FtpUpload_SOFile_Command = new Command(async () => await FtpUpload_SOFileAsync());
            CancelUpload_Command = new Command(execute: () => CancelUpload());
            DeleteSO_Command = new Command(async () => await DeleteSOAsync());
        }
        public FileManagerViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_AdvancedFileManagerPage_Command = new Command(async () => await GoTo_AdvancedFileManagerPageAsync());
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));

            // GET PROPERTIES + STATUS LABELS + READINGS FILE NAME
            GetProperties();
            GetAccountStatuses();
            GetServiceOrderStatuses();
            ReadingsFileNameLabel = Properties.ReadingsFileName;

            // DELETE OLD BACKUPS
            DeleteOldBackupsAsync().ConfigureAwait(true);

            // SET BOOLS
            SelectAllFilesToDownloadBool = false;
            ViewDownloadSOsSelection = false;
            ViewStatusSelection = false;

            // AUTOSAVE READINGS IF CHANGES MADE
            if (Properties.IsAccountDownloaded && Properties.AccountChangesMade)
            {
                WriteCsvToDeviceAsync(true).ConfigureAwait(true);
                Properties.AccountChangesMade = false;
                UpdateProperties();    
            }
        }
        public FileManagerViewModel(INavigation navigation, bool uploadReadings)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));

            // GET PROPERTIES + STATUS LABELS + READINGS FILE NAME
            GetProperties();
            GetAccountStatuses();
            GetServiceOrderStatuses();
            ReadingsFileNameLabel = Properties.ReadingsFileName;

            // SET BOOLS
            SelectAllFilesToDownloadBool = false;
            ViewDownloadSOsSelection = false;
            ViewStatusSelection = false;

            // UPLOAD READINGS
            if (uploadReadings)
                ExportReadingsFile_FTP_Async().ConfigureAwait(true);
                
        }

        private IDirectoryCreator ResolveDirectoryCreator()
        {
            IDirectoryCreator svc = null;
            try
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                    svc = services.GetService(typeof(IDirectoryCreator)) as IDirectoryCreator;
            }
            catch { }
            if (svc == null)
                throw new InvalidOperationException("No IDirectoryCreator implementation found. Register one in MauiProgram.");
            return svc;
        }

        // DOWNLOAD READINGS FILE
        private async Task DownloadNewReadingsFileAsync()
        {
            try
            {
                await CheckRequestStoragePermissionsAsync();

                // return if user does not want to replace the current file
                if (Properties.IsAccountDownloaded)
                {
                    if (!await Application.Current.MainPage.DisplayAlert(
                        "Warning!", "A readings file is already downloaded. Are you sure you wish to overwrite this file?",
                        "Yes, overwrite file", "Cancel"))
                        return;
                }

                // get download method, return if canceled/clicked out
                string[] downloadMethods = { "Get from Cloud", "Get from Tablet" };
                string downloadMethod = await Application.Current.MainPage.DisplayActionSheet(
                    "How would you like to download accounts?", "Cancel", null, downloadMethods);
                if (downloadMethod == "Cancel" || downloadMethod == null)
                    return;

                // download new readings
                if (downloadMethod == downloadMethods[0])
                    await DownloadReadingsFile_FTP_Async();
                else
                    await DownloadReadingsFile_Imported_Async();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }
        private async Task DownloadReadingsFile_FTP_Async()
        {
            try
            {
                // return if host folder is not set
                if (Properties.HostFolder == "")
                {
                    if (await Application.Current.MainPage.DisplayAlert("No Host Folder",
                        "Please enter a Host Folder name in the Settings page to access the server.",
                        "Go to Settings", "OK"))
                        await GoTo_SettingsPageAsync_(Navigation);
                    return;
                }

                AppServices.UserDialogs.ShowLoading("Connecting to Server...", MaskType.Gradient);

                // initialize ftp client + connect + set working directory
                // NOTE: AsyncFtpClient is required - sync FtpClient calls block
                // the UI thread on iOS (freeze/flash, empty listing).
                using var client = new AsyncFtpClient();
                NetworkCredential credentials = new NetworkCredential
                {
                    UserName = "UTILITY",
                    Password = "0rangeCounty14"
                };
                client.Host = "files.waterbill.com";
                client.Credentials = credentials;
                client.Config.EncryptionMode = FtpEncryptionMode.None;
                client.Config.SslProtocols = System.Security.Authentication.SslProtocols.None;
                client.Config.DataConnectionType = FtpDataConnectionType.PASV;
                client.Encoding = Encoding.UTF8;
                await client.Connect();
                string workingDirectory = "/" + Properties.HostFolder + "/MeterReadings/TOCLIENT/";

                // TESTING
                //string workingDirectory = "/Gretchen/MeterReadings/TOCLIENT/";

                // get file names from host folder
                FtpListItem[] ftpItems = await client.GetListing(workingDirectory);
                string[] fileNames = ftpItems
                    .Where(item => item.Type == FtpObjectType.File)
                    .Select(item => item.Name).ToArray();
                AppServices.UserDialogs.HideLoading();

                // return if no files in folder
                if (fileNames.Count() == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found",
                        "There are no readings files in the host folder.", "OK");
                    return;
                }

                // get file name to download, return if canceled/clicked out
                string fileNameToDownload = await Application.Current.MainPage.DisplayActionSheet(
                    "Download New Readings", "Cancel", null, fileNames);
                if (fileNameToDownload == "Cancel" || fileNameToDownload == null)
                    return;

                AppServices.UserDialogs.ShowLoading("Backing Up Current File...", MaskType.Gradient);

                // backup current readings
                await BackupCurrentReadingsFileAsync();
                AppServices.UserDialogs.HideLoading();
                AppServices.UserDialogs.ShowLoading("Downloading File...", MaskType.Gradient);

                // reset readings db's + set file name
                ResetReadingsDatabases();
                Properties.ReadingsFileName = fileNameToDownload;

                // set up csv reader configuration
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false,
                    MissingFieldFound = null
                };

                // read csv (async open so the UI thread is never blocked)
                using (Stream stream = await client.OpenRead(workingDirectory + fileNameToDownload))
                using (var reader = new StreamReader(stream))
                using (CsvReader csv = new CsvReader(reader, config))
                {
                    // set up csv reader
                    csv.Context.RegisterClassMap<AccountMap>();
                    int id = 0;

                    AppServices.UserDialogs.HideLoading();
                    // switch to progress loading
                    using (IProgressDialog progress =  AppServices.UserDialogs.Progress("Downloading File...", null, null, true, MaskType.Gradient))
                    {
                        // insert accounts and routes into db
                        foreach (Account acnt in csv.GetRecords<Account>())
                        {
                            // account row
                            if (id != 0)
                            {
                                acnt.ID = id;
                                acnt.LastReading.TrimStart('0');
                                if (acnt.LastReading == "")
                                    acnt.LastReading = "0";
                                acnt.NewDemand = "";
                                if (acnt.DemandYN == "Y")
                                    acnt.DemandNeeded = true;
                                else
                                    acnt.DemandNeeded = false;
                                acnt.NewUsage = "";
                                acnt.EnteredReading = false;
                                if (acnt.Latitude == "" || acnt.Longitude == "" || acnt.Latitude == "0" || acnt.Longitude == "0")
                                    acnt.IsLocationSaved = false;
                                else
                                    acnt.IsLocationSaved = true;
                                acnt.IsNull = false;

                                // insert account, create directory
                                InsertAccount(acnt);
                                _directoryCreator.CreateDirectory(GetAccountDirectory(acnt));

                                // insert route
                                Route rt = new Route
                                {
                                    RouteNumber = acnt.Route
                                };
                                InsertRoute(rt);
                            }
                            // header row
                            else
                            {
                                Properties.Misc1 = acnt.Misc1;
                                await Task.Delay(10);
                            }

                            // update progress + increment id
                            if (id % 10 == 0)
                                await Task.Delay(10);


                            //progress.PercentComplete = (int)(stream.Position / stream.Length);

                            //await Task.Delay(10000);

                            //progress.PercentComplete = (int)csv.Context.CharPosition / csv.Context.CharsRead;

                            // NEED TO FIX THIS


                            id++;
                        }
                    }
                }

                // update reading statuses + labels
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;

                // alert success, return to main page
                AppServices.UserDialogs.HideLoading();
                await Application.Current.MainPage.DisplayAlert("Success", "Readings file successfully downloaded from server.", "OK");
             
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                AppServices.UserDialogs.HideLoading();
                // socket = no wifi while trying to connect to ftp server
                // io = lost connection while trying to connect to ftp server
                if (ex is SocketException || ex is IOException)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.None)
                        await Application.Current.MainPage.DisplayAlert("No Internet Connection", "Please connect to the internet and try again.", "OK");
                    else
                        await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
            }
            finally
            {
                // update reading statuses + labels in case of error
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;

                // hide loading
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private async Task DownloadReadingsFile_Imported_Async()
        {
            try
            {
                // get list of file names imported, return if none
                List<string> fileNamesList = new List<string>();
                foreach (string filePath in Directory.GetFiles(Properties.ImportedFilesFolderPath))
                {
                    if (filePath.EndsWith(".csv"))
                        fileNamesList.Add(Path.GetFileName(filePath));
                }
                if (fileNamesList.Count == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no imported files on this device.", "OK");
                    return;
                }

                // get file name + path to download, return if canceled/clicked out
                string fileNameToDownload = await Application.Current.MainPage.DisplayActionSheet("Download Imported Accounts", "Cancel", null, fileNamesList.ToArray());
                if (fileNameToDownload == "Cancel" || fileNameToDownload == null)
                    return;
                string filePathToDownload = Path.Combine(Properties.ImportedFilesFolderPath, fileNameToDownload);

                // read csv
                await ReadCsvFromDeviceAsync(fileNameToDownload, filePathToDownload);

                // alert success, return to main page
                await Application.Current.MainPage.DisplayAlert("Success", "File successfully downloaded from imports.", "OK");
                 AppServices.UserDialogs.HideLoading();
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task DownloadReadingsFile_BackupFile_Async()
        {
            try
            {
                // get list of file names backed up, return if none
                List<string> fileNamesList = new List<string>();
                foreach (string filePath in Directory.GetFiles(Properties.AccountsBackupFilesFolderPath))
                {
                    if (filePath.EndsWith(".csv"))
                        fileNamesList.Add(Path.GetFileName(filePath));
                }
                if (fileNamesList.Count == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no backed up accounts on this device.", "OK");
                    return;
                }

                // add date identifier to autosaves
                string[] copiedFileNamesList = fileNamesList.ToArray();
                foreach (string fileName in copiedFileNamesList.Where(name => name.StartsWith("AutosavedReadings")))
                {
                    string lastWriteDate = File.GetLastWriteTime(Path.Combine(Properties.AccountsBackupFilesFolderPath, fileName)).Date.ToString("M/d/yy");
                    fileNamesList[fileNamesList.IndexOf(fileName)] = string.Format("{0} ({1})", fileName, lastWriteDate);
                }

                // get file name + path to download, return if canceled/clicked out
                string fileNameToDownload = await Application.Current.MainPage.DisplayActionSheet("Download Backed Up Accounts", "Cancel", null, fileNamesList.ToArray());
                if (fileNameToDownload == "Cancel" || fileNameToDownload == null)
                    return;
                else if (fileNameToDownload.StartsWith("AutosavedReadings"))
                    fileNameToDownload = fileNameToDownload.Substring(0, fileNameToDownload.IndexOf('.') + 4);
                string filePathToDownload = Path.Combine(Properties.AccountsBackupFilesFolderPath, fileNameToDownload);

                // return if user does not want to replace the current file
                if (Properties.IsAccountDownloaded)
                {
                    if (!await Application.Current.MainPage.DisplayAlert("Warning!",
                            "A readings file is already downloaded. Are you sure you wish to overwrite this file?", "Yes, overwrite file", "Cancel"))
                        return;
                }

                // read csv
                await ReadCsvFromDeviceAsync(fileNameToDownload, filePathToDownload);

                // alert success, return to main page
                await Application.Current.MainPage.DisplayAlert("Success", "File successfully downloaded from backups.", "OK");
                 AppServices.UserDialogs.HideLoading();
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task ReadCsvFromDeviceAsync(string fileNameToDownload, string filePathToDownload)
        {
            try
            {
                // start loading
                await Task.Delay(1);
                 AppServices.UserDialogs.ShowLoading("Backing Up Current File...", MaskType.Gradient);
                await Task.Delay(100);

                // backup current readings
                await BackupCurrentReadingsFileAsync();
                AppServices.UserDialogs.HideLoading();
                // change loading
                 AppServices.UserDialogs.ShowLoading("Downloading File...", MaskType.Gradient);

                // reset readings db's + set file name
                ResetReadingsDatabases();
                Properties.ReadingsFileName = fileNameToDownload;

                // set up csv reader configuration
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false
                };
                AppServices.UserDialogs.HideLoading();

                // read csv
                using (var streamReader = new StreamReader(filePathToDownload))
                using (CsvReader csv = new CsvReader(streamReader, config))
                using (IProgressDialog progress =  AppServices.UserDialogs.Progress("Downloading File...", null, null, true, MaskType.Gradient))
                {
                    // set up csv reader
                    csv.Context.RegisterClassMap<AccountMap>();
                    IEnumerable<Account> accounts = csv.EnumerateRecords(new Account());
                    int total = File.ReadAllLines(filePathToDownload).Count() - 1;
                    int id = 0;

                    // switch to progress loading
                    foreach (Account acnt in accounts)
                    {
                        // account row
                        if (id != 0)
                        {
                            // set extra account fields
                            acnt.ID = id;
                            acnt.LastReading.TrimStart('0');
                            if (acnt.LastReading == "")
                                acnt.LastReading = "0";
                            if (acnt.NewReading == "")
                            {
                                acnt.NewUsage = "";
                                acnt.EnteredReading = false;
                            }
                            else
                            {
                                acnt.NewUsage = (Convert.ToInt32(acnt.NewReading) - Convert.ToInt32(acnt.LastReading)).ToString();
                                acnt.EnteredReading = true;
                            }
                            if (acnt.DemandYN == "Y")
                                acnt.DemandNeeded = true;
                            else
                                acnt.DemandNeeded = false;
                            if (acnt.Latitude == "" || acnt.Longitude == "" || acnt.Latitude == "0" || acnt.Longitude == "0")
                                acnt.IsLocationSaved = false;
                            else
                                acnt.IsLocationSaved = true;
                            acnt.IsNull = false;

                            // insert account, create directory
                            InsertAccount(acnt);
                            _directoryCreator.CreateDirectory(GetAccountDirectory(acnt));

                            // insert route
                            Route rt = new Route
                            {
                                RouteNumber = acnt.Route
                            };
                            InsertRoute(rt);
                        }
                        // header row
                        else
                        {
                            // get misc1
                            Properties.Misc1 = acnt.Misc1;
                            await Task.Delay(10);
                        }

                        // update progress + increment id
                        if (id % 10 == 0)
                            await Task.Delay(1);
                        progress.PercentComplete = id * 100 / total;
                        id++;
                    }
                }

                AppServices.UserDialogs.HideLoading();
                // update reading statuses + labels
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;
            }
            catch (Exception ex)
            {
                AppServices.UserDialogs.HideLoading();
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                // update reading statuses + labels in case of error
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;

                // hide loading
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private async Task DownloadReadingsFile_BackupAccountsAsync()
        {
            try
            {
                // return if there are no backup accounts
                if (!GetAnyBackupAccountsBool())
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no backed up accounts.", "OK");
                    return;
                }

                // return if user does not want to replace the current file
                if (Properties.IsAccountDownloaded)
                {
                    if (!await Application.Current.MainPage.DisplayAlert("Warning!", "A readings file is already downloaded. The current readings will not be backed up. Are you sure you wish to overwrite this file?", "Yes, overwrite file", "Cancel"))
                        return;
                }

                // start loading
                 AppServices.UserDialogs.ShowLoading("Downloading File...", MaskType.Gradient);

                // reset readings db's + set file name + reset misc1
                string misc1 = Properties.Misc1;
                ResetReadingsDatabases();
                Properties.ReadingsFileName = Properties.BackupReadingsFileName;
                Properties.Misc1 = misc1;

                // download backup accounts
                foreach (BackupAccount bu in GetAllBackupAccountsList())
                {
                    Account acnt = new Account()
                    {
                        ID = bu.ID,
                        Route = bu.Route,
                        WalkSequence = bu.WalkSequence,
                        AccountNumber = bu.AccountNumber,
                        Name = bu.Name,
                        Address = bu.Address,
                        Location = bu.Location,
                        MeterNumber = bu.MeterNumber,
                        CustomerType = bu.CustomerType,
                        LastReadDate = bu.LastReadDate,
                        LastReading = bu.LastReading,
                        NewReadDate = bu.NewReadDate,
                        NewReadTime = bu.NewReadTime,
                        NewReading = bu.NewReading,
                        Notes = bu.Notes,
                        Misc1 = bu.Misc1,
                        Utility = bu.Utility,
                        Latitude = bu.Latitude,
                        Longitude = bu.Longitude,
                        AvgUsage = bu.AvgUsage,
                        Premise = bu.Premise,
                        DemandYN = bu.DemandYN,
                        NewUsage = bu.NewUsage,
                        DemandNeeded = bu.DemandNeeded,
                        NewDemand = bu.NewDemand,
                        EnteredReading = bu.EnteredReading,
                        IsLocationSaved = bu.IsLocationSaved,
                        IsNull = bu.IsNull
                    };

                    // insert account, create directory
                    InsertAccount(acnt);
                    _directoryCreator.CreateDirectory(GetAccountDirectory(acnt));

                    // insert route
                    Route rt = new Route
                    {
                        RouteNumber = acnt.Route
                    };
                    InsertRoute(rt);

                    // delay every 10
                    if (bu.ID % 10 == 0)
                        await Task.Delay(10);
                }

                // update reading statuses + labels
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;
                AppServices.UserDialogs.HideLoading();
                // alert success, return to main page
                await Application.Current.MainPage.DisplayAlert("Success", "File successfully downloaded from backups.", "OK");
                 AppServices.UserDialogs.HideLoading();
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                // update reading statuses + labels in case of error
                GetAccountStatuses();
                if (Properties.TotalAccounts != 0)
                    Properties.IsAccountDownloaded = true;
                UpdateProperties();
                ReadingsFileNameLabel = Properties.ReadingsFileName;

                // hide loading
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private async Task BackupCurrentReadingsFileAsync()
        {
            try
            {
                // return if no accounts in db
                if (!Properties.IsAccountDownloaded)
                    return;

                // delete current backups
                DeleteBackupAccountsDatabase();

                // reset backup file name
                Properties.BackupReadingsFileName = Properties.ReadingsFileName;

                // insert backup accounts into db
                foreach (Account acnt in GetAllAccountsList())
                {
                    BackupAccount bu = new BackupAccount()
                    {
                        ID = acnt.ID,
                        Route = acnt.Route,
                        WalkSequence = acnt.WalkSequence,
                        AccountNumber = acnt.AccountNumber,
                        Name = acnt.Name,
                        Address = acnt.Address,
                        Location = acnt.Location,
                        MeterNumber = acnt.MeterNumber,
                        CustomerType = acnt.CustomerType,
                        LastReadDate = acnt.LastReadDate,
                        LastReading = acnt.LastReading,
                        NewReadDate = acnt.NewReadDate,
                        NewReadTime = acnt.NewReadTime,
                        NewReading = acnt.NewReading,
                        Notes = acnt.Notes,
                        Misc1 = acnt.Misc1,
                        Utility = acnt.Utility,
                        Latitude = acnt.Latitude,
                        Longitude = acnt.Longitude,
                        AvgUsage = acnt.AvgUsage,
                        Premise = acnt.Premise,
                        DemandYN = acnt.DemandYN,
                        NewUsage = acnt.NewUsage,
                        DemandNeeded = acnt.DemandNeeded,
                        NewDemand = acnt.NewDemand,
                        EnteredReading = acnt.EnteredReading,
                        IsLocationSaved = acnt.IsLocationSaved,
                        IsNull = acnt.IsNull
                    };
                    InsertBackupAccount(bu);

                    // delay every 10
                    if (bu.ID % 10 == 0)
                        await Task.Delay(10);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // WRITE / UPLOAD READINGS FILE
        private async Task ExportReadingsFileAsync()
        {
            try
            {
                // storage permissions
                await CheckRequestStoragePermissionsAsync();

                // return if no readings are downloaded
                if (!Properties.IsAccountDownloaded)
                {
                    await Application.Current.MainPage.DisplayAlert("No Readings", "There are no readings to export.", "OK");
                    return;
                }

                // get export method, return if canceled/clicked out
                string[] exportMethods = { "Upload to Cloud", "Save to Device/Email" };
                string exportMethod = await Application.Current.MainPage.DisplayActionSheet("How would you like to export accounts?", "Cancel", null, exportMethods);
                if (exportMethod == "Cancel" || exportMethod == null)
                    return;

                // download new readings
                if (exportMethod == exportMethods[0])
                    await ExportReadingsFile_FTP_Async();
                else
                    await ExportReadingsFile_Email_Async();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task ExportReadingsFile_FTP_Async()
        {
            try
            {
                // return if host folder is not set
                if (Properties.HostFolder == "")
                {
                    if (await Application.Current.MainPage.DisplayAlert("No Host Folder",
                        "Please enter a Host Folder name in the Settings page to access the server.", "Go to Settings", "OK"))
                        await GoTo_SettingsPageAsync_(Navigation);
                    return;
                }

                // write csv, return if error
                string fileName = await WriteCsvToDeviceAsync(false);
                if (fileName == "error")
                    return;
                
                 AppServices.UserDialogs.ShowLoading("Connecting to Server...", MaskType.Gradient);

                // initialize ftp client, connect, set working directory
                var client = new FtpClient();
                NetworkCredential credentials = new NetworkCredential
                {
                    UserName = "UTILITY",
                    Password = "0rangeCounty14"
                };
                client.LoadProfile(new FtpProfile
                {
                    Host = "ftp://files.waterbill.com",
                    Credentials = credentials,
                   Encryption = FtpEncryptionMode.None,
                    Protocols = System.Security.Authentication.SslProtocols.None,
                    DataConnection = FtpDataConnectionType.PASV,
                    Encoding = Encoding.UTF8
                });
                var token = new CancellationToken();
                client.Connect();
               client.SetWorkingDirectory("/" + Properties.HostFolder + "/MeterReadings/TOHOST/");
                // TESTING
                //client.SetWorkingDirectory("/Gretchen/MeterReadings/TOHOST/");
                AppServices.UserDialogs.HideLoading();
                AppServices.UserDialogs.ShowLoading("Uploading File...", MaskType.Gradient);

                // upload file
                FtpStatus uploadStatus =  client.UploadFile(CsvWritePath, fileName, FtpRemoteExists.NoCheck, false, FtpVerify.None, null);

                // check if the upload is successful
                if (uploadStatus.IsSuccess())
                {
                    // update account statuses + label
                    GetAccountStatuses();
                    if (Properties.TotalAccounts == 0)
                        Properties.IsAccountDownloaded = false;
                    UpdateProperties();

                    // set write path to null
                    CsvWritePath = null;
                    AppServices.UserDialogs.HideLoading();
                    // alert success, return to main page
                    await Application.Current.MainPage.DisplayAlert("Success", "File successfully uploaded to server.", "OK");
                     AppServices.UserDialogs.HideLoading();
                    await GoTo_MainPageAsync_(Navigation);
                }
                else
                {
                    AppServices.UserDialogs.HideLoading();
                    // alert failure
                    await Application.Current.MainPage.DisplayAlert("Failure", "Something went wrong and the file was not uploaded. Please try again.", "OK");

                    // delete csv
                    if (CsvWritePath != null)
                        File.Delete(CsvWritePath);
                }
            }
            catch (Exception ex)
            {
                // hide loading
                 AppServices.UserDialogs.HideLoading();

                // socket = no wifi while trying to connect to ftp server
                // io = lost connection while trying to connect to ftp server
                // ftp = lost connection while trying to upload xml to ftp server
                if (ex is SocketException || ex is IOException || ex is FtpException)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.None)
                        await Application.Current.MainPage.DisplayAlert("No Internet Connection", "Please connect to the internet and try again.", "OK");
                    else
                        await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }

                // delete csv
                if (CsvWritePath != null)
                    File.Delete(CsvWritePath);
            }
            finally
            {
                // update account statuses + label in case of error
                GetAccountStatuses();
                if (Properties.TotalAccounts == 0)
                    Properties.IsAccountDownloaded = false;
                UpdateProperties();
                CsvWritePath = null;

                // hide loading 
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private async Task ExportReadingsFile_Email_Async()
        {
            try
            {
                // write csv, return if error
                string fileName = await WriteCsvToDeviceAsync(false);
                if (fileName == "error")
                    return;

                // change loading
                AppServices.UserDialogs.ShowLoading("Saving File...", MaskType.Gradient);

                // ensure the csv was written
                if (string.IsNullOrEmpty(CsvWritePath) || !File.Exists(CsvWritePath))
                {
                    AppServices.UserDialogs.HideLoading();
                    await Application.Current.MainPage.DisplayAlert("Error", "Export file not found.", "OK");
                    return;
                }

                // make exported files folder optional: create it if missing
                try
                {
                    if (!string.IsNullOrEmpty(Properties.ExportedFilesFolderPath) && !Directory.Exists(Properties.ExportedFilesFolderPath))
                        Directory.CreateDirectory(Properties.ExportedFilesFolderPath);
                }
                catch { }

                // prefer attaching the actual written csv to the email to avoid copy issues
                string attachmentPath = CsvWritePath;

                // update account statuses + label
                GetAccountStatuses();
                if (Properties.TotalAccounts == 0)
                    Properties.IsAccountDownloaded = false;
                UpdateProperties();

                // alert success
                AppServices.UserDialogs.HideLoading();
                await Application.Current.MainPage.DisplayAlert("Success", "File successfully saved to device.", "OK");

                // write email (attach the generated csv)
                string date = DateTime.Now.ToString("d");
                try
                {
                    // ensure attachment still exists and attach only the valid path
                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        // Copy attachment to cache directory so external apps (mail clients) can access it reliably
                        string cacheDir = FileSystem.CacheDirectory;
                        string tempFile = Path.Combine(cacheDir, Path.GetFileName(attachmentPath));
                        try
                        {
                            File.Copy(attachmentPath, tempFile, true);
                        }
                        catch
                        {
                            // ignore copy failures, we'll try to use original path
                            tempFile = attachmentPath;
                        }

                        EmailMessage email = new EmailMessage
                        {
                            Subject = "Meter Readings File " + date
                        };
                        email.Attachments.Add(new EmailAttachment(attachmentPath));

                        try
                        {
                            // ComposeAsync shows the native composer. Await it before deleting/clearing the file.
                            await Email.ComposeAsync(email);

                            // If you want to clear the CsvWritePath after successful compose, do it now.
                            CsvWritePath = null;

                            // Remove temporary copy if it exists and is different from original
                            try
                            {
                                if (tempFile != null && tempFile != attachmentPath && File.Exists(tempFile))
                                    File.Delete(tempFile);
                            }
                            catch { }
                        }
                        catch (FeatureNotSupportedException)
                        {
                            try
                            {
                                 AppServices.UserDialogs.ShowLoading("Preparing files...");
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
                                    if (File.Exists(attachmentPath))
                                        z.CreateEntryFromFile(attachmentPath, Path.GetFileName(attachmentPath));
                                }

                                await Share.RequestAsync(new ShareFileRequest
                                {
                                    Title = "Share Meter Readings File",
                                    File = new ShareFile(zipPath)
                                });
                            }
                            catch (Exception sex)
                            {
                                await AppServices.UserDialogs.ShowAlertAsync("Error", "Unable to share file: " + sex.Message, "OK");
                            }
                            finally
                            {
                                AppServices.UserDialogs.HideLoading();
                            }
                        }
                        catch (Exception ex)
                        {
                            await AppServices.UserDialogs.ShowAlertAsync("Error sending email", ex.Message, "OK");
                        }
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Error", "Attachment file not found.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    // On some platforms/devices Email.ComposeAsync is not supported. Fall back to Share.
                    if (ex is NotSupportedException || ex is PlatformNotSupportedException || (ex.Message != null && ex.Message.ToLower().Contains("not supported")))
                    {
                        try
                        {
                            await Share.RequestAsync(new ShareFileRequest
                            {
                                Title = "Share Meter Readings File",
                                File = new ShareFile(attachmentPath)
                            });
                        }
                        catch (Exception sex)
                        {
                            await Application.Current.MainPage.DisplayAlert("Error", "Unable to share file: " + sex.Message, "OK");
                        }
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Error", "Unable to compose email: " + ex.Message, "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                // update account statuses + label in case of error
                GetAccountStatuses();
                if (Properties.TotalAccounts == 0)
                    Properties.IsAccountDownloaded = false;
                UpdateProperties();
                CsvWritePath = null;

                // hide loading 
                 AppServices.UserDialogs.HideLoading();
            }
        }


        private async Task<string> WriteCsvToDeviceAsync(bool isAutosave)
        {
            try
            {
                // start loading
                string message = isAutosave ? "Backup up Accounts..." : "Writing File...";
                 AppServices.UserDialogs.ShowLoading(message, MaskType.Gradient);
                await Task.Delay(10);

                // create and write csv file
                CsvWritePath = await GetCsvFileAsync(isAutosave);
                using (FileStream f = File.Create(CsvWritePath))
                    f.Close();
                await WriteCsvAsync(CsvWritePath);
                AppServices.UserDialogs.HideLoading();
                // return fileName
                return CsvWritePath.Substring(Properties.AccountsBackupFilesFolderPath.Length + 1);

            }
            catch (Exception ex)
            {
                AppServices.UserDialogs.HideLoading();
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                return "error";
            }
        }
        private async Task<string> GetCsvFileAsync(bool isAutosave)
        {
            string fileName;
            string filePath;

            if (!isAutosave)
            {
                if (Properties.ReadingsFileName.StartsWith("Updated "))
                {
                    // check if file has already been versioned
                    if (Properties.ReadingsFileName.Substring(0, Properties.ReadingsFileName.Length - 5).EndsWith("_"))
                        fileName = Properties.ReadingsFileName.Substring(0, Properties.ReadingsFileName.Length - 6) + ".csv";
                    else
                        fileName = Properties.ReadingsFileName;
                }
                else
                {
                    fileName = "Updated " + Properties.ReadingsFileName;
                }

                filePath = Properties.AccountsBackupFilesFolderPath + "/" + fileName;

                if (File.Exists(filePath))
                {
                    if (await Application.Current.MainPage.DisplayAlert("Warning!",
                        "A backed up readings file with this name already exists. Would you like to overwrite the file or create a new file?",
                        "Create new file", "Overwrite file"))
                    {
                        string updatedName = fileName;
                        int version = 1;
                        while (File.Exists(filePath))
                        {
                            fileName = updatedName.Substring(0, updatedName.Length - 4) + "_" + version.ToString() + ".csv";
                            filePath = Properties.AccountsBackupFilesFolderPath + "/" + fileName;
                            version++;
                        }
                    }
                }
            }
            else
            {
                string currentFilePath;
                string oldestFilePath = "";
                DateTime oldestDt = DateTime.Now;

                for (int i = 1; i <= 5; i++)
                {
                    currentFilePath = Properties.AccountsBackupFilesFolderPath + "/AutosavedReadings_" + i.ToString() + ".csv";
                    if (File.Exists(currentFilePath))
                    {
                        DateTime fileDt = File.GetLastWriteTime(currentFilePath);
                        if (DateTime.Compare(fileDt, oldestDt) < 0)
                        {
                            oldestFilePath = currentFilePath;
                            oldestDt = fileDt;
                        }
                    }
                    else
                    {
                        oldestFilePath = currentFilePath;
                        break;
                    }
                }

                filePath = oldestFilePath;
            }

            return filePath;
        }
        private async Task WriteCsvAsync(string csvWritePath)
        {
            try
            {
                using (CrossCsvWriter writer = new CrossCsvWriter(csvWritePath))
                using (IProgressDialog progress =  AppServices.UserDialogs.Progress("Writing File...", null, null, true, MaskType.Gradient))
                {
                    for (int i = 0; i < Properties.TotalAccounts + 1; i++)
                    {
                        // account row
                        if (i != 0)
                        {
                            Account writeAccount = GetAccount_FromID(i);
                            CsvRow row = new CsvRow
                            {
                                string.Format("\"{0}\"", writeAccount.Route),
                                string.Format("\"{0}\"", writeAccount.WalkSequence),
                                string.Format("\"{0}\"", writeAccount.AccountNumber),
                                string.Format("\"{0}\"", writeAccount.Name),
                                string.Format("\"{0}\"", writeAccount.Address),
                                string.Format("\"{0}\"", writeAccount.Location),
                                string.Format("\"{0}\"", writeAccount.MeterNumber),
                                string.Format("\"{0}\"", writeAccount.CustomerType),
                                string.Format("\"{0}\"", writeAccount.LastReadDate),
                                string.Format("\"{0}\"", writeAccount.LastReading),
                                string.Format("\"{0}\"", writeAccount.NewReadDate),
                                string.Format("\"{0}\"", writeAccount.NewReadTime),
                                string.Format("\"{0}\"", writeAccount.NewReading),
                                string.Format("\"{0}\"", writeAccount.Notes),
                                string.Format("\"{0}\"", writeAccount.Misc1),
                                string.Format("\"{0}\"", writeAccount.Utility),
                                string.Format("\"{0}\"", writeAccount.Latitude),
                                string.Format("\"{0}\"", writeAccount.Longitude),
                                string.Format("\"{0}\"", writeAccount.AvgUsage),
                                string.Format("\"{0}\"", writeAccount.Premise),
                                string.Format("\"{0}\"", writeAccount.DemandYN),
                                string.Format("\"{0}\"", writeAccount.NewDemand),
                                string.Format("\"{0}\"", writeAccount.Balance)
                            };
                            await writer.WriteRow(row);
                        }
                        // header row
                        else
                        {
                            CsvRow headerRow = new CsvRow
                            {
                            string.Format("\"{0}\"", "Route"),
                            string.Format("\"{0}\"", "Walk Seq"),
                            string.Format("\"{0}\"", "Account#"),
                            string.Format("\"{0}\"", "Name"),
                            string.Format("\"{0}\"", "Address"),
                            string.Format("\"{0}\"", "Location"),
                            string.Format("\"{0}\"", "Meter#"),
                            string.Format("\"{0}\"", "Cust Type"),
                            string.Format("\"{0}\"", "Last Read Date"),
                            string.Format("\"{0}\"", "Last Reading"),
                            string.Format("\"{0}\"", "New Read Date"),
                            string.Format("\"{0}\"", "New Read Time"),
                            string.Format("\"{0}\"", "New Reading"),
                            string.Format("\"{0}\"", "Notes"),
                            string.Format("\"{0}\"", Properties.Misc1),
                            string.Format("\"{0}\"", "Utility"),
                            string.Format("\"{0}\"", "Latitude"),
                            string.Format("\"{0}\"", "Longitude"),
                            string.Format("\"{0}\"", "AvgUsageRaw"),
                            string.Format("\"{0}\"", "Premise"),
                            string.Format("\"{0}\"", "DemandYN"),
                            string.Format("\"{0}\"", "Demread"),
                            string.Format("\"{0}\"", "Customer Balance")
                            };
                            await writer.WriteRow(headerRow);
                        }

                        // update progress
                        if (i % 10 == 0)
                            await Task.Delay(1);
                        progress.PercentComplete = i * 100 / Properties.TotalAccounts;
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // DOWNLOAD SO FILE
        private async Task SelectSOsToDownloadAsync()
        {
            try
            {
                // return if host folder is not set
                if (Properties.HostFolder == "")
                {
                    if (await Application.Current.MainPage.DisplayAlert("No Host Folder",
                            "Please enter a Host Folder name in the Settings page to access the server.", "Go to Settings", "OK"))
                        await GoTo_SettingsPageAsync_(Navigation);
                    return;
                }

                // start loading
                 AppServices.UserDialogs.ShowLoading("Connecting to Server...", MaskType.Gradient);

                // initialize ftp client, connect, set working directory
                FtpClient = new FtpClient();
                NetworkCredential credentials = new NetworkCredential
                {
                    UserName = "UTILITY",
                    Password = "0rangeCounty14"
                };
                FtpClient.LoadProfile(new FtpProfile
                {
                    Host = "ftp://files.waterbill.com",
                    Credentials = credentials,
                   Encryption = FtpEncryptionMode.None,
                    Protocols = System.Security.Authentication.SslProtocols.None,
                    DataConnection = FtpDataConnectionType.PASV,
                    Encoding = Encoding.UTF8
                });
                FtpClient.Connect();
                string workingDirectory = "/" + Properties.HostFolder + "/ServiceOrders/TOCLIENT/";
                //string workingDirectory = "/Gretchen/ServiceOrders/TOCLIENT/"; // TESTING
                AppServices.UserDialogs.HideLoading();
                // get paths of all so's in folder, return if none
                FtpListItem[] ftpItems = FtpClient.GetListing(workingDirectory);
                string[] fileNames = ftpItems.Select(item => item.Name).ToArray();
                if (fileNames.Count() == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no service order files in the host folder.", "OK");
                    return;
                }

                // get download method, return if canceled/clicked out
                string downloadAll = "Download all files (" + fileNames.Count().ToString() + ")";
                string[] downloadOptions = { downloadAll, "Select files to download" };
                string downloadMethod = await Application.Current.MainPage.DisplayActionSheet(
                    "Choose Files to Download", "Cancel", null, downloadOptions);
                if (downloadMethod == "Cancel" || downloadMethod == null)
                    return;

                // reset files list
                FilesList = new List<FtpFile>();

                // download all files
                if (downloadMethod == downloadAll)
                {
                    foreach (string file in fileNames)
                    {
                        FtpFile ftpFile = new FtpFile
                        {
                            Name = file,
                            IsSelected = true
                        };
                        FilesList.Add(ftpFile);
                    }
                    await FtpDownload_SOFileAsync();
                }
                // select files to download
                else if (downloadMethod == "Select files to download")
                {
                    List<FtpFile> ftpFiles = new List<FtpFile>();
                    foreach (string file in fileNames)
                    {
                        FtpFile ftpFile = new FtpFile
                        {
                            Name = file,
                            IsSelected = false
                        };
                        ftpFiles.Add(ftpFile);
                    }
                    FilesList = ftpFiles;
                    // Ensure UI changes occur on main thread
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => ViewDownloadSOsSelection = true);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private async Task FtpDownload_SOFileAsync()
        {
            try
            {
                // return if no files selected
                if (!FilesList.Where(f => f.IsSelected).Any())
                {
                    await Application.Current.MainPage.DisplayAlert("Invalid Request", "Please select at least 1 file to download.", "OK");
                    return;
                }

                // start loading
                int filesDownloading = FilesList.Where(f => f.IsSelected).Count();
                if (filesDownloading == 1)
                     AppServices.UserDialogs.ShowLoading("Downloading 1 File...", MaskType.Gradient);
                else
                     AppServices.UserDialogs.ShowLoading("Downloading " + filesDownloading.ToString() + " Files...", MaskType.Gradient);

                // close so selection (ensure on main thread)
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => ViewDownloadSOsSelection = false);

                // delete tables from db
                DeleteTables();

                // initialize local variables + xml reader
                string workingDirectory = "/" + Properties.HostFolder + "/ServiceOrders/TOCLIENT/";
                //string workingDirectory = "/Gretchen/ServiceOrders/TOCLIENT/"; // TESTING
                AppServices.UserDialogs.HideLoading();
                // get paths of all so's in folder, return if none
                FtpListItem[] ftpItems = FtpClient.GetListing(workingDirectory);
                string[] fileNames = ftpItems.Select(item => item.Name).ToArray();
                if (fileNames.Count() == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no service order files in the host folder.", "OK");
                    return;
                }

                // initialize local variables + xml reader
                string XmlFirstTier = "none";
                string XmlSecondTier = "none";
                bool firstSO = true;
                ServiceOrder so = new ServiceOrder();
                PartItem part = new PartItem();
                ActionItem action = new ActionItem();
                Technician tech = new Technician();
                XmlReaderSettings xmlReaderSettings = new XmlReaderSettings
                {
                    Async = true
                };

                // read all xmls
                foreach (FtpFile file in FilesList.Where(f => f.IsSelected))
                {
                    // switch to per-file download loading to provide better feedback
                    AppServices.UserDialogs.HideLoading();
                    AppServices.UserDialogs.ShowLoading("Downloading File...", MaskType.Gradient);
                    using (var stream = FtpClient.OpenRead(workingDirectory + file.Name))
                    using (XmlReader xmlReader = XmlReader.Create(stream, xmlReaderSettings))
                    {
                        while (xmlReader.Read())
                        {
                            if (xmlReader.IsStartElement())
                            {
                                // get start element
                                string startElement = xmlReader.Name;

                                // check for first tier (Orders, Tables)
                                switch (startElement)
                                {
                                    case "Orders":
                                        XmlFirstTier = "Orders";
                                        break;
                                    case "Tables":
                                        XmlFirstTier = "Tables";
                                        break;
                                }

                                // so related
                                if (XmlFirstTier == "Orders")
                                {
                                    // check for second tier (SO children)
                                    switch (startElement)
                                    {
                                        case "ServiceOrder":
                                            if (!firstSO)
                                            {
                                                // insert + create directory for previous so
                                                InsertServiceOrder(so);
                                                _directoryCreator.CreateDirectory(GetSODirectory(so));
                                            }
                                            so = InitSO();
                                            firstSO = false;
                                            break;
                                        case "Customer":
                                            XmlSecondTier = "Customer";
                                            break;
                                        case "Meter":
                                            XmlSecondTier = "Meter";
                                            break;
                                        case "ServiceOrderInfo":
                                            XmlSecondTier = "SOInfo";
                                            break;
                                        case "Work":
                                            XmlSecondTier = "Work";
                                            break;
                                        case "Parts":
                                            XmlSecondTier = "Parts";
                                            break;
                                    }

                                    // read service orders
                                    if (XmlSecondTier == "Customer")
                                    {
                                        switch (startElement)
                                        {
                                            case "Accountnum":
                                                so.Customer.Accountnum = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "CustomerName":
                                                so.Customer.CustomerName = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "BillAddress1":
                                                so.Customer.BillAddress1 = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "BillAddress2":
                                                so.Customer.BillAddress2 = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "BillCityState":
                                                so.Customer.BillCityState = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "BillZip":
                                                so.Customer.BillZip = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Phone1":
                                                so.Customer.Phone1 = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Phone2":
                                                so.Customer.Phone2 = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Email":
                                                so.Customer.Email = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc1Name":
                                                so.Customer.Misc1Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc1Value":
                                                so.Customer.Misc1Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc2Name":
                                                so.Customer.Misc2Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc2Value":
                                                so.Customer.Misc2Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc3Name":
                                                so.Customer.Misc3Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc3Value":
                                                so.Customer.Misc3Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc4Name":
                                                so.Customer.Misc4Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc4Value":
                                                so.Customer.Misc4Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc5Name":
                                                so.Customer.Misc5Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc5Value":
                                                so.Customer.Misc5Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc6Name":
                                                so.Customer.Misc6Name = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Misc6Value":
                                                so.Customer.Misc6Value = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "CurrentBalance":
                                                so.Customer.CurrentBalance = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "PastDue":
                                                so.Customer.PastDue = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "LastPaymentDate":
                                                so.Customer.LastPaymentDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "LastPaymentAmount":
                                                so.Customer.LastPaymentAmount = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                        }
                                    }
                                    else if (XmlSecondTier == "Meter")
                                    {
                                        switch (startElement)
                                        {
                                            case "UtilityType":
                                                so.Meter.UtitlityType = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterID":
                                                so.Meter.MeterID = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterNum":
                                                so.Meter.MeterNum = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "ServiceAddress":
                                                so.Meter.ServiceAddress = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterLocation":
                                                so.Meter.MeterLocation = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Route":
                                                so.Meter.Route = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "ReadSequence":
                                                so.Meter.ReadSequence = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MXUNum":
                                                so.Meter.MXUNum = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterBrand":
                                                so.Meter.MeterBrand = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterType":
                                                so.Meter.MeterType = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterReadType":
                                                so.Meter.MeterReadType = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterSize":
                                                so.Meter.MeterSize = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterNumDials":
                                                so.Meter.MeterNumDials = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterInstallDate":
                                                so.Meter.MeterInstallDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterLastServiceDate":
                                                so.Meter.MeterLastServiceDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterServiceCode":
                                                so.Meter.MeterServiceCode = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "LastReadDate":
                                                so.Meter.LastReadDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "LastReading":
                                                so.Meter.LastReading = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "AvgUsage":
                                                so.Meter.AvgUsage = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Backflow":
                                                so.Meter.Backflow = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Latitude":
                                                so.Meter.Latitude = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Longitude":
                                                so.Meter.Longitude = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterMisc1":
                                                so.Meter.MeterMisc1 = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "MeterSpecialText":
                                                so.Meter.MeterSpecialText = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                        }
                                    }
                                    else if (XmlSecondTier == "SOInfo")
                                    {
                                        switch (startElement)
                                        {
                                            case "SONum":
                                                so.SONum = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "FileID":
                                                so.SOInfo.FileID = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "SOCreationDate":
                                                so.SOInfo.SOCreationDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "SOStatus":
                                                so.SOInfo.SOStatus = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "SOPriority":
                                                so.SOInfo.SOPriority = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "Instructions":
                                                so.SOInfo.Instructions = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "TechAssigned":
                                                so.SOInfo.TechAssigned = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "RequestedServiceDate":
                                                so.SOInfo.RequestedServiceDate = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "RequestedServiceStartTime":
                                                so.SOInfo.RequestedServiceStartTime = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                        }
                                    }
                                    else if (XmlSecondTier == "Work" && startElement == "Workitem")
                                    {
                                        WorkItem workItem = new WorkItem
                                        {
                                            Name = await xmlReader.ReadElementContentAsStringAsync()
                                        };
                                        so.Work.Add(workItem);
                                    }
                                    else if (XmlSecondTier == "Parts" && startElement == "Partitem")
                                    {
                                        SOPartItem soPart = new SOPartItem
                                        {
                                            Code = await xmlReader.ReadElementContentAsStringAsync()
                                        };
                                        so.Parts.Add(soPart);
                                    }
                                }
                                // table related
                                else if (XmlFirstTier == "Tables")
                                {
                                    // check for second tier (tables)
                                    switch (startElement)
                                    {
                                        case "StatusTable":
                                            XmlSecondTier = "StatusTable";
                                            break;
                                        case "PartsTable":
                                            XmlSecondTier = "PartsTable";
                                            break;
                                        case "ActionsTable":
                                            XmlSecondTier = "ActionsTable";
                                            break;
                                        case "TechnicianTable":
                                            XmlSecondTier = "TechnicianTable";
                                            break;
                                    }

                                    // read tables
                                    if (XmlSecondTier == "StatusTable" && startElement == "Status")
                                    {
                                        Status status = new Status
                                        {
                                            Name = await xmlReader.ReadElementContentAsStringAsync()
                                        };
                                        InsertStatus(status);
                                    }
                                    else if (XmlSecondTier == "PartsTable")
                                    {
                                        switch (startElement)
                                        {
                                            case "PartItem":
                                                part = new PartItem();
                                                break;
                                            case "PartCode":
                                                part.Code = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "PartName":
                                                part.Name = await xmlReader.ReadElementContentAsStringAsync();
                                                InsertPartItem(part);
                                                break;
                                        }
                                    }
                                    else if (XmlSecondTier == "ActionsTable")
                                    {
                                        switch (startElement)
                                        {
                                            case "ActionItem":
                                                action = new ActionItem();
                                                break;
                                            case "ActionCode":
                                                action.ActionCode = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "ActionDesc":
                                                action.ActionDesc = await xmlReader.ReadElementContentAsStringAsync();
                                                InsertActionItem(action);
                                                break;
                                        }
                                    }
                                    else if (XmlSecondTier == "TechnicianTable")
                                    {
                                        switch (startElement)
                                        {
                                            case "Technician":
                                                tech = new Technician();
                                                break;
                                            case "TechInitials":
                                                tech.TechInitials = await xmlReader.ReadElementContentAsStringAsync();
                                                break;
                                            case "TechName":
                                                tech.TechName = await xmlReader.ReadElementContentAsStringAsync();
                                                InsertTechnician(tech);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // hide per-file loading and delete so from server
                    AppServices.UserDialogs.HideLoading();
                    FtpClient.DeleteFile(workingDirectory + file.Name);
                }
                // insert + create directory for last so
                InsertServiceOrder(so);
                _directoryCreator.CreateDirectory(GetSODirectory(so));

                // update so statuses + label
                Properties.SOInc = 0;
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders != 0)
                    Properties.IsSODownloaded = true;
                UpdateProperties();

                // alert success, return to main page
                if (filesDownloading == 1)
                    await Application.Current.MainPage.DisplayAlert("Success", "1 file successfully downloaded from server.", "OK");
                else
                    await Application.Current.MainPage.DisplayAlert("Success", filesDownloading.ToString() + " files successfully downloaded from server.", "OK");
                 AppServices.UserDialogs.HideLoading();
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                // socket = no wifi while trying to connect to ftp server
                // io = lost connection while trying to connect to ftp server
                if (ex is SocketException || ex is IOException)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.None)
                        await Application.Current.MainPage.DisplayAlert("No Internet Connection", "Please connect to the internet and try again.", "OK");
                    else
                        await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
            }
            finally
            {
                // update so statuses + label in case of error
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders != 0)
                    Properties.IsSODownloaded = true;
                UpdateProperties();

                // hide loading
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private void SelectAllFilesToDownload()
        {
            SelectAllFilesToDownloadBool = !SelectAllFilesToDownloadBool;
            List<FtpFile> ftpFiles = new List<FtpFile>();
            foreach (FtpFile file in FilesList)
            {
                file.IsSelected = SelectAllFilesToDownloadBool;
                ftpFiles.Add(file);
            }
            FilesList = ftpFiles;
        }
        private void CancelDownload()
        {
            // close so selection
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => ViewDownloadSOsSelection = false);
        }
        private async Task GetFromBackup_SOFileAsync()
        {
            try
            {
                // get array of file names backed up, return if none
                List<string> fileNamesList = new List<string>();
                foreach (string filePath in Directory.GetFiles(Properties.ServiceOrdersBackupFilesFolderPath))
                {
                    if (filePath.EndsWith(".xml"))
                        fileNamesList.Add(Path.GetFileName(filePath));
                }
                if (fileNamesList.Count == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("No Files Found", "There are no backed up service orders on this device.", "OK");
                    return;
                }

                // get file name to download, return if canceled/clicked out
                string fileNameToDownload = await Application.Current.MainPage.DisplayActionSheet("Download Backed Up Service Orders", "Cancel", null, fileNamesList.ToArray());
                if (fileNameToDownload == "Cancel" || fileNameToDownload == null)
                    return;

           

                // initialize xml reader
                XmlReaderSettings xmlReaderSettings = new XmlReaderSettings
                {
                    Async = true
                };

                // read xml
                using (var streamReader = new StreamReader(Path.Combine(Properties.ServiceOrdersBackupFilesFolderPath, fileNameToDownload)))
                using (XmlReader xmlReader = XmlReader.Create(streamReader, xmlReaderSettings))
                {
                    while (xmlReader.Read())
                    {
                        // find so num node
                        if (xmlReader.IsStartElement() && xmlReader.Name == "SONum")
                        {
                            // update so
                            string soNum = await xmlReader.ReadElementContentAsStringAsync();
                            ServiceOrder so = GetServiceOrder_FromSONum(soNum);
                            so.IsBackup = false;
                            so.BackupDT = null;
                            so.SOInfo.IsBackup = false;
                            UpdateServiceOrder(so);

                            // create so directory
                            _directoryCreator.CreateDirectory(GetSODirectory(so));
                        }
                    }
                }
                // update so statuses + label
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders != 0)
                    Properties.IsSODownloaded = true;
                UpdateProperties();

                // alert success, return to main page
                await Application.Current.MainPage.DisplayAlert("Success", "File successfully downloaded from backups.", "OK");
                await GoTo_MainPageAsync_(Navigation);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                // update so statuses + label in case of error
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders != 0)
                    Properties.IsSODownloaded = true;
                UpdateProperties();

            }
        }
        private ServiceOrder InitSO()
        {
            ServiceOrder so = new ServiceOrder()
            {
                IsBackup = false,
                IsNull = false,
                SignatureSaved = false,
                Customer = new Customer(),
                Meter = new Meter(),
                SOInfo = new SOInfo(),
                Work = new List<WorkItem>(),
                Parts = new List<SOPartItem>()
            };
            return so;
        }

        // UPLOAD SO FILE
        private async Task SelectStatusesToUploadAsync()
        {
            try
            {
                if (Properties.IsSODownloaded)
                {
                    // return if host folder is not set
                    if (Properties.HostFolder == "")
                    {
                        if (await Application.Current.MainPage.DisplayAlert("No Host Folder",
                                "Please enter a Host Folder name in the Settings page to access the server.", "Go to Settings", "OK"))
                            await GoTo_SettingsPageAsync_(Navigation);
                        return;
                    }

                    // storage permissions
                    await CheckRequestStoragePermissionsAsync();

                    // get all statuses and so's
                    StatusesList = GetStatusesList();
                    GetServiceOrdersList();

                    // update statuses
                    foreach (Status status in StatusesList)
                    {
                        status.Count = GetStatusCount(status.Name);
                        status.WriteSOs = false;
                        status.DisplayLabel = status.Name + " (" + status.Count.ToString() + ")";
                    }
                    UpdateAllStatuses(StatusesList);

                    // open status selection
                    ViewStatusSelection = true;
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("No Service Orders", "There are no service orders to upload.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task FtpUpload_SOFileAsync()
        {
            try
            {
                // get statuses to write, return if no so's will be uploaded
                bool soUploaded = false;
                List<string> writeStatusesList = new List<string>();
                foreach (Status status in StatusesList)
                {
                    if (status.WriteSOs && status.Count != 0)
                    {
                        soUploaded = true;
                        writeStatusesList.Add(status.Name);
                    }
                }
                if (!soUploaded)
                {
                    await Application.Current.MainPage.DisplayAlert("Invalid Request", "Please select at least 1 status containing 1 or more service orders to continue.", "OK");
                    return;
                }

                // close status selection
                ViewStatusSelection = false;

                // storage permissions
                await CheckRequestStoragePermissionsAsync();

                // start loading
                 AppServices.UserDialogs.ShowLoading("Writing File...", MaskType.Gradient);
                await Task.Delay(10);

                // get list of file ids
                List<string> fileIDsList = new List<string>();
                foreach (ServiceOrder order in GetAllServiceOrders())
                {
                    ServiceOrder so = GetServiceOrder_FromID(order.ID);
                    if (writeStatusesList.Contains(so.SOInfo.SOStatus))
                    {
                        if (!fileIDsList.Contains(so.SOInfo.FileID))
                            fileIDsList.Add(so.SOInfo.FileID);
                    }
                }

                // initialize
                XmlWritePaths = new List<string>();
                SOsInXmlList = new List<ServiceOrder>();
                XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
                {
                    Async = true,
                    Indent = true,
                    NewLineOnAttributes = true
                };

                // write all files
                for (int i = 0; i < fileIDsList.Count; i++)
                {
                    // create file to write to
                    string currentFileID = fileIDsList[i];
                    string fileName = "UpdatedSOs " + currentFileID + " " + DateTime.Now.ToString("MM_dd_yyyy hh_mm_ss") + ".xml";
                    XmlWritePaths.Add(Path.Combine(Properties.ServiceOrdersBackupFilesFolderPath, fileName));
                    using (FileStream f = File.Create(XmlWritePaths[i]))
                        f.Close();                    
                    
                    // write xml
                    using (XmlWriter writer = XmlWriter.Create(XmlWritePaths[i], xmlWriterSettings))
                    {
                        // write start doc
                        await writer.WriteStartDocumentAsync();
                        await writer.WriteStartElementAsync(null, "UpdatedOrders", null);

                        // write each service order
                        foreach (ServiceOrder order in GetAllServiceOrders())
                        {
                            // get so, check if status is in write list and has appropriate file id
                            ServiceOrder so = GetServiceOrder_FromID(order.ID);
                            if (writeStatusesList.Contains(so.SOInfo.SOStatus) && currentFileID == so.SOInfo.FileID)
                            {
                                // write start element
                                await writer.WriteStartElementAsync(null, "ServiceOrder", null);

                                // write customer element
                                await writer.WriteStartElementAsync(null, "Customer", null);
                                await writer.WriteElementStringAsync(null, "Accountnum", null, so.Customer.Accountnum);
                                await writer.WriteEndElementAsync();  // customer

                                // write meter elements
                                await writer.WriteStartElementAsync(null, "Meter", null);
                                await writer.WriteElementStringAsync(null, "UtilityType", null, so.Meter.UtitlityType);
                                await writer.WriteElementStringAsync(null, "MeterID", null, so.Meter.MeterID);
                                if (so.Meter.MeterNum != null)
                                    await writer.WriteElementStringAsync(null, "MeterNum", null, so.Meter.MeterNum);
                                if (so.Meter.MeterLocation != null)
                                    await writer.WriteElementStringAsync(null, "MeterLocation", null, so.Meter.MeterLocation);
                                if (so.Meter.ReadSequence != null)
                                    await writer.WriteElementStringAsync(null, "ReadSequence", null, so.Meter.ReadSequence);
                                if (so.Meter.MXUNum != null)
                                    await writer.WriteElementStringAsync(null, "MXUNum", null, so.Meter.MXUNum);
                                if (so.Meter.MeterBrand != null)
                                    await writer.WriteElementStringAsync(null, "MeterBrand", null, so.Meter.MeterBrand);
                                if (so.Meter.MeterSize != null)
                                    await writer.WriteElementStringAsync(null, "MeterSize", null, so.Meter.MeterSize);
                                if (so.Meter.MeterNumDials != null)
                                    await writer.WriteElementStringAsync(null, "MeterNumDials", null, so.Meter.MeterNumDials);
                                if (so.Meter.MeterInstallDate != null)
                                    await writer.WriteElementStringAsync(null, "MeterInstallDate", null, so.Meter.MeterInstallDate);
                                if (so.Meter.MeterLastServiceDate != null)
                                    await writer.WriteElementStringAsync(null, "MeterLastServiceDate", null, so.Meter.MeterLastServiceDate);
                                if (so.Meter.MeterServiceCode != null)
                                    await writer.WriteElementStringAsync(null, "MeterServiceCode", null, so.Meter.MeterServiceCode);
                                if (so.Meter.Latitude != null)
                                    await writer.WriteElementStringAsync(null, "Latitude", null, so.Meter.Latitude);
                                if (so.Meter.Longitude != null)
                                    await writer.WriteElementStringAsync(null, "Longitude", null, so.Meter.Longitude);
                                if (so.Meter.Backflow != null)
                                    await writer.WriteElementStringAsync(null, "Backflow", null, so.Meter.Backflow);

                                if (so.Meter.CurrentReading != null)
                                    await writer.WriteElementStringAsync(null, "Reading", null, so.Meter.CurrentReading);
                                if (so.Meter.ChangeoutDate != null)
                                {
                                    await writer.WriteElementStringAsync(null, "MeterChangeoutDate", null, so.Meter.ChangeoutDate);
                                    await writer.WriteElementStringAsync(null, "RemovedMeterFinalReading", null, so.Meter.FinalReading);
                                    await writer.WriteElementStringAsync(null, "NewMeterInitialReading", null, so.Meter.InitialReading);
                                }
                                await writer.WriteEndElementAsync();  // meter

                                // write so info elements
                                await writer.WriteStartElementAsync(null, "ServiceOrderInfo", null);
                                await writer.WriteElementStringAsync(null, "SONum", null, so.SONum);
                                if (so.SOInfo.FileID != null)
                                    await writer.WriteElementStringAsync(null, "FileID", null, so.SOInfo.FileID);
                                if (so.SOInfo.SOStatus != null)
                                    await writer.WriteElementStringAsync(null, "SOStatus", null, so.SOInfo.SOStatus);
                                if (so.SOInfo.ActionTakenCode != null)
                                    await writer.WriteElementStringAsync(null, "ActionTaken", null, so.SOInfo.ActionTakenCode);
                                if (so.SOInfo.ServicedByInitials != null)
                                    await writer.WriteElementStringAsync(null, "ServicedByTech", null, so.SOInfo.ServicedByInitials);

                                // -- service date
                                if (so.SOInfo.ServiceDate != null)
                                    await writer.WriteElementStringAsync(null, "ServiceDate", null, so.SOInfo.ServiceDate);
                                else
                                {
                                    so.SOInfo.ServiceDate = DateTime.Now.ToString("d");
                                    UpdateServiceOrder(so);
                                    await writer.WriteElementStringAsync(null, "ServiceDate", null, so.SOInfo.ServiceDate);
                                }

                                if (so.SOInfo.ServiceStartTime != null)
                                    await writer.WriteElementStringAsync(null, "ServiceStartTime", null, so.SOInfo.ServiceStartTime);

                                // -- service end time
                                if (so.SOInfo.ServiceEndTime != null)
                                    await writer.WriteElementStringAsync(null, "ServiceEndTime", null, so.SOInfo.ServiceEndTime);
                                else if (so.SOInfo.ServiceStartTime != null && so.SOInfo.ServiceEndTime == null)
                                {
                                    so.SOInfo.ServiceEndTime = DateTime.Now.ToString("h:mm tt");
                                    UpdateServiceOrder(so);
                                    await writer.WriteElementStringAsync(null, "ServiceEndTime", null, so.SOInfo.ServiceEndTime);
                                }

                                if (so.SOInfo.Notes != null)
                                    await writer.WriteElementStringAsync(null, "TechNotes", null, so.SOInfo.Notes);
                                await writer.WriteEndElementAsync();  // service order info

                                // write part elements if at least 1
                                if (so.Parts.Count != 0)
                                {
                                    await writer.WriteStartElementAsync(null, "Parts", null);
                                    foreach (SOPartItem part in so.Parts)
                                        await writer.WriteElementStringAsync(null, "Partitem", null, part.Code);
                                    await writer.WriteEndElementAsync();  // parts
                                }

                                // write so end element
                                await writer.WriteEndElementAsync();  // service order

                                // add so to xml list
                                SOsInXmlList.Add(so);
                                await Task.Delay(1);
                            }
                        }

                        // write end doc
                        await writer.WriteEndElementAsync();  // updated orders
                        await writer.FlushAsync();
                        writer.Close();
                    }
                }
                AppServices.UserDialogs.HideLoading();
                // change loading
                 AppServices.UserDialogs.ShowLoading("Connecting to Server...", MaskType.Gradient);

                // initialize ftp client, connect, set working directory
                var client2 = new FtpClient();
                NetworkCredential credentials2 = new NetworkCredential
                {
                    UserName = "UTILITY",
                    Password = "0rangeCounty14"
                };
                client2.LoadProfile(new FtpProfile
                {
                    Host = "ftp://files.waterbill.com",
                    Credentials = credentials2,
                   Encryption = FtpEncryptionMode.None,
                    Protocols = System.Security.Authentication.SslProtocols.None,
                    DataConnection = FtpDataConnectionType.PASV,
                    Encoding = Encoding.UTF8
                });
                var token2 = new CancellationToken();
                client2.Connect();
                string workingDirectory2 = "/" + Properties.HostFolder + "/ServiceOrders/TOHOST/";
                //string workingDirectory = "/Gretchen/ServiceOrders/TOHOST/"; // TESTING
                AppServices.UserDialogs.HideLoading();
                // change loading
                if (fileIDsList.Count == 1)
                     AppServices.UserDialogs.ShowLoading("Uploading File...", MaskType.Gradient);
                else
                     AppServices.UserDialogs.ShowLoading("Uploading Files...", MaskType.Gradient);

                // upload file(s)
                int filesUploaded =  client2.UploadFiles(XmlWritePaths, workingDirectory2, FtpRemoteExists.NoCheck, false, FtpVerify.None, FtpError.DeleteProcessed);

                // check if the upload is successful
                if (fileIDsList.Count == filesUploaded)
                {
                    // backup so's
                    foreach (ServiceOrder order in SOsInXmlList)
                    {
                        ServiceOrder so = GetServiceOrder_FromID(order.ID);
                        so.IsBackup = true;
                        so.BackupDT = DateTime.Now;
                        so.SOInfo.IsBackup = true;
                        UpdateServiceOrder(so);
                    }
                    
                    // update so statuses + label
                    GetServiceOrderStatuses();
                    if (Properties.TotalServiceOrders == 0)
                        Properties.IsSODownloaded = false;
                    UpdateProperties();
                    XmlWritePaths = null;
                    AppServices.UserDialogs.HideLoading();
                    // alert success, return to main page
                    if (filesUploaded == 1)
                        await Application.Current.MainPage.DisplayAlert("Success", "1 file successfully uploaded to server.", "OK");
                    else
                        await Application.Current.MainPage.DisplayAlert("Success", filesUploaded.ToString() + " files successfully uploaded to server.", "OK");
                     AppServices.UserDialogs.HideLoading();
                    await GoTo_MainPageAsync_(Navigation);
                }
                else
                {
                    AppServices.UserDialogs.HideLoading();
                    // alert failure
                    await Application.Current.MainPage.DisplayAlert("Failure", "Something went wrong and the file was not uploaded. Please try again.", "OK");

                    // delete xmls
                    if (XmlWritePaths != null)
                    {
                        foreach (string file in XmlWritePaths)
                            File.Delete(file);
                    }
                }
            }
            // catch exception, delete xml
            catch (Exception ex)
            {
                // hide loading
                 AppServices.UserDialogs.HideLoading();

                // socket = no wifi while trying to connect to ftp server
                // io = lost connection while trying to connect to ftp server
                // ftp = lost connection while trying to upload xml to ftp server
                if (ex is SocketException || ex is IOException || ex is FtpException)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.None)
                        await Application.Current.MainPage.DisplayAlert("No Internet Connection", "Please connect to the internet and try again.", "OK");
                    else
                        await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
                }

                // delete xmls
                if (XmlWritePaths != null)
                {
                    foreach (string file in XmlWritePaths)
                        File.Delete(file);
                }
            }
            finally
            {
                // update so statuses + label in case of error
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders == 0)
                    Properties.IsSODownloaded = false;
                UpdateProperties();
                XmlWritePaths = null;

                // hide loading
                 AppServices.UserDialogs.HideLoading();
            }
        }

        private void CancelUpload()
        {
            // close status selection
            ViewStatusSelection = false;
        }

        // DELETE SO
        private async Task DeleteSOAsync()
        {
            try
            {
                // get so to delete, return if canceled/clicked out
                string soNumToDelete = await Application.Current.MainPage.DisplayActionSheet("Delete Service Order",
                    "Cancel", null, GetAllServiceOrders().Select(so => so.SONum).ToArray());
                if (soNumToDelete == "Cancel" || soNumToDelete == null)
                    return;

                // confirm user wants to delete so
                if (await Application.Current.MainPage.DisplayAlert("Warning!",
                    "Are you sure you would like to delete Service Order No. " + soNumToDelete + "?" +
                    "\nThis action cannot be undone.", "Yes, continue", "Cancel"))
                {
                    DeleteServiceOrder(GetServiceOrder_FromSONum(soNumToDelete));
                }

                // update so statuses + label
                GetServiceOrderStatuses();
                if (Properties.TotalServiceOrders == 0)
                    Properties.IsSODownloaded = false;
                UpdateProperties();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // DELETE OLD CSVs + XMLs + SOs
        private async Task DeleteOldBackupsAsync()
        {
            try
            {
                // storage permissions
                await CheckRequestStoragePermissionsAsync();
                
                // make sure backup duration is valid
                if (Properties.BackupFilesDurationDays <= 0)
                {
                    Properties.BackupFilesDurationDays = 120;
                    UpdateProperties();
                }

                // check for old backup account files (csvs)
                foreach (string file in Directory.GetFiles(Properties.AccountsBackupFilesFolderPath))
                {
                    // check if file is csv but do not delete autosaved files
                    if (file.EndsWith(".csv") && !Path.GetFileName(file).StartsWith("AutosavedReadings"))
                    {
                        // get file age in days, delete if older than 365 days
                        if ((DateTime.Now - File.GetLastWriteTime(file)).Days > Properties.BackupFilesDurationDays)
                            File.Delete(file);
                    }
                }

                // check for old backup service order files (xmls)
                foreach (string file in Directory.GetFiles(Properties.ServiceOrdersBackupFilesFolderPath))
                {
                    // check if file is xml
                    if (file.EndsWith(".xml"))
                    {
                        // get file age in days, delete if older than 31 days
                        if ((DateTime.Now - File.GetLastWriteTime(file)).Days > 31)
                            File.Delete(file);
                    }
                }

                // check for old service order folders (folders)
                foreach (string directory in Directory.EnumerateDirectories(Properties.ServiceOrdersFolderPath))
                {
                    // get directory age in days, delete if older than 180 days
                    if ((DateTime.Now - Directory.GetLastWriteTime(directory)).Days > 180)
                        Directory.Delete(directory, true);
                }

                // check for old backup service orders (SOs)
                DeleteOldServiceOrders();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // NAVIGATION
        private async Task GoTo_AdvancedFileManagerPageAsync()
        {
            try
            {
                await Navigation.PushAsync(new AdvancedFileManagerPage());
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // NON BINDING COLLECTIONS
        List<string> XmlWritePaths { get; set; }
        List<ServiceOrder> SOsInXmlList { get; set; }

        // NON BINDING VARIABLES
        string CsvWritePath { get; set; }
        FtpClient FtpClient { get; set; }
        bool SelectAllFilesToDownloadBool { get; set; }

        // BINDING COLLECTIONS
        List<FtpFile> filesList;
        public List<FtpFile> FilesList
        {
            get => filesList;
            set
            {
                if (filesList == value)
                    return;
                filesList = value;
                OnPropertyChanged();
            }
        }
        List<Status> statusesList;
        public List<Status> StatusesList
        {
            get => statusesList;
            set
            {
                statusesList = value;
                OnPropertyChanged();
            }
        }

        // BINDING VARIABLES
        string readingsFileNameLabel;
        public string ReadingsFileNameLabel
        {
            get => readingsFileNameLabel;
            set
            {
                if (readingsFileNameLabel == value)
                    return;
                readingsFileNameLabel = value;
                OnPropertyChanged();
            }
        }
        bool viewDownloadSOsSelection;
        public bool ViewDownloadSOsSelection
        {
            get => viewDownloadSOsSelection;
            set
            {
                if (viewDownloadSOsSelection == value)
                    return;
                viewDownloadSOsSelection = value;
                if (value)
                    ViewFMPopup = true;
                else if (!ViewStatusSelection)
                    ViewFMPopup = false;
                OnPropertyChanged();

                UpdateProperties();
            }
        }
        bool viewStatusSelection;
        public bool ViewStatusSelection
        {
            get => viewStatusSelection;
            set
            {
                if (viewStatusSelection == value)
                    return;
                viewStatusSelection = value;
                if (value)
                    ViewFMPopup = true;
                else if (!ViewDownloadSOsSelection)
                    ViewFMPopup = false;
                OnPropertyChanged();
            }
        }
        bool viewFMPopup;
        public bool ViewFMPopup
        {
            get => viewFMPopup;
            set
            {
                if (viewFMPopup == value)
                    return;
                viewFMPopup = value;
                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command DownloadNewReadingsFile_Command { get; }
        public Command DownloadBackupReadingsFile_Command { get; }
        public Command DownloadBackupReadings_Command { get; }
        public Command ExportReadingsFile_Command { get; }
        public Command SelectSOsToDownload_Command { get; }
        public Command FtpDownload_SOFile_Command { get; }
        public Command SelectAllFilesToDownload_Command { get; }
        public Command CancelDownload_Command { get; }
        public Command SelectStatusesToUpload_Command { get; }
        public Command FtpUpload_SOFile_Command { get; }
        public Command CancelUpload_Command { get; }
        public Command GetFromBackup_SOFile_Command { get; }
        public Command DeleteSO_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_AdvancedFileManagerPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
    }
}
