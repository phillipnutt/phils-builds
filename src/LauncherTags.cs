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
//
// What counts as a stage name is one rule, StageNames.IsLabel (src/Core.cs, checked
// offline), used for parts in the editor and for saved files alike. Only names of
// this save's launchers count (Library.IsLauncherName), so a craft whose parts the
// player named by hand is left alone; a launcher that was renamed or deleted still
// counts, so a craft built on it keeps working stage names.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static PhilsBuilds.StageNames;

namespace PhilsBuilds
{
    static class LauncherTags
    {
        // ---- Telling the parts apart -------------------------------------------------------

        // A launcher part, named for the piece it drops away with. Debris, or (a piece with
        // its own probe core or pod) a flyable type with a stage label for a name.
        public static bool IsLabel(Part p)
        {
            VesselNaming n = p?.vesselNaming;
            return n != null && StageNames.IsLabel(n.vesselName, n.vesselType == VesselType.Debris, n.namingPriority, Library.IsLauncherName);
        }

        // A launcher or subassembly file just saved: the names this mod wrote come off its
        // parts' VESSELNAMING nodes (nothing else in the file is touched). A launcher gets
        // fresh ones whenever it's placed. Saving a launcher or upper stage (`stage`) also
        // takes any stage-shaped name, and the craft name a lone launcher's top part carried.
        public static void StripFile(string path, bool stage)
        {
            try
            {
                ConfigNode root = ConfigNode.Load(path);
                if (root == null) return;
                bool changed = false;
                ConfigNode[] parts = root.GetNodes("PART");
                for (int i = 0; i < parts.Length; i++)
                    foreach (ConfigNode n in parts[i].GetNodes("VESSELNAMING"))
                    {
                        int.TryParse(n.GetValue("priority"), out int pri);
                        bool debris = n.GetValue("type") == "Debris";
                        bool ours = StageNames.IsLabel(n.GetValue("name"), debris, pri, Library.IsLauncherName, anyLauncher: stage) ||
                                    stage && i == 0 && pri == RootPriority && !debris;
                        if (!ours) continue;
                        parts[i].RemoveNode(n);
                        changed = true;
                    }
                if (changed) root.Save(path);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't tidy {path}: {e.Message}"); }
        }

        // A lone launcher's top part, carrying the ship's name: it sits on launcher parts
        // (the craft's root, or re-rooted), never on its own.
        public static bool IsRootCarrier(Part p)
        {
            VesselNaming n = p?.vesselNaming;
            if (n == null || n.namingPriority != RootPriority || n.vesselType == VesselType.Debris || string.IsNullOrEmpty(n.vesselName)) return false;
            return IsLabel(p.parent) || p.children.Any(IsLabel);
        }

        public static bool IsLauncherPart(Part p) => IsLabel(p) || IsRootCarrier(p);

        // The payload name this mod set (now, or as 0.1.0 did).
        static bool IsOurPayload(Part p) =>
            p?.vesselNaming != null && p.vesselNaming.vesselType != VesselType.Debris && !IsLabel(p) &&
            (p.vesselNaming.namingPriority == PayloadPriority || p.vesselNaming.namingPriority == OldPayloadPriority);

        static bool IsDecouplerPart(Part p) => p.isDecoupler(out ModuleDecouple _) || p.FindModuleImplementing<ModuleAnchoredDecoupler>() != null;

        // What a launcher piece flies as once it's dropped: debris, unless it can be flown.
        // KSP's [ and ] keys skip debris, and Phil gives first stages probe cores to land them.
        static VesselType PieceType(IEnumerable<Part> piece)
        {
            List<Part> command = piece.Where(p => p.FindModuleImplementing<ModuleCommand>() != null).ToList();
            if (command.Count == 0) return VesselType.Debris;
            return command.Any(p => p.CrewCapacity > 0) ? VesselType.Ship : VesselType.Probe;
        }

        // ---- The payload ----------------------------------------------------------------------

        // The payload: parts that aren't launcher parts and are joined to the craft's root
        // (payload built first, launcher attached under it), or sit on the launcher's top
        // piece (launcher placed first, payload built on it). Anything else added to a
        // launcher, like clamps or fins on a lower stage, is neither: it drops with its stage.
        // `anchor` is the payload's top part, where its name goes.
        public static List<Part> PayloadOf(ShipConstruct ship, HashSet<Part> launcher, out Part anchor)
        {
            anchor = null;
            var payload = new List<Part>();
            if (ship?.parts == null || ship.parts.Count == 0) return payload;
            Part root = ship.parts[0].localRoot;
            var starts = new List<Part>();
            if (root != null && !launcher.Contains(root)) starts.Add(root);
            else if (root != null)
            {
                // The launcher's top piece: from its top part down to the first decouplers.
                var top = new List<Part>();
                var stack = new Stack<Part>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    Part q = stack.Pop();
                    top.Add(q);
                    foreach (Part c in q.children) if (launcher.Contains(c) && !IsDecouplerPart(c)) stack.Push(c);
                }
                // Nearest the top first, so the payload's own top part comes first.
                starts.AddRange(top.SelectMany(q => q.children).Where(c => !launcher.Contains(c)));
            }
            anchor = starts.FirstOrDefault();
            var seen = new HashSet<Part>();
            foreach (Part s in starts)
            {
                var stack = new Stack<Part>();
                stack.Push(s);
                while (stack.Count > 0)
                {
                    Part q = stack.Pop();
                    if (!seen.Add(q)) continue;
                    payload.Add(q);
                    foreach (Part c in q.children) if (!launcher.Contains(c)) stack.Push(c);
                }
            }
            return payload;
        }

        static HashSet<Part> LauncherSet(ShipConstruct ship) =>
            new HashSet<Part>(ship?.parts?.Where(IsLauncherPart) ?? Enumerable.Empty<Part>());

        public static List<Part> PayloadOf(ShipConstruct ship) => PayloadOf(ship, LauncherSet(ship), out _);

        // The vessel type a payload named by this mod has, if there is one.
        public static VesselType? PayloadTypeIn(ShipConstruct ship)
        {
            Part p = PayloadOf(ship).FirstOrDefault(IsOurPayload) ?? ship?.parts?.FirstOrDefault(IsRootCarrier);
            return p?.vesselNaming?.vesselType;
        }

        public static float PayloadMass(ShipConstruct ship) => PayloadOf(ship).Sum(p => p.mass + p.GetResourceMass());

        // ---- Tagging -------------------------------------------------------------------------

        // The launcher a tagged part came from: "Cypress 010 Stage 1" -> "Cypress 010"
        // (or what it's called now, if it was renamed).
        public static string LauncherOf(Part p) => IsLabel(p) ? Library.LauncherNow(LauncherPart(p.vesselNaming.vesselName)) : null;

        // Names every part of a launcher just picked up from the library.
        public static void Tag(Part launcherRoot, string launcherName)
        {
            foreach (Part p in Subtree(launcherRoot))
                p.vesselNaming = new VesselNaming { vesselName = launcherName, vesselType = VesselType.Debris, namingPriority = LabelPriority };
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
            // Where a lone launcher's name sits now: it stays there (a re-rooted craft
            // keeps it on the launcher's real top, not the new root).
            Part carrier = launcher.FirstOrDefault(IsRootCarrier);

            // Each launcher part belongs to the nearest piece head above it (itself included):
            // a decoupler, or the part where the launcher meets the payload.
            bool IsHead(Part p) => IsDecouplerPart(p) || p.parent == null || !set.Contains(p.parent);
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
                if (n.vesselName != label || n.vesselType != type || n.namingPriority != LabelPriority)
                    p.vesselNaming = new VesselNaming { vesselName = label, vesselType = type, namingPriority = LabelPriority };
            }

            EnsurePayload(ship, set, shipName, payloadType, ref lastPayloadName, carrier);
        }

        // The craft's own name. With a payload, ours sits on the payload's top part at
        // PayloadPriority and follows the ship name; a name the player set on a payload
        // part, at a priority that beats the launcher's, is left alone. With no payload,
        // the launcher's top part carries it at RootPriority, so a launcher flown on its own
        // isn't named after its own top stage, as debris.
        public static void EnsurePayload(ShipConstruct ship, HashSet<Part> set, string shipName, VesselType payloadType, ref string lastPayloadName,
                                         Part carrier = null)
        {
            if (ship?.parts == null || string.IsNullOrEmpty(shipName)) return;
            List<Part> payload = PayloadOf(ship, set, out Part anchor);
            string last = lastPayloadName;

            // A name this mod put on a part that isn't the payload any more (a clamp added
            // to a lone launcher under an older version, say) comes off: it would carry the
            // craft's name away when that part drops.
            var inPayload = new HashSet<Part>(payload);
            foreach (Part p in ship.parts)
                if (!inPayload.Contains(p) && !set.Contains(p) && IsOurPayload(p) &&
                    (p.vesselNaming.vesselName == shipName || p.vesselNaming.vesselName == last))
                    p.vesselNaming = null;

            if (payload.Count == 0)
            {
                Part top = carrier != null && set.Contains(carrier) ? carrier : set.FirstOrDefault(p => p.parent == null || !set.Contains(p.parent));
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
            Part ours = payload.FirstOrDefault(p => IsOurPayload(p) && (last == null || p.vesselNaming.vesselName == last));
            bool playerNamed = payload.Any(p => p.vesselNaming != null && p != ours && p.vesselNaming.namingPriority > LabelPriority);
            if (ours == null && !playerNamed)
            {
                ours = anchor ?? payload[0];
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
