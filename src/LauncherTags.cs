// Spent-stage names, done with KSP's own per-part vessel naming.
//
// Any part can carry a vessel name, type and priority (stock "Rename Vessel").
// When a craft splits, or is launched, KSP names each piece after its
// highest-priority named part, counting only priorities above 0. So a launcher
// picked from the library gets every part named after it, at priority 1, as
// debris: "Cypress 010 Booster", "Cypress 010 Stage 1" (a piece with its own probe
// core or pod stays a probe or ship, so it can still be flown). The payload gets the
// ship's name at priority 3, so the launched craft and the payload keep their own
// name. A launcher with no payload (flown on its own) has its top part carry the
// ship's name at priority 2. Everything stays stock data in the craft file;
// nothing is renamed or retyped in flight by this mod.
//
// Phil's Builds 0.1.0 used -1 for the launcher and 1 for the payload. KSP skips
// priorities below 1, so those stage names never showed in flight (found
// 2026-11-07 by reading Part.decouple). Craft tagged then are upgraded the next
// time they're edited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PhilsBuilds
{
    static class LauncherTags
    {
        public const int LauncherPriority = 1, RootPriority = 2, PayloadPriority = 3;
        public const int OldLauncherPriority = -1, OldPayloadPriority = 1;      // Phil's Builds 0.1.0

        static readonly Regex Label = new Regex(@"^(.*?) (Stage \d+|Booster)$");

        // A launcher part, named for the piece it drops away with. Debris, or (a piece with
        // its own probe core or pod) a flyable type with a stage label for a name.
        public static bool IsLabel(Part p) =>
            p != null && p.vesselNaming != null && (p.vesselNaming.namingPriority == LauncherPriority || p.vesselNaming.namingPriority == OldLauncherPriority) &&
            !string.IsNullOrEmpty(p.vesselNaming.vesselName) && (p.vesselNaming.vesselType == VesselType.Debris || Label.IsMatch(p.vesselNaming.vesselName));

        public static bool IsLabelName(string name) => !string.IsNullOrEmpty(name) && Label.IsMatch(name);

        // What a launcher piece flies as once it's dropped: debris, unless it can be flown.
        // KSP's [ and ] keys skip debris, and Phil gives first stages probe cores to land them.
        static VesselType PieceType(IEnumerable<Part> piece)
        {
            List<Part> command = piece.Where(p => p.FindModuleImplementing<ModuleCommand>() != null).ToList();
            if (command.Count == 0) return VesselType.Debris;
            return command.Any(p => p.CrewCapacity > 0) ? VesselType.Ship : VesselType.Probe;
        }

        // A lone launcher's top part, carrying the ship's name. It's the craft's root, or
        // sits on another launcher part (after the craft was re-rooted).
        public static bool IsRootCarrier(Part p) =>
            p != null && p.vesselNaming != null && p.vesselNaming.namingPriority == RootPriority && p.vesselNaming.vesselType != VesselType.Debris &&
            !string.IsNullOrEmpty(p.vesselNaming.vesselName) && (p.parent == null || IsLabel(p.parent));

        public static bool IsLauncherPart(Part p) => IsLabel(p) || IsRootCarrier(p);

        // The payload name this mod set (now, or as 0.1.0 did).
        static bool IsOurPayload(Part p) =>
            p?.vesselNaming != null && p.vesselNaming.vesselType != VesselType.Debris && !IsLabel(p) &&
            (p.vesselNaming.namingPriority == PayloadPriority || p.vesselNaming.namingPriority == OldPayloadPriority);

        // The vessel type a payload named by this mod has, if there is one.
        public static VesselType? PayloadTypeIn(ShipConstruct ship)
        {
            Part p = ship?.parts?.FirstOrDefault(x => !IsLauncherPart(x) && IsOurPayload(x)) ?? ship?.parts?.FirstOrDefault(IsRootCarrier);
            return p?.vesselNaming?.vesselType;
        }

        // The launcher a tagged part came from: "Cypress 010 Stage 1" -> "Cypress 010".
        public static string LauncherOf(Part p)
        {
            if (!IsLabel(p)) return null;
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
            if (!ship.parts.Any(IsLabel)) return;
            List<Part> launcher = ship.parts.Where(IsLauncherPart).ToList();
            var set = new HashSet<Part>(launcher);
            string fallback = launcher.Select(LauncherOf).FirstOrDefault(n => n != null);

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
            foreach (IGrouping<string, Part> byLauncher in heads.GroupBy(h => LauncherOf(h) ?? fallback))
            {
                List<Part> stacked = byLauncher.Where(h => !Radial(h)).ToList();
                int deepest = stacked.Count == 0 ? 0 : stacked.Max(Depth);
                foreach (Part h in byLauncher)
                    labels[h] = byLauncher.Key + (Radial(h) ? " Booster" : " Stage " + (deepest - Depth(h) + 1));
            }
            // Every launcher part gets its piece's label (a lone launcher's top part too, for
            // now: EnsurePayload gives it the ship's name back if there's still no payload).
            var types = heads.ToDictionary(h => h, h => PieceType(launcher.Where(p => headOf[p] == h)));
            foreach (Part p in launcher)
            {
                string label = labels[headOf[p]];
                VesselType type = types[headOf[p]];
                VesselNaming n = p.vesselNaming;
                if (n.vesselName != label || n.vesselType != type || n.namingPriority != LauncherPriority)
                    p.vesselNaming = new VesselNaming { vesselName = label, vesselType = type, namingPriority = LauncherPriority };
            }

            EnsurePayload(ship, set, shipName, payloadType, ref lastPayloadName);
        }

        // The craft's own name. With a payload, ours sits on the payload's top part at
        // PayloadPriority and follows the ship name; a name the player set on a payload
        // part, at a priority that beats the launcher's, is left alone. With no payload,
        // the launcher's top part carries it at RootPriority, so a launcher flown on its own
        // isn't named after its own top stage, as debris.
        public static void EnsurePayload(ShipConstruct ship, HashSet<Part> set, string shipName, VesselType payloadType, ref string lastPayloadName)
        {
            if (ship?.parts == null || string.IsNullOrEmpty(shipName)) return;
            List<Part> payload = ship.parts.Where(p => !set.Contains(p) && !IsLauncherPart(p)).ToList();
            if (payload.Count == 0)
            {
                Part top = set.FirstOrDefault(p => p.parent == null || !set.Contains(p.parent));
                if (top == null) return;
                // It flies as what it is: a probe or ship if it has command parts.
                VesselType own = PieceType(set);
                VesselType type = own != VesselType.Debris ? own : payloadType;
                if (top.vesselNaming == null || top.vesselNaming.namingPriority != RootPriority || top.vesselNaming.vesselName != shipName ||
                    top.vesselNaming.vesselType != type)
                    top.vesselNaming = new VesselNaming { vesselName = shipName, vesselType = type, namingPriority = RootPriority };
                lastPayloadName = shipName;
                return;
            }
            string last = lastPayloadName;
            Part ours = payload.FirstOrDefault(p => IsOurPayload(p) && (last == null || p.vesselNaming.vesselName == last));
            bool playerNamed = payload.Any(p => p.vesselNaming != null && p != ours && p.vesselNaming.namingPriority > LauncherPriority);
            if (ours == null && !playerNamed)
            {
                ours = payload.FirstOrDefault(p => p.parent == null || set.Contains(p.parent)) ?? payload[0];
                ours.vesselNaming = new VesselNaming();
            }
            if (ours != null)
            {
                ours.vesselNaming.vesselName = shipName;
                ours.vesselNaming.vesselType = payloadType;
                ours.vesselNaming.namingPriority = PayloadPriority;
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
