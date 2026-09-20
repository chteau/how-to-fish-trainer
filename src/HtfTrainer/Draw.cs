using UnityEngine;

namespace HtfTrainer
{
    /// <summary>Minimal IMGUI primitives: filled rects, rotated lines, outlined boxes and shadowed text.</summary>
    internal static class Draw
    {
        private static Texture2D _pixel;
        private static GUIStyle _text;
        private static GUIStyle _textCentered;

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

        internal static void Rect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Pixel);
            GUI.color = prev;
        }

        internal static void Line(Vector2 a, Vector2 b, Color color, float width = 1f)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 0.01f) return;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            Rect(new Rect(a.x, a.y - width * 0.5f, length, width), color);
            GUI.matrix = saved;
        }

        /// <summary>Single-pixel rectangle outline.</summary>
        internal static void Box(Rect r, Color color, float thickness = 1f)
        {
            Rect(new Rect(r.xMin, r.yMin, r.width, thickness), color);
            Rect(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), color);
            Rect(new Rect(r.xMin, r.yMin, thickness, r.height), color);
            Rect(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), color);
        }

        /// <summary>
        /// CS2-style box: the coloured outline is sandwiched between two black ones so it stays
        /// readable against both the sky and the water.
        /// </summary>
        internal static void OutlinedBox(Rect r, Color color)
        {
            var shadow = new Color(0f, 0f, 0f, 0.85f);
            Box(new Rect(r.xMin - 1f, r.yMin - 1f, r.width + 2f, r.height + 2f), shadow);
            Box(new Rect(r.xMin + 1f, r.yMin + 1f, Mathf.Max(0f, r.width - 2f), Mathf.Max(0f, r.height - 2f)), shadow);
            Box(r, color);
        }

        internal static void Text(Vector2 pos, string content, Color color, int size = 11, bool centered = false)
        {
            var style = centered ? Centered(size) : Left(size);

            var prev = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(pos.x + 1f, pos.y + 1f, 400f, 20f), content, style);
            style.normal.textColor = color;
            GUI.Label(new Rect(pos.x, pos.y, 400f, 20f), content, style);
            style.normal.textColor = prev;
        }

        private static GUIStyle Left(int size)
        {
            if (_text == null)
                _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, richText = false };
            _text.fontSize = size;
            return _text;
        }

        private static GUIStyle Centered(int size)
        {
            if (_textCentered == null)
                _textCentered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = false };
            _textCentered.fontSize = size;
            return _textCentered;
        }

        /// <summary>Centered text needs the rect to carry the width, so it gets its own helper.</summary>
        internal static void TextCentered(float centerX, float y, string content, Color color, int size = 11)
        {
            const float width = 400f;
            Text(new Vector2(centerX - width * 0.5f, y), content, color, size, centered: true);
        }
    }
}
