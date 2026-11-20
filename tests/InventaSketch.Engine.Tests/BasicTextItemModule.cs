using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Re-exposes the hidden basic text type as a library item for draw-plan tests.</summary>
    internal sealed class BasicTextItemModule : IModule
    {
        public string Id => "etest.basictext";
        public I18nText Name => new("Basic text items");
        public string Version => "1.0";
        public IEnumerable<ObjectType> Types => Array.Empty<ObjectType>();
        public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
        public IEnumerable<LibraryItem> Library => new[] { new LibraryItem("basic.text", "basic.text", new I18nText("Text"), "basic") { Text = "Text" } };
    }
}
