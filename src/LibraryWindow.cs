// The library window, opened by the editor's Load button: craft by category and
// family, launchers by payload class with a fit check against what's in the
// editor, and the other subassemblies. In the look Phil's mods share with PIP.

using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PhilsVesselNaming
{
    public class LibraryWindow
    {
        public enum Tab { Craft, Launchers, Subs }
        enum Sort { Family, Recent, Mass, Cost }

        const float W = 960, H = 660, TitleH = 50, RailW = 196, DetailW = 300, FootH = 44;
        const float MidW = W - RailW - DetailW;
        const string LockId = "PhilsVesselNamingWindow", TypingLockId = "PhilsVesselNamingTyping";
        const ControlTypes WindowLock = ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_PAD_PICK_COPY |
            ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.EDITOR_ROOT_REFLOW | ControlTypes.CAMERACONTROLS;
        const string AllKey = null, UnfiledKey = "~unfiled", UpperKey = "~upper", UnratedKey = "~unrated";

        // Kept for the session, so the window opens the way it was left.
        static Vector2? savedPosition;
        static Tab tab = Tab.Craft;
        static string craftFilter, launcherFilter, subFilter;
        static string craftSearch = "", subSearch = "";
        static Sort sort = Sort.Family;
        static bool fitOnly = true;
        static readonly HashSet<string> collapsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static string selectedCraftPath, selectedSubPath;

        readonly VesselNamingEditor host;
        readonly List<Btn> buttons = new List<Btn>();
        readonly List<Kit.Field> fields = new List<Kit.Field>();
        RectTransform root, rail, mid, list, detail;
        Kit.Scroller scroller;
        Kit.Field searchField;
        DragMove drag;
        Action pending;
        bool windowLocked, typingLocked;
        int builtVersion = -1;
        bool confirmDelete, confirmLoad;
        bool mergeAfterConfirm;
        RectTransform dropZone;
        Text dropText;
        Image dropBg;

        public LibraryWindow(VesselNamingEditor host) { this.host = host; }

        public bool IsOpen => root != null;

        CraftFile SelCraft => Library.Craft.FirstOrDefault(c => string.Equals(c.Path, selectedCraftPath, StringComparison.OrdinalIgnoreCase));
        SubFile SelSub => Library.Subs.FirstOrDefault(s => string.Equals(s.Path, selectedSubPath, StringComparison.OrdinalIgnoreCase));

        // ---- Opening and closing ----------------------------------------------------

        public void Open(Tab? show = null)
        {
            if (root != null) { if (show.HasValue) SetTab(show.Value); return; }
            if (UIMasterController.Instance == null) return;
            if (show.HasValue) tab = show.Value;
            try
            {
                Look.Init();
                Library.Scan();
                Build();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsVesselNaming] Couldn't open the library: {e}");
                Close();
            }
        }

        public void Close()
        {
            ReleaseLocks();
            if (root != null)
            {
                savedPosition = root.anchoredPosition;
                UnityEngine.Object.Destroy(root.gameObject);
            }
            root = null;
            buttons.Clear();
            fields.Clear();
            searchField = null;
            confirmDelete = confirmLoad = false;
        }

        // Something changed outside (a save, a move): read the folders again.
        public void Rescan()
        {
            if (root == null) return;
            Library.Scan();
        }

        void Build()
        {
            Canvas canvas = UIMasterController.Instance.dialogCanvas;
            root = Ui.Node(canvas.transform, "PhilsVesselNaming");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(W, H);
            root.anchoredPosition = Clamped(savedPosition ?? Vector2.zero);
            Ui.Box(root, Look.Round(Look.Panel, Look.Line, 10), raycast: true);
            BuildTitle();
            rail = Ui.Node(root, "Rail").At(0, TitleH, RailW, H - TitleH);
            mid = Ui.Node(root, "Mid").At(RailW, TitleH, MidW, H - TitleH);
            detail = Ui.Node(root, "Detail").At(RailW + MidW, TitleH, DetailW, H - TitleH);
            root.SetAsLastSibling();
            RebuildAll();
        }

        Vector2 Clamped(Vector2 p)
        {
            Rect area = ((RectTransform)root.parent).rect;
            float x = Mathf.Max(0, (area.width - W) / 2), y = Mathf.Max(0, (area.height - H) / 2);
            return new Vector2(Mathf.Clamp(p.x, -x, x), Mathf.Clamp(p.y, -y, y));
        }

        Btn Track(Btn b) { buttons.Add(b); return b; }

        // Clicks only queue work; it runs in Update, after the click is done with the UI.
        void Later(Action a) => pending = a;

        Btn[] tabButtons;

        void BuildTitle()
        {
            Ui.Slab(root, "TitleBar", 1, 1, W - 2, TitleH - 1, Look.Panel2, 9, Ui.Corners.Top);
            Ui.Solid(Ui.Node(root, "TitleLine").At(1, TitleH - 1, W - 2, 1), Look.Line);
            RectTransform handle = Ui.Node(root, "Drag").At(0, 0, W, TitleH);
            Ui.Solid(handle, Color.clear, raycast: true);
            drag = handle.gameObject.AddComponent<DragMove>();
            drag.Target = root;

            Ui.Picture(Ui.Node(root, "Logo").At(14, 14, 22, 22), Look.Tag, Look.Amber);
            Ui.Label(Ui.Node(root, "Title").At(44, 0, 220, TitleH),
                $"<color={Look.Html(Look.Ink3)}>Phil's</color> <b>Vessel Naming</b>", Look.Display, 19, Look.Ink);

            tabButtons = new Btn[3];
            float x = 262;
            string[] names = { "Craft", "Launchers", "Subassemblies" };
            for (int i = 0; i < 3; i++)
            {
                Tab t = (Tab)i;
                float w = names[i].Length * 8.2f + 46;
                tabButtons[i] = Track(Btn.Make(root, names[i], x, 10, w, 30, () => Later(() => SetTab(t)))
                    .Skin(null, Look.Round(Look.Panel3, Look.Panel3, 7), Look.Round(Look.Panel3, Look.Line2, 7))
                    .Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel(names[i], Look.Body, 14, align: TextAnchor.MiddleLeft));
                tabButtons[i].Label.rectTransform.Stretch(12, 0, 0, 0);
                x += w + 4;
            }

            Track(Kit.Ghost(root, "Save…", W - 48 - 78, 10, 72, 30, () => Later(() => host.OpenSave())));
            Track(Btn.Make(root, "Close", W - 40, 10, 30, 30, () => Later(host.CloseWindow))
                .Skin(null, Look.Round(Look.Panel3, Look.Panel3, 6)).Colors(Look.Ink2, Look.Ink, Look.Ink).WithGlyph(Look.Cross, 12));
        }

        readonly Text[] tabCounts = new Text[3];

        void RefreshTabs()
        {
            int[] counts = { Library.Craft.Count, Library.Subs.Count(s => s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage),
                             Library.Subs.Count(s => s.Kind != SubKind.Launcher && s.Kind != SubKind.UpperStage) };
            for (int i = 0; i < 3; i++)
            {
                Btn b = tabButtons[i];
                b.On = (int)tab == i;
                b.Label.fontStyle = b.On ? FontStyle.Bold : FontStyle.Normal;
                if (tabCounts[i] == null)
                {
                    RectTransform badge = Ui.Node(b.Rt, "Count");
                    badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1, 0.5f);
                    badge.anchoredPosition = new Vector2(-8, 0);
                    badge.sizeDelta = new Vector2(24, 16);
                    Ui.Box(badge, Look.Round(Look.Panel3, Look.Panel3, 8));
                    tabCounts[i] = Ui.Label(Ui.Node(badge, "N").Stretch(), "", Look.Display, 11, Look.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                tabCounts[i].text = counts[i].ToString();
                Image bg = tabCounts[i].transform.parent.GetComponent<Image>();
                Ui.SetSprite(bg, b.On ? Look.Round(Look.Amber, Look.Amber, 8) : Look.Round(Look.Panel3, Look.Line2, 8));
                tabCounts[i].color = b.On ? Look.AmberInk : Look.Ink;
                b.Refresh();
            }
        }

        void SetTab(Tab t)
        {
            tab = t;
            confirmDelete = confirmLoad = false;
            RebuildAll();
        }

        // ---- Rebuilding -------------------------------------------------------------------

        void RebuildAll()
        {
            if (root == null) return;
            builtVersion = Library.Version;
            buttons.RemoveAll(b => b.Rt == null || !b.Rt.IsChildOf(root) || b.Rt.IsChildOf(rail) || b.Rt.IsChildOf(mid) || b.Rt.IsChildOf(detail));
            fields.Clear();
            Kit.Clear(rail);
            Kit.Clear(mid);
            Kit.Clear(detail);
            searchField = null;
            dropZone = null;
            RefreshTabs();
            BuildRail();
            BuildMid();
            BuildDetail();
        }

        void RebuildList()
        {
            if (list == null) { RebuildAll(); return; }
            buttons.RemoveAll(b => b.Rt == null || b.Rt.IsChildOf(list));
            float keep = scroller != null ? scroller.Offset : 0;
            Kit.Clear(scroller.Content);
            FillList();
            scroller.Offset = keep;
        }

        void RebuildDetail()
        {
            buttons.RemoveAll(b => b.Rt == null || b.Rt.IsChildOf(detail));
            fields.RemoveAll(f => f.Input == null || f.Input.transform.IsChildOf(detail));
            Kit.Clear(detail);
            BuildDetail();
        }

        // ---- The left column ----------------------------------------------------------------

        float railY;

        void BuildRail()
        {
            Ui.Slab(rail, "Bg", 1, 0, RailW - 1, H - TitleH - 1, Look.Rail, 9, Ui.Corners.BottomLeft);
            Ui.Solid(Ui.Node(rail, "Line").At(RailW - 1, 0, 1, H - TitleH - 1), Look.Line);
            railY = 6;
            if (tab == Tab.Craft) CraftRail();
            else if (tab == Tab.Launchers) LauncherRail();
            else SubRail();
        }

        void RailHead(string s)
        {
            railY += 6;
            Kit.Line(rail, 16, railY, RailW - 24, 14, s.ToUpperInvariant(), 10, Look.Ink3);
            railY += 15;
        }

        void RailItem(string label, string sub, int count, bool on, Action pick, float h = 22, bool warn = false, bool bold = false)
        {
            bool empty = count == 0 && !on;
            Btn b = Track(Btn.Make(rail, label, 8, railY, RailW - 16, h, empty ? (Action)null : () => Later(pick))
                .Skin(null, Look.Round(Look.Panel2, Look.Panel2, 6), Look.Round(Look.Wash(Look.Rail), Look.AmberLine, 6))
                .Colors(warn ? Look.Warn : Look.Fade(Look.Ink, 0.82f), Look.Ink, Look.Amber)
                .WithLabel(sub == null ? label : $"{label} <size=11><color={Look.Html(Look.Ink3)}>{sub}</color></size>", Look.Body, 13,
                           bold ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft));
            b.Label.rectTransform.Stretch(8, 0, 30, 0);
            b.On = on;
            b.Enabled = !empty;
            b.InkOff = Look.Fade(Look.Ink3, 0.55f);
            if (count > 0)
            {
                RectTransform badge = Ui.Node(b.Rt, "Count");
                badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1, 0.5f);
                badge.anchoredPosition = new Vector2(-6, 0);
                Text t = Ui.Label(Ui.Node(badge, "N").Stretch(), count.ToString(), Look.Display, 11, on ? Look.AmberInk : warn ? Look.Warn : Look.Ink,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                badge.sizeDelta = new Vector2(Mathf.Max(18, t.preferredWidth + 10), 16);
                Ui.Box(badge, on ? Look.Round(Look.Amber, Look.Amber, 8) : warn ? Look.Round(Color.Lerp(Look.Rail, Look.Warn, 0.18f), Look.Fade(Look.Warn, 0.3f), 8)
                                 : Look.Round(Look.Panel3, Look.Line2, 8));
                badge.SetAsFirstSibling();
                badge.SetAsLastSibling();
            }
            b.Refresh();
            railY += h;
        }

        void CraftRail()
        {
            RailItem("All craft", null, Library.Craft.Count, craftFilter == AllKey, () => { craftFilter = AllKey; Reselect(); RebuildAll(); }, 26, bold: true);
            foreach (Group g in new[] { Group.Crewed, Group.Uncrewed, Group.Surface, Group.AirSea })
            {
                RailHead(Category.GroupLabel(g));
                foreach (Category c in Category.Craft.Where(c => c.Group == g))
                {
                    int n = Library.Craft.Count(x => x.Category == c);
                    string id = c.Id;
                    RailItem(c.Label, null, n, craftFilter == id, () => { craftFilter = id; Reselect(); RebuildAll(); });
                }
            }
            railY += 4;
            Ui.Solid(Ui.Node(rail, "Sep").At(14, railY, RailW - 28, 1), Look.Line);
            railY += 5;
            int unfiled = Library.Craft.Count(c => !c.Filed);
            RailItem("Unfiled", null, unfiled, craftFilter == UnfiledKey, () => { craftFilter = UnfiledKey; Reselect(); RebuildAll(); }, warn: unfiled > 0);
        }

        void LauncherRail()
        {
            List<SubFile> launchers = Library.Subs.Where(s => s.Kind == SubKind.Launcher).ToList();
            RailItem("All launchers", null, launchers.Count, launcherFilter == AllKey, () => { launcherFilter = AllKey; RebuildAll(); }, 26, bold: true);
            RailHead("By payload");
            foreach (LauncherClass c in Enum.GetValues(typeof(LauncherClass)))
            {
                string key = c.ToString();
                RailItem(Naming.ClassLabel(c), Naming.ClassRange(c), launchers.Count(s => s.Rated && Naming.ClassOf(s.Rating) == c),
                    launcherFilter == key, () => { launcherFilter = key; RebuildAll(); }, 26);
            }
            int unrated = launchers.Count(s => !s.Rated);
            RailItem("Not rated yet", null, unrated, launcherFilter == UnratedKey, () => { launcherFilter = UnratedKey; RebuildAll(); }, 26, warn: unrated > 0);
            RailHead("Other stages");
            RailItem("Upper stages & tugs", null, Library.Subs.Count(s => s.Kind == SubKind.UpperStage), launcherFilter == UpperKey,
                () => { launcherFilter = UpperKey; RebuildAll(); }, 26);
            Kit.Text(rail, 16, H - TitleH - 76, RailW - 30, 64,
                "Classes come from the rating you type in, so a launcher moves class when you re-rate it.", 12, Look.Ink2,
                align: TextAnchor.LowerLeft);
        }

        void SubRail()
        {
            List<SubFile> subs = Library.Subs.Where(s => s.Kind != SubKind.Launcher && s.Kind != SubKind.UpperStage).ToList();
            RailItem("All subassemblies", null, subs.Count, subFilter == AllKey, () => { subFilter = AllKey; RebuildAll(); }, 26, bold: true);
            List<string> modules = subs.Where(s => s.Kind == SubKind.Module).Select(s => s.Family).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            if (modules.Count > 0)
            {
                RailHead("Modules");
                foreach (string m in modules)
                {
                    string key = "m:" + m;
                    RailItem(m, null, subs.Count(s => s.Kind == SubKind.Module && Naming.SameName(s.Family, m)), subFilter == key, () => { subFilter = key; RebuildAll(); });
                }
            }
            List<string> groups = subs.Where(s => s.Kind == SubKind.Block).Select(s => GroupOf(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            if (groups.Count > 0)
            {
                RailHead("Building blocks");
                foreach (string g in groups)
                {
                    string key = "b:" + g;
                    RailItem(g, null, subs.Count(s => s.Kind == SubKind.Block && Naming.SameName(GroupOf(s), g)), subFilter == key, () => { subFilter = key; RebuildAll(); });
                }
            }
            railY += 4;
            Ui.Solid(Ui.Node(rail, "Sep").At(14, railY, RailW - 28, 1), Look.Line);
            railY += 5;
            int unfiled = subs.Count(s => s.Kind == SubKind.Unfiled);
            RailItem("Unfiled", null, unfiled, subFilter == UnfiledKey, () => { subFilter = UnfiledKey; RebuildAll(); }, warn: unfiled > 0);
            Kit.Text(rail, 16, H - TitleH - 76, RailW - 30, 64,
                "Modules are pieces of a family. Building blocks are the handy bits you reuse everywhere.", 12, Look.Ink2, align: TextAnchor.LowerLeft);
        }

        static string GroupOf(SubFile s) => string.IsNullOrEmpty(s.Group) ? "Other" : s.Group;

        // Picks something in the new view if the selection isn't in it.
        void Reselect()
        {
            confirmDelete = confirmLoad = false;
            CraftFile c = SelCraft;
            if (c != null && !CraftInView(c)) selectedCraftPath = null;
        }

        bool CraftInView(CraftFile c) =>
            craftFilter == AllKey ? true : craftFilter == UnfiledKey ? !c.Filed : c.Category != null && c.Category.Id == craftFilter;

        // ---- The middle column ---------------------------------------------------------------

        void BuildMid()
        {
            float bottom = H - TitleH;
            if (tab == Tab.Craft)
            {
                CraftHead();
                list = Ui.Node(mid, "List").At(0, 92, MidW, bottom - 92 - FootH);
                CraftFoot(bottom - FootH);
            }
            else if (tab == Tab.Launchers)
            {
                LauncherHead();
                list = Ui.Node(mid, "List").At(0, 88, MidW, bottom - 88 - 58);
                DropFoot(bottom - 58, "Drop a part here to save it, and everything under it, as a launcher");
            }
            else
            {
                SubHead();
                list = Ui.Node(mid, "List").At(0, 72, MidW, bottom - 72 - 58);
                DropFoot(bottom - 58, "Drop a part here to save it, and everything under it, as a module or block");
            }
            scroller = Kit.Scroll(list, 0, 0, MidW, list.sizeDelta.y);
            FillList();
        }

        void FillList()
        {
            if (tab == Tab.Craft) CraftList();
            else if (tab == Tab.Launchers) LauncherList();
            else SubList();
        }

        void SearchBox(float y, string text, Action<string> changed)
        {
            searchField = Kit.Input(mid, MidW - 16 - 190, y, 190, 32, text, "Search names", v => { changed(v); Later(RebuildList); }, padLeft: 30, size: 13);
            Kit.Glyph((RectTransform)searchField.Input.transform, 10, 9, 14, Look.Search, Look.Ink3);
            fields.Add(searchField);
        }

        void CraftHead()
        {
            List<CraftFile> shown = Library.Craft.Where(CraftInView).ToList();
            int families = shown.Where(c => c.Filed).Select(c => c.Facility + c.Category.Id + c.Family).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            string title = craftFilter == AllKey ? "All craft" : craftFilter == UnfiledKey ? "Unfiled" : Category.ById(craftFilter)?.Label ?? "";
            string eyebrow = craftFilter == UnfiledKey ? $"{shown.Count} craft not in a family yet"
                : $"{shown.Count} craft · {families} famil{(families == 1 ? "y" : "ies")}";
            Kit.Eyebrow(mid, 16, 14, MidW - 230, eyebrow);
            Kit.Line(mid, 16, 28, MidW - 230, 30, title, 22, Look.Ink, Look.Display, style: FontStyle.Bold);
            SearchBox(16, craftSearch, v => craftSearch = v);

            Kit.Line(mid, 16, 58, 34, 26, "SORT", 11, Look.Ink3);
            RectTransform seg = Kit.Panel(mid, "Sort", 56, 56, MidW - 72, 30, Look.Void, Look.Line, 8);
            var keys = (Sort[])Enum.GetValues(typeof(Sort));
            float bw = (MidW - 72 - 6 - 2 * (keys.Length - 1)) / keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                Sort k = keys[i];
                Btn b = Track(Kit.Segment(seg, k.ToString(), 3 + i * (bw + 2), 3, bw, 24, () => Later(() => { sort = k; RebuildAll(); })));
                b.On = sort == k;
                b.Label.fontStyle = b.On ? FontStyle.Bold : FontStyle.Normal;
                b.Refresh();
            }
            Kit.Rule(mid, 0, 91, MidW, 1);
        }

        void CraftFoot(float y)
        {
            RectTransform foot = Ui.Node(mid, "Foot").At(0, y, MidW, FootH);
            Ui.Solid(foot, Look.Panel2);
            Kit.Rule(foot, 0, 0, MidW, 1);
            int unfiled = Library.Craft.Count(c => !c.Filed);
            Kit.Line(foot, 16, 1, 270, FootH - 1, craftFilter == UnfiledKey ? "Pick a craft, then File it." : "Click a mark to see it. Double-click to load.", 13, Look.Ink2);
            if (unfiled > 0 && craftFilter != UnfiledKey)
            {
                Kit.Line(foot, MidW - 16 - 80 - 150, 1, 150, FootH - 1, $"{unfiled} craft aren't filed", 13, Look.Warn, align: TextAnchor.MiddleRight);
                Track(Kit.Link(foot, "File them", MidW - 16 - 74, 7, 74, 30, () => Later(() => { craftFilter = UnfiledKey; Reselect(); RebuildAll(); }), Look.Amber));
            }
        }

        void CraftList()
        {
            float w = scroller.Width - 32, y = 12;
            RectTransform content = scroller.Content;
            string q = craftSearch.Trim();

            if (craftFilter == UnfiledKey)
            {
                List<CraftFile> craft = Library.Craft.Where(c => !c.Filed && (q.Length == 0 || Has(c.Name, q))).OrderBy(c => c.Facility).ThenBy(c => c.Name).ToList();
                if (craft.Count == 0) Empty(content, "Nothing unfiled", q.Length > 0 ? "No unfiled craft match that search." : "Every craft is in a family.");
                foreach (CraftFile c in craft)
                {
                    y = UnfiledRow(content, c, 16, y, w) + 6;
                }
                scroller.SetHeight(y + 8);
                return;
            }

            IEnumerable<Family> fams = Library.Families.Where(f => craftFilter == AllKey || f.Category.Id == craftFilter);
            if (q.Length > 0) fams = fams.Where(f => Has(f.Name, q) || f.Members.Any(m => Has(m.Name, q)));
            List<Family> families = SortFamilies(fams).ToList();
            if (families.Count == 0)
                Empty(content, "Nothing here", q.Length > 0 ? "No families match that search." : "Save a design to start a family here.");
            foreach (Family f in families) y = FamilyCard(content, f, 16, y, w, q) + 10;

            if (families.Count > 0)
            {
                // The key to the mark buttons.
                RectTransform key = Ui.Node(content, "Key").At(16, y, w, 22);
                MarkChip(key, 0, 1, "II", false, false, null, small: true);
                Kit.Line(key, 30, 0, 110, 20, "newest mark", 12, Look.Ink2);
                MarkChip(key, 130, 1, "I", true, false, null, small: true);
                Kit.Line(key, 160, 0, 160, 20, "retired, still loads", 12, Look.Ink2);
                y += 28;
            }
            scroller.SetHeight(y + 8);
        }

        IEnumerable<Family> SortFamilies(IEnumerable<Family> fams)
        {
            switch (sort)
            {
                case Sort.Recent: return fams.OrderByDescending(f => f.Newest);
                case Sort.Mass: return fams.OrderBy(f => f.Members.Max(m => { Library.LoadStats(m.Newest); return m.Newest.Mass; }));
                case Sort.Cost: return fams.OrderBy(f => f.Members.Max(m => { Library.LoadStats(m.Newest); return m.Newest.Cost; }));
                default: return fams.OrderBy(f => f.Category.Label).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        static bool Has(string s, string q) => s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        void Empty(RectTransform content, string title, string text)
        {
            Kit.Text(content, 30, 40, scroller.Width - 60, 80,
                $"<size=18><b><color={Look.Html(Look.Ink2)}>{title}</color></b></size>\n{text}", 14, Look.Ink3, align: TextAnchor.MiddleCenter);
        }

        float FamilyCard(RectTransform content, Family f, float x, float y, float w, string q)
        {
            string key = f.Facility + "|" + f.Category.Id + "|" + f.Name;
            bool open = !collapsed.Contains(key) || q.Length > 0;
            List<Member> members = q.Length > 0 && !Has(f.Name, q) ? f.Members.Where(m => Has(m.Name, q)).ToList() : f.Members;
            const float headH = 52, rowH = 56;
            float h = headH + (open ? members.Count * rowH : 0);
            RectTransform card = Kit.Panel(content, f.Name, x, y, w, h, Look.Rail, Look.Line, 9);
            Ui.Slab(card, "Head", 1, 1, w - 2, headH - 1, Look.Panel2, 8, open ? Ui.Corners.Top : Ui.Corners.All);
            RectTransform head = Ui.Node(card, "HeadClick").At(0, 0, w, headH);
            Kit.Clicks(head, n => Later(() => { if (!collapsed.Remove(key)) collapsed.Add(key); RebuildList(); }));
            Kit.Glyph(card, 12, 19, 14, open ? Look.ChevronDown : Look.ChevronRight, Look.Ink2);
            Kit.Line(card, 34, 7, w - 120, 24, f.Name, 18, Look.Ink, Look.Display, style: FontStyle.Bold);
            int craftCount = f.Craft.Count();
            string facility = f.Facility == "SPH" ? " · SPH" : "";
            Kit.Line(card, 34, 29, w - 50, 16,
                $"{f.Category.Label} · {f.Members.Count} member{(f.Members.Count == 1 ? "" : "s")} · {craftCount} craft{facility}", 12, Look.Ink2);
            if (!open) return y + h;

            float ry = headH;
            foreach (Member m in members)
            {
                MemberRow(card, m, f, ry, w);
                ry += rowH;
            }
            return y + h;
        }

        void MemberRow(RectTransform card, Member m, Family f, float y, float w)
        {
            CraftFile sel = SelCraft;
            bool selected = sel != null && m.Marks.Contains(sel);
            RectTransform row = Ui.Node(card, m.Name).At(1, y, w - 2, 56);
            Ui.Solid(Ui.Node(row, "Line").At(0, 0, w - 2, 1), Color.Lerp(Look.Rail, Look.Line, 0.6f));
            if (selected)
            {
                Ui.Solid(Ui.Node(row, "Sel").At(0, 1, w - 2, 55), Look.Wash(Look.Rail, 0.08f));
                Ui.Solid(Ui.Node(row, "Bar").At(0, 1, 3, 55), Look.Amber);
            }
            CraftFile newest = m.Newest;
            Kit.Clicks(row, n => Later(() =>
            {
                if (n >= 2) { selectedCraftPath = newest.Path; host.LoadCraft(newest, false); return; }
                selectedCraftPath = (selected ? sel : newest).Path;
                confirmDelete = confirmLoad = false;
                RebuildList();
                RebuildDetail();
            }));

            Thumb(row, newest, 12, 8, 44, 40);
            Library.LoadStats(newest);
            CraftNote note = Library.NoteFor(newest);
            var bits = new List<string>();
            if (!string.IsNullOrEmpty(m.Role)) bits.Add(m.Role);
            if (newest.Mass > 0) bits.Add(Naming.Tons(note != null && note.Payload > 0 ? note.Payload : newest.Mass));
            if (note != null && note.Launcher.Length > 0) bits.Add("on " + note.Launcher);
            Kit.Line(row, 67, 9, w - 67 - 12 - 40 * Math.Min(4, m.Marks.Count), 20, m.Name, 14, Look.Ink, style: FontStyle.Bold);
            Kit.Line(row, 67, 29, w - 67 - 12 - 40 * Math.Min(4, m.Marks.Count), 18, string.Join(" · ", bits), 12, Look.Ink2);

            // Mark buttons, newest on the right; up to five, then older ones are left off.
            List<CraftFile> marks = m.Marks.Skip(Math.Max(0, m.Marks.Count - 5)).ToList();
            float mx = w - 12 - marks.Count * 36 + 4;
            foreach (CraftFile c in marks)
            {
                bool isNewest = c == newest;
                CraftFile pick = c;
                string label = c.Mark > 0 ? Marks.Short(c.Style, c.Mark) : "—";
                MarkChip(row, mx, 16, label, !isNewest, sel == c, () => Later(() =>
                {
                    selectedCraftPath = pick.Path;
                    confirmDelete = confirmLoad = false;
                    RebuildList();
                    RebuildDetail();
                }));
                mx += 36;
            }
        }

        void MarkChip(RectTransform parent, float x, float y, string label, bool old, bool selected, Action click, bool small = false)
        {
            float w = small ? 24 : 32, h = small ? 20 : 24;
            Sprite normal = old ? Look.Round(Look.Rail, Look.Line2, 6) : Look.Round(Look.Panel3, Look.Line2, 6);
            Sprite on = Look.Round(Look.Amber, Look.Amber, 6);
            Btn b = Btn.Make(parent, "Mark " + label, x, y, w, h, click)
                .Skin(normal, Look.Round(old ? Look.Rail : Look.Panel3, Look.AmberLine, 6), on, on)
                .Colors(old ? Look.Ink2 : Look.Ink, Look.Ink, Look.AmberInk)
                .WithLabel(label, Look.Display, small ? 11 : 13, FontStyle.Bold);
            b.On = selected;
            b.Refresh();
            if (click != null) Track(b);
        }

        void Thumb(RectTransform parent, CraftFile c, float x, float y, float w, float h)
        {
            RectTransform box = Kit.Panel(parent, "Thumb", x, y, w, h, Look.Panel2, Look.Line, 6);
            Texture2D t = c != null ? Library.Thumbnail(c) : null;
            if (t != null) Fit(box, t, w - 4, h - 4);
            else Kit.Glyph(box, (w - Mathf.Min(w, h) * 0.7f) / 2, (h - Mathf.Min(w, h) * 0.7f) / 2, Mathf.Min(w, h) * 0.7f, Look.Rocket, Look.Ink3);
        }

        static void Fit(RectTransform box, Texture t, float w, float h)
        {
            float s = Mathf.Min(w / t.width, h / t.height);
            float tw = t.width * s, th = t.height * s;
            Ui.Picture(Ui.Node(box, "Pic").At((box.sizeDelta.x - tw) / 2, (box.sizeDelta.y - th) / 2, tw, th), t, Color.white);
        }

        float UnfiledRow(RectTransform content, CraftFile c, float x, float y, float w)
        {
            bool selected = SelCraft == c;
            RectTransform row = Kit.Panel(content, c.Name, x, y, w, 56, selected ? Look.Wash(Look.Rail, 0.08f) : Look.Rail, selected ? Look.Amber : Look.Line, 8);
            Kit.Clicks(row, n => Later(() =>
            {
                selectedCraftPath = c.Path;
                if (n >= 2) { host.LoadCraft(c, false); return; }
                confirmDelete = confirmLoad = false;
                RebuildList();
                RebuildDetail();
            }));
            Thumb(row, c, 12, 8, 44, 40);
            Library.LoadStats(c);
            Kit.Line(row, 67, 9, w - 67 - 90, 20, c.Name, 14, Look.Ink, style: FontStyle.Bold);
            string where = c.Facility + (string.IsNullOrEmpty(c.RelDir) ? "" : " / " + c.RelDir);
            Kit.Line(row, 67, 29, w - 67 - 90, 18, $"{where}{(c.Mass > 0 ? " · " + Naming.Tons(c.Mass) : "")}", 12, Look.Ink2);
            Track(Kit.Ghost(row, "File…", w - 12 - 70, 13, 70, 30, () => Later(() => host.OpenFile(c))));
            return y + 56;
        }

        // ---- Launchers ---------------------------------------------------------------------

        float payloadMass, payloadWidth;
        bool payloadKnown;

        void MeasurePayload()
        {
            payloadKnown = false;
            payloadMass = payloadWidth = 0;
            ShipConstruct ship = Stock.Ship;
            if (ship?.parts == null) return;
            List<Part> parts = ship.parts.Where(p => !LauncherTags.IsLauncherPart(p)).ToList();
            if (parts.Count == 0) return;
            payloadMass = parts.Sum(p => p.mass + p.GetResourceMass());
            try
            {
                Part top = parts.FirstOrDefault(p => p.parent == null || LauncherTags.IsLauncherPart(p.parent)) ?? parts[0];
                Vector3 size = ShipConstruction.CalculateCraftSize(parts, top);
                payloadWidth = Mathf.Max(size.x, size.z);
            }
            catch { payloadWidth = 0; }
            payloadKnown = true;
        }

        void LauncherHead()
        {
            MeasurePayload();
            Kit.Eyebrow(mid, 16, 14, MidW - 32, "In the editor now");
            string name = Stock.ShipName;
            Kit.Line(mid, 16, 28, MidW - 32, 30, payloadKnown
                    ? $"{(string.IsNullOrEmpty(name) ? "This craft" : name)} <size=17><color={Look.Html(Look.Ink2)}><b>·</b> {Naming.Tons(payloadMass)}{(payloadWidth > 0 ? " · " + Naming.Meters(payloadWidth) + " wide" : "")}</color></size>"
                    : $"Nothing yet <size=17><color={Look.Html(Look.Ink2)}>· build a payload to check the fit</color></size>",
                22, Look.Ink, Look.Display, style: FontStyle.Bold);
            Btn box = Track(Btn.Make(mid, "FitOnly", 16, 60, 300, 22, () => Later(() => { fitOnly = !fitOnly; RebuildAll(); }))
                .Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel("Only show launchers that can lift it", Look.Body, 13, align: TextAnchor.MiddleLeft));
            box.Label.rectTransform.Stretch(24, 0, 0, 0);
            RectTransform tick = Kit.Panel(box.Rt, "Tick", 0, 3, 16, 16, fitOnly ? Look.Amber : Look.Void, fitOnly ? Look.Amber : Look.Line2, 4);
            if (fitOnly) Kit.Glyph(tick, 2, 2, 12, Look.Check, Look.AmberInk);
            box.Enabled = payloadKnown;
            box.Refresh();
            Kit.Rule(mid, 0, 87, MidW, 1);
        }

        bool Fits(SubFile s) => s.Rated && s.Rating + 0.001f >= payloadMass && (s.Width <= 0 || payloadWidth <= 0 || s.Width + 0.01f >= payloadWidth);

        void LauncherList()
        {
            float w = scroller.Width - 32, y = 12;
            RectTransform content = scroller.Content;
            if (launcherFilter == UpperKey)
            {
                List<SubFile> stages = Library.Subs.Where(s => s.Kind == SubKind.UpperStage).OrderBy(s => s.Name).ToList();
                if (stages.Count == 0) Empty(content, "No upper stages yet", "Drop a stage on the box below to save one.");
                foreach (SubFile s in stages) y = LauncherRow(content, s, 16, y, w, false, false) + 7;
                scroller.SetHeight(y + 8);
                return;
            }

            IEnumerable<SubFile> all = Library.Subs.Where(s => s.Kind == SubKind.Launcher);
            if (launcherFilter == UnratedKey) all = all.Where(s => !s.Rated);
            else if (launcherFilter != AllKey && Enum.TryParse(launcherFilter, out LauncherClass cls)) all = all.Where(s => s.Rated && Naming.ClassOf(s.Rating) == cls);
            List<SubFile> launchers = all.OrderBy(s => s.Rated ? s.Rating : float.MaxValue).ThenBy(s => s.Name).ToList();
            bool check = fitOnly && payloadKnown;
            List<SubFile> fit = launchers.Where(s => !check ? s.Rated : Fits(s)).ToList();
            List<SubFile> small = check ? launchers.Where(s => s.Rated && !Fits(s)).ToList() : new List<SubFile>();
            List<SubFile> unrated = launchers.Where(s => !s.Rated).ToList();

            if (launchers.Count == 0)
                Empty(content, "No launchers here", "Save a launcher by dropping its top part on the box below.");
            bool first = true;
            foreach (SubFile s in fit) { y = LauncherRow(content, s, 16, y, w, check && first, false) + 7; first = false; }
            if (small.Count > 0)
            {
                Kit.Eyebrow(content, 18, y + 4, w, "Too small or too narrow");
                y += 24;
                foreach (SubFile s in small) y = LauncherRow(content, s, 16, y, w, false, true) + 7;
            }
            if (unrated.Count > 0)
            {
                Kit.Eyebrow(content, 18, y + 4, w, "Not rated yet");
                y += 24;
                foreach (SubFile s in unrated) y = LauncherRow(content, s, 16, y, w, false, false) + 7;
            }
            scroller.SetHeight(y + 8);
        }

        float LauncherRow(RectTransform content, SubFile s, float x, float y, float w, bool best, bool dim)
        {
            Library.LoadStats(s);
            bool selected = SelSub == s;
            RectTransform row = Kit.Panel(content, s.Name, x, y, w, 58, selected ? Look.Wash(Look.Rail, 0.08f) : Look.Rail, selected ? Look.Amber : Look.Line, 8);
            Kit.Clicks(row, n => Later(() =>
            {
                selectedSubPath = s.Path;
                confirmDelete = false;
                if (n >= 2) { host.PickUp(s); return; }
                RebuildList();
                RebuildDetail();
            }));
            Color ink = dim ? Look.Fade(Look.Ink, 0.5f) : Look.Ink, ink2 = dim ? Look.Fade(Look.Ink2, 0.6f) : Look.Ink2;
            RectTransform th = Kit.Panel(row, "Thumb", 12, 8, 40, 42, Look.Panel2, Look.Line, 6);
            Kit.Glyph(th, 4, 5, 32, Look.Rocket, dim ? Look.Fade(Look.Amber, 0.4f) : Look.Fade(Look.Amber, 0.8f));

            float textW = w - 64 - 150;
            Text name = Kit.Line(row, 64, 9, textW, 20, s.Name, 14, ink, style: FontStyle.Bold);
            if (best)
            {
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                float nx = 64 + Mathf.Min(textW - 70, name.preferredWidth) + 8;
                RectTransform tag = Kit.Panel(row, "Best", nx, 11, 62, 16, Look.Good, Look.Good, 4);
                Kit.Line(tag, 0, 0, 62, 16, "BEST FIT", 10, Look.AmberInk, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
            }
            string sub;
            if (s.Kind == SubKind.UpperStage)
                sub = $"Upper stage{(s.Mass > 0 ? " · " + Naming.Tons(s.Mass) : "")}{(s.Stages > 0 ? $" · {s.Stages} stage{(s.Stages == 1 ? "" : "s")}" : "")}";
            else if (!s.Rated) sub = "Add a rating to check whether it fits.";
            else if (dim) sub = s.Rating + 0.001f < payloadMass ? $"Lifts {Naming.Tons(s.Rating)}. This payload is {Naming.Tons(payloadMass)}."
                : $"Up to {Naming.Meters(s.Width)} wide. This payload is {Naming.Meters(payloadWidth)}.";
            else sub = $"Lifts {Naming.Tons(s.Rating)}{(s.Width > 0 ? " · up to " + Naming.Meters(s.Width) : "")}" +
                       $"{(s.Stages > 0 ? $" · {s.Stages} stage{(s.Stages == 1 ? "" : "s")}" : "")}{(s.Cost > 0 ? " · " + Naming.Funds(s.Cost) : "")}";
            Kit.Line(row, 64, 30, textW + 60, 18, sub, 12, ink2);

            if (s.Rated && payloadKnown && !dim)
            {
                float pct = Mathf.Clamp01(payloadMass / s.Rating);
                RectTransform bar = Kit.Panel(row, "Bar", w - 12 - 120, 18, 120, 6, Look.Panel3, Look.Panel3, 3);
                if (pct > 0.02f) Kit.Panel(bar, "Fill", 0, 0, Mathf.Max(6, 120 * pct), 6, Look.Amber, Look.Amber, 3);
                Kit.Line(row, w - 12 - 140, 27, 140, 16, best ? $"{Mathf.RoundToInt(pct * 100)}% of its rating" : $"{Mathf.RoundToInt(pct * 100)}%",
                    12, Look.Ink2, Look.Display, TextAnchor.MiddleRight);
            }
            else if (!s.Rated && s.Kind == SubKind.Launcher)
                Track(Kit.Ghost(row, "Rate it", w - 12 - 76, 14, 76, 30, () => Later(() => { selectedSubPath = s.Path; RebuildList(); RebuildDetail(); })));
            return y + 58;
        }

        // ---- Subassemblies ---------------------------------------------------------------------

        void SubHead()
        {
            List<SubFile> subs = SubsInView(false).ToList();
            string title = subFilter == AllKey ? "All subassemblies" : subFilter == UnfiledKey ? "Unfiled" : subFilter.Substring(2);
            Kit.Eyebrow(mid, 16, 14, MidW - 230, $"{subs.Count} subassembl{(subs.Count == 1 ? "y" : "ies")} · launchers have their own tab");
            Kit.Line(mid, 16, 28, MidW - 230, 30, title, 22, Look.Ink, Look.Display, style: FontStyle.Bold);
            SearchBox(18, subSearch, v => subSearch = v);
            Kit.Rule(mid, 0, 71, MidW, 1);
        }

        IEnumerable<SubFile> SubsInView(bool search)
        {
            IEnumerable<SubFile> subs = Library.Subs.Where(s => s.Kind != SubKind.Launcher && s.Kind != SubKind.UpperStage);
            if (subFilter == UnfiledKey) subs = subs.Where(s => s.Kind == SubKind.Unfiled);
            else if (subFilter != null && subFilter.StartsWith("m:")) subs = subs.Where(s => s.Kind == SubKind.Module && Naming.SameName(s.Family, subFilter.Substring(2)));
            else if (subFilter != null && subFilter.StartsWith("b:")) subs = subs.Where(s => s.Kind == SubKind.Block && Naming.SameName(GroupOf(s), subFilter.Substring(2)));
            if (search && subSearch.Trim().Length > 0) subs = subs.Where(s => Has(s.Name, subSearch.Trim()));
            return subs;
        }

        void SubList()
        {
            float w = scroller.Width - 32, y = 12;
            RectTransform content = scroller.Content;
            List<SubFile> subs = SubsInView(true).ToList();
            if (subs.Count == 0) Empty(content, "Nothing here", subSearch.Trim().Length > 0 ? "Nothing matches that search." : "Drop a part on the box below to save one.");

            var sections = new List<KeyValuePair<string, List<SubFile>>>();
            foreach (IGrouping<string, SubFile> g in subs.Where(s => s.Kind == SubKind.Module).GroupBy(s => s.Family, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
                sections.Add(new KeyValuePair<string, List<SubFile>>($"Modules · <b><color={Look.Html(Look.Ink)}>{g.Key} family</color></b>", g.OrderBy(s => s.Name).ToList()));
            foreach (IGrouping<string, SubFile> g in subs.Where(s => s.Kind == SubKind.Block).GroupBy(GroupOf, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
                sections.Add(new KeyValuePair<string, List<SubFile>>($"Building blocks · <b><color={Look.Html(Look.Ink)}>{g.Key}</color></b>", g.OrderBy(s => s.Name).ToList()));
            List<SubFile> unfiled = subs.Where(s => s.Kind == SubKind.Unfiled).OrderBy(s => s.Name).ToList();
            if (unfiled.Count > 0) sections.Add(new KeyValuePair<string, List<SubFile>>($"<color={Look.Html(Look.Warn)}>Unfiled</color>", unfiled));

            const int cols = 4;
            float gap = 7, tw = (w - gap * (cols - 1)) / cols, thh = 84;
            foreach (KeyValuePair<string, List<SubFile>> sec in sections)
            {
                Text head = Kit.Line(content, 18, y, w, 18, sec.Key.ToUpperInvariant().Replace("<B>", "<b>").Replace("</B>", "</b>")
                    .Replace("<COLOR=", "<color=").Replace("</COLOR>", "</color>"), 11, Look.Ink3);
                y += 22;
                for (int i = 0; i < sec.Value.Count; i++)
                {
                    SubFile s = sec.Value[i];
                    Tile(content, s, 16 + (i % cols) * (tw + gap), y + (i / cols) * (thh + gap), tw, thh);
                }
                y += ((sec.Value.Count + cols - 1) / cols) * (thh + gap) + 8;
            }
            scroller.SetHeight(y + 8);
        }

        void Tile(RectTransform content, SubFile s, float x, float y, float w, float h)
        {
            Library.LoadStats(s);
            bool selected = SelSub == s;
            RectTransform tile = Kit.Panel(content, s.Name, x, y, w, h, selected ? Look.Wash(Look.Rail, 0.08f) : Look.Rail, selected ? Look.Amber : Look.Line, 8);
            Kit.Clicks(tile, n => Later(() =>
            {
                selectedSubPath = s.Path;
                confirmDelete = false;
                if (n >= 2) { host.PickUp(s); return; }
                RebuildList();
                RebuildDetail();
            }));
            Kit.Glyph(tile, (w - 34) / 2, 7, 34, s.Kind == SubKind.Module ? Look.Module : Look.Module, s.Kind == SubKind.Unfiled ? Look.Ink3 : Look.Fade(Look.Ink, 0.75f));
            Text name = Kit.Text(tile, 4, 42, w - 8, 28, s.Name, 12, Look.Ink, align: TextAnchor.UpperCenter, style: FontStyle.Bold);
            name.verticalOverflow = VerticalWrapMode.Truncate;
            Kit.Line(tile, 4, h - 18, w - 8, 14, s.Diameter > 0 ? Naming.Meters(s.Diameter) : "", 11, Look.Ink2, align: TextAnchor.MiddleCenter);
        }

        // ---- The drop box ---------------------------------------------------------------------

        void DropFoot(float y, string text)
        {
            RectTransform foot = Ui.Node(mid, "Foot").At(0, y, MidW, 58);
            Ui.Solid(foot, Look.Panel2);
            Kit.Rule(foot, 0, 0, MidW, 1);
            dropZone = Ui.Node(foot, "Drop").At(16, 9, MidW - 32, 40);
            dropBg = Ui.Box(dropZone, Look.Round(Look.Panel2, Look.Line2, 8), raycast: true);
            Kit.Glyph(dropZone, 14, 12, 16, Look.Drop, Look.Ink2);
            dropText = Kit.Line(dropZone, 38, 0, MidW - 32 - 50, 40, text, 13, Look.Ink2);
            // Holding a part: save that part and what hangs under it. Holding nothing:
            // save the whole craft in the editor (a launcher built on its own, say).
            Kit.Clicks(dropZone, n =>
            {
                SubKind kind = tab == Tab.Launchers ? SubKind.Launcher : SubKind.Block;
                Part held = Stock.Held;
                if (held != null) Later(() => host.OpenSubDialog(null, held, kind));
                else if (EditorLogic.RootPart != null && Stock.Ship != null && Stock.Ship.parts.Count > 0)
                    Later(() => host.OpenSubDialog(null, EditorLogic.RootPart, kind, wholeCraft: true));
            });
            dropState = -1;
        }

        int dropState = -1;

        void RefreshDrop()
        {
            if (dropZone == null) return;
            int state = Stock.Held != null ? 2 : EditorLogic.RootPart != null && Stock.Ship != null && Stock.Ship.parts.Count > 0 ? 1 : 0;
            if (state == dropState) return;
            dropState = state;
            bool hot = state == 2;
            Ui.SetSprite(dropBg, hot ? Look.Round(Look.Wash(Look.Panel2, 0.15f), Look.Amber, 8) : Look.Round(Look.Panel2, Look.Line2, 8));
            dropText.color = hot ? Look.Amber : Look.Ink2;
            string what = tab == Tab.Launchers ? "a launcher" : "a module or block";
            if (state == 2) dropText.text = $"Click here to save {Stock.Held.partInfo?.title ?? "this part"} and everything under it";
            else if (state == 1) dropText.text = $"Click to save the whole craft as {what}, or pick up a part to save just that piece";
            else dropText.text = $"Build something or pick up a part, then click here to save it as {what}";
        }

        // ---- The right column -------------------------------------------------------------------

        void BuildDetail()
        {
            Ui.Slab(detail, "Bg", 0, 0, DetailW - 1, H - TitleH - 1, Look.Rail, 9, Ui.Corners.BottomRight);
            Ui.Solid(Ui.Node(detail, "Line").At(0, 0, 1, H - TitleH - 1), Look.Line);
            if (tab == Tab.Craft)
            {
                CraftFile c = SelCraft;
                if (c == null) DetailHint("Pick a craft", "Its picture, numbers and buttons show here.");
                else CraftDetail(c);
            }
            else
            {
                SubFile s = SelSub;
                bool inTab = s != null && (tab == Tab.Launchers ? s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage
                                                               : s.Kind != SubKind.Launcher && s.Kind != SubKind.UpperStage);
                if (!inTab) DetailHint(tab == Tab.Launchers ? "Pick a launcher" : "Pick a subassembly", "Pick it up from here to attach it, like any subassembly.");
                else if (s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage) LauncherDetail(s);
                else SubDetail(s);
            }
        }

        void DetailHint(string title, string text) =>
            Kit.Text(detail, 24, 180, DetailW - 48, 120,
                $"<size=17><b><color={Look.Html(Look.Ink2)}>{title}</color></b></size>\n{text}", 13, Look.Ink3, align: TextAnchor.MiddleCenter);

        const float DX = 14, DW = DetailW - 28;

        void CraftDetail(CraftFile c)
        {
            Library.LoadStats(c);
            float y = 14;
            RectTransform pic = Kit.Panel(detail, "Picture", DX, y, DW, 124, Look.Hero, Look.Line, 8);
            Texture2D t = Library.Thumbnail(c);
            if (t != null) Fit(pic, t, DW - 6, 118);
            else Kit.Glyph(pic, (DW - 70) / 2, 27, 70, Look.Rocket, Look.Ink3);
            y += 134;

            Family f = Library.FamilyOf(c);
            Member m = Library.MemberOf(c);
            Kit.Eyebrow(detail, DX, y, DW, c.Filed ? $"{c.Category.Label} · {c.Family} family" : $"Unfiled · {c.Facility}{(c.RelDir.Length > 0 ? " / " + c.RelDir : "")}");
            Text title = Kit.Text(detail, DX, y + 14, DW, 52, c.Name, 22, Look.Ink, Look.Display, TextAnchor.UpperLeft, FontStyle.Bold);
            float th = Mathf.Min(52, title.preferredHeight + 2);
            title.rectTransform.At(DX, y + 14, DW, th);
            y += 18 + th;

            float px = DX;
            if (c.Filed && c.Mark > 0)
            {
                bool newest = m != null && m.Newest == c;
                px += Kit.Pill(detail, px, y, $"Mark {Marks.Short(c.Style, c.Mark)} · {(newest ? "newest" : "retired")}", newest) + 6;
            }
            if (c.Role.Length > 0) px += Kit.Pill(detail, px, y, c.Role) + 6;
            Kit.Pill(detail, px, y, c.Facility);
            y += 32;

            CraftNote note = Library.NoteFor(c);
            if (note != null && note.Launcher.Length > 0)
            {
                RectTransform on = Kit.Panel(detail, "BuiltOn", DX, y, DW, 44, Look.Panel2, Look.Line, 8);
                Kit.Glyph(on, 8, 8, 28, Look.Rocket, Look.Amber);
                Kit.Line(on, 42, 5, DW - 50, 14, "BUILT ON", 10, Look.Ink3);
                SubFile l = Library.SubNamed(note.Launcher);
                Kit.Line(on, 42, 19, DW - 50, 20,
                    $"<b>{note.Launcher}</b>{(l != null && l.Rated ? $" <color={Look.Html(Look.Ink2)}>· lifts {Naming.Tons(l.Rating)}</color>" : "")}", 14, Look.Ink);
                y += 52;
            }

            float payload = note != null ? note.Payload : 0;
            Stats(ref y, new[]
            {
                ("Parts", c.Parts > 0 ? c.Parts.ToString() : "—"),
                ("Payload", payload > 0 ? Naming.Tons(payload) : "—"),
                ("Total", c.Mass > 0 ? Naming.Tons(c.Mass) : "—"),
                ("Cost", c.Cost > 0 ? Naming.Funds(c.Cost) : "—"),
                ("Height", c.Size.y > 0 ? Naming.Meters(c.Size.y) : "—"),
                ("Stages", c.Stages > 0 ? c.Stages.ToString() : "—"),
            });
            if (c.Description.Length > 0)
            {
                Text d = Kit.Text(detail, DX, y, DW, 60, c.Description, 13, Look.Ink2, align: TextAnchor.UpperLeft);
                d.verticalOverflow = VerticalWrapMode.Truncate;
            }

            float bottom = H - TitleH - 14;
            if (confirmLoad)
            {
                Kit.Text(detail, DX, bottom - 122, DW, 36, "Your changes in the editor haven't been saved. They'll be lost.", 13, Look.Warn, align: TextAnchor.LowerLeft);
                Track(Kit.Ghost(detail, "Keep editing", DX, bottom - 80, (DW - 8) / 2, 38, () => Later(() => { confirmLoad = false; RebuildDetail(); })));
                Track(Kit.Primary(detail, mergeAfterConfirm ? "Merge anyway" : "Load anyway", DX + (DW + 8) / 2, bottom - 80, (DW - 8) / 2, 38,
                    () => Later(() => { confirmLoad = false; host.LoadCraft(c, mergeAfterConfirm, force: true); }), 14));
                return;
            }
            if (confirmDelete)
            {
                Kit.Text(detail, DX, bottom - 122, DW, 36, $"Move {c.Name} to the save's Deleted craft folder?", 13, Look.Warn, align: TextAnchor.LowerLeft);
                Track(Kit.Ghost(detail, "Keep it", DX, bottom - 80, (DW - 8) / 2, 38, () => Later(() => { confirmDelete = false; RebuildDetail(); })));
                Track(Kit.Primary(detail, "Delete", DX + (DW + 8) / 2, bottom - 80, (DW - 8) / 2, 38, () => Later(() =>
                {
                    confirmDelete = false;
                    host.DeleteCraft(c);
                }), 14));
                return;
            }
            Track(Kit.Primary(detail, "Load", DX, bottom - 104, DW, 40, () => Later(() => TryLoad(c, false)), 16));
            Track(Kit.Ghost(detail, "Merge", DX, bottom - 58, (DW - 8) / 2, 34, () => Later(() => TryLoad(c, true))));
            if (c.Filed) Track(Kit.Ghost(detail, "Rename", DX + (DW + 8) / 2, bottom - 58, (DW - 8) / 2, 34, () => Later(() => host.OpenRename(f))));
            else Track(Kit.Ghost(detail, "File…", DX + (DW + 8) / 2, bottom - 58, (DW - 8) / 2, 34, () => Later(() => host.OpenFile(c))));
            Track(Kit.Link(detail, "Delete…", DX, bottom - 20, DW, 20, () => Later(() => { confirmDelete = true; RebuildDetail(); }), size: 12));
        }

        void TryLoad(CraftFile c, bool merge)
        {
            if (!merge && Stock.Unsaved)
            {
                confirmLoad = true;
                mergeAfterConfirm = false;
                RebuildDetail();
                return;
            }
            host.LoadCraft(c, merge);
        }

        void Stats(ref float y, (string, string)[] stats)
        {
            float cw = DW / 3f;
            for (int i = 0; i < stats.Length; i++)
            {
                float sx = DX + (i % 3) * cw, sy = y + (i / 3) * 40;
                Kit.Line(detail, sx, sy, cw - 4, 14, stats[i].Item1.ToUpperInvariant(), 10, Look.Ink3);
                Kit.Line(detail, sx, sy + 14, cw - 4, 22, stats[i].Item2, 16, Look.Ink, Look.Display, style: FontStyle.Bold);
            }
            y += ((stats.Length + 2) / 3) * 40 + 4;
        }

        void LauncherDetail(SubFile s)
        {
            Library.LoadStats(s);
            float y = 14;
            RectTransform pic = Kit.Panel(detail, "Picture", DX, y, DW, 100, Look.Hero, Look.Line, 8);
            Kit.Glyph(pic, (DW - 80) / 2, 10, 80, Look.Rocket, Look.Fade(Look.Amber, 0.85f));
            y += 110;
            string kind = s.Kind == SubKind.UpperStage ? "Upper stage" : s.Rated ? $"Launcher · {Naming.ClassLabel(Naming.ClassOf(s.Rating))}" : "Launcher · not rated";
            Kit.Eyebrow(detail, DX, y, DW, $"{kind}{(s.Family.Length > 0 ? " · " + s.Family + " family" : "")}");
            Kit.Line(detail, DX, y + 14, DW, 30, s.Name, 22, Look.Ink, Look.Display, style: FontStyle.Bold);
            y += 50;
            float px = DX;
            bool newest = !Library.Subs.Any(o => o != s && o.Kind == s.Kind && Naming.SameName(o.Family, s.Family) && o.Family.Length > 0 &&
                                                  (s.Kind != SubKind.Launcher || Naming.Code(o.Rating) == Naming.Code(s.Rating)) && o.Mark > s.Mark);
            px += Kit.Pill(detail, px, y, $"Mark {Marks.Short(s.Style, s.Mark)} · {(newest ? "newest" : "retired")}", newest) + 6;
            y += 32;

            if (s.Kind == SubKind.Launcher)
            {
                RectTransform box = Kit.Panel(detail, "Rating", DX, y, DW, 120, Look.Panel2, Look.Line, 8);
                float half = (DW - 28) / 2;
                Kit.Line(box, 10, 8, half, 14, "LIFTS TO ORBIT", 10, Look.Ink3);
                Kit.Line(box, 18 + half, 8, half, 14, "PAYLOAD UP TO", 10, Look.Ink3);
                Kit.Field tons = Kit.Input(box, 10, 24, half, 32, s.Rating > 0 ? Num(s.Rating) : "", "tons", null,
                    v => Later(() => host.SetRating(s, Parse(v), s.Width, s.Notes)), 16, Look.Display, limit: 8);
                Kit.Line(box, 10 + half - 22, 24, 18, 32, "t", 13, Look.Ink2);
                Kit.Field width = Kit.Input(box, 18 + half, 24, half, 32, s.Width > 0 ? Num(s.Width) : "", "meters", null,
                    v => Later(() => host.SetRating(s, s.Rating, Parse(v), s.Notes)), 16, Look.Display, limit: 8);
                Kit.Line(box, 18 + 2 * half - 22, 24, 18, 32, "m", 13, Look.Ink2);
                Kit.Line(box, 10, 62, DW - 20, 14, "TEST NOTES", 10, Look.Ink3);
                Kit.Field notes = Kit.Input(box, 10, 78, DW - 20, 32, s.Notes, "How you tested it", null,
                    v => Later(() => host.SetRating(s, s.Rating, s.Width, v)), 13, limit: 120);
                fields.Add(tons);
                fields.Add(width);
                fields.Add(notes);
                y += 128;
            }

            Stats(ref y, new[]
            {
                ("Parts", s.Parts > 0 ? s.Parts.ToString() : "—"),
                ("Mass", s.Mass > 0 ? Naming.Tons(s.Mass) : "—"),
                ("Cost", s.Cost > 0 ? Naming.Funds(s.Cost) : "—"),
            });
            List<string> flies = Library.FlownOn(s).Select(c => c.Name).ToList();
            if (flies.Count > 0)
            {
                Kit.Line(detail, DX, y, DW, 14, "FLIES", 10, Look.Ink3);
                Text f = Kit.Text(detail, DX, y + 14, DW, 36, string.Join(", ", flies), 12, Look.Ink2, align: TextAnchor.UpperLeft);
                f.verticalOverflow = VerticalWrapMode.Truncate;
            }
            SubButtons(s, "Attach it under your payload, like any subassembly.");
        }

        void SubDetail(SubFile s)
        {
            Library.LoadStats(s);
            float y = 14;
            RectTransform pic = Kit.Panel(detail, "Picture", DX, y, DW, 100, Look.Hero, Look.Line, 8);
            Kit.Glyph(pic, (DW - 80) / 2, 10, 80, Look.Module, Look.Fade(Look.Ink, 0.7f));
            y += 110;
            string kind = s.Kind == SubKind.Module ? $"Module · {s.Family} family" : s.Kind == SubKind.Block ? $"Building block · {GroupOf(s)}" : "Unfiled subassembly";
            Kit.Eyebrow(detail, DX, y, DW, kind);
            Kit.Line(detail, DX, y + 14, DW, 30, s.Name, 22, Look.Ink, Look.Display, style: FontStyle.Bold);
            y += 50;
            float px = DX;
            if (s.Diameter > 0) px += Kit.Pill(detail, px, y, Naming.Meters(s.Diameter)) + 6;
            if (s.Role.Length > 0) Kit.Pill(detail, px, y, s.Role);
            y += 32;
            Stats(ref y, new[]
            {
                ("Parts", s.Parts > 0 ? s.Parts.ToString() : "—"),
                ("Mass", s.Mass > 0 ? Naming.Tons(s.Mass) : "—"),
                ("Cost", s.Cost > 0 ? Naming.Funds(s.Cost) : "—"),
            });
            if (s.Contents.Count > 0)
            {
                Kit.Line(detail, DX, y, DW, 14, "CONTAINS", 10, Look.Ink3);
                Text t = Kit.Text(detail, DX, y + 14, DW, 52, string.Join(", ", s.Contents.Take(8)) + (s.Contents.Count > 8 ? "…" : ""), 12, Look.Ink2,
                    align: TextAnchor.UpperLeft);
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            SubButtons(s, null);
        }

        void SubButtons(SubFile s, string caption)
        {
            float bottom = H - TitleH - 14;
            if (confirmDelete)
            {
                Kit.Text(detail, DX, bottom - 122, DW, 36, $"Move {s.Name} to the save's Deleted craft folder?", 13, Look.Warn, align: TextAnchor.LowerLeft);
                Track(Kit.Ghost(detail, "Keep it", DX, bottom - 80, (DW - 8) / 2, 38, () => Later(() => { confirmDelete = false; RebuildDetail(); })));
                Track(Kit.Primary(detail, "Delete", DX + (DW + 8) / 2, bottom - 80, (DW - 8) / 2, 38, () => Later(() =>
                {
                    confirmDelete = false;
                    host.DeleteSub(s);
                }), 14));
                return;
            }
            float by = bottom - (caption != null ? 104 : 86);
            Track(Kit.Primary(detail, "Pick up", DX, by, DW, 40, () => Later(() => host.PickUp(s)), 16));
            if (caption != null) Kit.Line(detail, DX, by + 42, DW, 18, caption, 12, Look.Ink2, align: TextAnchor.MiddleCenter);
            float ly = bottom - 20;
            string fileLabel = s.Kind == SubKind.Unfiled ? "File it…" : "Change…";
            Track(Kit.Link(detail, fileLabel, DX, ly, DW / 2, 20, () => Later(() => host.OpenSubDialog(s, null, s.Kind == SubKind.Unfiled ? (tab == Tab.Launchers ? SubKind.Launcher : SubKind.Block) : s.Kind)),
                s.Kind == SubKind.Unfiled ? Look.Amber : (Color?)null, size: 12));
            Track(Kit.Link(detail, "Delete…", DX + DW / 2, ly, DW / 2, 20, () => Later(() => { confirmDelete = true; RebuildDetail(); }), size: 12));
        }

        static string Num(float f) => f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static float Parse(string s) =>
            float.TryParse((s ?? "").Replace(',', '.').Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) && f > 0 ? f : 0;

        public void Select(CraftFile c)
        {
            if (c == null) return;
            selectedCraftPath = c.Path;
            if (c.Filed) collapsed.Remove(c.Facility + "|" + c.Category.Id + "|" + c.Family);
            craftFilter = c.Filed ? c.Category.Id : UnfiledKey;
            tab = Tab.Craft;
        }

        public void Select(SubFile s)
        {
            if (s == null) return;
            selectedSubPath = s.Path;
            tab = s.Kind == SubKind.Launcher || s.Kind == SubKind.UpperStage ? Tab.Launchers : Tab.Subs;
            if (tab == Tab.Subs) subFilter = AllKey;
            if (tab == Tab.Launchers && launcherFilter != AllKey) launcherFilter = AllKey;
        }

        public void Refresh() { if (root != null) RebuildAll(); }

        // ---- Per frame ---------------------------------------------------------------------

        public void Update()
        {
            if (pending != null)
            {
                Action a = pending;
                pending = null;
                try { a(); }
                catch (Exception e) { Debug.LogError($"[PhilsVesselNaming] {e}"); }
            }
            if (root != null && builtVersion != Library.Version) RebuildAll();
        }

        public void LateUpdate()
        {
            if (root == null) { ReleaseLocks(); return; }
            try
            {
                buttons.RemoveAll(b => b.Rt == null);
                foreach (Btn b in buttons) b.Refresh();
                fields.RemoveAll(f => f.Input == null);
                foreach (Kit.Field f in fields) f.Refresh();
                RefreshDrop();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsVesselNaming] Window update failed, closing it: {e}");
                host.CloseWindow();
                return;
            }
            SetLock(ref windowLocked, LockId, WindowLock, MouseOver() || drag != null && drag.Dragging);
            SetLock(ref typingLocked, TypingLockId, ControlTypes.KEYBOARDINPUT, fields.Any(f => f.Focused));
        }

        public static void SetLock(ref bool isSet, string id, ControlTypes types, bool want)
        {
            if (want == isSet) return;
            if (want) InputLockManager.SetControlLock(types, id);
            else InputLockManager.RemoveControlLock(id);
            isSet = want;
        }

        void ReleaseLocks()
        {
            SetLock(ref windowLocked, LockId, WindowLock, false);
            SetLock(ref typingLocked, TypingLockId, ControlTypes.KEYBOARDINPUT, false);
        }

        public bool MouseOver()
        {
            if (root == null) return false;
            Canvas canvas = root.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(root, Input.mousePosition, cam);
        }
    }
}
