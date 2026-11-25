using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Canonical, host-independent document model. The document is the single source of truth;
    // SVG/PDF/PNG and host-native shapes are render targets. Geometry in mm, y down, angles in
    // degrees clockwise on screen.
    // =============================================================================================

    /// <summary>Optional semantic parameter value (number, text or boolean).</summary>
    [JsonConverter(typeof(ParamValueJsonConverter))]
    public readonly record struct ParamValue
    {
        public double? Number { get; init; }
        public string? Text { get; init; }
        public bool? Bool { get; init; }

        public static implicit operator ParamValue(double v) => new() { Number = v };
        public static implicit operator ParamValue(int v) => new() { Number = v };
        public static implicit operator ParamValue(string v) => new() { Text = v };
        public static implicit operator ParamValue(bool v) => new() { Bool = v };

        public override string ToString() => Number?.ToString(CultureInfo.InvariantCulture) ?? Text ?? (Bool.HasValue ? (Bool.Value ? "true" : "false") : "");
    }

    public sealed class ParamValueJsonConverter : JsonConverter<ParamValue>
    {
        public override ParamValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.Number => new ParamValue { Number = reader.GetDouble() },
            JsonTokenType.String => new ParamValue { Text = reader.GetString() },
            JsonTokenType.True => new ParamValue { Bool = true },
            JsonTokenType.False => new ParamValue { Bool = false },
            _ => default,
        };

        public override void Write(Utf8JsonWriter writer, ParamValue value, JsonSerializerOptions options)
        {
            if (value.Number.HasValue) writer.WriteNumberValue(value.Number.Value);
            else if (value.Text is not null) writer.WriteStringValue(value.Text);
            else if (value.Bool.HasValue) writer.WriteBooleanValue(value.Bool.Value);
            else writer.WriteNullValue();
        }
    }

    public sealed record Transform(double X, double Y, double Rotation = 0, bool FlipX = false, bool FlipY = false);

    public sealed record Size2(double W, double H);

    /// <summary>
    /// Link from a point of one object to a location on another object. (U, V) are normalised box
    /// coordinates of the target in [-0.5, 0.5], so the attachment follows moving, rotating and free
    /// scaling of the target.
    /// </summary>
    public sealed record Attachment(string ObjectId, double U, double V)
    {
        /// <summary>Optional id of the target anchor (pin/port) this point is docked to.</summary>
        public string? AnchorId { get; init; }
    }

    public sealed record SketchObject
    {
        public string Id { get; init; } = "";
        /// <summary>Registered object type id, e.g. "mech.gear.spur".</summary>
        public string Type { get; init; } = "";
        /// <summary>Box centre, rotation and flip. Ignored for point-geometry objects.</summary>
        public Transform Transform { get; init; } = new(0, 0);
        /// <summary>Box size in mm. Free scaling changes this, never the semantic parameters.</summary>
        public Size2 Size { get; init; } = new(20, 20);
        /// <summary>Point-geometry objects (lines, arrows, leaders, dimensions): page coordinates.</summary>
        public IReadOnlyList<Vec2>? Points { get; init; }
        /// <summary>Optional attachment per point (same index as <see cref="Points"/>).</summary>
        public IReadOnlyList<Attachment?>? Attach { get; init; }
        /// <summary>Representation/view id, e.g. "stick", "axial", "section".</summary>
        public string? View { get; init; }
        /// <summary>Optional semantic parameters. Never required for quick sketching.</summary>
        public IReadOnlyDictionary<string, ParamValue> Params { get; init; } = new Dictionary<string, ParamValue>();
        /// <summary>Style overrides on top of the type's defaults.</summary>
        public Style? Style { get; init; }
        /// <summary>Free text (labels, text boxes, callouts).</summary>
        public string? Text { get; init; }
        /// <summary>Linked patent reference numeral (key into <see cref="SketchDocument.References"/>).</summary>
        public string? Ref { get; init; }
        /// <summary>Objects sharing a group id are selected and transformed together (assembly).</summary>
        public string? GroupId { get; init; }
        public string? Layer { get; init; }
        public bool Locked { get; init; }
        /// <summary>
        /// Coupling partners the user released (<see cref="Links"/>): the pair touches but does not move or scale together.
        /// Stored on both objects; an entry is dropped once the pair no longer touches.
        /// </summary>
        public IReadOnlyList<string>? Released { get; init; }
        /// <summary>Provenance and extension data (module-namespaced keys).</summary>
        public IReadOnlyDictionary<string, string>? Meta { get; init; }

        [JsonIgnore] public bool IsPointGeometry => Points is not null;

        public double Num(string key, double fallback) => Params.TryGetValue(key, out var v) && v.Number.HasValue ? v.Number.Value : fallback;
        public int Int(string key, int fallback) => Params.TryGetValue(key, out var v) && v.Number.HasValue ? (int)Math.Round(v.Number.Value) : fallback;
        public string Str(string key, string fallback) => Params.TryGetValue(key, out var v) && v.Text is not null ? v.Text : fallback;
        public bool Flag(string key, bool fallback) => Params.TryGetValue(key, out var v) && v.Bool.HasValue ? v.Bool.Value : fallback;

        public SketchObject WithParam(string key, ParamValue value)
        {
            var d = new Dictionary<string, ParamValue>(Params.Count + 1);
            foreach (var kv in Params) d[kv.Key] = kv.Value;
            d[key] = value;
            return this with { Params = d };
        }

        /// <summary>Local-to-page matrix (translation * rotation * flip). Identity for point geometry.</summary>
        public Matrix2D Matrix()
        {
            if (Points is not null) return Matrix2D.Identity;
            var t = Transform;
            return Matrix2D.Translation(t.X, t.Y) * Matrix2D.Rotation(Geo.DegToRad(t.Rotation)) * Matrix2D.Scaling(t.FlipX ? -1 : 1, t.FlipY ? -1 : 1);
        }

        /// <summary>Normalised box coordinates (u, v in [-0.5, 0.5]) to page coordinates.</summary>
        public Vec2 BoxPoint(double u, double v) => Matrix().Apply(new Vec2(u * Size.W, v * Size.H));

        public Vec2[] Corners() => Points is not null
            ? Rect.FromPoints(Points).Corners()
            : new[] { BoxPoint(-0.5, -0.5), BoxPoint(0.5, -0.5), BoxPoint(0.5, 0.5), BoxPoint(-0.5, 0.5) };

        public Rect Bounds() => Rect.FromPoints(Corners());

        /// <summary>Converts a page point into normalised box coordinates of this object.</summary>
        public (double U, double V) ToBox(Vec2 page)
        {
            var local = Matrix().Invert().Apply(page);
            return (Size.W > Geo.Eps ? local.X / Size.W : 0, Size.H > Geo.Eps ? local.Y / Size.H : 0);
        }
    }

    public sealed record Layer(string Id, string Name, bool Visible = true, bool Locked = false, bool Helper = false);

    public sealed record Page
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "Page 1";
        /// <summary>Sheet size in mm.</summary>
        public Size2 Size { get; init; } = PageSizes.Slide16x9;
        /// <summary>Z-ordered object ids (first = bottom).</summary>
        public IReadOnlyList<string> Order { get; init; } = Array.Empty<string>();
        /// <summary>Figure label, e.g. "FIG. 1".</summary>
        public string? Figure { get; init; }
    }

    public sealed record ReferenceEntry(string Numeral, string Label, string? Note = null);

    public sealed record GridSettings(double Size = 5, bool Visible = true, bool Snap = true);

    public sealed record DocumentSettings
    {
        public string Units { get; init; } = "mm";
        public GridSettings Grid { get; init; } = new();
        /// <summary>Active patent/export profile id.</summary>
        public string? Profile { get; init; }
        /// <summary>
        /// Black-and-white output (patent style): renderers map every colour to black or white and drop translucent
        /// fills (highlighters); the objects keep their colours, so switching it off restores them.
        /// </summary>
        public bool Monochrome { get; init; }
        /// <summary>
        /// ISO 128 line group of the drawing: the width of visible (thick) lines in mm, every other line role scaled with it
        /// (<see cref="Style.LineGroups"/>; null = 0.5, the group the elements are designed in).
        /// </summary>
        public double? LineGroup { get; init; }
        /// <summary>Accent colour of the drawing (#rrggbb, <see cref="Accents"/>): coloured parts take its hue; null = each module's colours.</summary>
        public string? Accent { get; init; }
        /// <summary>
        /// Plan scale denominator of the document (100 means 1:100), used by types with <see cref="ObjectType.UsesPlanScale"/>
        /// when an object has no scale of its own. Null: each module's default.
        /// </summary>
        public double? PlanScale { get; init; }
    }

    public static class PageSizes
    {
        /// <summary>PowerPoint default widescreen slide (13.333 in x 7.5 in).</summary>
        public static readonly Size2 Slide16x9 = new(338.667, 190.5);
        public static readonly Size2 A4Portrait = new(210, 297);
        public static readonly Size2 A4Landscape = new(297, 210);
        public static readonly Size2 UsLetterPortrait = new(215.9, 279.4);
    }

    public sealed record SketchDocument
    {
        public const string FormatId = "inventasketch-document";
        public const int CurrentVersion = 1;

        public string Format { get; init; } = FormatId;
        public int Version { get; init; } = CurrentVersion;
        public string Id { get; init; } = Ids.New("d");
        public string Title { get; init; } = "Untitled sketch";
        public IReadOnlyList<Page> Pages { get; init; } = Array.Empty<Page>();
        public IReadOnlyDictionary<string, SketchObject> Objects { get; init; } = new Dictionary<string, SketchObject>();
        public IReadOnlyList<Layer> Layers { get; init; } = new[] { new Layer("default", "Default") };
        /// <summary>Patent reference numeral dictionary keyed by numeral.</summary>
        public IReadOnlyDictionary<string, ReferenceEntry> References { get; init; } = new Dictionary<string, ReferenceEntry>();
        public DocumentSettings Settings { get; init; } = new();

        public static SketchDocument Create(Size2? pageSize = null, string? title = null) => new()
        {
            Title = title ?? "Untitled sketch",
            Pages = new[] { new Page { Id = Ids.New("p"), Size = pageSize ?? PageSizes.Slide16x9 } },
        };

        public SketchObject? Get(string id) => Objects.TryGetValue(id, out var o) ? o : null;

        public Page? PageOf(string objectId) => Pages.FirstOrDefault(p => p.Order.Contains(objectId));

        /// <summary>Objects of a page in z-order.</summary>
        public IEnumerable<SketchObject> ObjectsOn(Page page)
        {
            foreach (var id in page.Order) if (Objects.TryGetValue(id, out var o)) yield return o;
        }

        /// <summary>Effective page points of a point-geometry object, honouring attachments.</summary>
        public IReadOnlyList<Vec2> ResolvePoints(SketchObject o) => ResolvePoints(o, 0);

        /// <summary>
        /// Points with attachments resolved: box targets at their normalised position, named points of other lines
        /// ("j…"/"p…", a link docked on another link's joint) at that point (followed up to a few links deep).
        /// </summary>
        private IReadOnlyList<Vec2> ResolvePoints(SketchObject o, int depth)
        {
            var pts = o.Points ?? Array.Empty<Vec2>();
            var result = new Vec2[pts.Count];
            for (var i = 0; i < pts.Count; i++)
            {
                var a = o.Attach is not null && i < o.Attach.Count ? o.Attach[i] : null;
                result[i] = pts[i];
                if (a is null || !Objects.TryGetValue(a.ObjectId, out var target)) continue;
                if (!target.IsPointGeometry) result[i] = target.BoxPoint(a.U, a.V);
                else if (depth < 8 && Attachments.PointIndex(a.AnchorId) is int k && target.Id != o.Id)
                {
                    var tp = ResolvePoints(target, depth + 1);
                    if (k < tp.Count) result[i] = tp[k];
                }
            }
            return result;
        }

        /// <summary>All ids sharing a group with any of the given ids (inclusive).</summary>
        public IReadOnlyList<string> ExpandGroups(IEnumerable<string> ids)
        {
            var list = ids.ToList();
            var groups = new HashSet<string>(list.Select(id => Get(id)?.GroupId).Where(g => g is not null)!);
            if (groups.Count == 0) return list;
            var set = new HashSet<string>(list);
            foreach (var o in Objects.Values) if (o.GroupId is not null && groups.Contains(o.GroupId)) set.Add(o.Id);
            return set.ToList();
        }
    }

    public static class Ids
    {
        private static readonly Random Rng = new();
        private static readonly object Lock = new();

        /// <summary>Short random id with prefix, e.g. "o3f9a1c2b7d4e".</summary>
        public static string New(string prefix = "o")
        {
            var bytes = new byte[6];
            lock (Lock) Rng.NextBytes(bytes);
            return prefix + string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
