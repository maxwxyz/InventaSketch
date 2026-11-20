using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>Module skeleton; object types are added in the module implementation slice.</summary>
    public sealed class DiagramsModule : IModule
    {
        public string Id => "diagrams";
        public I18nText Name => new("Diagrams (UML, ER, flow)", "Diagramme (UML, ER, Ablauf)");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
