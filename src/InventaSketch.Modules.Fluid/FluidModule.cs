using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Fluid power (hydraulics and pneumatics) circuit symbols after ISO 1219 ideas: pumps, motors, cylinders, valves, lines, accumulators, filters and sensors, drawn independently.
    /// Skeleton: types, categories and library follow.
    /// </summary>
    public sealed class FluidModule : IModule
    {
        public string Id => "fluid";
        public I18nText Name => new("Fluid power", "Fluidtechnik");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
