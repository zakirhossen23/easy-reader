using Microsoft.Maui.Controls;

#pragma warning disable CS8618

namespace EasyReader.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel() { }
        public AboutViewModel(INavigation navigation)
            : this()
        {
            // NAVIGATION
            Navigation = navigation;
            GoTo_MainPage_Command = new Command(async () => await GoTo_MainPageAsync_(Navigation));

            // GET PROPERTIES
            // GetProperties();

            // SET ABOUT INFO STRING
            AboutInfo_String = "EasyReader v2.1.18 is software written as an add on module to the El Dorado Utility Billing software. " +
                "It is licensed software (not sold); all rights to the software are owned by Creative Technologies Inc. " +
                "Use of the product is by a one-time software license fee with optional technical support contract. " +
                "By using this product you accept the terms and conditions of our software license and support agreement below.";
        }

        // BINDING VARIABLES
        public string AboutInfo_String { get; set; }

        // COMMANDS
        public INavigation Navigation { get; set; }
        public Command GoTo_MainPage_Command { get; }
    }
}
