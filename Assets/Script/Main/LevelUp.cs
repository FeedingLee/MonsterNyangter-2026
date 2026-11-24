using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LevelUp : MonoBehaviour
{
    RectTransform rect;
    Item[] items;

    public Object GM_Objcet;  // 게임매니저 오브젝트 가져오기

    public RectTransform Joy;
    public RectTransform Stick;

    public static LevelUp instance;

    // 레벨업 선택지 아이템 전체 (아침 선택지의 무기 레벨업 등에 사용)
    public Item[] Items => items;
    public bool LevelUpFinish = true;

    // 보너스 선택 (보너스 선택 젬): 게임 시작 시 레벨업 카드 선택을 먼저 N번 진행
    // 3장 모두 무작위(레벨 규칙 없음), 레벨·경험치는 그대로, 리롤은 평소처럼
    public int BonusTotal { get; private set; }
    public int BonusLeft { get; private set; }
    public bool BonusMode => BonusLeft > 0;

    public void ShowBonus(int count)
    {
        if (count <= 0) return;
        BonusTotal = count;
        BonusLeft = count;
        Show();
    }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        items = GetComponentsInChildren<Item>(true);
        items = ExtraCards.CreateCards(items);   // 밀어내기, 무기 숙련, 폭탄 통 등 추가 카드 (기존 카드를 복제)
        instance = this;
        BuildStatRow();

        // 화면은 LevelUpView(1920x1080 새 레벨업 화면)가 그림. 기존 판넬·카드는 데이터와 클릭 처리용으로 남기고 보이지 않게
        CanvasGroup hidden = gameObject.GetComponent<CanvasGroup>();
        if (hidden == null) hidden = gameObject.AddComponent<CanvasGroup>();
        hidden.alpha = 0f;
        hidden.blocksRaycasts = false;
        hidden.interactable = false;
    }

    // 이번 선택지 (LevelUpView가 표시)
    public readonly List<Item> CurrentChoices = new List<Item>();

    // 현재 제목 (보너스 레벨업 문구 포함)
    public string TitleText => titleText != null ? titleText.text : "헌터랭크 상승!";

    [Header("# 능력치 현황 (레벨업 판넬 아래쪽)")]
    public float statRowTopY = -24f;          // 기본 능력치 5개 줄 높이 (판넬을 키운 뒤 기준)
    public float statRowBottomY = -50f;       // 추가 능력치 카드 4개 줄 높이
    public float statSpacing = 24f;
    public float statSlotScale = 0.1f;        // 능력치 칸 크기 (원래 크기, 글자가 깨지지 않게)
    public float inventoryExtraHeight = 22f;  // 두 번째 줄을 위해 판넬(Inventory)을 아래로 늘리는 높이
    public float titleRaise = 10f;            // 판넬이 커진 만큼 제목을 올림
    [Min(0.05f)] public float titleSparkleInterval = 0.8f;   // 헌터랭크 상승 문구 주위 반짝임 간격

    private float titleSparkleTime;
    private float nextTitleSparkle;

    // 레벨업 창이 열려 있는 동안 제목 주위에 은은한 반짝임
    private void Update()
    {
        if (LevelUpFinish || rect == null || rect.localScale.x < 0.5f)
            return;
        titleSparkleTime += Time.unscaledDeltaTime;
        if (titleSparkleTime < nextTitleSparkle)
            return;
        nextTitleSparkle = titleSparkleTime + titleSparkleInterval * Random.Range(0.7f, 1.3f);
        Transform title = transform.Find("Text Title");
        RectTransform tr = title != null ? (RectTransform)title : rect;
        Vector2 half = tr.rect.size * 0.5f;
        Vector2 p = new Vector2(Random.Range(-half.x, half.x) * 0.95f, Random.Range(-half.y, half.y));
        UiFx.Play(UiFx.Kind.Sparkle, tr, p, Random.Range(7f, 11f), new Color(1f, 1f, 1f, 0.55f), 12f);   // 아주 작게, 시선을 뺏지 않게
    }

    // 능력치 현황: 기존 5칸(쿨타임·공격력·무기 강화·이동속도·스태미나)만 있어 레벨업으로 얻는 추가 능력치
    // (밀어내기·무기 숙련·관통 화살촉·한계 돌파)가 보이지 않던 것을 두 번째 줄로 추가. 무기 레벨 리본은 판넬과 어울리는 갈색 바로
    private void BuildStatRow()
    {
        Inventory template = null;
        List<Inventory> statSlots = new List<Inventory>();
        foreach (Inventory inv in GetComponentsInChildren<Inventory>(true))
        {
            Transform nameTag = inv.transform.Find("NameTag");
            if (nameTag != null)
            {
                RunConfig rc = RunConfig.Instance;
                UnityEngine.UI.Image tag = nameTag.GetComponent<UnityEngine.UI.Image>();
                if (tag != null && rc != null && rc.buttonBrown != null)
                {
                    tag.sprite = rc.buttonBrown;
                    tag.color = Color.white;
                }
                continue;                                     // 무기 칸
            }
            if (inv.transform.childCount > 0 && inv.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 1)
            {
                Item linked = inv.weapon_data != null ? inv.weapon_data.GetComponent<Item>() : null;
                if (linked != null && linked.data.itemType == ItemData.ItemType.Weapon_Upgrade)
                {
                    inv.gameObject.SetActive(false);         // '무기 강화'는 레벨업 선택지에 나오지 않아 얻을 수 없는 능력치라 숨김
                    continue;
                }
                statSlots.Add(inv);
                if (template == null) template = inv;
            }
        }
        if (template == null)
            return;

        // 판넬 키우기: 위아래로 반씩 늘리고 안쪽 칸들은 위로 올려 무기 칸과 위 테두리 사이는 그대로, 제목은 늘어난 만큼 올림
        RectTransform panelRect = (RectTransform)template.transform.parent;
        float grow = inventoryExtraHeight;
        panelRect.sizeDelta += new Vector2(0f, grow);
        foreach (Transform c in panelRect) ((RectTransform)c).anchoredPosition += new Vector2(0f, grow * 0.5f);
        Transform titleT = transform.Find("Text Title");
        if (titleT != null) ((RectTransform)titleT).anchoredPosition += new Vector2(0f, titleRaise);

        List<Item> extras = new List<Item>();
        foreach (Item it in items)
        {
            if (it == null || it.data == null) continue;
            ItemData.ItemType type = it.data.itemType;
            if (type == ItemData.ItemType.Knockback || type == ItemData.ItemType.WeaponMastery
                || type == ItemData.ItemType.Pierce || type == ItemData.ItemType.LastStand)
                extras.Add(it);
        }

        int kb = extras.FindIndex(e => e.data.itemType == ItemData.ItemType.Knockback);
        int ls = extras.FindIndex(e => e.data.itemType == ItemData.ItemType.LastStand);
        if (kb >= 0 && ls >= 0) { Item t = extras[kb]; extras[kb] = extras[ls]; extras[ls] = t; }   // 밀어내기 ↔ 한계 돌파 자리

        statSlots.Sort((a, b) => ((RectTransform)a.transform).anchoredPosition.x.CompareTo(((RectTransform)b.transform).anchoredPosition.x));
        foreach (Inventory s in statSlots) s.transform.localScale = Vector3.one * statSlotScale;
        PlaceRow(statSlots, statRowTopY);

        List<Inventory> added = new List<Inventory>();
        foreach (Item it in extras)
        {
            Inventory slot = Instantiate(template, template.transform.parent);
            slot.name = "Stat " + it.data.itemName;
            slot.weapon_data = it.gameObject;
            slot.level = 0;                                  // 0레벨은 판넬 기본 글자(Lv.00) 그대로
            UnityEngine.UI.Image icon = slot.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>();
            if (icon != null)
            {
                icon.sprite = it.data.itemIcon;
                icon.preserveAspect = true;
                if (it.data.itemType == ItemData.ItemType.Pierce)
                    icon.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);   // 세로로 긴 화살촉은 카드처럼 대각선으로
                if (it.data.itemType == ItemData.ItemType.Knockback)
                    icon.transform.localScale = Vector3.one * 2f;                    // 가로로 긴 휘두르기 그림이 작아 보여 키움
                if (it.data.itemType == ItemData.ItemType.WeaponMastery)
                    icon.transform.localScale = Vector3.one * 1.4f;                  // 교차한 검 그림도 작아 보여 키움
            }
            slot.ChangeImg = it.data.itemIcon;
            slot.transform.localScale = Vector3.one * statSlotScale;
            if (it.data.itemType == ItemData.ItemType.LastStand && icon != null)
                ExtraCards.DecorateLastStand(icon);           // '강화됨' 표시
            added.Add(slot);
        }
        PlaceRow(added, statRowBottomY);
    }

    private void PlaceRow(List<Inventory> row, float y)
    {
        float start = -(row.Count - 1) * statSpacing * 0.5f;
        for (int i = 0; i < row.Count; i++)
            ((RectTransform)row[i].transform).anchoredPosition = new Vector2(start + i * statSpacing, y);
    }

    // 레벨업 창 제목 (17레벨 등 소모품만 나오는 레벨에는 보너스 레벨업 문구로 바꿈)
    private UnityEngine.UI.Text titleText;
    private string defaultTitle;

    private void UpdateTitle()
    {
        if (titleText == null)
        {
            Transform title = transform.Find("Text Title");
            if (title == null)
                return;
            titleText = title.GetComponent<UnityEngine.UI.Text>();
            defaultTitle = titleText.text;
        }

        if (BonusMode)
        {
            titleText.text = "보너스 선택 " + (BonusTotal - BonusLeft + 1) + "/" + BonusTotal;
            return;
        }
        int level = GM_Objcet.GetComponent<GameManager>().level;
        bool bonus = level % Mathf.Max(1, GimmickCatalog.Instance.consumableOnlyEvery) == 0;   // Next()와 같은 규칙: 17의 배수는 소모품만 (5의 배수보다 먼저)
        titleText.text = bonus ? "보너스 레벨업!\n무기는 나오지 않습니다" : defaultTitle;
    }

    public void Show() // 아이템 창 띄우기
    {
        LevelUpFinish = false;
        UpdateTitle();
        Next();
        rect.localScale = Vector3.one;
        Joy.gameObject.SetActive(false);
        Stick.gameObject.SetActive(false);
        GameManager.instance.Stop();
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Player_LevelUp);
        // 등장 연출: 판넬 위쪽에서 금빛 빛줄기, 양옆 반짝임
        // 등장: 판넬 모서리에 작은 반짝임 (화면을 가리지 않게)
        UiFx.Play(UiFx.Kind.Sparkle, rect, new Vector2(-rect.rect.width * 0.42f, rect.rect.height * 0.36f), rect.rect.width * 0.12f, null, 16f);
        UiFx.Play(UiFx.Kind.Sparkle, rect, new Vector2(rect.rect.width * 0.42f, rect.rect.height * 0.28f), rect.rect.width * 0.1f, null, 16f);
        titleSparkleTime = 0f;
        LevelUpView.Open(this);                              // 새 레벨업 화면
        AudioManager.instance.EffectBgm(true);
    }

    public void Hide() // 아이템 창 숨기기
    {
        // 보너스 선택이 남았으면 창을 닫지 않고 다음 선택으로
        if (BonusMode && --BonusLeft > 0)
        {
            UpdateTitle();
            Next();
            LevelUpView.Close();
            LevelUpView.Open(this);
            return;
        }
        LevelUpFinish = true;
        rect.localScale = Vector3.zero;
        LevelUpView.Close();
        Joy.gameObject.SetActive(true);
        Stick.gameObject.SetActive(true);
        GameManager.instance.Resume();                       // 레벨업 카드 선택은 클릭음 없음
        AudioManager.instance.EffectBgm(false);
    }

    public void Select(int index)
    {
        items[index].OnClick();
    }

    // 현재 선택지를 새로 뽑음 (이전과 같은 선택지가 다시 나올 수 있음)
    public void Reroll()
    {
        Next();
        LevelUpView.Refresh();
        LevelUpView.PlayRerollFx();                          // 리롤: 새 카드 모서리에 작은 반짝임 (화면을 가리지 않게)
    }

    // 레벨업 선택 규칙 (우선순위: 17의 배수 → 5의 배수 → 짝수·홀수)
    // - 17의 배수: 소모품만 / 5의 배수: 무기만
    // - 짝수: 무기 1장 보장 + 나머지 무작위 / 홀수: 모두 무작위
    // - 무작위 칸: consumableSlotRate 확률로 소모품, 아니면 무기·기믹 카드 중 하나 (같은 카드 두 장 금지)
    // - 낼 무기·기믹 카드가 없으면 소모품으로 채움
    private void Next()
    {
        // 1. 모든 아이템 비활성화
        foreach (Item item in items)
        {
            item.gameObject.SetActive(false);
        }

        // 2. 지금 내보낼 수 있는 카드 (제거한 능력치 카드, 만렙, 메인무기, 조건 미달 카드는 제외)
        List<Item> offerable = new List<Item>();
        foreach (Item item in items)
        {
            if (!IsRemoved(item.data.itemType) && CanOffer(item))
            {
                offerable.Add(item);
            }
        }

        List<Item> weapons = offerable.FindAll(IsWeapon);
        List<Item> consumables = offerable.FindAll(IsConsumable);
        List<Item> gimmicks = offerable.FindAll(item => GimmickCards.IsGimmick(item.data.itemType));
        List<Item> battle = new List<Item>(weapons);
        battle.AddRange(gimmicks);

        GimmickCatalog rules = GimmickCatalog.Instance;
        List<Item> chosen = new List<Item>();
        int count = RunStats.LevelUpChoices;                 // 3장 (저주받은 금화를 고르면 2장)
        int level = GM_Objcet.GetComponent<GameManager>().level;

        if (BonusMode)
        {
            // 보너스 선택: 레벨 규칙 없이 무작위 칸만 (consumableSlotRate, 등장 조건은 그대로)
            while (chosen.Count < count)
            {
                bool consumable = Random.value < rules.consumableSlotRate;
                if (TakeOne(consumable ? consumables : battle, chosen)) continue;
                if (TakeOne(consumables, chosen)) continue;
                if (!TakeOne(battle, chosen)) break;
            }
        }
        else if (level % Mathf.Max(1, rules.consumableOnlyEvery) == 0)
        {
            TakeRandom(consumables, chosen, count);          // 소모품만
        }
        else if (level % Mathf.Max(1, rules.weaponOnlyEvery) == 0)
        {
            TakeRandom(weapons, chosen, count);              // 무기만 (모자라면 아래에서 소모품으로)
        }
        else
        {
            if (level % 2 == 0 && !TakeOne(weapons, chosen))   // 짝수: 무기 1장 보장 (없으면 소모품)
            {
                TakeOne(consumables, chosen);
            }
            while (chosen.Count < count)
            {
                bool consumable = Random.value < rules.consumableSlotRate;
                if (TakeOne(consumable ? consumables : battle, chosen))
                    continue;
                if (TakeOne(consumables, chosen))            // 무기·기믹이 없으면 소모품
                    continue;
                if (!TakeOne(battle, chosen))
                    break;
            }
        }

        // 3. 장수가 모자라면 남은 소모품으로 채움
        TakeRandom(consumables, chosen, count);

        CurrentChoices.Clear();
        CurrentChoices.AddRange(chosen);
        foreach (Item item in chosen)
        {
            item.gameObject.SetActive(true);
        }
    }

    // 레벨업 선택지에서 뺀 카드 (데이터는 그대로): 공격력·쿨타임·스태미나·이동속도 증가, 무기 강화(→ 쌍둥이 무기), 무기 숙련
    public static bool IsRemoved(ItemData.ItemType type)
    {
        switch (type)
        {
            case ItemData.ItemType.Rate_Up:
            case ItemData.ItemType.Speed_Up:
            case ItemData.ItemType.Damage_Up:
            case ItemData.ItemType.Stamina_Up:
            case ItemData.ItemType.Weapon_Upgrade:
            case ItemData.ItemType.WeaponMastery:
                return true;
            default:
                return false;
        }
    }

    // pool에서 chosen에 없는 카드 1장
    private static bool TakeOne(List<Item> pool, List<Item> chosen)
    {
        int before = chosen.Count;
        TakeRandom(pool, chosen, before + 1);
        return chosen.Count > before;
    }

    // pool에서 chosen에 없는 카드를 무작위로 골라 chosen이 total장이 될 때까지 채움
    private static void TakeRandom(List<Item> pool, List<Item> chosen, int total)
    {
        List<Item> left = pool.FindAll(item => !chosen.Contains(item));
        while (chosen.Count < total && left.Count > 0)
        {
            int pick = Random.Range(0, left.Count);
            chosen.Add(left[pick]);
            left.RemoveAt(pick);
        }
    }

    public static bool IsWeapon(Item item)
    {
        return item.data.itemType <= ItemData.ItemType.Lance;
    }

    public static bool IsConsumable(Item item)
    {
        switch (item.data.itemType)
        {
            case ItemData.ItemType.Heal:
            case ItemData.ItemType.Magnet:
            case ItemData.ItemType.Exp_Coupon:
            case ItemData.ItemType.Super_Magnet:
                return true;
            default:
                return ExtraCards.IsConsumable(item.data.itemType);   // 코인 주머니, 폭탄 통, 마비 두꺼비, 음식 섭취
        }
    }

    // 선택지로 내보낼 수 있는지 (이미 표시 중, 만렙, 메인무기는 불가)
    private static bool CanOffer(Item item)
    {
        if (item.gameObject.activeSelf)
            return false;

        if (item.level == item.data.damages.Length)
            return false;

        if (!ExtraCards.CanOffer(item))                      // 무기 숙련은 메인무기 확정 후, 보관형은 슬롯이 비어 있을 때만
            return false;

        return item.weapon == null || !item.weapon.IsMain;
    }
}
