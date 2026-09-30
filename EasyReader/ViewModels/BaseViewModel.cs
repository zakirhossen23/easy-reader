using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using SQLite;
using SQLiteNetExtensions.Extensions;
using EasyReader.Models;
using EasyReader.Models.ServiceOrders;
using EasyReader.Services;
using EasyReader.Views;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        // DATABASE and TABLES
        private readonly SQLiteConnection database;
        private readonly static object collisionLock = new object();
        public BaseViewModel()
        {
            // resolve IDatabaseConnection from MAUI DI if available, otherwise fall back to DependencyService
            IDatabaseConnection dbService = null;
            try
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null)
                    dbService = services.GetService(typeof(IDatabaseConnection)) as IDatabaseConnection;
            }
            catch { }
            if (dbService == null)
                throw new InvalidOperationException("No IDatabaseConnection implementation found. Register one in MauiProgram.");
            database = dbService.DbConnection();

            // create model tables
            database.CreateTable<Account>();
            database.CreateTable<BackupAccount>();
            database.CreateTable<Properties>();
            database.CreateTable<Route>();
            database.CreateTable<ServiceOrder>();

            // so children tables
            database.CreateTable<ActionItem>();
            database.CreateTable<Customer>();
            database.CreateTable<Meter>();
            database.CreateTable<PartItem>();
            database.CreateTable<SOInfo>();
            database.CreateTable<SOPartItem>();
            database.CreateTable<Status>();
            database.CreateTable<Technician>();
            database.CreateTable<WorkItem>();

            // initialize properties if none exists
            if (!database.Table<Properties>().Any())
                InitializeProperties();
            // load the properties row into the Properties property so it's not null
            GetProperties();
        }

        // TAKE PHOTO
        public async Task TakeSavePhotoAsync_(string objectType)
        {
            try
            {
                // only enabled if a file is downloaded for object type
                if ((objectType == "Account" && !Properties.IsAccountDownloaded) || (objectType == "SO" && !Properties.IsSODownloaded))
                    return;

                // camera + storage permissions
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                var storageReadStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                var storageWriteStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (cameraStatus != PermissionStatus.Granted || storageReadStatus != PermissionStatus.Granted || storageWriteStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.Camera>();
                    await Permissions.RequestAsync<Permissions.StorageRead>();
                    await Permissions.RequestAsync<Permissions.StorageWrite>();
                }

                // camera availability
                await AppServices.Media.InitializeAsync();
                if (!AppServices.Media.IsCameraAvailable || !AppServices.Media.IsTakePhotoSupported)
                    {
                        await AppServices.UserDialogs.ShowAlertAsync("No Camera", "No camera available.", "OK");
                        return;
                    }

                // take photo, return if canceled
                AppServices.UserDialogs.ShowLoading("Loading...");
                var file = await AppServices.Media.TakePhotoAsync();
                AppServices.UserDialogs.HideLoading();
                if (file == null)
                    return;

                // name photo, return if canceled
                string photoName = await AppServices.UserDialogs.ShowPromptAsync("Photo Name",
                    "What would you like to name your photo?\n*Note: Do not add .jpg/.png to the end of the name.", "OK", "Cancel", null, -1, "");
                if (photoName == null)
                    return;
                if (photoName == "")
                    photoName = "Photo";

                // get file path, check if file already exists
                string photoPath = "";
                int version = 1;
                if (objectType == "Account")
                {
                    photoPath = GetAccountDirectory(Account) + "/" + photoName + ".jpg";
                    while (File.Exists(photoPath))
                    {
                        photoPath = GetAccountDirectory(Account) + "/" + photoName + "_" + version.ToString() + ".jpg";
                        version++;
                    }
                }
                else if (objectType == "SO")
                {
                    photoPath = GetSODirectory(ServiceOrder) + "/" + photoName + ".jpg";
                    while (File.Exists(photoPath))
                    {
                        photoPath = GetSODirectory(ServiceOrder) + "/" + photoName + "_" + version.ToString() + ".jpg";
                        version++;
                    }
                }

                // write photo bytes to file, delete file from application storage
                using (var stream = await file.OpenReadAsync())
                using (var ms = new System.IO.MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    File.WriteAllBytes(photoPath, ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
            finally
            {
                AppServices.UserDialogs.HideLoading();
            }
        }

        // GEOLOCATION
        public async Task OpenMapsAsync_(string latitude, string longitude, string objectType)
        {
            try
            {
                // only enabled if a file is downloaded for object type + location is saved
                if ((objectType == "Account" && !Properties.IsAccountDownloaded) || (objectType == "SO" && !Properties.IsSODownloaded))
                    return;
                if ((objectType == "Account" && !Account.IsLocationSaved) || (objectType == "SO" && !ServiceOrder.Meter.IsLocationSaved))
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Location Saved", "Please save a meter location to use this feature.", "OK");
                    return;
                }

                // get integer placement of first number after the decimal
                int startLat = latitude.IndexOf(".") + 1;
                int startLong = longitude.IndexOf(".") + 1;

                // get all numbers after the decimal point
                string decimalLat = latitude.Substring(startLat, latitude.Length - startLat);
                string decimalLong = longitude.Substring(startLong, longitude.Length - startLong);

                // if more than 6 digits after the decimal, set lat/long to just the first 6
                if (decimalLat.Length > 6)
                    latitude = latitude.Substring(0, startLat + 6);
                if (decimalLong.Length > 6)
                    longitude = longitude.Substring(0, startLong + 6);

                // get map name
                string mapName;
                if (objectType == "Account")
                    mapName = "Meter Number " + Account.MeterNumber;
                else if (objectType == "SO" && ServiceOrder.Meter.MeterNum != null)
                    mapName = "Meter Number " + ServiceOrder.Meter.MeterNum;
                else
                    mapName = null;

                // open maps
                await AppServices.ExternalMaps.NavigateTo(mapName, Convert.ToDouble(latitude), Convert.ToDouble(longitude));
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        public async Task SaveCurrentLocationAsync_(string objectType)
        {
            try
            {
                // return if a file is not downloaded for object type
                if ((objectType == "Account" && !Properties.IsAccountDownloaded) || (objectType == "SO" && !Properties.IsSODownloaded))
                    return;

                // return if user does not want to overwrite the saved location
                if ((objectType == "Account" && Account.IsLocationSaved) || (objectType == "SO" && ServiceOrder.Meter.IsLocationSaved))
                {
                        if (!await AppServices.UserDialogs.ShowConfirmAsync("Warning!",
                            "This meter already has a saved location.\nAre you sure you wish to overwrite the previous location?", "Yes, save new location", "Cancel"))
                        return;
                    }

                // location permission
                var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locationStatus != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                //// get geolocation, alert failure + return if null
                //AppServices.UserDialogs.ShowLoading("Loading...");
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High));
                if (location == null)
                    {
                        await AppServices.UserDialogs.ShowAlertAsync("Error", "Something went wrong. Please try again.", "OK");
                        return;
                    }

                // update account or so
                if (objectType == "Account")
                {
                    Account.Latitude = location.Latitude.ToString();
                    Account.Longitude = location.Longitude.ToString();
                    Account.IsLocationSaved = true;
                    UpdateAccount(Account);
                }
                else if (objectType == "SO")
                {
                    ServiceOrder.Meter.Latitude = location.Latitude.ToString();
                    ServiceOrder.Meter.Longitude = location.Longitude.ToString();
                    ServiceOrder.Meter.IsLocationSaved = true;
                    UpdateServiceOrder(ServiceOrder);
                }

                //// hide loading, display success message
                //AppServices.UserDialogs.HideLoading();
                    await AppServices.UserDialogs.ShowAlertAsync("Success!", "Location successfully saved.", "OK");
            }
            catch (Exception ex)
            {
                    await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
            finally
            {
                //AppServices.UserDialogs.HideLoading();
            }
        }

        // GET DIRECTORIES
        public string GetAccountDirectory(Account acnt)
        {
            return Properties.AccountsFolderPath + "/" + acnt.AccountNumber;
        }
        public string GetSODirectory(ServiceOrder so)
        {
            return Properties.ServiceOrdersFolderPath + "/" + so.SONum;
        }

        // GET STATUSES
        public void GetAccountStatuses()
        {
            Properties.MissingAccounts = database.Table<Account>().Where(acnt => !acnt.EnteredReading).Count();
            Properties.TotalAccounts = database.Table<Account>().Where(acnt => !acnt.IsNull).Count();
            MissingReadingsStatusLabel = Properties.MissingAccounts.ToString() + " out of " + Properties.TotalAccounts.ToString();
            UpdateProperties();
        }
        public void GetServiceOrderStatuses()
        {
            Properties.OpenSOs = database.Table<ServiceOrder>().Where(so => !so.IsBackup && !so.IsClosed).Count();
            Properties.TotalServiceOrders = database.Table<ServiceOrder>().Where(so => !so.IsBackup).Count();
            OpenSOsStatusLabel = Properties.OpenSOs.ToString() + " out of " + Properties.TotalServiceOrders.ToString();
            //OpenSOsStatusLabel = $"{Properties.OpenSOs} out of {Properties.TotalServiceOrders}";
            UpdateProperties();
        }

        // DB SEARCHES
        // -- incs
        public int GetReadingInc()
        {
            if (Properties.ReadingIncID != -1)
            {
                int inc = GetReadingIncFromID(Properties.ReadingIncID);
                if (inc != -1)
                    return inc;
                else
                    return 0;
            }
            else
                return 0;
        }
        public int GetReadingIncFromID(int id)
        {
            return AccountsList.FindIndex(0, AccountsList.Count, acnt => acnt.ID == id);
        }
        public int GetSOInc()
        {
            if (Properties.SOIncID != -1)
            {
                int inc = ServiceOrdersList.FindIndex(0, ServiceOrdersList.Count, so => so.ID == Properties.SOIncID);
                if (inc != -1)
                    return inc;
                else
                {
                    // if inc less than list count, return so inc
                    // else, return 0
                    if (Properties.SOInc < ServiceOrdersList.Count)
                        return Properties.SOInc;
                    else
                        return 0;
                }
            }
            else
                return 0;
        }

        // -- searches
        public List<string> GetSearchResultsFromDB(string query)
        {
            lock (collisionLock)
            {
                List<string> names = database.Table<Account>().Where(a => a.Name.ToLower().Contains(query)).ToList().Select(a => a.Name).ToList();
                List<string> walkSeqs = database.Table<Account>().Where(a => a.WalkSequence.ToLower().Contains(query)).ToList().Select(a => a.WalkSequence).ToList();
                List<string> acntNums = database.Table<Account>().Where(a => a.AccountNumber.ToLower().Contains(query)).ToList().Select(a => a.AccountNumber).ToList();
                List<string> metNums = database.Table<Account>().Where(a => a.MeterNumber.ToLower().Contains(query)).ToList().Select(a => a.MeterNumber).ToList();
                List<string> addresses = database.Table<Account>().Where(a => a.Address.ToLower().Contains(query)).ToList().Select(a => a.Address).ToList();
                List<string> premises = database.Table<Account>().Where(a => a.Premise.ToLower().Contains(query)).ToList().Select(a => a.Premise).ToList();
                return names.Concat(walkSeqs).Concat(acntNums).Concat(metNums).Concat(addresses).Concat(premises).ToList();
            }
        }
        public Account GetAccountFromSearchResult(string query)
        {
            lock (collisionLock)
            {
                // get selected account, set reading inc id
                Account account = database.Table<Account>().First(acnt => acnt.Name.ToLower() == query ||
                    acnt.WalkSequence.ToLower() == query || acnt.AccountNumber.ToLower() == query ||
                    acnt.MeterNumber.ToLower() == query || acnt.Address.ToLower() == query ||
                    acnt.Premise.ToLower() == query);
                Properties.ReadingIncID = account.ID;
                UpdateProperties();

                // return route
                return account;
            }
        }

        // STORAGE PERMISSIONS
        public async Task CheckRequestStoragePermissionsAsync()
        {
            try
            {
#if IOS || MACCATALYST
                // App files live in the sandbox on Apple platforms - no
                // runtime storage permission exists or is needed. Requesting
                // StorageRead/StorageWrite here throws and aborts the caller
                // (seen as "nothing happens" after tapping download on iOS).
                return;
#else
                var storageReadStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                var storageWriteStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (storageReadStatus != PermissionStatus.Granted || storageWriteStatus != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.StorageRead>();
                    await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
#endif
            }
            catch
            {
                // Permissions are best-effort: never let a permission failure
                // silently cancel the download behind it.
            }
        }

        // PROPERTIES
        public void GetProperties()
        {
            lock (collisionLock)
            {
                Properties = database.Get<Properties>(0);
            }
        }
        public void UpdateProperties()
        {
            lock (collisionLock)
            {
                _ = database.Update(Properties);
            }
        }
        public void InitializeProperties()
        {
            Properties initProps = new Properties
            {
                ID = 0,

                ReadingInc = 0,
                ReadingIncID = -1,
                ReadingLastEnteredID = -1,
                TotalAccounts = 0,
                MissingAccounts = 0,
                IsAccountDownloaded = false,
                ReadingsFileName = "No File Uploaded",
                Misc1 = "Misc1",
                AccountChangesMade = true,

                SOInc = 0,
                SOIncID = -1,
                TotalServiceOrders = 0,
                OpenSOs = 0,
                IsSODownloaded = false,

                HostFolder = "",
                SortMethod = "RouteSequence",
                RouteSelected = "ALL ROUTES",
                IsMissingOnly = false,
                IsOpenOnly = false,
                IsAutoAdvance = true,
                IsVarianceEnabled = true,
                Variance = "50",
                Decimals = 0,
                BackupFilesDurationDays = 120
            };
            lock (collisionLock)
            {
                database.Insert(initProps);
            }
        }

        // ACCOUNT DB METHODS
        public void GetSortAccountsList()
        {
            // get accounts list with route selected + missing only if selected
            lock (collisionLock)
            {
                if (Properties.RouteSelected == "ALL ROUTES")
                {
                    if (Properties.IsMissingOnly)
                        AccountsList = database.Table<Account>().Where(acnt => !acnt.EnteredReading).ToList();
                    else
                        AccountsList = database.Table<Account>().ToList();
                }
                else
                {
                    if (Properties.IsMissingOnly)
                        AccountsList = database.Table<Account>().Where(acnt => !acnt.EnteredReading && acnt.Route == Properties.RouteSelected).ToList();
                    else
                        AccountsList = database.Table<Account>().Where(acnt => acnt.Route == Properties.RouteSelected).ToList();
                }
            }

            // sort accounts
            SortAccountsList();
        }
        public void GetSortMissingAccountsList()
        {
            // get accounts list with route selected + missing only
            lock (collisionLock)
            {
                if (Properties.RouteSelected == "ALL ROUTES")
                    AccountsList = database.Table<Account>().Where(acnt => !acnt.EnteredReading).ToList();
                else
                    AccountsList = database.Table<Account>().Where(acnt => !acnt.EnteredReading && acnt.Route == Properties.RouteSelected).ToList();
            }

            // sort accounts
            SortAccountsList();
        }
        public void GetSortAllAccountsList()
        {
            // get accounts list with route selected
            lock (collisionLock)
            {
                if (Properties.RouteSelected == "ALL ROUTES")
                    AccountsList = database.Table<Account>().ToList();
                else
                    AccountsList = database.Table<Account>().Where(acnt => acnt.Route == Properties.RouteSelected).ToList();
            }

            // sort accounts
            SortAccountsList();
        }
        public void GetSortAllAccountsAllRoutesList()
        {
            // get accounts list
            lock (collisionLock)
            {
                AccountsList = database.Table<Account>().ToList();
            }

            // sort accounts
            SortAccountsList();
        }
        public void SortAccountsList()
        {
            if (Properties.SortMethod == "RouteSequence")
                AccountsList = AccountsList.OrderBy(a => a.Route).ThenBy(a => a.WalkSequence).ToList();
            else if (Properties.SortMethod == "AccountMeter")
                AccountsList = AccountsList.OrderBy(a => a.AccountNumber).ThenBy(a => a.MeterNumber).ToList();
            else if (Properties.SortMethod == "NameMeter")
                AccountsList = AccountsList.OrderBy(a => a.Name).ThenBy(a => a.MeterNumber).ToList();
            else if (Properties.SortMethod == "AddressMeter")
                AccountsList = AccountsList.OrderBy(a => a.Address).ThenBy(a => a.MeterNumber).ToList();
            else if (Properties.SortMethod == "Misc1")
                AccountsList = AccountsList.OrderBy(a => a.Misc1).ToList();
            else if (Properties.SortMethod == "ImportOrder")
                AccountsList = AccountsList.OrderBy(a => a.ID).ToList();
        }
        public List<Account> GetAllAccountsList()
        {
            lock (collisionLock)
            {
                return database.Table<Account>().ToList();
            }
        }
        public void InsertAccount(Account acnt)
        {
            lock (collisionLock)
            {
                database.Insert(acnt);
            }
        }
        public void UpdateAccount(Account acnt)
        {
            lock (collisionLock)
            {
                database.Update(acnt);
            }
        }
        public Account GetAccount_FromID(int id)
        {
            lock (collisionLock)
            {
                return database.Get<Account>(id);
            }
        }
        public void ResetReadingsDatabases()
        {
            // delete db's
            lock (collisionLock)
            {
                database.DeleteAll<Account>();
                database.DeleteAll<Route>();
            }

            // reset properties
            Properties.ReadingInc = 0;
            Properties.ReadingIncID = -1;
            Properties.ReadingLastEnteredID = -1;
            Properties.ReadingsFileName = "No File Uploaded";
            Properties.Misc1 = "Misc1";
            Properties.RouteSelected = "ALL ROUTES";
            UpdateProperties();
        }
        public void CreateNullAccount()
        {
            Account nullAccount = new Account
            {
                IsNull = true,
                Route = "--",
                WalkSequence = "--",
                AccountNumber = "--",
                Name = "No Readings Uploaded",
                MeterNumber = "--",
                Misc1 = "--",
                Utility = "--",
                EnteredReading = true
            };
            InsertAccount(nullAccount);
        }
        public void DeleteNullAccount()
        {
            if (database.Table<Account>().Where(acnt => acnt.IsNull).Any())
            {
                lock (collisionLock)
                {
                    database.Delete<Account>(database.Table<Account>().First(acnt => acnt.IsNull).ID);
                }
            }
        }


        // BACKUP ACCOUNT DB METHODS
        public void InsertBackupAccount(BackupAccount buAcnt)
        {
            lock (collisionLock)
            {
                database.Insert(buAcnt);
            }
        }
        public List<BackupAccount> GetAllBackupAccountsList()
        {
            lock (collisionLock)
            {
                return database.Table<BackupAccount>().ToList();
            }
        }
        public void DeleteBackupAccountsDatabase()
        {
            lock (collisionLock)
            {
                database.DeleteAll<BackupAccount>();
            }
        }
        public bool GetAnyBackupAccountsBool()
        {
            if (database.Table<BackupAccount>().Where(bu => !bu.IsNull).Any())
                return true;
            else
                return false;
        }


        // ROUTE DB METHODS
        public void InsertRoute(Route route)
        {
            lock (collisionLock)
            {
                // insert route if it is not a duplicate
                if (!database.Table<Route>().Where(rt => rt.RouteNumber == route.RouteNumber).Any())
                    database.Insert(route);
            }
        }
        public List<Route> GetRoutesList()
        {
            lock (collisionLock)
            {
                return database.Table<Route>().ToList().OrderByDescending(rt => rt.RouteNumber.Length).ThenBy(rt => rt.RouteNumber).ToList();
            }
        }

        // SERVICE ORDER DB METHODS
        public void GetServiceOrdersList()
        {
            lock (collisionLock)
            {
                if (Properties.IsOpenOnly)
                    ServiceOrdersList = database.Table<ServiceOrder>().Where(so => !so.IsBackup && !so.IsClosed).ToList();
                else
                    ServiceOrdersList = database.Table<ServiceOrder>().Where(so => !so.IsBackup).ToList();
            }
        }
        public List<ServiceOrder> GetAllServiceOrders()
        {
            lock (collisionLock)
            {
                return database.Table<ServiceOrder>().Where(so => !so.IsBackup).ToList();
            }
        }
        public void InsertServiceOrder(ServiceOrder so)
        {
            lock (collisionLock)
            {
                // delete any duplicate so
                if (database.Table<ServiceOrder>().Where(soDup => soDup.SONum == so.SONum).Any())
                    DeleteServiceOrder(GetServiceOrder_FromSONum(so.SONum));

                // insert new so
                // insert so sub models, set foreign ids
                // -- customer
                database.Insert(so.Customer);
                so.CustomerID = so.Customer.ID;

                // -- meter
                if (so.Meter.Backflow == null)
                    so.Meter.Backflow = "Off";
                if (so.Meter.Latitude != null && so.Meter.Longitude != null && so.Meter.Latitude != "0" && so.Meter.Longitude != "0")
                    so.Meter.IsLocationSaved = true;
                else
                    so.Meter.IsLocationSaved = false;
                database.Insert(so.Meter);
                so.MeterID = so.Meter.ID;

                // -- so info
                so.SOInfo.IsBackup = false;
                database.Insert(so.SOInfo);
                so.SOInfoID = so.SOInfo.ID;                

                // insert so
                database.Insert(so);

                // insert part + work items, set so ids
                if (so.Parts.Count != 0)
                {
                    foreach (SOPartItem soPart in so.Parts)
                    {
                        soPart.ServiceOrderID = so.ID;
                        database.Insert(soPart);
                    }
                }
                if (so.Work.Count != 0)
                {
                    foreach (WorkItem workItem in so.Work)
                    {
                        workItem.ServiceOrderID = so.ID;
                        database.Insert(workItem);
                    }
                }
            }
        }
        public void UpdateServiceOrder(ServiceOrder so)
        {
            lock (collisionLock)
            {
                database.Update(so);
                database.Update(so.Customer);
                database.Update(so.Meter);
                database.Update(so.SOInfo);
            }
        }
        public ServiceOrder GetServiceOrder_FromID(int id)
        {
            lock (collisionLock)
            {
                return database.GetWithChildren<ServiceOrder>(id);
            }
        }
        public ServiceOrder GetServiceOrder_FromSONum(string soNum)
        {
            lock (collisionLock)
            {
                return database.GetWithChildren<ServiceOrder>(database.Table<ServiceOrder>().First(so => so.SONum == soNum).ID);
            }
        }
        public void DeleteServiceOrder(ServiceOrder so)
        {
            lock (collisionLock)
            {
                database.Delete<Customer>(so.CustomerID);
                database.Delete<Meter>(so.MeterID);
                database.Delete<SOInfo>(so.SOInfoID);
                if (so.Parts.Count != 0)
                {
                    foreach (SOPartItem soPart in so.Parts)
                        database.Delete<SOPartItem>(soPart.ID);
                }
                if (so.Work.Count != 0)
                {
                    foreach (WorkItem work in so.Work)
                        database.Delete<WorkItem>(work.ID);
                }
                database.Delete<ServiceOrder>(so.ID);
            }
        }
        public void DeleteOldServiceOrders()
        {
            // delete all SOs that were backed up 31+ days ago
            lock (collisionLock)
            {
                foreach (ServiceOrder so in database.Table<ServiceOrder>().Where(s => s.IsBackup == true))
                {
                    if ((DateTime.Now - so.BackupDT.Value).Days > 31)
                        DeleteServiceOrder(so);
                }
            }
        }
        public void DeleteTables()
        {
            lock (collisionLock)
            {
                database.DeleteAll<ActionItem>();
                database.DeleteAll<PartItem>();
                database.DeleteAll<Status>();
                database.DeleteAll<Technician>();
            }
        }
        public void CreateNullSO()
        {
            ServiceOrder nullSO = new ServiceOrder
            {
                IsBackup = false,
                IsNull = true,
                Customer = new Customer()
                {
                    Accountnum = "--",
                    CustomerName = "No Service Orders Uploaded"
                },
                Meter = new Meter()
                {
                    MeterNum = "--",
                    Route = "--",
                    ReadSequence = "--"
                },
                SOInfo = new SOInfo(),
                Work = new List<WorkItem>(),
                Parts = new List<SOPartItem>()
            };
            InsertServiceOrder(nullSO);
        }
        public void DeleteNullSO()
        {
            if (database.Table<ServiceOrder>().Where(so => so.IsNull).Any())
            {
                ServiceOrder nullSO = database.Table<ServiceOrder>().First(so => so.IsNull);
                DeleteServiceOrder(GetServiceOrder_FromID(nullSO.ID));
            }
        }

        // SERVICE ORDER CHILDREN DB METHODS
        // -- get model counts
        public int GetStatusCount(string status)
        {
            lock (collisionLock)
            {
                return database.Table<SOInfo>().Where(so => !so.IsBackup && so.SOStatus == status).Count();
            }
        }

        // -- get matching models
        public ActionItem GetActionTaken(string actionDesc)
        {
            lock (collisionLock)
            {
                return database.Table<ActionItem>().First(action => action.ActionDesc == actionDesc);
            }
        }
        public void GetSelectedParts()
        { 
            lock (collisionLock)
            {
                // set all to false if there are no so parts
                if (ServiceOrder.Parts.Count == 0)
                {
                    foreach (PartItem part in database.Table<PartItem>())
                        part.IsSelected = false;
                }
                // find matching parts
                else
                {
                    foreach (SOPartItem soPart in ServiceOrder.Parts)
                    {
                        PartItem part = database.Table<PartItem>().First(p => p.Code == soPart.Code);
                        part.IsSelected = true;
                        UpdatePartItem(part);
                    }
                }
            }
        }
        public string GetPartName(string soPartCode)
        {
            lock (collisionLock)
            {
                return database.Table<PartItem>().First(part => part.Code == soPartCode).Name;
            }
        }
        public Technician GetTechnician(string techName)
        {
            lock (collisionLock)
            {
                return database.Table<Technician>().First(tech => tech.TechName == techName);
            }
        }

        // -- get model lists
        public List<ActionItem> GetActionItemList()
        {
            lock (collisionLock)
            {
                return database.Table<ActionItem>().ToList();
            }
        }
        public List<PartItem> GetPartItemList()
        {
            lock (collisionLock)
            {
                return database.Table<PartItem>().ToList();
            }
        }
        public List<Status> GetStatusesList()
        {
            lock (collisionLock)
            {
                return database.Table<Status>().ToList();
            }
        }
        public List<Technician> GetTechnicianList()
        {
            lock (collisionLock)
            {
                return database.Table<Technician>().ToList();
            }
           
        }

        // -- insert, update, delete models
        public void InsertActionItem(ActionItem action)
        {
            lock (collisionLock)
            {
                if (!database.Table<ActionItem>().Where(act => act.ActionCode == action.ActionCode).Any())
                    database.Insert(action);
            }
        }
        public void InsertPartItem(PartItem part)
        {
            lock (collisionLock)
            {
                if (!database.Table<PartItem>().Where(pt => pt.Code == part.Code).Any())
                    database.Insert(part);
            }
        }
        public void UpdatePartItem(PartItem part) 
        {
            lock (collisionLock)
            {
                database.Update(part);
            }
        }
        public void InsertSOPartItem(SOPartItem soPart)
        {
            lock (collisionLock)
            {
                database.Insert(soPart);
            }
        }
        public void DeleteAllSOPartItems(List<SOPartItem> soParts)
        {
            lock (collisionLock)
            {
                foreach (SOPartItem soPart in soParts)
                {
                    database.Delete<SOPartItem>(soPart.ID);
                }
            }
        }
        public void InsertStatus(Status status)
        {
            lock (collisionLock)
            {
                if (!database.Table<Status>().Where(st => st.Name == status.Name).Any())
                    database.Insert(status);
            }
        }
        public void UpdateAllStatuses(List<Status> statuses)
        {
            lock (collisionLock)
            {
                database.UpdateAll(statuses);
            }
        }
        public void InsertTechnician(Technician technician)
        {
            lock (collisionLock)
            {
                if (!database.Table<Technician>().Where(tech => tech.TechInitials == technician.TechInitials).Any())
                    database.Insert(technician);
            }
        }

        // NAVIGATION
        public async Task GoTo_MainPageAsync_(INavigation nav)
        {
            try
            {
                await nav.PopToRootAsync(true);
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        public async Task GoTo_SearchPageAsync_(INavigation nav)
        {
            try
            {
                // Prefer Shell navigation; keep nav parameter for compatibility but use Shell.Current
                await Shell.Current.GoToAsync(nameof(SearchPage));
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        public async Task GoTo_SettingsPageAsync_(INavigation nav)
        {
            try
            {
                // Use Shell navigation to open settings
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        public async Task GoTo_NotesPageAsync_(INavigation nav, string senderObject)
        {
            try
            {
                if ((senderObject == "Account" && Properties.IsAccountDownloaded) || (senderObject == "SO" && Properties.IsSODownloaded))
                {
                    await nav.PushAsync(new NotesPage(senderObject));
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // NON BINDING LISTS
        public List<Account> AccountsList { get; set; }
        public List<ServiceOrder> ServiceOrdersList { get; set; }

        // BINDING MODELS
        Account account;
        public Account Account
        {
            get => account;
            set
            {
                if (account == value)
                    return;
                account = value;
                OnPropertyChanged();
            }
        }
        Properties properties;
        public Properties Properties
        {
            get => properties;
            set
            {
                if (properties == value)
                    return;
                properties = value;
                OnPropertyChanged();
            }
        }
        ServiceOrder serviceOrder;
        public ServiceOrder ServiceOrder
        {
            get => serviceOrder;
            set
            {
                serviceOrder = value;
                OnPropertyChanged();
            }
        }

        // BINDING VARIABLES
        // -- missing statuses
        string missingReadingsStatusLabel;
        public string MissingReadingsStatusLabel
        {
            get => missingReadingsStatusLabel;
            set
            {
                if (missingReadingsStatusLabel == value)
                    return;
                missingReadingsStatusLabel = value;
                OnPropertyChanged();
            }
        }
        string openSOsStatusLabel;
        public string OpenSOsStatusLabel
        {
            get => openSOsStatusLabel;
            set
            {
                if (openSOsStatusLabel == value)
                    return;
                openSOsStatusLabel = value;
                OnPropertyChanged();
            }
        }

        // ON PROPERTY CHANGED
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
