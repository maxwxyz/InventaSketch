using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Module skeleton; object types are added in the module implementation slice.</summary>
    public sealed class AnnotationModule : IModule
    {
        public string Id => "annotation";
        public I18nText Name => new("Annotation");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
    }
}
