using System;
using System.Collections.Generic;
using UnityEngine;

// 능력치 분야
public enum StatCategory { Attack, Survival, Utility }

// 표시 단위: % / 고정값 / 횟수
public enum StatUnit { Percent, Flat, Count }

// 증가형(모든 출처 합산 후 한 번 곱함) / 감소형(출처마다 곱함) / 더하기(고정값·횟수)
public enum StatDirection { Increase, Decrease, Additive }

// 능력치 출처 (출처별로 따로 누적)
public enum StatSource { Shop, Gem, SpecialGem, Morning }

// 상점 카드 등급
public enum ShopGrade { Bronze, Silver, Gold }

// 능력치 공통 정의 (상점·젬·특수 젬·아침 선택지 공용)
// Assets/Resources/StatCatalog 에셋을 실행 중에 불러와 사용한다. (메뉴 MonNyang > 상점 UI 에셋 설정 으로 생성·스프라이트 연결)
[CreateAssetMenu(fileName = "StatCatalog", menuName = "Scriptble Object/StatCatalog")]
public class StatCatalog : ScriptableObject
{
    private static StatCatalog instance;

    public static StatCatalog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<StatCatalog>("StatCatalog");
                if (instance == null)
                {
                    Debug.LogWarning("Resources/StatCatalog 에셋이 없어 기본값으로 실행합니다. (메뉴 MonNyang > 상점 UI 에셋 설정)");
                    instance = CreateInstance<StatCatalog>();
                }
            }
            return instance;
        }
    }

    [Serializable]
    public class StatDef
    {
        public RunStat stat;
        public string name;
        public StatCategory category;
        public StatUnit unit;
        public StatDirection direction;
        public Sprite icon;

        public StatDef() { }

        public StatDef(RunStat stat, string name, StatCategory category, StatUnit unit, StatDirection direction)
        {
            this.stat = stat;
            this.name = name;
            this.category = category;
            this.unit = unit;
            this.direction = direction;
        }
    }

    // 상점에 나오는 능력치와 등급별 수치 (Percent는 0.02 = 2%, 감소형은 음수)
    [Serializable]
    public class ShopStat
    {
        public RunStat stat;
        public float bronze;
        public float silver;
        public float gold;

        public ShopStat() { }

        public ShopStat(RunStat stat, float bronze, float silver, float gold)
        {
            this.stat = stat;
            this.bronze = bronze;
            this.silver = silver;
            this.gold = gold;
        }

        public float Value(ShopGrade grade) => grade == ShopGrade.Bronze ? bronze : grade == ShopGrade.Silver ? silver : gold;
    }

    // 골드 특수 선택지 (이득 + 대가)
    [Serializable]
    public class SpecialDef
    {
        public string name;
        public Sprite icon;
        public RunStat gainStat;
        public float gain;
        public RunStat costStat;
        public float cost;

        public SpecialDef() { }

        public SpecialDef(string name, RunStat gainStat, float gain, RunStat costStat, float cost)
        {
            this.name = name;
            this.gainStat = gainStat;
            this.gain = gain;
            this.costStat = costStat;
            this.cost = cost;
        }
    }

    [Header("# 능력치 정의 16종 (이름, 분야, 단위, 증가·감소)")]
    public List<StatDef> stats = new List<StatDef>
    {
        new StatDef(RunStat.Damage, "공격력", StatCategory.Attack, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.AttackSpeed, "공격속도", StatCategory.Attack, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.Cooldown, "공격 쿨타임", StatCategory.Attack, StatUnit.Percent, StatDirection.Decrease),
        new StatDef(RunStat.SkillPower, "무기 스킬 피해", StatCategory.Attack, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.SkillCooldown, "무기 스킬 쿨타임", StatCategory.Attack, StatUnit.Percent, StatDirection.Decrease),
        new StatDef(RunStat.SubWeaponDamage, "보조무기 피해", StatCategory.Attack, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.MaxHealthFlat, "최대 체력", StatCategory.Survival, StatUnit.Flat, StatDirection.Additive),
        new StatDef(RunStat.DamageTaken, "받는 피해", StatCategory.Survival, StatUnit.Percent, StatDirection.Decrease),
        new StatDef(RunStat.DodgeCooldown, "구르기 쿨타임", StatCategory.Survival, StatUnit.Percent, StatDirection.Decrease),
        new StatDef(RunStat.MoveSpeed, "이동속도", StatCategory.Survival, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.ExpGain, "경험치 획득량", StatCategory.Utility, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.CoinDrop, "코인 획득량", StatCategory.Utility, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.Rerolls, "리롤 횟수", StatCategory.Utility, StatUnit.Count, StatDirection.Additive),
        new StatDef(RunStat.WeaponSize, "무기·총알 크기", StatCategory.Utility, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.UltPower, "필살기 위력", StatCategory.Utility, StatUnit.Percent, StatDirection.Increase),
        new StatDef(RunStat.StartCoins, "시작 냥냥코인", StatCategory.Utility, StatUnit.Count, StatDirection.Additive),
        new StatDef(RunStat.BonusPicks, "보너스 선택", StatCategory.Utility, StatUnit.Count, StatDirection.Additive),
    };

    [Header("# 계산 하한·상한")]
    [Range(0.05f, 1f)] public float attackCdFloor = 0.4f;          // 공격 쿨타임 최종 배율 하한
    [Range(0.05f, 1f)] public float skillCdFloor = 0.5f;           // 전용 스킬 쿨타임 최종 배율 하한
    [Min(0f)] public float coinGainCap = 0.4f;                     // 코인 획득량 합산 증가 최대 (+40%)
    [Range(0.01f, 1f)] public float minimumScale = 0.05f;          // 어떤 배율도 이 아래로는 내려가지 않음

    [Header("# 상점 가격 (냥냥코인)")]
    [Min(0)] public int shopBasePrice = 30;
    [Min(0)] public int shopPriceStep = 3;
    [Min(0)] public int shopPriceMax = 60;

    [Header("# 상점 등급 확률 (브론즈 / 실버 / 골드, 합이 100이 아니어도 비율로 계산)")]
    public Vector3 gradeRates = new Vector3(60f, 30f, 10f);
    [Range(0f, 1f)] public float goldSpecialRate = 0.3f;           // 골드 카드 중 특수 선택지 확률
    [Min(1)] public int shopCardCount = 3;

    [Header("# 상점 능력치 14종 (등급별 수치, 아이콘은 Ingame_shop_icon_1 순서)")]
    public List<ShopStat> shopStats = new List<ShopStat>
    {
        new ShopStat(RunStat.Damage, 0.02f, 0.04f, 0.07f),
        new ShopStat(RunStat.AttackSpeed, 0.02f, 0.04f, 0.07f),
        new ShopStat(RunStat.Cooldown, -0.02f, -0.04f, -0.07f),
        new ShopStat(RunStat.SkillPower, 0.04f, 0.08f, 0.14f),
        new ShopStat(RunStat.SkillCooldown, -0.03f, -0.06f, -0.10f),
        new ShopStat(RunStat.SubWeaponDamage, 0.04f, 0.08f, 0.14f),
        new ShopStat(RunStat.MaxHealthFlat, 5f, 10f, 18f),
        new ShopStat(RunStat.DamageTaken, -0.02f, -0.04f, -0.07f),
        new ShopStat(RunStat.DodgeCooldown, -0.03f, -0.06f, -0.10f),
        new ShopStat(RunStat.MoveSpeed, 0.02f, 0.04f, 0.07f),
        new ShopStat(RunStat.ExpGain, 0.03f, 0.06f, 0.10f),
        new ShopStat(RunStat.CoinDrop, 0.04f, 0.08f, 0.14f),
        new ShopStat(RunStat.Rerolls, 1f, 1f, 2f),
        new ShopStat(RunStat.WeaponSize, 0.04f, 0.08f, 0.14f),
    };

    [Header("# 골드 특수 선택지 (아이콘은 Ingame_shop_icon_2 순서)")]
    public List<SpecialDef> specials = new List<SpecialDef>
    {
        new SpecialDef("유리 대포", RunStat.Damage, 0.14f, RunStat.MaxHealthFlat, -8f),
        new SpecialDef("무거운 발톱", RunStat.Damage, 0.14f, RunStat.AttackSpeed, -0.04f),
        new SpecialDef("날렵한 손", RunStat.Cooldown, -0.14f, RunStat.Damage, -0.04f),
        new SpecialDef("광전사", RunStat.AttackSpeed, 0.14f, RunStat.DamageTaken, 0.04f),
        new SpecialDef("한 방 승부", RunStat.SkillPower, 0.28f, RunStat.SkillCooldown, 0.06f),
        new SpecialDef("보조무기 특화", RunStat.SubWeaponDamage, 0.28f, RunStat.SkillPower, -0.08f),
        new SpecialDef("철갑", RunStat.DamageTaken, -0.14f, RunStat.MoveSpeed, -0.04f),
        new SpecialDef("질주 본능", RunStat.MoveSpeed, 0.14f, RunStat.DodgeCooldown, 0.08f),
        new SpecialDef("사냥꾼의 직감", RunStat.ExpGain, 0.20f, RunStat.CoinDrop, -0.10f),
    };

    [Header("# 상점 UI 스프라이트 (메뉴 MonNyang > 상점 UI 에셋 설정 으로 채움)")]
    public Sprite shopPanel;                                       // 메인 판넬 (제목 판·천 장식 포함, 비율 유지)
    public Sprite cardBronze;
    public Sprite cardSilver;
    public Sprite cardGold;
    public Sprite catIdle;                                         // 상점 고양이 (판넬과 따로 배치)
    public Sprite catSurprise;
    public Sprite catWave;
    public Sprite lantern;
    public Sprite coinBar;                                         // 냥냥코인 표시 띠
    public Sprite coinIcon;
    public Sprite buttonBrown;
    public Sprite buttonGreen;
    public Sprite divider;
    public Sprite paw;
    public Sprite specialTag;                                      // 특수 선택지 표시 (붉은 깃발)
    public Sprite titlePlate;                                      // 판넬 가운데에 덮는 제목 판 (판넬 그림 속 제목 판은 가운데가 아니라 덮어서 가림)
    public Sprite iconExit;
    public Sprite iconSelect;
    public Sprite[] specialIcons;                                  // specials 순서대로 (Ingame_shop_icon_2)

    [Header("# 상점 UI 배치 (상점 캔버스 기준, 가운데 0,0)")]
    public Vector2 panelPosition = new Vector2(0f, -40f);
    [Min(10f)] public float panelHeight = 600f;                    // 너비는 그림 비율로
    public Vector2 titlePlateAnchor = new Vector2(0.539f, 0.075f); // 판넬 그림 속 제목 판 중심 (왼쪽 위 0,0 → 오른쪽 아래 1,1). 높이만 씀
    public Vector2 titlePlateSize = new Vector2(410f, 88f);        // 가운데 제목 판 크기 (그림 속 제목 판을 덮을 만큼)
    [Min(1)] public int titleFontSize = 40;
    public float subtitleY = 192f;                                 // 판넬 중심 기준
    [Min(1)] public int subtitleFontSize = 24;
    public Vector2 catPosition = new Vector2(-285f, 284f);         // 고양이 (판넬과 독립)
    [Min(10f)] public float catHeight = 140f;
    public Vector2 lanternPosition = new Vector2(-405f, 262f);
    [Min(10f)] public float lanternHeight = 160f;
    public Vector2 coinBarPosition = new Vector2(320f, 292f);
    public Vector2 coinBarSize = new Vector2(300f, 54f);
    [Min(1)] public int coinFontSize = 26;
    public float cardY = 0f;                                     // 판넬 중심 기준
    [Min(10f)] public float cardHeight = 320f;
    public float cardSpacing = 285f;
    public Vector3 gradeLabelFromTop = new Vector3(28f, 28f, 36f);  // 등급 글자 위치: 카드 그림 위쪽에서 이름판 가운데까지 (브론즈, 실버, 골드, 그림 픽셀)
    public Vector2 specialTagPosition = new Vector2(-0.13f, -0.2f); // 특수 깃발: 카드 오른쪽 위 기준 (카드 크기 비율)
    public Vector2 specialTagSize = new Vector2(38f, 44f);
    [Min(1f)] public float cardHoverScale = 1.03f;
    [Min(1f)] public float cardSelectedScale = 1.07f;
    public Color cardDimColor = new Color(0.78f, 0.75f, 0.72f, 1f); // 다른 카드를 고르면 나머지는 살짝 어둡게
    public Vector2 buttonSize = new Vector2(240f, 62f);
    public float buttonY = -214f;                                  // 판넬 중심 기준
    public float buttonX = 285f;
    [Min(1)] public int buttonFontSize = 28;

    [Header("# 상점 카드 등장·연출 (초, 실제 시간)")]
    [Min(0.01f)] public float dealTime = 0.35f;                    // 카드가 스르륵 올라오는 시간
    [Min(0f)] public float dealStagger = 0.09f;                    // 카드 사이 간격
    public float dealOffset = -70f;                                // 아래에서 이만큼 떨어진 곳에서 올라옴
    [Min(0.01f)] public float pickOutTime = 0.3f;                  // 고른 카드가 떠오르며 사라지는 시간
    [Min(0f)] public float catReactTime = 0.9f;                    // 고양이가 손 흔들기·놀람을 보여 주는 시간

    [Header("# 상점 카드 색")]
    public Color gradeTextColor = Color.white;
    public Color cardTextColor = new Color(0.27f, 0.15f, 0.07f, 1f);
    public Color goodColor = new Color(0.2f, 0.55f, 0.12f, 1f);    // 이득 (초록)
    public Color badColor = new Color(0.78f, 0.18f, 0.12f, 1f);    // 대가 (빨강)
    public Color mutedColor = new Color(0.45f, 0.36f, 0.28f, 1f);
    public Color valueBandColor = new Color(0.55f, 0.38f, 0.22f, 0.14f);

    // ---------------- 조회 ----------------
    private Dictionary<RunStat, StatDef> lookup;

    public StatDef Def(RunStat stat)
    {
        if (lookup == null || lookup.Count != stats.Count)
        {
            lookup = new Dictionary<RunStat, StatDef>();
            foreach (StatDef d in stats)
            {
                if (d != null) lookup[d.stat] = d;
            }
        }
        return lookup.TryGetValue(stat, out StatDef def) ? def : null;
    }

    private void OnValidate()
    {
        lookup = null;
    }

    public string Name(RunStat stat)
    {
        StatDef d = Def(stat);
        return d != null ? d.name : stat.ToString();
    }

    public Sprite Icon(RunStat stat)
    {
        StatDef d = Def(stat);
        return d != null ? d.icon : null;
    }

    // 변화량 표시 (예: +4%, -2%, +10, +1회)
    public string FormatDelta(RunStat stat, float value)
    {
        StatDef d = Def(stat);
        StatUnit unit = d != null ? d.unit : StatUnit.Percent;
        switch (unit)
        {
            case StatUnit.Flat:
                return Signed(Mathf.RoundToInt(value));
            case StatUnit.Count:
                return Signed(Mathf.RoundToInt(value)) + "회";
            default:
                return Signed(Mathf.RoundToInt(value * 100f)) + "%";
        }
    }

    private static string Signed(int v) => (v >= 0 ? "+" : "") + v;

    // n번째 구매(0부터)의 가격
    public int Price(int purchases)
    {
        return Mathf.Min(shopPriceMax, shopBasePrice + shopPriceStep * Mathf.Max(0, purchases));
    }
}
