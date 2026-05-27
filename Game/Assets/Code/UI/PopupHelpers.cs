using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    internal static class PopupHelpers
    {
        // Finds or creates a Text child on the button so the button isn't a blank rectangle.
        // Idempotent — calling twice doesn't duplicate the child.
        public static Text EnsureButtonLabel(Button button, string text, int fontSize = 22)
        {
            if (button == null) return null;
            var existing = button.transform.Find("Label");
            Text t;
            if (existing != null)
            {
                t = existing.GetComponent<Text>();
                if (t == null) t = existing.gameObject.AddComponent<Text>();
            }
            else
            {
                // Some prefabs use "ButtonLabel" instead of "Label" — try that too before creating.
                var alt = button.transform.Find("ButtonLabel");
                if (alt != null)
                {
                    t = alt.GetComponent<Text>();
                    if (t == null) t = alt.gameObject.AddComponent<Text>();
                }
                else
                {
                    var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    var rt = go.GetComponent<RectTransform>();
                    rt.SetParent(button.transform, worldPositionStays: false);
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    t = go.GetComponent<Text>();
                }
            }
            if (string.IsNullOrEmpty(t.text)) t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.raycastTarget = false;
            t.fontStyle = FontStyle.Bold;
            t.font = t.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }
    }
}
