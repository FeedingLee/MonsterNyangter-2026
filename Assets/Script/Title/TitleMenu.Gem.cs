using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 강화(젬) 화면: 젬 관리 메인 / 보관함(일반·특수) / 합성(일반 1+1, 특수 x4)
// 저장은 GemSave(JSON), 수치·그림은 Resources/GemCatalog. 재설정·분해 화면은 TitleMenu.GemSpecial
public partial class TitleMenu
{
    private CanvasGroup gemMainPanel, gemStoragePanel, gemCombinePanel;
    private GemCatalog gc;

    private class GemSlotView
    {
        public Image frame, gem, lockIcon;
        public Text note;
    }

    private class GemCellView
    {
        public int gem;
        public RectTransform rect;
        public Image frame, icon, badge;
        public Text count;
    }

    private static readonly Color PaperText = new Color(0.27f, 0.15f, 0.07f, 1f);
    private static readonly Color MutedText = new Color(0.45f, 0.36f, 0.28f, 1f);

    // ---------------- 공통 ----------------
    private Image GemFrame(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color fallback)
    {
        Image image = Frame(name, parent, sprite, Vector4.zero, Color.white, Center, pos, size);
        if (sprite == null) image.color = fallback;
        return image;
    }

    private Text GemLabel(string name, Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, bool outline = false)
    {
        Text t = Label(name, parent, text, size, color, TextAnchor.MiddleCenter, Center, pos, box, outline);
        return t;
    }

    private Button GemButton(string name, Transform parent, string label, bool primary, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, int fontSize = 30)
    {
        Button b = MakeButton(name, parent, label, fontSize, primary, Center, pos, size, onClick);
        Sprite s = primary ? gc.buttonGreen : gc.buttonBrown;
        if (s != null) ((Image)b.targetGraphic).sprite = s;
        return b;
    }

    // 버튼 하나를 분야 색으로 (회색 버튼 그림에 색을 곱함)
    private void TintButton(Button b, Color color)
    {
        Image image = (Image)b.targetGraphic;
        if (gc.buttonGrey != null) image.sprite = gc.buttonGrey;
        image.color = color;
    }

    private Sprite PlainPaper => gc.plainPanel != null ? gc.plainPanel : gc.paperPanel;

    // 양피지 판 위 띠 안의 제목 (판 높이 기준, 띠 가운데)
    private Text BandTitle(Transform box, float boxHeight, string text, int fontSize = 30)
    {
        return GemLabel("Title", box, text, fontSize, PaperText, new Vector2(0f, boxHeight * 0.5f - gc.paperBandCenter), new Vector2(500f, 50f));
    }

    // 발바닥 띠 제목: 그림 비율 그대로 (발바닥이 눌리지 않게)
    private Image PawBanner(Transform parent, Vector2 pos, float height, string text, int fontSize)
    {
        Sprite s = gc.banner;
        float w = s != null ? height * s.rect.width / s.rect.height : height * 4f;
        Image image = Icon("Head", parent, s, Center, pos, new Vector2(w, height));
        GemLabel("Text", image.transform, text, fontSize, PaperText, gc.bannerTextOffset, new Vector2(w * 0.6f, height));
        return image;
    }

    // 한 줄에 들어가도록 글자 크기만 줄임 (줄바꿈 없음)
    private static void FitOneLine(Text t, int maxSize, int minSize, float width)
    {
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.resizeTextForBestFit = false;
        t.fontSize = maxSize;
        while (t.fontSize > minSize && t.preferredWidth > width) t.fontSize--;
    }

    private void SetDisabled(Button b, bool disabled)
    {
        b.interactable = !disabled;
        CanvasGroup g = b.GetComponent<CanvasGroup>();
        if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
        g.alpha = disabled ? 0.45f : 1f;
    }

    // 젬 한 칸 (분야 칸 + 젬 그림 + 개수 + E 표시)
    private GemCellView GemCell(Transform parent, int gem, Vector2 pos, float size, UnityEngine.Events.UnityAction onClick)
    {
        GemCatalog.GemDef d = gc.gems[gem];
        GemCellView v = new GemCellView { gem = gem };
        v.frame = GemFrame("Gem " + gem, parent, gc.SlotFrame(d.category), pos, Vector2.one * size, gc.CategoryColor(d.category));
        v.rect = v.frame.rectTransform;
        v.icon = Icon("Icon", v.rect, d.icon, Center, Vector2.zero, Vector2.one * size * 0.84f);
        v.badge = Icon("E", v.rect, gc.equippedBadge, Center, new Vector2(-size * 0.32f, size * 0.32f), Vector2.one * size * 0.3f);
        v.count = GemLabel("Count", v.rect, "", Mathf.RoundToInt(size * 0.26f), Color.white, new Vector2(size * 0.22f, -size * 0.34f), new Vector2(size * 0.7f, size * 0.34f), true);
        v.count.alignment = TextAnchor.MiddleRight;
        v.frame.raycastTarget = true;
        Button b = v.frame.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(PlayClick);
        b.onClick.AddListener(onClick);
        v.frame.gameObject.AddComponent<MenuButtonFx>().target = b;
        return v;
    }

    private void BuildGemPanels()
    {
        gc = GemCatalog.Instance;
        gemMainPanel = BuildGemMain();
        gemStoragePanel = BuildGemStorage();
        gemCombinePanel = BuildGemCombine();
        BuildSpecialPanels();
        foreach (CanvasGroup g in new[] { gemMainPanel, gemStoragePanel, gemCombinePanel })
        {
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.gameObject.SetActive(false);
        }
    }

    // ---------------- 젬 관리 메인 ----------------
    private readonly GemSlotView[] gemSlots = new GemSlotView[9];
    private readonly List<GemSlotView> specialSlotViews = new List<GemSlotView>();
    private Text specialSlotInfo;
    private Image gemShopper;
    private readonly Text[] effectTexts = new Text[3];

    private CanvasGroup BuildGemMain()
    {
        RectTransform panel = NewPanel("Gem Main");
        Header(panel, "캐릭터 강화");

        // 젬 상인 마스코트 (Gem_shopper, 오른쪽을 바라봄)
        Image card = GemFrame("Shopper Card", panel, gc.paperPanel, new Vector2(-630f, 120f), new Vector2(340f, 420f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(card.transform, 420f, "보석 전문가");
        gemShopper = Icon("Shopper", card.transform, ShopperFrame(0), Center, gc.shopperPosition, gc.shopperSize);
        gemShopper.rectTransform.localScale = new Vector3(gc.shopperFaceRight ? -1f : 1f, 1f, 1f);
        // 숨쉬기 기준점을 발끝으로 (위치는 그대로)
        gemShopper.rectTransform.pivot = new Vector2(0.5f, 0f);
        gemShopper.rectTransform.anchoredPosition = gc.shopperPosition - new Vector2(0f, gc.shopperSize.y * 0.5f);
        // 앞 테이블 (보석 전문가가 테이블 뒤에 서 있도록 나중에 그림)
        Image table = Icon("Table", card.transform, gc.shopperTable, Center, gc.shopperTablePosition, gc.shopperTableSize);
        table.preserveAspect = true;

        // 일반 젬 슬롯 (분야별 한 줄)
        Image box = GemFrame("Normal Gems", panel, gc.paperPanel, new Vector2(-60f, 120f), new Vector2(720f, 420f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(box.transform, 420f, "일반 젬");
        for (int c = 0; c < 3; c++)
        {
            StatCategory cat3 = (StatCategory)c;
            float y = 87f - c * 114f;                         // 칸 96 + 위아래 간격 18 (띠 제목 아래 ~ 상자 아래 여백 균등)
            Image tab = GemFrame("Tab " + c, box.transform, gc.Tab(cat3), new Vector2(-215f, y + 10f), new Vector2(140f, 64f), gc.CategoryColor(cat3));
            tab.preserveAspect = true;
            GemLabel("Name", box.transform, GemCatalog.CategoryName(cat3), 22, Color.white, new Vector2(-215f, y - 36f), new Vector2(150f, 30f), true);
            for (int k = 0; k < 3; k++)
            {
                int slot = c * 3 + k;
                Vector2 pos = new Vector2(-10f + k * 122f, y);
                GemSlotView v = new GemSlotView();
                v.frame = GemFrame("Slot " + slot, box.transform, gc.SlotFrame(cat3), pos, Vector2.one * 96f, gc.CategoryColor(cat3));
                v.gem = Icon("Gem", v.frame.transform, null, Center, Vector2.zero, Vector2.one * 80f);
                v.lockIcon = Icon("Lock", v.frame.transform, gc.lockBig, Center, new Vector2(0f, 8f), Vector2.one * 46f);
                v.note = GemLabel("Note", v.frame.transform, "", 16, Color.white, new Vector2(0f, -30f), new Vector2(110f, 24f), true);
                v.frame.raycastTarget = true;
                Button b = v.frame.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(PlayClick);
                b.onClick.AddListener(() => OnGemSlotClick(slot));
                v.frame.gameObject.AddComponent<MenuButtonFx>().target = b;
                gemSlots[slot] = v;
            }
        }

        // 특수 젬 슬롯 (처음 1칸, 클리어마다 1칸). 누르면 보관함 '특수' 탭
        Image sbox = GemFrame("Special Gems", panel, gc.paperPanel, new Vector2(570f, 120f), new Vector2(460f, 420f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(sbox.transform, 420f, "특수 젬");
        int specialCount = GemSave.Current.specialSlots.Length;
        for (int k = 0; k < specialCount; k++)
        {
            int slot = k;
            GemSlotView v = new GemSlotView();
            Vector2 size = new Vector2(142f, 161f);
            v.frame = GemFrame("Special " + k, sbox.transform, gc.slotSpecialFrame, new Vector2((k - (specialCount - 1) * 0.5f) * 144f, 30f), size, gc.specialColor);
            v.frame.preserveAspect = true;
            v.gem = Icon("Gem", v.frame.transform, null, Center, Vector2.zero, size);
            v.lockIcon = Icon("Lock", v.frame.transform, gc.lockBig, Center, new Vector2(0f, 12f), Vector2.one * 52f);
            v.note = GemLabel("Note", v.frame.transform, "", 17, Color.white, new Vector2(0f, -40f), new Vector2(120f, 26f), true);
            Clickable(v.frame, () => OnSpecialSlotClick(slot));
            specialSlotViews.Add(v);
        }
        specialSlotInfo = GemLabel("Info", sbox.transform, "", 22, MutedText, new Vector2(0f, -120f), new Vector2(420f, 90f));

        // 장착 효과 합계
        Image eff = GemFrame("Effects", panel, PlainPaper, new Vector2(0f, -228f), new Vector2(1500f, 160f), new Color(0.96f, 0.9f, 0.8f, 1f));
        PawBanner(eff.transform, new Vector2(-750f + 150f, 80f), 64f, "장착 효과", 26);   // 판 왼쪽 위 모서리에 걸침
        for (int c = 0; c < 3; c++)
        {
            StatCategory cat3 = (StatCategory)c;
            float x = -500f + c * 500f;
            Image tab = Icon("Tab", eff.transform, gc.Tab(cat3), Center, new Vector2(x - 170f, -10f), new Vector2(90f, 44f));
            effectTexts[c] = Label("Lines", eff.transform, "", 26, PaperText, TextAnchor.MiddleLeft, Center, new Vector2(x + 50f, -10f), new Vector2(330f, 120f), false);
        }

        // 버튼
        string[] labels = { "보관함", "합성", "재설정", "분해" };
        UnityEngine.Events.UnityAction[] actions = { () => OpenGemStorage(null, -1), () => Show(gemCombinePanel), () => Show(gemRerollPanel), () => Show(gemDecomposePanel) };
        for (int i = 0; i < labels.Length; i++)
        {
            GemButton(labels[i], panel, labels[i], false, new Vector2(-510f + i * 340f, -365f), new Vector2(300f, 76f), actions[i]);
        }
        ShardCounter(panel);
        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(mainPanel));
        return panel.GetComponent<CanvasGroup>();
    }

    private void OnGemMainOpen()
    {
        shopperClip = null;
        shopperIdleLeft = -1f;
        RefreshGemMain();
    }

    // ---------------- 보석 전문가 애니메이션 ----------------
    // 대기: 한 그림(shopperIdleFrame)에 멈춘 채 숨쉬기만 (발끝 기준으로 세로로 살짝 늘었다 줄었다)
    // shopperIdleTime초마다 동작(인사·젬 자랑·고민·반짝) 하나를 무작위로 shopperFps 속도로 한 번 재생하고 다시 대기
    private float shopperTime;
    private float shopperIdleLeft = -1f;
    private int[] shopperClip;                 // 재생 중인 동작 (null = 대기)
    private int shopperLastAction = -1;

    private Sprite ShopperFrame(int index)
    {
        Sprite[] f = gc.shopperFrames;
        return f != null && index >= 0 && index < f.Length ? f[index] : null;
    }

    private void AnimateShopper()
    {
        if (gemShopper == null || gc.shopperFrames == null || gc.shopperFrames.Length == 0)
            return;
        float dt = Time.unscaledDeltaTime;
        if (shopperIdleLeft < 0f) shopperIdleLeft = Random.Range(gc.shopperIdleTime.x, gc.shopperIdleTime.y);

        int frame = gc.shopperIdleFrame;
        if (shopperClip == null)
        {
            shopperIdleLeft -= dt;
            if (shopperIdleLeft <= 0f && gc.shopperActions.Count > 0)
            {
                int pick = Random.Range(0, gc.shopperActions.Count);
                if (gc.shopperActions.Count > 1 && pick == shopperLastAction) pick = (pick + 1) % gc.shopperActions.Count;
                shopperLastAction = pick;
                int[] frames = gc.shopperActions[pick].frames;
                shopperClip = frames != null && frames.Length > 0 ? frames : null;
                shopperTime = 0f;
                shopperIdleLeft = Random.Range(gc.shopperIdleTime.x, gc.shopperIdleTime.y);
            }
        }
        if (shopperClip != null)
        {
            shopperTime += dt;
            int step = Mathf.FloorToInt(shopperTime * gc.shopperFps);
            if (step >= shopperClip.Length) shopperClip = null;
            else frame = shopperClip[step];
        }
        if (frame >= 0 && frame < gc.shopperFrames.Length) gemShopper.sprite = gc.shopperFrames[frame];

        // 숨쉬기: 크기는 그대로 두고 아주 천천히 위아래로 (정수 픽셀로 끊어 움직여 그림이 일그러지지 않게)
        float wave = (1f - Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / gc.shopperBreathPeriod)) * 0.5f;
        float bob = Mathf.Round(wave * gc.shopperBreathPixels);
        gemShopper.rectTransform.anchoredPosition = gc.shopperPosition + new Vector2(0f, bob - gc.shopperSize.y * 0.5f);
    }

    private void RefreshGemMain()
    {
        GemSave.Data d = GemSave.Current;
        for (int i = 0; i < gemSlots.Length; i++)
        {
            GemSlotView v = gemSlots[i];
            bool open = GemSave.SlotOpen(i);
            int g = d.slots[i];
            v.gem.sprite = g >= 0 ? gc.gems[g].icon : null;
            v.gem.enabled = open && g >= 0;
            v.lockIcon.enabled = !open;
            v.note.text = open ? "" : "클리어 " + GemSave.ClearsNeeded(i) + "회";
            v.frame.color = open ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        // 특수 젬 슬롯
        int equippedSpecial = 0;
        for (int i = 0; i < specialSlotViews.Count; i++)
        {
            GemSlotView v = specialSlotViews[i];
            bool open = GemSave.SpecialSlotOpen(i);
            GemSave.SpecialGem g = GemSave.FindSpecial(d.specialSlots[i]);
            if (g != null) equippedSpecial++;
            v.gem.sprite = g != null ? gc.SpecialIconGlow(GemSave.SpecialCategory(g)) : null;   // 장착 칸만 광채 포함
            v.gem.enabled = open && g != null;
            v.lockIcon.enabled = !open;
            v.note.text = open ? "" : "클리어 " + GemSave.SpecialClearsNeeded(i) + "회";
            // 장착하면 빈 칸 그림은 숨기고(누르기는 그대로) 젬 그림이 그 자리를 대신함
            v.frame.color = g != null && open ? Color.clear : open ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }
        specialSlotInfo.text = d.specials.Count == 0 ? "합성이나 결과 보상으로 얻을 수 있습니다"
            : "장착 " + equippedSpecial + " / " + GemSave.SpecialOpenSlots + "칸 · 보유 " + d.specials.Count + "개";
        RefreshShards();

        // 장착 효과: 인게임과 같은 계산으로 합친 최종 수치 한 개 (손해가 섞이면 빨간 글자)
        Dictionary<RunStat, float> totals = GemSave.EquippedTotals();          // 일반 젬
        Dictionary<RunStat, float> specialUps = new Dictionary<RunStat, float>();   // 특수 젬 증가
        Dictionary<RunStat, List<float>> penalties = new Dictionary<RunStat, List<float>>();
        foreach (int id in d.specialSlots)
        {
            GemSave.SpecialGem g = GemSave.FindSpecial(id);
            if (g == null) continue;
            GemCatalog.SpecialOption up = gc.UpOption(g.up), down = gc.DownOption(g.down);
            if (up != null)
            {
                specialUps.TryGetValue(g.up, out float v);
                specialUps[g.up] = v + g.upValue * up.scale;
            }
            if (down != null)
            {
                if (!penalties.ContainsKey(g.down)) penalties[g.down] = new List<float>();
                penalties[g.down].Add(g.downValue * down.scale);
            }
        }
        StatCatalog sc = StatCatalog.Instance;
        string red = ColorUtility.ToHtmlStringRGB(gc.downColor);
        for (int c = 0; c < 3; c++)
        {
            List<string> lines = new List<string>();
            HashSet<RunStat> stats = new HashSet<RunStat>(totals.Keys);
            stats.UnionWith(specialUps.Keys);
            stats.UnionWith(penalties.Keys);
            foreach (RunStat stat in stats)
            {
                GemCatalog.GemDef def = gc.gems.Find(x => x.stat == stat);
                if (def == null || (int)def.category != c) continue;
                totals.TryGetValue(stat, out float gemGain);
                specialUps.TryGetValue(stat, out float specialGain);
                penalties.TryGetValue(stat, out List<float> list);
                string value = FormatNet(stat, NetEffect(stat, gemGain, specialGain, list));
                lines.Add(sc.Name(stat) + "  " + (list != null ? "<color=#" + red + ">" + value + "</color>" : value));
            }
            effectTexts[c].text = lines.Count > 0 ? string.Join("\n", lines) : "장착한 젬 없음";
            effectTexts[c].color = lines.Count > 0 ? PaperText : MutedText;
            effectTexts[c].fontSize = lines.Count > 3 ? 20 : lines.Count > 2 ? 22 : 26;
        }
    }

    // 일반 젬·특수 젬 증가·특수 젬 감소를 RunStats와 같은 규칙으로 합친 최종 변화량
    // (이득은 더하고, 감소 옵션은 하나씩 따로 곱함. 예: +18%, -15% → 1.18 x 0.85 = +0.3%)
    private static float NetEffect(RunStat stat, float gemGain, float specialGain, List<float> penalties)
    {
        StatCatalog.StatDef def = StatCatalog.Instance.Def(stat);
        StatDirection dir = def != null ? def.direction : StatDirection.Increase;
        if (dir == StatDirection.Additive)
        {
            float sum = gemGain + specialGain;
            if (penalties != null) foreach (float p in penalties) sum += p;
            return sum;
        }
        float floor = StatCatalog.Instance.minimumScale;
        // 쿨타임처럼 줄수록 좋은 능력치: 출처(일반 젬 / 특수 젬)끼리 곱함
        float result = dir == StatDirection.Decrease
            ? Mathf.Max(floor, 1f + gemGain) * Mathf.Max(floor, 1f + specialGain)
            : 1f + gemGain + specialGain;
        if (penalties != null) foreach (float p in penalties) result *= Mathf.Max(floor, 1f + p);
        return Mathf.Max(floor, result) - 1f;
    }

    // 퍼센트는 소수 첫째 자리까지 (정수면 정수로)
    private static string FormatNet(RunStat stat, float value)
    {
        StatCatalog.StatDef def = StatCatalog.Instance.Def(stat);
        if (def != null && def.unit != StatUnit.Percent) return StatCatalog.Instance.FormatDelta(stat, value);
        float pct = Mathf.Round(value * 1000f) / 10f;
        string text = Mathf.Approximately(pct, Mathf.Round(pct)) ? Mathf.RoundToInt(pct).ToString() : pct.ToString("0.0");
        return (pct >= 0f ? "+" : "") + text + "%";
    }

    // 특수 젬 칸을 누르면 보관함 '특수' 탭 (장착하면 그 칸에)
    private void OnSpecialSlotClick(int slot)
    {
        if (!GemSave.SpecialSlotOpen(slot))
            return;
        storageTargetSlot = -1;
        storageSpecialTarget = slot;
        storageTab = 4;
        storageSelected = -1;
        storageSpecial = GemSave.Current.specialSlots[slot];
        Show(gemStoragePanel);
    }

    // 슬롯을 누르면 그 분야 보관함 (장착하면 그 칸에)
    private void OnGemSlotClick(int slot)
    {
        if (!GemSave.SlotOpen(slot))
            return;
        OpenGemStorage(GemSave.SlotCategory(slot), slot);
    }

    // ---------------- 보관함 ----------------
    private readonly List<GemCellView> storageCells = new List<GemCellView>();
    private readonly Button[] storageTabs = new Button[5];
    private int storageTab;                 // 0 전체, 1 공격, 2 생존, 3 유틸, 4 특수
    private int storageSelected = -1;
    private int storageTargetSlot = -1;
    private int storageSpecial = -1;          // 특수 탭에서 고른 특수 젬 id
    private int storageSpecialTarget = -1;    // 특수 젬 칸을 눌러 들어왔으면 그 칸
    private RectTransform specialCards, gemInfoLockHolder;
    private Text storageEmpty;
    private Image gemInfoIcon, gemInfoTag;
    private Text gemInfoName, gemInfoTagText, gemInfoEffect, gemInfoCount;
    private Button equipButton, unequipButton, unequipAllButton;

    private CanvasGroup BuildGemStorage()
    {
        RectTransform panel = NewPanel("Gem Storage");
        Header(panel, "젬 보관함");

        string[] tabs = { "전체", "공격", "생존", "유틸", "특수" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            storageTabs[i] = GemButton("Tab " + i, panel, tabs[i], false, new Vector2(-680f + i * 190f, 330f), new Vector2(176f, 70f), () => SelectStorageTab(index), 28);
        }

        // 전체 해제 (장착한 젬을 한 번에 모두 해제)
        unequipAllButton = GemButton("Unequip All", panel, "전체 해제", false, new Vector2(560f, 330f), new Vector2(300f, 70f), UnequipAll, 28);

        Image grid = GemFrame("Grid", panel, PlainPaper, new Vector2(-220f, -40f), new Vector2(1000f, 620f), new Color(0.96f, 0.9f, 0.8f, 1f));
        for (int i = 0; i < gc.gems.Count; i++)
        {
            int gem = i;
            storageCells.Add(GemCell(grid.transform, i, Vector2.zero, 128f, () => SelectStorageGem(gem)));
        }
        specialCards = ScrollContent(grid.transform, new Vector2(0f, -5f), new Vector2(960f, 590f));   // 특수 탭: 두 줄 카드 목록
        storageEmpty = GemLabel("Empty", grid.transform, "", 30, MutedText, Vector2.zero, new Vector2(500f, 60f));

        // 선택한 젬: 제목은 위 띠 안, 그림 → 이름(한 줄) → 분야 → 효과 → 개수 → 버튼
        Image info = GemFrame("Info", panel, gc.paperPanel, new Vector2(560f, -40f), new Vector2(460f, 620f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(info.transform, 620f, "선택한 젬");
        gemInfoIcon = Icon("Icon", info.transform, null, Center, new Vector2(0f, 170f), new Vector2(120f, 120f));
        gemInfoName = GemLabel("Name", info.transform, "", 30, PaperText, new Vector2(0f, 86f), new Vector2(400f, 46f));
        gemInfoTag = GemFrame("Tag", info.transform, gc.buttonGrey, new Vector2(0f, 38f), new Vector2(150f, 44f), Color.gray);
        gemInfoTagText = GemLabel("Text", gemInfoTag.transform, "", 22, Color.white, Vector2.zero, new Vector2(140f, 40f), true);
        gemInfoEffect = GemLabel("Effect", info.transform, "", 32, PaperText, new Vector2(0f, -22f), new Vector2(420f, 60f));
        gemInfoCount = GemLabel("Count", info.transform, "", 24, MutedText, new Vector2(0f, -76f), new Vector2(420f, 40f));
        equipButton = GemButton("Equip", info.transform, "장착", true, new Vector2(0f, -160f), new Vector2(360f, 76f), EquipSelected);
        unequipButton = GemButton("Unequip", info.transform, "해제", false, new Vector2(0f, -248f), new Vector2(360f, 76f), UnequipSelected);
        gemInfoLockHolder = NewRect("Lock Holder", info.transform, Center, new Vector2(165f, 170f), new Vector2(70f, 70f));   // 특수 젬 잠금 버튼 자리

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(gemMainPanel));
        return panel.GetComponent<CanvasGroup>();
    }

    private void OpenGemStorage(StatCategory? category, int targetSlot)
    {
        storageTargetSlot = targetSlot;
        storageSpecialTarget = -1;
        storageSpecial = -1;
        storageTab = category == null ? 0 : (int)category.Value + 1;
        storageSelected = -1;
        if (targetSlot >= 0 && GemSave.Current.slots[targetSlot] >= 0) storageSelected = GemSave.Current.slots[targetSlot];
        Show(gemStoragePanel);
    }

    private void SelectStorageTab(int tab)
    {
        storageTab = tab;
        storageTargetSlot = -1;
        storageSpecialTarget = -1;
        RefreshGemStorage();
    }

    private void SelectStorageGem(int gem)
    {
        storageSelected = gem;
        RefreshGemStorage();
    }

    private void EquipSelected()
    {
        if (storageTab == 4)
        {
            if (GemSave.EquipSpecial(storageSpecial, storageSpecialTarget)) storageSpecialTarget = -1;
            RefreshGemStorage();
            return;
        }
        if (storageSelected < 0) return;
        if (GemSave.Equip(storageSelected, storageTargetSlot)) storageTargetSlot = -1;
        RefreshGemStorage();
    }

    private void UnequipSelected()
    {
        if (storageTab == 4)
        {
            GemSave.UnequipSpecial(storageSpecial);
            RefreshGemStorage();
            return;
        }
        if (storageSelected < 0) return;
        if (storageTargetSlot >= 0 && GemSave.Current.slots[storageTargetSlot] == storageSelected) GemSave.UnequipSlot(storageTargetSlot);
        else GemSave.Unequip(storageSelected);
        RefreshGemStorage();
    }

    private void UnequipAll()
    {
        GemSave.UnequipAll();
        storageTargetSlot = -1;
        storageSpecialTarget = -1;
        RefreshGemStorage();
    }

    private void RefreshGemStorage()
    {
        bool anyEquipped = false;
        foreach (int g in GemSave.Current.slots) if (g >= 0) anyEquipped = true;
        foreach (int id in GemSave.Current.specialSlots) if (id >= 0) anyEquipped = true;
        SetDisabled(unequipAllButton, !anyEquipped);

        for (int i = 0; i < storageTabs.Length; i++)
        {
            Image img = (Image)storageTabs[i].targetGraphic;
            img.sprite = i == storageTab ? gc.buttonGreen : gc.buttonBrown;
            img.color = Color.white;
            if (i >= 1 && i <= 3 && i != storageTab) TintButton(storageTabs[i], Color.Lerp(gc.CategoryColor((StatCategory)(i - 1)), Color.white, 0.15f));
            if (i == 4 && i != storageTab) TintButton(storageTabs[i], gc.specialColor);
        }

        // 특수 탭: 두 줄 카드 목록 + 특수 젬 정보
        bool specialTab = storageTab == 4;
        specialCards.parent.gameObject.SetActive(specialTab);
        ClearChildren(gemInfoLockHolder);
        gemInfoEffect.fontSize = 32;
        if (specialTab)
        {
            foreach (GemCellView v in storageCells) v.rect.gameObject.SetActive(false);
            FillSpecialCards();
            storageEmpty.gameObject.SetActive(GemSave.Current.specials.Count == 0);
            storageEmpty.text = "보유한 특수 젬이 없습니다";
            RefreshSpecialInfo();
            return;
        }

        // 보유한 젬만, 6칸씩 줄 맞춰
        int shown = 0;
        float cell = 128f, gap = 22f;
        foreach (GemCellView v in storageCells)
        {
            GemCatalog.GemDef d = gc.gems[v.gem];
            bool visible = storageTab != 4 && GemSave.Owned(v.gem) > 0 && (storageTab == 0 || (int)d.category == storageTab - 1);
            v.rect.gameObject.SetActive(visible);
            if (!visible) continue;
            int r = shown / 6, c = shown % 6;
            v.rect.anchoredPosition = new Vector2(-2.5f * (cell + gap) + c * (cell + gap), 220f - r * (cell + gap));
            v.count.text = "x" + GemSave.Owned(v.gem);
            v.badge.enabled = GemSave.EquippedCount(v.gem) > 0;
            v.rect.localScale = Vector3.one * (v.gem == storageSelected ? 1.08f : 1f);
            shown++;
        }
        storageEmpty.gameObject.SetActive(shown == 0);
        storageEmpty.text = "보유한 젬이 없습니다";

        bool has = storageSelected >= 0;
        gemInfoIcon.enabled = has;
        equipButton.gameObject.SetActive(has);
        unequipButton.gameObject.SetActive(has);
        gemInfoTag.gameObject.SetActive(has);
        if (!has)
        {
            gemInfoName.text = "";
            gemInfoEffect.text = "젬을 선택하세요";
            gemInfoCount.text = "";
            return;
        }
        GemCatalog.GemDef sel = gc.gems[storageSelected];
        gemInfoIcon.sprite = sel.icon;
        gemInfoName.text = sel.name;
        FitOneLine(gemInfoName, 30, 18, 400f);
        gemInfoTag.color = gc.CategoryColor(sel.category);
        gemInfoTagText.text = GemCatalog.CategoryName(sel.category);
        gemInfoEffect.text = gc.EffectText(storageSelected);
        int owned = GemSave.Owned(storageSelected), equipped = GemSave.EquippedCount(storageSelected);
        gemInfoCount.text = "보유 " + owned + "개 · 장착 " + equipped + "개";
        gemInfoCount.color = MutedText;
        bool freeSlot = false;
        for (int i = 0; i < GemSave.Current.slots.Length; i++)
            if (GemSave.SlotCategory(i) == sel.category && GemSave.SlotOpen(i) && (GemSave.Current.slots[i] < 0 || i == storageTargetSlot)) freeSlot = true;
        SetDisabled(equipButton, GemSave.Free(storageSelected) <= 0 || !freeSlot);
        SetDisabled(unequipButton, equipped <= 0);
    }

    // 특수 젬 카드 (그림 + 이름 + 증가 초록 / 감소 빨강 두 줄, 장착 E, 잠금 자물쇠) 2개씩
    private void FillSpecialCards()
    {
        ClearChildren(specialCards);
        List<GemSave.SpecialGem> list = GemSave.Current.specials;
        float w = 450f, h = 140f, gx = 20f, gy = 14f;
        for (int i = 0; i < list.Count; i++)
        {
            GemSave.SpecialGem g = list[i];
            int r = i / 2, c = i % 2;
            Vector2 pos = new Vector2((c - 0.5f) * (w + gx), -h * 0.5f - 8f - r * (h + gy));
            Image card = GemFrame("Card " + g.id, specialCards, PlainPaper, pos, new Vector2(w, h), PaperTint);
            TopAnchor(card.rectTransform);
            card.color = g.id == storageSpecial ? SelectedTint : new Color(0.93f, 0.86f, 0.76f, 1f);
            Clickable(card, () => { storageSpecial = g.id; RefreshGemStorage(); });
            Icon("Icon", card.transform, SpecialSprite(g), Center, new Vector2(-158f, 0f), Vector2.one * 124f);
            if (GemSave.IsSpecialEquipped(g.id)) Icon("E", card.transform, gc.equippedBadge, Center, new Vector2(-202f, 42f), Vector2.one * 36f);
            if (g.locked) Icon("Lock", card.transform, gc.lockBig, Center, new Vector2(-116f, -42f), Vector2.one * 36f);
            Text name = Label("Name", card.transform, GemSave.SpecialName(g), 20, PaperText, TextAnchor.MiddleLeft, Center, new Vector2(62f, 40f), new Vector2(280f, 30f), false);
            FitOneLine(name, 20, 14, 280f);
            SpecialLines(card.transform, g, new Vector2(62f, -14f), 280f, 22, 34f);
        }
        SetContentHeight(specialCards, (list.Count + 1) / 2 * (h + gy) + 16f);
    }

    // 선택한 특수 젬 정보 (장착·해제·잠금)
    private void RefreshSpecialInfo()
    {
        GemSave.SpecialGem sel = GemSave.FindSpecial(storageSpecial);
        bool has = sel != null;
        gemInfoIcon.enabled = has;
        equipButton.gameObject.SetActive(has);
        unequipButton.gameObject.SetActive(has);
        gemInfoTag.gameObject.SetActive(has);
        if (!has)
        {
            gemInfoName.text = "";
            gemInfoEffect.text = "특수 젬을 선택하세요";
            gemInfoCount.text = "";
            return;
        }
        gemInfoIcon.sprite = SpecialSprite(sel);
        gemInfoName.text = GemSave.SpecialName(sel);
        FitOneLine(gemInfoName, 30, 18, 400f);
        gemInfoTag.color = gc.specialColor;
        gemInfoTagText.text = "특수 · " + GemCatalog.CategoryName(GemSave.SpecialCategory(sel));
        gemInfoEffect.fontSize = 24;
        gemInfoEffect.text = "<color=#" + ColorUtility.ToHtmlStringRGB(gc.upColor) + ">" + UpLine(sel) + "</color>\n<color=#"
            + ColorUtility.ToHtmlStringRGB(gc.downColor) + ">" + DownLine(sel) + "</color>";
        bool equipped = GemSave.IsSpecialEquipped(sel.id);
        string why = equipped ? null : GemSave.CanEquipSpecial(sel, storageSpecialTarget);
        gemInfoCount.text = equipped ? "장착 중" : why ?? (sel.locked ? "잠금 (분해 불가)" : "");
        // 장착할 수 없는 이유는 경고 (빨갛고 조금 작게)
        gemInfoCount.color = why != null ? gc.warningColor : MutedText;
        int infoSize = why != null ? Mathf.RoundToInt(24 * gc.warningFontScale) : 24;
        FitOneLine(gemInfoCount, infoSize, 14, 420f);
        SetDisabled(equipButton, equipped || why != null);
        SetDisabled(unequipButton, !equipped);
        LockToggle(gemInfoLockHolder, Vector2.zero, 64f, sel.locked, () =>
        {
            GemSave.SetSpecialLocked(sel.id, !sel.locked);
            RefreshGemStorage();
        });
    }

    // ---------------- 합성: 일반 젬 (1+1) / 특수 젬 (일반 젬 x specialCombineCount) ----------------
    private readonly List<GemCellView> combineCells = new List<GemCellView>();
    private int[] materials = { -1, -1 };
    private readonly Image[] materialIcons = new Image[2];
    private Image[] specialMatIcons;
    private Image resultIcon, specialResultIcon;
    private Text combineMessage, combineWarning, specialNeedText;
    private UiGlowBloom specialResultGlow;
    private RectTransform combineList;                   // 합성 화면 보유 젬 스크롤 목록
    private Sprite UnknownSpecial => gc.unknownSpecialPlain != null ? gc.unknownSpecialPlain : gc.unknownSpecial;   // 광채 없는 ?
    private StatCategory combineCategory = StatCategory.Attack;
    private readonly Button[] categoryButtons = new Button[3];
    private Button combineButton, normalTabButton, specialTabButton;
    private RectTransform normalGroup, specialGroup;
    private bool combineSpecial;                  // true = 특수 젬 합성
    private int lastResult = -1;
    private GemSave.SpecialGem lastSpecial;

    private int MaterialCount => combineSpecial ? Mathf.Max(1, gc.specialCombineCount) : 2;

    private CanvasGroup BuildGemCombine()
    {
        RectTransform panel = NewPanel("Gem Combine");
        Header(panel, "젬 합성");
        ShardCounter(panel);
        materials = new int[Mathf.Max(2, gc.specialCombineCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = -1;

        normalTabButton = GemButton("Normal Tab", panel, "일반 젬 합성 (1+1)", true, new Vector2(-250f, 330f), new Vector2(460f, 72f), () => SetCombineMode(false), 28);
        specialTabButton = GemButton("Special Tab", panel, "특수 젬 합성 (x" + gc.specialCombineCount + ")", false, new Vector2(250f, 330f), new Vector2(460f, 72f), () => SetCombineMode(true), 28);

        Image list = GemFrame("Owned", panel, gc.paperPanel, new Vector2(-560f, -40f), new Vector2(600f, 620f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(list.transform, 620f, "보유 젬 (장착 중은 E)", 26);
        combineList = ScrollContent(list.transform, new Vector2(0f, -32f), new Vector2(560f, 530f));   // 젬이 많아지면 위아래 스크롤
        for (int i = 0; i < gc.gems.Count; i++)
        {
            int gem = i;
            GemCellView v = GemCell(combineList, i, Vector2.zero, 104f, () => AddMaterial(gem));
            TopAnchor(v.rect);
            combineCells.Add(v);
        }

        Image box = GemFrame("Combine", panel, gc.paperPanel, new Vector2(330f, -40f), new Vector2(940f, 620f), new Color(0.96f, 0.9f, 0.8f, 1f));
        BandTitle(box.transform, 620f, "합성하기");

        // 일반 젬 합성 (1+1, 결과 분야 선택)
        normalGroup = NewRect("Normal", box.transform, Center, Vector2.zero, new Vector2(940f, 620f));
        for (int k = 0; k < 2; k++)
        {
            int index = k;
            Image slot = GemFrame("Material " + k, normalGroup, gc.slotPlus, new Vector2(-260f + k * 230f, 120f), Vector2.one * 140f, new Color(0.4f, 0.3f, 0.2f, 1f));
            materialIcons[k] = Icon("Gem", slot.transform, null, Center, Vector2.zero, Vector2.one * 118f);
            Clickable(slot, () => RemoveMaterial(index));                      // 누르면 재료 빼기
        }
        GemLabel("Plus", normalGroup, "+", 64, PaperText, new Vector2(-145f, 120f), new Vector2(80f, 80f));
        Icon("Arrow", normalGroup, gc.arrowRight, Center, new Vector2(90f, 120f), new Vector2(54f, 76f));
        GemFrame("Result Slot", normalGroup, gc.slotDark, new Vector2(255f, 120f), Vector2.one * 150f, new Color(0.3f, 0.2f, 0.15f, 1f));
        resultIcon = Icon("Result", normalGroup, null, Center, new Vector2(255f, 120f), Vector2.one * 132f);
        GemLabel("Category Title", normalGroup, "결과 분야 선택", 28, PaperText, new Vector2(0f, -10f), new Vector2(400f, 46f));
        for (int c = 0; c < 3; c++)
        {
            StatCategory cat3 = (StatCategory)c;
            categoryButtons[c] = GemButton("Category " + c, normalGroup, GemCatalog.CategoryName(cat3), false, new Vector2(-280f + c * 280f, -85f), new Vector2(240f, 72f), () => { combineCategory = cat3; RefreshGemCombine(); });
            TintButton(categoryButtons[c], gc.CategoryColor(cat3));
        }

        // 특수 젬 합성 (일반 젬 n개 → 무작위 특수 젬 1개)
        specialGroup = NewRect("Special", box.transform, Center, Vector2.zero, new Vector2(940f, 620f));
        int n = Mathf.Max(1, gc.specialCombineCount);
        specialMatIcons = new Image[n];
        float slotSize = n > 4 ? 100f : 124f, step = slotSize + 16f;
        float startX = -105f - (n - 1) * step * 0.5f;
        for (int k = 0; k < n; k++)
        {
            int index = k;
            Image slot = GemFrame("Material " + k, specialGroup, gc.slotPlus, new Vector2(startX + k * step, 110f), Vector2.one * slotSize, new Color(0.4f, 0.3f, 0.2f, 1f));
            specialMatIcons[k] = Icon("Gem", slot.transform, null, Center, Vector2.zero, Vector2.one * slotSize * 0.84f);
            Clickable(slot, () => RemoveMaterial(index));
        }
        Icon("Arrow", specialGroup, gc.arrowRight, Center, new Vector2(startX + (n - 1) * step + slotSize * 0.5f + 40f, 110f), new Vector2(46f, 66f));
        // 보라 광채: 합성 전 ? 에는 없고, 합성하면 결과 뒤에서 피어남 (결과 그림보다 먼저 만들어 뒤에 그림)
        Image glow = Icon("Glow", specialGroup, gc.specialGlow, Center, new Vector2(330f, 110f), Vector2.one * gc.specialGlowSize);
        specialResultGlow = glow.gameObject.AddComponent<UiGlowBloom>();
        specialResultGlow.inTime = gc.specialGlowInTime;
        specialResultGlow.restAlpha = gc.specialGlowRestAlpha;
        specialResultGlow.pulse = gc.specialGlowPulse;
        glow.gameObject.SetActive(false);
        specialResultIcon = Icon("Result", specialGroup, UnknownSpecial, Center, new Vector2(330f, 110f), Vector2.one * 210f);
        Image need = GemFrame("Need", specialGroup, PlainPaper, new Vector2(0f, -45f), new Vector2(520f, 76f), PaperTint);
        specialNeedText = GemLabel("Text", need.transform, "", 26, PaperText, Vector2.zero, new Vector2(480f, 60f));

        combineMessage = GemLabel("Message", box.transform, "", 24, MutedText, new Vector2(0f, -165f), new Vector2(860f, 70f));
        // 경고: 안내 문구와 떨어진 줄에 빨갛고 조금 작게
        combineWarning = GemLabel("Warning", box.transform, "", Mathf.RoundToInt(24 * gc.warningFontScale), gc.warningColor, new Vector2(0f, -188f), new Vector2(860f, 30f));
        combineButton = GemButton("Combine", box.transform, "합성", true, new Vector2(0f, -250f), new Vector2(320f, 82f), DoCombine, 34);

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(gemMainPanel));
        return panel.GetComponent<CanvasGroup>();
    }

    private void OnGemCombineOpen()
    {
        ClearMaterials();
        lastResult = -1;
        lastSpecial = null;
        RefreshGemCombine();
    }

    private void SetCombineMode(bool special)
    {
        if (combineSpecial == special) return;
        combineSpecial = special;
        OnGemCombineOpen();
    }

    private void ClearMaterials()
    {
        for (int i = 0; i < materials.Length; i++) materials[i] = -1;
    }

    // 재료로 쓸 수 있는 남은 개수 (장착 중·이미 고른 재료 제외)
    private int MaterialLeft(int gem)
    {
        int used = 0;
        for (int i = 0; i < MaterialCount; i++) if (materials[i] == gem) used++;
        return GemSave.Free(gem) - used;
    }

    private void AddMaterial(int gem)
    {
        if (MaterialLeft(gem) <= 0) return;
        lastResult = -1;
        lastSpecial = null;
        int count = MaterialCount;
        int empty = -1;
        for (int i = 0; i < count; i++) if (materials[i] < 0) { empty = i; break; }
        materials[empty >= 0 ? empty : count - 1] = gem;
        RefreshGemCombine();
    }

    private void RemoveMaterial(int index)
    {
        if (index < 0 || index >= materials.Length) return;
        materials[index] = -1;
        RefreshGemCombine();
    }

    private bool MaterialsReady()
    {
        for (int i = 0; i < MaterialCount; i++) if (materials[i] < 0) return false;
        return true;
    }

    private void DoCombine()
    {
        if (!MaterialsReady()) return;
        if (combineSpecial)
        {
            int[] used = new int[MaterialCount];
            System.Array.Copy(materials, used, used.Length);
            GemSave.SpecialGem g = GemSave.CombineSpecial(used);
            if (g == null) return;
            lastSpecial = g;
            ClearMaterials();
            UiPop.Play(specialResultIcon.rectTransform, 1.18f, 0.35f);   // 얻은 순간 톡 커짐
            specialResultGlow.Bloom();                                    // ? 에 있던 보라 광채가 결과 뒤에서 피어남
            RefreshGemCombine();
            return;
        }
        int result = GemSave.Combine(materials[0], materials[1], combineCategory);
        if (result < 0) return;
        lastResult = result;
        ClearMaterials();
        UiFx.Play(UiFx.Kind.Sparkle, resultIcon.rectTransform, Vector2.zero, 160f, null, 16f);
        RefreshGemCombine();
    }

    private void RefreshGemCombine()
    {
        RefreshShards();
        ((Image)normalTabButton.targetGraphic).sprite = combineSpecial ? gc.buttonBrown : gc.buttonGreen;
        ((Image)specialTabButton.targetGraphic).sprite = combineSpecial ? gc.buttonGreen : gc.buttonBrown;
        normalGroup.gameObject.SetActive(!combineSpecial);
        specialGroup.gameObject.SetActive(combineSpecial);

        // 보유 젬: 장착 중이라 재료로 못 쓰는 젬은 흐리게 + E
        int shown = 0;
        float cell = 104f, gap = 20f;
        foreach (GemCellView v in combineCells)
        {
            bool visible = GemSave.Owned(v.gem) > 0;
            v.rect.gameObject.SetActive(visible);
            if (!visible) continue;
            int r = shown / 4, c = shown % 4;
            v.rect.anchoredPosition = new Vector2(-1.5f * (cell + gap) + c * (cell + gap), -cell * 0.5f - 12f - r * (cell + gap));   // 스크롤 목록 위에서부터
            int left = MaterialLeft(v.gem);
            v.count.text = "x" + Mathf.Max(0, left);
            v.badge.enabled = GemSave.EquippedCount(v.gem) > 0;
            v.icon.color = left > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            v.frame.color = GemSave.Free(v.gem) > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
            shown++;
        }
        SetContentHeight(combineList, 24f + Mathf.CeilToInt(shown / 4f) * (cell + gap));

        bool ready = MaterialsReady();
        SetDisabled(combineButton, !ready);
        if (combineSpecial)
        {
            int filled = 0;
            for (int k = 0; k < specialMatIcons.Length; k++)
            {
                int g = materials[k];
                if (g >= 0) filled++;
                specialMatIcons[k].sprite = g >= 0 ? gc.gems[g].icon : null;
                specialMatIcons[k].enabled = g >= 0;
            }
            specialResultIcon.sprite = lastSpecial != null ? SpecialSprite(lastSpecial) : UnknownSpecial;
            specialResultIcon.enabled = specialResultIcon.sprite != null;
            if (lastSpecial == null) specialResultGlow.gameObject.SetActive(false);
            specialNeedText.text = "필요 젬  " + filled + " / " + specialMatIcons.Length + "개";
            combineMessage.text = lastSpecial != null
                ? GemSave.SpecialName(lastSpecial) + " 획득!\n<color=#" + ColorUtility.ToHtmlStringRGB(gc.upColor) + ">" + UpLine(lastSpecial) + "</color>   <color=#"
                  + ColorUtility.ToHtmlStringRGB(gc.downColor) + ">" + DownLine(lastSpecial) + "</color>"
                : "일반 젬 " + specialMatIcons.Length + "개를 사용하여 무작위 특수 젬 1개를 획득합니다.";
            combineMessage.color = lastSpecial != null ? PaperText : MutedText;
            ShowCombineWarning(lastSpecial == null ? "장착 중인 젬은 재료로 쓸 수 없습니다" : null);
            return;
        }

        for (int k = 0; k < 2; k++)
        {
            int g = materials[k];
            materialIcons[k].sprite = g >= 0 ? gc.gems[g].icon : null;
            materialIcons[k].enabled = g >= 0;
        }
        resultIcon.sprite = lastResult >= 0 ? gc.gems[lastResult].icon : gc.Unknown(combineCategory);
        resultIcon.enabled = resultIcon.sprite != null;

        for (int c = 0; c < 3; c++)
        {
            bool on = (int)combineCategory == c;
            categoryButtons[c].transform.localScale = Vector3.one * (on ? 1.08f : 1f);
            ((Image)categoryButtons[c].targetGraphic).color = on ? gc.CategoryColor((StatCategory)c) : Color.Lerp(gc.CategoryColor((StatCategory)c), Color.gray, 0.55f);
        }

        combineMessage.text = lastResult >= 0 ? gc.gems[lastResult].name + " 획득! (" + gc.EffectText(lastResult) + ")"
            : "선택한 분야(" + GemCatalog.CategoryName(combineCategory) + ")의 일반 젬 중 하나를 무작위로 획득합니다.";
        combineMessage.color = lastResult >= 0 ? gc.CategoryColor(gc.gems[lastResult].category) : MutedText;
        ShowCombineWarning(null);
    }

    // 경고가 있으면 안내 문구를 조금 올리고 그 아래 떨어진 줄에 경고를 씀
    private void ShowCombineWarning(string warning)
    {
        bool has = !string.IsNullOrEmpty(warning);
        combineWarning.text = has ? warning : "";
        combineMessage.rectTransform.anchoredPosition = new Vector2(0f, has ? -148f : -165f);
    }
}
