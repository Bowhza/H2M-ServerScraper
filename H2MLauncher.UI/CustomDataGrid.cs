﻿using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace H2MLauncher.UI
{
    public class CustomDataGrid : DataGrid
    {
        static CustomDataGrid()
        {
            //DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomDataGrid), new FrameworkPropertyMetadata(typeof(CustomDataGrid)));

            // Override Coerce of ItemsSourceProperty 
            ItemsSourceProperty.OverrideMetadata(typeof(CustomDataGrid), new FrameworkPropertyMetadata(null, OnCoercItemsSource));
        }

        private static object OnCoercItemsSource(DependencyObject d, object newValue)
        {
            // DataGrid messes up sorting changing the ItemsSource. Overriding this method
            // to do nothing fixes that issue, and keeps column sorting intact when changing ItemsSource.
            return newValue;
        }
    }
}