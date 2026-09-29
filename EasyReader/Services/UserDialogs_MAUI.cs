using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace EasyReader.Services
{
    public class ProgressDialog_MAUI : IProgressDialog
    {
        public int PercentComplete { get; set; }
        public void Dispose() { }
    }

    public class UserDialogs_MAUI : IUserDialogs
    {
        static ContentPage? _loadingPage;
        // Reference count + gate so overlapping Show/Hide calls from
        // background continuations can't push/pop the modal out of order
        // (this was the loading-overlay flashing, worst on iOS).
        static int _loadingCount;
        static readonly SemaphoreSlim _loadingLock = new(1, 1);

        public Task ShowAlertAsync(string title, string message, string ok)
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            return page?.DisplayAlert(title, message, ok) ?? Task.CompletedTask;
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page == null) return false;
            return await page.DisplayAlert(title, message, accept, cancel);
        }

        public Task<string?> ShowActionSheetAsync(string title, string cancel, string[] options)
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page == null) return Task.FromResult<string?>(null);
            return page.DisplayActionSheet(title, cancel, null, options);
        }

        public Task<string?> ShowPromptAsync(string title, string message, string accept, string cancel, string placeholder, int maxLength, string initialValue)
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page == null) return Task.FromResult<string?>(null);
            return page.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, null, initialValue);
        }

        public void ShowLoading(string title, MaskType maskType = MaskType.None)
        {
            if (App.Current?.MainPage == null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _loadingLock.WaitAsync();
                try
                {
                    _loadingCount++;
                    if (_loadingPage != null) return;
                }
                finally
                {
                    _loadingLock.Release();
                }

                // overlay background depending on maskType
                Color overlayColor = maskType == MaskType.Gradient
                    ? new Color(0, 0, 0, 0.6f)
                    : new Color(0, 0, 0, 0.35f);

                var activity = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Color = Colors.White
                };

                var label = new Label
                {
                    Text = title ?? string.Empty,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold
                };

                var contentStack = new VerticalStackLayout
                {
                    Spacing = 12,
                    Padding = new Thickness(20),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { activity, label }
                };

                // modern card with slight opacity
                var card = new Frame
                {
                    Content = contentStack,
                    CornerRadius = 12,
                    HasShadow = true,
                    BackgroundColor = new Color(0, 0, 0, 0.75f),
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var grid = new Grid
                {
                    BackgroundColor = overlayColor,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                // center the card
                grid.Children.Add(card, 0, 0);
                Grid.SetRow(card, 0);
                Grid.SetColumn(card, 0);

                _loadingPage = new ContentPage
                {
                    BackgroundColor = Colors.Transparent,
                    Content = grid
                };

                try
                {
                    await App.Current.MainPage.Navigation.PushModalAsync(_loadingPage, false);
                }
                catch
                {
                    // swallow navigation errors
                    _loadingPage = null;
                }
            });
        }

        public void HideLoading()
        {
            if (App.Current?.MainPage == null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool shouldPop = false;
                await _loadingLock.WaitAsync();
                try
                {
                    if (_loadingCount > 0) _loadingCount--;
                    shouldPop = _loadingCount == 0 && _loadingPage != null;
                }
                finally
                {
                    _loadingLock.Release();
                }
                if (!shouldPop) return;
                try
                {
                    await App.Current.MainPage.Navigation.PopModalAsync(false);
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    _loadingPage = null;
                }
            });
        }

        public IProgressDialog Progress(string title, string? cancelText, string? title2, bool show, MaskType maskType)
        {
            var dlg = new ProgressDialog_MAUI();
            if (show)
                ShowLoading(title, maskType);
            return dlg;
        }
    }
}
