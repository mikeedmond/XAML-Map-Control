﻿// XAML Map Control - http://xamlmapcontrol.codeplex.com/
// Copyright © 2012 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

#if WINRT
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#else
using System.Windows;
using System.Windows.Controls;
#endif

namespace MapControl
{
    /// <summary>
    /// Manages a collection of selectable items on a Map. Uses MapItem as container for items
    /// and updates the IsCurrent attached property when the Items.CurrentItem property changes.
    /// </summary>
    public partial class MapItemsControl : ListBox
    {
        public MapItemsControl()
        {
            MapPanel.AddParentMapHandlers(this);
            DefaultStyleKey = typeof(MapItemsControl);
            Initialize();
        }

        partial void Initialize();

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new MapItem();
        }

        public UIElement ContainerFromItem(object item)
        {
            return item != null ? ItemContainerGenerator.ContainerFromItem(item) as UIElement : null;
        }

        public object ItemFromContainer(DependencyObject container)
        {
            return container != null ? ItemContainerGenerator.ItemFromContainer(container) : null;
        }
    }
}
