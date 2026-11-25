using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>
    /// Export helpers for figures: monochrome SVG per page (helper layers, helper lines and the sheet frame excluded),
    /// sheet frames, sheet numbers and figure numbering.
    /// </summary>
    public static class PatentExport
    {
        public const string HelperLayerId = "patent-helper";

        /// <summary>True if the object never goes into a monochrome figure export (sheet frame, helper or hidden layer).</summary>
        public static bool IsExcluded(SketchDocument doc, SketchObject o)
        {
            if (o.Type == PatentIds.Sheet) return true;
            if (o.Layer is null) return false;
            var layer = doc.Layers.FirstOrDefault(l => l.Id == o.Layer);
            return layer is not null && (layer.Helper || !layer.Visible);
        }

        /// <summary>Removes primitives drawn with the Helper line role (recursively).</summary>
        public static IReadOnlyList<Primitive> StripHelpers(IEnumerable<Primitive> prims)
        {
            var list = new List<Primitive>();
            foreach (var p in prims)
            {
                switch (p)
                {
                    case GroupPrim g:
                        var children = StripHelpers(g.Children);
                        if (children.Count > 0) list.Add(g with { Children = children });
                        break;
                    case LinePrim { Style.Role: LineRole.Helper }:
                    case PathPrim { Style.Role: LineRole.Helper }:
                    case EllipsePrim { Style.Role: LineRole.Helper }:
                    case RectPrim { Style.Role: LineRole.Helper }:
                        break;
                    default:
                        list.Add(p);
                        break;
                }
            }
            return list;
        }

        /// <summary>Monochrome SVG of one page for filing (no helpers, no sheet frame, profile font).</summary>
        public static string PageSvg(Registry registry, SketchDocument doc, Page page, PatentProfile? profile = null)
        {
            profile ??= PatentProfiles.Get(doc.Settings.Profile);
            var d = doc with { Settings = doc.Settings with { Profile = profile.Id } };
            var options = new SvgOptions { Monochrome = true, Background = "#ffffff", ObjectIds = false, FontFamily = profile.FontFamily };
            var ctx = new RenderContext(d, Lang.En, monochrome: true);
            string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"").Append(F(page.Size.W)).Append("mm\" height=\"").Append(F(page.Size.H))
              .Append("mm\" viewBox=\"0 0 ").Append(F(page.Size.W)).Append(' ').Append(F(page.Size.H)).Append("\">\n");
            sb.Append("<rect x=\"0\" y=\"0\" width=\"").Append(F(page.Size.W)).Append("\" height=\"").Append(F(page.Size.H)).Append("\" fill=\"#ffffff\"/>\n");
            foreach (var r in registry.RenderPage(d, page, ctx, o => !IsExcluded(d, o)))
            {
                var prims = StripHelpers(r.Primitives);
                if (prims.Count == 0) continue;
                sb.Append("<g>\n");
                foreach (var p in prims) SvgRenderer.Write(sb, p, options, "  ");
                sb.Append("</g>\n");
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        /// <summary>Monochrome SVG for every page in order.</summary>
        public static IReadOnlyList<string> AllPagesSvg(Registry registry, SketchDocument doc, PatentProfile? profile = null)
            => doc.Pages.Select(p => PageSvg(registry, doc, p, profile)).ToList();

        /// <summary>Activates a profile and sets every page to the profile's default sheet (portrait).</summary>
        public static SketchDocument ApplyProfile(SketchDocument doc, PatentProfile profile)
        {
            var sheet = profile.Sheets[0].Size;
            return doc with
            {
                Settings = doc.Settings with { Profile = profile.Id },
                Pages = doc.Pages.Select(p => p with { Size = sheet }).ToList(),
            };
        }

        /// <summary>Adds the helper layer (excluded from export and preflight) if missing.</summary>
        public static SketchDocument EnsureHelperLayer(SketchDocument doc)
            => doc.Layers.Any(l => l.Id == HelperLayerId) ? doc
                : doc with { Layers = doc.Layers.Concat(new[] { new Layer(HelperLayerId, "Drawing helper", Visible: true, Locked: true, Helper: true) }).ToList() };

        /// <summary>Adds a locked sheet frame (margins of the profile) to a page on the helper layer.</summary>
        public static SketchDocument AddSheetFrame(Registry registry, SketchDocument doc, string pageId, PatentProfile profile)
        {
            doc = EnsureHelperLayer(doc);
            var page = doc.Pages.First(p => p.Id == pageId);
            var type = registry.Type(PatentIds.Sheet) ?? throw new InvalidOperationException("Figures module is not registered.");
            var o = type.Create(new Vec2(page.Size.W / 2, page.Size.H / 2)) with { Size = page.Size, Layer = HelperLayerId, Locked = true };
            o = o.WithParam("profile", profile.Id);
            doc = Edits.Add(doc, pageId, o);
            return Edits.Reorder(doc, pageId, new[] { o.Id }, -int.MaxValue);
        }

        /// <summary>Ensures each page has one automatic sheet number at the top centre inside the usable area.</summary>
        public static SketchDocument ApplySheetNumbers(Registry registry, SketchDocument doc, PatentProfile profile)
        {
            var type = registry.Type(PatentIds.PageNumber) ?? throw new InvalidOperationException("Figures module is not registered.");
            foreach (var page in doc.Pages.ToList())
            {
                var usable = profile.Usable(page.Size);
                var at = new Vec2(usable.Center.X, usable.Y + 5);
                var existing = doc.ObjectsOn(page).FirstOrDefault(o => o.Type == PatentIds.PageNumber);
                if (existing is null)
                    doc = Edits.Add(doc, page.Id, type.Create(at));
                else
                    doc = Edits.Update(doc, new[] { existing.Id }, o => o.WithParam("auto", true) with { Transform = o.Transform with { X = at.X, Y = at.Y } });
            }
            return doc;
        }

        /// <summary>
        /// Numbers figure labels (ann.figlabel) consecutively in reading order (pages, then top-to-bottom,
        /// left-to-right) with the profile prefix ("FIG. 1"). Page.Figure is set when a page holds one figure.
        /// With a single figure and <paramref name="removeSingleLabel"/>, the label is removed where the profile requires it.
        /// </summary>
        public static SketchDocument NumberFigures(SketchDocument doc, PatentProfile profile, bool removeSingleLabel = false)
        {
            var labels = new List<(Page Page, SketchObject Obj)>();
            foreach (var page in doc.Pages)
                labels.AddRange(doc.ObjectsOn(page).Where(o => o.Type == "ann.figlabel" && !IsExcluded(doc, o))
                    .OrderBy(o => o.Transform.Y).ThenBy(o => o.Transform.X).Select(o => (page, o)));
            if (labels.Count == 1 && removeSingleLabel && profile.SingleFigureUnlabelled)
            {
                doc = Edits.Remove(doc, new[] { labels[0].Obj.Id });
                return doc with { Pages = doc.Pages.Select(p => p with { Figure = null }).ToList() };
            }
            var n = 0;
            var figures = new Dictionary<string, List<string>>();
            foreach (var (page, obj) in labels)
            {
                var text = profile.FigPrefix + " " + (++n).ToString(CultureInfo.InvariantCulture);
                doc = Edits.Update(doc, new[] { obj.Id }, o => o.WithParam("label", text) with { Text = null });
                if (!figures.TryGetValue(page.Id, out var l)) figures[page.Id] = l = new List<string>();
                l.Add(text);
            }
            return doc with { Pages = doc.Pages.Select(p => p with { Figure = figures.TryGetValue(p.Id, out var l) && l.Count == 1 ? l[0] : p.Figure }).ToList() };
        }
    }
}
