// Pictures of launchers and subassemblies, like the ones KSP takes of saved craft.
// KSP takes none for subassemblies, so Phil's Builds takes its own: when one is saved
// from the library's drop box, and the first time an older one is picked up or loaded.
// They sit beside KSP's own craft pictures in <KSP>/thumbs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PhilsBuilds
{
    static class Thumbs
    {
        // One picture per file, kept until the file changes; the old one is freed then.
        static readonly Dictionary<string, KeyValuePair<long, Texture2D>> cache = new Dictionary<string, KeyValuePair<long, Texture2D>>(StringComparer.OrdinalIgnoreCase);

        static string Name(string sub) => KSPUtil.SanitizeString($"{HighLogic.SaveFolder}_Subassemblies_{sub}", '_', true);

        public static string FileFor(string sub) => Path.Combine(KSPUtil.ApplicationRootPath, "thumbs", Name(sub) + ".png");

        // A blank picture (one taken by 0.1.0 of parts held on the cursor) counts as none, so
        // it's taken again the next time the launcher is loaded or added to a craft.
        public static bool Has(string sub) => !string.IsNullOrEmpty(sub) && Load(FileFor(sub)) != null;

        public static void TakeIfMissing(ShipConstruct sc, string sub) { if (!Has(sub)) Take(sc, sub); }

        public static Texture2D Of(SubFile s) => s == null ? null : Load(FileFor(s.Name));

        static Texture2D Load(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                long ticks = File.GetLastWriteTimeUtc(file).Ticks;
                if (cache.TryGetValue(file, out KeyValuePair<long, Texture2D> had))
                {
                    if (had.Key == ticks) return had.Value;
                    if (had.Value != null) UnityEngine.Object.Destroy(had.Value);
                }
                Texture2D t = new Texture2D(2, 2, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!t.LoadImage(File.ReadAllBytes(file)) || Blank(t)) { UnityEngine.Object.Destroy(t); t = null; }
                cache[file] = new KeyValuePair<long, Texture2D>(ticks, t);
                return t;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsBuilds] Couldn't read the picture {Path.GetFileName(file)}: {e.Message}");
                return null;
            }
        }

        static bool Blank(Texture2D t)
        {
            foreach (Color32 c in t.GetPixels32()) if (c.a != 0) return false;
            return true;
        }

        // A picture of these parts alone. Everything else in the editor is hidden for the
        // one render (it happens inside this call, so nothing flickers on screen).
        public static void Take(ShipConstruct sc, string sub)
        {
            if (sc?.parts == null || sc.parts.Count == 0 || string.IsNullOrEmpty(sub)) return;
            var keep = new HashSet<Part>(sc.parts.Where(p => p != null));
            var hidden = new List<Renderer>();
            var lifted = new List<GameObject>();
            try
            {
                // KSP moves parts held on the cursor to layer 1 and back to 0 when they're
                // attached. The picture camera sees only layer 0, so a launcher saved from the
                // drop box while held came out blank (Phil's Shkhara 022, 2026-11-08). Held
                // parts go back to layer 0 for the one render.
                foreach (Part p in keep)
                    foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.layer == HeldLayer && r.GetComponentInParent<Part>() == p)
                        {
                            r.gameObject.layer = 0;
                            lifted.Add(r.gameObject);
                        }
                // The craft's parts, and anything else on the cursor: a held part's mirrored
                // copies aren't in the craft's part list until they're attached.
                var all = new HashSet<Part>(Stock.Ship?.parts ?? new List<Part>());
                Part held = Stock.Held;
                if (held != null)
                    foreach (Part twin in held.symmetryCounterparts.Prepend(held))
                        foreach (Part q in LauncherTags.Subtree(twin)) all.Add(q);
                IEnumerable<Part> others = all.Where(p => p != null && !keep.Contains(p));
                foreach (Part p in others)
                    foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                        if (r.enabled && !keep.Contains(r.GetComponentInParent<Part>()))
                        {
                            r.enabled = false;
                            hidden.Add(r);
                        }
                bool sph = EditorDriver.editorFacility == EditorFacility.SPH;
                // The same camera angles KSP uses for craft saved in each building.
                if (sph) CraftThumbnail.TakeSnaphot(sc, 256, "thumbs", Name(sub), 45, 45, 45, 45, 0.9f);
                else CraftThumbnail.TakeSnaphot(sc, 256, "thumbs", Name(sub), 35, 135, 35, 135, 0.9f);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't take a picture of {sub}: {e.Message}"); }
            finally
            {
                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                foreach (GameObject g in lifted) if (g != null) g.layer = HeldLayer;
            }
        }

        const int HeldLayer = 1;     // EditorLogic's pick-up step: SetLayerRecursive(1, true, 1 << 21)

        public static void Rename(string from, string to)
        {
            try
            {
                string a = FileFor(from), b = FileFor(to);
                if (!File.Exists(a) || string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(b)) File.Delete(b);
                File.Move(a, b);
            }
            catch (Exception e) { Debug.LogWarning($"[PhilsBuilds] Couldn't move the picture of {from}: {e.Message}"); }
        }

        public static void Delete(string sub)
        {
            try { string f = FileFor(sub); if (File.Exists(f)) File.Delete(f); }
            catch { }
        }
    }
}
