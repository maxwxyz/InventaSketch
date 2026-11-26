namespace InventaSketch.Core
{
    /// <summary>
    /// Short label of a snap target for hosts to show at the target while dragging ("pitch circle", "axis", "hub" …), so
    /// the user sees what the part will dock onto before releasing.
    /// </summary>
    public static class SnapLabel
    {
        public static string? For(Anchor? target, Lang lang)
        {
            if (target is null) return null;
            var de = lang == Lang.De;
            var name = target.Role switch
            {
                AnchorRole.Pitch => de ? "Teilkreis" : "pitch circle",
                AnchorRole.PitchInner => de ? "Teilkreis innen" : "inner pitch circle",
                AnchorRole.PitchLine => de ? "Wälzgerade" : "pitch line",
                AnchorRole.Axis => de ? "Achse" : "axis",
                AnchorRole.Hub => de ? "Nabe" : "hub",
                AnchorRole.Mount => de ? "Befestigung" : "mount",
                AnchorRole.Port => de ? "Anschluss" : "port",
                AnchorRole.Rail => de ? "auf der Linie" : "on the line",
                AnchorRole.Apex => de ? "Kegelspitze" : "cone apex",
                AnchorRole.Corner => de ? "Ecke" : "corner",
                AnchorRole.EdgeMid => de ? "Kantenmitte" : "edge centre",
                AnchorRole.Center => de ? "Mitte" : "centre",
                AnchorRole.End => de ? "Ende" : "end",
                AnchorRole.Mid => de ? "Mitte" : "midpoint",
                AnchorRole.Quadrant => de ? "Quadrant" : "quadrant",
                _ => null,
            };
            if (name is null) return null;
            // Numbered pins and joints say which one ("pin 2", "Gelenk 1").
            var id = target.Id;
            if (id is not null && id.Length > 3 && id.StartsWith("pin", System.StringComparison.Ordinal) && int.TryParse(id.Substring(3), out var pin))
                return (de ? "Bolzen " : "pin ") + (pin + 1);
            if (id is not null && id.Length > 1 && id[0] == 'j' && target.Role == AnchorRole.Port && int.TryParse(id.Substring(1), out var joint))
                return (de ? "Gelenk " : "joint ") + (joint + 1);
            return name;
        }
    }
}
