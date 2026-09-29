using EasyReader.Models;
using EasyReader.Models.ServiceOrders;
using EasyReader.Services;
using FluentFTP;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class SOCloseViewModel : BaseViewModel
    {
        public SOCloseViewModel() 
        {
            // PAGE COMMANDS
            // -- update so
            StartEndService_Command = new Command(async () => await StartEndServiceAsync());
            UpdateStatus_Command = new Command(async () => await UpdateStatusAsync());
            UpdateActionTaken_Command = new Command(async () => await UpdateActionTakenAsync());
            OpenPartsSelection_Command = new Command(async () => await OpenPartsSelectionAsync());
            ClosePartsSelection_Command = new Command(async () => await ClosePartsSelectionAsync());
            UpdateTechnician_Command = new Command(async () => await UpdateTechnicianAsync());

            // -- take photo
            TakeSavePhoto_Command = new Command(async () => await TakeSavePhotoAsync_("SO"));

            // -- change meter
            OpenChangeMeter_Command = new Command(execute: () => OpenChangeMeter());
            SaveChangeMeter_Command = new Command(async () => await SaveChangeMeterAsync());
            CancelChangeMeter_Command = new Command(execute: () => CancelChangeMeter());

            // -- signature
            OpenSignature_Command = new Command(execute: () => OpenSignature());
            SaveSignature_Command = new Command(async () => await SaveSignatureAsync());
            CancelSignature_Command = new Command(execute: () => CancelSignatureAsync());

            // -- save
            CloseSO_Command = new Command(async () => await CloseSOAsync());

            // -- meter details
            OpenCloseMeterDetails_Command = new Command(execute: () => OpenCloseMeterDetails());
        }
        public SOCloseViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));
            GoTo_SettingsPage_Command = new Command(async () => await GoTo_SettingsPageAsync_(Navigation));
            GoTo_NotesPage_Command = new Command(async () => await GoTo_NotesPageAsync_(Navigation, "SO"));

            // GET PROPERTIES + SO LIST
            GetProperties();
            GetServiceOrdersList();

            // SET INDEX LABEL + VIEW BOOLS
            IndexLabel = (Properties.SOInc + 1).ToString() + " out of " + ServiceOrdersList.Count.ToString();
            ViewPartsSelection = false;
            ViewChangeMeter = false;
            ViewSignature = false;
            ViewMeterDetails = false;

            // NAVIGATE SOs
            NavigateServiceOrders();

            // SET START/STOP BUTTON + BACKFLOW LABELS
            if (ServiceOrder.SOInfo.ServiceStartTime == null)
                StartStopBtnLbl = "Start";
            else
                StartStopBtnLbl = "End";
            if (BackflowSwitch)
                BackflowLabel = "On";
            else
                BackflowLabel = "Off";
        }

        // NAVIGATE SOs
        private void NavigateServiceOrders()
        {
            ServiceOrder = GetServiceOrder_FromID(Properties.SOIncID);
        }

        // SERVICE START + END SERVICE TIME
        private async Task StartEndServiceAsync()
        {
            try
            {
                // start so
                if (ServiceOrder.SOInfo.ServiceStartTime == null)
                {
                    // get start time
                    ServiceOrder.SOInfo.ServiceStartTime = DateTime.Now.ToString("h:mm tt");
                    UpdateServiceOrder(ServiceOrder);

                    // update button label, nav so's
                    StartStopBtnLbl = "End";
                    NavigateServiceOrders();
                }
                // end so
                else if (ServiceOrder.SOInfo.ServiceStartTime != null && ServiceOrder.SOInfo.ServiceEndTime == null)
                {
                    // get end time, nav so's
                    ServiceOrder.SOInfo.ServiceEndTime = DateTime.Now.ToString("h:mm tt");
                    UpdateServiceOrder(ServiceOrder);
                    NavigateServiceOrders();
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // UPDATE SERVICE ORDER
        private async Task UpdateStatusAsync()
        {
            try
            {
                // return if no statuses
                if (GetStatusesList().Count == 0)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Statuses", "There are no statuses available to select.", "OK");
                    return;
                }

                // get new status, return if canceled/clicked out
                    string newStatus = await AppServices.UserDialogs.ShowActionSheetAsync(
                        "Update Service Order Status", "Cancel", GetStatusesList().Select(st => st.Name).ToArray());
                if (newStatus == "Cancel" || newStatus == null)
                    return;

                // update so, nav so's
                ServiceOrder.SOInfo.SOStatus = newStatus;
                UpdateServiceOrder(ServiceOrder);
                NavigateServiceOrders();
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task UpdateActionTakenAsync()
        {
            try
            {
                // return if no actions
                if (GetActionItemList().Count == 0)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Actions", "There are no actions available to select.", "OK");
                    return;
                }

                // get action taken, return if canceled/clicked out
                    string actionDesc = await AppServices.UserDialogs.ShowActionSheetAsync(
                        "Select Action Taken", "Cancel", GetActionItemList().Select(a => a.ActionDesc).ToArray());
                if (actionDesc == "Cancel" || actionDesc == null)
                    return;
                ActionItem action = GetActionTaken(actionDesc);

                // update so
                ServiceOrder.SOInfo.ActionTakenCode = action.ActionCode;
                ServiceOrder.SOInfo.ActionTakenDesc = action.ActionDesc;
                UpdateServiceOrder(ServiceOrder);
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task OpenPartsSelectionAsync()
        {
            try
            {
                // return if no parts
                if (GetPartItemList().Count == 0)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Parts", "There are no parts available to select.", "OK");
                    return;
                }

                // set IsSelected for each PartItem
                GetSelectedParts();

                // get + sort parts list
                PartsList = GetPartItemList().OrderByDescending(pt => pt.IsSelected).ThenBy(pt => pt.Name).ToList();

                // open parts selection
                ViewPartsSelection = true;
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task ClosePartsSelectionAsync()
        {
            try
            {
                // delete so parts list
                if (ServiceOrder.Parts.Count != 0)
                {
                    DeleteAllSOPartItems(ServiceOrder.Parts);
                    ServiceOrder.Parts.Clear();
                }

                // add all selected parts to so parts list
                foreach (PartItem part in PartsList.Where(pt => pt.IsSelected))
                {
                    // add so part
                    SOPartItem soPart = new SOPartItem
                    {
                        Code = part.Code,
                        ServiceOrderID = ServiceOrder.ID
                    };
                    InsertSOPartItem(soPart);
                    ServiceOrder.Parts.Add(soPart);

                    // reset part selected
                    part.IsSelected = false;
                    UpdatePartItem(part);
                }

                // reset view
                ViewPartsSelection = false;
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task UpdateTechnicianAsync()
        {
            try
            {
                // return if no technicians
                if (GetTechnicianList().Count == 0)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Technicians", "There are no technicians available to select.", "OK");
                    return;
                }

                // get new technician, return if canceled/clicked out
                string techName = await AppServices.UserDialogs.ShowActionSheetAsync(
                    "Update Technician", "Cancel", GetTechnicianList().Select(t => t.TechName).ToArray());
                if (techName == "Cancel" || techName == null)
                    return;
                Technician tech = GetTechnician(techName);

                // update so
                ServiceOrder.SOInfo.ServicedByInitials = tech.TechInitials;
                ServiceOrder.SOInfo.ServicedByName = tech.TechName;
                UpdateServiceOrder(ServiceOrder);
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // METER CHANGEOUT
        private void OpenChangeMeter()
        {
            // set view
            ViewChangeMeter = true;

            // set entries
            MeterNumEntry = ServiceOrder.Meter.MeterNum;
            MXUNumEntry = ServiceOrder.Meter.MXUNum;
            MeterBrandEntry = ServiceOrder.Meter.MeterBrand;
            MeterSizeEntry = ServiceOrder.Meter.MeterSize;
            MeterNumDialsEntry = ServiceOrder.Meter.MeterNumDials;
            FinalReadingEntry = ServiceOrder.Meter.FinalReading;
            InitialReadingEntry = ServiceOrder.Meter.InitialReading;

            // set backflow
            if (ServiceOrder.Meter.Backflow == "On")
                BackflowSwitch = true;
            else
                BackflowSwitch = false;
        }
        private async Task SaveChangeMeterAsync()
        {
            try
            {
                // confirm final and initial readings are entered
                if (FinalReadingEntry == "" || FinalReadingEntry == null || InitialReadingEntry == "" || InitialReadingEntry == null)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("More Information Needed", 
                        "Please enter a Final Reading for the old meter and an Initial Reading for the new meter to continue.", "OK");
                    return;
                }

                // confirm user wants to save changeout
                if (!await AppServices.UserDialogs.ShowConfirmAsync("Warning!", 
                    "Are you sure you wish to changeout the current meter? This action cannot be undone.", "Yes, continue", "Cancel"))
                    return;

                // update so
                ServiceOrder.Meter.MeterNum = MeterNumEntry;
                ServiceOrder.Meter.MXUNum = MXUNumEntry;
                ServiceOrder.Meter.MeterBrand = MeterBrandEntry;
                ServiceOrder.Meter.MeterSize = MeterSizeEntry;
                ServiceOrder.Meter.MeterNumDials = MeterNumDialsEntry;
                ServiceOrder.Meter.FinalReading = FinalReadingEntry;
                ServiceOrder.Meter.InitialReading = InitialReadingEntry;
                ServiceOrder.Meter.ChangeoutDate = DateTime.Now.ToString("d");
                UpdateServiceOrder(ServiceOrder);

                // reset view + alert success
                ViewChangeMeter = false;
                await AppServices.UserDialogs.ShowAlertAsync("Success!", 
                    "Meter changeout successfully saved. Information can still be edited in the Meter Changeout pop-up.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private void CancelChangeMeter()
        {
            ViewChangeMeter = false;
        }

        // SIGNATURE
        private void OpenSignature()
        {
            ViewSignature = true;
        }
        private async Task SaveSignatureAsync()
        {
            try
            {
                // storage permissions
                await CheckRequestStoragePermissionsAsync();

                // get signature bytes, return if null
                Signature = await SignatureFromStream();
                if (Signature == null)
                {
                    await AppServices.UserDialogs.ShowAlertAsync("No Signature", "Please sign the service order before saving.", "OK");
                    return;
                }

                // get file path, check if file already exists
                string fileName = "Signature " + DateTime.Now.Month.ToString() + "_" + DateTime.Now.Day.ToString() +
                    "_" + DateTime.Now.Year.ToString() + ".jpeg";
                string folderPath = Properties.ServiceOrdersFolderPath + "/" + ServiceOrder.SONum;
                string sigPath = folderPath + "/" + fileName;

                // check if user wants to overwrite previous signature
                if (ServiceOrder.SignatureSaved)
                {
                    if (!await AppServices.UserDialogs.ShowConfirmAsync("Signature Already Exists", 
                        "A signature for this Service Order already exists. Would you like to overwrite it?", "Yes, continue", "Cancel"))
                        return;

                    // delete old signatures
                    foreach (string signature in Directory.GetFiles(folderPath))
                    {
                        if (Path.GetFileName(signature).StartsWith("Signature"))
                            File.Delete(signature);
                    }
                }

                // save bytes as image, update so
                File.WriteAllBytes(sigPath, Signature);
                ServiceOrder.SignatureSaved = true;
                UpdateServiceOrder(ServiceOrder);

                // reset view + alert success
                ViewSignature = false;
                await AppServices.UserDialogs.ShowAlertAsync("Success", "Signature saved successfully.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task CancelSignatureAsync()
        {
            // check if there is an unsaved signature
            Signature = await SignatureFromStream();
            if (Signature != null && !ServiceOrder.SignatureSaved)
            {
                if (!await AppServices.UserDialogs.ShowConfirmAsync("Warning!", 
                    "The entered signature has not been saved. Are you sure you wish to continue?", "Yes, continue", "Cancel"))
                    return;
            }

            // reset view
            ViewSignature = false;
        }

        // CLOSE SO
        private async Task CloseSOAsync()
        {
            try
            {
                // update status
                await UpdateStatusAsync();

                // get date, parts used
                string date = DateTime.Now.ToString("d");
                string partsUsed = "";
                if (ServiceOrder.Parts.Count != 0)
                {
                    foreach (SOPartItem soPart in ServiceOrder.Parts)
                    {
                        partsUsed += GetPartName(soPart.Code) + ", ";
                    }
                    partsUsed = partsUsed.Substring(0, partsUsed.Length - 2);
                }

                // display review pop-up, confirm user wants to continue
                string review = "Please confirm the following information is correct before saving:\n" +
                    "\nService Date: " + date +
                    "\nService Start Time: " + ServiceOrder.SOInfo.ServiceStartTime +
                    "\nService End Time: " + ServiceOrder.SOInfo.ServiceEndTime +
                    "\nStatus: " + ServiceOrder.SOInfo.SOStatus +
                    "\nAction Taken: " + ServiceOrder.SOInfo.ActionTakenDesc +
                    "\nParts Used: " + partsUsed +
                    "\nTechnician: " + ServiceOrder.SOInfo.ServicedByName;
                if (!await AppServices.UserDialogs.ShowConfirmAsync("Review Service Order", review, "Upload", "Cancel"))
                    return;

                // save service date, update so
                if (ServiceOrder.SOInfo.ServiceStartTime != null && ServiceOrder.SOInfo.ServiceEndTime == null)
                    ServiceOrder.SOInfo.ServiceEndTime = DateTime.Now.ToString("h:mm tt");
                ServiceOrder.IsClosed = true;
                ServiceOrder.SOInfo.ServiceDate = date;
                UpdateServiceOrder(ServiceOrder);





                // update open so status
                GetServiceOrderStatuses();

                // update so inc
                if (Properties.IsOpenOnly)
                {
                    // get so's list
                    GetServiceOrdersList();

                    // if on last but not only so, go back 1
                    if (Properties.SOInc == ServiceOrdersList.Count && Properties.SOInc != 0)
                        Properties.SOInc--;
                    // if all so's closed, turn off open only
                    else if (ServiceOrdersList.Count == 0)
                        Properties.IsOpenOnly = false;
                }
                else if (Properties.IsAutoAdvance && Properties.SOInc != (ServiceOrdersList.Count - 1))
                {
                    Properties.SOInc++;
                    Properties.SOIncID = ServiceOrdersList[Properties.SOInc].ID;
                }








                //Upload
                // change loading
                AppServices.UserDialogs.ShowLoading("Uploading...", MaskType.Gradient);




                // initialize
                List<string> XmlWritePaths = new List<string>();
                List<ServiceOrder> SOsInXmlList = new List<ServiceOrder>();
                XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
                {
                    Async = true,
                    Indent = true,
                    NewLineOnAttributes = true
                };

                // write all files
                // create file to write to
                int currentInfoID = ServiceOrder.SOInfo.ID;
                string fileName = "UpdatedSOs " + ServiceOrder.SOInfo.FileID + " " + DateTime.Now.ToString("MM_dd_yyyy hh_mm_ss") + ".xml";
                XmlWritePaths.Add(Path.Combine(Properties.ServiceOrdersBackupFilesFolderPath, fileName));
                using (FileStream f = File.Create(XmlWritePaths[0]))
                    f.Close();

                // write xml
                using (XmlWriter writer = XmlWriter.Create(XmlWritePaths[0], xmlWriterSettings))
                {
                    // write start doc
                    await writer.WriteStartDocumentAsync();
                    await writer.WriteStartElementAsync(null, "UpdatedOrders", null);

                    var so = ServiceOrder;

                    if (currentInfoID == so.ID)
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

                    // write end doc
                    await writer.WriteEndElementAsync();  // updated orders
                    await writer.FlushAsync();
                    writer.Close();
                }


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
           
                // upload file(s)
                int filesUploaded = client2.UploadFiles(XmlWritePaths, workingDirectory2, FtpRemoteExists.NoCheck, false, FtpVerify.None, FtpError.DeleteProcessed);

                // check if the upload is successful
                if (1 == filesUploaded)
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

                    // alert: success
                    await AppServices.UserDialogs.ShowAlertAsync("Service Order Closed", "The service order has been successfully uploaded to server and closed.", "OK");

                    AppServices.UserDialogs.HideLoading();
                }
                else
                {
                    AppServices.UserDialogs.HideLoading();
                    // alert failure
                    await AppServices.UserDialogs.ShowAlertAsync("Failure", "Something went wrong and the file was not uploaded. Please try again.", "OK");



                    // delete xmls
                    if (XmlWritePaths != null)
                    {
                        foreach (string file in XmlWritePaths)
                            File.Delete(file);
                    }
                    AppServices.UserDialogs.HideLoading();
                }



                // go back to detail page
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        // METER DETAILS
        private void OpenCloseMeterDetails()
        {
            // set vieW
            ViewMeterDetails = !ViewMeterDetails;

            // if opening, get current reading entry
            if (ViewMeterDetails)
                CurrentReadingEntry = ServiceOrder.Meter.CurrentReading;
        }        

        // NON BINDING VARIABLES
        // -- signature
        public Func<Task<byte[]>> SignatureFromStream { get; set; }
        public byte[] Signature { get; set; }

        // BINDING COLLECTIONS
        List<PartItem> partsList;
        public List<PartItem> PartsList
        {
            get => partsList;
            set
            {
                partsList = value;
                OnPropertyChanged();
            }
        }

        // BINDING VARIABLES
        // -- order
        public string IndexLabel { get; set; }

        // -- view bools
        bool viewPartsSelection;
        public bool ViewPartsSelection
        {
            get => viewPartsSelection;
            set
            {
                viewPartsSelection = value;
                OnPropertyChanged();
            }
        }
        bool viewChangeMeter;
        public bool ViewChangeMeter
        {
            get => viewChangeMeter;
            set
            {
                viewChangeMeter = value;
                OnPropertyChanged();
            }
        }
        bool viewSignature;
        public bool ViewSignature
        {
            get => viewSignature;
            set
            {
                viewSignature = value;
                OnPropertyChanged();
            }
        }
        bool viewMeterDetails;
        public bool ViewMeterDetails
        {
            get => viewMeterDetails;
            set
            {
                viewMeterDetails = value;
                OnPropertyChanged();
            }
        }

        // -- start/stop button label
        string startStopBtnLbl;
        public string StartStopBtnLbl
        {
            get => startStopBtnLbl;
            set
            {
                startStopBtnLbl = value;
                OnPropertyChanged();
            }
        }

        // -- changeout entries
        string meterNumEntry;
        public string MeterNumEntry
        {
            get => meterNumEntry;
            set
            {
                if (meterNumEntry == value)
                    return;
                meterNumEntry = value;
                OnPropertyChanged();
            }
        }
        string mxuNumEntry;
        public string MXUNumEntry
        {
            get => mxuNumEntry;
            set
            {
                if (mxuNumEntry == value)
                    return;
                mxuNumEntry = value;
                OnPropertyChanged();
            }
        }
        string meterBrandEntry;
        public string MeterBrandEntry
        {
            get => meterBrandEntry;
            set
            {
                if (meterBrandEntry == value)
                    return;
                meterBrandEntry = value;
                OnPropertyChanged();
            }
        }
        string meterSizeEntry;
        public string MeterSizeEntry
        {
            get => meterSizeEntry;
            set
            {
                if (meterSizeEntry == value)
                    return;
                meterSizeEntry = value;
                OnPropertyChanged();
            }
        }
        string meterNumDialsEntry;
        public string MeterNumDialsEntry
        {
            get => meterNumDialsEntry;
            set
            {
                if (meterNumDialsEntry == value)
                    return;
                meterNumDialsEntry = value;
                OnPropertyChanged();
            }
        }
        string finalReadingEntry;
        public string FinalReadingEntry
        {
            get => finalReadingEntry;
            set
            {
                if (finalReadingEntry == value)
                    return;
                finalReadingEntry = value;
                OnPropertyChanged();
            }
        }
        string initialReadingEntry;
        public string InitialReadingEntry
        {
            get => initialReadingEntry;
            set
            {
                if (initialReadingEntry == value)
                    return;
                initialReadingEntry = value;
                OnPropertyChanged();
            }
        }

        // -- current reading
        string currentReadingEntry;
        public string CurrentReadingEntry
        {
            get => currentReadingEntry;
            set
            {
                if (currentReadingEntry == value)
                    return;
                currentReadingEntry = value;
                ServiceOrder.Meter.CurrentReading = value;
                UpdateServiceOrder(ServiceOrder);
                OnPropertyChanged();
            }
        }

        // -- backflow
        string backflowLabel;
        public string BackflowLabel
        {
            get => backflowLabel;
            set
            {
                backflowLabel = value;
                ServiceOrder.Meter.Backflow = value;
                UpdateServiceOrder(ServiceOrder);
                OnPropertyChanged();
            }
        }
        bool backflowSwitch;
        public bool BackflowSwitch
        {
            get => backflowSwitch;
            set
            {
                // set switch
                backflowSwitch = value;

                // update label -> calls UpdateServiceOrder
                if (value)
                    BackflowLabel = "On";
                else
                    BackflowLabel = "Off";

                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command StartEndService_Command { get; }
        public Command UpdateStatus_Command { get; }
        public Command UpdateActionTaken_Command { get; }
        public Command OpenPartsSelection_Command { get; }
        public Command ClosePartsSelection_Command { get; }
        public Command UpdateTechnician_Command { get; }
        public Command TakeSavePhoto_Command { get; }
        public Command OpenChangeMeter_Command { get; }
        public Command SaveChangeMeter_Command { get; }
        public Command CancelChangeMeter_Command { get; }
        public Command OpenSignature_Command { get; }
        public Command SaveSignature_Command { get; }
        public Command CancelSignature_Command { get; }
        public Command CloseSO_Command { get; }
        public Command OpenCloseMeterDetails_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
        public Command GoTo_SettingsPage_Command { get; }
        public Command GoTo_NotesPage_Command { get; }
    }
}
