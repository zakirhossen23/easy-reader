using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AboutPage : ContentPage
    {
        public AboutPage()
        {
            InitializeComponent();
            BindingContext = new AboutViewModel(Navigation);
             pdfJsViewer.LoadPdfFromMauiAssets("EasyReaderUseInstructionsVer2.pdf");
        }
    }

}