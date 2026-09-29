namespace EasyReader
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for Shell navigation so pages can be opened via Shell.Current.GoToAsync
            Routing.RegisterRoute(nameof(Views.EnterReadingsPage), typeof(Views.EnterReadingsPage));
            Routing.RegisterRoute(nameof(Views.MissingReadingsPage), typeof(Views.MissingReadingsPage));
            Routing.RegisterRoute(nameof(Views.AccountDetailsPage), typeof(Views.AccountDetailsPage));
            Routing.RegisterRoute(nameof(Views.SearchPage), typeof(Views.SearchPage));
            Routing.RegisterRoute(nameof(Views.FileManagerPage), typeof(Views.FileManagerPage));
            Routing.RegisterRoute(nameof(Views.SODetailsPage), typeof(Views.SODetailsPage));
            Routing.RegisterRoute(nameof(Views.AboutPage), typeof(Views.AboutPage));
            Routing.RegisterRoute(nameof(Views.SettingsPage), typeof(Views.SettingsPage));
            Routing.RegisterRoute(nameof(Views.NotesPage), typeof(Views.NotesPage));
            Routing.RegisterRoute(nameof(Views.EmailPhotosPage), typeof(Views.EmailPhotosPage));
        }
    }
}
