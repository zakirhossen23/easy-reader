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
using EasyReader.Models;
using EasyReader.Views;
using EasyReader.Services;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class EmailPhotosViewModel : BaseViewModel
    {
        public EmailPhotosViewModel()
        {
            EmailSelected_Command = new Command(async () => await EmailSelectedAsync());
            SelectAll_Command = new Command(() => SelectAllFiles = !SelectAllFiles);
            Cancel_Command = new Command(async () => await CancelAsync());
            Open_Command = new Command(async () => await OpenSelectedAsync());
            Rename_Command = new Command(async () => await RenameSelectedAsync());
            Delete_Command = new Command(async () => await DeleteSelectedAsync());
            ToggleSortOrder_Command = new Command(() => SortOrder = (SortOrder == "Asc") ? "Desc" : "Asc");

            AllSOFiles = new ObservableCollection<PhotoItem>();
            DisplayedSOFiles = new ObservableCollection<PhotoItem>();
            SelectAllFiles = false;
            LoadAllSOFiles();
        }

        // unified photo item for SO or Account files
        public class PhotoItem
        {
            public string Path { get; set; }
            public string Name { get; set; }
            // for SO files this is SONum, for account files this is AccountNumber
            public string Tag { get; set; }
            public DateTime CreatedDate { get; set; }
            public bool IsSelected { get; set; }
            // original type marker: "SO" or "Account"
            public string SourceType { get; set; }
        }

        private void LoadAllSOFiles()
        {
            try
            {
                AllSOFiles.Clear();
                //if (!Properties.IsSODownloaded)
                //    return;

                // iterate all service order directories
                var soBase = Properties.ServiceOrdersFolderPath;
                if (Directory.Exists(soBase))
                {
                    foreach (var dir in Directory.GetDirectories(soBase))
                    {
                        var sonum = Path.GetFileName(dir);
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            var fi = new FileInfo(file);
                            AllSOFiles.Add(new PhotoItem
                            {
                                Path = file,
                                Name = Path.GetFileName(file),
                                Tag = sonum,
                                CreatedDate = fi.CreationTime,
                                IsSelected = false,
                                SourceType = "SO"
                            });
                        }
                    }
                }

                // iterate account directories and include account photos as well
                var accBase = Properties.AccountsFolderPath;
                if (Directory.Exists(accBase))
                {
                    foreach (var dir in Directory.GetDirectories(accBase))
                    {
                        var accnum = Path.GetFileName(dir);
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            var fi = new FileInfo(file);
                            AllSOFiles.Add(new PhotoItem
                            {
                                Path = file,
                                Name = Path.GetFileName(file),
                                Tag = accnum,
                                CreatedDate = fi.CreationTime,
                                IsSelected = false,
                                SourceType = "Account"
                            });
                        }
                    }
                }
                // apply current filter/sort
                ApplyFilterSort();
            }
            catch (Exception ex)
            {
                AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK").ConfigureAwait(false);
            }
        }

        private async Task EmailSelectedAsync()
        {
            try
            {
                var selected = AllSOFiles.Where(f => f.IsSelected).ToList();
                if (!selected.Any())
                    return;

                var email = new EmailMessage
                {
                    Subject = "Photos"
                };

                var missing = selected.Where(f => !File.Exists(f.Path)).ToList();
                if (missing.Any())
                {
                    await AppServices.UserDialogs.ShowAlertAsync("File Missing", $"{missing.Count} selected file(s) no longer exist on disk. Please refresh and try again.", "OK");
                    return;
                }

                foreach (var f in selected)
                    email.Attachments.Add(new EmailAttachment(f.Path));

                // Call Email API directly so we can surface errors to the user
                try
                {
                    await Email.ComposeAsync(email);
                }
                catch (FeatureNotSupportedException)
                {
                    // Email not supported on this device - fallback to sharing a single zip of selected files
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
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        private Task CancelAsync()
        {
            // just pop page if navigation available
            if (Application.Current?.MainPage?.Navigation != null)
                return Application.Current.MainPage.Navigation.PopAsync();
            return Task.CompletedTask;
        }

        public ObservableCollection<PhotoItem> AllSOFiles { get; set; }
        // collection exposed to UI after filtering/sorting
        public ObservableCollection<PhotoItem> DisplayedSOFiles { get; set; }

        public Command Open_Command { get; }
        public Command Rename_Command { get; }
        public Command Delete_Command { get; }

        string searchText = "";
        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText == value)
                    return;
                searchText = value;
                ApplyFilterSort();
                OnPropertyChanged();
            }
            }

        private int GetTotalFilesSelected()
        {
            return AllSOFiles.Where(f => f.IsSelected).Count();
        }

        private async Task OpenSelectedAsync()
        {
            try
            {
                int filesSelected = GetTotalFilesSelected();
                if (filesSelected == 1)
                {
                    var file = AllSOFiles.First(f => f.IsSelected);
                    if (file.SourceType == "SO")
                    {
                        var soFile = new SOFile
                        {
                            Path = file.Path,
                            Name = file.Name,
                            SONum = file.Tag,
                            CreatedDate = file.CreatedDate
                        };
                        await Application.Current.MainPage.Navigation.PushAsync(new GalleryPage(soFile));
                    }
                    else
                    {
                        var acFile = new AccountFile
                        {
                            Path = file.Path,
                            Name = file.Name
                        };
                        await Application.Current.MainPage.Navigation.PushAsync(new GalleryPage(acFile));
                    }
                }
                else if (filesSelected > 1)
                    await AppServices.UserDialogs.ShowAlertAsync("Too Many Files Selected", "Please select only 1 file to open.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        private async Task RenameSelectedAsync()
        {
            try
            {
                int filesSelected = GetTotalFilesSelected();
                if (filesSelected == 1)
                {
                    string newFileName = await AppServices.UserDialogs.ShowPromptAsync("Rename File", "What would you like to rename this file?", "OK", "Cancel", "", -1, "");
                    if (string.IsNullOrEmpty(newFileName))
                        return;

                    var file = AllSOFiles.First(f => f.IsSelected);
                    string fileExt = Path.GetExtension(file.Path);
                    string dir = Path.GetDirectoryName(file.Path) ?? Properties.ServiceOrdersFolderPath;
                    string newFilePath = Path.Combine(dir, newFileName + fileExt);
                    int version = 1;
                    while (File.Exists(newFilePath))
                    {
                        newFilePath = Path.Combine(dir, newFileName + "_" + version.ToString() + fileExt);
                        version++;
                    }

                    var fileBytes = File.ReadAllBytes(file.Path);
                    File.WriteAllBytes(newFilePath, fileBytes);
                    File.Delete(file.Path);

                    LoadAllSOFiles();
                    ApplyFilterSort();
                }
                else if (filesSelected > 1)
                    await AppServices.UserDialogs.ShowAlertAsync("Too Many Files Selected", "Please select only 1 file to rename.", "OK");
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        private async Task DeleteSelectedAsync()
        {
            try
            {
                int filesSelected = GetTotalFilesSelected();
                if (filesSelected > 0)
                {
                    bool answer = false;
                    if (filesSelected == 1)
                        answer = await AppServices.UserDialogs.ShowConfirmAsync("Warning!", "Are you sure you would like to delete 1 file?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    else
                        answer = await AppServices.UserDialogs.ShowConfirmAsync("Warning!", $"Are you sure you would like to delete {filesSelected} files?\nThis action cannot be undone.", "Yes, continue", "Cancel");
                    if (!answer) return;

                    foreach (var f in AllSOFiles.Where(f => f.IsSelected).ToList())
                        File.Delete(f.Path);

                    LoadAllSOFiles();
                    ApplyFilterSort();
                }
            }
            catch (Exception ex)
            {
                await AppServices.UserDialogs.ShowAlertAsync("Error", ex.Message, "OK");
            }
        }

        string sortBy = "Name"; // Name | SONum | Date
        public string SortBy
        {
            get => sortBy;
            set
            {
                if (sortBy == value)
                    return;
                sortBy = value;
                ApplyFilterSort();
                OnPropertyChanged();
            }
        }

        string sortOrder = "Desc"; // Asc | Desc
        public string SortOrder
        {
            get => sortOrder;
            set
            {
                if (sortOrder == value)
                    return;
                sortOrder = value;
                ApplyFilterSort();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SortOrderIcon));
            }
        }

        bool selectAllFiles;
        public bool SelectAllFiles
        {
            get => selectAllFiles;
            set
            {
                if (selectAllFiles == value)
                    return;
                selectAllFiles = value;
                foreach (var f in AllSOFiles)
                    f.IsSelected = value;
                OnPropertyChanged();
            }
        }

        public Command EmailSelected_Command { get; }
        public Command SelectAll_Command { get; }
        public Command Cancel_Command { get; }
        public Command<string> SetSortCommand => new Command<string>(s => SortBy = s);
        public Command<string> SetSearchCommand => new Command<string>(t => SearchText = t);
        public Command ToggleSortOrder_Command { get; }

        public string SortOrderIcon => SortOrder == "Asc" ? "▲" : "▼";

        private void ApplyFilterSort()
        {
            try
            {
                var list = AllSOFiles.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var q = SearchText.ToLower();
                    list = list.Where(f => (f.Name != null && f.Name.ToLower().Contains(q)) || (f.Tag != null && f.Tag.ToLower().Contains(q)));
                }

                if (SortBy == "Name")
                {
                    list = (SortOrder == "Asc") ? list.OrderBy(f => f.Name).ThenBy(f => f.Tag)
                                                   : list.OrderByDescending(f => f.Name).ThenByDescending(f => f.Tag);
                }
                else if (SortBy == "SONum")
                {
                    list = (SortOrder == "Asc") ? list.OrderBy(f => f.Tag).ThenBy(f => f.Name)
                                                   : list.OrderByDescending(f => f.Tag).ThenByDescending(f => f.Name);
                }
                else if (SortBy == "Date")
                {
                    list = (SortOrder == "Asc") ? list.OrderBy(f => f.CreatedDate)
                                                   : list.OrderByDescending(f => f.CreatedDate);
                }

                DisplayedSOFiles.Clear();
                foreach (var it in list)
                    DisplayedSOFiles.Add(it);
            }
            catch { }
        }
    }
}
