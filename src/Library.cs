// The library: every craft and subassembly in this save, sorted into
// categories, families and marks.
//
// Craft are filed as real folders, Ships/<VAB|SPH>/<Category>/<Family>/<Name>.craft,
// so they stay sorted in the stock load dialog even without this mod. A craft's
// name gives its family and mark ("Haven Lab II": family "Haven Lab", mark 2).
// Subassemblies stay in the flat Subassemblies folder (the only place stock reads
// them from); what kind each one is, and launcher ratings, live in this save's
// PhilsBuilds.cfg.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace PhilsBuilds
{
    public class CraftFile
    {
        public string Path, Facility, RelDir, Name;
        public Category Category;           // null when unfiled
        public string Family;               // the name without its mark; null when unfiled
        public string Base;                 // the name without its mark, filed or not
        public MarkStyle Style = MarkStyle.Roman;
        public int Mark;                    // 0 when the name has no mark
        public DateTime Written;
        public string Description = "";
        public int Parts, Stages;
        public float Mass, Cost;
        public Vector3 Size;
        public bool StatsLoaded;
        public string Id, Line;             // stable ids (filed craft only), from the save's own file
        public bool Filed => Category != null;
    }

    // A family is one design line and its marks: Orpheus I, III, IV. A program
    // built in pieces is several families sharing a name: Evergreen, Evergreen Lab.
    public class Family
    {
        public string Name;
        public Category Category;
        public string Facility;
        public readonly List<CraftFile> Marks = new List<CraftFile>();    // oldest first
        public CraftFile Newest => Marks.LastOrDefault();
        public DateTime Written => Marks.Select(c => c.Written).DefaultIfEmpty().Max();
    }

    public class SubFile
    {
        public string Path, Name;
        public SubKind Kind = SubKind.Unfiled;
        public string Family = "", Group = "", Notes = "";
        public float Rating, Width;         // launchers: tons to orbit, widest payload in meters
        public MarkStyle Style = MarkStyle.Roman;
        public int Mark = 1;
        public DateTime Written;
        public string Description = "";
        public int Parts, Stages;
        public float Mass, Cost;
        public Vector3 Size;
        public bool StatsLoaded;
        public readonly List<string> Contents = new List<string>();     // part titles, most common first
        public string Id, Line;             // stable ids (filed subassemblies only), from the save's own file
        public bool Rated => Kind == SubKind.Launcher && Rating > 0;
        public float Diameter => Mathf.Max(Size.x, Size.z);
    }

    // What the save remembers about a craft beyond its folder and name.
    public class CraftNote
    {
        public string Key;                  // "VAB/Stations/Haven/Haven Lab I"
        public string Launcher = "";
        public float Payload;
    }

    // A design's stable ids: its own, and its design line's (shared by its marks).
    public class DesignIds
    {
        public string Id, Line;
    }

    public static class Library
    {
        public static readonly List<CraftFile> Craft = new List<CraftFile>();
        public static readonly List<Family> Families = new List<Family>();
        public static readonly List<SubFile> Subs = new List<SubFile>();
        public static readonly Dictionary<string, CraftNote> Notes = new Dictionary<string, CraftNote>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, ConfigNode> subNodes = new Dictionary<string, ConfigNode>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, MarkStyle> familyStyles = new Dictionary<string, MarkStyle>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, DesignIds> designIds = new Dictionary<string, DesignIds>(StringComparer.OrdinalIgnoreCase);
        // Launchers renamed (old name -> new) or deleted (name -> ""). Craft built on one
        // still carry its stage names, and they stay this mod's: a renamed launcher's follow
        // its new name, a deleted one's keep the name they had.
        static readonly Dictionary<string, string> formerLaunchers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static int Version { get; private set; }
        static string scannedSave;

        public static string SaveDir => System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder ?? "");
        public static string ShipsDir(string facility) => System.IO.Path.Combine(SaveDir, "Ships", facility);
        public static string SubsDir => System.IO.Path.Combine(SaveDir, "Subassemblies");
        static string StorePath => System.IO.Path.Combine(SaveDir, "PhilsBuilds.cfg");
        public static string DeletedDir => System.IO.Path.Combine(SaveDir, "Deleted craft");

        // ---- Scanning --------------------------------------------------------------

        public static void Scan()
        {
            Craft.Clear();
            Families.Clear();
            Subs.Clear();
            LoadStore();
            try { CraftProfileInfo.PrepareCraftMetaFileLoad(); } catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Craft metadata: {e.Message}"); }

            foreach (string fac in new[] { "VAB", "SPH" })
            {
                string root = ShipsDir(fac);
                if (!Directory.Exists(root)) continue;
                foreach (string file in SafeFiles(root, "*.craft", SearchOption.AllDirectories))
                {
                    try { Craft.Add(ReadCraft(file, fac, root)); }
                    catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Skipped {file}: {e.Message}"); }
                }
            }
            BuildFamilies();

            if (Directory.Exists(SubsDir))
                foreach (string file in SafeFiles(SubsDir, "*.craft", SearchOption.TopDirectoryOnly))
                {
                    try { Subs.Add(ReadSub(file)); }
                    catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Skipped {file}: {e.Message}"); }
                }
            AssignIds();
            scannedSave = HighLogic.SaveFolder;
            Version++;
            BuildsApi.RaiseLibraryChanged();
        }

        // Reads the library if it hasn't been read for this save yet (another mod may ask
        // outside the editor).
        public static void EnsureScanned()
        {
            if (Version == 0 || scannedSave != HighLogic.SaveFolder) Scan();
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern, SearchOption option)
        {
            try { return Directory.GetFiles(dir, pattern, option); }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Can't read {dir}: {e.Message}"); return new string[0]; }
        }

        static CraftFile ReadCraft(string file, string fac, string root)
        {
            var c = new CraftFile
            {
                Path = Full(file),
                Facility = fac,
                Name = System.IO.Path.GetFileNameWithoutExtension(file),
                Written = File.GetLastWriteTime(file),
            };
            string dir = System.IO.Path.GetDirectoryName(Full(file));
            c.RelDir = dir.Length > Full(root).Length ? dir.Substring(Full(root).Length).Trim('\\', '/').Replace('\\', '/') : "";
            string[] parts = c.RelDir.Length == 0 ? new string[0] : c.RelDir.Split('/');

            Category cat = parts.Length >= 1 ? Category.ByFolder(parts[0]) : null;
            // KSP writes "Auto-Saved Ship" into the save folder at every launch; it's
            // never part of a family.
            bool autoSave = string.Equals(c.Name, "Auto-Saved Ship", StringComparison.OrdinalIgnoreCase);
            if (cat != null && !cat.Subassembly && parts.Length <= 2 && !autoSave) c.Category = cat;

            bool hasMark = Marks.TryParse(c.Name, out string rest, out MarkStyle style, out int mark);
            c.Base = hasMark ? rest : c.Name;
            c.Style = style;
            c.Mark = hasMark ? mark : 0;
            // The family is the name without its mark ("Orpheus IV" is Orpheus), whatever
            // the folder inside the category is called.
            if (c.Category != null) c.Family = c.Base;
            return c;
        }

        static void BuildFamilies()
        {
            foreach (IGrouping<string, CraftFile> g in Craft.Where(c => c.Filed)
                         .GroupBy(c => c.Facility + "|" + c.Category.Id + "|" + c.Family, StringComparer.OrdinalIgnoreCase))
            {
                CraftFile first = g.First();
                var f = new Family { Name = first.Family, Category = first.Category, Facility = first.Facility };
                f.Marks.AddRange(g.OrderBy(c => c.Mark).ThenBy(c => c.Written));
                Families.Add(f);
            }
        }

        static SubFile ReadSub(string file)
        {
            var s = new SubFile
            {
                Path = Full(file),
                Name = System.IO.Path.GetFileNameWithoutExtension(file),
                Written = File.GetLastWriteTime(file),
            };
            if (subNodes.TryGetValue(s.Name, out ConfigNode n))
            {
                string k = n.GetValue("kind") ?? "";
                if (k == "Module") k = "Block";             // modules and blocks became one kind
                if (Enum.TryParse(k, out SubKind kind)) s.Kind = kind;
                s.Family = n.GetValue("family") ?? "";
                s.Group = n.GetValue("group") ?? "";
                if (s.Kind == SubKind.Block && s.Group.Length == 0) s.Group = s.Family;
                s.Notes = n.GetValue("notes") ?? "";
                s.Rating = Float(n.GetValue("rating"));
                s.Width = Float(n.GetValue("width"));
                if (Enum.TryParse(n.GetValue("style") ?? "", out MarkStyle st)) s.Style = st;
                int.TryParse(n.GetValue("mark") ?? "1", out s.Mark);
                if (s.Mark < 1) s.Mark = 1;
            }
            return s;
        }

        static float Float(string s) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;

        static string Full(string p) => System.IO.Path.GetFullPath(p);

        // ---- Stats, loaded when first shown ----------------------------------------

        static MethodInfo getSaveData;

        public static void LoadStats(CraftFile c)
        {
            if (c.StatsLoaded) return;
            c.StatsLoaded = true;
            try
            {
                if (getSaveData == null)
                    getSaveData = typeof(CraftProfileInfo).GetMethod("GetSaveData", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                string meta = System.IO.Path.ChangeExtension(c.Path, ".loadmeta");
                var info = getSaveData?.Invoke(null, new object[] { c.Path, meta }) as CraftProfileInfo;
                if (info == null) return;
                c.Parts = info.partCount;
                c.Stages = info.stageCount;
                c.Mass = info.totalMass;
                c.Cost = info.totalCost;
                c.Size = info.shipSize;
                c.Description = Unescape(info.description);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsBuilds] No stats for {c.Name}: {e.Message}");
            }
        }

        public static void LoadStats(SubFile s)
        {
            if (s.StatsLoaded) return;
            s.StatsLoaded = true;
            try
            {
                ShipTemplate t = ShipConstruction.LoadTemplate(s.Path);
                if (t == null) return;
                s.Parts = t.partCount;
                s.Stages = t.stageCount;
                s.Mass = t.totalMass;
                s.Cost = t.totalCost;
                s.Size = t.shipSize;
                s.Description = Unescape(t.shipDescription);
                s.Contents.Clear();
                if (t.config != null)
                    s.Contents.AddRange(t.config.GetNodes("PART")
                        .Select(ShipTemplate.GetPartName)
                        .Select(n => (PartLoader.getPartInfoByName(n) ?? PartLoader.getPartInfoByName(n.Replace('_', '.')))?.title ?? n)
                        .GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsBuilds] No stats for {s.Name}: {e.Message}");
            }
        }

        static string Unescape(string s) => (s ?? "").Replace("¨", "\n").Trim();

        // ---- Thumbnails ------------------------------------------------------------

        static readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();

        public static Texture2D Thumbnail(CraftFile c)
        {
            string key = c.Path + "|" + c.Written.Ticks;
            if (thumbs.TryGetValue(key, out Texture2D t)) return t;
            t = null;
            try
            {
                string name = ShipConstruction.GetPlayerCraftThumbnailName(System.IO.Path.GetDirectoryName(c.Path), c.Name);
                string file = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "thumbs", name + ".png");
                if (File.Exists(file))
                {
                    t = new Texture2D(2, 2, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (!t.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.Destroy(t); t = null; }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsBuilds] No thumbnail for {c.Name}: {e.Message}");
                t = null;
            }
            return thumbs[key] = t;
        }

        // The thumbnail file stock would use for a craft saved at `folder` as `name`.
        public static string ThumbFile(string folder, string name)
        {
            string n = ShipConstruction.GetPlayerCraftThumbnailName(folder, name);
            return System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "thumbs", n + ".png");
        }

        // ---- Lookups ---------------------------------------------------------------

        // Every name this save already uses: family names, craft and subassembly names.
        public static HashSet<string> UsedNames()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Family f in Families) used.Add(f.Name);
            foreach (CraftFile c in Craft) used.Add(c.Base);
            foreach (SubFile s in Subs)
            {
                if (!string.IsNullOrEmpty(s.Family)) used.Add(s.Family);
                used.Add(s.Name);
            }
            return used;
        }

        public static CraftFile CraftAt(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string full;
            try { full = Full(path); } catch { return null; }
            return Craft.FirstOrDefault(c => string.Equals(c.Path, full, StringComparison.OrdinalIgnoreCase));
        }

        public static Family FamilyOf(CraftFile c) =>
            c == null || !c.Filed ? null : Families.FirstOrDefault(f => f.Category == c.Category && f.Facility == c.Facility &&
                                                                         string.Equals(f.Name, c.Family, StringComparison.OrdinalIgnoreCase));


        public static SubFile SubNamed(string name) => Subs.FirstOrDefault(s => Naming.SameName(s.Name, name));

        // A launcher or upper stage of this name.
        public static SubFile StageNamed(string name) =>
            string.IsNullOrEmpty(name) ? null : Subs.FirstOrDefault(s => (s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage) && Naming.SameName(s.Name, name));

        public static bool HasLauncher(string name) => Subs.Any(s => s.Kind == SubKind.Launcher && Naming.SameName(s.Name, name));

        // A launcher of this name was in the library and has been renamed or deleted.
        public static bool WasLauncher(string name) => !string.IsNullOrEmpty(name) && formerLaunchers.ContainsKey(name.Trim());

        // What a launcher is called now: a renamed one's latest name; any other name as it is
        // (a launcher in the library, a deleted one, or a name this mod never wrote).
        public static string LauncherNow(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = name.Trim();
            for (int i = 0; i < 20 && !HasLauncher(name) && formerLaunchers.TryGetValue(name, out string now) && now.Length > 0; i++) name = now;
            return name;
        }

        public static string NoteKey(CraftFile c) => NoteKey(c.Facility, c.RelDir, c.Name);

        public static string NoteKey(string facility, string relDir, string name) =>
            facility + "/" + (string.IsNullOrEmpty(relDir) ? "" : relDir + "/") + name;

        public static CraftNote NoteFor(CraftFile c) => c != null && Notes.TryGetValue(NoteKey(c), out CraftNote n) ? n : null;

        public static IEnumerable<CraftFile> FlownOn(SubFile launcher) =>
            Craft.Where(c => NoteFor(c) is CraftNote n && Naming.SameName(n.Launcher, launcher.Name));

        public static MarkStyle StyleOf(string family, MarkStyle fallback) =>
            familyStyles.TryGetValue(family ?? "", out MarkStyle s) ? s : fallback;

        public static void SetStyle(string family, MarkStyle style)
        {
            familyStyles[family] = style;
            SaveStore();
        }

        public static string FolderFor(string facility, Category cat, string family) =>
            System.IO.Path.Combine(ShipsDir(facility), cat.Folder, Naming.Clean(family));

        // ---- Stable ids ------------------------------------------------------------------

        // Paths change (a family renamed, a craft filed, a launcher re-rated from Oak 030
        // to Oak 060), so each design gets ids of its own, kept in the save's file and
        // carried along by every move and rename here. New designs get theirs the first
        // time they're seen; a new mark joins its line's id.
        public static string KeyOf(CraftFile c) => "craft:" + NoteKey(c);
        public static string KeyOf(SubFile s) => "sub:" + s.Name;
        static string CraftKey(string facility, string relDir, string name) => "craft:" + NoteKey(facility, relDir, name);

        static string LineKey(CraftFile c) => $"craft|{c.Facility}|{c.Category.Id}|{c.Family}";

        static string LineKey(SubFile s)
        {
            string fam = string.IsNullOrEmpty(s.Family) ? s.Name : s.Family;
            switch (s.Kind)
            {
                case SubKind.Launcher: return $"launcher|{fam}|{(s.Rated ? Naming.Code(s.Rating) : "")}";
                case SubKind.UpperStage: return "upper|" + fam;
                default: return "block|" + s.Name;
            }
        }

        static string NewId() => Guid.NewGuid().ToString("N");

        static void AssignIds()
        {
            bool changed = false;
            var lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<CraftFile> craft = Craft.Where(c => c.Filed).ToList();
            List<SubFile> subs = Subs.Where(s => s.Kind != SubKind.Unfiled).ToList();
            foreach (CraftFile c in craft)
                if (designIds.TryGetValue(KeyOf(c), out DesignIds known) && !lines.ContainsKey(LineKey(c))) lines[LineKey(c)] = known.Line;
            foreach (SubFile s in subs)
                if (designIds.TryGetValue(KeyOf(s), out DesignIds known) && !lines.ContainsKey(LineKey(s))) lines[LineKey(s)] = known.Line;

            DesignIds Get(string key, string lineKey)
            {
                if (designIds.TryGetValue(key, out DesignIds found)) return found;
                if (!lines.TryGetValue(lineKey, out string line)) lines[lineKey] = line = NewId();
                changed = true;
                return designIds[key] = new DesignIds { Id = NewId(), Line = line };
            }

            foreach (CraftFile c in craft)
            {
                DesignIds ids = Get(KeyOf(c), LineKey(c));
                c.Id = ids.Id;
                c.Line = ids.Line;
            }
            foreach (SubFile s in subs)
            {
                DesignIds ids = Get(KeyOf(s), LineKey(s));
                s.Id = ids.Id;
                s.Line = ids.Line;
            }
            if (changed) SaveStore();
        }

        static bool MoveIds(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || !designIds.TryGetValue(from, out DesignIds ids)) return false;
            designIds.Remove(from);
            designIds[to] = ids;
            return true;
        }

        // ---- The save's own file -----------------------------------------------------

        static void LoadStore()
        {
            subNodes.Clear();
            Notes.Clear();
            familyStyles.Clear();
            designIds.Clear();
            formerLaunchers.Clear();
            if (!File.Exists(StorePath)) return;
            try
            {
                ConfigNode root = ConfigNode.Load(StorePath);
                ConfigNode n = root?.GetNode("PHILS_BUILDS") ?? root;
                if (n == null) return;
                foreach (ConfigNode s in n.GetNodes("SUBASSEMBLY"))
                {
                    string file = s.GetValue("file");
                    if (!string.IsNullOrEmpty(file)) subNodes[file] = s;
                }
                foreach (ConfigNode c in n.GetNodes("CRAFT"))
                {
                    string key = c.GetValue("key");
                    if (string.IsNullOrEmpty(key)) continue;
                    Notes[key] = new CraftNote { Key = key, Launcher = c.GetValue("launcher") ?? "", Payload = Float(c.GetValue("payload")) };
                }
                foreach (ConfigNode f in n.GetNodes("FAMILY"))
                    if (Enum.TryParse(f.GetValue("style") ?? "", out MarkStyle st) && !string.IsNullOrEmpty(f.GetValue("name")))
                        familyStyles[f.GetValue("name")] = st;
                foreach (ConfigNode d in n.GetNodes("DESIGN"))
                {
                    string key = d.GetValue("key"), id = d.GetValue("id"), line = d.GetValue("line");
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(id)) designIds[key] = new DesignIds { Id = id, Line = string.IsNullOrEmpty(line) ? id : line };
                }
                foreach (ConfigNode f in n.GetNodes("FORMER_LAUNCHER"))
                {
                    string name = f.GetValue("name");
                    if (!string.IsNullOrEmpty(name)) formerLaunchers[name] = f.GetValue("now") ?? "";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't read {StorePath}: {e.Message}");
            }
        }

        public static void SaveStore()
        {
            try
            {
                var root = new ConfigNode();
                ConfigNode n = root.AddNode("PHILS_BUILDS");
                foreach (SubFile s in Subs.Where(x => x.Kind != SubKind.Unfiled).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ConfigNode c = n.AddNode("SUBASSEMBLY");
                    c.AddValue("file", s.Name);
                    c.AddValue("kind", s.Kind.ToString());
                    if (s.Family.Length > 0) c.AddValue("family", s.Family);
                    if (s.Group.Length > 0) c.AddValue("group", s.Group);
                    if (s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage)
                    {
                        c.AddValue("style", s.Style.ToString());
                        c.AddValue("mark", s.Mark);
                    }
                    if (s.Kind == SubKind.Launcher)
                    {
                        c.AddValue("rating", s.Rating.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                        c.AddValue("width", s.Width.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    if (s.Notes.Length > 0) c.AddValue("notes", SafeValue(s.Notes));
                }
                // Keep nodes for subassemblies that are missing right now (a file moved away
                // for a while shouldn't lose its rating).
                foreach (KeyValuePair<string, ConfigNode> kv in subNodes)
                    if (!Subs.Any(s => Naming.SameName(s.Name, kv.Key))) n.AddNode(kv.Value);
                foreach (CraftNote note in Notes.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ConfigNode c = n.AddNode("CRAFT");
                    c.AddValue("key", note.Key);
                    if (note.Launcher.Length > 0) c.AddValue("launcher", note.Launcher);
                    if (note.Payload > 0) c.AddValue("payload", note.Payload.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                }
                foreach (KeyValuePair<string, MarkStyle> kv in familyStyles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ConfigNode c = n.AddNode("FAMILY");
                    c.AddValue("name", kv.Key);
                    c.AddValue("style", kv.Value.ToString());
                }
                foreach (KeyValuePair<string, DesignIds> kv in designIds.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ConfigNode c = n.AddNode("DESIGN");
                    c.AddValue("key", kv.Key);
                    c.AddValue("id", kv.Value.Id);
                    c.AddValue("line", kv.Value.Line);
                }
                foreach (KeyValuePair<string, string> kv in formerLaunchers.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ConfigNode c = n.AddNode("FORMER_LAUNCHER");
                    c.AddValue("name", kv.Key);
                    if (kv.Value.Length > 0) c.AddValue("now", kv.Value);
                }
                Directory.CreateDirectory(SaveDir);
                root.Save(StorePath);
                // Refresh the remembered nodes from what was just written.
                subNodes.Clear();
                foreach (ConfigNode s in n.GetNodes("SUBASSEMBLY")) subNodes[s.GetValue("file")] = s;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't write {StorePath}: {e.Message}");
            }
        }

        // Config values can't hold braces or "//" (KSP reads those as structure and comments).
        static string SafeValue(string s) => s.Replace("{", "(").Replace("}", ")").Replace("//", "/").Replace("\n", " ").Replace("\r", "");

        public static void Remember(SubFile s)
        {
            SaveStore();
            Version++;
        }

        public static void SetNote(string key, string launcher, float payload)
        {
            if (string.IsNullOrEmpty(launcher) && payload <= 0) Notes.Remove(key);
            else Notes[key] = new CraftNote { Key = key, Launcher = launcher ?? "", Payload = payload };
            SaveStore();
        }

        // ---- Moving files around ---------------------------------------------------

        // Moves a craft (and its .loadmeta and thumbnail) to a new folder and name,
        // and sets the name inside the file to match.
        public static string MoveCraft(CraftFile c, string folder, string newName)
        {
            newName = Naming.Clean(newName);
            Directory.CreateDirectory(folder);
            string dest = System.IO.Path.Combine(folder, newName + ".craft");
            if (string.Equals(Full(dest), c.Path, StringComparison.OrdinalIgnoreCase)) return dest;
            if (File.Exists(dest)) throw new IOException($"\"{newName}\" already exists there.");

            ConfigNode node = ConfigNode.Load(c.Path);
            if (node == null) throw new IOException($"Couldn't read {c.Name}.");
            node.SetValue("ship", newName, true);
            node.Save(dest);
            string oldThumb = ThumbFile(System.IO.Path.GetDirectoryName(c.Path), c.Name);
            string newThumb = ThumbFile(folder, newName);
            try { if (File.Exists(oldThumb) && !File.Exists(newThumb)) File.Copy(oldThumb, newThumb); } catch { }
            RemoveCraftFiles(c.Path, oldThumb);

            // Carry the save's note and the design's ids over to the new place.
            string fac = c.Facility;
            string rel = System.IO.Path.GetDirectoryName(Full(dest)).Substring(Full(ShipsDir(fac)).Length).Trim('\\', '/').Replace('\\', '/');
            bool moved = MoveIds(KeyOf(c), CraftKey(fac, rel, newName));
            CraftNote note = NoteFor(c);
            if (note != null)
            {
                Notes.Remove(note.Key);
                note.Key = NoteKey(fac, rel, newName);
                Notes[note.Key] = note;
                moved = true;
            }
            if (moved) SaveStore();
            RemoveEmptyFolders(System.IO.Path.GetDirectoryName(c.Path), c.Facility);
            return dest;
        }

        static void RemoveCraftFiles(string craftPath, string thumb)
        {
            File.Delete(craftPath);
            string meta = System.IO.Path.ChangeExtension(craftPath, ".loadmeta");
            try { if (File.Exists(meta)) File.Delete(meta); } catch { }
            try { if (thumb != null && File.Exists(thumb)) File.Delete(thumb); } catch { }
        }

        // Folders a family leaves empty go too, up to the Ships/<VAB|SPH> folder.
        static void RemoveEmptyFolders(string dir, string facility)
        {
            string stop = Full(ShipsDir(facility)).TrimEnd('\\', '/');
            try
            {
                while (dir != null && Full(dir).TrimEnd('\\', '/').Length > stop.Length &&
                       Full(dir).StartsWith(stop, StringComparison.OrdinalIgnoreCase) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    dir = System.IO.Path.GetDirectoryName(dir);
                }
            }
            catch { }
        }

        // "Delete" moves a craft into the save's Deleted craft folder, out of every list.
        public static void DeleteCraft(CraftFile c)
        {
            string dest = System.IO.Path.Combine(DeletedDir, c.Facility, string.IsNullOrEmpty(c.RelDir) ? "" : c.RelDir.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dest);
            string target = System.IO.Path.Combine(dest, c.Name + ".craft");
            for (int i = 2; File.Exists(target); i++) target = System.IO.Path.Combine(dest, $"{c.Name} ({i}).craft");
            File.Move(c.Path, target);
            string meta = System.IO.Path.ChangeExtension(c.Path, ".loadmeta");
            try { if (File.Exists(meta)) File.Delete(meta); } catch { }
            Notes.Remove(NoteKey(c));
            designIds.Remove(KeyOf(c));
            SaveStore();
            RemoveEmptyFolders(System.IO.Path.GetDirectoryName(c.Path), c.Facility);
        }

        public static void DeleteSub(SubFile s)
        {
            string dest = System.IO.Path.Combine(DeletedDir, "Subassemblies");
            Directory.CreateDirectory(dest);
            string target = System.IO.Path.Combine(dest, s.Name + ".craft");
            for (int i = 2; File.Exists(target); i++) target = System.IO.Path.Combine(dest, $"{s.Name} ({i}).craft");
            File.Move(s.Path, target);
            Thumbs.Delete(s.Name);
            if (s.Kind == SubKind.Launcher) formerLaunchers[s.Name] = "";
            Subs.Remove(s);
            subNodes.Remove(s.Name);
            designIds.Remove(KeyOf(s));
            SaveStore();
        }

        // Renames a subassembly file (and the name inside it).
        public static void RenameSub(SubFile s, string newName)
        {
            newName = Naming.Clean(newName);
            if (Naming.SameName(newName, s.Name) && newName == s.Name) return;
            string dest = System.IO.Path.Combine(SubsDir, newName + ".craft");
            if (!Naming.SameName(newName, s.Name) && File.Exists(dest)) throw new IOException($"\"{newName}\" already exists.");
            ConfigNode node = ConfigNode.Load(s.Path);
            if (node == null) throw new IOException($"Couldn't read {s.Name}.");
            node.SetValue("ship", newName, true);
            string tmp = dest + ".tmp";
            node.Save(tmp);
            File.Delete(s.Path);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
            subNodes.Remove(s.Name);
            // Craft notes that name this launcher follow the rename.
            foreach (CraftNote n in Notes.Values.Where(n => Naming.SameName(n.Launcher, s.Name))) n.Launcher = newName;
            if (s.Kind == SubKind.Launcher && !Naming.SameName(s.Name, newName))
            {
                formerLaunchers[s.Name] = newName;
                formerLaunchers.Remove(newName);
            }
            MoveIds(KeyOf(s), "sub:" + newName);
            Thumbs.Rename(s.Name, newName);
            s.Name = newName;
            s.Path = Full(dest);
            SaveStore();
        }
    }
}
