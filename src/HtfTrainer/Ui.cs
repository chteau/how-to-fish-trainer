using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// A small immediate-mode widget set drawn entirely from solid rectangles, so nothing depends on
    /// Unity's default IMGUI skin — that skin is a translucent grey and is effectively invisible over
    /// bright water. Corners are square throughout: it reads as deliberate rather than unfinished.
    /// </summary>
    internal static class Ui
    {
        // ---- palette ----
        internal static readonly Color Backdrop = Hex(0x0E1013, 0.97f);
        internal static readonly Color Surface = Hex(0x16191F);
        internal static readonly Color SurfaceAlt = Hex(0x1C2029);
        internal static readonly Color Line = Hex(0x2A303B);
        internal static readonly Color LineSoft = Hex(0x21262F);
        internal static readonly Color Text = Hex(0xE8ECF2);
        internal static readonly Color Muted = Hex(0x7D8494);
        internal static readonly Color Faint = Hex(0x565D6B);
        internal static readonly Color Accent = Hex(0x4CC2FF);
        internal static readonly Color AccentDim = Hex(0x1E4B63);
        internal static readonly Color Warn = Hex(0xF2B441);
        internal static readonly Color Good = Hex(0x4ADE80);

        internal const int SizeTitle = 15;
        internal const int SizeBody = 13;
        internal const int SizeSmall = 11;

        private static Texture2D _pixel;
        private static GUIStyle _style;
        private static int _widgetId;
        private static int _activeSlider = -1;

        private static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        private static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1, TextureFormat.ARGB32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                }
                return _pixel;
            }
        }

        internal static void BeginFrame()
        {
            _widgetId = 0;
            if (Event.current != null && Event.current.type == EventType.MouseUp) _activeSlider = -1;
        }

        // ---- primitives ----

        internal static void Fill(Rect r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Pixel);
            GUI.color = prev;
        }

        internal static void Stroke(Rect r, Color color, float t = 1f)
        {
            Fill(new Rect(r.xMin, r.yMin, r.width, t), color);
            Fill(new Rect(r.xMin, r.yMax - t, r.width, t), color);
            Fill(new Rect(r.xMin, r.yMin, t, r.height), color);
            Fill(new Rect(r.xMax - t, r.yMin, t, r.height), color);
        }

        internal static void Text2(Rect r, string content, Color color,
                                   int size = SizeBody,
                                   TextAnchor anchor = TextAnchor.MiddleLeft,
                                   FontStyle fontStyle = FontStyle.Normal)
        {
            if (_style == null) _style = new GUIStyle { richText = false, wordWrap = false };
            _style.fontSize = size;
            _style.alignment = anchor;
            _style.fontStyle = fontStyle;
            _style.normal.textColor = color;
            GUI.Label(r, content, _style);
        }

        // ---- interaction ----

        private static bool Hot(Rect r) =>
            Event.current != null && r.Contains(Event.current.mousePosition);

        private static bool Pressed(Rect r)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0) return false;
            if (!r.Contains(e.mousePosition)) return false;
            e.Use();
            return true;
        }

        // ---- widgets ----

        internal static bool Button(Rect r, string label, bool primary = false, bool enabled = true)
        {
            var hover = enabled && Hot(r);
            var bg = primary
                ? (hover ? Accent : AccentDim)
                : (hover ? SurfaceAlt : Surface);

            Fill(r, enabled ? bg : Surface);
            Stroke(r, primary && !hover ? Accent : Line);
            Text2(r, label, !enabled ? Faint : primary && hover ? Hex(0x0E1013) : Text,
                  SizeBody, TextAnchor.MiddleCenter);

            return enabled && Pressed(r);
        }

        /// <summary>A switch rather than a checkbox: the filled track reads at a glance mid-game.</summary>
        internal static bool Toggle(Rect r, string label, bool value, bool enabled = true)
        {
            const float trackW = 34f;
            const float trackH = 16f;

            var track = new Rect(r.xMax - trackW, r.y + (r.height - trackH) * 0.5f, trackW, trackH);
            var hover = enabled && Hot(r);

            Text2(new Rect(r.x, r.y, r.width - trackW - 10f, r.height), label,
                  !enabled ? Faint : hover ? Text : Hex(0xC9D1DC));

            Fill(track, !enabled ? Surface : value ? Accent : SurfaceAlt);
            Stroke(track, !enabled ? LineSoft : value ? Accent : Line);

            var knob = new Rect(value ? track.xMax - 13f : track.x + 1f, track.y + 1f, 12f, trackH - 2f);
            Fill(knob, !enabled ? Faint : value ? Hex(0x0E1013) : Hex(0x8A93A3));

            return enabled && Pressed(r) ? !value : value;
        }

        internal static float Slider(Rect r, string label, float value, float min, float max,
                                     string format = "0.0", bool enabled = true)
        {
            const float labelW = 128f;
            const float valueW = 52f;
            const float trackH = 4f;

            var id = ++_widgetId;
            Text2(new Rect(r.x, r.y, labelW, r.height), label, enabled ? Hex(0xC9D1DC) : Faint);

            var bar = new Rect(r.x + labelW, r.y, r.width - labelW - valueW, r.height);
            var track = new Rect(bar.x, bar.y + (bar.height - trackH) * 0.5f, bar.width, trackH);

            var e = Event.current;
            if (enabled && e != null)
            {
                var grab = new Rect(bar.x - 4f, bar.y, bar.width + 8f, bar.height);
                if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition))
                {
                    _activeSlider = id;
                    e.Use();
                }
                if (_activeSlider == id &&
                    (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
                {
                    var t = Mathf.Clamp01((e.mousePosition.x - track.x) / Mathf.Max(1f, track.width));
                    value = Mathf.Lerp(min, max, t);
                    if (e.type == EventType.MouseDrag) e.Use();
                }
            }

            var fraction = Mathf.Clamp01(Mathf.InverseLerp(min, max, value));
            Fill(track, enabled ? LineSoft : Surface);
            Fill(new Rect(track.x, track.y, track.width * fraction, track.height),
                 enabled ? Accent : Faint);

            var knobX = track.x + track.width * fraction;
            var knob = new Rect(knobX - 3f, bar.y + bar.height * 0.5f - 7f, 6f, 14f);
            Fill(knob, enabled ? Text : Faint);

            Text2(new Rect(bar.xMax + 6f, r.y, valueW - 6f, r.height),
                  value.ToString(format), enabled ? Text : Faint, SizeBody, TextAnchor.MiddleRight);

            return value;
        }

        /// <summary>Horizontal segmented control; returns the index that should be selected.</summary>
        internal static int Segmented(Rect r, string[] options, int selected, bool enabled = true)
        {
            var w = r.width / options.Length;
            var result = selected;

            for (var i = 0; i < options.Length; i++)
            {
                var cell = new Rect(r.x + w * i, r.y, w, r.height);
                var on = i == selected;
                var hover = enabled && Hot(cell);

                Fill(cell, on ? AccentDim : hover ? SurfaceAlt : Surface);
                Stroke(cell, on ? Accent : Line);
                Text2(cell, options[i], !enabled ? Faint : on ? Accent : Hex(0xA8B0BE),
                      SizeSmall, TextAnchor.MiddleCenter);

                if (enabled && Pressed(cell)) result = i;
            }

            return result;
        }

        /// <summary>Multi-select variant: returns the index toggled this frame, or -1.</summary>
        internal static int MultiSegmented(Rect r, string[] options, bool[] states, bool enabled = true)
        {
            var w = r.width / options.Length;
            var toggled = -1;

            for (var i = 0; i < options.Length; i++)
            {
                var cell = new Rect(r.x + w * i, r.y, w, r.height);
                var on = states[i];
                var hover = enabled && Hot(cell);

                Fill(cell, on ? AccentDim : hover ? SurfaceAlt : Surface);
                Stroke(cell, on ? Accent : Line);
                Text2(cell, options[i], !enabled ? Faint : on ? Accent : Hex(0xA8B0BE),
                      SizeSmall, TextAnchor.MiddleCenter);

                if (enabled && Pressed(cell)) toggled = i;
            }

            return toggled;
        }

        private static GUIStyle _field;

        internal static string TextField(Rect r, string text, string controlName = null)
        {
            Fill(r, SurfaceAlt);
            Stroke(r, Line);

            if (_field == null)
                _field = new GUIStyle { alignment = TextAnchor.MiddleLeft, richText = false };
            _field.fontSize = SizeBody;
            _field.normal.textColor = Text;
            _field.focused.textColor = Text;

            if (controlName != null) GUI.SetNextControlName(controlName);
            return GUI.TextField(new Rect(r.x + 7f, r.y, r.width - 14f, r.height), text ?? "", _field);
        }

        internal static void Note(Rect r, string content, Color tint)
        {
            Fill(new Rect(r.x, r.y, 2f, r.height), tint);
            Text2(new Rect(r.x + 9f, r.y, r.width - 9f, r.height), content, Muted, SizeSmall);
        }

        internal static void SectionLabel(Rect r, string content)
        {
            Text2(r, content.ToUpperInvariant(), Faint, SizeSmall, TextAnchor.LowerLeft, FontStyle.Bold);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), LineSoft);
        }

        internal static void Chip(Rect r, string content, Color fg)
        {
            Fill(r, Surface);
            Stroke(r, fg);
            Text2(r, content, fg, SizeSmall, TextAnchor.MiddleCenter);
        }
    }
}
