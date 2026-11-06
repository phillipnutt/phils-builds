// Phil's Vessel Naming
//
// Names, families and a library for craft and subassemblies, to help roleplay a
// space program. In the VAB and SPH the Load button opens the library (craft by
// category and family, launchers by payload class, other subassemblies), and
// saving a design that isn't in a family yet asks what it is and deals it a
// themed name. Launchers picked from the library name their spent stages.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

[assembly: KSPAssembly("PhilsVesselNaming", 0, 1)]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.1.0")]
[assembly: System.Reflection.AssemblyTitle("Phil's Vessel Naming")]

namespace PhilsVesselNaming
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class VesselNamingEditor : MonoBehaviour
    {
        LibraryWindow window;
        Dialog dialog;
        Button.ButtonClickedEvent ourLoad, ourSave;
        string lastPayloadName;
        string pendingTag;
        int pendingTagFrames, hookCheck;
        bool hooked;

        void Start()
        {
            Look.Init();
            window = new LibraryWindow(this);
            GameEvents.onEditorShipModified.Add(OnShipModified);
            GameEvents.onEditorLoad.Add(OnEditorLoad);
            StartCoroutine(Hook());
        }

        void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnShipModified);
            GameEvents.onEditorLoad.Remove(OnEditorLoad);
            dialog?.Close();
            window?.Close();
            if (Stock.Editor != null && Stock.Editor.shipNameField != null) Stock.Editor.shipNameField.onValueChanged.RemoveListener(OnNameChanged);
        }

        // The editor wires its own buttons up as it starts, so ours go on once it's ready,
        // and are put back if anything replaces them.
        IEnumerator Hook()
        {
            while (Stock.Editor == null || Stock.Editor.loadBtn == null || Stock.Editor.saveBtn == null) yield return null;
            for (int i = 0; i < 5; i++) yield return null;
            HookButtons();
            Stock.HideStockSubassemblies();
            if (Stock.Editor.shipNameField != null) Stock.Editor.shipNameField.onValueChanged.AddListener(OnNameChanged);
            hooked = true;
            // Opening on a craft from a family folder (Revert, a scene change): saves go back there.
            AdoptFolder(EditorDriver.filePathToLoad);
            Debug.Log("[PhilsVesselNaming] Ready: Load opens the library.");
        }

        void HookButtons()
        {
            EditorLogic e = Stock.Editor;
            if (e == null) return;
            if (e.loadBtn != null && e.loadBtn.onClick != ourLoad)
            {
                ourLoad = new Button.ButtonClickedEvent();
                ourLoad.AddListener(() => ToggleWindow());
                e.loadBtn.onClick = ourLoad;
            }
            if (e.saveBtn != null && e.saveBtn.onClick != ourSave)
            {
                ourSave = new Button.ButtonClickedEvent();
                ourSave.AddListener(OnSaveButton);
                e.saveBtn.onClick = ourSave;
            }
        }

        void Update()
        {
            if (hooked && ++hookCheck >= 60)
            {
                hookCheck = 0;
                HookButtons();
                Stock.HideStockSubassemblies();
            }
            if (pendingTag != null)
            {
                Part held = Stock.Held;
                if (held != null)
                {
                    LauncherTags.Tag(held, pendingTag);
                    Category cat = CurrentCategory();
                    LauncherTags.EnsurePayload(Stock.Ship, new System.Collections.Generic.HashSet<Part>(LauncherTags.Subtree(held)), Stock.ShipName,
                        PayloadType(cat), ref lastPayloadName);
                    pendingTag = null;
                }
                else if (--pendingTagFrames <= 0) pendingTag = null;
            }
            window?.Update();
            dialog?.Update();
        }

        void LateUpdate()
        {
            window?.LateUpdate();
            dialog?.LateUpdate();
        }

        // ---- Events -------------------------------------------------------------------------

        void OnShipModified(ShipConstruct ship) => Relabel();

        void OnNameChanged(string name) => Relabel();

        void OnEditorLoad(ShipConstruct ship, CraftBrowserDialog.LoadType type)
        {
            if (type != CraftBrowserDialog.LoadType.Normal) { Relabel(); return; }
            lastPayloadName = null;
            AdoptFolder(EditorDriver.filePathToLoad);
            Relabel();
        }

        // After loading a craft, its own folder is where it saves.
        void AdoptFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (Directory.Exists(dir) && dir.StartsWith(Path.GetFullPath(Library.ShipsDir(Stock.Facility)), StringComparison.OrdinalIgnoreCase))
                    Stock.SetSaveFolder(dir);
            }
            catch { }
        }

        void Relabel()
        {
            try
            {
                Category cat = CurrentCategory();
                LauncherTags.Relabel(Stock.Ship, Stock.ShipName, PayloadType(cat), ref lastPayloadName);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsVesselNaming] Stage names: {e.Message}"); }
        }

        // The vessel type the payload gets: its category's, or whatever it already had.
        static VesselType PayloadType(Category cat)
        {
            if (cat != null) return LauncherTags.TypeOf(cat);
            Part named = Stock.Ship?.parts?.FirstOrDefault(p => p.vesselNaming != null && p.vesselNaming.namingPriority == LauncherTags.PayloadPriority);
            return named != null ? named.vesselNaming.vesselType : VesselType.Ship;
        }

        // ---- Where the open design is filed --------------------------------------------------

        // The category and family folder the editor saves into, if it's a family folder.
        public static bool CurrentFiling(out Category cat, out string family)
        {
            cat = null;
            family = null;
            string folder = Stock.SaveFolder;
            if (string.IsNullOrEmpty(folder)) return false;
            try
            {
                string root = Path.GetFullPath(Library.ShipsDir(Stock.Facility)).TrimEnd('\\', '/');
                string full = Path.GetFullPath(folder).TrimEnd('\\', '/');
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || full.Length <= root.Length) return false;
                string[] parts = full.Substring(root.Length).Trim('\\', '/').Split('\\', '/');
                if (parts.Length != 2) return false;
                cat = Category.ByFolder(parts[0]);
                family = parts[1];
                return cat != null && !cat.Subassembly;
            }
            catch { return false; }
        }

        static Category CurrentCategory() => CurrentFiling(out Category c, out _) ? c : null;

        // ---- The window ----------------------------------------------------------------------

        void ToggleWindow()
        {
            if (dialog != null) return;
            if (window.IsOpen) window.Close();
            else
            {
                window.Open();
                CraftFile current = Library.CraftAt(Stock.CurrentFile);
                if (current != null) { window.Select(current); window.Refresh(); }
            }
        }

        public void CloseWindow() => window.Close();

        public void DialogClosed(Dialog d)
        {
            if (dialog == d) dialog = null;
        }

        void Show(Dialog d)
        {
            dialog?.Close();
            dialog = d;
        }

        void Refresh(CraftFile select = null, SubFile selectSub = null)
        {
            Library.Scan();
            if (!window.IsOpen) return;
            if (select != null) window.Select(select);
            if (selectSub != null) window.Select(selectSub);
            window.Refresh();
        }

        static void Say(string s) => ScreenMessages.PostScreenMessage(s, 4f, ScreenMessageStyle.UPPER_CENTER);

        // ---- Saving ------------------------------------------------------------------------

        // The stock Save button: a filed design saves over its open mark; a new one
        // asks what it is first.
        void OnSaveButton()
        {
            if (dialog != null) return;
            if (Stock.Ship == null || Stock.Ship.parts.Count == 0) { QuickSave(); return; }
            if (CurrentFiling(out _, out _)) QuickSave();
            else { Library.Scan(); Show(new SavePanel(this, null)); }
        }

        // The library's Save… button: the next mark for a filed design, or filing a new one.
        public void OpenSave()
        {
            if (CurrentFiling(out _, out string family) && Stock.Ship != null && Stock.Ship.parts.Count > 0) Show(new NextMarkDialog(this, family));
            else Show(new SavePanel(this, null));
        }

        public void QuickSave()
        {
            Relabel();
            Stock.Save(AfterSave);
        }

        public void SaveAs(string name)
        {
            Stock.ShipName = name;
            Relabel();
            Stock.Save(() => { AfterSave(); Say($"Saved {name}"); });
        }

        public void SaveInto(string folder, string name, Category cat)
        {
            string oldFile = Stock.CurrentFile;
            CraftFile old = Library.CraftAt(oldFile);
            bool moveOld = old != null && !old.Filed && File.Exists(oldFile);
            Directory.CreateDirectory(folder);
            if (!Stock.SetSaveFolder(folder)) throw new IOException("Couldn't use that folder for saving.");
            Stock.ShipName = name;
            lastPayloadName = null;
            Relabel();
            Stock.Save(() =>
            {
                AfterSave();
                if (moveOld && File.Exists(Stock.CurrentFile ?? "") && !string.Equals(Path.GetFullPath(oldFile), Path.GetFullPath(Stock.CurrentFile), StringComparison.OrdinalIgnoreCase))
                {
                    try { Library.DeleteCraft(old); } catch (Exception e) { Debug.LogWarning($"[PhilsVesselNaming] Couldn't move the old copy: {e.Message}"); }
                }
                Refresh(Library.CraftAt(Stock.CurrentFile));
                Say($"Saved {name} in {cat.Label}");
            });
        }

        // Writes down what the save remembers about the craft just saved.
        void AfterSave()
        {
            try
            {
                string file = Stock.CurrentFile;
                if (file == null) return;
                string root = Path.GetFullPath(Library.ShipsDir(Stock.Facility)).TrimEnd('\\', '/');
                string dir = Path.GetDirectoryName(Path.GetFullPath(file));
                string rel = dir.Length > root.Length ? dir.Substring(root.Length).Trim('\\', '/').Replace('\\', '/') : "";
                string key = Library.NoteKey(Stock.Facility, rel, Path.GetFileNameWithoutExtension(file));
                string launcher = LauncherTags.LauncherIn(Stock.Ship);
                Library.Scan();
                Library.SetNote(key, launcher, launcher != null ? LauncherTags.PayloadMass(Stock.Ship) : 0);
                if (window.IsOpen) window.Refresh();
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsVesselNaming] After save: {e.Message}"); }
        }

        public void OpenFile(CraftFile c) => Show(new SavePanel(this, c));

        public void FileCraft(CraftFile c, string folder, string name, Category cat)
        {
            bool isCurrent = SamePath(c.Path, Stock.CurrentFile);
            string dest = Library.MoveCraft(c, folder, name);
            if (isCurrent)
            {
                Stock.SetSaveFolder(folder);
                Stock.ShipName = name;
            }
            Refresh(Library.CraftAt(dest) ?? Library.Craft.FirstOrDefault(x => SamePath(x.Path, dest)));
            Say($"Filed {name} in {cat.Label}");
        }

        static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        public void OpenRename(Family f) { if (f != null) Show(new RenameDialog(this, f)); }

        public void RenameFamily(Family f, string newName)
        {
            string folder = Library.FolderFor(f.Facility, f.Category, newName);
            MarkStyle style = Library.StyleOf(f.Name, f.Craft.FirstOrDefault(c => c.Mark > 0)?.Style ?? MarkStyle.Roman);
            string current = Stock.CurrentFile;
            string select = null;
            foreach (CraftFile c in f.Craft.ToList())
            {
                string n = c.Name.StartsWith(f.Name, StringComparison.OrdinalIgnoreCase) ? newName + c.Name.Substring(f.Name.Length) : c.Name;
                bool isCurrent = SamePath(c.Path, current);
                string dest = Library.MoveCraft(c, folder, n);
                if (isCurrent)
                {
                    Stock.SetSaveFolder(folder);
                    Stock.ShipName = n;
                }
                select = dest;
            }
            Library.SetStyle(newName, style);
            Library.Scan();
            Refresh(Library.CraftAt(select));
            Say($"Renamed {f.Name} to {newName}");
        }

        public void DeleteCraft(CraftFile c)
        {
            Library.DeleteCraft(c);
            Refresh();
            Say($"Moved {c.Name} to Deleted craft");
        }

        // ---- Loading -------------------------------------------------------------------------

        public void LoadCraft(CraftFile c, bool merge, bool force = false)
        {
            if (!merge && !force && Stock.Unsaved) return;     // the window asks first
            window.Close();
            if (merge) Stock.Merge(c.Path);
            else Stock.Load(c.Path);
        }

        // ---- Subassemblies ----------------------------------------------------------------------

        public void PickUp(SubFile s)
        {
            window.Close();
            if (!Stock.PickUp(s.Path)) { Say($"Couldn't pick up {s.Name}"); return; }
            if (s.Kind == SubKind.Launcher)
            {
                pendingTag = s.Name;
                pendingTagFrames = 30;
            }
        }

        public void OpenSubDialog(SubFile existing, Part held, SubKind kind) => Show(new SubDialog(this, existing, held, kind));

        public void SaveSub(SubFile editing, Part held, string name, SubFile info)
        {
            SubFile s = editing;
            if (held != null)
            {
                if (!Stock.SaveSubassembly(name, "", held)) throw new IOException("KSP couldn't save it.");
                Library.Scan();
                s = Library.SubNamed(name);
                if (s == null) throw new IOException("It saved, but under a different name.");
                StripLauncherNames(s.Path);
            }
            else if (s != null && !Naming.SameName(s.Name, name) || s != null && s.Name != name)
            {
                Library.RenameSub(s, name);
            }
            if (s == null) return;
            s.Kind = info.Kind;
            s.Family = info.Family;
            s.Role = info.Role;
            s.Group = info.Group;
            s.Rating = info.Rating;
            s.Width = info.Width;
            s.Notes = info.Notes;
            s.Style = info.Style;
            s.Mark = info.Mark;
            Library.SaveStore();
            EditorPartList.Instance?.RefreshSubassemblies();
            Refresh(selectSub: Library.SubNamed(name));
            Say($"Saved {name}");
        }

        // A launcher saved from a craft that was built on one carries the old stage
        // names; they come off, and it gets fresh ones when it's next picked up.
        static void StripLauncherNames(string path)
        {
            try
            {
                ConfigNode root = ConfigNode.Load(path);
                if (root == null) return;
                bool changed = false;
                foreach (ConfigNode part in root.GetNodes("PART"))
                    foreach (ConfigNode n in part.GetNodes().ToArray())
                        if (n.GetValue("priority") == LauncherTags.LauncherPriority.ToString() && n.GetValue("type") == "Debris" && n.HasValue("name"))
                        {
                            part.RemoveNode(n);
                            changed = true;
                        }
                if (changed) root.Save(path);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsVesselNaming] Couldn't tidy {path}: {e.Message}"); }
        }

        public void SetRating(SubFile s, float tons, float width, string notes)
        {
            bool changed = Math.Abs(s.Rating - tons) > 0.0001f || Math.Abs(s.Width - width) > 0.0001f || (s.Notes ?? "") != (notes ?? "");
            if (!changed) return;
            string oldCode = s.Rating > 0 ? Naming.Code(s.Rating) : null;
            s.Rating = tons;
            s.Width = width;
            s.Notes = notes ?? "";
            // A launcher named for its old rating ("Oak 030") follows the new one.
            if (tons > 0 && s.Family.Length > 0 && oldCode != Naming.Code(tons))
            {
                string expected = oldCode == null ? s.Family : Naming.Launcher(s.Family, s.Rating > 0 ? float.Parse(oldCode) : 0, s.Style, s.Mark);
                if (oldCode == null || Naming.SameName(s.Name, expected) || s.Name.StartsWith(s.Family + " " + oldCode, StringComparison.OrdinalIgnoreCase))
                {
                    string newName = Naming.Launcher(s.Family, tons, s.Style, s.Mark);
                    if (Library.SubNamed(newName) == null)
                    {
                        try { Library.RenameSub(s, newName); EditorPartList.Instance?.RefreshSubassemblies(); }
                        catch (Exception e) { Debug.LogWarning($"[PhilsVesselNaming] Couldn't rename {s.Name}: {e.Message}"); }
                    }
                }
            }
            Library.SaveStore();
            Refresh(selectSub: s);
        }

        public void DeleteSub(SubFile s)
        {
            Library.DeleteSub(s);
            EditorPartList.Instance?.RefreshSubassemblies();
            Refresh();
            Say($"Moved {s.Name} to Deleted craft");
        }
    }
}
