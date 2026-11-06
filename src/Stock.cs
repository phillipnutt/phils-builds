// The stock editor pieces this mod drives: saving, loading, merging, picking up
// subassemblies and the save folder. Some of them are private in KSP 1.12, so
// they're reached by reflection; each one fails quietly and logs if KSP changes.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace PhilsVesselNaming
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
                Debug.LogWarning("[PhilsVesselNaming] Some stock editor functions weren't found; parts of the mod may not work in this KSP version.");
        }

        public static EditorLogic Editor => EditorLogic.fetch;

        public static string Facility => EditorDriver.editorFacility == EditorFacility.SPH ? "SPH" : "VAB";

        public static string ShipName
        {
            get => Editor != null && Editor.shipNameField != null ? Editor.shipNameField.text : "";
            set { if (Editor != null && Editor.shipNameField != null) Editor.shipNameField.text = value; }
        }

        public static ShipConstruct Ship => Editor?.ship;

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
                Debug.LogWarning($"[PhilsVesselNaming] Couldn't set the save folder to {folder}: {ex.Message}");
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
            catch (Exception ex) { Debug.LogError($"[PhilsVesselNaming] Save failed: {ex}"); }
        }

        public static void Load(string path)
        {
            try { EditorLogic.LoadShipFromFile(path); }
            catch (Exception ex) { Debug.LogError($"[PhilsVesselNaming] Load failed: {ex}"); }
        }

        public static void Merge(string path)
        {
            Find();
            if (Editor == null || shipToLoadSelected == null) return;
            try { shipToLoadSelected.Invoke(Editor, new object[] { path, CraftBrowserDialog.LoadType.Merge }); }
            catch (Exception ex) { Debug.LogError($"[PhilsVesselNaming] Merge failed: {ex}"); }
        }

        // Puts a subassembly on the cursor, as clicking it in the stock list does.
        public static bool PickUp(string path)
        {
            if (Editor == null) return false;
            try
            {
                ShipTemplate t = ShipConstruction.LoadTemplate(path);
                if (t == null) return false;
                Editor.OnPartListIconTap(t);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhilsVesselNaming] Couldn't pick up {path}: {ex}");
                return false;
            }
        }

        // Saves the held part and everything under it as a subassembly, the stock way.
        public static bool SaveSubassembly(string name, string description, Part root)
        {
            try
            {
                if (SubassemblyDropZone.Instance != null)
                {
                    SubassemblyDropZone.Instance.SaveSubassembly(name, description ?? "", root);
                    return true;
                }
                var sc = new ShipConstruct(name, description ?? "", root);
                ShipConstruction.SaveSubassembly(sc, name);
                if (sc.vesselDeltaV != null) UnityEngine.Object.Destroy(sc.vesselDeltaV.gameObject);
                EditorPartList.Instance?.RefreshSubassemblies();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhilsVesselNaming] Couldn't save subassembly {name}: {ex}");
                return false;
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
                Debug.LogWarning($"[PhilsVesselNaming] Couldn't hide the stock subassembly list: {ex.Message}");
            }
        }
    }
}
