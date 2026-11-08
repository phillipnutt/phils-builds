// Phil's Builds
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

[assembly: KSPAssembly("PhilsBuilds", 0, 1)]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.1.0")]
[assembly: System.Reflection.AssemblyTitle("Phil's Builds")]

namespace PhilsBuilds
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class BuildsEditor : MonoBehaviour
    {
        public static BuildsEditor Instance { get; private set; }

        LibraryWindow window;
        Dialog dialog;
        Button.ButtonClickedEvent ourLoad, ourSave;
        string lastPayloadName;
        int hookCheck;
        bool hooked;
        // A load or merge from the library, waiting for the editor to say it's done.
        Design placing;
        PlaceHow placingHow;
        int placingFrames;
        SubFile loadingSub;     // a launcher or upper stage being opened as the craft

        void Awake() => Instance = this;

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
            if (Instance == this) Instance = null;
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
            Debug.Log("[PhilsBuilds] Ready: Load opens the library.");
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
            if ((placing != null || loadingSub != null) && --placingFrames <= 0) { placing = null; loadingSub = null; }
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
            SubFile opened = null;
            if (loadingSub != null && type == CraftBrowserDialog.LoadType.Normal && ship?.parts != null && ship.parts.Count > 0)
            {
                opened = loadingSub;
                loadingSub = null;
                OpenedSub(ship, opened);
            }
            // The editor's parts come in file order, so they're the design's slots.
            if (placing != null && ship?.parts != null &&
                placingHow == (type == CraftBrowserDialog.LoadType.Merge ? PlaceHow.Merge : PlaceHow.Load))
            {
                Design d = placing;
                placing = null;
                BuildsApi.RaisePlaced(d, placingHow, ship.parts);
            }
            if (type != CraftBrowserDialog.LoadType.Normal) { Relabel(); return; }
            lastPayloadName = null;
            AdoptFolder(EditorDriver.filePathToLoad);
            Relabel();
            // KSP took its undo step as the file loaded: before the lift and the stage names.
            if (opened != null) Stock.RetakeUndoStep();
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
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Stage names: {e.Message}"); }
        }

        // The vessel type the payload gets: its category's, or whatever it already had.
        static VesselType PayloadType(Category cat)
        {
            if (cat != null) return LauncherTags.TypeOf(cat);
            return LauncherTags.PayloadTypeIn(Stock.Ship) ?? VesselType.Ship;
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

        public void RefreshWindow()
        {
            if (window != null && window.IsOpen) window.Refresh();
        }

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
            Library.Scan();
            if (!CurrentFiling(out _, out string family)) Show(new SavePanel(this, null));
            // A name typed in the box that isn't a mark of this family ("Orpheus" in the
            // Orpheus I family) would save a stray file into the folder, so ask instead.
            else if (!FitsFamily(Stock.ShipName, family))
                Show(new SavePanel(this, null, $"\"{Stock.ShipName}\" isn't a mark of the {family} family, so where should it go?"));
            // Phil: Save always asks, over the open mark or as the next one.
            else Show(new NextMarkDialog(this, family));
        }

        // A name fits the open family when it's a mark of the family, or a mark of a
        // member already saved in this folder (the folder's name can differ, like the
        // "Orpheus I" folder holding Orpheus III).
        static bool FitsFamily(string name, string family)
        {
            if (!Marks.TryParse(name, out string rest, out _, out _)) return false;
            if (Naming.InFamily(rest, family)) return true;
            string folder = Stock.SaveFolder;
            return Library.Craft.Any(c => Naming.SameName(c.Base, rest) && c.Filed && SamePath(Path.GetDirectoryName(c.Path), folder));
        }

        // The library's Save… button: the next mark for a filed design, or filing a new one.
        public void OpenSave()
        {
            Library.Scan();
            if (CurrentFiling(out _, out string family) && Stock.Ship != null && Stock.Ship.parts.Count > 0 && FitsFamily(Stock.ShipName, family))
                Show(new NextMarkDialog(this, family));
            else Show(new SavePanel(this, null));
        }

        public void QuickSave()
        {
            Relabel();
            bool existed = FileExists(Stock.CurrentFile);
            Stock.Save(() => AfterSave(existed));
        }

        public void SaveAs(string name)
        {
            Stock.ShipName = name;
            Relabel();
            bool existed = FileExists(Stock.CurrentFile);
            Stock.Save(() => { AfterSave(existed); Say($"Saved {name}"); });
        }

        static bool FileExists(string path) => !string.IsNullOrEmpty(path) && File.Exists(path);

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
            bool existed = FileExists(Stock.CurrentFile);
            Stock.Save(() =>
            {
                AfterSave(existed);
                if (moveOld && File.Exists(Stock.CurrentFile ?? "") && !string.Equals(Path.GetFullPath(oldFile), Path.GetFullPath(Stock.CurrentFile), StringComparison.OrdinalIgnoreCase))
                {
                    try { Library.DeleteCraft(old); } catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't move the old copy: {e.Message}"); }
                }
                Refresh(Library.CraftAt(Stock.CurrentFile));
                Say($"Saved {name} in {cat.Label}");
            });
        }

        // Writes down what the save remembers about the craft just saved, and tells
        // anyone listening which parts went into it.
        void AfterSave(bool existedBefore)
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
                // Stock writes the editor's parts in their list order.
                if (Stock.Ship?.parts != null) BuildsApi.RaiseSaved(BuildsApi.OfFile(file), !existedBefore, Stock.Ship.parts);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] After save: {e.Message}"); }
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
            MarkStyle style = Library.StyleOf(f.Name, f.Marks.FirstOrDefault(c => c.Mark > 0)?.Style ?? MarkStyle.Roman);
            string current = Stock.CurrentFile;
            string select = null;
            foreach (CraftFile c in f.Marks.ToList())
            {
                string n = Naming.SwapFamily(c.Name, f.Name, newName);
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
            placing = BuildsApi.OfFile(c.Path);                 // null for an unfiled craft
            placingHow = merge ? PlaceHow.Merge : PlaceHow.Load;
            placingFrames = 30;
            if (merge) Stock.Merge(c.Path);
            else Stock.Load(c.Path);
        }

        // ---- Subassemblies ----------------------------------------------------------------------

        // A whole craft on the cursor would be deleted by picking something else up.
        static bool HandsFull()
        {
            if (!Stock.HoldingCraft || Stock.Empty) return false;
            Say("Put down the craft you're holding first");
            return true;
        }

        public void PickUp(SubFile s)
        {
            if (HandsFull()) return;
            window.Close();
            Design d = BuildsApi.OfFile(s.Path);
            string launcher = s.Kind == SubKind.Launcher ? s.Name : null;
            string name = IsStage(s) && Stock.Empty ? s.Name : null;
            if (!Stock.PickUp(s.Path, sc =>
                {
                    // A subassembly saved before the library took pictures gets one now.
                    if (!Thumbs.Has(s.Name)) Thumbs.Take(sc, s.Name);
                    Spawned(sc, d, PlaceHow.PickUp, launcher, null, name);
                }))
                Say($"Couldn't pick up {s.Name}");
        }

        static bool IsStage(SubFile s) => s != null && (s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage);

        // A launcher or upper stage opened as the whole craft, on an empty editor (Phil:
        // a launcher on its own should load, not only pick up). It's named after itself,
        // its stages are named as a picked-up launcher's are, and it saves as a new design.
        public void LoadSub(SubFile s)
        {
            if (s == null) return;
            if (!IsStage(s) || !Stock.Empty) { PickUp(s); return; }
            if (HandsFull()) return;
            window.Close();
            placing = BuildsApi.OfFile(s.Path);
            placingHow = PlaceHow.Load;
            placingFrames = 30;
            loadingSub = s;
            Stock.Load(s.Path);
        }

        void OpenedSub(ShipConstruct ship, SubFile s)
        {
            try
            {
                if (!Thumbs.Has(s.Name)) Thumbs.Take(ship, s.Name);
                float lift = Stock.LiftOffFloor(ship);
                if (lift > 0) Debug.Log($"[PhilsBuilds] Lifted {s.Name} {lift:0.00} m onto the floor (floor at {Stock.Floor:0.00} m)");
                if (s.Kind == SubKind.Launcher) LauncherTags.Tag(ship.parts[0].localRoot, s.Name);
                // Saves go to the facility's own folder until it's filed, not into the
                // Subassemblies folder it came from or a family folder left open before.
                Stock.SetSaveFolder(Library.ShipsDir(Stock.Facility));
                Stock.ShipName = s.Name;
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Opening {s.Name}: {e.Message}"); }
        }

        // A hangar copy from the window: New picks up the design itself; anything else
        // is built by the mod that keeps the hangar, then held here.
        public void PickUpCopy(Design d, CopyRow row)
        {
            if (d == null || row == null || row.Reserved) return;
            if (row.IsNew)
            {
                SubFile s = Library.Subs.FirstOrDefault(x => SamePath(x.Path, d.Path));
                CraftFile c = s == null ? Library.CraftAt(d.Path) : null;
                if (s != null) PickUp(s);
                else if (c != null) LoadCraft(c, merge: true);
                return;
            }
            ICopies copies = BuildsApi.Copies;
            if (copies == null || HandsFull()) return;
            window.Close();
            string name = (d.Kind == DesignKind.Launcher || d.Kind == DesignKind.UpperStage) && Stock.Empty ? d.Name : null;
            try
            {
                copies.Build(d, row, sc =>
                {
                    if (this == null || sc == null || !Stock.Hold(sc)) return;
                    Spawned(sc, d, PlaceHow.Copy, d.Kind == DesignKind.Launcher ? d.Name : null, row.Id, name);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't build {d.Name} {row.Label}: {e}");
                Say($"Couldn't build {d.Name} {row.Label}");
            }
        }

        // Parts are on the cursor (or are the craft, on an empty editor): name a
        // launcher's spent stages, and say where the parts came from. A stage picked into
        // an empty editor gives the craft its name, if it hasn't one yet.
        void Spawned(ShipConstruct sc, Design d, PlaceHow how, string launcher, string copyId, string shipName = null)
        {
            if (shipName != null && Stock.IsDefaultName(Stock.ShipName)) Stock.ShipName = shipName;
            if (launcher != null)
            {
                Part root = Stock.Held ?? sc.parts[0].localRoot;
                LauncherTags.Tag(root, launcher);
                LauncherTags.EnsurePayload(Stock.Ship, new System.Collections.Generic.HashSet<Part>(LauncherTags.Subtree(root)), Stock.ShipName,
                    PayloadType(CurrentCategory()), ref lastPayloadName);
            }
            if (d != null) BuildsApi.RaisePlaced(d, how, sc.parts, copyId);
        }

        public void OpenSubDialog(SubFile existing, Part held, SubKind kind, bool wholeCraft = false) =>
            Show(new SubDialog(this, existing, held, kind, wholeCraft));

        public void SaveSub(SubFile editing, Part held, string name, SubFile info)
        {
            SubFile s = editing;
            System.Collections.Generic.List<Part> saved = null;
            bool existed = FileExists(Path.Combine(Library.SubsDir, name + ".craft"));
            bool stage = info.Kind == SubKind.Launcher || info.Kind == SubKind.UpperStage;
            if (held != null)
            {
                saved = Stock.SaveSubassembly(name, "", held);
                if (saved == null) throw new IOException("KSP couldn't save it.");
                Library.Scan();
                s = Library.SubNamed(name);
                if (s == null) throw new IOException("It saved, but under a different name.");
                StripLauncherNames(s.Path, stage);
            }
            else if (s != null && !Naming.SameName(s.Name, name) || s != null && s.Name != name)
            {
                Library.RenameSub(s, name);
            }
            if (s == null) return;
            s.Kind = info.Kind;
            s.Family = info.Family;
            s.Group = info.Group;
            s.Rating = info.Rating;
            s.Width = info.Width;
            s.Notes = info.Notes;
            s.Style = info.Style;
            s.Mark = info.Mark;
            Library.SaveStore();
            EditorPartList.Instance?.RefreshSubassemblies();
            string path = s.Path;
            Refresh(selectSub: Library.SubNamed(name));         // reads the library again, with this one's kind
            if (held != null && info.Kind == SubKind.Launcher) BecomeLauncher(held, name);
            Say($"Saved {name}");
            if (saved == null) return;
            BuildsApi.RaiseSaved(BuildsApi.OfFile(path), !existed, saved);
            // Listeners may have written to the parts just saved (the Reusability Suite
            // stamps its design id on them), so the file is written again to match.
            if (BuildsApi.HasSavedListeners && Stock.SaveSubassembly(name, "", held, picture: false) != null) StripLauncherNames(path, stage);
        }

        // The parts just saved as a launcher are that launcher now, though they may have
        // come from another one (deleted, or still in the library): they get its stage names,
        // and a craft that's only this launcher, still named after the old one, takes its name.
        void BecomeLauncher(Part held, string launcher)
        {
            try
            {
                var tree = new System.Collections.Generic.HashSet<Part>(LauncherTags.Subtree(held));
                bool whole = Stock.HoldingCraft || (Stock.Ship?.parts?.All(tree.Contains) ?? true);
                LauncherTags.Tag(held, launcher);
                if (whole && (Stock.IsDefaultName(Stock.ShipName) || LauncherTags.Known(Stock.ShipName))) Stock.ShipName = launcher;
                Relabel();
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Naming {launcher}'s stages: {e.Message}"); }
        }

        // A subassembly saved from a craft that was built on a library launcher carries
        // its stage names; they come off, and a launcher gets fresh ones when it's next
        // picked up. Only the parts' VESSELNAMING nodes are looked at, and only names this
        // mod writes: stage names of launchers in the library (or that were), and (saving a
        // launcher or upper stage) any stage-shaped name at a stage's priority, whichever
        // launcher it came from, and the craft name a lone launcher's top part carried.
        static void StripLauncherNames(string path, bool stage)
        {
            try
            {
                ConfigNode root = ConfigNode.Load(path);
                if (root == null) return;
                bool changed = false;
                string label = LauncherTags.LauncherPriority.ToString(), old = LauncherTags.OldLauncherPriority.ToString(), top = LauncherTags.RootPriority.ToString();
                ConfigNode[] parts = root.GetNodes("PART");
                for (int i = 0; i < parts.Length; i++)
                    foreach (ConfigNode n in parts[i].GetNodes("VESSELNAMING"))
                    {
                        string pri = n.GetValue("priority"), name = n.GetValue("name");
                        bool ours = (pri == label || pri == old) && (LauncherTags.IsLabelName(name) || stage && LauncherTags.LooksLikeLabel(name)) ||
                                    stage && i == 0 && pri == top && n.GetValue("type") != "Debris";
                        if (!ours) continue;
                        parts[i].RemoveNode(n);
                        changed = true;
                    }
                if (changed) root.Save(path);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't tidy {path}: {e.Message}"); }
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
                        catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't rename {s.Name}: {e.Message}"); }
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
