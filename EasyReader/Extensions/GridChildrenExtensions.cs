using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Controls
{
    public static class GridChildrenExtensions
    {
        public static void Add(this IList<View> children, View view, int column, int row)
        {
            if (children == null || view == null) return;
            children.Add(view);
            Grid.SetColumn(view, column);
            Grid.SetRow(view, row);
        }

        // left, right, top, bottom (Xamarin.Forms overload style)
        public static void Add(this IList<View> children, View view, int left, int right, int top, int bottom)
        {
            if (children == null || view == null) return;
            children.Add(view);
            Grid.SetColumn(view, left);
            Grid.SetColumnSpan(view, right - left);
            Grid.SetRow(view, top);
            Grid.SetRowSpan(view, bottom - top);
        }
    }
}
