using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메인무기 전용 스킬 버튼 (화면 오른쪽, 쿨타임 표시)
// 메인무기가 정해지면 MainWeaponController가 실행 중에 만든다. 누르고 있는 동안도 인식해 대검 모아 베기에 사용한다.
// 모양, 크기, 글자 설정은 매 프레임 다시 적용하므로 게임 중 인스펙터에서 바꿔도 바로 보인다.
public class MainWeaponSkillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public const string ButtonObjectName = "Main Skill Button";

    private RectTransform root;
    private Image background;
    private Image iconImage;
    private Image overlay;
    private Text label;
    private Outline outline;
    private Text innerLabel;
    private Outline innerOutline;
    private Sprite circleSprite;
    private bool pressLatched;
    // 시너지 빛 입자 (공명: 은은한 빛 입자, 각성: 더 많고 밝은 입자 + 반짝임 별)
    private RectTransform sparkleRoot;
    private class Spark
    {
        public Image image;
        public Vector2 velocity;
        public float age;
        public float life;
        public float size;
        public bool star;
    }
    private readonly System.Collections.Generic.List<Spark> sparks = new System.Collections.Generic.List<Spark>();
    private float spawnBudget;
    private float nextStar;

    // 버튼을 누르고 있는지
    public bool IsHeld { get; private set; }

    // 마지막 확인 이후 버튼을 눌렀으면 한 번만 true
    public bool ConsumePress()
    {
        bool pressed = pressLatched;
        pressLatched = false;
        return pressed;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        pressLatched = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
    }

    private void OnDisable()
    {
        IsHeld = false;
    }

    private void OnDestroy()
    {
        UiCircleSprite.Release(circleSprite);
        if (root != null)
        {
            Destroy(root.gameObject);
        }
    }

    public static MainWeaponSkillButton Create()
    {
        UltimateManager ultimate = FindObjectOfType<UltimateManager>();
        Transform parent = FindParent(ultimate);

        RectTransform rootRect = UltimateManager.CreateRect("Main Skill", parent);
        if (ultimate != null && ultimate.transform.parent == parent)
        {
            rootRect.SetSiblingIndex(ultimate.transform.GetSiblingIndex() + 1);
        }
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        if (rootRect.parent.TryGetComponent(out LayoutGroup _))
        {
            rootRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // 시너지 빛 입자를 담을 칸 (오버레이 캔버스라 파티클 시스템 대신 UI 이미지로 만든 입자를 씀)
        RectTransform sparkleRoot = UltimateManager.CreateRect("Synergy Sparkles", rootRect);
        UltimateManager.Stretch(sparkleRoot);

        RectTransform backgroundRect = UltimateManager.CreateRect(ButtonObjectName, rootRect);
        UltimateManager.Stretch(backgroundRect);
        Image background = backgroundRect.gameObject.AddComponent<Image>();

        RectTransform iconRect = UltimateManager.CreateRect("Icon", rootRect);
        Image iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform overlayRect = UltimateManager.CreateRect("Overlay", rootRect);
        UltimateManager.Stretch(overlayRect);
        Image overlay = overlayRect.gameObject.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;

        RectTransform labelRect = UltimateManager.CreateRect("Label", rootRect);
        Text label = labelRect.gameObject.AddComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        Outline outline = labelRect.gameObject.AddComponent<Outline>();

        // 버튼 안 글자 (쿨타임 초, 탄 수)
        RectTransform innerRect = UltimateManager.CreateRect("Count", rootRect);
        Text inner = innerRect.gameObject.AddComponent<Text>();
        inner.alignment = TextAnchor.MiddleCenter;
        inner.horizontalOverflow = HorizontalWrapMode.Overflow;
        inner.verticalOverflow = VerticalWrapMode.Overflow;
        inner.raycastTarget = false;
        Outline innerOutline = innerRect.gameObject.AddComponent<Outline>();

        MainWeaponSkillButton button = backgroundRect.gameObject.AddComponent<MainWeaponSkillButton>();
        button.sparkleRoot = sparkleRoot;
        button.innerLabel = inner;
        button.innerOutline = innerOutline;
        button.root = rootRect;
        button.background = background;
        button.iconImage = iconImage;
        button.overlay = overlay;
        button.label = label;
        button.outline = outline;
        button.circleSprite = UiCircleSprite.Create();
        return button;
    }

    // 시너지 단계 표시: 공명은 테두리 색, 각성은 테두리 색 + 반짝임 애니메이션
    public void SetSynergy(WeaponSynergy.Stage stage, WeaponSynergyData data)
    {
        if (sparkleRoot == null)
            return;

        float dt = Time.unscaledDeltaTime;                   // 일시정지 중에도 움직임
        bool on = stage != WeaponSynergy.Stage.None && root.gameObject.activeSelf;
        bool awakened = stage == WeaponSynergy.Stage.Awakening;
        Color color = awakened ? data.awakeningColor : data.resonanceColor;
        float size = root.sizeDelta.x;
        sparkleRoot.SetAsLastSibling();                      // 버튼 위에 그림 (입자는 둘레에서 바깥으로 퍼짐)

        if (on)
        {
            // 버튼 둘레에서 입자가 생겨 바깥으로 떠오르며 사라짐
            spawnBudget += (awakened ? 16f : 5f) * dt;
            while (spawnBudget >= 1f)
            {
                spawnBudget -= 1f;
                SpawnSpark(size, color, awakened, false);
            }
            if (awakened && Time.unscaledTime >= nextStar)
            {
                nextStar = Time.unscaledTime + Random.Range(0.25f, 0.5f);
                SpawnSpark(size, Color.white, true, true);   // 각성: 큰 반짝임 별
            }
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark p = sparks[i];
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f)
            {
                Destroy(p.image.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            p.image.rectTransform.anchoredPosition += p.velocity * dt;
            float scale = p.star ? Mathf.Sin(k * Mathf.PI) : 1f - k * 0.6f;
            p.image.rectTransform.sizeDelta = Vector2.one * p.size * scale;
            Color c = p.image.color;
            p.image.color = new Color(c.r, c.g, c.b, (p.star ? 1f : 0.85f) * Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI));
            if (p.star)
            {
                p.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            }
        }
    }

    private void SpawnSpark(float buttonSize, Color color, bool bright, bool star)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float radius = buttonSize * 0.5f * Random.Range(0.85f, 1.05f);

        GameObject go = new GameObject(star ? "Star" : "Spark", typeof(RectTransform));
        go.transform.SetParent(sparkleRoot, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = SpriteFx.SparkleSprite(star ? 3 : (bright ? 1 : 0));
        if (image.sprite == null)
        {
            image.sprite = circleSprite;
        }
        image.preserveAspect = true;
        Color tint = Color.Lerp(color, Color.white, bright ? 0.45f : 0.2f);
        image.color = new Color(tint.r, tint.g, tint.b, 0f);
        image.rectTransform.anchoredPosition = dir * radius;

        Spark p = new Spark
        {
            image = image,
            velocity = dir * Random.Range(8f, 22f) + Vector2.up * Random.Range(6f, 18f),
            life = star ? 0.45f : Random.Range(0.6f, 1.1f),
            size = star ? buttonSize * 0.32f : buttonSize * Random.Range(bright ? 0.1f : 0.07f, bright ? 0.17f : 0.12f),
            star = star,
        };
        sparks.Add(p);
    }

    // 필살기 버튼과 같은 캔버스, 같은 부모 아래(필살기 버튼 바로 뒤)에 배치해 레벨업 창 등을 가리지 않도록 함
    private static Transform FindParent(UltimateManager ultimate)
    {
        if (ultimate != null && ultimate.transform.parent != null)
        {
            return ultimate.transform.parent;
        }

        GameObject canvasObject = new GameObject("Main Skill Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject.transform;
    }

    // cooldownFill: 남은 쿨타임 비율(1이면 방금 사용), active: 스킬 사용 중 강조
    // inner: 버튼 안 글자 (쿨타임 초, 탄 수), text: 버튼 아래 글자 (사용 키)
    public void SetState(bool visible, float cooldownFill, bool active, string inner, string text, Sprite icon, MainWeaponSettings s)
    {
        if (root.gameObject.activeSelf != visible)
        {
            root.gameObject.SetActive(visible);
        }
        if (!visible)
            return;

        // 위치, 크기, 모양
        root.anchoredPosition = s.skillButtonOffset;
        root.sizeDelta = Vector2.one * s.skillButtonSize;
        root.localScale = Vector3.one * s.skillButtonScale;

        Sprite shape = s.skillButtonSprite != null ? s.skillButtonSprite : circleSprite;
        background.sprite = shape;
        background.color = active ? s.skillButtonActiveColor : s.skillButtonColor;
        overlay.sprite = shape;
        overlay.color = s.skillButtonOverlayColor;
        overlay.fillAmount = Mathf.Clamp01(cooldownFill);

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.rectTransform.sizeDelta = Vector2.one * (s.skillButtonSize * s.skillButtonIconScale);

        // 글자 (위치, 크기, 색, 테두리)
        Font font = s.skillButtonFont != null ? s.skillButtonFont : s.guideFont;
        label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = s.skillButtonFontSize;
        label.color = s.skillButtonLabelColor;
        label.text = text;
        label.rectTransform.sizeDelta = new Vector2(s.skillButtonSize * 2f, s.skillButtonFontSize * 1.5f);
        label.rectTransform.anchoredPosition = s.skillButtonLabelOffset;

        outline.enabled = s.skillButtonOutlineWidth > 0f;
        outline.effectColor = s.skillButtonOutlineColor;
        outline.effectDistance = new Vector2(s.skillButtonOutlineWidth, -s.skillButtonOutlineWidth);

        // 버튼 안 아래쪽: 쿨타임 남은 초, 탄 수 (아이콘을 가리지 않게 작게)
        innerLabel.font = label.font;
        innerLabel.fontSize = Mathf.Max(1, Mathf.RoundToInt(s.skillButtonFontSize * 1.1f));
        innerLabel.color = s.skillButtonLabelColor;
        innerLabel.text = inner;
        innerLabel.rectTransform.sizeDelta = new Vector2(s.skillButtonSize, s.skillButtonSize * 0.4f);
        innerLabel.rectTransform.anchoredPosition = new Vector2(0f, -s.skillButtonSize * 0.3f);
        innerOutline.enabled = outline.enabled;
        innerOutline.effectColor = s.skillButtonOutlineColor;
        innerOutline.effectDistance = outline.effectDistance;
        innerLabel.transform.SetAsLastSibling();               // 쿨타임 덮개 위에 표시
    }
}
