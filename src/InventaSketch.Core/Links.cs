using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>Kind of a link between two objects (<see cref="Links"/>).</summary>
    public enum LinkKind
    {
        /// <summary>Gears whose pitch circles touch (<see cref="Coupling"/>): they scale and push each other.</summary>
        Mesh,
        /// <summary>Shaft <see cref="Link.A"/> carries <see cref="Link.B"/> (hub on its axis).</summary>
        Carried,
        /// <summary>Concentric parts (gear and shaft in axial view): they move together.</summary>
        Bonded,
        /// <summary>Point <see cref="Link.Index"/> of line <see cref="Link.A"/> docked to <see cref="Link.B"/> (<see cref="Attachment"/>).</summary>
        Attached,
        /// <summary>Belt or chain <see cref="Link.A"/> running over pulley or sprocket <see cref="Link.B"/> (<see cref="ObjectType.SpansHubs"/>).</summary>
        Spanned,
    }

    /// <summary>
    /// A place where two objects touch or are docked, at page point <see cref="At"/>. <see cref="Released"/>: the objects
    /// touch but do not follow each other (a released mechanical pair, or a line end lying on an anchor without being
    /// docked). Hosts draw it as a closed or open lock marker; a click toggles it (<see cref="Links.Toggle"/>).
    /// </summary>
    public sealed record Link(LinkKind Kind, string A, string B, Vec2 At, bool Released)
    {
        /// <summary>Point index of the line end (<see cref="LinkKind.Attached"/>), else -1.</summary>
        public int Index { get; init; } = -1;
        /// <summary>Anchor the free line end lies on (<see cref="LinkKind.Attached"/>, released): docked to on toggle.</summary>
        public Anchor? Target { get; init; }

        /// <summary>The other object of the link, seen from <paramref name="id"/>.</summary>
        public string Other(string id) => id == A ? B : A;
    }

    /// <summary>
    /// Links between objects for the "what is connected" overlay: mechanical coupling (<see cref="Coupling"/>: mesh,
    /// carried, bonded) and docked line ends (<see cref="Attachment"/>), with the place where they touch. Everything that
    /// snaps together is linked; the user can release a link (and couple it again) with one click on its marker. A released
    /// mechanical pair is stored in <see cref="SketchObject.Released"/> of both objects and forgotten once they no longer
    /// touch, so parts snapped together again are coupled again.
    /// </summary>
    public static class Links
    {
        /// <summary>Distance (mm) within which a free line end counts as lying on an anchor.</summary>
        private const double Touch = 0.3;

        /// <summary>
        /// Links where one side is in <paramref name="ids"/>: mechanical links, docked line ends and free line ends of
        /// these objects (or ending on them) that lie on an anchor.
        /// </summary>
        public static IReadOnlyList<Link> For(Registry registry, SketchDocument doc, IEnumerable<string> ids, Lang lang = Lang.En)
        {
            var set = new HashSet<string>(ids, StringComparer.Ordinal);
            var list = Coupling.MechanicalLinks(registry, doc, set, lang).ToList();
            var ctx = new RenderContext(doc, lang);
            List<(SketchObject Owner, Anchor A)>? anchors = null;
            foreach (var o in doc.Objects.Values.Where(o => o.IsPointGeometry && o.Points is { Count: > 1 }))
            {
                var pts = doc.ResolvePoints(o);
                for (var i = 0; i < pts.Count; i++)
                {
                    var a = o.Attach is not null && i < o.Attach.Count ? o.Attach[i] : null;
                    if (a is not null)
                    {
                        if (doc.Get(a.ObjectId) is not null && (set.Contains(o.Id) || set.Contains(a.ObjectId)))
                            list.Add(new Link(LinkKind.Attached, o.Id, a.ObjectId, pts[i], false) { Index = i });
                        continue;
                    }
                    if (i != 0 && i != pts.Count - 1) continue;
                    anchors ??= doc.Objects.Values.Where(x => registry.Type(x.Type) is not null)
                        .SelectMany(x => registry.PageAnchors(x, ctx).Where(Dockable).Select(y => (x, y))).ToList();
                    var hit = anchors.Where(t => t.Owner.Id != o.Id && (set.Contains(o.Id) || set.Contains(t.Owner.Id)))
                        .Select(t => (t.Owner, t.A, D: Distance(t.A, pts[i])))
                        .Where(t => t.D <= Touch).OrderBy(t => Math.Round(t.D, 3)).ThenBy(t => t.A.Id is null ? 1 : 0).FirstOrDefault();
                    if (hit.Owner is not null)
                        list.Add(new Link(LinkKind.Attached, o.Id, hit.Owner.Id, pts[i], true) { Index = i, Target = hit.A });
                }
            }
            return list;
        }

        /// <summary>Anchors a line end can dock to: named points (pins, ports, corners, edge midpoints …) and line rails.</summary>
        private static bool Dockable(Anchor a)
            => a.Kind == AnchorKind.Point || (a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail && a.Id is not null);

        private static double Distance(Anchor a, Vec2 p)
            => a.Kind == AnchorKind.Point ? a.P.DistanceTo(p) : Geo.ClosestOnSegment(p, a.P, a.Q).Point.DistanceTo(p);

        /// <summary>
        /// Releases a coupled link or couples a released one: mechanical pairs gain or lose their entry in
        /// <see cref="SketchObject.Released"/> (on the objects present in <paramref name="doc"/>), docked line ends are
        /// undocked (the point stays where it is), free line ends on an anchor are docked to it. Returns the same instance if
        /// nothing applies (an object is not in this document).
        /// </summary>
        public static SketchDocument Toggle(SketchDocument doc, Link link)
        {
            if (link.Kind != LinkKind.Attached)
            {
                var ids = new[] { link.A, link.B }.Where(doc.Objects.ContainsKey).ToList();
                if (ids.Count == 0) return doc;
                return Edits.Update(doc, ids, o =>
                {
                    var other = o.Id == link.A ? link.B : link.A;
                    var rel = (o.Released ?? Array.Empty<string>()).Where(x => x != other).ToList();
                    if (!link.Released) rel.Add(other);
                    return o with { Released = rel.Count > 0 ? rel : null };
                });
            }
            var line = doc.Get(link.A);
            if (line?.Points is null || link.Index < 0 || link.Index >= line.Points.Count) return doc;
            var at = doc.ResolvePoints(line)[link.Index];
            if (!link.Released) return Edits.SetPoint(doc, line.Id, link.Index, at);
            var target = doc.Get(link.B);
            if (target is null || link.Target is null) return doc;
            var p = link.Target.Kind == AnchorKind.Point ? link.Target.P : Geo.ClosestOnSegment(at, link.Target.P, link.Target.Q).Point;
            var att = Attachments.For(target, p, link.Target);
            return att is null ? doc : Edits.SetPoint(doc, line.Id, link.Index, p, att);
        }

        /// <summary>Gear ratio i = n_self / n_other of two meshing gears (pitch radius of the other over the own).</summary>
        private static double? Ratio(Registry registry, SketchDocument doc, string self, string other)
        {
            var ctx = new RenderContext(doc, Lang.En);
            double? R(string id) => doc.Get(id) is { } o
                ? registry.PageAnchors(o, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner)?.Radius
                : null;
            return R(self) is double a && R(other) is double b && a > 1e-9 ? b / a : null;
        }

        /// <summary>Tooltip of a link marker, seen from the selected object <paramref name="self"/>: what it is and what a click does.</summary>
        public static string Describe(Registry registry, SketchDocument doc, Link link, string self, Lang lang = Lang.En)
        {
            string NameOf(string id) => doc.Get(id) is { } o && registry.Type(o.Type) is { } t ? t.Name.Get(lang) : id;
            var other = NameOf(link.Other(self));
            var de = lang == Lang.De;
            var what = link.Kind switch
            {
                LinkKind.Mesh => (de ? "Eingriff mit " : "Meshes with ") + other,
                LinkKind.Carried when self == link.A => (de ? "Trägt " : "Carries ") + other,
                LinkKind.Carried => (de ? "Sitzt auf " : "Sits on ") + other,
                LinkKind.Bonded => (de ? "Fest mit " : "Fixed to ") + other,
                LinkKind.Spanned when self == link.A => (de ? "Läuft über " : "Runs over ") + other,
                LinkKind.Spanned => other + (de ? " läuft hierüber" : " runs over this"),
                _ when self == link.A => (de ? "Linienende an " : "Line end at ") + other,
                _ => other + (de ? " an diesem Punkt" : " at this point"),
            };
            if (link.Kind == LinkKind.Mesh && Ratio(registry, doc, self, link.Other(self)) is double i)
                what += " (i = " + i.ToString("0.##", de ? System.Globalization.CultureInfo.GetCultureInfo("de-DE") : System.Globalization.CultureInfo.InvariantCulture) + ")";
            var state = link.Kind == LinkKind.Attached
                ? link.Released ? (de ? "frei – Klick dockt an" : "free – click docks it") : (de ? "angedockt – Klick löst" : "docked – click releases")
                : link.Released ? (de ? "gelöst – Klick koppelt" : "released – click couples") : (de ? "gekoppelt – Klick löst" : "coupled – click releases");
            return what + ": " + state;
        }
    }
}
