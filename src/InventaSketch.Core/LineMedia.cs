using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// The medium and role of a connection line (<see cref="LineMedia.Of"/>): the medium a line carries
    /// (<see cref="Building.LineMedium"/>, a <see cref="PortMedia"/> path), its role within that medium (a conductor "L1", "N",
    /// "PE", "L+"; heating "flow"/"return"; hydraulic "pressure", "tank", "pilot", "drain"; a process fluid …) and the entry of the
    /// colour scheme it takes in the colour mode "by medium" (<see cref="MediumColour"/>, null: the line keeps its own colours).
    /// </summary>
    public sealed record LineMediumInfo(string Medium, string? Role, MediumColour? Colour);

    /// <summary>
    /// One entry of the colour scheme "by medium": id, bilingual name, stroke colour (#rrggbb), an optional second colour
    /// drawn as dashes over the first (green-yellow protective conductors) and where the colour comes from: a cited standard
    /// (IEC 60445 for conductor identification, DIN 2403 for pipelines by fluid) or "convention" (common drawing practice).
    /// </summary>
    public sealed record MediumColour(string Id, I18nText Name, string Color, string? Stripe, I18nText Source);

    /// <summary>
    /// Display mode "colour by medium" (ROADMAP plan review 2026-11-25 night, item 11): a slide-wide switch
    /// (<see cref="DocumentSettings.ColorBy"/> = <see cref="ByMedium"/>) that colours connection lines by medium and role at
    /// render time (<see cref="Registry.RenderLocal"/>); the stored objects never change, so switching it off restores the
    /// original look. Module-agnostic: media come from <see cref="Building.LineMedium"/> (the line type's own medium, else the
    /// port it is docked on), roles from the generic parameter <see cref="RoleParam"/>, a type's
    /// <see cref="ObjectType.MediumRole"/> and the medium path itself (heating flow/return, hot/cold water, air kinds …).
    /// </summary>
    public static class LineMedia
    {
        /// <summary>Value of <see cref="DocumentSettings.ColorBy"/> for "colour by medium".</summary>
        public const string ByMedium = "medium";

        /// <summary>Generic text parameter of any connection line: its role ("L1", "N", "PE", "L+", "pressure", "steam" …).</summary>
        public const string RoleParam = "lineRole";

        /// <summary>
        /// Role a type reports for lines that already show their real conductor colour (a cabinet wire with its core colour):
        /// the colour mode leaves them as they are unless a role is set explicitly.
        /// </summary>
        public const string OwnColour = "own";

        private static readonly I18nText Iec60445 = new("IEC 60445", "IEC 60445");
        private static readonly I18nText Din2403 = new("DIN 2403", "DIN 2403");
        private static readonly I18nText Convention = new("Convention", "Konvention");

        private static MediumColour C(string id, string en, string de, string color, I18nText source, string? stripe = null) => new(id, new I18nText(en, de), color, stripe, source);

        /// <summary>
        /// The colour scheme, in legend order. Conductor colours of AC systems follow IEC 60445 (identification of conductors
        /// by colours: L1 brown, L2 black, L3 grey, neutral and mid-point conductor blue, protective conductor green-yellow;
        /// a PEN conductor is green-yellow with blue marks at its ends). Pipeline colours by fluid follow the group colours of
        /// DIN 2403 (water green, steam red, air grey, flammable gases yellow, acids orange, alkalis violet, flammable liquids
        /// brown, oxygen blue; compressed air takes the air colour). Everything else is common practice and marked "convention":
        /// DC polarity, bus and signal lines, heating flow/return, domestic water, air kinds, refrigerant, vacuum, optical fibre
        /// and hydraulic pressure/return/pilot/drain lines.
        /// </summary>
        public static readonly IReadOnlyList<MediumColour> Scheme = new[]
        {
            // Electrical: AC conductors (IEC 60445)
            C("L1", "Line conductor L1", "Außenleiter L1", "#8d5524", Iec60445),
            C("L2", "Line conductor L2", "Außenleiter L2", "#1a1a1a", Iec60445),
            C("L3", "Line conductor L3", "Außenleiter L3", "#8c8c8c", Iec60445),
            C("N", "Neutral conductor N", "Neutralleiter N", "#2f8fdd", Iec60445),
            C("PE", "Protective conductor PE", "Schutzleiter PE", "#2e9d3a", Iec60445, "#f2d500"),
            C("PEN", "PEN conductor (blue marks at the ends)", "PEN-Leiter (blaue Markierung an den Enden)", "#2e9d3a", Iec60445, "#f2d500"),
            C("M", "Mid-point conductor M", "Mittelleiter M", "#2f8fdd", Iec60445),
            // Electrical: DC, power and bus (convention)
            C("L+", "DC positive L+", "Gleichstrom Plus L+", "#d32f2f", Convention),
            C("L-", "DC negative L−", "Gleichstrom Minus L−", "#283593", Convention),
            C("dc", "DC power (polarity not set)", "Gleichstrom (Polarität nicht festgelegt)", "#e65100", Convention),
            C("power", "Power (conductor not set)", "Energie (Leiter nicht festgelegt)", "#1a1a1a", Convention),
            C("bus", "Bus / data line", "Bus-/Datenleitung", "#7b1fa2", Convention),
            C("signal", "Signal / instrument line", "Signal-/Messleitung", "#ef6c00", Convention),
            // Heating, water, air, energy carriers (convention; water and gas as DIN 2403)
            C("flow", "Heating flow (warm)", "Heizung Vorlauf (warm)", "#d32f2f", Convention),
            C("return", "Heating return (cold)", "Heizung Rücklauf (kalt)", "#1976d2", Convention),
            C("hot", "Domestic hot water", "Trinkwasser warm", "#e64a19", Convention),
            C("cold", "Domestic cold water", "Trinkwasser kalt", "#2e7d32", Convention),
            C("circulation", "Hot water circulation", "Zirkulation", "#c2185b", Convention),
            C("waste", "Waste water", "Abwasser", "#6d4c41", Convention),
            C("water", "Water", "Wasser", "#2e7d32", Din2403),
            C("heat", "Heat (warm)", "Wärme (warm)", "#d32f2f", Convention),
            C("cooling", "Cooling (cold)", "Kälte (kalt)", "#1976d2", Convention),
            C("air.supply", "Supply air", "Zuluft", "#1565c0", Convention),
            C("air.extract", "Extract air", "Abluft", "#f9a825", Convention),
            C("air.outdoor", "Outdoor air", "Außenluft", "#2e7d32", Convention),
            C("air.exhaust", "Exhaust air", "Fortluft", "#6d4c41", Convention),
            C("gas", "Flammable gas (natural gas, hydrogen)", "Brennbares Gas (Erdgas, Wasserstoff)", "#fbc02d", Din2403),
            C("air.compressed", "Compressed air", "Druckluft", "#9e9e9e", Din2403),
            C("refrigerant", "Refrigerant", "Kältemittel", "#827717", Convention),
            C("vacuum", "Vacuum", "Vakuum", "#455a64", Convention),
            C("optical", "Optical fibre", "Lichtwellenleiter", "#00acc1", Convention),
            // Fluid power (convention)
            C("pressure", "Pressure line (P)", "Druckleitung (P)", "#d32f2f", Convention),
            C("tank", "Return line (T)", "Rücklaufleitung (T)", "#1976d2", Convention),
            C("pilot", "Pilot line", "Steuerleitung", "#ef6c00", Convention),
            C("drain", "Drain / leakage line", "Leckölleitung", "#00897b", Convention),
            // Process lines by fluid (DIN 2403 group colours)
            C("p.water", "Water", "Wasser", "#2e7d32", Din2403),
            C("p.steam", "Steam", "Dampf", "#c62828", Din2403),
            C("p.air", "Air", "Luft", "#9e9e9e", Din2403),
            C("p.gas", "Flammable gas", "Brennbares Gas", "#fbc02d", Din2403),
            C("p.acid", "Acid", "Säure", "#ef6c00", Din2403),
            C("p.alkali", "Alkali", "Lauge", "#7b1fa2", Din2403),
            C("p.oil", "Flammable liquid", "Brennbare Flüssigkeit", "#6d4c41", Din2403),
            C("p.oxygen", "Oxygen", "Sauerstoff", "#1565c0", Din2403),
        };

        /// <summary>The scheme entry with <paramref name="id"/>, or null.</summary>
        public static MediumColour? Entry(string? id) => id is null ? null : Scheme.FirstOrDefault(c => c.Id == id);

        // ------------------------------------------------------------------ roles

        private static readonly (string Value, I18nText Label)[] AcRoles =
        {
            ("L1", new("L1", "L1")), ("L2", new("L2", "L2")), ("L3", new("L3", "L3")), ("N", new("N", "N")),
            ("PE", new("PE", "PE")), ("PEN", new("PEN", "PEN")),
        };

        private static readonly (string Value, I18nText Label)[] DcRoles =
        {
            ("L+", new("L+", "L+")), ("L-", new("L−", "L−")), ("M", new("M", "M")),
        };

        private static readonly (string Value, I18nText Label)[] FluidRoles =
        {
            ("pressure", new("Pressure line (P)", "Druckleitung (P)")), ("tank", new("Return line (T)", "Rücklaufleitung (T)")),
        };

        private static readonly (string Value, I18nText Label)[] ProcessRoles =
        {
            ("water", new("Water", "Wasser")), ("steam", new("Steam", "Dampf")), ("air", new("Air", "Luft")), ("gas", new("Flammable gas", "Brennbares Gas")),
            ("acid", new("Acid", "Säure")), ("alkali", new("Alkali", "Lauge")), ("oil", new("Flammable liquid", "Brennbare Flüssigkeit")), ("oxygen", new("Oxygen", "Sauerstoff")),
        };

        private static bool IsElectricPower(string medium) => PortMedia.Covers(PortMedia.Electric, medium);

        /// <summary>
        /// The roles a user can set for a line of <paramref name="medium"/> (generic parameter <see cref="RoleParam"/>), in menu
        /// order; empty when the medium itself says it all (heating flow, cold water, air kinds …) or has no roles (data).
        /// </summary>
        public static IReadOnlyList<(string Value, I18nText Label)> RolesFor(string? medium)
        {
            if (medium is null) return Array.Empty<(string, I18nText)>();
            if (medium == PortMedia.ElectricDc) return DcRoles.Concat(AcRoles.Where(r => r.Value == "PE")).ToList();
            if (IsElectricPower(medium)) return AcRoles.Concat(DcRoles).ToList();
            if (PortMedia.Covers(PortMedia.Fluid, medium)) return FluidRoles;
            if (PortMedia.Covers(PortMedia.Process, medium)) return ProcessRoles;
            return Array.Empty<(string, I18nText)>();
        }

        /// <summary>Label of the role choice of a medium ("Conductor", "Line role", "Fluid (DIN 2403)").</summary>
        public static I18nText RoleLabel(string medium)
            => IsElectricPower(medium) ? new("Conductor", "Leiter")
             : PortMedia.Covers(PortMedia.Process, medium) ? new("Fluid (DIN 2403)", "Stoff (DIN 2403)")
             : new("Line role", "Leitungsfunktion");

        /// <summary>
        /// Medium, role and scheme colour of a connection line; null for objects that are no connection line (boxes, annotation
        /// lines, lines without a medium). Role: the generic parameter <see cref="RoleParam"/>, else the type's
        /// <see cref="ObjectType.MediumRole"/>, else what the medium path tells.
        /// </summary>
        public static LineMediumInfo? Of(Registry registry, SketchDocument doc, SketchObject line, Lang lang = Lang.En)
        {
            if (!line.IsPointGeometry || registry.Type(line.Type) is not { } type) return null;
            var medium = Building.LineMedium(registry, doc, line, lang);
            if (medium is null) return null;
            var set = line.Str(RoleParam, "").Trim();
            var role = set.Length > 0 ? set : type.MediumRole(line) ?? DerivedRole(medium);
            return new LineMediumInfo(medium, role, Entry(ColourId(medium, role)));
        }

        /// <summary>The role the medium path itself gives (heating flow → "flow", data → "bus", DC → "dc" …), or null.</summary>
        public static string? DerivedRole(string medium)
        {
            if (medium == PortMedia.Data) return "bus";
            if (medium == PortMedia.Instrument) return "signal";
            if (medium == PortMedia.Pcb) return null;
            if (medium == PortMedia.ElectricDc) return "dc";
            if (IsElectricPower(medium)) return "power";
            if (medium == PortMedia.HeatingFlow) return "flow";
            if (medium == PortMedia.HeatingReturn) return "return";
            if (medium == PortMedia.HotWater) return "hot";
            if (medium == PortMedia.ColdWater) return "cold";
            if (medium == PortMedia.Circulation) return "circulation";
            if (medium == PortMedia.Waste) return "waste";
            if (medium == PortMedia.Water) return "water";
            if (medium == PortMedia.Heat) return "heat";
            if (medium == PortMedia.Cooling) return "cooling";
            if (medium == PortMedia.Gas || medium == PortMedia.Hydrogen) return "gas";
            if (medium == PortMedia.Refrigerant || medium == PortMedia.Vacuum || medium == PortMedia.Optical) return medium;
            if (medium.StartsWith(PortMedia.Air + ".", StringComparison.Ordinal)) return medium;
            return null;
        }

        /// <summary>The scheme id for a medium and role, or null (the line keeps its colours).</summary>
        public static string? ColourId(string medium, string? role)
        {
            if (role is null || role == OwnColour) return null;
            if (medium == PortMedia.Pcb) return null;
            if (IsElectricPower(medium) || medium == PortMedia.Data || medium == PortMedia.Instrument)
                return role is "L1" or "L2" or "L3" or "N" or "PE" or "PEN" or "M" or "L+" or "L-" or "dc" or "power" or "bus" or "signal" ? role : null;
            if (PortMedia.Covers(PortMedia.Fluid, medium)) return role is "pressure" or "tank" or "pilot" or "drain" ? role : null;
            if (PortMedia.Covers(PortMedia.Process, medium)) return ProcessRoles.Any(r => r.Value == role) ? "p." + role : null;
            return Entry(role) is not null ? role : null;
        }

        /// <summary>The scheme colour of a line when the colour mode is on in <paramref name="doc"/>, else null.</summary>
        public static MediumColour? ColourFor(Registry registry, SketchDocument doc, SketchObject o, Lang lang = Lang.En)
            => o.IsPointGeometry && doc.Settings.ColorBy == ByMedium ? Of(registry, doc, o, lang)?.Colour : null;

        /// <summary>The scheme entries used by the connection lines of <paramref name="doc"/> (all pages), in scheme order.</summary>
        public static IReadOnlyList<MediumColour> Used(Registry registry, SketchDocument doc, Lang lang = Lang.En)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in doc.Objects.Values)
                if (o.IsPointGeometry && Of(registry, doc, o, lang)?.Colour is { } c) ids.Add(c.Id);
            return Scheme.Where(c => ids.Contains(c.Id)).ToList();
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>
        /// <paramref name="local"/> recoloured to <paramref name="c"/>: every stroke (white highlights and "none" stay), fills
        /// that repeat a stroke colour or are black (junction dots) take the colour, other fills take its hue and keep their
        /// lightness (pale stays pale), coloured texts take it. With a stripe colour every stroked line gets a dashed copy in that
        /// colour on top (green-yellow).
        /// </summary>
        public static IReadOnlyList<Primitive> Apply(IReadOnlyList<Primitive> local, MediumColour c)
        {
            var list = new List<Primitive>(local.Count + 2);
            foreach (var p in local)
            {
                var q = Recolor(p, c.Color);
                list.Add(q);
                if (c.Stripe is { } stripe && q is PathPrim or LinePrim && StyleOf(q) is { } s && s.HasStroke && (s.Fill is null || s.Fill.Kind == FillKind.None) && !IsLight(s.Stroke))
                    list.Add(Prim.Restyle(q, x => x with { Stroke = stripe, Dash = DashStyle.Dash, ArrowStart = null, ArrowEnd = null }) with { Tag = "stripe" });
            }
            return list;
        }

        private static Style? StyleOf(Primitive p) => p switch
        {
            LinePrim l => l.Style,
            PathPrim pa => pa.Style,
            EllipsePrim e => e.Style,
            RectPrim r => r.Style,
            _ => null,
        };

        private static Primitive Recolor(Primitive p, string color)
        {
            switch (p)
            {
                case TextPrim t:
                    return t.TextStyle.Color is { } tc && !IsDark(tc) && !IsLight(tc) ? t with { TextStyle = t.TextStyle with { Color = color } } : t;
                case GroupPrim g:
                    return g with { Children = g.Children.Select(x => Recolor(x, color)).ToList() };
                default:
                    return Prim.Restyle(p, s =>
                    {
                        var stroke = s.Stroke;
                        var next = s;
                        if (s.HasStroke && !IsLight(stroke)) next = next with { Stroke = color };
                        if (s.Fill is { Kind: FillKind.Solid, Color: { } fc } && !IsLight(fc))
                        {
                            var same = string.Equals(fc, stroke ?? "#000000", StringComparison.OrdinalIgnoreCase) || IsDark(fc);
                            next = next with { Fill = s.Fill with { Color = same ? color : Accents.Recolor(fc, color) } };
                        }
                        return next;
                    });
            }
        }

        private static double Luma(string c)
        {
            var h = c.Trim().TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(x => new string(x, 2)));
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v)) return -1;
            return (0.299 * ((v >> 16) & 255) + 0.587 * ((v >> 8) & 255) + 0.114 * (v & 255)) / 255;
        }

        private static bool IsLight(string? c) => c is not null && Luma(c) > 0.92;

        private static bool IsDark(string c) { var l = Luma(c); return l >= 0 && l < 0.08; }

        // ------------------------------------------------------------------ legend

        /// <summary>
        /// A legend of <paramref name="entries"/> as plain annotation elements ("ann.line" and "ann.text", when the annotation
        /// module is registered; else empty): a title, then per colour a short line in that colour (green-yellow: a dashed
        /// yellow line over the green one) and its name with the source, top left at <paramref name="at"/> (page mm). Hosts
        /// insert the objects as one unit; it is an ordinary drawing that stays as it is.
        /// </summary>
        public static IReadOnlyList<SketchObject> Legend(Registry registry, IReadOnlyList<MediumColour> entries, Vec2 at, Lang lang)
        {
            var lineItem = registry.AllItems.FirstOrDefault(i => i.TypeId == "ann.line");
            var textItem = registry.AllItems.FirstOrDefault(i => i.TypeId == "ann.text");
            if (lineItem is null || textItem is null || entries.Count == 0) return Array.Empty<SketchObject>();
            const double row = 5.0, lineLength = 10.0, textWidth = 62.0, textHeight = 4.5;
            var list = new List<SketchObject>();
            SketchObject Text(string text, double y, bool bold)
            {
                var t = registry.Create(textItem, new Vec2(at.X + textWidth / 2, y)) with { Text = text, Size = new Size2(textWidth, textHeight) };
                t = t.WithParam("align", "left");
                return bold ? t with { Style = (t.Style ?? new Style()) with { Bold = true } } : t;
            }
            list.Add(Text(new I18nText("Colours by medium", "Farben nach Medium").Get(lang), at.Y, true));
            for (var i = 0; i < entries.Count; i++)
            {
                var c = entries[i];
                var y = at.Y + (i + 1) * row;
                var a = new Vec2(at.X, y);
                var b = new Vec2(at.X + lineLength, y);
                list.Add(registry.CreateBetween(lineItem, a, b) with { Style = new Style { Stroke = c.Color, StrokeWidth = 0.7 } });
                if (c.Stripe is { } stripe)
                    list.Add(registry.CreateBetween(lineItem, a, b) with { Style = new Style { Stroke = stripe, StrokeWidth = 0.7, Dash = DashStyle.Dash } });
                var name = c.Name.Get(lang) + (c.Source.En == "Convention" ? "" : " (" + c.Source.Get(lang) + ")");
                var t = Text(name, y, false);
                list.Add(t with { Transform = t.Transform with { X = at.X + lineLength + 2 + textWidth / 2 } });
            }
            return list;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// The generic role choice of a connection line ("Conductor ▸ L1 / L2 / L3 / N / PE / PEN / L+ / L− / M", "Line role ▸
        /// pressure / return", "Fluid (DIN 2403) ▸ …"), a dropdown (<see cref="ActionChoices"/>); "Not set" clears it. Nothing for
        /// lines whose medium says it all or whose type shows its real conductor colour already.
        /// </summary>
        public static IEnumerable<ContextAction> Actions(Registry registry, SketchObject o, RenderContext ctx)
        {
            if (!o.IsPointGeometry || registry.Type(o.Type) is not { } type || type.MediumRole(o) == OwnColour) return Array.Empty<ContextAction>();
            var medium = Building.LineMedium(registry, ctx.Doc, o, ctx.Lang);
            var roles = RolesFor(medium);
            if (medium is null || roles.Count == 0) return Array.Empty<ContextAction>();
            var values = new List<(string Value, I18nText Label, string? Icon)> { ("", new I18nText("Not set", "Nicht festgelegt"), null) };
            values.AddRange(roles.Select(r => (r.Value, r.Label, (string?)null)));
            return ActionChoices.FromValues("line.role", RoleLabel(medium), RoleParam, o.Str(RoleParam, "").Trim(), values);
        }
    }
}
