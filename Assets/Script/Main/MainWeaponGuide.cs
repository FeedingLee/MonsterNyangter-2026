using UnityEngine;
using UnityEngine.UI;

// 메인무기로 전환되는 순간 화면 위쪽에 조작 방법을 잠시 보여 주는 안내 UI
// 씬에 따로 배치하지 않아도 실행 중에 전용 캔버스를 만들어 표시한다.
public class MainWeaponGuide : MonoBehaviour
{
    private const float FadeTime = 1f;

    private CanvasGroup group;
    private float duration;
    private float elapsed;

    public static void Show(string title, string body, MainWeaponSettings settings)
    {
        GameObject root = new GameObject("Main Weapon Guide", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform panel = UltimateManager.CreateRect("Panel", root.transform);
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -180f);
        panel.sizeDelta = new Vector2(660f, 0f);
        panel.localScale = Vector3.one * GimmickCatalog.Instance.mainWeaponPanelScale;   // 창 크기 축소

        Image background = panel.gameObject.AddComponent<Image>();
        background.color = settings.guideBackgroundColor;
        background.raycastTarget = false;
        RunConfig rc = RunConfig.Instance;
        Color textColor = settings.guideTextColor;
        if (rc != null && rc.morningBoxLight != null)
        {
            // Morning_card 양피지 상자, 살짝 투명하게 (뒤 전투가 비쳐 보이게)
            background.sprite = rc.morningBoxLight;
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, GimmickCatalog.Instance.mainWeaponBgAlpha);   // 배경을 더 투명하게
            textColor = rc.parchmentTextColor;
            panel.sizeDelta = new Vector2(600f, 0f);
        }

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 20);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Font font = settings.guideFont != null ? settings.guideFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateText(panel, "Title", title, font, settings.guideTitleSize, FontStyle.Bold, textColor);
        CreateText(panel, "Body", body, font, settings.guideBodySize, FontStyle.Normal, textColor);

        // 해금 반짝임 (판넬 위쪽 양 끝, 레이아웃에 끼지 않게 따로)
        RectTransform deco = UltimateManager.CreateRect("Deco", root.transform);
        deco.anchorMin = deco.anchorMax = new Vector2(0.5f, 1f);
        deco.anchoredPosition = new Vector2(0f, -180f);
        deco.sizeDelta = Vector2.zero;
        UiFx.Play(UiFx.Kind.Twinkle, deco, new Vector2(-270f, -12f), 40f, new Color(1f, 0.95f, 0.75f, 0.85f), 10f);
        UiFx.Play(UiFx.Kind.Twinkle, deco, new Vector2(272f, -40f), 34f, new Color(1f, 0.95f, 0.75f, 0.85f), 10f);

        MainWeaponGuide guide = root.AddComponent<MainWeaponGuide>();
        guide.group = canvasGroup;
        guide.duration = settings.guideShowTime;
    }

    private static void CreateText(Transform parent, string objectName, string content, Font font, int size, FontStyle style, Color color)
    {
        RectTransform rect = UltimateManager.CreateRect(objectName, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    // 게임이 멈춰 있어도 정해진 시간 뒤에 사라지도록 실제 시간 기준으로 처리
    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed <= duration)
            return;

        group.alpha = 1f - (elapsed - duration) / FadeTime;
        if (group.alpha <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
