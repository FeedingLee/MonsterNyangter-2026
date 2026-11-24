using UnityEngine;
using UnityEngine.UI;

// 레벨업 선택지 리롤 버튼
// 레벨업 패널 아래의 빈 오브젝트에 붙이면 버튼을 실행 시 자동으로 생성하고, 레벨업 중에만 표시한다.
public class LevelUpReroll : MonoBehaviour
{
    public const string ButtonObjectName = "Reroll Button";

    [Header("# Rule")]
    [Min(0)] public int uses = 3;                               // 게임당 리롤 가능 횟수
    public AudioClip sound;                                     // 리롤 효과음 (비우면 선택 효과음)

    [Header("# Button")]
    public Sprite buttonSprite;                                 // 버튼 모양 (비우면 원)
    public Color buttonColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
    public Sprite icon;                                         // 버튼 안 아이콘
    [Range(0.1f, 1f)] public float iconScale = 0.6f;            // 버튼 대비 아이콘 크기

    [Header("# Layout")]
    public Vector2 position = new Vector2(0f, -300f);           // 부모 중앙 기준 버튼 위치
    [Min(1f)] public float buttonSize = 110f;                   // 버튼 크기

    [Header("# Remaining Count")]
    public Vector2 labelOffset = new Vector2(0f, -80f);         // 버튼 중앙 기준 텍스트 위치
    [Min(1)] public int labelFontSize = 28;
    public Font labelFont;                                      // 비워두면 Unity 기본 폰트 사용
    public Color labelColor = Color.white;

    private RectTransform rect;
    private GameObject visualRoot;
    private Button button;
    private Image background;
    private Image iconImage;
    private Text labelText;
    private Sprite circleSprite;
    private int remainingUses;
    private bool shownInfinite;                                 // 현재 표시 중인 리롤 무한 상태

    public static LevelUpReroll Instance { get; private set; }

    // 남은 리롤 횟수
    public int Remaining => remainingUses;

    // 리롤 횟수 추가 (고양이 직감, 행운의 부적)
    public void AddUses(int count)
    {
        remainingUses += count;
        RefreshCount();
    }

    // 리롤 1회 사용 (아침 선택지 다시 뽑기와 공유). 테스트 모드 리롤 무한이면 줄지 않음
    public bool TryUse()
    {
        if (TestMode.InfiniteReroll)
            return true;
        if (remainingUses <= 0)
            return false;

        remainingUses--;
        RefreshCount();
        return true;
    }

    private void Awake()
    {
        Instance = this;
        rect = transform as RectTransform;
        if (rect == null || GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("LevelUpReroll은 Canvas 아래의 UI 오브젝트에 붙여야 합니다.", this);
            enabled = false;
            return;
        }

        // 레벨업 패널에 레이아웃이 있어도 위치를 직접 지정할 수 있도록 제외
        if (!TryGetComponent(out LayoutElement layoutElement))
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        remainingUses = uses;
        circleSprite = UiCircleSprite.Create();
        BuildVisual();
        ApplySettings();
        RefreshCount();
        visualRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        bool show = LevelUp.instance != null && !LevelUp.instance.LevelUpFinish;
        if (visualRoot.activeSelf != show)
        {
            visualRoot.SetActive(show);
        }

        // 테스트 모드에서 리롤 무한을 켜고 끄면 표시를 바로 갱신
        if (shownInfinite != TestMode.InfiniteReroll)
        {
            RefreshCount();
        }
    }

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

    // 새 레벨업 화면의 리롤 버튼에서 호출
    public void Click() => OnClickReroll();

    private void OnClickReroll()
    {
        bool infinite = TestMode.InfiniteReroll;
        if (remainingUses <= 0 && !infinite)
            return;

        if (!infinite)                                          // 테스트 모드 리롤 무한이면 횟수를 쓰지 않음
        {
            remainingUses--;
        }
        LevelUp.instance.Reroll();

        if (sound != null)
        {
            AudioManager.instance.PlayClip(sound);
        }
        else
        {
            RunUI.ClickSound();                    // 비어 있으면 카드 선택 소리
        }

        RefreshCount();
    }

    private void RefreshCount()
    {
        shownInfinite = TestMode.InfiniteReroll;
        bool canUse = shownInfinite || remainingUses > 0;

        labelText.text = shownInfinite ? "∞" : remainingUses.ToString();
        button.interactable = canUse;
        iconImage.color = canUse ? Color.white : Color.gray;
    }

    private void BuildVisual()
    {
        RectTransform root = UltimateManager.CreateRect("Visual", transform);
        UltimateManager.Stretch(root);
        visualRoot = root.gameObject;

        RectTransform backgroundRect = UltimateManager.CreateRect(ButtonObjectName, root);
        UltimateManager.Stretch(backgroundRect);
        background = backgroundRect.gameObject.AddComponent<Image>();

        button = backgroundRect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(OnClickReroll);

        RectTransform iconRect = UltimateManager.CreateRect("Icon", root);
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform labelRect = UltimateManager.CreateRect("Remaining Count", root);
        labelText = labelRect.gameObject.AddComponent<Text>();
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
    }

    private void ApplySettings()
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(buttonSize, buttonSize);

        background.sprite = buttonSprite != null ? buttonSprite : circleSprite;
        background.color = buttonColor;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.rectTransform.sizeDelta = Vector2.one * (buttonSize * iconScale);

        labelText.font = labelFont != null ? labelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = labelFontSize;
        labelText.color = labelColor;
        labelText.rectTransform.sizeDelta = new Vector2(buttonSize, labelFontSize * 1.5f);
        labelText.rectTransform.anchoredPosition = labelOffset;
    }
}
