using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Module skeleton; object types are added in the module implementation slice.</summary>
    public sealed class KinematicsModule : IModule
    {
        public string Id => "kinematics";
        public I18nText Name => new("Kinematics & mechanisms", "Kinematik & Mechanismen");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
