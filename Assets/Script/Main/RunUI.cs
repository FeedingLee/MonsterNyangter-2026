using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 낮/밤, 상점, 아침 선택지 UI를 코드로 만들 때 쓰는 공용 도우미
public static class RunUI
{
    private static Sprite rounded;
    private static readonly System.Collections.Generic.Dictionary<(Sprite, Vector4), Sprite> slicedCache =
        new System.Collections.Generic.Dictionary<(Sprite, Vector4), Sprite>();

    // 테두리(9-slice)가 없는 에셋 스프라이트에 테두리를 지정한 사본을 만듦 (크기를 바꿔도 모서리 장식이 늘어나지 않음)
    public static Sprite Sliced(Sprite source, Vector4 border)
    {
        if (source == null)
            return null;

        if (slicedCache.TryGetValue((source, border), out Sprite cached) && cached != null)
            return cached;

        Rect rect = source.packed ? source.textureRect : source.rect;
        Vector4 clamped = new Vector4(
            Mathf.Min(border.x, rect.width * 0.49f), Mathf.Min(border.y, rect.height * 0.49f),
            Mathf.Min(border.z, rect.width * 0.49f), Mathf.Min(border.w, rect.height * 0.49f));
        Sprite sliced = Sprite.Create(source.texture, rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0,
            SpriteMeshType.FullRect, clamped);
        sliced.name = source.name + " (Sliced)";
        slicedCache[(source, border)] = sliced;
        return sliced;
    }

    // 기지 체력·능력치 판 글꼴 (RunConfig.panelFont, 비어 있으면 그대로)
    public static void ApplyPanelFont(Text text)
    {
        Font f = RunConfig.Instance != null ? RunConfig.Instance.panelFont : null;
        if (f != null && text != null)
        {
            text.font = f;
            text.fontStyle = RunConfig.Instance.panelFontStyle;   // 노토 산스 가변 글꼴은 기본 굵기가 가늘어 굵게
        }
    }

    // 에셋 판넬 (스프라이트가 없으면 둥근 기본 판넬)
    public static Image AssetPanel(string name, Transform parent, Sprite sprite, Vector4 border, Color fallbackColor,
        Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Panel(name, parent, fallbackColor, anchor, position, size);
        if (sprite != null)
        {
            image.sprite = Sliced(sprite, border);
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = RunConfig.Instance.uiPixelScale;
        }
        return image;
    }

    // 에셋 버튼 바 (글자는 흰색 + 테두리)
    public static Button AssetButton(string name, Transform parent, Sprite sprite, string label, int fontSize, Color fallbackColor,
        Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick)
    {
        Button button = Button(name, parent, label, fontSize, fallbackColor, anchor, position, size, onClick);
        SetButtonSprite(button, sprite);
        return button;
    }

    public static void SetButtonSprite(Button button, Sprite sprite)
    {
        if (sprite == null)
            return;

        Image image = (Image)button.targetGraphic;
        image.sprite = Sliced(sprite, RunConfig.Instance.buttonBorder);
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = RunConfig.Instance.uiPixelScale;
    }

    public static Font Font
    {
        get
        {
            Font font = RunConfig.Instance.font;
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    // 모서리가 둥근 흰색 9-slice 스프라이트 (패널, 버튼 배경)
    public static Sprite Rounded
    {
        get
        {
            if (rounded == null)
            {
                rounded = CreateRounded(64, 16);
            }
            return rounded;
        }
    }

    private static Sprite CreateRounded(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - (x + 0.5f), 0f, (x + 0.5f) - (size - radius));
                float dy = Mathf.Max(radius - (y + 0.5f), 0f, (y + 0.5f) - (size - radius));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius + 0.5f - distance));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    // 화면 전체를 덮는 캔버스 (720x1280 기준으로 크기 조절)
    public static Canvas CreateCanvas(string name, int sortingOrder, bool clickable)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0.5f;

        if (clickable)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = UltimateManager.CreateRect(name, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Panel(string name, Transform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    public static Image Icon(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    public static Text Label(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment,
        Vector2 anchor, Vector2 position, Vector2 size, float outlineWidth = 2f)
    {
        Text text = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Text>();
        text.font = Font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (outlineWidth > 0f)
        {
            // 반투명 한 겹을 두껍게 주면 번져 흐려 보이고, 기본 Outline(대각선 4방향)을 두껍게 주면 모서리가 뾰족뾰족함
            // → 큰 글씨는 8방향 외곽선(SoftOutline), 작은 글씨는 불투명 기본 외곽선
            if (fontSize >= 30)
            {
                SoftOutline soft = text.gameObject.AddComponent<SoftOutline>();
                soft.color = Color.black;
                soft.distance = Mathf.Min(outlineWidth, 2.5f);
            }
            else
            {
                Outline outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
            }
        }
        return text;
    }

    public static Button Button(string name, Transform parent, string label, int fontSize, Color color,
        Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick)
    {
        Image background = Panel(name, parent, color, anchor, position, size);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        Text text = Label("Label", background.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, size);
        UltimateManager.Stretch(text.rectTransform);
        return button;
    }

    public static void SetButtonText(Button button, string content)
    {
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = content;
        }
    }

    // 클릭 효과음 (기존 선택 효과음)
    public static void ClickSound()
    {
        PlayOr(RunConfig.Instance.uiPickSound, AudioManager.Sfx.Select);
    }

    // 지정한 클립이 있으면 재생, 없으면 기존 효과음
    public static void PlayOr(AudioClip clip, AudioManager.Sfx fallback)
    {
        if (AudioManager.instance == null)
            return;

        if (clip != null)
        {
            AudioManager.instance.PlayClip(clip);
        }
        else
        {
            AudioManager.instance.PlaySfx(fallback);
        }
    }
}
