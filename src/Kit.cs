// Building blocks for the windows, on top of Ui and Btn: text, text fields,
// scroll areas and the house buttons (orange, outlined, link, chip).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PhilsVesselNaming
{
    public static class Kit
    {
        public static Text Text(RectTransform parent, float x, float y, float w, float h, string s, int size, Color color,
            Font font = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Text t = Ui.Label(Ui.Node(parent, "Text").At(x, y, w, h), s, font ?? Look.Body, size, color, align, style);
            return t;
        }

        // One line that never wraps; long text is cut at the box edge.
        public static Text Line(RectTransform parent, float x, float y, float w, float h, string s, int size, Color color,
            Font font = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Text t = Text(parent, x, y, w, h, s, size, color, font, align, style);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Text Eyebrow(RectTransform parent, float x, float y, float w, string s) =>
            Line(parent, x, y, w, 15, s.ToUpperInvariant(), 11, Look.Ink3);

        public static RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h, Color fill, Color border, int radius)
        {
            RectTransform rt = Ui.Node(parent, name).At(x, y, w, h);
            Ui.Box(rt, Look.Round(fill, border, radius));
            return rt;
        }

        public static void Rule(RectTransform parent, float x, float y, float w, float h) => Ui.Solid(Ui.Node(parent, "Rule").At(x, y, w, h), Look.Line);

        public static RawImage Glyph(RectTransform parent, float x, float y, float size, Texture tex, Color color) =>
            Ui.Picture(Ui.Node(parent, "Glyph").At(x, y, size, size), tex, color);

        // ---- Buttons ----------------------------------------------------------------

        public static Btn Primary(RectTransform parent, string label, float x, float y, float w, float h, Action onClick, int size = 15) =>
            Btn.Make(parent, label, x, y, w, h, onClick)
                .Skin(Look.Round(Look.Amber, Look.Amber, 8), Look.Round(Look.AmberHover, Look.AmberHover, 8))
                .Colors(Look.AmberInk, Look.AmberInk, Look.AmberInk)
                .WithLabel(label, Look.Display, size, FontStyle.Bold);

        public static Btn Ghost(RectTransform parent, string label, float x, float y, float w, float h, Action onClick, int size = 13) =>
            Btn.Make(parent, label, x, y, w, h, onClick)
                .Skin(Look.Round(Color.clear, Look.Line2, 8), Look.Round(Color.clear, Look.AmberLine, 8))
                .Colors(Look.Ink2, Look.Ink, Look.Ink)
                .WithLabel(label, Look.Body, size);

        public static Btn Link(RectTransform parent, string label, float x, float y, float w, float h, Action onClick, Color? color = null,
            TextAnchor align = TextAnchor.MiddleCenter, int size = 13)
        {
            Color c = color ?? Look.Ink2;
            return Btn.Make(parent, label, x, y, w, h, onClick).Colors(c, c == Look.Ink2 ? Look.Ink : Look.AmberHover, c)
                .WithLabel($"<u>{label}</u>", Look.Body, size, align: align);
        }

        // A pill that can be switched on (orange) or off (outlined).
        public static Btn Chip(RectTransform parent, string label, float x, float y, float w, float h, Action onClick, int size = 13) =>
            Btn.Make(parent, label, x, y, w, h, onClick)
                .Skin(Look.Round(Color.clear, Look.Line2, (int)(h / 2)), Look.Round(Look.Panel2, Look.AmberLine, (int)(h / 2)),
                      Look.Round(Look.Amber, Look.Amber, (int)(h / 2)), Look.Round(Look.AmberHover, Look.AmberHover, (int)(h / 2)))
                .Colors(Look.Ink2, Look.Ink, Look.AmberInk)
                .WithLabel(label, Look.Body, size);

        // A segment of a segmented control.
        public static Btn Segment(RectTransform parent, string label, float x, float y, float w, float h, Action onClick, int size = 13) =>
            Btn.Make(parent, label, x, y, w, h, onClick)
                .Skin(null, Look.Round(Look.Panel2, Look.Panel2, 5), Look.Round(Look.Panel3, Look.Line2, 5))
                .Colors(Look.Ink2, Look.Ink, Look.Ink)
                .WithLabel(label, Look.Body, size);

        // A small pill label (not a button).
        public static float Pill(RectTransform parent, float x, float y, string s, bool amber = false)
        {
            RectTransform rt = Ui.Node(parent, "Pill");
            Text t = Ui.Label(Ui.Node(rt, "Text").Stretch(9, 0, 9, 0), s, Look.Body, 12, amber ? Look.Amber : Look.Ink2, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Ceil(t.preferredWidth) + 18;
            rt.At(x, y, w, 22);
            Ui.Box(rt, amber ? Look.Round(Look.Wash(Look.Rail), Look.AmberLine, 11) : Look.Round(Look.Panel3, Look.Line2, 11));
            rt.SetAsFirstSibling();
            return w;
        }

        // ---- Text fields --------------------------------------------------------------

        public class Field
        {
            public InputField Input;
            public Image Bg;
            public Text Text;
            public bool Focused => Input != null && Input.isFocused;
            public string Value { get => Input.text; set => Input.text = value ?? ""; }
            public void Refresh(bool accent = false) =>
                Ui.SetSprite(Bg, Look.Round(Look.Void, Focused || accent ? Look.AmberLine : Look.Line2, 8));
        }

        public static Field Input(RectTransform parent, float x, float y, float w, float h, string text, string placeholder,
            Action<string> onChange, Action<string> onEnd = null, int size = 14, Font font = null, float padLeft = 10, int limit = 60)
        {
            RectTransform box = Ui.Node(parent, "Field").At(x, y, w, h);
            box.gameObject.SetActive(false);    // a field must be set up before it wakes, or it never makes a caret
            var f = new Field { Bg = Ui.Box(box, Look.Round(Look.Void, Look.Line2, 8), raycast: true) };
            f.Text = Ui.Label(Ui.Node(box, "Text").Stretch(padLeft, 0, 10, 0), "", font ?? Look.Body, size, Look.Ink);
            f.Text.supportRichText = false;
            f.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text ph = Ui.Label(Ui.Node(box, "Placeholder").Stretch(padLeft, 0, 10, 0), placeholder ?? "", Look.Body, Mathf.Min(size, 14), Look.Ink3);
            f.Input = box.gameObject.AddComponent<InputField>();
            f.Input.textComponent = f.Text;
            f.Input.placeholder = ph;
            f.Input.targetGraphic = f.Bg;
            f.Input.transition = Selectable.Transition.None;
            f.Input.lineType = InputField.LineType.SingleLine;
            f.Input.characterLimit = limit;
            f.Input.customCaretColor = true;
            f.Input.caretColor = Look.Amber;
            f.Input.selectionColor = new Color(1f, 0.65f, 0.17f, 0.35f);
            var nav = f.Input.navigation;
            nav.mode = Navigation.Mode.None;
            f.Input.navigation = nav;
            f.Input.text = text ?? "";
            if (onChange != null) f.Input.onValueChanged.AddListener(v => onChange(v));
            if (onEnd != null) f.Input.onEndEdit.AddListener(v => onEnd(v));
            box.gameObject.SetActive(true);
            return f;
        }

        // ---- Scrolling ------------------------------------------------------------------

        // A vertical scroll area. Lay children into `content` from its top-left with
        // Ui.At, then set its height with SetHeight.
        public class Scroller
        {
            public RectTransform Area, Viewport, Content;
            public ScrollRect Scroll;
            public void SetHeight(float h) => Content.sizeDelta = new Vector2(0, Mathf.Max(h, 1));
            public float Width => Area.sizeDelta.x - 10;
            public float Position { get => Scroll.verticalNormalizedPosition; set => Scroll.verticalNormalizedPosition = value; }
            public float Offset { get => Content.anchoredPosition.y; set => Content.anchoredPosition = new Vector2(0, value); }
        }

        public static Scroller Scroll(RectTransform parent, float x, float y, float w, float h)
        {
            var s = new Scroller { Area = Ui.Node(parent, "Scroll").At(x, y, w, h) };
            s.Scroll = s.Area.gameObject.AddComponent<ScrollRect>();
            s.Scroll.horizontal = false;
            s.Scroll.vertical = true;
            s.Scroll.movementType = ScrollRect.MovementType.Clamped;
            s.Scroll.inertia = false;
            s.Scroll.scrollSensitivity = 40;

            s.Viewport = Ui.Node(s.Area, "Viewport").Stretch(0, 0, 10, 0);
            Ui.Solid(s.Viewport, Color.clear, raycast: true);    // so the wheel scrolls over gaps too
            s.Viewport.gameObject.AddComponent<RectMask2D>();

            s.Content = Ui.Node(s.Viewport, "Content");
            s.Content.anchorMin = new Vector2(0, 1);
            s.Content.anchorMax = new Vector2(1, 1);
            s.Content.pivot = new Vector2(0.5f, 1);
            s.Content.anchoredPosition = Vector2.zero;
            s.Content.sizeDelta = new Vector2(0, 1);

            RectTransform bar = Ui.Node(s.Area, "Scrollbar");
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(1, 0.5f);
            bar.offsetMin = new Vector2(-7, 6);
            bar.offsetMax = new Vector2(-2, -6);
            Ui.Box(bar, Look.Round(Look.Panel2, Look.Panel2, 2), raycast: true);
            RectTransform handle = Ui.Node(bar, "Handle");
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImg = Ui.Box(handle, Look.Round(Look.Line2, Look.Line2, 2), raycast: true);
            Scrollbar sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.transition = Selectable.Transition.None;
            sb.targetGraphic = handleImg;
            sb.handleRect = handle;
            sb.direction = Scrollbar.Direction.BottomToTop;
            var nav = sb.navigation;
            nav.mode = Navigation.Mode.None;
            sb.navigation = nav;

            s.Scroll.viewport = s.Viewport;
            s.Scroll.content = s.Content;
            s.Scroll.verticalScrollbar = sb;
            s.Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return s;
        }

        public static void Clear(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(rt.GetChild(i).gameObject);
        }

        // A plain clickable area (rows, tiles): calls back on click with the click count.
        public static Clickable Clicks(RectTransform rt, Action<int> onClick)
        {
            Graphic g = rt.GetComponent<Graphic>();
            if (g == null) Ui.Solid(rt, Color.clear, raycast: true);
            else g.raycastTarget = true;
            var c = rt.gameObject.AddComponent<Clickable>();
            c.Clicked = onClick;
            return c;
        }
    }

    public class Clickable : MonoBehaviour, IPointerClickHandler
    {
        public Action<int> Clicked;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(e.clickCount);
        }
    }
}
