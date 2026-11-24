using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// View ids that mean the same drawing in different domains, so a mixed selection can switch together: a wall's
    /// <c>elevation</c> and an installation's <c>section</c> both look at the plan from its lower page edge at the real
    /// heights. "All in view: Section" switches the installations to <c>section</c> and the walls, doors and windows to
    /// <c>elevation</c>.
    /// </summary>
    public static class ViewAliases
    {
        private static readonly string[][] Groups =
        {
            new[] { "section", "elevation" },
        };

        /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are the same view id or aliases of each other.</summary>
        public static bool Same(string? a, string? b)
        {
            if (a is null || b is null) return false;
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;
            return Groups.Any(g => g.Contains(a, StringComparer.Ordinal) && g.Contains(b, StringComparer.Ordinal));
        }

        /// <summary>
        /// The view of <paramref name="type"/> that shows <paramref name="view"/>: the view itself, else an alias the type
        /// offers; null if the type has neither.
        /// </summary>
        public static string? Resolve(ObjectType? type, string? view)
        {
            if (type is null || view is null) return null;
            if (type.Views.Any(v => v.Id == view)) return view;
            return type.Views.Select(v => v.Id).FirstOrDefault(id => Same(id, view));
        }

        /// <summary>True if <paramref name="type"/> offers <paramref name="view"/> or an alias of it.</summary>
        public static bool Offers(ObjectType? type, string view) => Resolve(type, view) is not null;
    }
}
