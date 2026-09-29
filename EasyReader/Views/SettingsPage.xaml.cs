using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            BindingContext = new SettingsViewModel(Navigation);
            base.OnAppearing();
        }
    }
}