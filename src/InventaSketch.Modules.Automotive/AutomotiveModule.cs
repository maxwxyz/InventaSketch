using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>Module skeleton; object types are added in the module implementation slice.</summary>
    public sealed class AutomotiveModule : IModule
    {
        public string Id => "automotive";
        public I18nText Name => new("Vehicles, ADAS & HMI", "Fahrzeuge, ADAS & HMI");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
