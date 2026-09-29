using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.Models;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class GalleryPage : ContentPage
    {
        public GalleryPage(AccountFile accountFile)
        {
            InitializeComponent();
            BindingContext = new GalleryViewModel(Navigation, accountFile);
        }
        public GalleryPage(SOFile soFile)
        {
            InitializeComponent();
            BindingContext = new GalleryViewModel(Navigation, soFile);
        }
    }
}