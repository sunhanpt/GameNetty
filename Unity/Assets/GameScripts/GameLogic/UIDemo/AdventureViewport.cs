using UnityEngine;
using UnityEngine.UI;

public sealed class AdventureViewport : MonoBehaviour
{
    private RectTransform bounds;
    private RectTransform content;
    private Rect lastArea;
    private Vector2 lastScreen;
    private float lastWidth;

    public static RectTransform Create(Transform parent)
    {
        GameObject container = new GameObject("SafeArea", typeof(RectTransform), typeof(AdventureViewport));
        AdventureViewport viewport = container.GetComponent<AdventureViewport>();
        viewport.bounds = (RectTransform)container.transform;
        viewport.bounds.SetParent(parent, false);
        AdventureUi.Stretch(viewport.bounds);

        viewport.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.content.SetParent(viewport.bounds, false);
        viewport.content.anchorMin = new Vector2(0.5f, 0f);
        viewport.content.anchorMax = new Vector2(0.5f, 1f);
        viewport.content.sizeDelta = new Vector2(440f, -40f);
        viewport.Refresh();
        return viewport.content;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        if (screen.x <= 0 || screen.y <= 0) return;
        Rect area = Screen.safeArea;
        if (area.width <= 0 || area.height <= 0) area = new Rect(Vector2.zero, screen);
        if (screen != lastScreen || area != lastArea)
        {
            bounds.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            bounds.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            lastScreen = screen;
            lastArea = area;
        }

        float width = bounds.rect.width;
        if (width != lastWidth)
        {
            content.sizeDelta = new Vector2(Mathf.Clamp(width - 32f, 0f, 600f), -40f);
            lastWidth = width;
        }
    }
}

internal static class AdventureUi
{
    public static readonly Color Background = new Color32(14, 27, 30, 255);
    public static readonly Color Surface = new Color32(24, 43, 45, 255);
    public static readonly Color Control = new Color32(42, 66, 64, 255);
    public static readonly Color Ink = new Color32(248, 238, 214, 255);
    public static readonly Color Muted = new Color32(174, 200, 183, 255);
    public static readonly Color Gold = new Color32(241, 192, 113, 255);
    public static readonly Color Warning = new Color32(246, 159, 148, 255);

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Text Label(Transform parent, Font font, string name, int size, TextAnchor alignment)
    {
        Text label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.font = font;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Ink;
        label.raycastTarget = false;
        label.supportRichText = false;
        return label;
    }

    public static VerticalLayoutGroup Stack(GameObject panel, RectOffset padding, float spacing)
    {
        VerticalLayoutGroup group = panel.AddComponent<VerticalLayoutGroup>();
        group.padding = padding;
        group.spacing = spacing;
        group.childForceExpandHeight = false;
        group.childControlHeight = group.childControlWidth = true;
        return group;
    }

    public static void StyleButton(Button button, Text label, bool primary)
    {
        Image image = button.GetComponent<Image>();
        image.sprite = null;
        image.color = primary ? Gold : Control;
        label.color = primary ? Background : Ink;
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(231, 242, 236, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(188, 206, 200, 255);
        colors.disabledColor = new Color32(170, 182, 179, 200);
        button.colors = colors;
    }
}
