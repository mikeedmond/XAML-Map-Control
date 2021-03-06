﻿// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// © 2017 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
#if WINDOWS_UWP
using Windows.UI.Xaml.Controls;
#else
using System.Windows.Controls;
#endif

namespace MapControl
{
    public partial class Tile
    {
        public static TimeSpan FadeDuration { get; set; } = TimeSpan.FromSeconds(0.15);

        public readonly int ZoomLevel;
        public readonly int X;
        public readonly int Y;
        public readonly Image Image = new Image { Opacity = 0d };

        public Tile(int zoomLevel, int x, int y)
        {
            ZoomLevel = zoomLevel;
            X = x;
            Y = y;
        }

        public bool Pending { get; set; } = true;

        public int XIndex
        {
            get
            {
                var numTiles = 1 << ZoomLevel;
                return ((X % numTiles) + numTiles) % numTiles;
            }
        }
    }
}
