// The small windows: Save a new design (and filing an old craft), Save the
// next mark, saving or filing a subassembly, and renaming a family. Each sits
// over a dimmed screen, so nothing behind it can be clicked while it's open.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PhilsBuilds
{
    public abstract class Dialog
    {
        protected readonly BuildsEditor host;
        protected readonly List<Btn> buttons = new List<Btn>();
        protected readonly List<Kit.Field> fields = new List<Kit.Field>();
        protected RectTransform root, body, foot, popup;
        RectTransform blocker;
        protected float W, H;
        protected const float TitleH = 56, FootH = 64, Pad = 18;
        Action pending;
        bool locked, typing;
        const string LockId = "PhilsBuildsDialog", TypingLockId = "PhilsBuildsDialogTyping";

        protected Dialog(BuildsEditor host) { this.host = host; }

        public bool IsOpen => root != null;

        protected void Open(string title, string subtitle, float w, float h)
        {
            Look.Init();
            W = w;
            H = h;
            Canvas canvas = UIMasterController.Instance.dialogCanvas;
            blocker = Ui.Node(canvas.transform, "PhilsBuildsDim").Stretch();
            Ui.Solid(blocker, new Color(0, 0, 0, 0.45f), raycast: true);
            root = Ui.Node(canvas.transform, "PhilsBuildsDialog");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(w, h);
            root.anchoredPosition = Vector2.zero;
            Ui.Box(root, Look.Round(Look.Panel, Look.Line, 10), raycast: true);

            Ui.Slab(root, "TitleBar", 1, 1, w - 2, TitleH - 1, Look.Panel2, 9, Ui.Corners.Top);
            Ui.Solid(Ui.Node(root, "TitleLine").At(1, TitleH - 1, w - 2, 1), Look.Line);
            Ui.Picture(Ui.Node(root, "Logo").At(16, 17, 22, 22), Look.Tag, Look.Amber);
            Kit.Line(root, 48, 8, w - 100, 24, title, 19, Look.Ink, Look.Display, style: FontStyle.Bold);
            Kit.Line(root, 48, 31, w - 100, 16, subtitle, 12, Look.Ink2);
            buttons.Add(Btn.Make(root, "Close", w - 40, 13, 30, 30, () => Later(Close))
                .Skin(null, Look.Round(Look.Panel3, Look.Panel3, 6)).Colors(Look.Ink2, Look.Ink, Look.Ink).WithGlyph(Look.Cross, 12));

            body = Ui.Node(root, "Body").At(0, TitleH, w, h - TitleH - FootH);
            RectTransform footBg = Ui.Slab(root, "FootBg", 1, h - FootH, w - 2, FootH - 1, Look.Panel2, 9, Ui.Corners.Bottom);
            Ui.Solid(Ui.Node(root, "FootLine").At(1, h - FootH, w - 2, 1), Look.Line);
            foot = Ui.Node(root, "Foot").At(0, h - FootH, w, FootH);
            blocker.SetAsLastSibling();
            root.SetAsLastSibling();
            Rebuild();
        }

        protected void Rebuild()
        {
            if (root == null) return;
            ClosePopup();
            buttons.RemoveAll(b => b.Rt == null || b.Rt.IsChildOf(body) || b.Rt.IsChildOf(foot));
            fields.Clear();
            Kit.Clear(body);
            Kit.Clear(foot);
            BuildBody();
            BuildFoot();
        }

        protected abstract void BuildBody();
        protected abstract void BuildFoot();
        protected virtual void PerFrame() { }

        protected void Later(Action a) => pending = a;

        protected Btn Track(Btn b) { buttons.Add(b); return b; }

        public void Close()
        {
            SetLocks(false);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            if (blocker != null) UnityEngine.Object.Destroy(blocker.gameObject);
            root = blocker = null;
            buttons.Clear();
            fields.Clear();
            host.DialogClosed(this);
        }

        public void Update()
        {
            if (pending != null)
            {
                Action a = pending;
                pending = null;
                try { a(); }
                catch (Exception e) { Debug.LogError($"[PhilsBuilds] {e}"); }
            }
        }

        public void LateUpdate()
        {
            if (root == null) { SetLocks(false); return; }
            try
            {
                buttons.RemoveAll(b => b.Rt == null);
                foreach (Btn b in buttons) b.Refresh();
                fields.RemoveAll(f => f.Input == null);
                foreach (Kit.Field f in fields) f.Refresh();
                PerFrame();
                bool typing = fields.Any(f => f.Focused);
                if (Input.GetKeyDown(KeyCode.Escape) && !typing) Later(Close);
                // Enter presses the orange button, once no text box is being typed in.
                if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !typing &&
                    primaryButton != null && primaryButton.Enabled && primaryAction != null) Later(primaryAction);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsBuilds] Dialog failed, closing it: {e}");
                Close();
                return;
            }
            SetLocks(true);
        }

        void SetLocks(bool open)
        {
            LibraryWindow.SetLock(ref locked, LockId, ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_PAD_PICK_COPY |
                ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.EDITOR_ROOT_REFLOW | ControlTypes.CAMERACONTROLS | ControlTypes.EDITOR_UI, open);
            LibraryWindow.SetLock(ref typing, TypingLockId, ControlTypes.KEYBOARDINPUT, open && fields.Any(f => f.Focused));
        }

        // ---- Shared pieces -----------------------------------------------------------------

        protected float Label(float y, string s, string note = null)
        {
            Kit.Line(body, Pad, y, W - 2 * Pad, 16,
                s.ToUpperInvariant() + (note != null ? $" <color={Look.Html(Look.Ink3)}>{note}</color>" : ""), 11, Look.Ink2, style: FontStyle.Bold);
            return y + 22;
        }

        protected Text Hint(float y, string s) => Kit.Text(body, Pad, y, W - 2 * Pad, 34, s, 12, Look.Ink2, align: TextAnchor.UpperLeft);

        // A row of equal segments; returns the y below it.
        protected float Segments(float y, string[] labels, int selected, Action<int> pick)
        {
            RectTransform seg = Kit.Panel(body, "Seg", Pad, y, W - 2 * Pad, 34, Look.Void, Look.Line, 8);
            float bw = (W - 2 * Pad - 6 - 2 * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                Btn b = Track(Kit.Segment(seg, labels[i], 3 + i * (bw + 2), 3, bw, 28, () => Later(() => pick(k))));
                b.On = i == selected;
                b.Label.fontStyle = b.On ? FontStyle.Bold : FontStyle.Normal;
                b.Refresh();
            }
            return y + 42;
        }

        // Chips that wrap onto more lines; returns the y below them.
        protected float Chips(float y, string[] labels, int selected, Action<int> pick)
        {
            float x = Pad;
            for (int i = 0; i < labels.Length; i++)
            {
                float w = labels[i].Length * 7.5f + 24;
                if (x + w > W - Pad) { x = Pad; y += 34; }
                int k = i;
                Btn b = Track(Kit.Chip(body, labels[i], x, y, w, 28, () => Later(() => pick(k))));
                b.On = i == selected;
                b.Refresh();
                x += w + 6;
            }
            return y + 36;
        }

        // A button that looks like a dropdown; opens a list to choose from.
        protected float Select(float y, string text, string sub, IList<string> options, Action<int> pick, float x = -1, float w = -1)
        {
            if (x < 0) x = Pad;
            if (w < 0) w = W - 2 * Pad;
            Btn b = null;
            b = Track(Btn.Make(body, "Select", x, y, w, 36, () => Later(() => OpenPopup(b.Rt, options, pick)))
                .Skin(Look.Round(Look.Void, Look.Line2, 8), Look.Round(Look.Void, Look.AmberLine, 8))
                .Colors(Look.Ink, Look.Ink, Look.Ink)
                .WithLabel(sub == null ? text : $"{text}  <size=12><color={Look.Html(Look.Ink2)}>{sub}</color></size>", Look.Body, 14, align: TextAnchor.MiddleLeft));
            b.Label.rectTransform.Stretch(12, 0, 30, 0);
            Kit.Glyph(b.Rt, w - 26, 11, 14, Look.ChevronDown, Look.Ink2);
            return y + 44;
        }

        // One of two big choices side by side (save over, or as the next mark).
        protected void Option(float x, float y, float w, string title, string text, bool on, Action pick)
        {
            Btn b = Track(Btn.Make(body, title, x, y, w, 100, () => Later(pick))
                .Skin(Look.Round(Look.Rail, Look.Line, 9), Look.Round(Look.Rail, Look.AmberLine, 9), Look.Round(Look.Wash(Look.Rail, 0.08f), Look.Amber, 9)));
            b.On = on;
            b.Refresh();
            Kit.Line(b.Rt, 14, 12, w - 28, 26, title, 19, on ? Look.Amber : Look.Ink, Look.Display, style: FontStyle.Bold);
            Kit.Text(b.Rt, 14, 40, w - 28, 52, text, 12, Look.Ink2, align: TextAnchor.UpperLeft);
        }

        protected void OpenPopup(RectTransform anchor, IList<string> options, Action<int> pick)
        {
            ClosePopup();
            // A click anywhere outside the list closes it.
            popup = Ui.Node(root, "PopupShade").Stretch();
            Ui.Solid(popup, Color.clear, raycast: true);
            Kit.Clicks(popup, n => Later(ClosePopup));

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 local = root.InverseTransformPoint(corners[0]);     // bottom-left of the anchor
            float x = local.x + W / 2, y = H / 2 - local.y + 4;
            float w = anchor.sizeDelta.x;
            int cols = options.Count > 12 ? 2 : 1;
            int rows = (options.Count + cols - 1) / cols;
            float rowH = 28, h = Mathf.Min(rows * rowH + 10, H - y - 10);
            if (h < 120) { h = Mathf.Min(rows * rowH + 10, H - 20); y = Mathf.Max(10, H - 10 - h); }
            RectTransform list = Kit.Panel(popup, "List", x, y, w, h, Look.Panel3, Look.Line2, 8);
            Kit.Scroller sc = Kit.Scroll(list, 0, 5, w, h - 10);
            float cw = (w - 10 - 12) / cols;
            for (int i = 0; i < options.Count; i++)
            {
                int k = i;
                Btn b = Track(Btn.Make(sc.Content, options[i], 6 + (i / rows) * cw, (i % rows) * rowH, cw - 2, rowH - 2, () => Later(() => { ClosePopup(); pick(k); }))
                    .Skin(null, Look.Round(Look.Panel2, Look.AmberLine, 6)).Colors(Look.Ink, Look.Ink, Look.Ink)
                    .WithLabel(options[i], Look.Body, 13, align: TextAnchor.MiddleLeft));
                b.Label.rectTransform.Stretch(10, 0, 6, 0);
            }
            sc.SetHeight(rows * rowH);
        }

        protected void ClosePopup()
        {
            if (popup != null) UnityEngine.Object.Destroy(popup.gameObject);
            popup = null;
        }

        Btn primaryButton;
        Action primaryAction;

        protected void FootButtons(string primary, Action save, bool enabled, string linkText = null, Action link = null)
        {
            if (linkText != null) Track(Kit.Link(foot, linkText, Pad, 17, 190, 30, () => Later(link), align: TextAnchor.MiddleLeft, size: 12));
            float pw = Mathf.Max(96, primary.Length * 9 + 36);
            Btn p = Track(Kit.Primary(foot, primary, W - Pad - pw, 12, pw, 40, () => Later(save), 16));
            p.InkOff = Look.Fade(Look.AmberInk, 0.6f);
            SetEnabled(p, enabled);
            primaryButton = p;
            primaryAction = save;
            Track(Kit.Ghost(foot, "Cancel", W - Pad - pw - 10 - 92, 12, 92, 40, () => Later(Close), 14));
        }

        // An orange button that can't be pressed yet shows dimmed.
        protected static void SetEnabled(Btn b, bool on)
        {
            if (b == null) return;
            Color dim = Color.Lerp(Look.Panel2, Look.Amber, 0.4f);
            b.Enabled = on;
            if (on) b.Skin(Look.Round(Look.Amber, Look.Amber, 8), Look.Round(Look.AmberHover, Look.AmberHover, 8));
            else b.Skin(Look.Round(dim, dim, 8), Look.Round(dim, dim, 8));
        }

        protected static float Parse(string s) =>
            float.TryParse((s ?? "").Replace(',', '.').Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) && f > 0 ? f : 0;

        protected static string Num(float f) => f > 0 ? f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "";
    }

    // ---- Save a new design / file an old craft -------------------------------------------------

    public class SavePanel : Dialog
    {
        static string lastCategory;
        readonly CraftFile filing;          // set when filing a craft from the library (not the one in the editor)
        Category cat;
        bool newFamily = true;
        Family existing;
        string name = "";
        bool nameTyped;
        int cursor;
        MarkStyle style = MarkStyle.Roman;
        Text previewName, previewWhere, previewPath, problem, deckHint;
        Kit.Field nameField;
        Btn saveButton;

        public SavePanel(BuildsEditor host, CraftFile filing, string reason = null) : base(host)
        {
            this.filing = filing;
            cat = Category.ById(lastCategory) ?? Guess();
            if (filing != null && Marks.TryParse(filing.Name, out _, out MarkStyle s, out _)) style = s;
            // A name already typed in KSP's name box (or the old craft's own name) is
            // used, mark and all, and if a family of that name exists this adds to it.
            // Only a craft still called "Untitled Space Craft" gets a dealt name, or (in the
            // editor) one named after a launcher it was opened as: what's built on it is a
            // new design, so it gets a name of its own.
            string typed = filing != null ? filing.Base : Stock.ShipName;
            if (!(filing == null ? Stock.IsPlaceholderName(typed) : Stock.IsDefaultName(typed)))
            {
                string fam = Naming.FamilyPart(typed);
                Family match = Library.Families.FirstOrDefault(f => f.Facility == Facility && Naming.SameName(f.Name, fam));
                if (match != null)
                {
                    cat = match.Category;
                    existing = match;
                    newFamily = false;
                }
                name = fam;
                nameTyped = true;
            }
            else Deal(true);
            Open(filing == null ? "Save a new design" : $"File {filing.Name}",
                filing == null ? reason ?? "This craft isn't in a family yet" : "Moves it into a family folder and names it to match", 640, 720);
        }

        string Facility => filing?.Facility ?? Stock.Facility;

        static Category Guess() => Category.ById(Stock.Facility == "SPH" ? "Spaceplanes" : "Probes");

        public static string StageLabel(SubKind kind) => (kind == SubKind.Launcher ? "Launcher" : "Upper stage") + " · to the Launchers tab";

        List<Family> FamiliesHere => Library.Families.Where(f => f.Category == cat && f.Facility == Facility).OrderBy(f => f.Name).ToList();

        string FamilyName
        {
            get
            {
                if (!newFamily) return existing?.Name ?? "";
                string n = Naming.FamilyPart(name);
                if (cat.Id == "Hoppers" && n.Length > 0 && !n.StartsWith("X-"))
                {
                    int next = Library.Families.Count(f => f.Category.Id == "Hoppers") + 1;
                    n = $"X-{next} {n}";
                }
                return n;
            }
        }

        MarkStyle Style => newFamily ? style : Library.StyleOf(existing?.Name, existing?.Marks.FirstOrDefault(c => c.Mark > 0)?.Style ?? style);

        int NextMark => newFamily || existing == null || existing.Marks.Count == 0 ? 1 : existing.Marks.Max(c => c.Mark) + 1;

        string FinalName => FamilyName.Length == 0 ? "" : Naming.Craft(FamilyName, "", Style, NextMark);

        string Folder => FamilyName.Length == 0 ? "" : Library.FolderFor(Facility, cat, FamilyName);

        string Problem()
        {
            if (FamilyName.Length == 0) return newFamily ? "Give the family a name." : "Pick a family.";
            if (newFamily && Library.Families.Any(f => Naming.SameName(f.Name, FamilyName) && f.Category == cat && f.Facility == Facility))
                return $"There's already a {FamilyName} family here. Add to it instead?";
            if (File.Exists(Path.Combine(Folder, FinalName + ".craft"))) return $"{FinalName} already exists.";
            return null;
        }

        void Deal(bool first = false)
        {
            HashSet<string> used = Library.UsedNames();
            string n = NameDecks.Deal(NameDecks.Key(cat.Id), used, ref cursor);
            if (n != null) { name = n; nameTyped = false; }
            else if (first) name = "";
        }

        protected override void BuildBody()
        {
            float y = 16;
            y = Label(y, "What is it?");
            List<Category> cats = Category.Craft.ToList();
            List<string> labels = cats.Select(c => c.Label).ToList();
            // The craft in the editor can also be saved as a stage, to the Launchers tab, with
            // its rating (Phil, 2026-11-08: the drop box shouldn't be the only way).
            bool stages = filing == null && EditorLogic.RootPart != null;
            if (stages) { labels.Add(StageLabel(SubKind.Launcher)); labels.Add(StageLabel(SubKind.UpperStage)); }
            y = Select(y, cat.Label, Category.GroupLabel(cat.Group), labels, i =>
            {
                if (i >= cats.Count)
                {
                    SubKind kind = i == cats.Count ? SubKind.Launcher : SubKind.UpperStage;
                    Close();
                    host.OpenSubDialog(null, EditorLogic.RootPart, kind, wholeCraft: true);
                    return;
                }
                cat = cats[i];
                lastCategory = cat.Id;
                existing = null;
                newFamily = true;
                if (!nameTyped) { cursor = 0; Deal(); }
                Rebuild();
            });
            Hint(y - 6, "Picks its group in the library, its name list and its map icon.");
            y += 22;

            y = Label(y, "Family");
            List<Family> here = FamiliesHere;
            float half = (W - 2 * Pad - 8) / 2;
            Btn a = Track(Btn.Make(body, "New", Pad, y, half, 40, () => Later(() => { newFamily = true; Rebuild(); }))
                .Skin(Look.Round(Look.Rail, Look.Line, 8), Look.Round(Look.Rail, Look.AmberLine, 8), Look.Round(Look.Wash(Look.Rail, 0.08f), Look.Amber, 8))
                .Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel("Start a new family", Look.Body, 14, align: TextAnchor.MiddleLeft));
            a.Label.rectTransform.Stretch(36, 0, 8, 0);
            Radio(a.Rt, newFamily);
            a.On = newFamily;
            a.Refresh();
            if (here.Count == 0)
            {
                Kit.Panel(body, "NoFamilies", Pad + half + 8, y, half, 40, Look.Rail, Look.Line, 8);
                Kit.Line(body, Pad + half + 8 + 14, y, half - 20, 40, $"No {cat.Label.ToLowerInvariant()} families yet", 13, Look.Ink3);
            }
            else
            {
                string label = !newFamily && existing != null ? $"Add to {existing.Name}" : "Add to a family…";
                Btn b = null;
                b = Track(Btn.Make(body, "Existing", Pad + half + 8, y, half, 40, () => Later(() =>
                    {
                        if (here.Count == 1) { existing = here[0]; newFamily = false; Rebuild(); }
                        else OpenFamilies(b.Rt, here);
                    }))
                    .Skin(Look.Round(Look.Rail, Look.Line, 8), Look.Round(Look.Rail, Look.AmberLine, 8), Look.Round(Look.Wash(Look.Rail, 0.08f), Look.Amber, 8))
                    .Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel(here.Count == 1 ? $"Add to {here[0].Name}" : label, Look.Body, 14, align: TextAnchor.MiddleLeft));
                b.Label.rectTransform.Stretch(36, 0, 8, 0);
                Radio(b.Rt, !newFamily);
                b.On = !newFamily;
                b.Refresh();
            }
            y += 50;

            y = Label(y, newFamily ? "Name" : "Family name");
            if (newFamily)
            {
                nameField = Kit.Input(body, Pad, y, W - 2 * Pad - 150, 42, name, "Type a name", v => { name = v; nameTyped = true; RefreshPreview(); },
                    size: 19, font: Look.Display, padLeft: 12, limit: 40);
                fields.Add(nameField);
                Btn deal = Track(Btn.Make(body, "Deal", W - Pad - 142, y, 142, 42, () => Later(() => { Deal(); nameField.Value = name; nameTyped = false; RefreshPreview(); }))
                    .Skin(Look.Round(Look.Panel3, Look.Line2, 8), Look.Round(Look.Panel3, Look.AmberLine, 8)).Colors(Look.Ink, Look.Ink, Look.Ink)
                    .WithLabel("Deal another", Look.Body, 13));
                deal.Label.rectTransform.Stretch(26, 0, 4, 0);
                Kit.Glyph(deal.Rt, 14, 12, 18, Look.Die, Look.Ink);
                y += 48;
                string theme = NameDecks.Theme(cat.Id);
                int left = NameDecks.Unused(cat.Id, Library.UsedNames());
                deckHint = Hint(y, $"From the {cat.Label} list{(theme.Length > 0 ? ", " + theme.ToLowerInvariant() : "")}. {left} name{(left == 1 ? "" : "s")} you haven't used yet. Or type your own; the mark is added for you." +
                                   (cat.Id == "Hoppers" ? " Test vehicles get an X-number." : ""));
                y += 38;
            }
            else
            {
                Kit.Panel(body, "Fixed", Pad, y, W - 2 * Pad, 42, Look.Rail, Look.Line, 8);
                Kit.Line(body, Pad + 12, y, W - 2 * Pad - 24, 42, existing?.Name ?? "", 19, Look.Ink, Look.Display, style: FontStyle.Bold);
                y += 50;
            }

            if (newFamily)
            {
                y = Label(y + 2, "Mark numbering");
                y = Segments(y, Marks.Styles.Select(Marks.StyleLabel).ToArray(), Array.IndexOf(Marks.Styles, style), i => { style = Marks.Styles[i]; Rebuild(); });
            }
            else y += 4;

            RectTransform box = Kit.Panel(body, "Preview", Pad, y, W - 2 * Pad, 106, Look.Rail, Look.Line, 9);
            Kit.Line(box, 14, 10, W - 2 * Pad - 28, 14, "SAVES AS", 11, Look.Ink3);
            previewName = Kit.Line(box, 14, 24, W - 2 * Pad - 28, 36, "", 28, Look.Amber, Look.Display, style: FontStyle.Bold);
            previewWhere = Kit.Line(box, 14, 60, W - 2 * Pad - 28, 18, "", 12, Look.Ink2);
            previewPath = Kit.Line(box, 14, 80, W - 2 * Pad - 28, 18, "", 12, Look.Ink3);
            y += 114;
            problem = Kit.Line(body, Pad, y, W - 2 * Pad, 18, "", 12, Look.Warn);
            RefreshPreview();
        }

        static void Radio(RectTransform rt, bool on)
        {
            RectTransform dot = Kit.Panel(rt, "Radio", 14, 13, 14, 14, Look.Rail, on ? Look.Amber : Look.Line2, 7);
            if (on) Kit.Panel(dot, "Dot", 4, 4, 6, 6, Look.Amber, Look.Amber, 3);
        }

        void OpenFamilies(RectTransform anchor, List<Family> here)
        {
            var names = here.Select(f => f.Name).ToList();
            OpenPopup(anchor, names, i => { existing = here[i]; newFamily = false; Rebuild(); });
        }

        void RefreshPreview()
        {
            if (previewName == null) return;
            string final = FinalName;
            previewName.text = final.Length > 0 ? final : "—";
            string launcher = filing == null ? LauncherTags.LauncherIn(Stock.Ship) : Library.NoteFor(filing)?.Launcher;
            previewWhere.text = $"{cat.Label}, {(FamilyName.Length > 0 ? FamilyName : "…")} family" +
                                (string.IsNullOrEmpty(launcher) ? "" : $" · built on {launcher} " + (Library.HasLauncher(launcher) ? "(found by itself)" : "(deleted)"));
            string rel = FamilyName.Length == 0 ? "" : $"Ships/{Facility}/{cat.Folder}/{Naming.Clean(FamilyName)}/{final}.craft";
            previewPath.text = rel;
            string p = Problem();
            problem.text = p ?? (filing == null && IsUnfiledCurrent() ? "The unfiled copy moves to the save's Deleted craft folder." : "");
            problem.color = p != null ? Look.Warn : Look.Ink3;
            SetEnabled(saveButton, p == null);

        }

        bool IsUnfiledCurrent()
        {
            string cur = Stock.CurrentFile;
            if (cur == null || !File.Exists(cur)) return false;
            CraftFile c = Library.CraftAt(cur);
            return c != null && !c.Filed;
        }

        protected override void BuildFoot()
        {
            FootButtons("Save", () => Later(DoSave), Problem() == null,
                filing == null ? "Save without filing" : null, filing == null ? (Action)(() => { Close(); host.QuickSave(); }) : null);
            saveButton = buttons.LastOrDefault(b => b.Label != null && b.Label.text == "Save");
            RefreshPreview();
        }

        void DoSave()
        {
            if (Problem() != null) { RefreshPreview(); return; }
            string family = FamilyName, final = FinalName, folder = Folder;
            lastCategory = cat.Id;
            if (newFamily) Library.SetStyle(family, style);
            try
            {
                if (filing != null) host.FileCraft(filing, folder, final, cat);
                else host.SaveInto(folder, final, cat);
            }
            catch (Exception e)
            {
                problem.text = e.Message;
                problem.color = Look.Warn;
                return;
            }
            Close();
        }
    }

    // ---- Save the next mark ---------------------------------------------------------------------

    // The one Save dialog for a design already in the library (a craft's marks, or a launcher
    // or upper stage): save over the open one, save it as the next mark, or save it as a new
    // design (Phil, play-test 2026-11-08: "Save as new" as a third choice). Save over comes
    // first, so Save then Enter keeps working on the open one.
    public abstract class MarkDialog : Dialog
    {
        protected enum Choice { Over, Next, New }
        Choice choice = Choice.Over;
        protected string note;      // over the choices: what saving a loose copy does

        protected MarkDialog(BuildsEditor host) : base(host) { }

        protected void OpenMarks(string title, string subtitle) => Open(title, subtitle, 720, string.IsNullOrEmpty(note) ? 330 : 372);

        // `keeps` is what stays as it is when this is saved as something new.
        protected void Choices(string overTitle, string overText, string nextTitle, string nextText, string keeps)
        {
            float y = 18;
            if (!string.IsNullOrEmpty(note))
            {
                Kit.Text(body, Pad, 14, W - 2 * Pad, 36, note, 13, Look.Warn, align: TextAnchor.UpperLeft);
                y = 56;
            }
            float w = (W - 2 * Pad - 20) / 3;
            Option(Pad, y, w, overTitle, overText, choice == Choice.Over, () => { choice = Choice.Over; Rebuild(); });
            Option(Pad + w + 10, y, w, nextTitle, nextText, choice == Choice.Next, () => { choice = Choice.Next; Rebuild(); });
            Option(Pad + 2 * (w + 10), y, w, "Save as new design", $"Something else now. Pick what it is and name it. {keeps} stays as it is.",
                choice == Choice.New, () => { choice = Choice.New; Rebuild(); });
            Kit.Text(body, Pad, y + 112, W - 2 * Pad, 20, "Pick one and press the orange button (or Enter). Cancel saves nothing.", 12, Look.Ink2,
                align: TextAnchor.UpperLeft);
        }

        // The orange button follows the choice; a new design goes on to the Save panel.
        protected void Foot(string nextName, Action over, Action next) =>
            FootButtons(choice == Choice.New ? "Next…" : choice == Choice.Next ? $"Save {nextName}" : "Save", () =>
            {
                Close();
                if (choice == Choice.New) host.OpenNewDesign();
                else if (choice == Choice.Next) next();
                else over();
            }, true);

        protected static string Tail(string name) =>
            Marks.TryParse(name, out _, out MarkStyle s, out int m) ? "Mark " + Marks.Short(s, m) : name;
    }

    // ---- Save a craft from a family ------------------------------------------------------------

    public class NextMarkDialog : MarkDialog
    {
        readonly string current, next, newest, folder, line;
        readonly CraftFile loose;

        // `folder` is the family's; `loose` is set when the craft open is a loose copy of one of
        // its marks (a file outside the family folder): saving puts it back, and the copy goes
        // to Deleted craft.
        public NextMarkDialog(BuildsEditor host, string family, string folder, CraftFile loose = null) : base(host)
        {
            this.folder = folder;
            this.loose = loose;
            current = Stock.ShipName;
            bool marked = Marks.TryParse(current, out string rest, out MarkStyle s, out int mark);
            if (!marked) { rest = current; mark = 1; }
            line = rest;
            MarkStyle style = Library.StyleOf(family, marked ? s : MarkStyle.Roman);
            // The next mark comes after the newest one saved, not just after the open one:
            // with Mark III open and a Mark IV on file, the next is Mark V.
            int top = mark;
            foreach (CraftFile c in Library.Craft)
                if (c.Mark > top && Naming.SameName(c.Base, rest) && (Paths.Same(System.IO.Path.GetDirectoryName(c.Path), folder) || c.Filed && c.Facility == Stock.Facility))
                {
                    top = c.Mark;
                    newest = c.Name;
                }
            next = rest + " " + Marks.Format(style, top + 1);
            if (loose != null)
            {
                string cat = Category.ByFolder((Library.InShips(folder) ?? "").Split('/')[0])?.Label ?? "family";
                note = $"This is a loose copy of {current}, outside its {cat} folder. However you save it, the loose copy then goes to Deleted craft.";
            }
            OpenMarks($"Save {rest}", loose != null ? $"A loose copy of {current}. What did you make?"
                : marked ? $"You have Mark {Marks.Short(s, mark)} open. What did you make?" : "What did you make?");
        }

        protected override void BuildBody() =>
            Choices($"Save over {Tail(current)}", $"Small fixes. It stays {current}.",
                $"Save as {Tail(next)}",
                newest != null ? $"A new version, {next}, after {newest}. Older marks stay and still load." : $"A new version, {next}. The older mark stays and still loads.",
                line);

        protected override void BuildFoot() =>
            Foot(Tail(next), () => host.SaveMark(null, folder, loose), () => host.SaveMark(next, folder, loose));
    }

    // ---- Save a launcher or upper stage that's open on its own -----------------------------------

    // The craft in the editor is a launcher or upper stage from the library with nothing
    // built on it (loaded, or saved from here). Save asks what it asks for a craft's marks:
    // over it, or as its next mark (Phil, 2026-11-08: "Save over Haytor 002" or "next mark").
    public class StageMarkDialog : MarkDialog
    {
        readonly SubFile current;
        readonly string next;
        readonly int nextMark;

        public StageMarkDialog(BuildsEditor host, SubFile current) : base(host)
        {
            this.current = current;
            // The next mark comes after the newest of its family (and, for a launcher, of
            // its rating: "Haytor 003" is another line), and never takes a name in use.
            string family = current.Family.Length > 0 ? current.Family : current.Name;
            int top = Math.Max(current.Mark, 1);
            foreach (SubFile s in Library.Subs)
                if (s.Kind == current.Kind && current.Family.Length > 0 && Naming.SameName(s.Family, current.Family) &&
                    (current.Kind != SubKind.Launcher || Naming.Code(s.Rating) == Naming.Code(current.Rating)))
                    top = Math.Max(top, s.Mark);
            nextMark = top + 1;
            next = NameFor(family, nextMark);
            for (int i = 0; i < 50 && Library.SubNamed(next) != null; i++) next = NameFor(family, ++nextMark);
            string what = current.Kind == SubKind.Launcher ? "launcher" : "upper stage";
            OpenMarks($"Save {current.Name}", $"You have the {what} {current.Name} open. What did you make?");
        }

        string NameFor(string family, int mark) =>
            current.Kind == SubKind.Launcher && current.Rating > 0 ? Naming.Launcher(family, current.Rating, current.Style, mark) : Naming.Stage(family, current.Style, mark);

        // Built something on it? That's the third choice: a new design.
        protected override void BuildBody()
        {
            string lifts = current.Kind == SubKind.Launcher && current.Rated ? $", lifts {Naming.Tons(current.Rating)}" : "";
            Choices($"Save over {current.Name}", $"Small fixes. It stays {current.Name}{lifts}.",
                $"Save as {next}", $"A new version. {current.Name} stays in the library and still loads.", current.Name);
        }

        protected override void BuildFoot() =>
            Foot(next, () => host.SaveStage(current, current.Name, current.Mark), () => host.SaveStage(current, next, nextMark));
    }

    // ---- Save or file a subassembly --------------------------------------------------------------

    public class SubDialog : Dialog
    {
        readonly SubFile editing;           // an existing subassembly being filed or changed
        readonly Part held;                 // or the part to save, held on the cursor
        SubKind kind;
        float tons, width;
        string notes = "", family = "", group = "", blockName = "";
        bool nameTyped;
        int cursor;
        MarkStyle style = MarkStyle.Roman;
        Text previewName, problem;
        Kit.Field familyField;
        Btn saveButton;

        static readonly SubKind[] Kinds = { SubKind.Launcher, SubKind.UpperStage, SubKind.Block };
        static readonly string[] KindLabels = { "Launcher", "Upper stage", "Module or block" };

        public SubDialog(BuildsEditor host, SubFile editing, Part held, SubKind kind, bool wholeCraft = false) : base(host)
        {
            this.editing = editing;
            this.held = held;
            this.kind = kind == SubKind.Unfiled ? SubKind.Launcher : kind;
            if (editing != null)
            {
                tons = editing.Rating;
                width = editing.Width;
                notes = editing.Notes;
                family = editing.Family;
                group = editing.Group;
                style = editing.Style;
                blockName = editing.Name;
                nameTyped = family.Length > 0;
                if (editing.Kind == SubKind.Unfiled) GuessFromName(editing.Name);
            }
            else blockName = wholeCraft ? Stock.ShipName : held?.partInfo?.title ?? "";
            if (family.Length == 0 && SubKinds.IsStage(this.kind)) Deal();
            string title = editing == null ? "Save a subassembly" : editing.Kind == SubKind.Unfiled ? $"File {editing.Name}" : $"Change {editing.Name}";
            Open(title, editing != null ? "Sets what it is and names it to match" : wholeCraft ? "The whole craft in the editor" : $"{held?.partInfo?.title ?? "The held part"} and everything under it", 600, 660);
        }

        // Phil's old style: "Launch - Oak 030" means the Oak launcher, rated 30 t.
        void GuessFromName(string n)
        {
            string s = n;
            int dash = s.LastIndexOf(" - ", StringComparison.Ordinal);
            string prefix = dash > 0 ? s.Substring(0, dash).Trim() : "";
            if (dash > 0) s = s.Substring(dash + 3).Trim();
            var m = System.Text.RegularExpressions.Regex.Match(s, @"^(.*\S)\s+(\d{3})$");
            if (m.Success)
            {
                kind = SubKind.Launcher;
                family = m.Groups[1].Value;
                tons = int.Parse(m.Groups[2].Value);
                nameTyped = true;
            }
            else if (prefix.Length > 0)
            {
                group = prefix;
                blockName = n.Replace(" - ", " ");
                if (!prefix.StartsWith("Launch", StringComparison.OrdinalIgnoreCase)) kind = SubKind.Block;
            }
        }

        string DeckKey => kind == SubKind.Launcher ? NameDecks.Key("Launchers", Naming.ClassOf(tons > 0 ? tons : 10)) : NameDecks.Key("UpperStages");

        void Deal()
        {
            HashSet<string> used = Library.UsedNames();
            string n = NameDecks.Deal(DeckKey, used, ref cursor);
            if (n != null) { family = n; nameTyped = false; }
        }

        int Mark
        {
            get
            {
                if (!SubKinds.IsStage(kind)) return 1;
                IEnumerable<SubFile> same = Library.Subs.Where(s => s != editing && s.Kind == kind && Naming.SameName(s.Family, family) &&
                                                                    (kind != SubKind.Launcher || Naming.Code(s.Rating) == Naming.Code(tons)));
                if (editing != null && editing.Kind == kind && Naming.SameName(editing.Family, family) &&
                    (kind != SubKind.Launcher || Naming.Code(editing.Rating) == Naming.Code(tons))) return editing.Mark;
                return same.Any() ? same.Max(s => s.Mark) + 1 : 1;
            }
        }

        string FinalName
        {
            get
            {
                string f = Naming.LauncherFamily(family);
                switch (kind)
                {
                    case SubKind.Launcher: return f.Length == 0 || tons <= 0 ? f : Naming.Launcher(f, tons, style, Mark);
                    case SubKind.UpperStage: return f.Length == 0 ? "" : Naming.Stage(f, style, Mark);
                    default: return Naming.Clean(blockName);
                }
            }
        }

        string Problem()
        {
            string n = FinalName;
            if (kind == SubKind.Launcher && tons <= 0) return "Type how many tons it lifts to orbit.";
            if (n.Length == 0) return kind == SubKind.Block ? "Give it a name." : "Give the family a name.";
            SubFile clash = Library.SubNamed(n);
            if (clash != null && clash != editing) return $"{n} already exists.";
            return null;
        }

        protected override void BuildBody()
        {
            float y = 16;
            y = Label(y, "What is it?");
            y = Segments(y, KindLabels, Array.IndexOf(Kinds, kind), i =>
            {
                kind = Kinds[i];
                if (!nameTyped && SubKinds.IsStage(kind)) { cursor = 0; Deal(); }
                Rebuild();
            });

            if (kind == SubKind.Launcher)
            {
                float half = (W - 2 * Pad - 10) / 2;
                Label(y, "Lifts to orbit");
                Kit.Line(body, Pad + half + 10, y, half, 16, "PAYLOAD UP TO", 11, Look.Ink2, style: FontStyle.Bold);
                y += 22;
                fields.Add(Kit.Input(body, Pad, y, half - 26, 38, Num(tons), "tons", v =>
                {
                    LauncherClass before = Naming.ClassOf(tons > 0 ? tons : 10);
                    tons = Parse(v);
                    if (!nameTyped && Naming.ClassOf(tons > 0 ? tons : 10) != before) { cursor = 0; Deal(); if (familyField != null) familyField.Value = family; nameTyped = false; }
                    RefreshPreview();
                }, size: 17, font: Look.Display, limit: 8));
                Kit.Line(body, Pad + half - 20, y, 20, 38, "t", 14, Look.Ink2);
                fields.Add(Kit.Input(body, Pad + half + 10, y, half - 26, 38, Num(width), "meters", v => { width = Parse(v); RefreshPreview(); },
                    size: 17, font: Look.Display, limit: 8));
                Kit.Line(body, W - Pad - 20, y, 20, 38, "m", 14, Look.Ink2);
                y += 44;
                Hint(y, "Your own test numbers: the heaviest payload it gets to orbit, and the widest it fits.");
                y += 30;
            }

            if (SubKinds.IsStage(kind))
            {
                y = Label(y, "Family name");
                familyField = Kit.Input(body, Pad, y, W - 2 * Pad - 150, 42, family, "Type a name", v => { family = v; nameTyped = true; RefreshPreview(); },
                    size: 19, font: Look.Display, padLeft: 12, limit: 40);
                fields.Add(familyField);
                Btn deal = Track(Btn.Make(body, "Deal", W - Pad - 142, y, 142, 42, () => Later(() => { Deal(); familyField.Value = family; nameTyped = false; RefreshPreview(); }))
                    .Skin(Look.Round(Look.Panel3, Look.Line2, 8), Look.Round(Look.Panel3, Look.AmberLine, 8)).Colors(Look.Ink, Look.Ink, Look.Ink)
                    .WithLabel("Deal another", Look.Body, 13));
                deal.Label.rectTransform.Stretch(26, 0, 4, 0);
                Kit.Glyph(deal.Rt, 14, 12, 18, Look.Die, Look.Ink);
                y += 48;
                string theme = NameDecks.Theme(DeckKey);
                Hint(y, theme.Length > 0 ? $"Dealt from {theme.ToLowerInvariant()}, sized to the payload. Use an existing family's name to add a mark to it." : "Use an existing family's name to add a mark to it.");
                y += 34;
                y = Label(y, "Mark numbering");
                y = Segments(y, Marks.Styles.Select(Marks.StyleLabel).ToArray(), Array.IndexOf(Marks.Styles, style), i => { style = Marks.Styles[i]; Rebuild(); });
            }
            else
            {
                y = Label(y, "Name");
                fields.Add(Kit.Input(body, Pad, y, W - 2 * Pad, 42, blockName, "What it is: DESU Drive, Control 1.25 Top…", v => { blockName = v; RefreshPreview(); },
                    size: 17, font: Look.Display, padLeft: 12, limit: 50));
                y += 50;
                y = Label(y, "Group", "(a program like DESU, or a job like Control or Power)");
                List<string> groups = Library.Subs.Where(s => s.Kind == SubKind.Block && s.Group.Length > 0).Select(s => s.Group)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                fields.Add(Kit.Input(body, Pad, y, W - 2 * Pad - (groups.Count > 0 ? 150 : 0), 38, group, "Group", v => group = v, limit: 30));
                if (groups.Count > 0) Select(y + 1, "Pick…", null, groups, i => { group = groups[i]; Rebuild(); }, W - Pad - 142, 142);
                y += 46;
            }

            if (kind == SubKind.Launcher)
            {
                y = Label(y, "Test notes", "(optional)");
                fields.Add(Kit.Input(body, Pad, y, W - 2 * Pad, 36, notes, "How you tested it", v => notes = v, limit: 120));
                y += 44;
            }

            RectTransform box = Kit.Panel(body, "Preview", Pad, y + 4, W - 2 * Pad, 70, Look.Rail, Look.Line, 9);
            Kit.Line(box, 14, 10, W - 2 * Pad - 28, 14, "SAVES AS", 11, Look.Ink3);
            previewName = Kit.Line(box, 14, 24, W - 2 * Pad - 28, 36, "", 26, Look.Amber, Look.Display, style: FontStyle.Bold);
            problem = Kit.Line(body, Pad, y + 80, W - 2 * Pad, 18, "", 12, Look.Warn);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            if (previewName == null) return;
            string n = FinalName;
            previewName.text = n.Length > 0 ? n : "—";
            string p = Problem();
            problem.text = p ?? "";
            SetEnabled(saveButton, p == null);

        }

        protected override void BuildFoot()
        {
            FootButtons("Save", () => Later(DoSave), Problem() == null);
            saveButton = buttons.LastOrDefault(b => b.Label != null && b.Label.text == "Save");
            RefreshPreview();
        }

        void DoSave()
        {
            if (Problem() != null) { RefreshPreview(); return; }
            var info = new SubFile
            {
                Kind = kind,
                Family = kind == SubKind.Block ? "" : Naming.LauncherFamily(family),
                Group = kind == SubKind.Block ? Naming.Clean(group) : "",
                Rating = kind == SubKind.Launcher ? tons : 0,
                Width = kind == SubKind.Launcher ? width : 0,
                Notes = kind == SubKind.Launcher ? notes ?? "" : "",
                Style = style,
                Mark = Mark,
            };
            try { host.SaveSub(editing, held, FinalName, info); }
            catch (Exception e) { problem.text = e.Message; return; }
            Close();
        }
    }

    // ---- Rename a family ----------------------------------------------------------------------------

    // Renames a family, moves it to another category, or both (Phil, play-test 2026-11-08:
    // Magellan was filed under Probes by mistake, and nothing could move it).
    public class RenameDialog : Dialog
    {
        readonly Family family;
        string name;
        Category cat;
        Text problem, preview;

        public RenameDialog(BuildsEditor host, Family family) : base(host)
        {
            this.family = family;
            name = family.Name;
            cat = family.Category;
            Open($"Rename or move {family.Name}", "Changes the family's folder and every craft in it", 520, 400);
        }

        bool Renamed => Naming.Clean(name) != family.Name;
        bool Moved => cat != family.Category;

        string Problem()
        {
            string n = Naming.Clean(name);
            if (n.Length == 0) return "Give it a name.";
            if (!Renamed && !Moved) return "";
            if (Library.Families.Any(f => f != family && f.Category == cat && f.Facility == family.Facility && Naming.SameName(f.Name, n)))
                return $"There's already a {n} family in {cat.Label}.";
            return null;
        }

        protected override void BuildBody()
        {
            float y = Label(16, "Name");
            fields.Add(Kit.Input(body, Pad, y, W - 2 * Pad, 42, name, "Family name", v => { name = v; Refresh(); RebuildFoot(); }, size: 19, font: Look.Display, padLeft: 12, limit: 40));
            y += 56;
            y = Label(y, "Category");
            List<Category> cats = Category.Craft.ToList();
            y = Select(y, cat.Label, Category.GroupLabel(cat.Group), cats.Select(c => c.Label).ToList(), i => { cat = cats[i]; Rebuild(); });
            y += 4;
            preview = Kit.Text(body, Pad, y, W - 2 * Pad, 54, "", 12, Look.Ink2, align: TextAnchor.UpperLeft);
            problem = Kit.Line(body, Pad, y + 56, W - 2 * Pad, 18, "", 12, Look.Warn);
            Refresh();
        }

        void Refresh()
        {
            if (preview == null) return;
            string n = Naming.Clean(name);
            var examples = family.Marks.Take(3).Select(c => Naming.SwapFamily(c.Name, family.Name, n));
            preview.text = $"Becomes: {cat.Label} › {n}, with " + string.Join(", ", examples) + (family.Marks.Count() > 3 ? "…" : "") +
                ". Marks and hangar copies stay.";
            string p = Problem();
            problem.text = p ?? "";
        }

        // The orange button says what it'll do, and waits until there's something to do.
        string Verb => Renamed && Moved ? "Rename and move" : Moved ? "Move" : "Rename";

        void RebuildFoot()
        {
            if (foot == null) return;
            buttons.RemoveAll(b => b.Rt == null || b.Rt.IsChildOf(foot));
            Kit.Clear(foot);
            BuildFoot();
        }

        protected override void BuildFoot() =>
            FootButtons(Verb, () => Later(() =>
            {
                if (Problem() != null) return;
                try { host.RenameFamily(family, Naming.Clean(name), cat); }
                catch (Exception e) { problem.text = e.Message; return; }
                Close();
            }), Problem() == null);
    }
}
