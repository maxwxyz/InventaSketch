using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// The installation view switch of the building services (plan ↔ section ↔ schematic): the rule lives in Core
    /// (<see cref="InstallationViews"/>) so every installation element – building services, electrical house symbols –
    /// switches together; this class keeps the module's names.
    /// </summary>
    public static class ServiceViews
    {
        public const string MetaPlanY = InstallationViews.MetaPlanY;
        public const string MetaPlanRotation = InstallationViews.MetaPlanRotation;
        public const string MetaPlanPoints = InstallationViews.MetaPlanPoints;
        public const string MetaFloorY = InstallationViews.MetaFloorY;

        /// <summary>The rule object for <see cref="ObjectType.AssemblyViewSwitch"/>.</summary>
        public static IAssemblyViewSwitch Rule => InstallationViews.Rule;

        public static bool TakesPart(ObjectType? type) => InstallationViews.TakesPart(type);

        public static double BottomY(double floorY, double heightMm, double scale) => InstallationViews.BottomY(floorY, heightMm, scale);

        public static double HeightAt(double floorY, double bottomY, double scale) => InstallationViews.HeightAt(floorY, bottomY, scale);

        public static SketchDocument Switch(Registry registry, SketchDocument doc, IReadOnlyList<string> ids, string view, double? floorY = null, Lang lang = Lang.En)
            => InstallationViews.Switch(registry, doc, ids, view, floorY, lang);
    }
}
