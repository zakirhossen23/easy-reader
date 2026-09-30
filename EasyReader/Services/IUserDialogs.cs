using System;
using System.Threading.Tasks;

namespace EasyReader.Services
{
    public enum MaskType { None, Gradient }

    public interface IProgressDialog : IDisposable
    {
        int PercentComplete { get; set; }
    }

    public interface IUserDialogs
    {
        Task ShowAlertAsync(string title, string message, string ok);
        Task<string?> ShowPromptAsync(string title, string message, string accept, string cancel, string placeholder, int maxLength, string initialValue);
        Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
        Task<string?> ShowActionSheetAsync(string title, string cancel, string[] options);

        void ShowLoading(string title, MaskType maskType = MaskType.None);
        void HideLoading();
        // Awaitable pop of the loading modal. Use before showing an alert /
        // action sheet on iOS: the fire-and-forget HideLoading() may not have
        // popped yet, and iOS drops a second modal presented on top of it.
        Task HideLoadingAsync();
        IProgressDialog Progress(string title, string? cancelText, string? title2, bool show, MaskType maskType);
    }
}
