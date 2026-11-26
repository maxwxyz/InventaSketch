using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Element values of the robotics module (engineering calculations, section 1): the mass m of tools, sensors, tool
    /// changers and workpieces; the rated payload m_max, the load m_L and the reach R of robots. m_L is the sum of the masses
    /// entered on the tool chain docked at the robot's flange (flange → tool changer → sensor → gripper …) and on the
    /// workpiece docked at the chain's TCP; R is derived from the link lengths and joint limits (the largest horizontal
    /// distance of the reach envelope from the base axis) unless entered. Checks: <c>robot.payload</c> (m_L above m_max) and
    /// <c>robot.reach</c> (a workpiece or target next to the robot outside its reach envelope). Every value is optional; the
    /// payload is a datasheet value and has no default.
    /// </summary>
    internal static class RoboValues
    {
        /// <summary>Id of the tool-side mount of tool changers and sensors (a gripper's "flange" docks there).</summary>
        public const string ToolMount = "tool";

        /// <summary>Distance (page mm) within which two mounts or ports count as docked.</summary>
        public const double DockTol = 0.5;

        public const string WorkpieceId = "robot.cell.workpiece";
        public const string TcpMarkerId = "robot.frame.tcp";

        private static I18nText T(string en, string de) => new(en, de);

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, string symbol, QuantityKind kind, string unit, double max)
            => new(key, name, T(symbol, symbol), kind) { Unit = unit, Param = key, Relevance = VarRelevance.Card, Min = 0, Max = max };

        // ------------------------------------------------------------------ parts (tools, sensors, workpieces)

        private static readonly I18nText MassName = T("Mass", "Masse");

        public static readonly IReadOnlyList<VarDef> MassVars = new[] { V("mass", MassName, "m", Quantities.Mass_, "kg", 1e5) };

        public static readonly IReadOnlyList<ParamDef> MassParams = new[] { Text("mass", MassName, "kg"), ValueLabels.ShowValues };

        // ------------------------------------------------------------------ robots

        private static readonly I18nText PayloadName = T("Rated payload (datasheet)", "Nenntraglast (Datenblatt)");
        private static readonly I18nText LoadName = T("Load: tool chain and workpiece (docked parts with a mass)", "Last: Werkzeugkette und Werkstück (angedockte Teile mit Masse)");
        private static readonly I18nText ReachName = T("Reach (largest horizontal distance of the reach envelope from the base axis)", "Reichweite (größter waagerechter Abstand des Arbeitsraums von der Grundachse)");
        private static readonly I18nText MomentName = T("Static load moment at the flange (m_L·g·d)", "Statisches Lastmoment am Flansch (m_L·g·d)");
        private static readonly I18nText MomentMaxName = T("Permissible moment at the flange (datasheet)", "Zulässiges Moment am Flansch (Datenblatt)");
        private static readonly I18nText CogName = T("Centre-of-gravity distance of the load from the flange", "Schwerpunktabstand der Last vom Flansch");

        /// <summary>Standard gravity g (m/s²), the conventional value of the SI brochure [S1].</summary>
        public const double G = 9.80665;

        private static VarDef Flange(string axis) =>
            new("f" + axis, T("Flange position " + axis + " (forward kinematics, base frame)", "Flanschposition " + axis + " (Vorwärtskinematik, Basiskoordinaten)"), T(axis + "_F", axis + "_F"), Quantities.Length)
            {
                Unit = "mm", Relevance = VarRelevance.Panel, ReadOnly = true, Min = -1e3, Max = 1e3,
            };

        public static readonly IReadOnlyList<VarDef> ArmVars = new[]
        {
            V("payload", PayloadName, "m_max", Quantities.Mass_, "kg", 1e5),
            V("load", LoadName, "m_L", Quantities.Mass_, "kg", 1e5),
            V("reach", ReachName, "R", Quantities.Length, "mm", 1e3),
            V("moment", MomentName, "M_L", Quantities.Torque, "N·m", 1e7),
            V("momentMax", MomentMaxName, "M_max", Quantities.Torque, "N·m", 1e7) with { Relevance = VarRelevance.Panel },
            V("cog", CogName, "d", Quantities.Length, "mm", 100) with { Relevance = VarRelevance.Panel },
            Flange("x"), Flange("y"), Flange("z"),
        };

        /// <summary>The robot values with only <paramref name="card"/> left on the card (types that add card values of their own).</summary>
        public static IReadOnlyList<VarDef> ArmVarsWithCard(params string[] card)
            => ArmVars.Select(v => v.Relevance == VarRelevance.Card && !card.Contains(v.Key) ? v with { Relevance = VarRelevance.Panel } : v).ToList();

        public static readonly IReadOnlyList<ParamDef> ArmParams = new[]
        {
            Text("payload", PayloadName, "kg"), Text("load", LoadName, "kg"), Text("reach", ReachName, "mm"),
            Text("moment", MomentName, "N·m"), Text("momentMax", MomentMaxName, "N·m"), Text("cog", CogName, "mm"), ValueLabels.ShowValues,
        };

        /// <summary>M_L = m_L·g·d: the static moment of the load at the flange [S1].</summary>
        private static readonly Relation Moment =
            new Relation("robot.moment", T("Static load moment at the flange", "Statisches Lastmoment am Flansch"), "{moment} = {load}·g·{cog}", "moment", "load", "cog")
            {
                Source = "S1",
                Validity = T("load at rest, centre of gravity d from the flange across the tool axis (worst case: horizontal lever arm)", "Last in Ruhe, Schwerpunkt im Abstand d vom Flansch quer zur Werkzeugachse (ungünstigster Fall: waagerechter Hebelarm)"),
            }
                .Solve("moment", "{load}·g·{cog}", v => v("load") * G * v("cog"))
                .Solve("cog", "{moment}/({load}·g)", v => v("moment") / (v("load") * G))
                .Solve("load", "{moment}/(g·{cog})", v => v("moment") / (G * v("cog")));

        public static readonly IReadOnlyList<Relation> ArmRelations = new[] { Moment };

        private static bool Above(double? value, double? limit) => value is double v && limit is double l && v > l * (1 + 1e-9);

        /// <summary>robot.payload: the docked tool chain and workpiece weigh more than the robot's rated payload.</summary>
        public static readonly ValueCheck PayloadExceeded = new("robot.payload", CheckSeverity.Warning,
            T("The mass of the docked tool chain and workpiece m_L is above the rated payload m_max of the robot. Choose a robot with a higher payload or lighter tooling (the datasheet's load diagram also limits the centre-of-gravity offset).",
              "Die Masse der angedockten Werkzeugkette mit Werkstück m_L liegt über der Nenntraglast m_max des Roboters. Einen Roboter mit höherer Traglast oder leichtere Werkzeuge wählen (das Traglastdiagramm im Datenblatt begrenzt zusätzlich den Schwerpunktabstand)."),
            "m_L ≤ m_max", c => Above(c["load"], c["payload"]));

        /// <summary>robot.reach: a workpiece or TCP target assigned to this robot lies outside its reach envelope.</summary>
        public static readonly ValueCheck OutOfReach = new("robot.reach", CheckSeverity.Warning,
            T("A workpiece or target next to this robot lies outside its reach envelope (as drawn, plus the wrist and tool length). Move the robot or the target, or check the link lengths and joint limits.",
              "Ein Werkstück oder Zielpunkt bei diesem Roboter liegt außerhalb seines Arbeitsraums (wie gezeichnet, zuzüglich Hand- und Werkzeuglänge). Roboter oder Ziel verschieben oder die Gliedlängen und Achsgrenzen prüfen."),
            "target ∈ reach envelope", c => c.Context.Registry is { } r && r.Type(c.Object.Type) is RobotArmType arm && arm.TargetOutOfReach(c.Object, c.Context));

        /// <summary>robot.moment: the static moment of the load at the flange is above the permissible moment.</summary>
        public static readonly ValueCheck MomentExceeded = new("robot.moment", CheckSeverity.Warning,
            T("The static moment of the load at the flange M_L = m_L·g·d is above the permissible moment M_max of the robot. Bring the centre of gravity closer to the flange, use lighter tooling or a larger robot (dynamic moments in motion come on top).",
              "Das statische Lastmoment am Flansch M_L = m_L·g·d liegt über dem zulässigen Moment M_max des Roboters. Den Schwerpunkt näher an den Flansch bringen, leichtere Werkzeuge oder einen größeren Roboter wählen (dynamische Momente in der Bewegung kommen hinzu)."),
            "M_L ≤ M_max", c => Above(c["moment"], c["momentMax"])) { Source = "S1" };

        public static readonly IReadOnlyList<ValueCheck> ArmChecks = new[] { PayloadExceeded, OutOfReach, MomentExceeded };

        /// <summary>s = a1 + … + an (the masses of the docked parts).</summary>
        private static Relation Sum(int n)
        {
            var keys = Enumerable.Range(1, n).Select(i => "a" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
            var expr = string.Join(" + ", keys.Select(k => "{" + k + "}"));
            return new Relation("robot.load", T("Mass of the docked tool chain and workpiece", "Masse der angedockten Werkzeugkette mit Werkstück"), "{s} = " + expr, new[] { "s" }.Concat(keys).ToArray())
                .Solve("s", expr, v => keys.Sum(k => v(k)));
        }

        /// <summary>The reach derived from the geometry: x = R (SI), shown with its millimetre value.</summary>
        private static Relation ReachConstant(double mm)
        {
            var text = mm.ToString("0", CultureInfo.InvariantCulture) + " mm";
            return new Relation("robot.reach.geometry", T("Reach from the link lengths and joint limits", "Reichweite aus den Gliedlängen und Achsgrenzen"), "{x} = " + text, "x")
                .Solve("x", text, _ => mm / 1000);
        }

        /// <summary>A flange coordinate from forward kinematics: x = value (SI), shown with its millimetre value.</summary>
        private static Relation FlangeConstant(string axis, double mm)
        {
            var text = mm.ToString("0.#", CultureInfo.InvariantCulture) + " mm";
            return new Relation("robot.flange." + axis, T("Flange position from the joint values (forward kinematics)", "Flanschposition aus den Gelenkwerten (Vorwärtskinematik)"), "{x} = " + text, "x")
                .Solve("x", text, _ => mm / 1000);
        }

        public static bool HasMass(SketchObject o) => ValueModel.ReadEntered(o, MassVars[0]).Value is not null;

        /// <summary>Links of a robot: the load from the masses on its docked chain, the reach from its geometry (unless entered).</summary>
        public static IEnumerable<BoundRelation> ArmLinks(RobotArmType type, SketchObject o, RenderContext ctx)
        {
            if (ctx.Registry is { } reg)
            {
                var chain = Chain(reg, ctx.Doc, o, ctx);
                var parts = chain.Parts.Concat(chain.Workpiece is null ? Array.Empty<SketchObject>() : new[] { chain.Workpiece }).Where(HasMass).ToList();
                if (parts.Count > 0)
                {
                    var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = new VarRef(o.Id, "load") };
                    for (var i = 0; i < parts.Count; i++) map["a" + (i + 1).ToString(CultureInfo.InvariantCulture)] = new VarRef(parts[i].Id, "mass");
                    yield return new BoundRelation(Sum(parts.Count), map);
                }
            }
            if (ValueModel.ReadEntered(o, ArmVars[2]).Value is null && type.ReachOf(o) is double r && r > 0)
                yield return new BoundRelation(ReachConstant(r), new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["x"] = new VarRef(o.Id, "reach") });
            if (type.FlangeValues && type.Pose(o, type.Q(o)) is { Ok: true } pose)
            {
                var f = pose.Flange;
                foreach (var (axis, mm) in new[] { ("x", f.X), ("y", f.Y), ("z", f.Z) })
                    yield return new BoundRelation(FlangeConstant(axis, mm), new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["x"] = new VarRef(o.Id, "f" + axis) });
            }
        }

        // ------------------------------------------------------------------ the docked tool chain

        /// <summary>
        /// The tool chain docked at a robot's flange, in order (each part's mount "flange" on the previous output: the robot's
        /// flange, then the tool-side mount of a tool changer or sensor), the TCP of its last part (page) and the workpiece
        /// whose port "grip" sits on that TCP.
        /// </summary>
        internal sealed record ToolChain(IReadOnlyList<SketchObject> Parts, Vec2? Tcp, SketchObject? Workpiece);

        private static Anchor? Find(Registry reg, SketchObject o, RenderContext ctx, AnchorRole role, string id)
            => reg.PageAnchors(o, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == role && a.Id == id);

        private static IEnumerable<SketchObject> PageObjects(SketchDocument doc, string id)
            => doc.PageOf(id) is { } page ? doc.ObjectsOn(page) : doc.Objects.Values;

        public static ToolChain Chain(Registry reg, SketchDocument doc, SketchObject arm, RenderContext ctx)
        {
            var parts = new List<SketchObject>();
            var flange = Find(reg, arm, ctx, AnchorRole.Mount, "flange");
            if (flange is null) return new ToolChain(parts, null, null);
            var candidates = PageObjects(doc, arm.Id).Where(x => x.Id != arm.Id && !x.IsPointGeometry && reg.Type(x.Type) is RoboBoxType { ToolSide: true }).ToList();
            var at = flange.P;
            Vec2? tcp = flange.P;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var step = 0; step < 12; step++)
            {
                SketchObject? next = null;
                foreach (var x in candidates)
                {
                    if (seen.Contains(x.Id)) continue;
                    var m = Find(reg, x, ctx, AnchorRole.Mount, "flange");
                    if (m is not null && m.P.DistanceTo(at) <= DockTol) { next = x; break; }
                }
                if (next is null) break;
                seen.Add(next.Id);
                parts.Add(next);
                var output = Find(reg, next, ctx, AnchorRole.Mount, ToolMount);
                if (output is not null) { at = output.P; tcp = output.P; continue; }
                tcp = Find(reg, next, ctx, AnchorRole.Port, "tcp")?.P;
                break;
            }
            SketchObject? workpiece = null;
            if (tcp is Vec2 t && parts.Count > 0)
                foreach (var w in PageObjects(doc, arm.Id).Where(x => x.Type == WorkpieceId))
                    if (Find(reg, w, ctx, AnchorRole.Port, "grip") is { } g && g.P.DistanceTo(t) <= DockTol) { workpiece = w; break; }
            return new ToolChain(parts, tcp, workpiece);
        }
    }
}
