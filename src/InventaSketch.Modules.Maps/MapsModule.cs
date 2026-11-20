using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>
    /// Maps and routes: map backgrounds, roads and paths, route lines with waypoints, points of interest, regions, scale bar and north arrow; schematic route sketches for presentations.
    /// Skeleton: types, categories and library follow.
    /// </summary>
    public sealed class MapsModule : IModule
    {
        public string Id => "maps";
        public I18nText Name => new("Maps & routes", "Karten & Routen");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
