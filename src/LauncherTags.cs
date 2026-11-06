// Spent-stage names, done with KSP's own per-part vessel naming.
//
// Any part can carry a vessel name, type and priority (stock "Rename Vessel").
// When a craft splits, KSP names each new piece after its highest-priority named
// part. So a launcher picked from the library gets every part named after it,
// at a low priority, as debris: "Cypress 010 Booster", "Cypress 010 Stage 1".
// The payload gets the ship's name at a higher priority, so the launched craft
// and the payload keep their own name. Everything stays stock data in the craft
// file; nothing is renamed or retyped in flight by this mod.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PhilsVesselNaming
{
    static class LauncherTags
    {
        public const int LauncherPriority = -1, PayloadPriority = 1;

        static readonly Regex Label = new Regex(@"^(.*?) (Stage \d+|Booster)$");

        public static bool IsLauncherPart(Part p) =>
            p != null && p.vesselNaming != null && p.vesselNaming.namingPriority == LauncherPriority &&
            p.vesselNaming.vesselType == VesselType.Debris && !string.IsNullOrEmpty(p.vesselNaming.vesselName);

        // The launcher a tagged part came from: "Cypress 010 Stage 1" -> "Cypress 010".
        public static string LauncherOf(Part p)
        {
            if (!IsLauncherPart(p)) return null;
            string n = p.vesselNaming.vesselName;
            Match m = Label.Match(n);
            return m.Success ? m.Groups[1].Value : n;
        }

        // Names every part of a launcher just picked up from the library.
        public static void Tag(Part launcherRoot, string launcherName)
        {
            foreach (Part p in Subtree(launcherRoot))
                p.vesselNaming = new VesselNaming { vesselName = launcherName, vesselType = VesselType.Debris, namingPriority = LauncherPriority };
        }

        public static IEnumerable<Part> Subtree(Part p)
        {
            if (p == null) yield break;
            var stack = new Stack<Part>();
            stack.Push(p);
            while (stack.Count > 0)
            {
                Part q = stack.Pop();
                yield return q;
                if (q.children != null) foreach (Part c in q.children) stack.Push(c);
            }
        }

        public static string LauncherIn(ShipConstruct ship) =>
            ship?.parts?.Select(LauncherOf).FirstOrDefault(n => n != null);

        public static float PayloadMass(ShipConstruct ship) =>
            ship?.parts == null ? 0 : ship.parts.Where(p => !IsLauncherPart(p)).Sum(p => p.mass + p.GetResourceMass());

        // Splits the launcher into the pieces that drop away (cut at decouplers)
        // and names each: side pieces are Boosters, stacked ones Stage 1, 2, 3 from
        // the bottom up (Stage 1 drops first). Then makes sure the payload carries
        // the ship's name, so the launched craft is named after it, not a stage.
        public static void Relabel(ShipConstruct ship, string shipName, VesselType payloadType, ref string lastPayloadName)
        {
            if (ship?.parts == null || ship.parts.Count == 0) return;
            List<Part> launcher = ship.parts.Where(IsLauncherPart).ToList();
            if (launcher.Count == 0) return;
            var set = new HashSet<Part>(launcher);

            // Each launcher part belongs to the nearest piece head above it (itself included):
            // a decoupler, or the part where the launcher meets the payload.
            bool IsHead(Part p) => p.isDecoupler(out ModuleDecouple _) || p.FindModuleImplementing<ModuleAnchoredDecoupler>() != null ||
                                   p.parent == null || !set.Contains(p.parent);
            var headOf = new Dictionary<Part, Part>();
            foreach (Part p in launcher)
            {
                Part h = p;
                while (!IsHead(h)) h = h.parent;
                headOf[p] = h;
            }
            List<Part> heads = headOf.Values.Distinct().ToList();

            bool Radial(Part h) => h.attachMode == AttachModes.SRF_ATTACH && h.parent != null;

            // Depth of a stacked piece: how many stacked heads lie between it and the payload.
            int Depth(Part h)
            {
                int d = 0;
                for (Part q = h.parent; q != null && set.Contains(q); q = q.parent)
                    if (headOf.TryGetValue(q, out Part qh) && qh == q && !Radial(q)) d++;
                return d;
            }

            var labels = new Dictionary<Part, string>();
            foreach (IGrouping<string, Part> byLauncher in heads.GroupBy(h => LauncherOf(h)))
            {
                List<Part> stacked = byLauncher.Where(h => !Radial(h)).ToList();
                int deepest = stacked.Count == 0 ? 0 : stacked.Max(Depth);
                foreach (Part h in byLauncher)
                    labels[h] = byLauncher.Key + (Radial(h) ? " Booster" : " Stage " + (deepest - Depth(h) + 1));
            }
            foreach (Part p in launcher)
            {
                string label = labels[headOf[p]];
                if (p.vesselNaming.vesselName != label) p.vesselNaming.vesselName = label;
            }

            EnsurePayload(ship, set, shipName, payloadType, ref lastPayloadName);
        }

        // The payload's name. Ours sits on the payload's top part at PayloadPriority and
        // follows the ship name; a name the player set on a payload part is left alone.
        public static void EnsurePayload(ShipConstruct ship, HashSet<Part> set, string shipName, VesselType payloadType, ref string lastPayloadName)
        {
            if (ship?.parts == null || string.IsNullOrEmpty(shipName)) return;
            List<Part> payload = ship.parts.Where(p => !set.Contains(p) && !IsLauncherPart(p)).ToList();
            if (payload.Count == 0) return;
            string last = lastPayloadName;
            Part ours = payload.FirstOrDefault(p => p.vesselNaming != null && p.vesselNaming.namingPriority == PayloadPriority &&
                                                    (last == null || p.vesselNaming.vesselName == last));
            bool playerNamed = payload.Any(p => p.vesselNaming != null && p != ours);
            if (ours == null && !playerNamed)
            {
                ours = payload.FirstOrDefault(p => p.parent == null || set.Contains(p.parent)) ?? payload[0];
                ours.vesselNaming = new VesselNaming { namingPriority = PayloadPriority };
            }
            if (ours != null)
            {
                ours.vesselNaming.vesselName = shipName;
                ours.vesselNaming.vesselType = payloadType;
                lastPayloadName = shipName;
            }
        }

        // Takes the launcher names off a part tree (used when a launcher is saved
        // again as a subassembly; it gets fresh names when it's next picked up).
        public static void Clear(Part root)
        {
            foreach (Part p in Subtree(root))
                if (IsLauncherPart(p)) p.vesselNaming = null;
        }

        public static VesselType TypeOf(Category c)
        {
            if (c != null && Enum.TryParse(c.VesselType, out VesselType t)) return t;
            return VesselType.Ship;
        }
    }
}
