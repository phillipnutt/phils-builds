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
        CraftFile mergingWhole; // a craft merged into an empty editor: it's the whole craft

        void Awake() => Instance = this;

        void Start()
        {
            Look.Init();
            window = new LibraryWindow(this);
            GameEvents.onEditorShipModified.Add(OnShipModified);
            GameEvents.onEditorLoad.Add(OnEditorLoad);
            GameEvents.onGameSceneLoadRequested.Add(OnLeaving);
            StartCoroutine(Hook());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.onEditorShipModified.Remove(OnShipModified);
            GameEvents.onEditorLoad.Remove(OnEditorLoad);
            GameEvents.onGameSceneLoadRequested.Remove(OnLeaving);
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
            Stock.ShutSaveFolders();
            if (Stock.Editor.shipNameField != null) Stock.Editor.shipNameField.onValueChanged.AddListener(OnNameChanged);
            hooked = true;
            OpeningFolder();
            Debug.Log("[PhilsBuilds] Ready: Load opens the library.");
        }

        // Where saves were going as the editor last closed, and on which craft.
        static string leftSave, leftFolder, leftShip;

        void OnLeaving(GameScenes to)
        {
            bool ok = hooked && Stock.Editor != null;
            leftSave = ok ? HighLogic.SaveFolder : null;
            leftFolder = ok ? Stock.SaveFolder : null;
            leftShip = ok ? Stock.ShipName : null;
        }

        // Where saves go as the editor opens (S9, 2026-11-13). Phil: coming back to the editor
        // with the same craft (Revert to the editor, the end of a KRASH sim, back in from the
        // space center: KSP's cached craft), Save treats it exactly as before you left. A craft
        // opened from a file saves to that file's folder (AdoptFolder), and so does one that
        // was launched from a filed craft file (the launch pad's own craft list, then Revert).
        // Anything else, a new craft or a different one, saves to the building's own folder.
        void OpeningFolder()
        {
            var how = EditorDriver.StartupBehaviour;
            if (how == EditorDriver.StartupBehaviours.LOAD_FROM_FILE) { AdoptFolder(EditorDriver.filePathToLoad); return; }
            string folder = Library.ShipsDir(Stock.Facility), why = "";
            if (how == EditorDriver.StartupBehaviours.LOAD_FROM_CACHE)
            {
                if (leftShip != null && leftSave == HighLogic.SaveFolder && Naming.SameName(leftShip, Stock.ShipName) &&
                    Directory.Exists(leftFolder) && Library.InShips(leftFolder) != null)
                { folder = leftFolder; why = ", the craft it closed on"; }
                else if (LaunchedFrom() is CraftFile c)
                { folder = Path.GetDirectoryName(c.Path); why = ", launched from " + c.Path; }
            }
            Stock.SetSaveFolder(folder);
            Debug.Log($"[PhilsBuilds] Editor opened ({how}{why}): saves go to {Stock.SaveFolder}");
        }

        // The filed craft file KSP last launched, when it's the craft open now.
        static CraftFile LaunchedFrom()
        {
            string path = FlightDriver.newShipToLoadPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            return Library.CraftAt(path) is CraftFile c && c.Filed && Naming.SameName(c.Name, KSPUtil.SanitizeString(Stock.ShipName, '_', true)) ? c : null;
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
                Stock.ShutSaveFolders();
            }
            if ((placing != null || loadingSub != null || mergingWhole != null) && --placingFrames <= 0) { placing = null; loadingSub = null; mergingWhole = null; }
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
            if (mergingWhole != null && type == CraftBrowserDialog.LoadType.Merge)
            {
                CraftFile c = mergingWhole;
                mergingWhole = null;
                Arrived(null, c.Name, false, true, c.Filed ? Path.GetDirectoryName(c.Path) : null);
            }
            if (type != CraftBrowserDialog.LoadType.Normal) { Relabel(); return; }
            lastPayloadName = null;
            AdoptFolder(EditorDriver.filePathToLoad);
            Relabel();
            // KSP took its undo step as the file loaded: before the lift and the stage names.
            if (opened != null) Stock.RetakeUndoStep();
        }

        // The one rule for where saves go after a load: the loaded file's own folder if it's
        // one of the craft folders, otherwise (a subassembly, a hangar copy, a file from
        // anywhere else) the facility's own folder, never a family folder left open before.
        void AdoptFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                Stock.SetSaveFolder(Directory.Exists(dir) && Library.InShips(dir) != null ? dir : Library.ShipsDir(Stock.Facility));
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
            string rel = Library.InShips(Stock.SaveFolder);
            string[] parts = string.IsNullOrEmpty(rel) ? new string[0] : rel.Split('/');
            if (parts.Length != 2) return false;
            cat = Category.ByFolder(parts[0]);
            family = parts[1];
            return cat != null && !cat.Subassembly;
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
            if (!Stock.HasCraft) { QuickSave(); return; }
            Show(SaveDialog());
        }

        // The library's Save… button.
        public void OpenSave() => Show(SaveDialog());

        // The one choice of Save dialog, for both Save buttons.
        Dialog SaveDialog()
        {
            Library.Scan();
            SubFile stage = OpenStage();
            if (stage != null) return new StageMarkDialog(this, stage);
            if (!Stock.HasCraft) return new SavePanel(this, null);
            if (!CurrentFiling(out _, out string family))
            {
                // A loose copy of a filed mark (KSP's own save put it outside the family
                // folder): Save offers what the filed mark would, and puts it back.
                CraftFile twin = LooseTwin(out CraftFile loose);
                return twin != null ? new NextMarkDialog(this, twin.Family, Path.GetDirectoryName(twin.Path), loose) : (Dialog)new SavePanel(this, null);
            }
            // A name typed in the box that isn't a mark of this family ("Orpheus" in the
            // Orpheus I family) would save a stray file into the folder, so ask instead.
            if (!FitsFamily(Stock.ShipName, family))
                return new SavePanel(this, null, $"\"{Stock.ShipName}\" isn't a mark of the {family} family, so where should it go?");
            // Phil: Save always asks: over the open mark, as the next one, or as a new design.
            return new NextMarkDialog(this, family, Stock.SaveFolder);
        }

        // The craft open came from an unfiled file with the same name as a filed mark in this
        // building (Phil's loose "Magellan I", play-test 2026-11-08): that mark, and the file.
        static CraftFile LooseTwin(out CraftFile loose)
        {
            loose = Library.CraftAt(Stock.CurrentFile);
            if (loose == null || loose.Filed) { loose = null; return null; }
            CraftFile l = loose;
            return Library.Craft.FirstOrDefault(c => c.Filed && c.Facility == l.Facility && Naming.SameName(c.Name, l.Name));
        }

        // A name fits the open family when it's a mark of the family, or a mark of a
        // member already saved in this folder (the folder's name can differ, like the
        // "Orpheus I" folder holding Orpheus III).
        static bool FitsFamily(string name, string family)
        {
            if (!Marks.TryParse(name, out string rest, out _, out _)) return false;
            if (Naming.InFamily(rest, family)) return true;
            string folder = Stock.SaveFolder;
            return Library.Craft.Any(c => Naming.SameName(c.Base, rest) && c.Filed && Paths.Same(Path.GetDirectoryName(c.Path), folder));
        }

        public void OpenNewDesign() => Show(new SavePanel(this, null));

        public void OpenStageSave(SubFile s) => Show(new StageMarkDialog(this, s));

        // The launcher or upper stage from the library that the craft in the editor is, on
        // its own: the craft carries its name (it was loaded, or saved from here), and nothing
        // is built on it. Null otherwise. (A payload on an upper stage can't be told apart;
        // its Save dialog has a link to save a new craft instead.)
        public SubFile OpenStage()
        {
            if (!Stock.HasCraft || EditorLogic.RootPart == null) return null;
            SubFile s = Library.StageNamed(Stock.ShipName);
            if (s == null) return null;
            if (s.Kind == SubKind.Launcher && LauncherTags.PayloadOf(Stock.Ship).Count > 0) return null;
            return s;
        }

        // The craft design open in the editor, when more has been built onto it and none of it
        // is a library launcher: maybe a launcher under a payload, still to be saved on its own.
        // Null otherwise. The drop box and the whole-craft launcher save then say how to save
        // just the launcher (Phil had to ask, play-test 2026-11-08).
        public CraftFile PayloadUnderBuild()
        {
            if (!Stock.HasCraft || !(Library.CraftAt(Stock.CurrentFile) is CraftFile c) || !c.Filed) return null;
            Library.LoadStats(c);
            var parts = Stock.Ship?.parts;
            if (parts == null || c.Parts <= 0 || parts.Count <= c.Parts) return null;
            return parts.Any(LauncherTags.IsLauncherPart) ? null : c;
        }

        // Saves the craft in the editor as a launcher or upper stage already in the library:
        // over it (the same name), or as its next mark.
        public void SaveStage(SubFile current, string name, int mark)
        {
            if (current == null || EditorLogic.RootPart == null) return;
            try
            {
                var info = new SubFile
                {
                    Kind = current.Kind, Family = current.Family, Group = current.Group, Rating = current.Rating, Width = current.Width,
                    Notes = current.Notes, Style = current.Style, Mark = mark,
                };
                SaveSub(null, EditorLogic.RootPart, name, info);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Saving {name}: {e}");
                Say($"Couldn't save {name}: {e.Message}");
            }
        }

        public void QuickSave() => SaveMark(null);

        // Saves the open craft as `name` (null: under its own name), into `folder` (null:
        // where it saves now). A loose copy it was opened from goes to Deleted craft once the
        // family has it.
        public void SaveMark(string name, string folder = null, CraftFile loose = null)
        {
            if (folder != null && !Stock.SetSaveFolder(folder)) { Say("Couldn't use that folder for saving"); return; }
            if (name != null) Stock.ShipName = name;
            Relabel();
            bool existed = FileExists(Stock.CurrentFile);
            Stock.Save(() =>
            {
                AfterSave(existed);
                if (loose != null && File.Exists(Stock.CurrentFile ?? "") && File.Exists(loose.Path) && !Paths.Same(loose.Path, Stock.CurrentFile))
                {
                    try { Library.DeleteCraft(loose); }
                    catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't move the loose copy {loose.Name}: {e.Message}"); }
                    Refresh(Library.CraftAt(Stock.CurrentFile));
                }
                if (name != null || loose != null) Say($"Saved {Stock.ShipName}");
            });
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
                if (moveOld && File.Exists(Stock.CurrentFile ?? "") && !Paths.Same(oldFile, Stock.CurrentFile))
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
                string rel = Library.InShips(Path.GetDirectoryName(Path.GetFullPath(file))) ?? "";
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
            bool isCurrent = Paths.Same(c.Path, Stock.CurrentFile);
            string dest = Library.MoveCraft(c, folder, name);
            if (isCurrent)
            {
                Stock.SetSaveFolder(folder);
                Stock.ShipName = name;
            }
            Refresh(Library.CraftAt(dest) ?? Library.Craft.FirstOrDefault(x => Paths.Same(x.Path, dest)));
            Say($"Filed {name} in {cat.Label}");
        }

        public void OpenRename(Family f) { if (f != null) Show(new RenameDialog(this, f)); }

        // Renames a family, moves it to another category, or both: every mark goes to the new
        // folder, keeping its design ids (so hangar copies stay with it) and notes.
        public void RenameFamily(Family f, string newName, Category cat = null)
        {
            cat = cat ?? f.Category;
            bool moved = cat != f.Category, renamed = newName != f.Name;
            string folder = Library.FolderFor(f.Facility, cat, newName);
            MarkStyle style = Library.StyleOf(f.Name, f.Marks.FirstOrDefault(c => c.Mark > 0)?.Style ?? MarkStyle.Roman);
            string current = Stock.CurrentFile;
            string select = null;
            foreach (CraftFile c in f.Marks.ToList())
            {
                string n = Naming.SwapFamily(c.Name, f.Name, newName);
                bool isCurrent = Paths.Same(c.Path, current);
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
            Relabel();
            Say(moved && renamed ? $"Renamed {f.Name} to {newName} and moved it to {cat.Label}" : moved ? $"Moved {f.Name} to {cat.Label}" : $"Renamed {f.Name} to {newName}");
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
            mergingWhole = merge && Stock.Empty ? c : null;
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
            bool intoEmpty = Stock.Empty;
            if (!Stock.PickUp(s.Path, sc =>
                {
                    Thumbs.TakeIfMissing(sc, s.Name);       // one saved before the library took pictures
                    Spawned(sc, d, PlaceHow.PickUp, null, s.IsStage ? s.Name : null, s.Kind == SubKind.Launcher, intoEmpty, null);
                }))
                Say($"Couldn't load {s.Name}");
        }


        // A launcher or upper stage opened as the whole craft, on an empty editor (Phil:
        // a launcher on its own should load, not only pick up). It's named after itself,
        // its stages are named as a picked-up launcher's are, and it saves as a new design.
        public void LoadSub(SubFile s)
        {
            if (s == null) return;
            if (!s.IsStage || !Stock.Empty) { PickUp(s); return; }
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
                Thumbs.TakeIfMissing(ship, s.Name);
                float lift = Stock.LiftOffFloor(ship);
                if (lift > 0) Debug.Log($"[PhilsBuilds] Lifted {s.Name} {lift:0.00} m onto the floor (floor at {Stock.Floor:0.00} m)");
                if (s.Kind == SubKind.Launcher) LauncherTags.Tag(ship.parts[0].localRoot, s.Name);
                Stock.ShipName = s.Name;        // saves go to the facility's own folder (AdoptFolder)
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
                SubFile s = Library.Subs.FirstOrDefault(x => Paths.Same(x.Path, d.Path));
                CraftFile c = s == null ? Library.CraftAt(d.Path) : null;
                if (s != null) PickUp(s);
                else if (c != null) LoadCraft(c, merge: true);
                return;
            }
            ICopies copies = BuildsApi.Copies;
            if (copies == null || HandsFull()) return;
            window.Close();
            bool named = d.Kind != DesignKind.Block, intoEmpty = Stock.Empty;
            string folder = FolderOf(d);
            try
            {
                copies.Build(d, row, sc =>
                {
                    if (this == null || sc == null || !Stock.Hold(sc)) return;
                    Spawned(sc, d, PlaceHow.Copy, row.Id, named ? d.Name : null, d.Kind == DesignKind.Launcher, intoEmpty, folder);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't build {d.Name} {row.Label}: {e}");
                Say($"Couldn't build {d.Name} {row.Label}");
            }
        }

        // A hangar copy loaded just as it's stored, by the mod that keeps the hangar (Phil,
        // play-test 2026-11-08: beside Load in the window). It replaces the craft; the window
        // has already asked about unsaved changes.
        public void OpenCopy(Action open)
        {
            if (open == null) return;
            window.Close();
            try { open(); }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't load the hangar copy: {e}");
                Say("Couldn't load that hangar copy");
            }
        }

        // Parts are on the cursor (or are the craft, on an empty editor). A craft design or a
        // launcher or upper stage (`name`, its name; null for a block) arrives by the rule
        // below, and anyone listening hears where the parts came from.
        void Spawned(ShipConstruct sc, Design d, PlaceHow how, string copyId, string name, bool launcher, bool intoEmpty, string folder)
        {
            if (name != null)
            {
                Part root = Stock.Held ?? sc.parts[0].localRoot;
                Arrived(root, name, launcher, intoEmpty, folder);
                if (launcher)
                    LauncherTags.EnsurePayload(Stock.Ship, new System.Collections.Generic.HashSet<Part>(LauncherTags.Subtree(root)), Stock.ShipName,
                        PayloadType(CurrentCategory()), ref lastPayloadName);
            }
            if (d != null) BuildsApi.RaisePlaced(d, how, sc.parts, copyId);
        }

        // The one rule for a design joining the editor's craft (loaded or added from the
        // library, built from the hangar, or just saved from it): a launcher's parts get its
        // stage names, and if it's the whole craft and the craft has only a stand-in name
        // (KSP's default, or a stage's), the craft takes the design's name and saves go to
        // its family folder (`folder`; the facility's own for a stage or an unfiled craft),
        // so Save knows it. Phil, play-test 2026-11-08: a hangar Gallant I stayed "Untitled".
        void Arrived(Part root, string name, bool launcher, bool whole, string folder = null)
        {
            if (whole && Stock.IsPlaceholderName(Stock.ShipName))
            {
                Stock.ShipName = name;
                Stock.SetSaveFolder(folder ?? Library.ShipsDir(Stock.Facility));
            }
            if (launcher) LauncherTags.Tag(root, name);
        }

        // Where a design's craft saves: its family folder (a craft), or null.
        static string FolderOf(Design d) =>
            d != null && d.Kind == DesignKind.Craft && Library.CraftAt(d.Path) is CraftFile c && c.Filed ? Path.GetDirectoryName(c.Path) : null;

        public void OpenSubDialog(SubFile existing, Part held, SubKind kind, bool wholeCraft = false) =>
            Show(new SubDialog(this, existing, held, kind, wholeCraft));

        public void SaveSub(SubFile editing, Part held, string name, SubFile info)
        {
            SubFile s = editing;
            System.Collections.Generic.List<Part> saved = null;
            bool existed = FileExists(Path.Combine(Library.SubsDir, name + ".craft"));
            bool stage = info.IsStage;
            if (held != null)
            {
                saved = Stock.SaveSubassembly(name, "", held);
                if (saved == null) throw new IOException("KSP couldn't save it.");
                Library.Scan();
                s = Library.SubNamed(name);
                if (s == null) throw new IOException("It saved, but under a different name.");
                LauncherTags.StripFile(s.Path, stage);
            }
            else if (s != null && !Naming.SameName(s.Name, name) || s != null && s.Name != name)
            {
                string was = s.Name;
                Library.RenameSub(s, name);
                FollowRename(was, name);
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
            if (held != null && stage) BecomeStage(held, name, info.Kind);
            Say($"Saved {name}");
            if (saved == null) return;
            BuildsApi.RaiseSaved(BuildsApi.OfFile(path), !existed, saved);
            // Listeners may have written to the parts just saved (the Reusability Suite
            // stamps its design id on them), so the file is written again to match.
            if (BuildsApi.HasSavedListeners && Stock.SaveSubassembly(name, "", held, picture: false) != null) LauncherTags.StripFile(path, stage);
        }

        // The parts just saved as a launcher or upper stage are that stage now, though they
        // may have come from another one (deleted, or still in the library).
        void BecomeStage(Part held, string name, SubKind kind)
        {
            try
            {
                var tree = new System.Collections.Generic.HashSet<Part>(LauncherTags.Subtree(held));
                Arrived(held, name, kind == SubKind.Launcher, Stock.HoldingCraft || (Stock.Ship?.parts?.All(tree.Contains) ?? true));
                Relabel();
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Naming {name}'s stages: {e.Message}"); }
        }

        // A launcher or upper stage renamed in the library: a craft in the editor named after
        // it follows, so Save still knows it.
        static void FollowRename(string was, string now)
        {
            if (Naming.SameName(Stock.ShipName, was)) Stock.ShipName = now;
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
                        try { string was = s.Name; Library.RenameSub(s, newName); FollowRename(was, newName); EditorPartList.Instance?.RefreshSubassemblies(); }
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
