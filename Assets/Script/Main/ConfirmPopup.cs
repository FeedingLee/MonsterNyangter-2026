using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 확인 창 (실행 중 생성, 1920x1080, 게임이 멈춰 있어도 동작)
// 양피지 판 + 안내 문구 + 취소 / 확인 버튼. 되돌릴 수 없는 동작(포기하기, 특수 젬 분해) 전에 한 번 묻는다.
public static class ConfirmPopup
{
    private static GameObject current;

    public static bool IsOpen => current != null;

    // warning: 본문 아래 한 줄 띄워 빨갛고 조금 작게 따로 쓰는 경고 문구 (없으면 null)
    public static void Show(string message, string warning, string confirmLabel, UnityAction onConfirm, UnityAction onCancel = null)
    {
        Close();
        GemCatalog cat = GemCatalog.Instance;

        GameObject go = new GameObject("Confirm Popup", typeof(RectTransform));
        current = go;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        // 뒤를 누르지 못하게 덮는 어두운 바탕
        RectTransform dim = NewRect("Dim", go.transform, Vector2.zero, Vector2.zero);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.6f);

        // 판 높이 = 본문 + (경고) + 버튼 줄. 위에서부터 차례로 쌓음
        const int bodySize = 34;
        int lines = string.IsNullOrEmpty(message) ? 0 : message.Split('\n').Length;
        float bodyH = Mathf.Max(1, lines) * bodySize * 1.25f;
        bool hasWarning = !string.IsNullOrEmpty(warning);
        int warnSize = Mathf.RoundToInt(bodySize * cat.warningFontScale);
        float warnH = hasWarning ? warning.Split('\n').Length * warnSize * 1.3f : 0f;
        float pad = 44f, buttonH = 82f, buttonGap = 34f;
        float boardH = pad + bodyH + (hasWarning ? cat.warningGap + warnH : 0f) + buttonGap + buttonH + pad;
        Image board = Panel("Board", go.transform, cat.plainPanel != null ? cat.plainPanel : cat.paperPanel, Vector2.zero, new Vector2(720f, boardH));
        float top = boardH * 0.5f - pad;
        Text text = NewText("Message", board.transform, message, bodySize, new Color(0.27f, 0.15f, 0.07f, 1f), new Vector2(0f, top - bodyH * 0.5f), new Vector2(640f, bodyH + 10f));
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        top -= bodyH;
        if (hasWarning)
        {
            top -= cat.warningGap;
            Text warn = NewText("Warning", board.transform, warning, warnSize, cat.warningColor, new Vector2(0f, top - warnH * 0.5f), new Vector2(640f, warnH + 8f));
            warn.horizontalOverflow = HorizontalWrapMode.Wrap;
            top -= warnH;
        }
        float buttonY = top - buttonGap - buttonH * 0.5f;

        MakeButton("Cancel", board.transform, cat.buttonBrown, "취소", new Vector2(-165f, buttonY), () =>
        {
            Close();
            onCancel?.Invoke();
        });
        MakeButton("Confirm", board.transform, cat.buttonGreen, confirmLabel, new Vector2(165f, buttonY), () =>
        {
            Close();
            onConfirm?.Invoke();
        });
    }

    public static void Close()
    {
        if (current != null) Object.Destroy(current);
        current = null;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    private static Image Panel(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Image image = NewRect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        if (sprite == null) image.color = new Color(0.96f, 0.9f, 0.8f, 1f);
        return image;
    }

    private static Text NewText(string name, Transform parent, string content, int size, Color color, Vector2 pos, Vector2 box)
    {
        Text t = NewRect(name, parent, pos, box).gameObject.AddComponent<Text>();
        t.font = RunUI.Font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return t;
    }

    private static void MakeButton(string name, Transform parent, Sprite sprite, string label, Vector2 pos, UnityAction onClick)
    {
        Image image = Panel(name, parent, sprite, pos, new Vector2(280f, 82f));
        Button b = image.gameObject.AddComponent<Button>();
        b.targetGraphic = image;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(RunUI.ClickSound);
        b.onClick.AddListener(onClick);
        Text t = NewText("Label", image.transform, label, 34, Color.white, Vector2.zero, new Vector2(260f, 80f));
        SoftOutline o = t.gameObject.AddComponent<SoftOutline>();   // 큰 글씨: 8방향 불투명 외곽선 (흐림·뾰족함 없음)
        o.color = new Color(0.1f, 0.05f, 0.02f, 1f);
        o.distance = 2f;
    }
}
