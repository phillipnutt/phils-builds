// What other mods can use from Phil's Builds (written for Phil's Reusability Suite).
//
// Phil's Builds behaves exactly the same when nothing uses this: the events have no
// listeners, and the library window only draws hangar copies once a provider is
// registered with SetCopies. Everything another mod gets back is a snapshot; changing
// it changes nothing here.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhilsBuilds
{
    public enum DesignKind { Craft, Launcher, UpperStage, Block }

    // How parts from a design came into the editor.
    public enum PlaceHow { Load, Merge, PickUp, Copy }

    public enum ChipTone { Plain, Good, Warn }

    // One saved design: a filed craft, or a filed subassembly. Unfiled craft and
    // subassemblies aren't designs.
    public sealed class Design
    {
        public string Id { get; internal set; }         // stable for this design in this save: survives renames, filing and re-rating
        public string FamilyId { get; internal set; }   // shared by the marks of one design line (for launchers: one family and payload code)
        public DesignKind Kind { get; internal set; }
        public string Name { get; internal set; }       // "Oak 005 II"
        public string Family { get; internal set; }     // "Oak"; "" for a block saved without one
        public int Mark { get; internal set; }          // 0 when the name has no mark
        public string MarkText { get; internal set; }   // "II", "Mk II", "B"; "" when there's no mark
        public bool IsNewestMark { get; internal set; }
        public string Category { get; internal set; }   // category id: "Capsules", "Launchers", "UpperStages"; "" for blocks
        public string CategoryLabel { get; internal set; }
        public string Facility { get; internal set; }   // "VAB" or "SPH" for craft; "" for subassemblies (they're shared)
        public float Tons { get; internal set; }        // launchers: tons to orbit; 0 when not rated
        public float Width { get; internal set; }       // launchers: widest payload in meters; 0 when not given
        public string Group { get; internal set; }      // blocks: their group ("DESU", "Control")
        public string Notes { get; internal set; }      // launchers: test notes
        public string Path { get; internal set; }       // the .craft file
        public override string ToString() => Name;
    }

    // Parts from a design have just come into the editor. Parts[i] is slot i: the
    // part's place in the design file. Fired once the parts exist. Picked-up and
    // merged parts are held on the cursor (or loose), so they aren't in
    // EditorLogic.fetch.ship.parts until attached, except on an empty editor.
    public sealed class Placed
    {
        public Design Design { get; internal set; }
        public PlaceHow How { get; internal set; }
        public IList<Part> Parts { get; internal set; }
        public string CopyId { get; internal set; }     // How == Copy: the Id of the hangar row picked
    }

    // A design was just written from the editor. Parts are in the order written, so
    // Parts[i] is slot i of the file as it is now.
    public sealed class Saved
    {
        public Design Design { get; internal set; }
        public bool IsNewFile { get; internal set; }    // a new design or a new mark, not a save over an existing file
        public IList<Part> Parts { get; internal set; }
    }

    // A mod that keeps built copies of designs (a hangar) registers one of these.
    public interface ICopies
    {
        // The rows under a design in the library window, in order. Empty or null
        // shows the design as it is today.
        IList<CopyRow> CopiesOf(Design design);

        // A short line added to the design's own row, in green ("2 flown first stages
        // in the hangar"). Null or "" adds nothing.
        string Note(Design design);

        // The side panel for a row the player picked. Null shows a plain one.
        CopyDetail Detail(Design design, CopyRow row);

        // The player pressed Pick up on a hangar row (never called for a New row: that
        // picks up the design itself, as usual). Build what goes on the cursor and call
        // hold(ship) when it's ready, or hold(null) to cancel. It may take frames.
        void Build(Design design, CopyRow row, Action<ShipConstruct> hold);
    }

    public sealed class CopyRow
    {
        public string Id = "";              // the provider's own id for this copy
        public bool IsNew;                  // "New · built from the design": picking it picks up the design itself
        public string Label = "";           // "New", "#4"
        public string Text = "";            // "first stage · 1 flight · complete"
        public string Warning = "";         // "missing 1 grid fin", shown in yellow after the text
        public string Price = "";           // "30,510 · 1.1 days"
        public bool Reserved;               // greyed out, can be looked at but not picked
        public string ReservedText = "";    // shown in place of Text: "Under Comsat II, VAB queue"
    }

    public sealed class CopyDetail
    {
        public string Eyebrow = "";         // "Hangar copy · Oak 005 Mk I"
        public string Title = "";           // "Oak 005 #4"
        public List<CopyChip> Chips = new List<CopyChip>();
        public List<CopyLine> Lines = new List<CopyLine>();     // the cost box
        public string TotalLabel = "";      // "Total · build time"
        public string Total = "";           // "30,510 · 1.1 days"
        public string LogTitle = "";        // "Flights"
        public List<string> Log = new List<string>();
        public string Hint = "";            // under the Load / Add to craft button

        // Version 2. When Open is set, a second button (OpenLabel: "Load as stored") sits beside
        // Load: it replaces the editor's craft. Phil's Builds asks first if there are unsaved
        // changes, as its own Load does, then closes its window and calls Open.
        public string OpenLabel = "";
        public Action Open;
    }

    public sealed class CopyChip
    {
        public string Text = "";
        public ChipTone Tone;
        public CopyChip() { }
        public CopyChip(string text, ChipTone tone = ChipTone.Plain) { Text = text ?? ""; Tone = tone; }
    }

    public sealed class CopyLine
    {
        public string Label = "", Value = "";
        public bool Good;                   // a saving, shown in green
        public CopyLine() { }
        public CopyLine(string label, string value, bool good = false) { Label = label ?? ""; Value = value ?? ""; Good = good; }
    }

    public static class BuildsApi
    {
        public const int Version = 2;       // 2: CopyDetail.OpenLabel and Open

        // Fired whenever Phil's Builds reads the library again: after every save,
        // filing, rename, re-rating and delete, and when its window opens.
        public static event Action LibraryChanged;
        public static event Action<Placed> DesignPlaced;
        public static event Action<Saved> DesignSaved;

        internal static ICopies Copies { get; private set; }

        // ---- Reading the library -------------------------------------------------------

        static List<Design> cache;
        static int cacheVersion = -1;

        public static IList<Design> Designs()
        {
            Library.EnsureScanned();
            if (cache == null || cacheVersion != Library.Version)
            {
                cache = new List<Design>();
                foreach (CraftFile c in Library.Craft) if (Make(c) is Design d) cache.Add(d);
                foreach (SubFile s in Library.Subs) if (Make(s) is Design d) cache.Add(d);
                cacheVersion = Library.Version;
            }
            return cache.AsReadOnly();
        }

        public static Design Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Designs().FirstOrDefault(d => d.Id == id);

        public static Design OfFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string full;
            try { full = System.IO.Path.GetFullPath(path); } catch { return null; }
            return Designs().FirstOrDefault(d => string.Equals(d.Path, full, StringComparison.OrdinalIgnoreCase));
        }

        // The design open in the editor, or null when it isn't filed in a family (or
        // hasn't been saved under its current name yet).
        public static Design Current => Stock.Editor == null ? null : OfFile(Stock.CurrentFile);

        // Opens Phil's Builds' Save panel in the editor, to file the open craft.
        public static void OpenSave() => BuildsEditor.Instance?.OpenSave();

        // ---- Hangar copies -----------------------------------------------------------------

        public static void SetCopies(ICopies provider)
        {
            Copies = provider;
            RefreshWindow();
        }

        // Asks the library window to draw its rows again (say, when the hangar changed).
        public static void RefreshWindow() => BuildsEditor.Instance?.RefreshWindow();

        // ---- Inside Phil's Builds --------------------------------------------------------------

        static Design Make(CraftFile c)
        {
            if (c == null || !c.Filed || string.IsNullOrEmpty(c.Id)) return null;
            Family f = Library.FamilyOf(c);
            return new Design
            {
                Id = c.Id,
                FamilyId = c.Line,
                Kind = DesignKind.Craft,
                Name = c.Name,
                Family = c.Family ?? "",
                Mark = c.Mark,
                MarkText = c.Mark > 0 ? Marks.Short(c.Style, c.Mark) : "",
                IsNewestMark = f == null || f.Newest == c,
                Category = c.Category.Id,
                CategoryLabel = c.Category.Label,
                Facility = c.Facility,
                Group = "",
                Notes = "",
                Path = c.Path,
            };
        }

        static Design Make(SubFile s)
        {
            if (s == null || s.Kind == SubKind.Unfiled || string.IsNullOrEmpty(s.Id)) return null;
            DesignKind kind = s.Kind == SubKind.Launcher ? DesignKind.Launcher : s.Kind == SubKind.UpperStage ? DesignKind.UpperStage : DesignKind.Block;
            Category cat = kind == DesignKind.Launcher ? Category.ById("Launchers") : kind == DesignKind.UpperStage ? Category.ById("UpperStages") : null;
            bool marked = kind != DesignKind.Block;
            return new Design
            {
                Id = s.Id,
                FamilyId = s.Line,
                Kind = kind,
                Name = s.Name,
                Family = s.Family ?? "",
                Mark = marked ? s.Mark : 0,
                MarkText = marked ? Marks.Short(s.Style, s.Mark) : "",
                IsNewestMark = !marked || !Library.Subs.Any(o => o != s && o.Kind == s.Kind && o.Line == s.Line && o.Mark > s.Mark),
                Category = cat?.Id ?? "",
                CategoryLabel = cat?.Label ?? "",
                Facility = "",
                Tons = kind == DesignKind.Launcher ? s.Rating : 0,
                Width = kind == DesignKind.Launcher ? s.Width : 0,
                Group = s.Group ?? "",
                Notes = s.Notes ?? "",
                Path = s.Path,
            };
        }

        static readonly IList<CopyRow> NoRows = new List<CopyRow>().AsReadOnly();

        internal static IList<CopyRow> CopiesOf(Design d)
        {
            if (Copies == null || d == null) return NoRows;
            try
            {
                IList<CopyRow> rows = Copies.CopiesOf(d);
                if (rows == null) return NoRows;
                List<CopyRow> list = rows.Where(r => r != null).ToList();
                foreach (CopyRow r in list)
                {
                    r.Id = r.Id ?? "";
                    r.Label = r.Label ?? "";
                    r.Text = r.Text ?? "";
                    r.Warning = r.Warning ?? "";
                    r.Price = r.Price ?? "";
                    r.ReservedText = r.ReservedText ?? "";
                }
                return list.AsReadOnly();
            }
            catch (Exception e) { Debug.LogError($"[PhilsBuilds] Hangar copies of {d.Name}: {e}"); return NoRows; }
        }

        internal static string NoteOf(Design d)
        {
            if (Copies == null || d == null) return null;
            try { return Copies.Note(d); }
            catch (Exception e) { Debug.LogError($"[PhilsBuilds] Hangar note for {d.Name}: {e}"); return null; }
        }

        internal static CopyDetail DetailOf(Design d, CopyRow r)
        {
            if (Copies == null || d == null || r == null) return null;
            try { return Copies.Detail(d, r); }
            catch (Exception e) { Debug.LogError($"[PhilsBuilds] Hangar detail for {d.Name} {r.Label}: {e}"); return null; }
        }

        internal static void RaiseLibraryChanged()
        {
            Action ev = LibraryChanged;
            if (ev == null) return;
            foreach (Action a in ev.GetInvocationList())
            {
                try { a(); }
                catch (Exception e) { Debug.LogError($"[PhilsBuilds] A LibraryChanged listener failed: {e}"); }
            }
        }

        internal static void RaisePlaced(Design d, PlaceHow how, IEnumerable<Part> parts, string copyId = null)
        {
            if (d == null || parts == null) return;
            Raise(DesignPlaced, new Placed { Design = d, How = how, Parts = parts.ToList().AsReadOnly(), CopyId = copyId ?? "" }, "DesignPlaced");
        }

        internal static bool HasSavedListeners => DesignSaved != null;

        internal static void RaiseSaved(Design d, bool isNewFile, IEnumerable<Part> parts)
        {
            if (d == null || parts == null) return;
            Raise(DesignSaved, new Saved { Design = d, IsNewFile = isNewFile, Parts = parts.ToList().AsReadOnly() }, "DesignSaved");
        }

        static void Raise<T>(Action<T> ev, T arg, string name)
        {
            if (ev == null) return;
            foreach (Action<T> a in ev.GetInvocationList())
            {
                try { a(arg); }
                catch (Exception e) { Debug.LogError($"[PhilsBuilds] A {name} listener failed: {e}"); }
            }
        }
    }
}
