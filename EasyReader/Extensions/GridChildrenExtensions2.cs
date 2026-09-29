using System.Collections.Generic;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Controls
{
    public static class GridChildrenExtensions2
    {
        public static void Add(this IList<Microsoft.Maui.IView> children, Microsoft.Maui.IView view, int column, int row)
        {
            if (children == null || view == null) return;
            children.Add(view);
            if (view is Microsoft.Maui.Controls.BindableObject bo)
            {
                bo.SetValue(Grid.ColumnProperty, column);
                bo.SetValue(Grid.RowProperty, row);
            }
        }

        public static void Add(this IList<Microsoft.Maui.IView> children, Microsoft.Maui.IView view, int left, int right, int top, int bottom)
        {
            if (children == null || view == null) return;
            children.Add(view);
            if (view is Microsoft.Maui.Controls.BindableObject bo)
            {
                bo.SetValue(Grid.ColumnProperty, left);
                bo.SetValue(Grid.ColumnSpanProperty, right - left);
                bo.SetValue(Grid.RowProperty, top);
                bo.SetValue(Grid.RowSpanProperty, bottom - top);
            }
        }
    }
}
