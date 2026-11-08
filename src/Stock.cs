// The stock editor pieces this mod drives: saving, loading, merging, picking up
// subassemblies and the save folder. Some of them are private in KSP 1.12, so
// they're reached by reflection; each one fails quietly and logs if KSP changes.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace PhilsBuilds
{
    static class Stock
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static MethodInfo saveShipWithCallback, shipToLoadSelected, setDefaultSaveFolder;
        static FieldInfo undoLevel, undoIndexAtLastSave;

        static void Find()
        {
            if (saveShipWithCallback != null) return;
            Type e = typeof(EditorLogic);
            saveShipWithCallback = e.GetMethods(Any).FirstOrDefault(m => m.Name == "saveShip" && m.GetParameters().Length == 1);
            shipToLoadSelected = e.GetMethods(Any).FirstOrDefault(m => m.Name == "ShipToLoadSelected" &&
                                                                       m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string));
            setDefaultSaveFolder = typeof(EditorDriver).GetMethod("SetDefaultSaveFolder", Any);
            undoLevel = e.GetField("undoLevel", Any);
            undoIndexAtLastSave = e.GetField("undoIndexAtLastSave", Any);
            if (saveShipWithCallback == null || shipToLoadSelected == null || setDefaultSaveFolder == null || undoLevel == null || undoIndexAtLastSave == null)
                Debug.LogWarning("[PhilsBuilds] Some stock editor functions weren't found; parts of the mod may not work in this KSP version.");
        }

        public static EditorLogic Editor => EditorLogic.fetch;

        public static string Facility => EditorDriver.editorFacility == EditorFacility.SPH ? "SPH" : "VAB";

        public static string ShipName
        {
            get => Editor != null && Editor.shipNameField != null ? Editor.shipNameField.text : "";
            set { if (Editor != null && Editor.shipNameField != null) Editor.shipNameField.text = value; }
        }

        public static ShipConstruct Ship => Editor?.ship;

        // Nothing built yet (a part held over an empty editor doesn't count).
        public static bool Empty => Ship?.parts == null || Ship.parts.Count == 0 || HoldingCraft && Ship.parts.Count == 1;

        // The craft itself is on the cursor, held by its root part. Picking something else up
        // would delete it, as clicking the parts list does.
        public static bool HoldingCraft => Held != null && Held == EditorLogic.RootPart;

        // KSP's own names for a craft nobody has named yet.
        public static bool IsDefaultName(string n) =>
            string.IsNullOrEmpty(n?.Trim()) || Naming.SameName(n, "Untitled Space Craft") || Naming.SameName(n, "Auto-Saved Ship") ||
            Naming.SameName(n, EditorLogic.autoShipName);

        public static Part Held => EditorLogic.SelectedPart;

        public static string SaveFolder => EditorDriver.DefaultCraftSavePath;

        public static bool SetSaveFolder(string folder)
        {
            Find();
            try
            {
                Directory.CreateDirectory(folder);
                return setDefaultSaveFolder != null && (bool)setDefaultSaveFolder.Invoke(null, new object[] { folder });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhilsBuilds] Couldn't set the save folder to {folder}: {ex.Message}");
                return false;
            }
        }

        // The craft file the open design saves to.
        public static string CurrentFile =>
            string.IsNullOrEmpty(SaveFolder) || string.IsNullOrEmpty(ShipName) ? null
                : Path.Combine(SaveFolder, KSPUtil.SanitizeString(ShipName, '_', true) + ".craft");

        // Changes since the last save (the stock load dialog asks first when there are some).
        public static bool Unsaved
        {
            get
            {
                Find();
                try
                {
                    if (Editor == null || Editor.ship == null || Editor.ship.parts.Count == 0) return false;
                    return (int)undoLevel.GetValue(Editor) != (int)undoIndexAtLastSave.GetValue(Editor);
                }
                catch { return false; }
            }
        }

        // The stock save, exactly as the Save button does it (including its
        // overwrite question), with something to do once it's written.
        public static void Save(Action after)
        {
            Find();
            if (Editor == null || saveShipWithCallback == null) return;
            try { saveShipWithCallback.Invoke(Editor, new object[] { new Callback(() => after?.Invoke()) }); }
            catch (Exception ex) { Debug.LogError($"[PhilsBuilds] Save failed: {ex}"); }
        }

        public static void Load(string path)
        {
            try { EditorLogic.LoadShipFromFile(path); }
            catch (Exception ex) { Debug.LogError($"[PhilsBuilds] Load failed: {ex}"); }
        }

        // The building's floor height (KSP keeps it as the bottom of the space parts can go in).
        public static float Floor => EditorBounds.Instance != null ? EditorBounds.Instance.constructionBounds.min.y : 0f;

        // A subassembly's parts sit where the cursor held them when it was saved, often
        // partly under the floor. Loaded as the craft, it's lifted so its lowest point is
        // on the floor. Launch clamps don't count: in the editor their towers stretch down
        // to the floor by themselves every frame (and haven't yet, just after a load).
        // Returns how far it went up.
        public static float LiftOffFloor(ShipConstruct ship)
        {
            if (ship?.parts == null || ship.parts.Count == 0) return 0;
            try
            {
                // In the editor a part hangs from its parent's transform, so the tops of the
                // part trees (normally just the root) hold every part's models, and move them all.
                var tops = ship.parts.Where(p => p.transform.parent == null || p.transform.parent.GetComponentInParent<Part>() == null).ToList();
                var clamp = new System.Collections.Generic.Dictionary<Part, bool>();
                float low = float.MaxValue;
                foreach (Part top in tops)
                    foreach (Renderer r in top.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                        if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;    // the part's own model, not effects
                        Part owner = r.GetComponentInParent<Part>();
                        if (owner == null) continue;
                        if (!clamp.TryGetValue(owner, out bool isClamp)) clamp[owner] = isClamp = owner.FindModuleImplementing<LaunchClamp>() != null;
                        if (!isClamp) low = Math.Min(low, r.bounds.min.y);
                    }
                if (low == float.MaxValue) return 0;
                float lift = Floor - low;
                if (lift <= 0.01f) return 0;
                foreach (Part p in tops) p.transform.position += Vector3.up * lift;
                return lift;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhilsBuilds] Couldn't lift the craft off the floor: {ex.Message}");
                return 0;
            }
        }

        public static void Merge(string path)
        {
            Find();
            if (Editor == null || shipToLoadSelected == null) return;
            try { shipToLoadSelected.Invoke(Editor, new object[] { path, CraftBrowserDialog.LoadType.Merge }); }
            catch (Exception ex) { Debug.LogError($"[PhilsBuilds] Merge failed: {ex}"); }
        }

        // Puts a subassembly on the cursor, as clicking it in the stock list does: drop
        // whatever is held, build the parts from the file, hold them. Done here rather
        // than through the stock list so `spawned` gets the parts, in file order.
        public static bool PickUp(string path, Action<ShipConstruct> spawned)
        {
            if (Editor == null) return false;
            try
            {
                ShipTemplate t = ShipConstruction.LoadTemplate(path);
                if (t == null) return false;
                ShipConstruction.CreateConstructFromTemplate(t, sc =>
                {
                    if (sc == null || !Hold(sc)) return;
                    try { spawned?.Invoke(sc); }
                    catch (Exception ex) { Debug.LogError($"[PhilsBuilds] After picking up {path}: {ex}"); }
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't pick up {path}: {ex}");
                return false;
            }
        }

        // Puts parts that already exist on the cursor (on an empty editor they become the craft).
        public static bool Hold(ShipConstruct sc)
        {
            if (Editor == null || sc?.parts == null || sc.parts.Count == 0) return false;
            try
            {
                Editor.OnPartListBackgroundTap();
                Editor.SpawnConstruct(sc);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't hold {sc.shipName}: {ex}");
                return false;
            }
        }

        // Saves the held part and everything under it as a subassembly. This is what
        // the stock drop zone does (stage numbers taken relative to the craft's root
        // part), without the stock list's own steps, which fail while it's hidden.
        // Gives back the parts in the order written, or null if it failed.
        // The editor's latest undo step, taken again from the craft as it is now (after
        // changes made just after a load, so undo doesn't bring back the craft as loaded).
        public static void RetakeUndoStep()
        {
            try { Editor?.ResetBackup(); }
            catch (Exception ex) { Debug.LogWarning($"[PhilsBuilds] Couldn't update the undo step: {ex.Message}"); }
        }

        public static System.Collections.Generic.List<Part> SaveSubassembly(string name, string description, Part root, bool picture = true)
        {
            try
            {
                var sc = new ShipConstruct(name, description ?? "", root);
                int offset = EditorLogic.RootPart != null ? EditorLogic.RootPart.inverseStage : 0;
                // Stage numbers are shifted only for the file, then put back, so the
                // craft in the editor keeps its staging (it may be the whole craft).
                var stages = sc.parts.ToDictionary(p => p, p => p.inverseStage);
                try
                {
                    foreach (Part p in sc.parts) p.inverseStage -= offset;
                    ShipConstruction.SaveSubassembly(sc, name);
                }
                finally
                {
                    foreach (var kv in stages) if (kv.Key != null) kv.Key.inverseStage = kv.Value;
                }
                if (picture) Thumbs.Take(sc, name);
                if (sc.vesselDeltaV != null) UnityEngine.Object.Destroy(sc.vesselDeltaV.gameObject);
                try { EditorPartList.Instance?.RefreshSubassemblies(); } catch { }
                return new System.Collections.Generic.List<Part>(sc.parts);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhilsBuilds] Couldn't save subassembly {name}: {ex}");
                return null;
            }
        }

        // Hides the stock Subassemblies category in the parts list: the library has them now.
        public static void HideStockSubassemblies()
        {
            try
            {
                if (PartCategorizer.Instance == null) return;
                object cat = typeof(PartCategorizer).GetField("subassemblies", Any)?.GetValue(PartCategorizer.Instance);
                if (cat == null) return;
                object button = cat.GetType().GetField("button", Any)?.GetValue(cat);
                if (button is Component c && c.gameObject.activeSelf) c.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhilsBuilds] Couldn't hide the stock subassembly list: {ex.Message}");
            }
        }
    }
}
