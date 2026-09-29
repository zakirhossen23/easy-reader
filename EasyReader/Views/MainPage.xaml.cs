using System;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        // Track whether the ButtonLinks grid is transposed (rows/columns swapped)
        bool _isTransposed = false;
        List<GridLength> _originalRowHeights;
        List<GridLength> _originalColumnWidths;
        Dictionary<object, (int row, int col, int rowSpan, int colSpan)> _originalPositions = new();
        public MainPage()
        {
            InitializeComponent();
            NavigationPage.SetHasBackButton(this, false);
            // Cache the original row/column definitions for the ButtonLinks grid
            // Ensure ButtonLinks is initialized from XAML first (InitializeComponent called)
            if (ButtonLinks != null)
            {
                _originalRowHeights = ButtonLinks.RowDefinitions.Select(r => r.Height).ToList();
                _originalColumnWidths = ButtonLinks.ColumnDefinitions.Select(c => c.Width).ToList();
                // Cache original child grid positions
                foreach (var child in ButtonLinks.Children)
                {
                    if (child is BindableObject bo)
                    {
                        int row = (int)bo.GetValue(Grid.RowProperty);
                        int col = (int)bo.GetValue(Grid.ColumnProperty);
                        int rowSpan = (int)bo.GetValue(Grid.RowSpanProperty);
                        int colSpan = (int)bo.GetValue(Grid.ColumnSpanProperty);
                        _originalPositions[child] = (row, col, rowSpan, colSpan);
                    }
                }
            }
        }
        protected override void OnAppearing()
        {
            BindingContext = new MainViewModel(Navigation);
            base.OnAppearing();
        }
        // SET GRID BASED ON SCREEN ORIENTATION
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            // When layout orientation changes, transpose the ButtonLinks grid so
            // row definitions become column definitions and child positions swap.
            if (ButtonLinks == null)
                return;

            bool shouldTranspose = width > height; // landscape -> transpose

            if (shouldTranspose != _isTransposed)
            {
                TransposeButtonLinksGrid(shouldTranspose);
            }
        }

        void TransposeButtonLinksGrid(bool toTranspose)
        {
            if (ButtonLinks == null || _originalRowHeights == null || _originalColumnWidths == null)
                return;

            // If already in desired state, nothing to do
            if (toTranspose == _isTransposed)
                return;

            // Rebuild RowDefinitions and ColumnDefinitions based on desired layout.
            // In landscape (toTranspose == true) we layout tiles in a single row and multiple columns
            // In portrait (toTranspose == false) we restore the original 3x3 grid.
            ButtonLinks.RowDefinitions.Clear();
            ButtonLinks.ColumnDefinitions.Clear();

            if (toTranspose)
            {
                // Single row
                ButtonLinks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

                // Columns equal to number of children (keeps tiles uniform)
                int total = ButtonLinks.Children.Count;
                for (int i = 0; i < total; i++)
                    ButtonLinks.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

                // Place children sequentially across the single row
                int idx = 0;
                foreach (var child in ButtonLinks.Children)
                {
                    if (child is BindableObject bo)
                    {
                        bo.SetValue(Grid.RowProperty, 0);
                        bo.SetValue(Grid.ColumnProperty, idx);
                        // reset spans to 1 for uniform layout
                        bo.SetValue(Grid.RowSpanProperty, 1);
                        bo.SetValue(Grid.ColumnSpanProperty, 1);
                        idx++;
                    }
                }
            }
            else
            {
                // Restore original rows/columns
                foreach (var h in _originalRowHeights)
                    ButtonLinks.RowDefinitions.Add(new RowDefinition { Height = h });

                foreach (var w in _originalColumnWidths)
                    ButtonLinks.ColumnDefinitions.Add(new ColumnDefinition { Width = w });

                // Restore original child positions
                foreach (var child in ButtonLinks.Children)
                {
                    if (child is BindableObject bo && _originalPositions.TryGetValue(child, out var pos))
                    {
                        bo.SetValue(Grid.RowProperty, pos.row);
                        bo.SetValue(Grid.ColumnProperty, pos.col);
                        bo.SetValue(Grid.RowSpanProperty, pos.rowSpan);
                        bo.SetValue(Grid.ColumnSpanProperty, pos.colSpan);
                    }
                }
            }

            _isTransposed = toTranspose;
        }
    }


    }