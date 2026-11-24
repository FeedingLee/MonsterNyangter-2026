using UnityEngine;
using UnityEngine.UI;

// 캠프 상점 (낮에만, 상점 NPC 옆에서 이용)
// 냥냥코인을 내고 능력치 카드 3장 중 1장을 고른다 (브론즈·실버·골드 등급, 골드 중 일부는 특수 선택지, 리롤 없음).
// 놓인 카드는 사거나 새 판이 될 때까지 유지되고, 가격은 구매마다 오른다. 수치와 그림은 Resources/StatCatalog.
// 판넬·고양이·랜턴·코인 띠는 서로 독립된 UI 오브젝트라 따로 크기·위치를 조정할 수 있다 (Ingame_shop 시트).
public class UpgradeShop : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static bool PlayerInRange { get; private set; }     // 상점을 이용할 수 있는 거리 안 (낮, 상점 NPC가 있을 때) - 캠프 비비기 처벌 예외
    public static Bounds RangeAnchor { get; private set; }     // 이용 거리를 재는 기준 (상점 NPC 그림)

    private static UpgradeShop instance;

    private RunConfig config;
    private GameManager gm;
    private GameObject world;                 // 맵에 텐트가 없을 때만 직접 만든 텐트
    private SpriteRenderer tent;              // 상점으로 쓰는 텐트
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform marker;             // 텐트 위 "상점" 표지판
    private Button prompt;
    private GameObject panel;

    public static void Create(Vector3 position)
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        new GameObject("Upgrade Camp").AddComponent<UpgradeShop>().Build(position);
    }

    private void Build(Vector3 fallbackPosition)
    {
        instance = this;
        config = RunConfig.Instance;
        gm = GameManager.instance;

        tent = FindMapTent();
        if (tent != null)
        {
            transform.position = tent.bounds.center;
        }
        else
        {
            // 맵에 텐트가 없으면 시작 위치 근처에 직접 세움
            transform.position = fallbackPosition;
            world = new GameObject("Camp Visual");
            world.transform.SetParent(transform, false);
            tent = CreateVisual("Tent", config.tentSprite, Vector2.zero, config.tentScale, -1, world.transform);
        }

        // UI (상점 UI는 기존 UI 위에 표시)
        canvas = RunUI.CreateCanvas("Shop UI", 20, true);
        canvasRect = (RectTransform)canvas.transform;

        BuildMarker();

        BuildPrompt();

        BuildPanel();
        panel.SetActive(false);
    }

    // 맵에 배치된 텐트 중 상점 텐트 스프라이트를 쓰는 것 (여러 개면 시작 위치에 가장 가까운 것)
    private SpriteRenderer FindMapTent()
    {
        if (config.tentSprite == null)
            return null;

        SpriteRenderer best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = gm.player.transform.position;

        foreach (SpriteRenderer renderer in FindObjectsOfType<SpriteRenderer>())
        {
            if (renderer.sprite != config.tentSprite)
                continue;

            float distance = Vector2.Distance(origin, renderer.bounds.center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = renderer;
            }
        }
        return best;
    }

    private SpriteRenderer CreateVisual(string name, Sprite sprite, Vector2 offset, float scale, int sortingOffset, Transform parent)
    {
        SpriteRenderer playerSprite = gm.player.GetComponent<SpriteRenderer>();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = playerSprite.sharedMaterial;
        renderer.sortingLayerID = playerSprite.sortingLayerID;
        renderer.sortingOrder = playerSprite.sortingOrder + sortingOffset;
        return renderer;
    }

    // 텐트 꼭대기 위에 떠 있는 "상점" 표지판 (양피지 + 발바닥)
    private void BuildMarker()
    {
        Image sign = RunUI.AssetPanel("Shop Marker", canvas.transform, config.cardSprite, config.cardBorder,
            new Color(0.95f, 0.85f, 0.7f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 40f));   // 작게 (150x58에서 70%)
        sign.raycastTarget = false;
        marker = sign.rectTransform;
        marker.pivot = new Vector2(0.5f, 0f);                    // 아래쪽 가운데가 텐트 꼭대기에 오도록

        RunUI.Icon("Paw", marker, config.pawSprite, new Vector2(0f, 0.5f), new Vector2(23f, 0f), Vector2.one * config.shopSignPawSize);   // 테두리·글자와 겹치지 않게 작게
        RunUI.Label("Text", marker, "상점", 20, config.parchmentTextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(11f, 0f), new Vector2(70f, 36f), 0f);

        marker.gameObject.SetActive(false);
    }

    // 상점 열기 버튼: 표지판과 같은 깔끔한 양피지 판 + 발바닥 아이콘 + 갈색 글자
    private void BuildPrompt()
    {
        Image plaque = RunUI.AssetPanel("Shop Prompt", canvas.transform, config.rowSprite, config.rowBorder,
            new Color(0.85f, 0.55f, 0.15f, 0.95f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(240f, 56f));   // 작게 (340x80에서 70%)
        plaque.color = new Color(plaque.color.r, plaque.color.g, plaque.color.b, config.shopPromptAlpha);   // 배경 반투명 (글자·아이콘은 그대로)
        prompt = plaque.gameObject.AddComponent<Button>();
        prompt.targetGraphic = plaque;
        prompt.navigation = new Navigation { mode = Navigation.Mode.None };
        prompt.onClick.AddListener(Open);

        RunUI.Icon("Paw", plaque.transform, config.pawSprite, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(30f, 30f));
        RunUI.Label("Text", plaque.transform, "상점 열기 [" + config.interactKey + "]", 21, config.parchmentTextColor,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(17f, 0f), new Vector2(182f, 42f), 0f);

        plaque.gameObject.AddComponent<CardPop>();               // 나타날 때 살짝 튀어나오는 효과
        prompt.gameObject.SetActive(false);
    }


    // ---------------- 상점 판넬 (판넬, 고양이, 랜턴, 코인 띠는 서로 독립된 오브젝트) ----------------
    private class CardView
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Image frame;
        public Text grade;
        public Image icon;
        public Text name;
        public Image band;
        public Text value;          // 일반: 수치 / 특수: 이득 줄
        public Text cost;           // 특수: 대가 줄
        public Image divider;
        public Text totalTitle;
        public Text total;
        public Image tag;
        public float hover;         // 마우스 올림 (0~1)
    }

    private StatCatalog cat;
    private RectTransform shopRoot;
    private Image catImage;
    private Text coinText;
    private Text priceText;
    private Button selectButton;
    private readonly CardView[] cards = new CardView[3];
    private int selected = -1;
    private bool animating;
    private float catReactUntil;
    private Sprite catReactSprite;

    private void BuildPanel()
    {
        cat = StatCatalog.Instance;

        shopRoot = RunUI.Rect("Shop", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        panel = shopRoot.gameObject;

        // 어두운 바탕 (게임 화면을 살짝 가림)
        Image dim = RunUI.Rect("Dim", shopRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f)).gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.35f);

        // 랜턴 → 판넬 → 고양이 순서로 그림 (고양이가 판넬 위에 손을 올린 모습)
        SpriteImage("Lantern", shopRoot, cat.lantern, cat.lanternPosition, cat.lanternHeight);

        Vector2 panelSize = SizeByHeight(cat.shopPanel, cat.panelHeight, new Vector2(900f, 580f));
        Image board = RunUI.Rect("Panel", shopRoot, new Vector2(0.5f, 0.5f), cat.panelPosition, panelSize).gameObject.AddComponent<Image>();
        board.sprite = cat.shopPanel;
        board.color = cat.shopPanel != null ? Color.white : new Color(0.25f, 0.17f, 0.11f, 1f);
        Transform root = board.transform;

        // 제목 (판넬 그림 속 제목 판 위)
        // 판넬 그림 속 제목 판은 가운데에서 조금 오른쪽이라, 같은 높이의 판넬 가운데에 제목 판을 덮어 씀
        Vector2 titlePos = new Vector2(0f, (0.5f - cat.titlePlateAnchor.y) * panelSize.y);
        if (cat.titlePlate != null)
        {
            Image plate = RunUI.Rect("Title Plate", root, new Vector2(0.5f, 0.5f), titlePos, cat.titlePlateSize).gameObject.AddComponent<Image>();
            plate.sprite = cat.titlePlate;
            plate.type = cat.titlePlate.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            plate.raycastTarget = false;
        }
        RunUI.Label("Title", root, "상점", cat.titleFontSize, cat.cardTextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), titlePos, new Vector2(300f, 60f), 0f);

        // 안내 문구 + 양쪽 발바닥
        Text subtitle = RunUI.Label("Subtitle", root, "아래의 능력치 중 하나를 선택하세요!", cat.subtitleFontSize, new Color(0.97f, 0.91f, 0.79f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, cat.subtitleY), new Vector2(620f, 40f), 0f);
        float half = subtitle.preferredWidth * 0.5f + 34f;
        RunUI.Icon("Paw L", root, cat.paw, new Vector2(0.5f, 0.5f), new Vector2(-half, cat.subtitleY), new Vector2(30f, 30f));
        RunUI.Icon("Paw R", root, cat.paw, new Vector2(0.5f, 0.5f), new Vector2(half, cat.subtitleY), new Vector2(30f, 30f));

        // 카드 3장
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = BuildCard(root, i);
        }

        // 나가기 / 선택
        Button exit = ShopButton("Exit", root, cat.buttonBrown, cat.iconExit, "나가기", new Vector2(-cat.buttonX, cat.buttonY), Close);
        exit.transition = Selectable.Transition.ColorTint;
        selectButton = ShopButton("Select", root, cat.buttonGreen, cat.iconSelect, "선택", new Vector2(cat.buttonX, cat.buttonY), Buy);
        priceText = RunUI.Label("Price", root, "", cat.buttonFontSize - 6, new Color(0.97f, 0.91f, 0.79f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, cat.buttonY), new Vector2(cat.buttonX * 2f - cat.buttonSize.x - 10f, 40f), 2f);   // 두 버튼 사이

        // 고양이 (판넬과 따로 크기·위치 조정)
        catImage = SpriteImage("Cat", shopRoot, cat.catIdle, cat.catPosition, cat.catHeight);

        // 냥냥코인 띠
        Image bar = RunUI.Rect("Coins", shopRoot, new Vector2(0.5f, 0.5f), cat.coinBarPosition, cat.coinBarSize).gameObject.AddComponent<Image>();
        bar.sprite = cat.coinBar;
        bar.type = cat.coinBar != null && cat.coinBar.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        bar.color = cat.coinBar != null ? Color.white : new Color(0.2f, 0.13f, 0.08f, 1f);
        float h = cat.coinBarSize.y;
        RunUI.Icon("Icon", bar.transform, cat.coinIcon != null ? cat.coinIcon : config.coinIcon, new Vector2(0f, 0.5f), new Vector2(h * 0.75f, 0f), Vector2.one * h * 0.7f);
        RunUI.Label("Name", bar.transform, "냥냥코인", cat.coinFontSize - 2, new Color(0.97f, 0.91f, 0.79f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(h * 1.3f + 70f, 0f), new Vector2(140f, h), 0f);
        coinText = RunUI.Label("Count", bar.transform, "", cat.coinFontSize + 6, new Color(1f, 0.82f, 0.3f, 1f), TextAnchor.MiddleRight,
            new Vector2(1f, 0.5f), new Vector2(-h * 0.5f - 50f, 0f), new Vector2(100f, h), 2f);
    }

    // 그림 비율대로 높이에 맞춘 이미지 (판넬과 독립)
    private Image SpriteImage(string name, Transform parent, Sprite sprite, Vector2 pos, float height)
    {
        Image image = RunUI.Icon(name, parent, sprite, new Vector2(0.5f, 0.5f), pos, SizeByHeight(sprite, height, Vector2.one * height));
        image.preserveAspect = true;
        return image;
    }

    private static Vector2 SizeByHeight(Sprite sprite, float height, Vector2 fallback)
    {
        if (sprite == null)
            return fallback;
        return new Vector2(height * sprite.rect.width / sprite.rect.height, height);
    }

    private Button ShopButton(string name, Transform parent, Sprite sprite, Sprite icon, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Image image = RunUI.Rect(name, parent, new Vector2(0.5f, 0.5f), pos, cat.buttonSize).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : new Color(0.4f, 0.3f, 0.2f, 1f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.66f, 0.6f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.45f, 0.43f, 0.41f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        float iconSize = cat.buttonSize.y * 0.5f;
        Text text = RunUI.Label("Label", image.transform, label, cat.buttonFontSize, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cat.buttonSize.x, cat.buttonSize.y), 2f);
        float width = text.preferredWidth;
        float total = iconSize + 12f + width;
        RunUI.Icon("Icon", image.transform, icon, new Vector2(0.5f, 0.5f), new Vector2(-total * 0.5f + iconSize * 0.5f, 0f), Vector2.one * iconSize);
        text.rectTransform.anchoredPosition = new Vector2(icon != null ? total * 0.5f - width * 0.5f : 0f, 0f);
        return button;
    }

    private CardView BuildCard(Transform root, int index)
    {
        CardView v = new CardView();
        Vector2 size = SizeByHeight(cat.cardBronze, cat.cardHeight, new Vector2(240f, cat.cardHeight));
        float x = (index - 1) * cat.cardSpacing;
        v.rect = RunUI.Rect("Card " + index, root, new Vector2(0.5f, 0.5f), new Vector2(x, cat.cardY), size);
        v.group = v.rect.gameObject.AddComponent<CanvasGroup>();
        v.frame = v.rect.gameObject.AddComponent<Image>();
        v.frame.preserveAspect = true;

        Button button = v.rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => SelectCard(index));
        CardHover hover = v.rect.gameObject.AddComponent<CardHover>();
        hover.view = v;

        float w = size.x, h = size.y;
        Color text = cat.cardTextColor;
        v.grade = RunUI.Label("Grade", v.rect, "", 22, cat.gradeTextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -h * 0.088f), new Vector2(w * 0.6f, 32f), 2f);   // 이름판 가운데
        v.icon = RunUI.Icon("Icon", v.rect, null, new Vector2(0.5f, 0.5f), new Vector2(0f, h * 0.165f), Vector2.one * h * 0.22f);
        v.name = RunUI.Label("Name", v.rect, "", 28, text, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.005f), new Vector2(w * 0.8f, 34f), 0f);
        v.band = RunUI.Rect("Band", v.rect, new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.12f), new Vector2(w * 0.72f, h * 0.13f)).gameObject.AddComponent<Image>();
        v.band.color = cat.valueBandColor;
        v.band.raycastTarget = false;
        v.value = RunUI.Label("Value", v.rect, "", 30, cat.goodColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.12f), new Vector2(w * 0.76f, 44f), 0f);
        v.cost = RunUI.Label("Cost", v.rect, "", 19, cat.badColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.17f), new Vector2(w * 0.76f, 30f), 0f);
        v.divider = RunUI.Icon("Divider", v.rect, cat.divider, new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.29f), new Vector2(w * 0.78f, 12f));
        v.divider.preserveAspect = false;
        v.totalTitle = RunUI.Label("Total Title", v.rect, "누적 효과", 17, cat.mutedColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.245f), new Vector2(110f, 22f), 0f);
        Image titleBack = RunUI.Rect("Back", v.totalTitle.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 20f)).gameObject.AddComponent<Image>();
        titleBack.raycastTarget = false;
        titleBack.transform.SetAsFirstSibling();
        titleBack.enabled = false;
        v.total = RunUI.Label("Total", v.rect, "", 18, text, TextAnchor.UpperCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -h * 0.385f), new Vector2(w * 0.74f, 48f), 0f);
        v.total.supportRichText = true;
        // 긴 이름·수치는 칸에 맞게 글자를 줄임 (넘치지 않게)
        FitText(v.name, 16);
        FitText(v.value, 12);
        FitText(v.cost, 12);
        FitText(v.total, 11);
        v.tag = RunUI.Icon("Special", v.rect, cat.specialTag, new Vector2(1f, 1f), new Vector2(w * cat.specialTagPosition.x, h * cat.specialTagPosition.y), cat.specialTagSize);
        return v;
    }

    private static void FitText(Text text, int minSize)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = text.fontSize;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    // 마우스를 올리면 카드가 살짝 커짐
    private class CardHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public CardView view;
        private bool over;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { over = true; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { over = false; }
        private void OnDisable() { over = false; }
        private void Update()
        {
            view.hover = Mathf.MoveTowards(view.hover, over ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        }
    }

    private static void PlayClip(AudioClip clip, AudioManager.Sfx fallback)
    {
        if (clip != null)
        {
            AudioManager.instance.PlayClip(clip);
        }
        else
        {
            AudioManager.instance.PlaySfx(fallback);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false;
        }
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    private void Update()
    {
        bool available = DayNightCycle.ShopAvailable;
        UpdateMarker(available && gm.isLive && !IsOpen);

        // 텐트 가장자리에서 일정 거리 안이면 상점 이용 가능 (큰 텐트도 문 앞에서 열 수 있도록)
        Vector3 playerPosition = gm.player.transform.position;
        Bounds anchor = AnchorBounds;
        Vector3 closest = anchor.ClosestPoint(new Vector3(playerPosition.x, playerPosition.y, anchor.center.z));
        bool keeperHere = ShopKeeper.Instance == null || ShopKeeper.Instance.Present;   // 상점 NPC가 있을 때만
        RangeAnchor = anchor;
        PlayerInRange = available && keeperHere && Vector2.Distance(playerPosition, closest) <= config.interactRadius;
        bool near = available && keeperHere && !IsOpen && gm.isLive && !MorningChoice.IsOpen &&
                    Vector2.Distance(playerPosition, closest) <= config.interactRadius;
        if (prompt.gameObject.activeSelf != near)
        {
            prompt.gameObject.SetActive(near);
        }

        if (near && Input.GetKeyDown(config.interactKey))
        {
            Open();
        }
        else if (IsOpen)
        {
            UpdateOpenPanel();
            // ESC는 옵션창이 같은 프레임에 열리므로 쓰지 않음 (나가기 버튼 또는 상점 키)
            if (Input.GetKeyDown(config.interactKey))
            {
                Close();
            }
        }
    }

    // 상점 위치: 상점 NPC가 있으면 NPC, 없으면 텐트
    private Bounds AnchorBounds => ShopKeeper.Instance != null ? ShopKeeper.Instance.Bounds : tent.bounds;

    // 표지판을 텐트 꼭대기(장대 끝) 바로 위에 띄우고 위아래로 살짝 움직임
    private void UpdateMarker(bool show)
    {
        Camera cam = Camera.main;
        if (show && cam != null)
        {
            Bounds bounds = AnchorBounds;
            bool keeper = ShopKeeper.Instance == null || ShopKeeper.Instance.Present;   // 상점 NPC가 없으면 표지판도 없음
            Vector3 top = new Vector3(bounds.center.x + bounds.size.x * config.shopMarkerOffset.x,
                bounds.max.y + bounds.size.y * config.shopMarkerOffset.y + Mathf.Sin(Time.time * 3f) * 0.12f, 0f);
            if (ShopKeeper.Instance != null)
            {
                top = new Vector3(bounds.center.x, bounds.max.y + config.shopperMarkerHeight + Mathf.Sin(Time.time * 3f) * 0.08f, 0f);   // 상점 NPC 머리 위
            }
            Vector3 screen = cam.WorldToScreenPoint(top);
            show = screen.z > 0f && keeper;
            if (show)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
                marker.anchoredPosition = local;
            }
        }

        if (marker.gameObject.activeSelf != show)
        {
            marker.gameObject.SetActive(show);
        }
    }

    private void Open()
    {
        if (IsOpen || !gm.isLive)
            return;

        IsOpen = true;
        panel.SetActive(true);
        prompt.gameObject.SetActive(false);
        marker.gameObject.SetActive(false);
        gm.Stop();
        PlayClip(config.shopOpenSound, AudioManager.Sfx.Select);   // 상점 열기 효과음 (마림바)
        ReactCat(cat.catWave);                                    // 반갑게 손 흔들기

        // 놓인 카드가 없을 때만 새로 뽑음 (나갔다 와도 같은 카드 → 리롤 없음)
        bool fresh = RunStats.ShopOffer.Count == 0;
        if (fresh)
        {
            DealOffer();
        }
        selected = -1;
        Refresh();
        StartCoroutine(DealIn());
    }

    private void Close()
    {
        if (!IsOpen || animating)
            return;

        IsOpen = false;
        panel.SetActive(false);
        gm.Resume();
        PlayClip(config.shopClickSound, AudioManager.Sfx.Select);   // 잔잔한 딸깍
    }

    // 카드 3장 무작위 (등급 확률, 골드 중 일부는 특수 선택지). 같은 능력치·같은 등급은 겹치지 않음
    private void DealOffer()
    {
        RunStats.ShopOffer.Clear();
        int count = Mathf.Min(cards.Length, Mathf.Max(1, cat.shopCardCount));
        int guard = 0;
        while (RunStats.ShopOffer.Count < count && guard++ < 200)
        {
            ShopCard card = new ShopCard { grade = RollGrade() };
            if (card.grade == ShopGrade.Gold && cat.specials.Count > 0 && Random.value < cat.goldSpecialRate)
            {
                card.specialIndex = Random.Range(0, cat.specials.Count);
            }
            else if (cat.shopStats.Count > 0)
            {
                card.statIndex = Random.Range(0, cat.shopStats.Count);
            }
            else
            {
                break;
            }

            bool duplicate = RunStats.ShopOffer.Exists(c => c.grade == card.grade && c.statIndex == card.statIndex && c.specialIndex == card.specialIndex);
            if (!duplicate)
            {
                RunStats.ShopOffer.Add(card);
            }
        }
    }

    private ShopGrade RollGrade()
    {
        Vector3 r = cat.gradeRates;
        float total = Mathf.Max(0.0001f, r.x + r.y + r.z);
        float roll = Random.value * total;
        if (roll < r.x) return ShopGrade.Bronze;
        if (roll < r.x + r.y) return ShopGrade.Silver;
        return ShopGrade.Gold;
    }

    private int Price => cat.Price(RunStats.ShopPurchases);

    private void SelectCard(int index)
    {
        if (animating || index >= RunStats.ShopOffer.Count)
            return;
        selected = index;
        PlayClip(config.shopClickSound, AudioManager.Sfx.Select);
        Refresh();
    }

    private void Buy()
    {
        if (animating || selected < 0 || selected >= RunStats.ShopOffer.Count)
            return;
        if (!CoinWallet.TrySpend(Price))
            return;

        ShopCard card = RunStats.ShopOffer[selected];
        if (card.IsSpecial)
        {
            StatCatalog.SpecialDef s = cat.specials[card.specialIndex];
            Grant(s.gainStat, s.gain);
            Grant(s.costStat, s.cost);
        }
        else
        {
            StatCatalog.ShopStat s = cat.shopStats[card.statIndex];
            Grant(s.stat, s.Value(card.grade));
        }
        RunStats.ShopPurchases++;

        PlayClip(config.shopBuySound, AudioManager.Sfx.Player_LevelUp);   // 구매 효과음
        ReactCat(cat.catSurprise);
        StartCoroutine(PickAndDeal(selected));
    }

    private static void Grant(RunStat stat, float value)
    {
        RunStats.AddValue(stat, value, StatSource.Shop);
        if (stat == RunStat.Rerolls && LevelUpReroll.Instance != null)
        {
            LevelUpReroll.Instance.AddUses(Mathf.RoundToInt(value));     // 리롤 횟수는 바로 지급
        }
    }

    // 고른 카드는 떠오르며 사라지고, 나머지도 사라진 뒤 새 카드 3장이 스르륵 올라옴
    private System.Collections.IEnumerator PickAndDeal(int picked)
    {
        animating = true;
        float t = 0f;
        Vector2[] start = new Vector2[cards.Length];
        for (int i = 0; i < cards.Length; i++) start[i] = cards[i].rect.anchoredPosition;
        while (t < cat.pickOutTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / cat.pickOutTime);
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].group.alpha = 1f - k;
                cards[i].rect.anchoredPosition = start[i] + new Vector2(0f, i == picked ? 40f * k : -20f * k);
            }
            yield return null;
        }

        RunStats.ShopOffer.Clear();
        DealOffer();
        selected = -1;
        Refresh();
        yield return DealIn();
    }

    private System.Collections.IEnumerator DealIn()
    {
        animating = true;
        float[] baseX = new float[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            baseX[i] = (i - 1) * cat.cardSpacing;
            cards[i].group.alpha = 0f;
            cards[i].rect.anchoredPosition = new Vector2(baseX[i], cat.cardY + cat.dealOffset);
        }

        float t = 0f;
        float end = cat.dealTime + cat.dealStagger * (cards.Length - 1);
        while (t < end)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < cards.Length; i++)
            {
                float k = Mathf.Clamp01((t - cat.dealStagger * i) / cat.dealTime);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);        // 끝에서 부드럽게 멈춤
                cards[i].group.alpha = k;
                cards[i].rect.anchoredPosition = new Vector2(baseX[i], cat.cardY + cat.dealOffset * (1f - e));
            }
            yield return null;
        }
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].group.alpha = 1f;
            cards[i].rect.anchoredPosition = new Vector2(baseX[i], cat.cardY);
        }
        animating = false;
        Refresh();
    }

    private void ReactCat(Sprite sprite)
    {
        catReactSprite = sprite;
        catReactUntil = Time.unscaledTime + cat.catReactTime;
    }

    // 매 프레임: 고양이 표정, 카드 크기(마우스 올림·선택), 코인
    private void UpdateOpenPanel()
    {
        coinText.text = CoinWallet.Coins.ToString();
        Sprite face = Time.unscaledTime < catReactUntil && catReactSprite != null ? catReactSprite : cat.catIdle;
        if (catImage.sprite != face && face != null) catImage.sprite = face;

        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < cards.Length; i++)
        {
            CardView v = cards[i];
            float goal = i == selected ? cat.cardSelectedScale : 1f + (cat.cardHoverScale - 1f) * v.hover;
            v.rect.localScale = Vector3.Lerp(v.rect.localScale, Vector3.one * goal, k);
            v.frame.color = selected >= 0 && i != selected ? cat.cardDimColor : Color.white;
        }

        bool canBuy = !animating && selected >= 0 && CoinWallet.Coins >= Price;
        if (selectButton.interactable != canBuy) selectButton.interactable = canBuy;
        priceText.text = "가격 " + Price + " 냥냥코인";
        priceText.color = CoinWallet.Coins >= Price ? new Color(0.97f, 0.91f, 0.79f, 1f) : cat.badColor;
    }

    // 카드 내용 채우기
    private void Refresh()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            CardView v = cards[i];
            bool has = i < RunStats.ShopOffer.Count;
            v.rect.gameObject.SetActive(has);
            if (!has)
                continue;

            ShopCard card = RunStats.ShopOffer[i];
            v.frame.sprite = card.grade == ShopGrade.Bronze ? cat.cardBronze : card.grade == ShopGrade.Silver ? cat.cardSilver : cat.cardGold;
            // 등급마다 그림 크기가 조금씩 달라(골드는 반짝이 포함) 같은 배율로 그려 테두리 크기를 맞춤
            if (v.frame.sprite != null && cat.cardBronze != null)
            {
                float k = cat.cardHeight / cat.cardBronze.rect.height;
                v.rect.sizeDelta = v.frame.sprite.rect.size * k;
            }
            v.grade.text = card.grade == ShopGrade.Bronze ? "브론즈" : card.grade == ShopGrade.Silver ? "실버" : "골드";
            // 등급마다 그림 위쪽 여백이 달라(골드는 반짝이) 이름판 가운데에 맞춤
            float fromTop = card.grade == ShopGrade.Bronze ? cat.gradeLabelFromTop.x : card.grade == ShopGrade.Silver ? cat.gradeLabelFromTop.y : cat.gradeLabelFromTop.z;
            float scale = cat.cardBronze != null ? cat.cardHeight / cat.cardBronze.rect.height : 1f;
            v.grade.rectTransform.anchoredPosition = new Vector2(0f, -fromTop * scale);
            v.tag.gameObject.SetActive(card.IsSpecial);

            if (card.IsSpecial)
            {
                StatCatalog.SpecialDef s = cat.specials[card.specialIndex];
                v.icon.sprite = s.icon;
                v.name.text = s.name;
                v.band.enabled = false;
                v.value.fontSize = v.value.resizeTextMaxSize = 21;
                v.value.text = Arrow(s.gainStat, s.gain) + " " + cat.Name(s.gainStat) + " " + cat.FormatDelta(s.gainStat, s.gain);
                v.value.color = Good(s.gainStat, s.gain) ? cat.goodColor : cat.badColor;
                v.value.rectTransform.anchoredPosition = new Vector2(0f, -cat.cardHeight * 0.085f);
                v.value.rectTransform.sizeDelta = new Vector2(v.value.rectTransform.sizeDelta.x, 26f);   // 한 줄 (길면 글자를 줄임)
                v.cost.rectTransform.sizeDelta = new Vector2(v.cost.rectTransform.sizeDelta.x, 24f);
                v.total.rectTransform.sizeDelta = new Vector2(v.total.rectTransform.sizeDelta.x, 36f);
                v.cost.gameObject.SetActive(true);
                v.cost.text = Arrow(s.costStat, s.cost) + " " + cat.Name(s.costStat) + " " + cat.FormatDelta(s.costStat, s.cost);
                v.cost.color = Good(s.costStat, s.cost) ? cat.goodColor : cat.badColor;
                v.cost.rectTransform.anchoredPosition = new Vector2(0f, -cat.cardHeight * 0.165f);
                v.total.fontSize = v.total.resizeTextMaxSize = 18;
                v.total.lineSpacing = 0.95f;
                v.total.rectTransform.anchoredPosition = new Vector2(0f, -cat.cardHeight * 0.375f);
                // 이름은 위 두 줄에 있으므로 누적 효과는 ▲ / ▼ 표시와 수치만
                v.total.text = Arrow(s.gainStat, s.gain) + " " + TotalLine(s.gainStat, s.gain, s.gainStat == s.costStat ? s.cost : 0f, false) + "\n" +
                               Arrow(s.costStat, s.cost) + " " + TotalLine(s.costStat, s.cost, s.gainStat == s.costStat ? s.gain : 0f, false);
            }
            else
            {
                StatCatalog.ShopStat s = cat.shopStats[card.statIndex];
                float value = s.Value(card.grade);
                v.icon.sprite = cat.Icon(s.stat);
                v.name.text = cat.Name(s.stat);
                v.band.enabled = true;
                v.value.fontSize = v.value.resizeTextMaxSize = 34;
                v.value.text = cat.FormatDelta(s.stat, value);
                v.value.color = Good(s.stat, value) ? cat.goodColor : cat.badColor;
                v.value.rectTransform.anchoredPosition = new Vector2(0f, -cat.cardHeight * 0.12f);
                v.value.rectTransform.sizeDelta = new Vector2(v.value.rectTransform.sizeDelta.x, 44f);
                v.total.rectTransform.sizeDelta = new Vector2(v.total.rectTransform.sizeDelta.x, 26f);
                v.cost.gameObject.SetActive(false);
                v.total.fontSize = v.total.resizeTextMaxSize = 21;
                v.total.lineSpacing = 1f;
                v.total.rectTransform.anchoredPosition = new Vector2(0f, -cat.cardHeight * 0.385f);
                v.total.text = TotalLine(s.stat, value, 0f, false);
            }
            v.icon.enabled = v.icon.sprite != null;
        }
    }

    // 이득인지 (감소형은 줄어드는 쪽이 이득)
    private bool Good(RunStat stat, float delta)
    {
        StatCatalog.StatDef d = cat.Def(stat);
        bool decrease = d != null && d.direction == StatDirection.Decrease;
        return decrease ? delta < 0f : delta > 0f;
    }

    private string Arrow(RunStat stat, float delta) => Good(stat, delta) ? "▲" : "▼";

    // 누적 효과 한 줄: 지금 → 산 뒤 (최대 체력은 실제 최대 체력 수치)
    private string TotalLine(RunStat stat, float delta, float sameStatExtra, bool showName)
    {
        float now = RunStats.Get(stat);
        float after = RunStats.Preview(stat, delta + sameStatExtra);
        string color = ColorUtility.ToHtmlStringRGB(Good(stat, after - now) ? cat.goodColor : cat.badColor);
        string a, b;
        if (stat == RunStat.MaxHealthFlat)
        {
            float pct = RunStats.Get(RunStat.MaxHealth);
            float baseHp = gm.maxHealth / Mathf.Max(0.0001f, pct) - now;   // 고정값을 뺀 기준 체력
            a = Mathf.RoundToInt((baseHp + now) * pct).ToString();
            b = Mathf.RoundToInt((baseHp + after) * pct).ToString();
            return (showName ? "최대 체력 " : "") + a + " → <color=#" + color + ">" + b + "</color>";
        }
        a = cat.FormatDelta(stat, RunStats.AsDelta(stat, now));
        b = cat.FormatDelta(stat, RunStats.AsDelta(stat, after));
        string label = showName ? cat.Name(stat) + " " : "";
        return label + a + " → <color=#" + color + ">" + b + "</color>";
    }
}
