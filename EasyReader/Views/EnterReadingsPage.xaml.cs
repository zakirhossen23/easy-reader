using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.ApplicationModel;
using EasyReader.ViewModels;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class EnterReadingsPage : ContentPage
    {
        public EnterReadingsPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            BindingContext = new EnterReadingsViewModel(Navigation);
            base.OnAppearing();
        }

        // SET NUM PAD GRID BASED ON SCREEN ORIENTATION
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            // Existing outer grid layout adjustments
            if (height > width)
            {
                // Portrait: arrange outerGrid as a 3x3 so popup can be centered in the middle cell (matches provided Normal XAML)
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.RowDefinitions.Clear();

                // three columns so popup sits in the middle column
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // rows similar to original: top area, actions area, bottom buttons
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.7, GridUnitType.Star) });

                // Place innerGrid across top full width
                Grid.SetColumn(innerGrid, 0);
                Grid.SetRow(innerGrid, 0);
                Grid.SetColumnSpan(innerGrid, 3);
                Grid.SetRowSpan(innerGrid, 1);

                // Place action and function buttons centered in middle column (or span all three)
                Grid.SetColumn(gridActions, 0);
                Grid.SetRow(gridActions, 1);
                Grid.SetColumnSpan(gridActions, 3);

                Grid.SetColumn(FunctionsBtns, 0);
                Grid.SetRow(FunctionsBtns, 2);
                Grid.SetColumnSpan(FunctionsBtns, 3);

                // Ensure overlay covers all three columns/rows
                if (overlayBox != null)
                {
                    Grid.SetColumn(overlayBox, 0);
                    Grid.SetRow(overlayBox, 0);
                    Grid.SetColumnSpan(overlayBox, 3);
                    Grid.SetRowSpan(overlayBox, 3);
                }

              
            }
            else
            {
                // Landscape: adjust outerGrid to two columns
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.RowDefinitions.Clear();
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(innerGrid, 0);
                Grid.SetColumnSpan(innerGrid, 1);
                Grid.SetRow(innerGrid, 0);
                Grid.SetRowSpan(innerGrid, 2);
                // place actions and function buttons in column 1 (second column)
                Grid.SetColumn(gridActions, 1);
                Grid.SetColumn(FunctionsBtns, 1);
                Grid.SetRow(gridActions, 0);
                Grid.SetRow(FunctionsBtns, 1);
            }

            // Dynamic adjustments for the num-pad popup so it responds to orientation/size
            try
            {
                if (numPadPopup != null)
                {
                    if (height > width)
                    {
                        numPadPopup.ColumnDefinitions.Clear();
                        numPadPopup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        numPadPopup.RowDefinitions.Clear();
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7, GridUnitType.Star) });

                        Grid.SetColumn(numPadPopup, 0);
                        Grid.SetRow(numPadPopup, 0);
                        Grid.SetColumnSpan(numPadPopup, 3);
                        Grid.SetRowSpan(numPadPopup, 3);

                        if (usageGrid != null)
                        {
                            Grid.SetColumn(usageGrid, 0);
                            Grid.SetRow(usageGrid, 0);
                            Grid.SetRowSpan(usageGrid, 1);
                            Grid.SetColumnSpan(usageGrid, 1);
                        }

                        if (frameNewReadingPopup != null)
                        {
                            Grid.SetColumn(frameNewReadingPopup, 0);
                            Grid.SetRow(frameNewReadingPopup, 1);
                            Grid.SetRowSpan(frameNewReadingPopup, 1);
                        }
                        if (lblNewReadingPopup != null)
                        {
                            Grid.SetColumn(lblNewReadingPopup, 0);
                            Grid.SetRow(lblNewReadingPopup, 1);
                            Grid.SetRowSpan(lblNewReadingPopup, 1);
                        }

                        if (numPadButtonsGrid != null)
                        {
                            Grid.SetColumn(numPadButtonsGrid, 0);
                            Grid.SetRow(numPadButtonsGrid, 2);
                            Grid.SetRowSpan(numPadButtonsGrid, 1);
                        }

                        if (btnCloseNumPad != null)
                        {
                            Grid.SetColumn(btnCloseNumPad, 3);
                            Grid.SetRow(btnCloseNumPad, 0);
                            btnCloseNumPad.VerticalOptions = LayoutOptions.Start;
                        }
                    }
                    else
                    {
                        // Landscape: two-column popup with usage summary on the right
                        numPadPopup.ColumnDefinitions.Clear();
                        numPadPopup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        numPadPopup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        numPadPopup.RowDefinitions.Clear();
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                        numPadPopup.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7, GridUnitType.Star) });

                        Grid.SetColumn(numPadPopup, 0);
                        Grid.SetColumnSpan(numPadPopup, 2);
                        Grid.SetRow(numPadPopup, 0);
                        Grid.SetRowSpan(numPadPopup, 3);

                        if (usageGrid != null)
                        {
                            Grid.SetColumn(usageGrid, 1);
                            Grid.SetRow(usageGrid, 0);
                            Grid.SetRowSpan(usageGrid, 2);
                        }

                        if (frameNewReadingPopup != null)
                        {
                            Grid.SetColumn(frameNewReadingPopup, 0);
                            Grid.SetRow(frameNewReadingPopup, 0);
                        }
                        if (lblNewReadingPopup != null)
                        {
                            Grid.SetColumn(lblNewReadingPopup, 0);
                            Grid.SetRow(lblNewReadingPopup, 0);
                        }

                        if (numPadButtonsGrid != null)
                        {
                            Grid.SetColumn(numPadButtonsGrid, 0);
                            Grid.SetRow(numPadButtonsGrid, 1);
                            Grid.SetRowSpan(numPadButtonsGrid, 2);
                        }

                        if (btnCloseNumPad != null)
                        {
                            Grid.SetColumn(btnCloseNumPad, 1);
                            Grid.SetRow(btnCloseNumPad, 2);
                            btnCloseNumPad.VerticalOptions = LayoutOptions.End;
                        }
                    }
                }
            }
            catch
            {
                // ignore layout changes if elements aren't ready yet
            }
        }
    }
}