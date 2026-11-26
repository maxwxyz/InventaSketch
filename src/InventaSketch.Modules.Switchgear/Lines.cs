using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>Wires (single cores in the cabinet) and cables (installation, bus): polylines whose ends dock on pins.</summary>
    internal static class Lines
    {
        public const string WireId = "sw.line.wire";
        public const string CableId = "sw.line.cable";
        /// <summary>Library item of the bus cable (connector of bus terminals).</summary>
        public const string BusCableItem = "sw.line.cable.knx";

        /// <summary>The connection of a pin of the given medium: a wire at screw terminals, a bus cable at bus terminals, an installation cable at room devices.</summary>
        public static PortConnection ConnectionFor(string medium) => medium switch
        {
            PortMedia.Data => PortConnection.Of(PortMedia.Data, BusCableItem),
            PortMedia.ElectricInstallation => PortConnection.Of(PortMedia.ElectricInstallation, CableId),
            _ => PortConnection.Of(PortMedia.Electric, WireId),
        };

        public static bool IsLine(string type) => type == WireId || type == CableId;

        /// <summary>Core colours (IEC 60757 codes) and their drawing colours.</summary>
        public static readonly (string Code, string En, string De, string Colour)[] Colours =
        {
            ("BK", "Black", "Schwarz", "#212121"),
            ("BN", "Brown", "Braun", "#6d4c41"),
            ("GY", "Grey", "Grau", "#9e9e9e"),
            ("BU", "Blue", "Blau", "#1e88e5"),
            ("GNYE", "Green-yellow", "Grün-gelb", "#43a047"),
            ("RD", "Red", "Rot", "#e53935"),
            ("DBU", "Dark blue", "Dunkelblau", "#1a237e"),
            ("WH", "White", "Weiß", "#bdbdbd"),
            ("VT", "Violet", "Violett", "#8e24aa"),
            ("OG", "Orange", "Orange", "#fb8c00"),
        };

        public static string ColourOf(string code) => Colours.FirstOrDefault(c => c.Code == code).Colour ?? "#212121";

        /// <summary>Midpoint and direction of the longest segment (label placement).</summary>
        public static (Vec2 Mid, Vec2 Dir) Longest(IReadOnlyList<Vec2> p)
        {
            var best = 0; var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var dir = (p[best + 1] - p[best]).Normalized();
            if (dir.Length < Geo.Eps) dir = new Vec2(1, 0);
            return (Vec2.Lerp(p[best], p[best + 1], 0.5), dir);
        }

        /// <summary>Unit normal pointing up on the page (left for vertical segments).</summary>
        public static Vec2 Up(Vec2 dir)
        {
            var n = dir.Perp;
            return n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X > 0) ? -n : n;
        }

        /// <summary>A text along the longest segment, above it, kept upright.</summary>
        public static Primitive Label(IReadOnlyList<Vec2> p, string text, double size, double gap = 0)
        {
            var (mid, dir) = Longest(p);
            if (dir.X < -1e-9 || (Math.Abs(dir.X) <= 1e-9 && dir.Y > 0)) dir = -dir;
            var at = mid + Up(dir) * (size * 0.9 + gap);
            var angle = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
            return Prim.Text(at.X, at.Y, text, new TextStyle { Size = size, Anchor = TextAnchor.Middle, Baseline = TextBaseline.Middle }, angle) with { Tag = SwTags.Label };
        }

        public static IEnumerable<Anchor> RailAnchors(IReadOnlyList<Vec2> p)
        {
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > Geo.Eps) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal abstract class SwLineType : ObjectType
    {
        protected SwLineType(string id, I18nText name, params string[] keywords)
        {
            Id = id; Name = name; Keywords = keywords;
        }

        public override string Id { get; }
        public override I18nText Name { get; }
        public override string Category => SwCategories.Wiring;
        public override IReadOnlyList<string> Keywords { get; }
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override bool InsertablePoints => true;
        public override bool IsPart => false;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            return pts.Count < 2 ? Array.Empty<Primitive>() : RenderPoints(o, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Lines.RailAnchors(ctx.Doc.ResolvePoints(o));

        protected static ContextAction RefInput(SketchObject o, string en, string de)
            => ContextAction.WithInput("sw.ref", new I18nText(en + " …", de + " …"), IconIds.Name, new I18nText(en, de), o.Str("ref", ""),
                (x, t) => x.WithParam("ref", t.Trim())) with { Group = "label" };
    }

    /// <summary>Single core (wire) in the cabinet: colour, cross-section and wire number.</summary>
    internal sealed class WireType : SwLineType
    {
        public WireType() : base(Lines.WireId, new I18nText("Wire (core)", "Ader (Verdrahtung)"),
            "wire", "core", "ader", "verdrahtung", "draht", "litze", "h07v-k") { }

        public static readonly ParamDef WireRef = Pm.Text("ref", "Wire number", "Adernummer");
        public static readonly ParamDef Section = Pm.Values("section", "Cross-section (mm²)", "Querschnitt (mm²)", "1.5", "0.5", "0.75", "1", "1.5", "2.5", "4", "6", "10", "16");
        public static readonly ParamDef Colour = Pm.Choice("colour", "Colour", "Farbe", "BK", Lines.Colours.Select(c => (c.Code, c.Code + " " + c.En, c.Code + " " + c.De)).ToArray());

        public override IReadOnlyList<ParamDef> Parameters => new[] { Colour, Section, WireRef };
        public override string? LineMedium(SketchObject line) => PortMedia.Electric;
        /// <summary>A core is drawn in its real colour: colour by medium leaves it (<see cref="LineMedia"/>).</summary>
        public override string? MediumRole(SketchObject line) => LineMedia.OwnColour;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var code = o.Str("colour", "BK");
            var list = new List<Primitive>();
            if (code == "GNYE")
            {
                list.Add(Prim.Polyline(p, Sx.Coloured("#43a047", 0.5)) with { Tag = SwTags.Body });
                list.Add(Prim.Polyline(p, Sx.Coloured("#fdd835", 0.5) with { Dash = DashStyle.Dash }) with { Tag = SwTags.Body });
            }
            else list.Add(Prim.Polyline(p, Sx.Coloured(Lines.ColourOf(code), 0.5)) with { Tag = SwTags.Body });
            var label = o.Str("ref", "").Trim();
            if (label.Length > 0) list.Add(Lines.Label(p, label, 2.2));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "sw.colour", Colour, Colour.Label, "param", null)) yield return a;
            foreach (var a in ActionChoices.FromParam(o, "sw.section", Section, Section.Label, "param", null)) yield return a;
            yield return RefInput(o, "Wire number", "Adernummer");
        }
    }

    /// <summary>Installation or bus cable: type, cores × cross-section, length and designation.</summary>
    internal sealed class CableType : SwLineType
    {
        public CableType() : base(Lines.CableId, new I18nText("Cable (designation, type, length)", "Kabel (Kennzeichen, Typ, Länge)"),
            "cable", "kabel", "leitung", "nym", "nym-j", "nyy", "mantelleitung", "w1", "j-y(st)y", "knx") { }

        public static readonly ParamDef CableRef = Pm.Text("ref", "Cable designation (e.g. -W1)", "Kabelkennzeichen (z. B. -W1)");
        public static readonly ParamDef Kind = Pm.Choice("cableType", "Cable type", "Kabeltyp", "nymj",
            ("nymj", "NYM-J", "NYM-J"), ("nyyj", "NYY-J", "NYY-J"), ("h07vk", "H07V-K", "H07V-K"), ("h05vvf", "H05VV-F", "H05VV-F"),
            ("jysty", "J-Y(ST)Y", "J-Y(ST)Y"), ("cat", "Data cable (Cat)", "Datenkabel (Cat)"));
        public static readonly ParamDef Cores = Pm.Values("cores", "Cores", "Adern", "3", "1", "2", "3", "4", "5", "7");
        public static readonly ParamDef Section = Pm.Values("section", "Cross-section (mm²)", "Querschnitt (mm²)", "1.5", "0.8", "1.5", "2.5", "4", "6", "10", "16");
        public static readonly ParamDef Length = Pm.Num("length", "Length (m)", "Länge (m)", 0, 0, 10000, "m", 0.5);
        public static readonly ParamDef From = Pm.Text("from", "From (when not docked)", "Von (wenn nicht angedockt)");
        public static readonly ParamDef To = Pm.Text("to", "To (when not docked)", "Nach (wenn nicht angedockt)");

        public override IReadOnlyList<ParamDef> Parameters => new[] { Kind, Cores, Section, CableRef, Length, From, To };

        public static bool IsData(SketchObject o) => o.Str("cableType", "nymj") is "jysty" or "cat";

        public override string? LineMedium(SketchObject line) => IsData(line) ? PortMedia.Data : PortMedia.ElectricInstallation;

        public static string TypeText(SketchObject o)
            => (Kind.Options ?? Array.Empty<ParamOption>()).FirstOrDefault(x => x.Value == o.Str("cableType", "nymj"))?.Label.En ?? o.Str("cableType", "");

        /// <summary>"NYM-J 3×1.5" (J-Y(ST)Y in pairs: "J-Y(ST)Y 2×2×0.8").</summary>
        public static string Spec(SketchObject o)
        {
            var cores = o.Str("cores", "3");
            var section = o.Str("section", "1.5");
            if (o.Str("cableType", "nymj") == "jysty" && int.TryParse(cores, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n % 2 == 0)
                return TypeText(o) + " " + (n / 2).ToString(CultureInfo.InvariantCulture) + "×2×" + section;
            return TypeText(o) + " " + cores + "×" + section;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var colour = o.Str("cableType", "nymj") == "jysty" ? "#2e7d32" : "#455a64";
            var list = new List<Primitive> { Prim.Polyline(p, Sx.Coloured(colour, 0.8)) with { Tag = SwTags.Body } };
            // Cores: oblique strokes across the longest segment (up to three), else one stroke and the number.
            // Cores: oblique strokes (up to three, else one stroke and the number below the line), a quarter along the
            // longest segment so they stay clear of the label in its middle.
            var (mid, dir) = Lines.Longest(p);
            var segment = Enumerable.Range(0, p.Count - 1).Max(i => p[i].DistanceTo(p[i + 1]));
            var at0 = mid - dir * (segment * 0.25);
            var n = Math.Max(1, (int)Num.ParseOr(o.Str("cores", "3"), 3));
            var nrm = Lines.Up(dir);
            var strokes = n <= 3 ? n : 1;
            for (var i = 0; i < strokes; i++)
            {
                var c = at0 + dir * ((i - (strokes - 1) / 2.0) * 1.4);
                list.Add(Prim.Line(c - nrm * 1.6 - dir * 0.7, c + nrm * 1.6 + dir * 0.7, Sx.Coloured(colour, 0.25)) with { Tag = SwTags.Symbol });
            }
            if (n > 3)
            {
                var at = at0 - nrm * 2.4;
                list.Add(Prim.Text(at.X, at.Y, n.ToString(CultureInfo.InvariantCulture), new TextStyle { Size = 2.0, Anchor = TextAnchor.Middle }) with { Tag = SwTags.Symbol });
            }
            var rf = o.Str("ref", "").Trim();
            if (rf.Length > 0)
            {
                var len = o.Num("length", 0);
                var text = rf + " " + Spec(o) + (len > 0 ? " " + len.ToString("0.#", CultureInfo.InvariantCulture) + " m" : "");
                list.Add(Lines.Label(p, text, 2.2, 0.4));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in ActionChoices.FromParam(o, "sw.cableType", Kind, Kind.Label, "param", null)) yield return a;
            foreach (var a in ActionChoices.FromParam(o, "sw.cores", Cores, Cores.Label, "param", null)) yield return a;
            foreach (var a in ActionChoices.FromParam(o, "sw.section", Section, Section.Label, "param", null)) yield return a;
            yield return RefInput(o, "Cable designation", "Kabelkennzeichen");
            yield return ContextAction.WithInput("sw.length", new I18nText("Length …", "Länge …"), IconIds.QtyLength, new I18nText("Length", "Länge"),
                o.Num("length", 0).ToString("0.#", CultureInfo.InvariantCulture), (x, t) => x.WithParam("length", Math.Max(0, Num.Parse(t))), "m") with { Group = "label" };
        }
    }
}
