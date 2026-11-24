using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 화면 왼쪽: 현재 캐릭터 능력치 현황 + 적용 중인 버프/리스크, 보관 슬롯(1칸)
// 능력치는 캐릭터 기본값 x 레벨업 능력치 x 상점 강화 x 선택지/버프까지 모두 계산한 최종값
// 머리글을 누르면 접고 펼 수 있다. 보관 슬롯은 클릭 또는 Q 키로 사용.
public class StatPanel : MonoBehaviour
{
    public const string SlotButtonName = "Storage Slot Button";

    private RunConfig config;
    private GameManager gm;
    private Canvas canvas;
    private Image panel;
    private Text header;
    private RectTransform rowsRoot;                          // 능력치 줄 (아이콘 + 글자)
    private readonly List<Image> rowIcons = new List<Image>();
    private readonly List<Text> rowTexts = new List<Text>();
    private readonly List<Text> rowValues = new List<Text>();     // 오른쪽 정렬 값
    private readonly List<KeyValuePair<Sprite, string>> entries = new List<KeyValuePair<Sprite, string>>();
    private bool collapsed;
    private float nextRefresh;

    private Image slotIcon;
    private Image slotImage;
    private Color slotBaseColor = Color.white;
    private Text slotLabel;

    private void Awake()
    {
        config = RunConfig.Instance;
        gm = GetComponent<GameManager>();
        Build();
        canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    private void Build()
    {
        canvas = RunUI.CreateCanvas("Stat Panel", -4, true);   // 레벨업 창, 상점보다 뒤에 표시
        Transform root = canvas.transform;

        // 능력치 판 (양피지)
        panel = RunUI.AssetPanel("Stats", root, config.cardSprite, config.cardBorder,
            new Color(0.1f, 0.08f, 0.06f, 0.8f), new Vector2(1f, 1f), config.statPanelOffset, new Vector2(config.statPanelWidth, 200f));
        panel.rectTransform.pivot = new Vector2(1f, 1f);                 // 화면 오른쪽 위 (옵션 버튼·헌터랭크 아래)
        panel.rectTransform.anchoredPosition = config.statPanelOffset;
        panel.rectTransform.localScale = Vector3.one * config.statPanelScale;   // 화면을 덜 가리게 판 전체 축소
        if (config.cardSprite != null)
        {
            panel.color = new Color(1f, 1f, 1f, config.statPanelAlpha);   // 반투명 (게임 화면을 최대한 가리지 않도록)
        }

        // 머리글 (누르면 접기/펼치기)
        Button headerButton = panel.gameObject.AddComponent<Button>();
        headerButton.transition = Selectable.Transition.None;
        headerButton.navigation = new Navigation { mode = Navigation.Mode.None };
        headerButton.onClick.AddListener(() =>
        {
            collapsed = !collapsed;                              // 접기/펼치기는 소리 없이
            nextRefresh = 0f;
        });

        header = RunUI.Label("Header", panel.transform, "", config.statFontSize + 2, config.parchmentTextColor, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(config.statPanelWidth - 30f, 32f), 0f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(16f + config.statIconSize + config.statIconGap, -10f);
        RunUI.ApplyPanelFont(header);
        Image paw = RunUI.Icon("Header Icon", panel.transform, config.pawSprite, new Vector2(0f, 1f),
            new Vector2(16f + config.statIconSize * 0.5f, -10f - 16f), Vector2.one * config.statIconSize);   // 레퍼런스: 머리글 앞 발바닥
        paw.preserveAspect = true;
        paw.raycastTarget = false;
        paw.enabled = paw.sprite != null;

        rowsRoot = RunUI.Rect("Rows", panel.transform, new Vector2(0f, 1f), new Vector2(16f, -44f), new Vector2(config.statPanelWidth - 30f, 100f));
        rowsRoot.pivot = new Vector2(0f, 1f);
        rowsRoot.anchoredPosition = new Vector2(16f, -44f);

        // 보관 슬롯 (왼쪽 아래)
        Image slot = RunUI.AssetPanel(SlotButtonName, root, config.slotSprite != null ? config.slotSprite : config.cardSprite, config.slotBorder,
            new Color(0.1f, 0.08f, 0.06f, 0.8f), Vector2.zero, config.storageSlotOffset, Vector2.one * config.storageSlotSize);
        slotImage = slot;
        slotBaseColor = slot.color;
        Button slotButton = slot.gameObject.AddComponent<Button>();
        slotButton.transition = Selectable.Transition.None;      // 투명도는 직접 조절
        slotButton.targetGraphic = slot;
        slotButton.navigation = new Navigation { mode = Navigation.Mode.None };
        slotButton.onClick.AddListener(UseSlot);

        slotIcon = RunUI.Icon("Icon", slot.transform, null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (config.storageSlotSize * 0.7f));
        slotLabel = RunUI.Label("Label", slot.transform, "", 20, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, -18f), new Vector2(config.storageSlotSize * 2f, 30f), 2f);
    }

    private void Update()
    {
        bool visible = config.showStatPanel && DayNightCycle.Instance != null && DayNightCycle.Instance.HudVisible;
        if (canvas.gameObject.activeSelf != visible)
        {
            canvas.gameObject.SetActive(visible);
        }
        if (!visible)
            return;

        if (Input.GetKeyDown(config.useItemKey))
        {
            UseSlot();
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }
        AlignSlot();
        FixHudRaycast();
    }

    // 위쪽 HUD 글자(타이머·헌터랭크·처치 수·경험치 바)는 클릭을 받을 필요가 없는데, 영역이 넓어 능력치 판 머리글 클릭을 가로챔
    private void FixHudRaycast()
    {
        if (hudRaycastFixed)
            return;
        HUD[] huds = FindObjectsOfType<HUD>(true);
        if (huds.Length == 0)
            return;
        foreach (HUD h in huds)
        {
            Transform root = h.transform;                     // HUD 글자 자신만 (옆의 필살기·회피 버튼은 건드리지 않음)
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g.GetComponentInParent<Selectable>() == null && g.GetComponent<Selectable>() == null)
                    g.raycastTarget = false;
            }
        }
        hudRaycastFixed = true;
    }

    // 보관 슬롯을 오른쪽 회피 버튼과 같은 높이·크기로 (화면 비율이 달라도 맞게, 캔버스가 달라 매번 계산)
    private DodgeCooldownUI dodge;
    private bool hudRaycastFixed;

    private void AlignSlot()
    {
        if (!config.alignSlotToSkillButton || slotImage == null)
            return;
        if (dodge == null) dodge = FindObjectOfType<DodgeCooldownUI>();
        if (dodge == null)
            return;
        RectTransform button = (RectTransform)dodge.transform;
        Vector3[] corners = new Vector3[4];
        button.GetWorldCorners(corners);
        float worldY = (corners[0].y + corners[2].y) * 0.5f;
        float worldSize = corners[2].y - corners[0].y;
        RectTransform slot = slotImage.rectTransform;
        float slotScale = Mathf.Max(0.0001f, slot.parent.lossyScale.y);
        Vector3 p = slot.TransformPoint(slot.rect.center);
        slot.position += new Vector3(0f, worldY - p.y, 0f);
        float size = worldSize / slotScale;
        if (Mathf.Abs(slot.sizeDelta.x - size) > 0.5f)
        {
            slot.sizeDelta = Vector2.one * size;
            slotIcon.rectTransform.sizeDelta = Vector2.one * (size * 0.7f);
        }
    }

    private void UseSlot()
    {
        if (ExtraCards.UseStored())
        {
            nextRefresh = 0f;
        }
    }

    private void Refresh()
    {
        // 보관 슬롯
        Sprite stored = ExtraCards.StoredIcon;
        slotIcon.sprite = stored;
        slotIcon.enabled = stored != null;
        slotLabel.text = stored != null ? "사용 [" + config.useItemKey + "]" : "빈 슬롯";
        slotLabel.color = stored != null ? Color.white : new Color(1f, 1f, 1f, 0.6f);

        // 비어 있으면 슬롯 틀을 투명하게 (게임 화면을 가리지 않도록)
        Color frame = slotBaseColor;
        slotImage.color = new Color(frame.r, frame.g, frame.b, frame.a * (stored != null ? 1f : config.emptySlotAlpha));

        header.text = collapsed ? "능력치 [+]" : "능력치 [-]";
        entries.Clear();
        if (!collapsed) BuildText();
        float rowsHeight = LayoutRows();

        float height = collapsed ? 52f : 60f + rowsHeight;
        panel.rectTransform.sizeDelta = new Vector2(config.statPanelWidth, height);
    }

    // 줄마다 [아이콘][글자], 아이콘이 없는 줄(효과)은 글자만 왼쪽부터. 반환: 전체 높이
    private float LayoutRows()
    {
        float icon = config.statIconSize;
        float gap = config.statIconGap;
        float width = config.statPanelWidth - 30f;
        float y = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i >= rowTexts.Count)
            {
                Image im = RunUI.Icon("Icon", rowsRoot, null, new Vector2(0f, 1f), Vector2.zero, Vector2.one * icon);
                im.preserveAspect = true;
                im.raycastTarget = false;
                rowIcons.Add(im);
                Text t = RunUI.Label("Text", rowsRoot, "", config.statFontSize, config.parchmentTextColor, TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 30f), 0f);
                t.rectTransform.pivot = new Vector2(0f, 1f);
                t.supportRichText = true;
                t.raycastTarget = false;
                RunUI.ApplyPanelFont(t);
                rowTexts.Add(t);
                Text v = RunUI.Label("Value", rowsRoot, "", config.statFontSize, config.parchmentTextColor, TextAnchor.UpperRight,
                    new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 30f), 0f);
                v.rectTransform.pivot = new Vector2(1f, 1f);
                v.supportRichText = true;
                v.raycastTarget = false;
                RunUI.ApplyPanelFont(v);
                rowValues.Add(v);
            }
            Sprite sprite = entries[i].Key;
            bool header = entries[i].Value.StartsWith("──");
            float textX = sprite != null || !header ? icon + gap : 0f;
            string line = entries[i].Value;
            int tab = line.IndexOf('\t');
            string nameText = tab >= 0 ? line.Substring(0, tab) : line;
            string valueText = tab >= 0 ? line.Substring(tab + 1) : "";
            Text text = rowTexts[i];
            Text value = rowValues[i];
            text.gameObject.SetActive(true);
            value.gameObject.SetActive(valueText.Length > 0);
            text.fontSize = config.statFontSize;
            value.fontSize = config.statFontSize;
            text.text = nameText;
            value.text = valueText;
            text.rectTransform.sizeDelta = new Vector2(width - textX, 30f);
            float h = Mathf.Max(icon, text.preferredHeight);
            float textY = -y - Mathf.Max(0f, (icon - text.preferredHeight) * 0.5f);
            text.rectTransform.sizeDelta = new Vector2(width - textX, h);
            text.rectTransform.anchoredPosition = new Vector2(textX, textY);
            value.rectTransform.sizeDelta = new Vector2(width * 0.5f, h);
            value.rectTransform.anchoredPosition = new Vector2(width, textY);
            Image im2 = rowIcons[i];
            im2.sprite = sprite;
            im2.enabled = sprite != null;
            im2.gameObject.SetActive(true);
            im2.rectTransform.sizeDelta = Vector2.one * icon;
            im2.rectTransform.anchoredPosition = new Vector2(icon * 0.5f, -y - icon * 0.5f);
            y += h + config.statRowGap;
        }
        for (int i = entries.Count; i < rowTexts.Count; i++)
        {
            rowTexts[i].gameObject.SetActive(false);
            rowValues[i].gameObject.SetActive(false);
            rowIcons[i].gameObject.SetActive(false);
        }
        rowsRoot.sizeDelta = new Vector2(width, y);
        return Mathf.Max(0f, y - config.statRowGap);
    }

    private void Add(string text, Sprite icon = null) => entries.Add(new KeyValuePair<Sprite, string>(icon, text));

    // 능력치 아이콘 (상점 능력치 아이콘 Ingame_shop_icon_1)
    private static Sprite Icon(RunStat stat)
    {
        StatCatalog cat = StatCatalog.Instance;
        StatCatalog.StatDef def = cat != null ? cat.Def(stat) : null;
        return def != null ? def.icon : null;
    }

    private void BuildText()
    {
        Player player = gm.player;

        // 최종 능력치
        float attack = Character.Damage * (1f + Gear.damage_stat) * PlayerBuff.Damage;
        float attackSpeed = Character.WeaponSpeed * (1f + Gear.stamina_stat) * PlayerBuff.Stamina;
        float cooldown = 1f - Character.WeaponRate * Mathf.Max(0.05f, 1f - Gear.rate_stat) * PlayerBuff.RateScale;
        float move = player.memoryspeed * PlayerBuff.MoveSpeed * MainWeaponController.MoveSpeedScale;
        float dodge = player.dodgeCooldown * RunStats.Get(RunStat.DodgeCooldown);

        Add("체력\t" + Mathf.CeilToInt(gm.health) + " / " + Mathf.CeilToInt(gm.maxHealth), Icon(RunStat.MaxHealthFlat));
        Add("공격력\t" + Percent(attack), Icon(RunStat.Damage));
        Add("공격 속도\t" + Percent(attackSpeed), Icon(RunStat.AttackSpeed));
        Add("쿨타임 감소\t" + Percent(cooldown), Icon(RunStat.Cooldown));
        Add("이동속도\t" + move.ToString("0.0"), Icon(RunStat.MoveSpeed));
        Add("회피 쿨타임\t" + dodge.ToString("0.0") + "초", Icon(RunStat.DodgeCooldown));

        // 기본값이 아닐 때만 표시하는 능력치
        Bonus("필살기 쿨타임", RunStats.Get(RunStat.UltCooldown) - 1f, Icon(RunStat.Cooldown));
        Bonus("필살기 위력", RunStats.Get(RunStat.UltPower) - 1f, Icon(RunStat.Damage));
        Bonus("무기 스킬 피해", ExtraCards.WeaponSkillScale - 1f, Icon(RunStat.SkillPower));
        Bonus("무기 스킬 쿨타임", RunStats.Get(RunStat.SkillCooldown) - 1f, Icon(RunStat.SkillCooldown));
        Bonus("보조무기 피해", RunStats.Get(RunStat.SubWeaponDamage) - 1f, Icon(RunStat.SubWeaponDamage));
        Bonus("받는 피해", RunStats.Get(RunStat.DamageTaken) - 1f, Icon(RunStat.DamageTaken));
        Bonus("무기·총알 크기", RunStats.Get(RunStat.WeaponSize) - 1f, Icon(RunStat.WeaponSize));
        Bonus("넉백", ExtraCards.KnockbackBonus, config.knockbackIcon);
        if (ExtraCards.PierceBonus > 0)
        {
            Add("관통\t+" + ExtraCards.PierceBonus, config.pierceIcon);
        }
        Bonus("경험치 범위", RunStats.Get(RunStat.ExpRange) - 1f, Icon(RunStat.ExpGain));
        Bonus("경험치 획득", RunStats.Get(RunStat.ExpGain) - 1f, Icon(RunStat.ExpGain));
        Bonus("코인 획득", RunStats.Get(RunStat.CoinDrop) - 1f, Icon(RunStat.CoinDrop));
        float regen = RunStats.Get(RunStat.Regen);
        if (regen > 0f)
        {
            Add("체력 회복\t" + regen.ToString("0.#") + "/초", Icon(RunStat.MaxHealthFlat));
        }
        float heal = RunStats.Get(RunStat.HealScale);
        if (heal < 0.999f)
        {
            Add(Paint(heal <= 0f ? "회복 불가" : "회복 효과 " + Percent(heal), config.statRiskColor), Icon(RunStat.MaxHealthFlat));
        }

        // 적용 중인 버프와 리스크 (아이콘 없이 글자만)
        List<string> effects = new List<string>();
        if (ExtraCards.FoodActive)
        {
            effects.Add(Paint("음식 섭취 " + Mathf.CeilToInt(ExtraCards.FoodRemaining) + "초", config.statBuffColor));
        }
        if (ExtraCards.LastStandActive)
        {
            effects.Add(Paint("한계 돌파 발동 +" + Mathf.RoundToInt(ExtraCards.LastStandBonus * 100f) + "%", config.statBuffColor));
        }

        // 기존 소모 아이템의 남은 시간
        if (Item.Exp_Bonus == 1)
        {
            effects.Add(Paint("격운티켓 (경험치 2배) " + Remaining(GameManager.ExpBonusEndTime) + "초", config.statBuffColor));
        }
        if (Item.Super_Magnet == 1)
        {
            effects.Add(Paint("슈퍼 그물망 " + Remaining(GameManager.SuperMagnetEndTime) + "초", config.statBuffColor));
        }
        if (!Mathf.Approximately(PlayerBuff.RawDamage, 1f) || !Mathf.Approximately(PlayerBuff.RawMoveSpeed, 1f))
        {
            effects.Add(Paint("필살기 효과 중", config.statBuffColor));
        }
        if (MainWeaponController.SkillModeActive)               // 능력치 받는 피해 감소만 있을 때는 표시하지 않음
        {
            effects.Add(Paint("무기 스킬 모드 중", config.statBuffColor));
        }
        if (RunStats.Get(RunStat.UltEnabled) < 0.5f)
        {
            effects.Add(Paint("필살기 봉인", config.statRiskColor));
        }
        if (DayNightCycle.IsNight && RunStats.Get(RunStat.NightHpAdd) > 0f)   // 1120: 야간 강화 제거, 체력 증가 합(피의 계약)만 표시
        {
            effects.Add(Paint("밤: 몬스터 체력 +" + Mathf.RoundToInt(RunStats.Get(RunStat.NightHpAdd) * 100f) + "%", config.statRiskColor));
        }
        foreach (RunStats.EffectLabel label in RunStats.Labels)
        {
            string text = label.life == ModifierLife.UntilMorning ? label.name + " (아침까지)" : "지옥: " + label.name;
            effects.Add(Paint(text, label.life == ModifierLife.UntilMorning ? new Color(0.75f, 0.4f, 0.05f) : config.statRiskColor));
        }

        if (effects.Count > 0)
        {
            Add("── 효과 ──");
            foreach (string e in effects) Add(e);
        }
    }

    private static int Remaining(float endTime)
    {
        return Mathf.CeilToInt(Mathf.Max(0f, endTime - Time.time));
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    private void Bonus(string name, float value, Sprite icon)
    {
        if (Mathf.Abs(value) < 0.005f)
            return;

        int percent = Mathf.RoundToInt(value * 100f);
        Add(name + "\t" + (percent > 0 ? "+" : "") + percent + "%", icon);
    }

    private static string Paint(string text, Color color)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
