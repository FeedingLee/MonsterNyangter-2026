using System.Collections.Generic;
using UnityEngine;

// 레벨업 추가 카드 (씬에 배치하지 않고 실행 중에 기존 카드를 복제해 만든다)
// 능력치 카드: 밀어내기, 무기 숙련, 관통 화살촉, 배수진
// 소모성 카드: 코인 주머니(즉시), 폭탄 통 / 마비 두꺼비 / 음식 섭취(보관 슬롯 1칸, 클릭 또는 Q로 사용)
// 수치 기준: 레벨업 카드 1장 = 상점 1회 구매 = 30코인 ≈ 상시 공격력 +6%
public static class ExtraCards
{
    private static readonly Dictionary<ItemData.ItemType, Item> cards = new Dictionary<ItemData.ItemType, Item>();
    private static float foodEndTime;

    // 보관 슬롯에 들어 있는 카드 (없으면 null)
    public static ItemData.ItemType? Stored { get; private set; }

    private static RunConfig Config => RunConfig.Instance;

    public static bool IsExtra(ItemData.ItemType type) => type >= ItemData.ItemType.Knockback;
    // 단계가 오르는 카드 (기존 능력치 카드 + 전투 기믹 카드)
    public static bool IsStatCard(ItemData.ItemType type) =>
        (type >= ItemData.ItemType.Knockback && type <= ItemData.ItemType.LastStand) || GimmickCards.IsNewGimmick(type);
    public static bool IsStorage(ItemData.ItemType type) => type >= ItemData.ItemType.BombBarrel && type <= ItemData.ItemType.Food;
    public static bool IsConsumable(ItemData.ItemType type) => type >= ItemData.ItemType.CoinPouch && type <= ItemData.ItemType.Food;

    // ---------------------------------------------------------------
    // 카드 생성 (LevelUp.Awake에서 호출)
    // ---------------------------------------------------------------
    public static Item[] CreateCards(Item[] existing)
    {
        cards.Clear();
        Stored = null;
        foodEndTime = 0f;
        GimmickCards.ResetRun();

        // 소모품 카드(회복약 등)를 틀로 사용 (없으면 첫 카드)
        Item template = null;
        foreach (Item item in existing)
        {
            if (item.data != null && item.data.itemType == ItemData.ItemType.Heal)
            {
                template = item;
                break;
            }
        }
        if (template == null && existing.Length > 0)
        {
            template = existing[0];
        }
        if (template == null)
            return existing;

        RunConfig c = Config;
        List<Item> all = new List<Item>(existing);
        int id = 14;
        all.Add(Create(template, id++, ItemData.ItemType.Knockback, "밀어내기", KnockbackIcon(c), c.knockbackPerLevel.Length));
        all.Add(Create(template, id++, ItemData.ItemType.WeaponMastery, "무기 숙련", c.masteryIcon, c.weaponMasteryPerLevel.Length));
        all.Add(Create(template, id++, ItemData.ItemType.Pierce, "관통 화살촉", c.pierceIcon, c.piercePerLevel.Length));
        all.Add(Create(template, id++, ItemData.ItemType.LastStand, "한계 돌파", c.lastStandIcon, c.lastStandPerLevel.Length));
        all.Add(Create(template, id++, ItemData.ItemType.CoinPouch, "금화 상자", c.coinPouchIcon, 1));
        all.Add(Create(template, id++, ItemData.ItemType.BombBarrel, "폭탄 통", c.bombIcon, 1));
        all.Add(Create(template, id++, ItemData.ItemType.ParalysisToad, "마비 두꺼비", c.toadIcon, 1));
        all.Add(Create(template, id++, ItemData.ItemType.Food, "음식 섭취", c.foodIcon, 1));

        // 전투 기믹 카드 (아이콘은 Ingame_Levelup_icon, C 항목 번호 순서)
        string[] names = { "회피 반격", "반격 본능", "집중", "연쇄 번개", "처치 폭발", "사냥 본능", "쌍둥이 무기", "보호막", "냉기 발자국", "위기 탈출", "생명의 일격" };
        for (int i = 0; i < names.Length; i++)
        {
            ItemData.ItemType type = ItemData.ItemType.DodgeCounter + i;
            all.Add(Create(template, id++, type, names[i], GimmickCards.Icon(type), GimmickCards.MaxLevel(type)));
        }
        // 기존 기믹(밀어내기·관통 화살촉·한계 돌파)도 같은 아이콘 묶음으로
        foreach (ItemData.ItemType type in new[] { ItemData.ItemType.Knockback, ItemData.ItemType.Pierce, ItemData.ItemType.LastStand })
        {
            Sprite icon = GimmickCards.Icon(type);
            if (icon != null && cards.TryGetValue(type, out Item item)) item.data.itemIcon = icon;
        }
        return all.ToArray();
    }

    // 밀어내기 아이콘: 지정한 아이콘이 없으면 Swing 원본에서 한 프레임을 잘라 사용
    private static Sprite knockbackCrop;

    private static Sprite KnockbackIcon(RunConfig c)
    {
        if (c.knockbackIcon != null)
            return c.knockbackIcon;
        if (c.knockbackIconSource == null)
            return null;

        if (knockbackCrop == null)
        {
            Texture2D texture = c.knockbackIconSource.texture;
            Rect rect = c.knockbackIconRect;
            rect.xMin = Mathf.Clamp(rect.xMin, 0f, texture.width);
            rect.yMin = Mathf.Clamp(rect.yMin, 0f, texture.height);
            rect.xMax = Mathf.Clamp(rect.xMax, rect.xMin + 1f, texture.width);
            rect.yMax = Mathf.Clamp(rect.yMax, rect.yMin + 1f, texture.height);
            knockbackCrop = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), c.knockbackIconSource.pixelsPerUnit);
            knockbackCrop.name = "Swing (Knockback Icon)";
        }
        return knockbackCrop;
    }

    private static Item Create(Item template, int id, ItemData.ItemType type, string name, Sprite icon, int maxLevel)
    {
        ItemData data = ScriptableObject.CreateInstance<ItemData>();
        data.name = name;
        data.itemType = type;
        data.itemId = id;
        data.itemName = name;
        data.itemDesc = "";
        data.itemIcon = icon != null ? icon : template.data.itemIcon;
        data.damages = new float[Mathf.Max(1, maxLevel)];     // 길이 = 최대 레벨 (만렙 판정에 사용)
        data.W_Speeds = new float[data.damages.Length];
        data.W_Rates = new float[data.damages.Length];
        data.counts = new int[data.damages.Length];
        data.weaponimage = new Sprite[0];

        GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
        clone.name = "Item " + name;
        clone.SetActive(false);

        Item item = clone.GetComponent<Item>();
        item.SetData(data);
        item.level = 0;
        item.weapon = null;
        item.gear = null;

        cards[type] = item;
        return item;
    }

    public static int Level(ItemData.ItemType type)
    {
        return cards.TryGetValue(type, out Item item) && item != null ? item.level : 0;
    }

    private static float Table(float[] table, int level)
    {
        return level <= 0 || table == null || table.Length == 0 ? 0f : table[Mathf.Min(level, table.Length) - 1];
    }

    // ---------------------------------------------------------------
    // 효과 수치 (각 스크립트에서 읽음)
    // ---------------------------------------------------------------
    public static float KnockbackBonus => Table(Config.knockbackPerLevel, Level(ItemData.ItemType.Knockback));
    public static float WeaponSkillBonus => Table(Config.weaponMasteryPerLevel, Level(ItemData.ItemType.WeaponMastery));
    public static float WeaponSkillScale => (1f + WeaponSkillBonus) * RunStats.Get(RunStat.SkillPower);   // 무기 숙련 x 상점 「스킬 위력」

    public static int PierceBonus
    {
        get
        {
            int level = Level(ItemData.ItemType.Pierce);
            int[] table = Config.piercePerLevel;
            return level <= 0 || table.Length == 0 ? 0 : table[Mathf.Min(level, table.Length) - 1];
        }
    }

    public static float LastStandBonus => Table(Config.lastStandPerLevel, Level(ItemData.ItemType.LastStand));

    // 배수진이 지금 발동 중인지 (체력이 기준 이하)
    public static bool LastStandActive
    {
        get
        {
            GameManager gm = GameManager.instance;
            return LastStandBonus > 0f && gm != null && gm.maxHealth > 0f && gm.health > 0f &&
                   gm.health <= gm.maxHealth * Config.lastStandThreshold;
        }
    }

    public static float FoodRemaining => Mathf.Max(0f, foodEndTime - Time.time);
    public static bool FoodActive => FoodRemaining > 0f;

    // 공격력 배율 (배수진 발동 + 음식)
    public static float DamageScale => (LastStandActive ? 1f + LastStandBonus : 1f) * (FoodActive ? 1f + Config.foodDamage : 1f);

    // 이동속도 배율 (음식)
    public static float MoveScale => FoodActive ? 1f + Config.foodMoveSpeed : 1f;

    // ---------------------------------------------------------------
    // 레벨업 선택지
    // ---------------------------------------------------------------
    // 한계 돌파 아이콘에 '강화됨' 표시: 오른쪽 위 작은 반짝임 + 붉은 위 화살표 (아이콘 자식으로 겹침)
    public static void DecorateLastStand(UnityEngine.UI.Image icon)
    {
        if (icon == null || icon.transform.parent == null) return;
        RectTransform holder = (RectTransform)icon.transform.parent;
        if (holder.Find(icon.name + " Boost Rise") != null) return;
        Vector2 size = icon.rectTransform.rect.size;
        Sprite[] rise = SpriteFx.Frames("S3", 3);                // 금빛 솟구침: 캐릭터 그림 뒤에서 기운이 오름
        if (rise != null && rise.Length > 4 && rise[4] != null)
        {
            UnityEngine.UI.Image a = new GameObject(icon.name + " Boost Rise", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            a.rectTransform.SetParent(holder, false);
            a.rectTransform.SetSiblingIndex(icon.transform.GetSiblingIndex());   // 아이콘 바로 뒤
            a.rectTransform.anchorMin = icon.rectTransform.anchorMin;
            a.rectTransform.anchorMax = icon.rectTransform.anchorMax;
            a.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition + new Vector2(0f, -size.y * 0.05f);
            a.rectTransform.localScale = icon.rectTransform.localScale;
            // 아이콘 둘레에 작은 불꽃 (sub_effect_1) 세 개가 일렁임 - 아이콘 뒤
            Sprite[] fire = SpriteFx.SmallFireFrames;
            Vector2[] spots = { new Vector2(-0.42f, -0.28f), new Vector2(0.42f, -0.28f), new Vector2(0f, 0.38f) };
            for (int i = 0; fire != null && fire.Length > 0 && fire[0] != null && i < spots.Length; i++)
            {
                UnityEngine.UI.Image f = new GameObject(icon.name + " Boost Fire", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                f.rectTransform.SetParent(holder, false);
                f.rectTransform.SetSiblingIndex(icon.transform.GetSiblingIndex());
                f.rectTransform.anchorMin = icon.rectTransform.anchorMin;
                f.rectTransform.anchorMax = icon.rectTransform.anchorMax;
                f.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition + Vector2.Scale(size, spots[i]);
                f.rectTransform.sizeDelta = size * (i == 2 ? 0.32f : 0.4f);
                f.rectTransform.localScale = icon.rectTransform.localScale;
                f.sprite = fire[0];
                f.preserveAspect = true;
                f.raycastTarget = false;
                UiSpriteLoop loop = f.gameObject.AddComponent<UiSpriteLoop>();
                loop.frames = fire;
                loop.offset = i * 0.13f;
            }
            a.rectTransform.sizeDelta = size * 0.9f;
            a.sprite = rise[4];
            a.color = new Color(1f, 0.95f, 0.75f, 0.85f);
            a.preserveAspect = true;
            a.raycastTarget = false;
        }
    }

    // 지금 선택지로 나올 수 있는지
    public static bool CanOffer(Item item)
    {
        ItemData.ItemType type = item.data.itemType;
        if (!IsExtra(type))
            return true;

        if (IsStorage(type) && Stored == type)
            return false;                                       // 같은 아이템을 이미 들고 있으면 안 나옴 (다른 보관형은 나오고, 고르면 슬롯이 교체됨)

        if (type == ItemData.ItemType.WeaponMastery)
            return false;                                       // 레벨업 선택지에서 제외 (데이터는 유지)

        if (type == ItemData.ItemType.CounterInstinct && MainWeaponController.Current == null)
            return false;                                       // 반격 본능: 메인무기 확정 후에만

        if (type == ItemData.ItemType.TwinWeapon && !HasTwinTarget())
            return false;                                       // 쌍둥이 무기: 보조 활·헤비보우건·쌍검이 있을 때만

        return true;
    }

    // 쌍둥이 무기 대상: 메인무기가 아닌 쌍검(0)·활(1)·헤비보우건(2) (시작 나무 막대기 제외)
    public static bool HasTwinTarget()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null) return false;
        foreach (Weapon w in gm.player.GetComponentsInChildren<Weapon>())
        {
            if (!w.IsMain && !w.IsStarter && w.Data != null && w.id >= 0 && w.id <= 2) return true;
        }
        return false;
    }

    // 카드 설명 (카드 폭에 맞게 짧게)
    public static string Describe(Item item)
    {
        RunConfig c = Config;
        int level = item.level;
        switch (item.data.itemType)
        {
            case ItemData.ItemType.Knockback:
                return "적을 더 멀리\n밀어냅니다\n" + Step(c.knockbackPerLevel, level) + "\n(보스 제외)";
            case ItemData.ItemType.WeaponMastery:
                return "무기 스킬 피해\n" + Step(c.weaponMasteryPerLevel, level);
            case ItemData.ItemType.Pierce:
                return "투사체가 적을\n더 관통합니다\n" + StepInt(c.piercePerLevel, level);
            case ItemData.ItemType.LastStand:
                return "체력 " + Mathf.RoundToInt(c.lastStandThreshold * 100f) + "%\n이하일 때\n공격력\n" + Step(c.lastStandPerLevel, level);
            // 소모 카드: 즉시/보관은 레벨 칸에 표시되므로 설명에는 넣지 않음
            case ItemData.ItemType.CoinPouch:
                return "냥냥코인을\n" + c.coinPouchRange.x + "~" + c.coinPouchRange.y + "개\n획득합니다";
            case ItemData.ItemType.BombBarrel:
                return c.bombDelay.ToString("0.#") + "초 뒤\n폭발해\n큰 피해";
            case ItemData.ItemType.ParalysisToad:
                return "주변 적을\n잠시\n마비시킵니다";
            case ItemData.ItemType.Food:
                return c.foodDuration.ToString("0.#") + "초 동안\n공격력과\n이동속도 증가";
            default:
                return "";
        }
    }

    // 현재 단계 → 다음 단계 (처음이면 다음 단계만)
    private static string Step(float[] table, int level)
    {
        string next = "+" + Mathf.RoundToInt(Table(table, level + 1) * 100f) + "%";
        string now = "+" + Mathf.RoundToInt(Table(table, level) * 100f) + "%";
        if (level <= 0) return next;
        return now == next ? now + " (최대)" : now + "→" + next;   // 최대 단계면 같은 값 반복 대신 (최대)
    }

    private static string StepInt(int[] table, int level)
    {
        int next = table.Length == 0 ? 0 : table[Mathf.Min(level + 1, table.Length) - 1];
        if (level <= 0)
            return "+" + next;
        int now = table[Mathf.Min(level, table.Length) - 1];
        return now == next ? "+" + now + " (최대)" : "+" + now + "→+" + next;
    }

    // 카드를 골랐을 때 (능력치 카드의 레벨 증가는 Item.OnClick에서 처리)
    public static void Apply(Item item)
    {
        RunConfig c = Config;
        switch (item.data.itemType)
        {
            case ItemData.ItemType.CoinPouch:
                CoinWallet.Add(Random.Range(c.coinPouchRange.x, c.coinPouchRange.y + 1));   // 20~40개 균등 확률
                PlayClip(c.coinPouchSound);
                break;
            case ItemData.ItemType.BombBarrel:
            case ItemData.ItemType.ParalysisToad:
            case ItemData.ItemType.Food:
                Stored = item.data.itemType;
                break;
        }
    }

    // ---------------------------------------------------------------
    // 보관 슬롯
    // ---------------------------------------------------------------
    public static Sprite StoredIcon
    {
        get
        {
            if (Stored == null || !cards.TryGetValue(Stored.Value, out Item item) || item == null)
                return null;
            return item.data.itemIcon;
        }
    }

    public static string StoredName
    {
        get
        {
            if (Stored == null || !cards.TryGetValue(Stored.Value, out Item item) || item == null)
                return "";
            return item.data.itemName;
        }
    }

    // 보관한 카드 사용 (게임 진행 중에만)
    public static bool UseStored()
    {
        GameManager gm = GameManager.instance;
        if (Stored == null || gm == null || !gm.isLive)
            return false;

        RunConfig c = Config;
        Vector3 position = gm.player.transform.position;
        switch (Stored.Value)
        {
            case ItemData.ItemType.BombBarrel:
                TimedDevice.Spawn(TimedDevice.Kind.Bomb, position);
                break;
            case ItemData.ItemType.ParalysisToad:
                TimedDevice.Spawn(TimedDevice.Kind.Toad, position);
                break;
            case ItemData.ItemType.Food:
                foodEndTime = Time.time + c.foodDuration;
                PlayClip(c.foodSound);
                SpriteFx.FoodAura(gm.player.transform, c.foodDuration);   // 버프 동안 발밑 금빛 고리 + 작은 불꽃
                break;
        }

        Stored = null;
        return true;
    }

    public static void PlayClip(AudioClip clip)
    {
        if (AudioManager.instance == null)
            return;

        if (clip != null)
        {
            AudioManager.instance.PlayClip(clip);
        }
        else
        {
            RunUI.ClickSound();                                  // 비어 있으면 카드 선택 소리
        }
    }
}
