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
using EasyReader.Views;
using EasyReader.Models;
using EasyReader.Services;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class SOFilesViewModel : BaseViewModel
    {
        public SOFilesViewModel()
        {
            // PAGE COMMANDS
            OpenSOFile_Command = new Command(async () => await OpenSOFileAsync());
            RenameSOFile_Command = new Command(async () => await RenameSOFileAsync());
            DeleteSOFiles_Command = new Command(async () => await DeleteSOFilesAsync());
            SendSOFiles_Command = new Command(async () => await SendSOFilesAsync());
        }
        public SOFilesViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));

            // GET PROPERTIES + SO
            GetProperties();
            ServiceOrder = GetServiceOrder_FromID(Properties.SOIncID);

            // INITIALIZE OC + GET SO FILES
            SOFilesOC = new ObservableCollection<SOFile>();
            SelectAllFiles = false;
            GetSOFilesOC(false);
        }

        // SO FILE ACTIONS
        private void GetSOFilesOC(bool isSelected)
        {
            try
            {
                // clear OC
                SOFilesOC.Clear();

                if (Properties.IsSODownloaded)
                {
                    // create SO files
                    foreach (string file in Directory.GetFiles(GetSODirectory(ServiceOrder)))
                    {
                        SOFile soFile = new SOFile
                        {
                            Path = file,
                            Name = Path.GetFileName(file),
                            IsSelected = isSelected
                        };
                        SOFilesOC.Add(soFile);
                    }

                    // reorder OC
                    var ocList = SOFilesOC.OrderBy(f => f.Name).ToList();
                    for (int i = 0; i < ocList.Count; i++)
                        SOFilesOC.Move(SOFilesOC.IndexOf(ocList[i]), i);
                }
            }
            catch (Exception ex)
            {
                AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK").ConfigureAwait(false);
            }
        }
        private int GetTotalFilesSelected()
        {
            return SOFilesOC.Where(f => f.IsSelected).Count();
        }
        private async Task OpenSOFileAsync()
        {
            try
            {
                // get total files selected
                int filesSelected = GetTotalFilesSelected();

                // find selected file, go to gallery page
                if (filesSelected == 1)
                {
                    SOFile file = SOFilesOC.First(f => f.IsSelected);
                    await Navigation.PushAsync(new GalleryPage(file));
                }
                // alert: too many files selected
                else if (filesSelected > 1)
                    await AppServices.UserDialogs.ShowAlertAsync("Too Many Files Selected", "PLease select only 1 file to open.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task RenameSOFileAsync()
        {
            try
            {
                // get total files selected
                int filesSelected = GetTotalFilesSelected();

                // find selected file, rename
                if (filesSelected == 1)
                {
                    // get new name of file from user, return if canceled/clicked out
                    string newFileName = await AppServices.UserDialogs.ShowPromptAsync("Rename File", "What would you like to rename this file?", "OK", "Cancel", "", -1, "");
                    if (newFileName == "" || newFileName == null)
                        return;

                    // get file + file extension
                    SOFile file = SOFilesOC.First(f => f.IsSelected);
                    string fileExt = Path.GetExtension(file.Path);

                    // check if file exists, add version number
                    string newFilePath = GetSODirectory(ServiceOrder) + "/" + newFileName + fileExt;
                    int version = 1;
                    while (File.Exists(newFilePath))
                    {
                        newFilePath = GetSODirectory(ServiceOrder) + "/" + newFileName + "_" + version.ToString() + fileExt;
                        version++;
                    }

                    // create new file, copy bytes, delete old file
                    var fileBytes = File.ReadAllBytes(file.Path);
                    File.WriteAllBytes(newFilePath, fileBytes);
                    File.Delete(file.Path);

                    // refresh OC
                    GetSOFilesOC(false);
                }
                // alert: too many files selected
                else if (filesSelected > 1)
                    await AppServices.UserDialogs.ShowAlertAsync("Too Many Files Selected", "PLease select only 1 file to rename.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task DeleteSOFilesAsync()
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
                        answer = await AppServices.UserDialogs.ShowConfirmAsync("Warning!",
                            "Are you sure you would like to delete 1 file?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    else
                        answer = await AppServices.UserDialogs.ShowConfirmAsync("Warning!",
                            "Are you sure you would like to delete " + filesSelected.ToString() + " files?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    if (!answer)
                        return;

                    // delete selected files
                    foreach (SOFile file in SOFilesOC.Where(f => f.IsSelected))
                        File.Delete(file.Path);

                    // refresh OC
                    GetSOFilesOC(false);
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }
        private async Task SendSOFilesAsync()
        {
            try
            {
                if (GetTotalFilesSelected() > 0)
                {
                    var selected = SOFilesOC.Where(f => f.IsSelected).ToList();

                    var email = new EmailMessage
                    {
                        Subject = "SO Number: " + ServiceOrder.SONum
                    };

                    var missing = selected.Where(f => !File.Exists(f.Path)).ToList();
                    if (missing.Any())
                    {
                        await AppServices.UserDialogs.ShowAlertAsync("File Missing", $"{missing.Count} selected file(s) no longer exist on disk. Please refresh and try again.", "OK");
                        return;
                    }

                    foreach (SOFile file in selected)
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

        // BINDING COLLECTIONS
        public ObservableCollection<SOFile> SOFilesOC { get; set; }

        // BINDING VARIABLES
        bool selectAllFiles;
        public bool SelectAllFiles
        {
            get => selectAllFiles;
            set
            {
                if (selectAllFiles == value)
                    return;
                selectAllFiles = value;
                GetSOFilesOC(value);
                OnPropertyChanged();
            }
        }

        // COMMANDS
        public Command OpenSOFile_Command { get; }
        public Command RenameSOFile_Command { get; }
        public Command DeleteSOFiles_Command { get; }
        public Command SendSOFiles_Command { get; }
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; set; }
    }
}
