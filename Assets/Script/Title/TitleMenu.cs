using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 씬 메뉴 (씬 파일 수정 없이 Title 씬이 열릴 때 자동 생성)
// 메인 메뉴 → 캐릭터 선택 → 스테이지 선택 → 출발(Main 씬) / 메인 메뉴 → 설정
// 배경과 태양 빛·나뭇잎은 기존 Title BG + TitleAmbience 그대로 사용
// 데이터와 수치는 Resources/MenuCatalog 에셋. 화면별 코드는 TitleMenu.Character / Stage / Settings
public partial class TitleMenu : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MenuCatalog.Instance.titleSceneName || FindObjectOfType<TitleMenu>() != null)
            return;
        Time.timeScale = 1f;   // 일시정지 중에 그만두기로 돌아와도 타이틀 연출·배경음이 멈추지 않게
        new GameObject("Title Menu").AddComponent<TitleMenu>();
    }

    private MenuCatalog cat;
    private RectTransform root;
    private CanvasGroup rootGroup;
    private CanvasGroup backdrop;
    private CanvasGroup mainPanel, characterPanel, stagePanel, settingsPanel;
    private CanvasGroup current;
    private Image fadeOverlay;
    private Text loadingText;
    private bool busy, launching;

    // 소리
    private AudioSource clickSource;
    private AudioSource titleBgm;
    private BGM_FadeIn titleBgmFade;
    private float titleBgmScale = 1f;

    private void Start()
    {
        cat = MenuCatalog.Instance;
        AudioClip oldClick = HideOldTitleUi();
        SetupAudio(oldClick);
        BuildCanvas();

        mainPanel = BuildMainMenu();
        characterPanel = BuildCharacterSelect();
        stagePanel = BuildStageSelect();
        settingsPanel = BuildSettings();
        BuildGemPanels();                                    // 캐릭터 강화(젬) 화면

        foreach (CanvasGroup g in new[] { characterPanel, stagePanel, settingsPanel })
        {
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.gameObject.SetActive(false);
        }
        backdrop.alpha = 0f;
        current = mainPanel;

        // 인게임에서 그만두기로 돌아왔으면 캐릭터 선택 화면(마지막 캐릭터)부터
        if (GameLaunch.ConsumeOpenCharacterSelect())
        {
            int index = cat.characters.FindIndex(c => c.playerId == GameLaunch.LastCharacterId);
            characterIndex = Mathf.Max(0, index);
            mainPanel.alpha = 0f;
            mainPanel.blocksRaycasts = false;
            mainPanel.gameObject.SetActive(false);
            characterPanel.gameObject.SetActive(true);
            characterPanel.alpha = 1f;
            characterPanel.blocksRaycasts = true;
            backdrop.alpha = 1f;
            current = characterPanel;
            OnCharacterOpen();
        }
    }

    private Transform titleBackground;
    private Vector3 titleBackgroundBase;
    private Vector2 parallax;

    // 마우스 위치에 따라 배경을 반대쪽으로 살짝 밀어 깊이감을 줌 (태양 빛·나뭇잎은 TitleAmbience가 배경 위치를 따라감)
    private void UpdateParallax()
    {
        if (titleBackground == null)
        {
            GameObject bg = GameObject.Find(cat.titleBackgroundName);
            if (bg == null) return;
            titleBackground = bg.transform;
            titleBackgroundBase = titleBackground.position;
            titleBackground.localScale *= cat.parallaxZoom;
        }
        Vector2 target = Vector2.zero;
        if (Screen.width > 0 && Screen.height > 0 && !launching)
        {
            Vector3 m = Input.mousePosition;
            Vector2 n = new Vector2(Mathf.Clamp01(m.x / Screen.width) - 0.5f, Mathf.Clamp01(m.y / Screen.height) - 0.5f) * 2f;
            target = new Vector2(-n.x * cat.parallaxAmount, -n.y * cat.parallaxVertical);
        }
        parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-cat.parallaxSmooth * Time.unscaledDeltaTime));
        titleBackground.position = titleBackgroundBase + (Vector3)parallax;
    }

    private void Update()
    {
        UpdateParallax();
        UpdateToast();
        if (busy || launching)
            return;

        if (ConfirmPopup.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ConfirmPopup.Close();   // 확인 창이 떠 있으면 ESC는 취소만
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Back();
        }
        else if (current == characterPanel)
        {
            UpdateCharacterKeys();
        }
        else if (current == stagePanel && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OnDepart();
        }

        if (current == characterPanel)
        {
            AnimateCharacter();
        }
        else if (current == gemMainPanel)
        {
            AnimateShopper();
        }
    }

    // 뒤로 가기 (ESC, 돌아가기 버튼)
    private void Back()
    {
        if (current == characterPanel) Show(mainPanel);
        else if (current == stagePanel) Show(characterPanel);
        else if (current == settingsPanel) CancelSettings();
        else if (current == gemMainPanel) Show(mainPanel);
        else if (current == gemStoragePanel || current == gemCombinePanel || current == gemRerollPanel || current == gemDecomposePanel) Show(gemMainPanel);
    }

    // ---------------- 기존 타이틀 UI ----------------
    // '클릭하여 시작하기' 문구, 기존 로고, 화면 전체 시작 버튼을 숨김. 시작 버튼의 소리는 메뉴 버튼 소리로 씀
    private AudioClip HideOldTitleUi()
    {
        AudioClip clip = null;
        Scene scene = SceneManager.GetActiveScene();
        foreach (string name in cat.hiddenTitleObjects)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != name)
                        continue;
                    AudioSource source = t.GetComponent<AudioSource>();
                    if (clip == null && source != null) clip = source.clip;
                    t.gameObject.SetActive(false);
                }
            }
        }
        return clip;
    }

    // ---------------- 소리 ----------------
    private void SetupAudio(AudioClip oldClick)
    {
        clickSource = gameObject.AddComponent<AudioSource>();
        clickSource.playOnAwake = false;
        clickSource.clip = cat.clickSound != null ? cat.clickSound : oldClick;

        titleBgmFade = FindObjectOfType<BGM_FadeIn>();
        if (titleBgmFade != null)
        {
            titleBgm = titleBgmFade.GetComponent<AudioSource>();
            titleBgmScale = cat.titleBgmOutputScale;             // 실제 타이틀 배경음 = 설정값 x 배율
        }
        PreviewVolume(MenuCatalog.SavedBgmVolume, MenuCatalog.SavedSfxVolume);
    }

    private float sfxPreview = 0.5f;

    // 설정 슬라이더를 움직이면 바로 들리도록 (저장은 적용 버튼)
    private void PreviewVolume(float bgm, float sfx)
    {
        sfxPreview = sfx;
        if (titleBgmFade == null || titleBgm == null)
            return;

        float max = Mathf.Clamp01(bgm * titleBgmScale);
        titleBgmFade.maxVolume = max;
        if (Time.timeSinceLevelLoad >= titleBgmFade.fadeInSeconds && !launching)
        {
            titleBgm.volume = max;                               // 페이드 인이 끝난 뒤에는 직접 반영
        }
    }

    private void PlayClick()
    {
        if (clickSource != null && clickSource.clip != null)
        {
            clickSource.PlayOneShot(clickSource.clip, Mathf.Clamp01(sfxPreview * cat.clickVolumeScale));
        }
    }

    // ---------------- 화면 전환 ----------------
    private void Show(CanvasGroup next)
    {
        if (busy || launching || next == current)
            return;
        StartCoroutine(SwitchRoutine(next));
    }

    private IEnumerator SwitchRoutine(CanvasGroup next)
    {
        busy = true;
        CanvasGroup prev = current;
        current = next;

        if (next == characterPanel) OnCharacterOpen();
        else if (next == stagePanel) OnStageOpen();
        else if (next == settingsPanel) OnSettingsOpen();
        else if (next == gemMainPanel) OnGemMainOpen();
        else if (next == gemStoragePanel) RefreshGemStorage();
        else if (next == gemCombinePanel) OnGemCombineOpen();
        else if (next == gemRerollPanel) OnGemRerollOpen();
        else if (next == gemDecomposePanel) OnGemDecomposeOpen();

        prev.blocksRaycasts = false;
        next.gameObject.SetActive(true);
        next.blocksRaycasts = false;
        RectTransform nextRect = (RectTransform)next.transform;
        float fromBackdrop = backdrop.alpha;
        float toBackdrop = next == mainPanel ? 0f : 1f;

        float t = 0f;
        while (t < cat.panelFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / cat.panelFadeTime));
            prev.alpha = 1f - k;
            next.alpha = k;
            nextRect.anchoredPosition = new Vector2(0f, -cat.panelSlide * (1f - k));
            backdrop.alpha = Mathf.Lerp(fromBackdrop, toBackdrop, k);
            yield return null;
        }

        prev.alpha = 0f;
        prev.gameObject.SetActive(false);
        next.alpha = 1f;
        nextRect.anchoredPosition = Vector2.zero;
        next.blocksRaycasts = true;
        backdrop.alpha = toBackdrop;
        busy = false;
    }

    // 출발: 선택 전달 → 화면·배경음이 서서히 꺼진 뒤 스테이지 씬 불러오기
    private IEnumerator LaunchRoutine(int playerId, int stageIndex, string sceneName)
    {
        launching = true;
        rootGroup.blocksRaycasts = false;
        GameLaunch.Select(playerId, stageIndex, sceneName);

        fadeOverlay.gameObject.SetActive(true);
        float startVolume = titleBgm != null ? titleBgm.volume : 0f;
        if (titleBgmFade != null) titleBgmFade.enabled = false;
        float t = 0f;
        while (t < cat.launchFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / cat.launchFadeTime);
            fadeOverlay.color = new Color(0f, 0f, 0f, k);
            if (titleBgm != null) titleBgm.volume = Mathf.Lerp(startVolume, 0f, k);
            yield return null;
        }
        loadingText.gameObject.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }
    }

    // ---------------- 캔버스와 메인 메뉴 ----------------
    private void BuildCanvas()
    {
        GameObject go = new GameObject("Title Menu Canvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = cat.sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = cat.referenceResolution;
        scaler.matchWidthOrHeight = cat.matchWidthOrHeight;
        go.AddComponent<GraphicRaycaster>();
        root = (RectTransform)go.transform;
        rootGroup = go.AddComponent<CanvasGroup>();

        // 하위 화면 공통 바탕: 어두운 목재 + 장식 테두리
        RectTransform back = Stretch(NewRect("Backdrop", root));
        backdrop = back.gameObject.AddComponent<CanvasGroup>();
        backdrop.blocksRaycasts = false;
        Image dark = back.gameObject.AddComponent<Image>();
        dark.color = cat.backdropColor;
        Image frame = Frame("Screen Frame", back, cat.screenFrame, Vector4.zero, cat.screenFrameTint, Center, Vector2.zero, Vector2.zero);
        Stretch(frame.rectTransform, cat.screenFrameInset);
        frame.pixelsPerUnitMultiplier = cat.frameScale;
        frame.raycastTarget = false;
        dark.raycastTarget = false;

        // 출발할 때 덮는 검은 화면 (맨 위)
        GameObject fadeGo = new GameObject("Fade", typeof(RectTransform));
        fadeGo.transform.SetParent(root, false);
        fadeOverlay = fadeGo.AddComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        Stretch((RectTransform)fadeGo.transform);
        loadingText = Label("Loading", fadeGo.transform, "불러오는 중...", 32, cat.lightTextColor, TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(-220f, 70f), new Vector2(400f, 60f), true);
        loadingText.gameObject.SetActive(false);
        fadeGo.SetActive(false);
    }

    private CanvasGroup BuildMainMenu()
    {
        RectTransform panel = NewPanel("Main Menu");

        Image logo = Icon("Logo", panel, cat.logo, Center, cat.logoPosition, cat.logoSize);
        logo.preserveAspect = true;

        // 고지 배너 (아래 가운데)
        Image notice = Frame("Notice", panel, cat.noticeBanner, Vector4.zero, Color.white, new Vector2(0.5f, 0f), cat.noticePosition, cat.noticeSize);
        Text noticeText = Label("Text", notice.transform, cat.noticeText, cat.noticeFontSize, cat.lightTextColor, TextAnchor.MiddleCenter,
            Center, Vector2.zero, cat.noticeSize, true);
        noticeText.horizontalOverflow = HorizontalWrapMode.Overflow;

        string[] labels = { "플레이", "젬 관리", "설정", "나가기" };
        UnityAction[] actions =
        {
            () => Show(characterPanel),
            () => Show(gemMainPanel),
            () => Show(settingsPanel),
            QuitGame,
        };
        for (int i = 0; i < labels.Length; i++)
        {
            bool primary = i == 0;
            MakeButton(labels[i], panel, labels[i], cat.menuFontSize, primary,
                Center, cat.menuFirstButtonPosition + new Vector2(0f, -cat.menuButtonSpacing * i), cat.menuButtonSize, actions[i]);
        }
        return panel.GetComponent<CanvasGroup>();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- 공통 부품 ----------------
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    // 화면 하나 (전체를 덮는 빈 판 + CanvasGroup). 출발 검은 화면보다 아래에 둠
    private RectTransform NewPanel(string name)
    {
        RectTransform panel = Stretch(NewRect(name, root));
        panel.SetSiblingIndex(fadeOverlay.transform.GetSiblingIndex());
        panel.gameObject.AddComponent<CanvasGroup>();
        return panel;
    }

    // 하위 화면의 타이틀 배너 (Panel_2 잎 장식 제목판, 그림 비율 그대로: 높이 = headerSize.y)
    private void Header(RectTransform panel, string title)
    {
        Sprite s = cat.headerPlate;
        float h = cat.headerSize.y;
        Vector2 size = s != null ? new Vector2(h * s.rect.width / s.rect.height, h) : cat.headerSize;
        Image plate = Icon("Header", panel, s, new Vector2(0.5f, 1f), new Vector2(0f, -cat.headerTop), size);
        Label("Title", plate.transform, title, cat.headerFontSize, cat.darkTextColor, TextAnchor.MiddleCenter,
            Center, cat.headerTextOffset, new Vector2(size.x * 0.7f, h), false);
    }

    // 하위 화면 아래쪽 버튼 (왼쪽 = 돌아가기, 오른쪽 = 진행)
    private Button BottomButton(RectTransform panel, string label, bool right, bool primary, Sprite icon, UnityAction onClick)
    {
        Vector2 anchor = right ? new Vector2(1f, 0f) : Vector2.zero;
        Vector2 pos = new Vector2(right ? -cat.bottomButtonMargin.x : cat.bottomButtonMargin.x, cat.bottomButtonMargin.y);
        return MakeButton(label, panel, label, cat.bottomButtonFontSize, primary, anchor, pos, cat.bottomButtonSize, onClick, icon);
    }

    // 게임 에셋 버튼 (초록 = 강조, 갈색 = 보통). 누른 그림 교체 대신 색·크기·금테로 반응을 구분
    private Button MakeButton(string name, Transform parent, string label, int fontSize, bool primary,
        Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, Sprite icon = null)
    {
        Sprite sprite = primary ? cat.buttonGreen : cat.buttonBrown;
        Image image = Frame(name, parent, sprite, cat.objectPanelBorder, Color.white, anchor, pos, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = cat.buttonNormalTint;
        colors.highlightedColor = cat.buttonHoverTint;
        colors.selectedColor = cat.buttonNormalTint;
        colors.pressedColor = cat.buttonPressedTint;
        colors.disabledColor = cat.buttonDisabledTint;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Outline gold = GoldFrame(image.gameObject);
        MenuButtonFx fx = image.gameObject.AddComponent<MenuButtonFx>();
        fx.target = button;
        fx.frame = gold;
        fx.frameColor = cat.goldFrameColor;
        fx.frameHoverColor = cat.goldFrameHoverColor;

        Text text = Label("Label", image.transform, label, fontSize, primary ? Color.white : cat.lightTextColor, TextAnchor.MiddleCenter,
            Center, Vector2.zero, size, true);
        Stretch(text.rectTransform);

        // 아이콘 + 글자: 글자 너비만큼 묶어서 가운데 정렬
        if (icon != null)
        {
            float iconSize = size.y * cat.buttonIconSize;
            float gap = iconSize * 0.3f;
            float textWidth = text.preferredWidth;
            float total = iconSize + gap + textWidth;
            Image iconImage = Icon("Icon", image.transform, icon, Center, new Vector2(-total * 0.5f + iconSize * 0.5f, 0f), Vector2.one * iconSize);
            iconImage.name = "Icon";
            text.rectTransform.offsetMin = new Vector2(iconSize + gap, 0f);
            text.rectTransform.offsetMax = Vector2.zero;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Center;
            text.rectTransform.sizeDelta = new Vector2(textWidth + 4f, size.y);
            text.rectTransform.anchoredPosition = new Vector2(total * 0.5f - textWidth * 0.5f, 0f);
        }

        button.onClick.AddListener(PlayClick);
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }
        return button;
    }

    private Outline GoldFrame(GameObject go)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = cat.goldFrameColor;
        outline.effectDistance = new Vector2(cat.goldFrameWidth, -cat.goldFrameWidth);
        outline.useGraphicAlpha = true;
        return outline;
    }

    // 9-slice 판넬. 테두리 정보가 없는 그림은 border로 잘라 씀
    private Image Frame(string name, Transform parent, Sprite sprite, Vector4 border, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rect = NewRect(name, parent, anchor, pos, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        Sprite s = Sliced(sprite, border);
        if (s != null)
        {
            image.sprite = s;
            image.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = cat.pixelScale;
            image.color = color;
        }
        else
        {
            image.color = new Color(color.r * 0.5f, color.g * 0.4f, color.b * 0.3f, color.a);   // 스프라이트가 없을 때 갈색 판
        }
        return image;
    }

    private static Sprite Sliced(Sprite sprite, Vector4 border)
    {
        if (sprite == null)
            return null;
        return sprite.border == Vector4.zero && border != Vector4.zero ? RunUI.Sliced(sprite, border) : sprite;
    }

    private Image Icon(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Image image = NewRect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private Text Label(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment,
        Vector2 anchor, Vector2 pos, Vector2 size, bool outline)
    {
        Text text = NewRect(name, parent, anchor, pos, size).gameObject.AddComponent<Text>();
        text.font = RunUI.Font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.1f;
        text.raycastTarget = false;
        if (outline)
        {
            Outline o = text.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.08f, 0.04f, 0.02f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Center;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Center;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    // 잠금 아이콘 (에셋이 없을 때 코드로 그린 픽셀 자물쇠)
    private static Sprite pixelLock;
    private Sprite LockSprite
    {
        get
        {
            if (cat.lockIcon != null) return cat.lockIcon;
            if (pixelLock != null) return pixelLock;
            string[] rows =
            {
                "...####...",
                "..#....#..",
                ".#......#.",
                ".#......#.",
                ".#......#.",
                "##########",
                "#oooooooo#",
                "#ooo##ooo#",
                "#ooo##ooo#",
                "#oooo#ooo#",
                "#oooooooo#",
                "##########",
            };
            int w = rows[0].Length, h = rows.Length;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color edge = new Color(0.1f, 0.09f, 0.08f, 1f), body = new Color(0.62f, 0.6f, 0.58f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x];
                    tex.SetPixel(x, y, c == '#' ? edge : c == 'o' ? body : Color.clear);
                }
            tex.Apply();
            pixelLock = Sprite.Create(tex, new Rect(0, 0, w, h), Center, 10f);
            return pixelLock;
        }
    }
}
