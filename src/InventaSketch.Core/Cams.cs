using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Cam discs as parts (<see cref="ObjectType.CamContour"/>): turning a cam (its rotation) moves the followers riding on
    /// it. A follower is a slider (<see cref="LinkageRole.Slider"/>) whose guide runs towards the cam and whose joint sits
    /// on the cam contour – offset by the roller radius for a roller follower, the contour's extreme along the guide for a
    /// flat-faced one, the contour itself for a knife edge (<see cref="ObjectType.CamFollower"/>). The follower's joint goes
    /// to the new contact on its guide, and a self-built chain docked on it follows (<see cref="Linkage.Drag"/>).
    /// </summary>
    public static class Cams
    {
        /// <summary>How far (mm) a follower's joint may be off the contact to count as riding on the cam.</summary>
        public const double ContactTolerance = 0.6;

        /// <summary>True if <paramref name="o"/> is a cam disc.</summary>
        public static bool IsCam(Registry registry, SketchObject o)
            => !o.IsPointGeometry && registry.Type(o.Type)?.CamContour(o) is { Count: >= 3 };

        /// <summary>The cam contour in page coordinates (closed polygon), or null.</summary>
        public static IReadOnlyList<Vec2>? PageContour(Registry registry, SketchObject cam)
        {
            var local = registry.Type(cam.Type)?.CamContour(cam);
            if (local is null || local.Count < 3) return null;
            var m = cam.Matrix();
            return local.Select(m.Apply).ToList();
        }

        /// <summary>A follower of a cam: the slider, its guide and the joint position.</summary>
        public sealed record Follower(string SliderId, Vec2 Joint, Vec2 GuideA, Vec2 GuideB);

        /// <summary>Sliders riding on <paramref name="camId"/>: guide line through the cam and the joint at the contact.</summary>
        public static IReadOnlyList<Follower> Followers(Registry registry, SketchDocument doc, string camId, Lang lang = Lang.En)
        {
            var list = new List<Follower>();
            if (doc.Get(camId) is not { } cam || PageContour(registry, cam) is not { } contour) return list;
            var kind = registry.Type(cam.Type)!.CamFollower(cam);
            var ctx = new RenderContext(doc, lang);
            foreach (var s in doc.Objects.Values.Where(x => !x.IsPointGeometry && registry.Type(x.Type)?.LinkageRole(x) == LinkageRole.Slider))
            {
                var anchors = registry.PageAnchors(s, ctx).ToList();
                var guide = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Axis);
                var joint = anchors.FirstOrDefault(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port);
                if (guide is null || joint is null) continue;
                var at = Contact(contour, kind, guide.P, guide.Q, joint.P);
                if (at is Vec2 c && c.DistanceTo(joint.P) <= ContactTolerance) list.Add(new Follower(s.Id, joint.P, guide.P, guide.Q));
            }
            return list;
        }

        /// <summary>
        /// Contact of a follower on the line <paramref name="a"/>–<paramref name="b"/> with <paramref name="contour"/>, on
        /// the side of <paramref name="near"/> (the follower stays on its side of the cam); null if the line misses the cam.
        /// </summary>
        public static Vec2? Contact(IReadOnlyList<Vec2> contour, (string Kind, double Radius) follower, Vec2 a, Vec2 b, Vec2 near)
        {
            var d = b - a;
            if (d.Length < 1e-9) return null;
            var u = d * (1 / d.Length);
            var centre = new Vec2(contour.Average(p => p.X), contour.Average(p => p.Y));
            var foot = a + u * (centre - a).Dot(u);
            // Side of the follower along the guide, seen from the cam.
            var side = (near - foot).Dot(u) >= 0 ? 1.0 : -1.0;
            var dir = u * side;
            var reach = contour.Max(p => p.DistanceTo(centre)) + follower.Radius + 1;
            if (follower.Kind == "flat")
            {
                // The face is square to the guide: it rests on the contour's extreme along the guide.
                var t = contour.Max(p => (p - foot).Dot(dir));
                return foot + dir * t;
            }
            var rr = follower.Kind == "roller" ? Math.Max(0, follower.Radius) : 0;
            bool Touches(double t)
            {
                var p = foot + dir * t;
                if (Geo.PointInPolygon(p, contour)) return true;
                if (rr <= 0) return false;
                var best = double.MaxValue;
                for (var i = 0; i < contour.Count; i++)
                    best = Math.Min(best, Geo.ClosestOnSegment(p, contour[i], contour[(i + 1) % contour.Count]).Point.DistanceTo(p));
                return best <= rr;
            }
            // From outside inwards: the first touching position, refined by bisection.
            const int Steps = 240;
            double outer = reach, step = reach / Steps;
            if (Touches(outer)) return null;
            for (var k = 1; k <= Steps; k++)
            {
                var t = reach - k * step;
                if (!Touches(t)) { outer = t; continue; }
                double lo = t, hi = outer;
                for (var i = 0; i < 40; i++)
                {
                    var mid = (lo + hi) / 2;
                    if (Touches(mid)) lo = mid; else hi = mid;
                }
                return foot + dir * hi;
            }
            return null;
        }

        /// <summary>
        /// The document with cam <paramref name="camId"/> turned by <paramref name="degrees"/> about its centre and every
        /// follower moved to its new contact (with the chain docked on it).
        /// </summary>
        public static SketchDocument Turn(Registry registry, SketchDocument doc, string camId, double degrees, Lang lang = Lang.En)
        {
            if (doc.Get(camId) is not { } cam || !IsCam(registry, cam)) return doc;
            var followers = Followers(registry, doc, camId, lang);
            var result = Edits.Update(doc, new[] { camId }, o => o with { Transform = o.Transform with { Rotation = Math.Round(o.Transform.Rotation + degrees, 6) } });
            var turned = result.Get(camId)!;
            var contour = PageContour(registry, turned)!;
            var kind = registry.Type(turned.Type)!.CamFollower(turned);
            foreach (var f in followers)
            {
                if (Contact(contour, kind, f.GuideA, f.GuideB, f.Joint) is not Vec2 to) continue;
                // A chain docked on the slider follows through the linkage solver; a lone slider just moves.
                var member = result.Objects.Values.FirstOrDefault(l => l.IsPointGeometry && l.Attach is not null && l.Attach.Any(x => x?.ObjectId == f.SliderId));
                if (member is not null)
                {
                    var index = member.Attach!.Select((x, i) => (x, i)).First(t => t.x?.ObjectId == f.SliderId).i;
                    result = Linkage.Drag(registry, result, member.Id, index, to, lang);
                }
                else
                {
                    var d = to - f.Joint;
                    result = Edits.Update(result, new[] { f.SliderId }, o => o with { Transform = o.Transform with { X = Math.Round(o.Transform.X + d.X, 6), Y = Math.Round(o.Transform.Y + d.Y, 6) } });
                }
            }
            return result;
        }
    }
}
