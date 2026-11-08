// The naming rules, kept free of KSP so they're easy to read and check:
// mark numbering (I, II, III / Mk 1 / Block I / A, B, C), how names are put
// together, payload codes and launcher classes, and the build categories.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PhilsBuilds
{
    public enum MarkStyle { Roman, Mk, Block, Letter }

    public static class Marks
    {
        public static readonly MarkStyle[] Styles = { MarkStyle.Roman, MarkStyle.Mk, MarkStyle.Block, MarkStyle.Letter };

        public static string StyleLabel(MarkStyle s)
        {
            switch (s)
            {
                case MarkStyle.Mk: return "Mk I, Mk II";
                case MarkStyle.Block: return "Block I, II";
                case MarkStyle.Letter: return "A, B, C";
                default: return "I, II, III";
            }
        }

        public static string Format(MarkStyle s, int n)
        {
            if (n < 1) n = 1;
            switch (s)
            {
                // Phil writes "Magnolia Mk I", the British way.
                case MarkStyle.Mk: return "Mk " + Roman(n);
                case MarkStyle.Block: return "Block " + Roman(n);
                case MarkStyle.Letter: return Letter(n);
                default: return Roman(n);
            }
        }

        // Short form for the little mark buttons: "II" or "B".
        public static string Short(MarkStyle s, int n)
        {
            switch (s)
            {
                case MarkStyle.Letter: return Letter(n);
                default: return Roman(n);
            }
        }

        public static string Roman(int n)
        {
            if (n < 1 || n > 3999) return n.ToString(CultureInfo.InvariantCulture);
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] digits = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
                while (n >= values[i]) { s.Append(digits[i]); n -= values[i]; }
            return s.ToString();
        }

        // Letters skip I, O, V and X, so a letter mark is never mistaken for a
        // Roman numeral (or a zero).
        const string LetterSet = "ABCDEFGHJKLMNPQRSTUWYZ";

        public static string Letter(int n) =>
            n >= 1 && n <= LetterSet.Length ? LetterSet[n - 1].ToString() : n.ToString(CultureInfo.InvariantCulture);

        static readonly Regex RomanRe = new Regex(@"^M{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$");

        public static int ParseRoman(string s)
        {
            if (string.IsNullOrEmpty(s) || !RomanRe.IsMatch(s)) return 0;
            var map = new Dictionary<char, int> { { 'I', 1 }, { 'V', 5 }, { 'X', 10 }, { 'L', 50 }, { 'C', 100 }, { 'D', 500 }, { 'M', 1000 } };
            int total = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int v = map[s[i]];
                if (i + 1 < s.Length && map[s[i + 1]] > v) total -= v;
                else total += v;
            }
            return total;
        }

        static readonly Regex MkRe = new Regex(@"^(.*?)\s*\bMk\.?\s*(\d+)$", RegexOptions.IgnoreCase);
        static readonly Regex BlockRe = new Regex(@"^(.*?)\s+Block\s+([IVXLCDM]+)$");
        static readonly Regex TailRe = new Regex(@"^(.*\S)\s+(\S+)$");

        // Splits a trailing mark off a name: "Haven Lab II" -> "Haven Lab", Roman, 2.
        // "Magnolia Mk I" and "Magnolia Mk 1" are both read as Mk style.
        public static bool TryParse(string name, out string rest, out MarkStyle style, out int number)
        {
            rest = name?.Trim() ?? "";
            style = MarkStyle.Roman;
            number = 0;
            if (rest.Length == 0) return false;

            Match m = BlockRe.Match(rest);
            if (m.Success && (number = ParseRoman(m.Groups[2].Value)) > 0)
            {
                rest = m.Groups[1].Value.Trim();
                style = MarkStyle.Block;
                return true;
            }
            m = MkRe.Match(rest);
            if (m.Success && int.TryParse(m.Groups[2].Value, out number) && number > 0)
            {
                rest = m.Groups[1].Value.Trim();
                style = MarkStyle.Mk;
                return true;
            }
            // "Mk I": a Roman numeral after Mk.
            Match tail = TailRe.Match(rest);
            if (!tail.Success) return false;
            string head = tail.Groups[1].Value, last = tail.Groups[2].Value;
            int roman = ParseRoman(last);
            if (roman > 0)
            {
                Match mk = new Regex(@"^(.*?)\s*\bMk\.?$", RegexOptions.IgnoreCase).Match(head);
                if (mk.Success) { rest = mk.Groups[1].Value.Trim(); style = MarkStyle.Mk; number = roman; return true; }
                rest = head;
                style = MarkStyle.Roman;
                number = roman;
                return true;
            }
            if (last.Length == 1 && LetterSet.IndexOf(last[0]) >= 0)
            {
                rest = head;
                style = MarkStyle.Letter;
                number = LetterSet.IndexOf(last[0]) + 1;
                return true;
            }
            return false;
        }
    }

    public enum LauncherClass { Light, Medium, Heavy, SuperHeavy }

    public static class Naming
    {
        // "Haven" + "Lab" + mark 1 -> "Haven Lab I".
        public static string Craft(string family, string role, MarkStyle style, int mark)
        {
            string name = family.Trim();
            if (!string.IsNullOrEmpty(role)) name += " " + role.Trim();
            return name + " " + Marks.Format(style, mark);
        }

        // Launchers carry their payload code; the first mark has no numeral,
        // like Phil's "Oak 030". "Oak" + 60 t + mark 2 -> "Oak 060 II".
        public static string Launcher(string family, float tons, MarkStyle style, int mark)
        {
            string name = family.Trim() + " " + Code(tons);
            return mark > 1 ? name + " " + Marks.Format(style, mark) : name;
        }

        // An upper stage or tug: "Sherpa I".
        public static string Stage(string family, MarkStyle style, int mark) => family.Trim() + " " + Marks.Format(style, mark);

        // Payload code: whole tons to orbit, three digits, at least 001.
        public static string Code(float tons)
        {
            int t = Math.Max(1, (int)Math.Floor(tons + 0.0001f));
            return t.ToString(t < 1000 ? "000" : "0", CultureInfo.InvariantCulture);
        }

        public static LauncherClass ClassOf(float tons)
        {
            if (tons <= 5f) return LauncherClass.Light;
            if (tons <= 20f) return LauncherClass.Medium;
            if (tons <= 50f) return LauncherClass.Heavy;
            return LauncherClass.SuperHeavy;
        }

        public static string ClassLabel(LauncherClass c)
        {
            switch (c)
            {
                case LauncherClass.Light: return "Light";
                case LauncherClass.Medium: return "Medium";
                case LauncherClass.Heavy: return "Heavy";
                default: return "Super-heavy";
            }
        }

        public static string ClassRange(LauncherClass c)
        {
            switch (c)
            {
                case LauncherClass.Light: return "up to 5 t";
                case LauncherClass.Medium: return "5 to 20 t";
                case LauncherClass.Heavy: return "20 to 50 t";
                default: return "50 t up";
            }
        }

        // Tons with one decimal under 10, whole tons above: "7.4 t", "183 t".
        public static string Tons(float t) =>
            (t < 10f ? t.ToString("0.0", CultureInfo.InvariantCulture) : t.ToString("#,0", CultureInfo.InvariantCulture)) + " t";

        public static string Meters(float m) =>
            (m < 10f ? m.ToString("0.##", CultureInfo.InvariantCulture) : m.ToString("0.#", CultureInfo.InvariantCulture)) + " m";

        public static string Funds(float f) => f.ToString("#,0", CultureInfo.InvariantCulture);

        // What changed from one mark to the next, like "+2.3 t · +1,200 funds · +3 parts".
        // Figures that didn't change are left out; "" if none did (or nothing is known).
        public static string MarkChanges(float mass0, float mass1, float cost0, float cost1, int parts0, int parts1, int stages0, int stages1)
        {
            var bits = new List<string>();
            string Sign(float d) => d > 0 ? "+" : "-";
            float dm = mass1 - mass0;
            if (mass0 > 0 && mass1 > 0 && Math.Abs(dm) >= 0.05f) bits.Add(Sign(dm) + Tons(Math.Abs(dm)));
            float dc = cost1 - cost0;
            if (cost0 > 0 && cost1 > 0 && Math.Abs(dc) >= 1f) bits.Add(Sign(dc) + Funds(Math.Abs(dc)) + " funds");
            int dp = parts1 - parts0;
            if (parts0 > 0 && parts1 > 0 && dp != 0) bits.Add($"{Sign(dp)}{Math.Abs(dp)} part{(Math.Abs(dp) == 1 ? "" : "s")}");
            int ds = stages1 - stages0;
            if (stages0 > 0 && stages1 > 0 && ds != 0) bits.Add($"{Sign(ds)}{Math.Abs(ds)} stage{(Math.Abs(ds) == 1 ? "" : "s")}");
            return string.Join(" · ", bits);
        }

        // File-safe: KSP saves "Haven Lab I" as "Haven Lab I.craft"; these
        // characters can't be in a Windows file name.
        public static string Clean(string s)
        {
            if (s == null) return "";
            var bad = new HashSet<char>("\\/:*?\"<>|".ToCharArray());
            string c = new string(s.Where(ch => !bad.Contains(ch) && !char.IsControl(ch)).ToArray());
            return Regex.Replace(c, @"\s+", " ").Trim();
        }

        public static bool SameName(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        // Whether a craft name belongs to a family: the family's name, then a space
        // or nothing. "Orpheus II" belongs to Orpheus; "Orpheusly" doesn't.
        public static bool InFamily(string name, string family) =>
            !string.IsNullOrEmpty(family) && name != null &&
            (SameName(name, family) || name.StartsWith(family.Trim() + " ", StringComparison.OrdinalIgnoreCase));

        // A craft name with its family name swapped: "Orpheus I I" from "Orpheus I" to
        // "Orpheus" gives "Orpheus I". Names outside the family stay as they are.
        public static string SwapFamily(string name, string from, string to)
        {
            if (!InFamily(name, from)) return name;
            return to.Trim() + name.Trim().Substring(from.Trim().Length);
        }

        // The family part of a typed launcher or stage name: a payload code and mark
        // typed on the end are dropped ("Oak 030 II" -> "Oak").
        public static string LauncherFamily(string typed)
        {
            string n = FamilyPart(typed);
            Match m = Regex.Match(n, @"^(.*\S)\s+\d{3,4}$");
            return m.Success ? m.Groups[1].Value : n;
        }

        // The family part of a typed name: a mark typed on the end is dropped, since
        // the mod adds it ("Orpheus I" -> "Orpheus").
        public static string FamilyPart(string typed)
        {
            string n = Clean(typed);
            return Marks.TryParse(n, out string rest, out _, out _) && rest.Length > 0 ? rest : n;
        }
    }

    public enum Group { Launch, Crewed, Uncrewed, Surface, AirSea }

    // A build category: its library group, its name list, its folder and the
    // map icon (stock vessel type) its craft get.
    public class Category
    {
        public string Id, Label, Folder, Rail;
        public Group Group;
        public string VesselType;       // a VesselType name, kept as text so this file needs no KSP
        public bool Subassembly;        // launchers and upper stages live in the Subassemblies folder

        public static readonly Category[] All =
        {
            new Category("Launchers", "Launchers", "Launchers", Group.Launch, "Debris", sub: true),
            new Category("UpperStages", "Upper stages & tugs", "Upper stages and tugs", Group.Launch, "Ship", sub: true),
            new Category("Capsules", "Capsules", "Capsules", Group.Crewed, "Ship"),
            new Category("Landers", "Landers", "Landers", Group.Crewed, "Lander"),
            new Category("DeepSpace", "Deep-space ships", "Deep-space ships", Group.Crewed, "Ship"),
            new Category("Rescue", "Rescue craft", "Rescue craft", Group.Crewed, "Ship"),
            new Category("Probes", "Probes", "Probes", Group.Uncrewed, "Probe"),
            new Category("ScienceLanders", "Science landers", "Science landers", Group.Uncrewed, "Lander"),
            new Category("Relays", "Relays & comsats", "Relays and comsats", Group.Uncrewed, "Relay"),
            new Category("Survey", "Survey sats", "Survey sats", Group.Uncrewed, "Probe"),
            new Category("Tankers", "Tankers & depots", "Tankers and depots", Group.Uncrewed, "Ship"),
            new Category("Mining", "Mining & ISRU", "Mining and ISRU", Group.Uncrewed, "Ship"),
            new Category("Stations", "Stations", "Stations", Group.Surface, "Station"),
            new Category("Bases", "Bases", "Bases", Group.Surface, "Base"),
            new Category("Rovers", "Rovers", "Rovers", Group.Surface, "Rover"),
            new Category("Hoppers", "Hoppers & tests", "Hoppers and tests", Group.Surface, "Ship"),
            new Category("Spaceplanes", "Spaceplanes", "Spaceplanes", Group.AirSea, "Plane"),
            new Category("Aircraft", "Jets & aircraft", "Jets and aircraft", Group.AirSea, "Plane"),
            new Category("CargoPlanes", "Cargo planes", "Cargo planes", Group.AirSea, "Plane"),
            new Category("Bombers", "Bombers & droppers", "Bombers and droppers", Group.AirSea, "Plane"),
            new Category("Rotorcraft", "Rotorcraft", "Rotorcraft", Group.AirSea, "Plane"),
            new Category("Boats", "Boats & subs", "Boats and subs", Group.AirSea, "Ship"),
        };

        Category(string id, string label, string folder, Group group, string vesselType, bool sub = false)
        {
            Id = id;
            Label = label;
            Folder = folder;
            Group = group;
            VesselType = vesselType;
            Subassembly = sub;
        }

        public static IEnumerable<Category> Craft => All.Where(c => !c.Subassembly);

        public static Category ById(string id) => All.FirstOrDefault(c => c.Id == id);

        public static Category ByFolder(string folder) =>
            All.FirstOrDefault(c => string.Equals(c.Folder, folder, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(c.Id, folder, StringComparison.OrdinalIgnoreCase));

        public static string GroupLabel(Group g)
        {
            switch (g)
            {
                case Group.Launch: return "Launch";
                case Group.Crewed: return "Crewed";
                case Group.Uncrewed: return "Uncrewed";
                case Group.Surface: return "Stations & surface";
                default: return "Air & sea";
            }
        }
    }

    // What a subassembly is, in the library. Block covers modules and building
    // blocks alike: anything with a name and a group (DESU, Control, Power…).
    public enum SubKind { Unfiled, Launcher, UpperStage, Block }

    public static class SubKinds
    {
        // Launchers and upper stages: rated or marked like stages, kept on the Launchers tab.
        public static bool IsStage(SubKind k) => k == SubKind.Launcher || k == SubKind.UpperStage;
    }

    // The names this mod writes on a launcher's parts, as KSP's per-part vessel naming
    // (name, type, priority). KSP names a piece that drops away after its highest-priority
    // named part, counting priorities above 0 only.
    public static class StageNames
    {
        public const int LabelPriority = 1, RootPriority = 2, PayloadPriority = 3;
        public const int OldLabelPriority = -1, OldPayloadPriority = 1;     // Phil's Builds 0.1.0

        static readonly Regex Shape = new Regex(@"^(.*?) (Stage \d+|Booster)$");

        // "Cypress 010 Stage 1" -> "Cypress 010"; a bare launcher name stays as it is.
        public static string LauncherPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Match m = Shape.Match(name);
            return m.Success ? m.Groups[1].Value : name;
        }

        // The one rule for "this naming is a stage name this mod wrote", on a part in the
        // editor or in a saved file: at a stage's priority, debris or stage-shaped
        // ("Oak 030 Stage 1", "Oak 030 Booster"), for one of this save's launchers (now
        // or before a rename or delete), so a name the player typed is left alone.
        // `anyLauncher` also takes a stage-shaped name from any launcher: a launcher or
        // upper stage being saved gets fresh names whenever it's placed.
        public static bool IsLabel(string name, bool debris, int priority, Func<string, bool> isLauncher, bool anyLauncher = false)
        {
            if (string.IsNullOrEmpty(name) || priority != LabelPriority && priority != OldLabelPriority) return false;
            bool shaped = Shape.IsMatch(name);
            if (!debris && !shaped) return false;
            return anyLauncher && shaped || isLauncher != null && isLauncher(LauncherPart(name));
        }
    }

    // Files and folders, compared the one way: full paths, any case, a trailing slash or not.
    public static class Paths
    {
        static string Full(string p) => System.IO.Path.GetFullPath(p).TrimEnd('\\', '/');

        public static bool Same(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // Where `folder` sits under `root`, folders joined with '/' ("" for the root
        // itself), or null when it's somewhere else.
        public static string Under(string root, string folder)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(folder)) return null;
            try
            {
                string r = Full(root), f = Full(folder);
                if (string.Equals(r, f, StringComparison.OrdinalIgnoreCase)) return "";
                if (!f.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase) && !f.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase)) return null;
                return f.Substring(r.Length + 1).Replace('\\', '/');
            }
            catch { return null; }
        }
    }
}
