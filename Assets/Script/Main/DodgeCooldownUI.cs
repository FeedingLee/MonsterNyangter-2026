using UnityEngine;
using UnityEngine.UI;

// 회피 쿨타임 원형 UI
// Canvas 아래의 빈 오브젝트에 붙이면 원, 아이콘, 텍스트를 실행 시 자동으로 생성한다.
public class DodgeCooldownUI : MonoBehaviour
{
    public const string ButtonObjectName = "Dodge Button";

    [Header("# Content")]
    public Sprite icon;                                     // 원 안에 표시할 아이콘
    public string label = "회피";                           // 원 아래에 표시할 텍스트
    public Font labelFont;                                  // 비워두면 Unity 기본 폰트 사용

    [Header("# Button Shape")]
    public Sprite buttonSprite;                             // 버튼 모양 (비우면 원)

    [Header("# Layout")]
    public Vector2 offsetFromCorner = new Vector2(-110f, 170f); // 화면 오른쪽 아래 모서리 기준 위치 (5시 방향)
    [Min(1f)] public float circleSize = 120f;               // 원 지름
    [Range(0.2f, 2f)] public float buttonScale = 0.7f;       // 버튼 전체 배율 (아이콘·글자 정렬을 유지한 채 함께 축소)
    [Range(0.1f, 1f)] public float iconScale = 0.6f;        // 원 대비 아이콘 크기
    [Min(1)] public int labelFontSize = 28;                 // 텍스트 크기
    public Vector2 labelOffset = new Vector2(0f, -88f);     // 원 중앙 기준 텍스트 위치

    [Header("# Label Outline")]
    [Min(0f)] public float labelOutlineWidth = 0f;          // 텍스트 테두리 두께 (0이면 없음)
    public Color labelOutlineColor = Color.black;           // 텍스트 테두리 색

    [Header("# Color")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);  // 비어 있는 원의 색
    public Color fillColor = new Color(0.15f, 0.5f, 1f, 1f);            // 차오르는 원의 색
    public Color labelColor = Color.white;                              // 텍스트 색
    public Image.FillMethod fillMethod = Image.FillMethod.Radial360;    // Radial360: 시계 방향, Vertical: 아래에서 위로

    [Header("# Reference")]
    public Player player;                                   // 비워두면 GameManager의 Player 사용

    private RectTransform rect;
    private GameObject visualRoot;
    private Image background;
    private Image fill;
    private Image iconImage;
    private Text labelText;
    private Outline labelOutline;
    private Sprite circleSprite;

    private void Awake()
    {
        rect = transform as RectTransform;
        if (rect == null || GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("DodgeCooldownUI는 Canvas 아래의 UI 오브젝트에 붙여야 합니다.", this);
            enabled = false;
            return;
        }

        circleSprite = UiCircleSprite.Create();
        BuildVisual();
        ApplySettings();
    }

    private void LateUpdate()
    {
        if (player == null && GameManager.instance != null)
        {
            player = GameManager.instance.player;
        }

        // 캐릭터 선택 전처럼 플레이어가 비활성화된 동안에는 표시하지 않음
        bool show = player != null && player.gameObject.activeInHierarchy;
        if (visualRoot.activeSelf != show)
        {
            visualRoot.SetActive(show);
        }

        if (show)
        {
            fill.fillAmount = player.DodgeCooldownProgress;
        }
    }

    // 플레이 중 인스펙터에서 값을 바꾸면 바로 반영
    private void OnValidate()
    {
        if (Application.isPlaying && visualRoot != null)
        {
            ApplySettings();
        }
    }

    private void OnDestroy()
    {
        UiCircleSprite.Release(circleSprite);
    }

    private void OnClickDodge()
    {
        if (player != null)
        {
            player.TryDodge();
        }
    }

    private void BuildVisual()
    {
        RectTransform root = CreateRect("Visual", transform);
        Stretch(root);
        visualRoot = root.gameObject;

        background = CreateCircle(ButtonObjectName, root);
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(OnClickDodge);

        fill = CreateCircle("Fill", root);
        fill.type = Image.Type.Filled;
        fill.fillClockwise = true;
        fill.fillAmount = 1f;

        RectTransform iconRect = CreateRect("Icon", root);
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform labelRect = CreateRect("Label", root);
        labelText = labelRect.gameObject.AddComponent<Text>();
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        labelOutline = labelRect.gameObject.AddComponent<Outline>();
    }

    private void ApplySettings()
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offsetFromCorner;
        rect.sizeDelta = new Vector2(circleSize, circleSize);
        rect.localScale = Vector3.one * buttonScale;

        Sprite shape = buttonSprite != null ? buttonSprite : circleSprite;
        background.sprite = shape;
        fill.sprite = shape;

        background.color = backgroundColor;

        fill.color = fillColor;
        fill.fillMethod = fillMethod;
        fill.fillOrigin = fillMethod == Image.FillMethod.Radial360
            ? (int)Image.Origin360.Top
            : (int)Image.OriginVertical.Bottom;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.rectTransform.sizeDelta = Vector2.one * (circleSize * iconScale);

        labelText.text = label;
        labelText.font = labelFont != null ? labelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = labelFontSize;
        labelText.color = labelColor;
        labelText.rectTransform.sizeDelta = new Vector2(circleSize * 2f, labelFontSize * 1.5f);
        labelText.rectTransform.anchoredPosition = labelOffset;

        labelOutline.enabled = labelOutlineWidth > 0f;
        labelOutline.effectColor = labelOutlineColor;
        labelOutline.effectDistance = new Vector2(labelOutlineWidth, -labelOutlineWidth);
    }

    private Image CreateCircle(string objectName, Transform parent)
    {
        RectTransform circleRect = CreateRect(objectName, parent);
        Stretch(circleRect);

        Image image = circleRect.gameObject.AddComponent<Image>();
        image.sprite = circleSprite;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}
