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
        readonly Func<Task>? _hide;
        int _disposed;

        public ProgressDialog_MAUI(Func<Task>? hide = null)
        {
            _hide = hide;
        }

        public int PercentComplete { get; set; }

        public void Dispose()
        {
            // Progress(show: true) pushes the overlay, so disposing must pop
            // it. The old no-op Dispose leaked the ref-count and left the
            // overlay (e.g. "Backup up Accounts...") on screen forever.
            if (_hide == null || Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _ = _hide();
        }
    }

    public class UserDialogs_MAUI : IUserDialogs
    {
        static ContentPage? _loadingPage;
        // Reference count so overlapping Show/Hide pairs share one overlay.
        static int _loadingCount;
        // Strict FIFO chain for every push/pop: Show posts its UI work to the
        // main thread and Hide used to race it, so a Hide processed before its
        // Show left the modal stranded (worst on iOS, where the main-thread
        // queue drains slower). Enqueueing preserves call order.
        static readonly object _chainLock = new();
        static Task _chain = Task.CompletedTask;

        static Task Enqueue(Func<Task> work)
        {
            lock (_chainLock)
            {
                _chain = _chain.ContinueWith(
                    _ => RunSafely(work),
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap();
                return _chain;
            }
        }

        static async Task RunSafely(Func<Task> work)
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch
            {
                // Overlay work is best-effort; never fault the chain.
            }
        }

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

            _ = Enqueue(async () =>
            {
                _loadingCount++;
                if (_loadingPage != null) return;

                // Build and push on the main thread; the whole link runs in
                // call order, so a later Hide can never slip in before this.
                var page = await MainThread.InvokeOnMainThreadAsync(() =>
                    BuildLoadingPage(title, maskType)).ConfigureAwait(false);
                _loadingPage = page;

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        App.Current.MainPage.Navigation.PushModalAsync(page, false))
                        .ConfigureAwait(false);
                }
                catch
                {
                    // swallow navigation errors
                    _loadingPage = null;
                }
            });
        }

        static ContentPage BuildLoadingPage(string? title, MaskType maskType)
        {
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

            return new ContentPage
            {
                BackgroundColor = Colors.Transparent,
                Content = grid
            };
        }

        public void HideLoading()
        {
            _ = HideLoadingAsync();
        }

        public Task HideLoadingAsync()
        {
            if (App.Current?.MainPage == null) return Task.CompletedTask;

            // Awaiting this guarantees the modal is gone before the caller
            // presents an alert / action sheet on iOS.
            return Enqueue(async () =>
            {
                if (_loadingCount > 0) _loadingCount--;
                if (_loadingCount != 0 || _loadingPage == null) return;

                _loadingPage = null;
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        App.Current.MainPage.Navigation.PopModalAsync(false))
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            });
        }

        public IProgressDialog Progress(string title, string? cancelText, string? title2, bool show, MaskType maskType)
        {
            if (show)
                ShowLoading(title, maskType);
            return new ProgressDialog_MAUI(show ? (Func<Task>)HideLoadingAsync : null);
        }
    }
}
