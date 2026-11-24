using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 레벨업 화면 (1920x1080 기준, 씬 배치 없이 실행 중에 생성)
// 위: 보유 무기 6칸(3x2, 무기 레벨) / 보유 기믹 칸(7x2, 기믹 단계)  ← 서로 다른 데이터
// 아래: 이번 선택지 카드 3장 → 카드를 고르고 [선택] (두 번 누르면 바로 선택)
// 카드를 고르면 기존 카드(Item) 버튼의 클릭을 그대로 실행한다 (Item.OnClick + LevelUp.Hide).
// 그림·배치는 Resources/GimmickCatalog (Ingame_Levelup_panel, Panel_1, Icon_1, Ingame_Levelup_icon)
public class LevelUpView : MonoBehaviour
{
    private static LevelUpView instance;

    public static void Open(LevelUp levelUp)
    {
        if (instance == null)
        {
            instance = new GameObject("LevelUp View").AddComponent<LevelUpView>();
            instance.Build();
        }
        instance.levelUp = levelUp;
        instance.selected = -1;
        instance.root.SetActive(true);
        instance.RefreshAll();
    }

    public static void Close()
    {
        if (instance != null) instance.root.SetActive(false);
    }

    // 리롤 연출: 카드마다 모서리에 작은 반짝임 하나
    public static void PlayRerollFx()
    {
        if (instance == null || !instance.root.activeSelf)
            return;
        GimmickCatalog c = instance.cat;
        foreach (CardView v in instance.cards)
        {
            if (v == null || !v.rect.gameObject.activeSelf) continue;
            Vector2 half = v.rect.sizeDelta * 0.5f;
            // 카드 안 무작위 1~2곳 (서로 겹치지 않게 반짝임 크기 이상 떨어뜨림)
            int count = Random.Range(1, 3);                          // 1~2개
            List<Vector2> spots = new List<Vector2>();
            for (int tries = 0; spots.Count < count && tries < 30; tries++)
            {
                Vector2 p = new Vector2(Random.Range(-0.6f, 0.6f) * half.x, Random.Range(-0.6f, 0.6f) * half.y);
                if (spots.TrueForAll(q => (q - p).magnitude >= c.rerollFxSize)) spots.Add(p);
            }
            foreach (Vector2 spot in spots)
            UiFx.Play(UiFx.Kind.Sparkle, v.rect, spot, c.rerollFxSize, new Color(1f, 1f, 1f, c.rerollFxAlpha), 18f);
        }
    }

    public static void Refresh()
    {
        if (instance != null && instance.root.activeSelf)
        {
            instance.selected = -1;
            instance.RefreshAll();
        }
    }

    private class SlotView
    {
        public Image frame, icon;
        public Text label;
    }

    private class CardView
    {
        public RectTransform rect;
        public Image frame, icon, tag;
        public Image synergy;                                   // 메인 무기의 짝(시너지) 무기 카드: 메인 무기 아이콘
        public CardSparkles sparkles;
        public Text name, desc, step;
        public float hover;
        public float lastClick = -1f;
    }

    private GimmickCatalog cat;
    private LevelUp levelUp;
    private GameObject root;
    private Text title;
    private readonly List<SlotView> weaponSlots = new List<SlotView>();
    private readonly List<SlotView> gimmickSlots = new List<SlotView>();
    private readonly CardView[] cards = new CardView[3];
    private Button confirm;
    private Button reroll;
    private Text rerollText;
    private int selected = -1;

    private static readonly ItemData.ItemType[] WeaponOrder =
    {
        ItemData.ItemType.DualBlades, ItemData.ItemType.HuntingBow, ItemData.ItemType.SniperHBG,
        ItemData.ItemType.GreatSword, ItemData.ItemType.Lance,
    };

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------------- 만들기 ----------------
    private void Build()
    {
        cat = GimmickCatalog.Instance;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;                                   // 상점(20) 위, HUD 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = Rect("Root", transform, Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)root.transform);
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.45f);

        Image panel = Frame("Panel", root.transform, cat.frame, cat.panelPosition, cat.panelSize, new Color(0.93f, 0.85f, 0.72f, 1f));
        Transform p = panel.transform;
        float top = cat.panelSize.y * 0.5f;

        // 보유 무기 (3x2)
        BuildSlotBox("Weapons", p, cat.weaponBoxPosition, cat.badgeWeapon, 3, 6, cat.weaponSlotSize, cat.weaponSlotGap, weaponSlots);
        // 보유 기믹 (7x2)
        BuildSlotBox("Gimmicks", p, cat.gimmickBoxPosition, cat.badgeGimmick, cat.gimmickColumns, cat.gimmickSlotCount, cat.gimmickSlotSize, cat.gimmickSlotGap, gimmickSlots);

        // 구분선
        float dividerY = cat.gimmickBoxPosition.y - BoxHeight(cat.gimmickSlotCount, cat.gimmickColumns, cat.gimmickSlotSize, cat.gimmickSlotGap) * 0.5f - 18f;
        Image line = Icon("Divider", p, cat.divider, new Vector2(0f, dividerY), new Vector2(cat.panelSize.x * 0.82f, 20f));
        line.preserveAspect = false;

        // 카드 3장
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = BuildCard(p, i);
        }

        // 제목 띠 (칸 묶음 위에 그려지도록 나중에 만듦). 문구는 한 줄로, 길면 글자 크기를 줄임
        Image header = Frame("Header", p, cat.header, new Vector2(0f, top - 14f), cat.headerSize, new Color(0.9f, 0.8f, 0.65f, 1f));
        title = Label("Title", header.transform, "", cat.headerFontSize, cat.textDark, TextAnchor.MiddleCenter, cat.headerTextOffset, new Vector2(cat.headerSize.x * 0.8f, cat.headerSize.y * 0.8f), false);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;

        // [선택] / [리롤]
        confirm = MakeButton("Confirm", p, cat.buttonGreen, cat.selectIcon, "선택", new Vector2(0f, cat.confirmY), cat.confirmSize, cat.confirmFontSize, Confirm);
        reroll = MakeButton("Reroll", p, cat.buttonBrown, cat.rerollIcon, "리롤", new Vector2(cat.rerollX, cat.confirmY), cat.rerollSize, cat.confirmFontSize - 6, OnReroll);
        rerollText = reroll.GetComponentInChildren<Text>();

        // 잎 장식 (버튼 양옆)
        if (cat.leaf != null)
        {
            float x = cat.confirmSize.x * 0.5f + 34f;
            Icon("Leaf L", p, cat.leaf, new Vector2(-x, cat.confirmY - 4f), new Vector2(64f, 46f));
            Image r = Icon("Leaf R", p, cat.leaf, new Vector2(x, cat.confirmY - 4f), new Vector2(64f, 46f));
            r.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
        root.SetActive(false);
    }

    private static float BoxHeight(int count, int columns, Vector2 slot, Vector2 gap)
    {
        int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
        return rows * slot.y + (rows - 1) * gap.y + 46f;
    }

    private void BuildSlotBox(string name, Transform parent, Vector2 center, Sprite badge, int columns, int count, Vector2 slot, Vector2 gap, List<SlotView> list)
    {
        int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
        float badgeW = 78f;
        float gridW = columns * slot.x + (columns - 1) * gap.x;
        Vector2 size = new Vector2(24f + badgeW + 20f + gridW + 24f, BoxHeight(count, columns, slot, gap));
        Image box = Frame(name, parent, cat.box, center, size, new Color(0.96f, 0.9f, 0.8f, 1f));

        Image b = Icon("Badge", box.transform, badge, new Vector2(-size.x * 0.5f + 24f + badgeW * 0.5f, 0f), new Vector2(badgeW, Mathf.Min(size.y - 36f, badgeW * 1.6f)));
        b.preserveAspect = true;

        float left = -size.x * 0.5f + 24f + badgeW + 20f + slot.x * 0.5f;
        float topY = (rows - 1) * (slot.y + gap.y) * 0.5f + 6f;
        for (int i = 0; i < count; i++)
        {
            int r = i / columns, c = i % columns;
            Vector2 pos = new Vector2(left + c * (slot.x + gap.x), topY - r * (slot.y + gap.y));
            SlotView v = new SlotView();
            v.frame = Frame("Slot " + i, box.transform, cat.slot, pos, slot, new Color(0.9f, 0.82f, 0.7f, 1f));
            v.icon = Icon("Icon", v.frame.transform, null, new Vector2(0f, slot.y * 0.04f), slot * 0.72f);
            Image tag = Frame("Tag", v.frame.transform, cat.levelTag, new Vector2(0f, -slot.y * 0.5f - 4f), new Vector2(slot.x * 0.86f, 28f), new Color(0.6f, 0.42f, 0.28f, 1f));
            v.label = Label("Level", tag.transform, "", cat.slotLevelFontSize, cat.textLight, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(slot.x, 28f), true);
            list.Add(v);
        }
    }

    private CardView BuildCard(Transform parent, int index)
    {
        CardView v = new CardView();
        Vector2 size = cat.cardSize;
        float x = (index - 1) * cat.cardSpacing;
        v.frame = Frame("Card " + index, parent, cat.card, new Vector2(x, cat.cardY), size, new Color(0.96f, 0.9f, 0.8f, 1f));
        v.frame.raycastTarget = true;
        v.rect = v.frame.rectTransform;

        // 카드 그림의 위 이름판 / 아래 단계판 자리 (카드 높이 비율)
        float h = size.y, w = size.x;
        // 카드 그림의 위 이름판·아래 단계판은 9-slice 테두리 안이라 카드 크기와 상관없이 그림 픽셀 그대로 (위에서 cardNameFromTop, 아래에서 cardStepFromBottom)
        float nameY = h * 0.5f - cat.cardNameFromTop;
        v.tag = Frame("Tag", v.rect, cat.buttonGrey, new Vector2(0f, nameY), cat.cardTagSize, new Color(0.6f, 0.6f, 0.6f, 1f));   // 분류별 색 (무기·소모품·기믹)
        v.name = Label("Name", v.tag.transform, "", cat.cardNameFontSize, Color.white, TextAnchor.MiddleCenter, Vector2.zero, cat.cardTagSize - new Vector2(20f, 4f), true);
        v.name.verticalOverflow = VerticalWrapMode.Truncate;
        v.name.resizeTextForBestFit = true;
        v.name.resizeTextMinSize = 14;
        v.name.resizeTextMaxSize = cat.cardNameFontSize;

        // 이름표 아래 간격 → 아이콘 → 간격 → 설명 → 간격 → 단계판
        float iconTop = nameY - cat.cardTagSize.y * 0.5f - cat.cardIconGap;
        float iconY = iconTop - cat.cardIconSize * 0.5f;
        float stepY = -h * 0.5f + cat.cardStepFromBottom;
        float descTop = iconTop - cat.cardIconSize - cat.cardDescGap;
        float descBottom = stepY + 22f + cat.cardDescGap;
        v.icon = Icon("Icon", v.rect, null, new Vector2(0f, iconY), Vector2.one * cat.cardIconSize);
        // 짝 무기 표시: 무기 그림 오른쪽 위 모서리에 작은 메인 무기 아이콘 (그림을 가리지 않고 배지처럼)
        v.synergy = Icon("Synergy", v.rect, null, new Vector2(cat.cardIconSize * 0.62f, iconY + cat.cardIconSize * 0.36f), Vector2.one * cat.cardIconSize * 0.42f);
        v.synergy.enabled = false;
        v.sparkles = v.frame.gameObject.AddComponent<CardSparkles>();   // 짝 무기 카드: 은은한 금빛 반짝임 (예전 카드와 같은 효과)
        v.sparkles.enabled = false;
        v.desc = Label("Desc", v.rect, "", cat.cardDescFontSize, cat.textDark, TextAnchor.MiddleCenter, new Vector2(0f, (descTop + descBottom) * 0.5f), new Vector2(w * 0.84f, Mathf.Max(20f, descTop - descBottom)), false);
        RunUI.ApplyPanelFont(v.desc);                            // 작은 설명 글자는 또렷한 글꼴 (제목은 픽셀 글꼴 유지)
        v.desc.supportRichText = true;
        v.desc.resizeTextForBestFit = true;
        v.desc.resizeTextMinSize = 12;
        v.desc.resizeTextMaxSize = cat.cardDescFontSize;
        v.desc.verticalOverflow = VerticalWrapMode.Truncate;
        v.step = Label("Step", v.rect, "", cat.cardStepFontSize, cat.textDark, TextAnchor.MiddleCenter, new Vector2(0f, stepY), new Vector2(w * 0.5f, 30f), false);
        RunUI.ApplyPanelFont(v.step);

        Button button = v.frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => OnCardClick(index));
        CardHover hover = v.frame.gameObject.AddComponent<CardHover>();
        hover.view = v;
        return v;
    }

    private class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public CardView view;
        private bool over;
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; }
        private void OnDisable() { over = false; view.hover = 0f; }
        private void Update() { view.hover = Mathf.MoveTowards(view.hover, over ? 1f : 0f, Time.unscaledDeltaTime * 8f); }
    }

    // ---------------- 동작 ----------------
    private void OnCardClick(int index)
    {
        if (levelUp == null || index >= levelUp.CurrentChoices.Count)
            return;
        CardView v = cards[index];
        bool doubleClick = selected == index && Time.unscaledTime - v.lastClick < 0.35f;
        v.lastClick = Time.unscaledTime;
        if (doubleClick)
        {
            Confirm();
            return;
        }
        selected = index;                                            // 레벨업 카드는 클릭음 없음
    }

    private void Confirm()
    {
        if (levelUp == null || selected < 0 || selected >= levelUp.CurrentChoices.Count)
            return;
        Item item = levelUp.CurrentChoices[selected];
        Button button = item.GetComponent<Button>();
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();                                 // 기존 카드 클릭 (Item.OnClick + LevelUp.Hide)
        }
        else
        {
            item.OnClick();
            levelUp.Hide();
        }
    }

    private void OnReroll()
    {
        if (LevelUpReroll.Instance != null) LevelUpReroll.Instance.Click();
    }

    private void Update()
    {
        if (!root.activeSelf)
            return;
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < cards.Length; i++)
        {
            CardView v = cards[i];
            float goal = i == selected ? cat.cardSelectedScale : 1f + (cat.cardHoverScale - 1f) * v.hover;
            v.rect.localScale = Vector3.Lerp(v.rect.localScale, Vector3.one * goal, k);
            v.frame.color = selected >= 0 && i != selected ? cat.cardDimColor : Color.white;
        }
        confirm.interactable = selected >= 0;

        LevelUpReroll r = LevelUpReroll.Instance;
        bool canReroll = r != null && (r.Remaining > 0 || TestMode.InfiniteReroll);
        reroll.gameObject.SetActive(r != null);
        reroll.interactable = canReroll;
        if (r != null) rerollText.text = "리롤 " + (TestMode.InfiniteReroll ? "∞" : r.Remaining.ToString());
    }

    private void RefreshAll()
    {
        title.text = levelUp != null ? levelUp.TitleText.Replace("\n", " ") : "";
        FitOneLine(title, cat.headerFontSize, cat.headerSize.x * 0.62f);   // 띠 양끝 장식 안쪽에 들어가게
        Item[] items = levelUp != null ? levelUp.Items : new Item[0];

        // 보유 무기: 얻은 순서(무기 종류 순)대로 앞 칸부터, 나머지는 빈 칸
        List<Item> owned = new List<Item>();
        foreach (ItemData.ItemType type in WeaponOrder)
        {
            Item it = System.Array.Find(items, x => x != null && x.data != null && x.data.itemType == type);
            if (it != null && it.level > 0) owned.Add(it);
        }
        for (int i = 0; i < weaponSlots.Count; i++)
        {
            Item it = i < owned.Count ? owned[i] : null;
            bool max = it != null && it.level >= it.data.damages.Length;
            SetSlot(weaponSlots[i], it != null ? (max && it.data.maxLevelIcon != null ? it.data.maxLevelIcon : it.data.itemIcon) : null,
                it == null ? "" : max ? "MAX" : "Lv." + it.level);
        }

        // 보유 기믹: C 번호 순서로 얻은 것부터 (단계 = 현재 / 최대)
        List<Item> gimmicks = new List<Item>();
        foreach (ItemData.ItemType type in GimmickCatalog.Order)
        {
            Item it = System.Array.Find(items, x => x != null && x.data != null && x.data.itemType == type);
            if (it != null && it.level > 0) gimmicks.Add(it);
        }
        for (int i = 0; i < gimmickSlots.Count; i++)
        {
            Item it = i < gimmicks.Count ? gimmicks[i] : null;
            SetSlot(gimmickSlots[i], it != null ? CardIcon(it) : null, it == null ? "" : it.level + "/" + it.data.damages.Length);
        }

        // 선택지 카드
        for (int i = 0; i < cards.Length; i++)
        {
            CardView v = cards[i];
            Item it = levelUp != null && i < levelUp.CurrentChoices.Count ? levelUp.CurrentChoices[i] : null;
            v.rect.gameObject.SetActive(it != null);
            if (it == null) continue;
            v.rect.localScale = Vector3.one;
            v.name.text = it.data.itemName.Trim();
            v.icon.sprite = CardIcon(it);
            v.icon.enabled = v.icon.sprite != null;
            v.icon.rectTransform.localScale = Vector3.one * (it.data.itemType == ItemData.ItemType.CoinPouch ? cat.goldBoxIconScale : 1f);
            v.tag.color = LevelUp.IsWeapon(it) ? cat.tagColorWeapon : LevelUp.IsConsumable(it) ? cat.tagColorConsumable : cat.tagColorGimmick;
            v.desc.text = Description(it);
            v.step.text = StepText(it);
            MainWeaponController main = MainWeaponController.Current;
            bool partner = main != null && main.MainWeapon != null && main.MainWeapon.Data != null && WeaponSynergy.IsPartner(it.data.itemType);
            v.synergy.enabled = partner;
            if (v.sparkles.enabled != partner) v.sparkles.enabled = partner;
            if (partner) { v.synergy.sprite = main.MainWeapon.Data.itemIcon; v.synergy.transform.SetAsLastSibling(); }
        }
    }

    // 한 줄에 들어가도록 글자 크기를 줄임
    private static void FitOneLine(Text text, int maxSize, float width)
    {
        text.fontSize = maxSize;
        while (text.fontSize > 12 && text.preferredWidth > width)
        {
            text.fontSize--;
        }
    }

    private void SetSlot(SlotView v, Sprite icon, string label)
    {
        v.icon.sprite = icon;
        v.icon.enabled = icon != null;
        v.label.text = label;
        v.label.transform.parent.gameObject.SetActive(icon != null);
        v.frame.color = icon != null ? Color.white : cat.emptySlotTint;
    }

    private static Sprite CardIcon(Item it)
    {
        if (it.data.itemType == ItemData.ItemType.Super_Magnet && GimmickCatalog.Instance.superMagnetIcon != null)
            return GimmickCatalog.Instance.superMagnetIcon;             // 슈퍼 그물망은 붉은 그물 (포획용 그물과 구분)
        Sprite g = GimmickCards.Icon(it.data.itemType);
        return g != null ? g : it.data.itemIcon;
    }

    // 현재 단계 / 최대 단계 (소모품은 '소모품')
    private static string StepText(Item it)
    {
        if (LevelUp.IsConsumable(it))
            return "소모품";
        if (LevelUp.IsWeapon(it))
            return "Lv." + it.level + " / " + it.data.damages.Length;
        return it.level + " / " + it.data.damages.Length;
    }

    // 카드 설명: 기믹은 단계별 수치(다음 단계 강조), 무기·소모품은 기존 카드 문구
    private string Description(Item it)
    {
        string g = GimmickDescription(it.data.itemType, it.level);
        if (g != null)
            return g;
        Text[] texts = it.GetComponentsInChildren<Text>(true);
        string text = texts.Length > 2 ? texts[2].text : it.data.itemDesc;
        return text.Replace("\n\n", "\n").Trim();
    }

    private string Steps(float[] table, int level, string prefix, string suffix, float mul = 100f)
    {
        List<string> parts = new List<string>();
        for (int i = 0; table != null && i < table.Length; i++)
        {
            string v = prefix + Mathf.RoundToInt(table[i] * mul) + suffix;
            parts.Add(i == level ? "<color=#" + ColorUtility.ToHtmlStringRGB(cat.textAccent) + ">" + v + "</color>" : v);
        }
        return string.Join(" → ", parts);
    }

    private string StepsInt(int[] table, int level, string prefix, string suffix)
    {
        float[] f = new float[table != null ? table.Length : 0];
        for (int i = 0; i < f.Length; i++) f[i] = table[i];
        return Steps(f, level, prefix, suffix, 1f);
    }

    private string GimmickDescription(ItemData.ItemType type, int level)
    {
        RunConfig rc = RunConfig.Instance;
        GimmickCatalog c = cat;
        switch (type)
        {
            case ItemData.ItemType.Knockback:
                return "적을 더 멀리 밀어냅니다 (보스 제외)\n" + Steps(rc.knockbackPerLevel, level, "+", "%");
            case ItemData.ItemType.Pierce:
                return "투사체가 적을 더 관통합니다\n" + StepsInt(rc.piercePerLevel, level, "+", "");
            case ItemData.ItemType.LastStand:
                return "체력 " + Mathf.RoundToInt(rc.lastStandThreshold * 100f) + "% 이하일 때 공격력\n" + Steps(rc.lastStandPerLevel, level, "+", "%");
            case ItemData.ItemType.DodgeCounter:
                return "구르기 후 " + c.dodgeCounterWindow.ToString("0.#") + "초 안 첫 공격 피해\n" + Steps(c.dodgeCounterBonus, level, "+", "%");
            case ItemData.ItemType.CounterInstinct:
                return "우클릭 기술 후 " + c.counterInstinctDuration.ToString("0.#") + "초간 공격속도\n" + Steps(c.counterInstinctSpeed, level, "+", "%");
            case ItemData.ItemType.Focus:
                return "피격 없이 " + c.focusInterval.ToString("0.#") + "초마다 피해 (최대 " + c.focusMaxStacks + "중첩)\n" + Steps(c.focusPerStack, level, "+", "%");
            case ItemData.ItemType.ChainLightning:
                return "적중 " + StepsInt(c.chainHitsPer, level, "", "") + "번마다\n주변 적 " + StepsInt(c.chainTargets, level, "", "") + "명에게 번개";
            case ItemData.ItemType.KillExplosion:
                return "처치 시 확률로 주변 폭발\n" + Steps(c.explodeChance, level, "", "%");
            case ItemData.ItemType.HuntInstinct:
                return "엘리트·중간보스·대장 몬스터에게\n주는 피해 " + Steps(c.huntBonus, level, "+", "%") + "\n(메인 보스 제외)";
            case ItemData.ItemType.TwinWeapon:
                // 다음 단계 효과만 (Lv1: 투사체 +1·칼 +1 / Lv2: 투사체 +1 → +2·칼 크기)
                int p1 = c.twinProjectiles.Length > 0 ? c.twinProjectiles[0] : 1, p2 = c.twinProjectiles.Length > 1 ? c.twinProjectiles[1] : 2;
                if (level <= 0)
                    return "보조 활·헤비보우건 투사체 +" + p1 + "\n보조 쌍검 칼 +" + (c.twinBlades.Length > 0 ? c.twinBlades[0] : 1);
                return "보조 활·헤비보우건 투사체\n+" + p1 + " → <color=#" + ColorUtility.ToHtmlStringRGB(c.textAccent) + ">+" + p2 + "</color>\n보조 쌍검 칼 크기 +" + Mathf.RoundToInt((c.twinBladeSizeMul - 1f) * 100f) + "%";
            case ItemData.ItemType.Shield:
                return "피해 1회를 막는 보호막\n" + Steps(c.shieldInterval, level, "", "초", 1f) + "마다";
            case ItemData.ItemType.FrostStep:
                return "주변 반경 " + c.frostRadius + " 안 적 이동속도 " + Steps(c.frostSlow, level, "-", "%") + "\n(보스·중간보스 제외,\n다른 감속과 중첩되지 않음)";
            case ItemData.ItemType.Escape:
                return "체력 " + Mathf.RoundToInt(c.escapeHealthRatio * 100f) + "% 이하가 되면\n" + c.escapeInvincibleTime.ToString("0.#") + "초 무적 + 밀쳐내기\n(낮·밤마다 1회)";
            case ItemData.ItemType.LifeStrike:
                return StepsInt(c.lifeKillsPer, level, "", "") + "마리 처치마다 최대 체력 +1%\n5초마다 다음 공격에 최대 체력의\n" + Steps(c.lifeStrikeRatio, level, "", "%") + " 추가 피해";
            default:
                return null;
        }
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

    private Image Frame(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color fallback)
    {
        Image image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = cat.pixelScale;
        }
        else
        {
            image.color = fallback;
        }
        return image;
    }

    private static Image Icon(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Image image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private static Text Label(string name, Transform parent, string content, int fontSize, Color color, TextAnchor align, Vector2 pos, Vector2 size, bool outline)
    {
        Text t = Rect(name, parent, pos, size).gameObject.AddComponent<Text>();
        t.font = RunUI.Font;
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.05f;
        t.raycastTarget = false;
        if (outline)
        {
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.05f, 0.02f, 1f);     // 불투명 (반투명이면 흐려 보임)
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    private Button MakeButton(string name, Transform parent, Sprite sprite, Sprite icon, string label, Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        Image image = Frame(name, parent, sprite, pos, size, new Color(0.4f, 0.6f, 0.25f, 1f));
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.66f, 0.6f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.5f, 0.48f, 0.46f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Text text = Label("Label", image.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, Vector2.zero, size, true);
        float iconSize = size.y * 0.5f;
        float width = text.preferredWidth + 30f;
        Icon("Icon", image.transform, icon, new Vector2(-width * 0.5f - iconSize * 0.15f, 0f), Vector2.one * iconSize);
        text.rectTransform.anchoredPosition = new Vector2(icon != null ? iconSize * 0.45f : 0f, 0f);
        return button;
    }
}
