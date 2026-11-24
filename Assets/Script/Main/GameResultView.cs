using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임 결과 화면 (1920x1080, 실행 중 생성)
// 1) 어두운 반투명 배경 위로 Quest Clear / Quest Failed 아이콘이 스르륵 나타나고, 아래 판에 '결과 보기'
// 2) '결과 보기'를 누르면 결과 패널: 생존 시간·버틴 밤·처치 수·보스 처치, 획득한 젬, 특수 젬 드롭(얻으면 강조), 다시하기·나가기
// 사망·포기하기(옵션 해골 버튼 = 즉사)·클리어 모두 이 화면으로 온다.
// 기존 결과 화면(GameResult)은 흐름(옵션 버튼 숨김 등)을 위해 켜 두고 보이지 않게만 한다.
public class GameResultView : MonoBehaviour
{
    private static GameResultView instance;
    private static float runTime;
    private static bool counting;
    private static bool shown;                                   // 한 판에 한 번만 (사망 처리가 겹쳐도 보상·화면이 두 번 나오지 않게)

    // 게임 시작 (DayNightCycle.StartRun)
    public static void BeginRun()
    {
        runTime = 0f;
        counting = true;
        shown = false;
        if (instance == null)
        {
            instance = new GameObject("Game Result View").AddComponent<GameResultView>();
        }
    }

    private void Update()
    {
        if (counting && GameManager.instance != null && GameManager.instance.isLive)
        {
            runTime += Time.deltaTime;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 게임 끝 (GameManager): 보상 지급 후 결과 화면
    public static void Show(bool cleared)
    {
        if (shown) return;
        shown = true;
        counting = false;
        GameManager gm = GameManager.instance;
        int nights = Nights;
        List<int> gems = GemSave.GrantRunReward(cleared, nights, out GemSave.SpecialGem special);

        if (gm != null && gm.uiResult != null)
        {
            CanvasGroup old = gm.uiResult.GetComponent<CanvasGroup>();
            if (old == null) old = gm.uiResult.gameObject.AddComponent<CanvasGroup>();
            old.alpha = 0f;                                          // 기존 결과 화면은 보이지 않게 (켜진 상태는 유지)
            old.blocksRaycasts = false;
        }
        if (instance == null) instance = new GameObject("Game Result View").AddComponent<GameResultView>();
        instance.Build(cleared, gm != null ? gm.kill : 0, nights, gems, special);
    }

    private static int Nights => DayNightCycle.Instance != null ? DayNightCycle.Instance.NightsSurvived : 0;

    // ---------------- 화면 ----------------
    private GemCatalog cat;
    private RectTransform root;
    private CanvasGroup introGroup;
    private CanvasGroup panelGroup;

    private void Build(bool cleared, int kills, int nights, List<int> gems, GemSave.SpecialGem special)
    {
        cat = GemCatalog.Instance;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = Rect("Root", transform, Vector2.zero, Vector2.zero);
        Stretch(root);
        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, cat.resultDimAlpha);

        BuildIntro(cleared);
        BuildPanel(cleared, kills, nights, gems, special);
        panelGroup.gameObject.SetActive(false);
        StartCoroutine(FadeIn(introGroup, cat.resultIntroTime, 0f, false));   // 제자리에서 서서히 나타남
    }

    private void BuildIntro(bool cleared)
    {
        RectTransform r = Rect("Intro", root, Vector2.zero, Vector2.zero);
        Stretch(r);
        introGroup = r.gameObject.AddComponent<CanvasGroup>();
        Sprite icon = cleared ? cat.questClear : cat.questFailed;
        float h = cleared ? cat.resultIconHeight : cat.resultFailedIconHeight;
        Vector2 size = icon != null ? new Vector2(h * icon.rect.width / icon.rect.height, h) : new Vector2(h, h);
        Image img = Img("Title", r, icon, cat.resultIconPosition, size);
        img.preserveAspect = true;
        if (icon == null) Label("Title", r, cleared ? "QUEST CLEAR" : "QUEST FAILED", 80, Color.yellow, cat.resultIconPosition, size);

        Button bar = MakeButton("Result Button", r, cat.buttonBrown != null ? cat.buttonBrown : cat.resultBar, "결과 보기", cat.resultBarFontSize, cat.resultBarPosition, cat.resultBarSize, OpenPanel);
        bar.GetComponentInChildren<Text>().color = new Color(0.97f, 0.91f, 0.79f, 1f);
    }

    private void OpenPanel()
    {
        RunUI.ClickSound();
        StartCoroutine(SwapToPanel());
    }

    private IEnumerator SwapToPanel()
    {
        introGroup.blocksRaycasts = false;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            introGroup.alpha = 1f - t / 0.2f;
            yield return null;
        }
        introGroup.gameObject.SetActive(false);
        panelGroup.gameObject.SetActive(true);
        yield return FadeIn(panelGroup, 0.3f, 30f, true);
    }

    private IEnumerator FadeIn(CanvasGroup g, float time, float rise, bool zoom)
    {
        RectTransform r = (RectTransform)g.transform;
        g.alpha = 0f;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            g.alpha = k;
            r.anchoredPosition = new Vector2(0f, -rise * (1f - k));
            r.localScale = Vector3.one * (zoom ? Mathf.Lerp(0.92f, 1f, k) : 1f);
            yield return null;
        }
        g.alpha = 1f;
        r.anchoredPosition = Vector2.zero;
        r.localScale = Vector3.one;
    }

    private void BuildPanel(bool cleared, int kills, int nights, List<int> gems, GemSave.SpecialGem special)
    {
        RectTransform r = Rect("Result Panel", root, Vector2.zero, Vector2.zero);
        Stretch(r);
        panelGroup = r.gameObject.AddComponent<CanvasGroup>();
        Color dark = new Color(0.27f, 0.15f, 0.07f, 1f);
        Sprite plain = cat.plainPanel != null ? cat.plainPanel : cat.paperPanel;

        Frame("Board", r, cat.woodPanel, new Vector2(0f, -10f), new Vector2(1500f, 900f), new Color(0.93f, 0.85f, 0.72f, 1f));
        Banner("Header", r, new Vector2(0f, 400f), 92f, "게임 결과", 40, dark);

        // 결과 띠
        Image res = Frame("Result", r, cleared ? cat.buttonGreen : cat.buttonGrey, new Vector2(0f, 270f), new Vector2(560f, 110f), cleared ? cat.survivalColor : Color.gray);
        Label("Text", res.transform, cleared ? "클리어!" : "실패", 54, Color.white, Vector2.zero, new Vector2(520f, 100f), true);

        // 기록 (띠 없는 판, 4줄을 판 안쪽 가운데에)
        Image stats = Frame("Stats", r, plain, new Vector2(-330f, 65f), new Vector2(640f, 250f), new Color(0.97f, 0.9f, 0.8f, 1f));
        int sec = Mathf.FloorToInt(runTime);
        string[] names = { "생존 시간", "버틴 밤", "처치 수", "보스 처치" };
        string[] values = { (sec / 60) + "분 " + (sec % 60).ToString("00") + "초", nights + "회", kills.ToString("N0"), cleared ? "처치" : "실패" };
        for (int i = 0; i < names.Length; i++)
        {
            float y = 81f - i * 54f;
            Text n = Label("Name " + i, stats.transform, names[i], 30, dark, new Vector2(-135f, y), new Vector2(270f, 52f));
            n.alignment = TextAnchor.MiddleLeft;
            Text v = Label("Value " + i, stats.transform, values[i], 30, dark, new Vector2(135f, y), new Vector2(270f, 52f));
            v.alignment = TextAnchor.MiddleRight;
            if (i == 3) v.color = cleared ? cat.survivalColor : cat.attackColor;
        }

        // 획득한 특수 젬: 제목은 판 위 띠 안, 얻으면 그림·이름·두 줄(증가 초록 / 감소 빨강)
        Image box = Frame("Special", r, cat.paperPanel, new Vector2(370f, 65f), new Vector2(620f, 250f), new Color(0.97f, 0.9f, 0.8f, 1f));
        float bandY = 125f - cat.paperBandCenter;
        Text boxTitle = Label("Title", box.transform, special != null ? "특수 젬 획득!" : "획득 특수 젬", 30, special != null ? cat.specialColor : dark, new Vector2(0f, bandY), new Vector2(400f, 50f));
        if (special != null)
        {
            Image big = Img("Icon", box.transform, cat.SpecialIcon(GemSave.SpecialCategory(special)), new Vector2(-200f, -28f), new Vector2(150f, 150f));
            UiFxLoop.Attach(big.rectTransform, Vector2.zero, 150f);
            pulse = big.rectTransform;
            Text name = Label("Name", box.transform, GemSave.SpecialName(special), 28, dark, new Vector2(80f, 28f), new Vector2(380f, 44f));
            name.alignment = TextAnchor.MiddleLeft;
            GemCatalog.SpecialOption up = cat.UpOption(special.up), down = cat.DownOption(special.down);
            StatCatalog sc = StatCatalog.Instance;
            Text upLine = Label("Up", box.transform, "▲ " + sc.Name(special.up) + " " + cat.OptionValue(up, special.upValue), 26, cat.upColor, new Vector2(80f, -18f), new Vector2(380f, 40f));
            upLine.alignment = TextAnchor.MiddleLeft;
            Text downLine = Label("Down", box.transform, "▼ " + sc.Name(special.down) + " " + cat.OptionValue(down, special.downValue), 26, cat.downColor, new Vector2(80f, -60f), new Vector2(380f, 40f));
            downLine.alignment = TextAnchor.MiddleLeft;
        }
        else
        {
            Label("None", box.transform, "이번에는 특수 젬이 없습니다", 26, new Color(0.45f, 0.36f, 0.28f, 1f), new Vector2(0f, -30f), new Vector2(500f, 60f));
        }

        // 획득한 젬
        Image gemBox = Frame("Gems", r, plain, new Vector2(0f, -210f), new Vector2(1280f, 170f), new Color(0.97f, 0.9f, 0.8f, 1f));
        Image gemHead = Banner("Head", gemBox.transform, new Vector2(0f, 85f), 70f, "획득한 젬", 28, dark);
        gemHead.rectTransform.anchoredPosition = new Vector2(-640f + gemHead.rectTransform.sizeDelta.x * 0.5f + 30f, 85f);   // 판 왼쪽 위 모서리에 걸침
        int count = gems.Count + (special != null ? 1 : 0);          // 특수 젬을 얻었으면 마지막 칸
        float cell = 110f, gap = 18f;
        float start = -(count - 1) * (cell + gap) * 0.5f;
        for (int i = 0; i < gems.Count; i++)
        {
            GemCatalog.GemDef d = cat.gems[gems[i]];
            Vector2 p = new Vector2(start + i * (cell + gap), -10f);
            Frame("Slot " + i, gemBox.transform, cat.SlotFrame(d.category), p, Vector2.one * cell, cat.CategoryColor(d.category));
            Img("Gem " + i, gemBox.transform, d.icon, p, Vector2.one * cell * 0.82f);
        }
        if (special != null)
        {
            Vector2 sp = new Vector2(start + gems.Count * (cell + gap), -10f);
            Image sg = Img("Special Gem", gemBox.transform, cat.SpecialIcon(GemSave.SpecialCategory(special)), sp, Vector2.one * cell * 1.15f);
            UiFxLoop.Attach(sg.rectTransform, Vector2.zero, cell);
        }
        if (gems.Count == 0 && special == null) Label("None", gemBox.transform, "획득한 젬이 없습니다", 28, dark, new Vector2(0f, -10f), new Vector2(500f, 60f));

        // 버튼
        MakeButton("Retry", r, cat.buttonBrown, "다시하기", 38, new Vector2(-260f, -352f), new Vector2(420f, 90f), Retry);
        MakeButton("Exit", r, cat.buttonGreen, "나가기", 38, new Vector2(260f, -352f), new Vector2(420f, 90f), ExitToTitle);
    }

    // 특수 젬 그림이 은은하게 커졌다 작아짐 (강조)
    private RectTransform pulse;

    private void LateUpdate()
    {
        if (pulse != null) pulse.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    // 다시하기 / 나가기는 바로 씬이 바뀌므로 클릭 소리를 내지 않음
    private void Retry()
    {
        GameLaunch.QuitToCharacterSelect();                          // 타이틀의 캐릭터 선택 화면(마지막 캐릭터)으로
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuCatalog.Instance.titleSceneName);
    }

    // 발바닥 띠 제목: 그림 비율 그대로 (발바닥이 눌리지 않게)
    private Image Banner(string name, Transform parent, Vector2 pos, float height, string text, int fontSize, Color color)
    {
        Sprite s = cat.banner;
        float w = s != null ? height * s.rect.width / s.rect.height : height * 4f;
        Image image = Img(name, parent, s, pos, new Vector2(w, height));
        Label("Text", image.transform, text, fontSize, color, cat.bannerTextOffset, new Vector2(w * 0.6f, height));
        return image;
    }

    private void ExitToTitle()
    {
        GameLaunch.Clear();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuCatalog.Instance.titleSceneName);
    }

    // ---------------- 부품 ----------------
    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static Image Frame(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color fallback)
    {
        Image image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else image.color = fallback;
        return image;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Image image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private static Text Label(string name, Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, bool outline = false)
    {
        Text t = Rect(name, parent, pos, box).gameObject.AddComponent<Text>();
        t.font = RunUI.Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            SoftOutline o = t.gameObject.AddComponent<SoftOutline>();   // 8방향 불투명 외곽선 (흐림·뾰족함 없음)
            o.color = new Color(0.1f, 0.05f, 0.02f, 1f);
            o.distance = 2f;
        }
        return t;
    }

    private static Button MakeButton(string name, Transform parent, Sprite sprite, string label, int fontSize, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Image image = Frame(name, parent, sprite, pos, size, new Color(0.45f, 0.3f, 0.2f, 1f));
        image.raycastTarget = true;
        Button b = image.gameObject.AddComponent<Button>();
        b.targetGraphic = image;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = b.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.66f, 0.6f, 1f);
        colors.selectedColor = colors.normalColor;
        b.colors = colors;
        b.onClick.AddListener(onClick);
        Label("Label", image.transform, label, fontSize, Color.white, Vector2.zero, size, true);
        return b;
    }
}
