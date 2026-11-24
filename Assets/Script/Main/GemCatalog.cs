using System;
using System.Collections.Generic;
using UnityEngine;

// 캐릭터 강화 젬 데이터 (일반 젬 16종 수치, 결과 보상, 젬 화면 그림)
// Assets/Resources/GemCatalog 에셋을 실행 중에 불러와 사용한다. (메뉴 MonNyang > 젬 UI 에셋 설정 으로 생성·스프라이트 연결)
[CreateAssetMenu(fileName = "GemCatalog", menuName = "Scriptble Object/GemCatalog")]
public class GemCatalog : ScriptableObject
{
    private static GemCatalog instance;

    public static GemCatalog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GemCatalog>("GemCatalog");
                if (instance == null)
                {
                    Debug.LogWarning("Resources/GemCatalog 에셋이 없어 기본값으로 실행합니다. (메뉴 MonNyang > 젬 UI 에셋 설정)");
                    instance = CreateInstance<GemCatalog>();
                }
            }
            return instance;
        }
    }

    [Serializable]
    public class GemDef
    {
        public string name;
        public StatCategory category;
        public RunStat stat;
        public float value;                  // Percent는 0.04 = 4%, 감소형은 음수, 고정값·횟수는 그대로
        public Sprite icon;
        public bool retired;                 // 보상·합성 풀에서 제외 (데이터는 남김)

        public GemDef() { }

        public GemDef(string name, StatCategory category, RunStat stat, float value)
        {
            this.name = name;
            this.category = category;
            this.stat = stat;
            this.value = value;
        }
    }

    [Header("# 일반 젬 16종 (순서 = Normal_Gem_1 시트 순서, 저장 번호)")]
    public List<GemDef> gems = new List<GemDef>
    {
        new GemDef("공격력 젬", StatCategory.Attack, RunStat.Damage, 0.04f),
        new GemDef("공격속도 젬", StatCategory.Attack, RunStat.AttackSpeed, 0.04f),
        new GemDef("공격 쿨타임 젬", StatCategory.Attack, RunStat.Cooldown, -0.04f),
        new GemDef("무기 스킬 피해 젬", StatCategory.Attack, RunStat.SkillPower, 0.08f),
        new GemDef("무기 스킬 쿨타임 젬", StatCategory.Attack, RunStat.SkillCooldown, -0.05f),
        new GemDef("보조무기 피해 젬", StatCategory.Attack, RunStat.SubWeaponDamage, 0.08f),
        new GemDef("최대 체력 젬", StatCategory.Survival, RunStat.MaxHealthFlat, 10f),
        new GemDef("받는 피해 젬", StatCategory.Survival, RunStat.DamageTaken, -0.04f),
        new GemDef("구르기 쿨타임 젬", StatCategory.Survival, RunStat.DodgeCooldown, -0.06f),
        new GemDef("이동속도 젬", StatCategory.Survival, RunStat.MoveSpeed, 0.04f),
        new GemDef("경험치 획득량 젬", StatCategory.Utility, RunStat.ExpGain, 0.06f),
        new GemDef("냥냥코인 획득량 젬", StatCategory.Utility, RunStat.CoinDrop, 0.08f),
        new GemDef("시작 리롤 젬", StatCategory.Utility, RunStat.Rerolls, 1f),
        new GemDef("필살기 위력 젬", StatCategory.Utility, RunStat.UltPower, 0.10f),
        new GemDef("무기·총알 크기 젬", StatCategory.Utility, RunStat.WeaponSize, 0.08f),
        new GemDef("시작 냥냥코인 젬", StatCategory.Utility, RunStat.StartCoins, 20f),
        new GemDef("보너스 선택 젬", StatCategory.Utility, RunStat.BonusPicks, 1f),   // 수치는 bonusPickPerGem
    };

    [Header("# 결과 보상 (일반 젬 개수)")]
    [Min(0)] public int gemRewardClear = 4;
    [Min(1)] public int failNightsPerGem = 3;          // 실패: 버틴 밤 수 / 이 값 (버림)
    [Min(0)] public int failGemMax = 2;                // 실패 보상 최대 개수
    [Min(0)] public int specialDropMinNights = 3;      // 실패 시 특수 젬 드롭 조건: 버틴 밤 이 횟수 이상 (특수 젬은 다음 작업)

    // ---------------- 특수 젬 ----------------
    // 옵션 하나: 능력치와 범위. 수치는 from~to 사이 정수 (Percent는 1 = 1%, 최대 체력은 1 = 1)
    // 증가 옵션: from = 약한 끝, to = 강한 끝 / 감소 옵션: from = 약한 손해, to = 큰 손해
    [Serializable]
    public class SpecialOption
    {
        public RunStat stat;
        public StatCategory category;        // 증가 옵션의 분야 = 특수 젬 그림
        public int from;
        public int to;
        public float scale = 0.01f;          // 정수 → 능력치 값 (Percent 0.01, 최대 체력 1)
        public bool retired;                 // 생성 풀에서 제외 (데이터는 남김)

        public SpecialOption() { }

        public SpecialOption(RunStat stat, StatCategory category, int from, int to, float scale = 0.01f)
        {
            this.stat = stat;
            this.category = category;
            this.from = from;
            this.to = to;
            this.scale = scale;
        }

        public int Min => Mathf.Min(from, to);
        public int Max => Mathf.Max(from, to);
    }

    [Header("# 특수 젬 증가 옵션 11종 (범위 안 정수, 균등)")]
    public List<SpecialOption> specialUpOptions = new List<SpecialOption>
    {
        new SpecialOption(RunStat.Damage, StatCategory.Attack, 10, 18),
        new SpecialOption(RunStat.AttackSpeed, StatCategory.Attack, 10, 18),
        new SpecialOption(RunStat.SkillPower, StatCategory.Attack, 20, 35),
        new SpecialOption(RunStat.SkillCooldown, StatCategory.Attack, -10, -18),
        new SpecialOption(RunStat.SubWeaponDamage, StatCategory.Attack, 20, 30),
        new SpecialOption(RunStat.MaxHealthFlat, StatCategory.Survival, 20, 35, 1f),
        new SpecialOption(RunStat.DamageTaken, StatCategory.Survival, -6, -12),
        new SpecialOption(RunStat.MoveSpeed, StatCategory.Survival, 8, 14),
        new SpecialOption(RunStat.UltPower, StatCategory.Utility, 20, 35) { retired = true },   // 필살기 위력: 풀에서 제외
        new SpecialOption(RunStat.WeaponSize, StatCategory.Utility, 15, 25),
        new SpecialOption(RunStat.ExpGain, StatCategory.Utility, 15, 25),
        new SpecialOption(RunStat.CoinDrop, StatCategory.Utility, 15, 25),
    };

    [Header("# 특수 젬 감소 옵션 11종 (증가 옵션과 같은 능력치는 제외하고 균등)")]
    public List<SpecialOption> specialDownOptions = new List<SpecialOption>
    {
        new SpecialOption(RunStat.Damage, StatCategory.Attack, -4, -8),
        new SpecialOption(RunStat.AttackSpeed, StatCategory.Attack, -4, -8),
        new SpecialOption(RunStat.SkillPower, StatCategory.Attack, -15, -25),
        new SpecialOption(RunStat.SkillCooldown, StatCategory.Attack, 8, 15),
        new SpecialOption(RunStat.SubWeaponDamage, StatCategory.Attack, -15, -30),
        new SpecialOption(RunStat.MaxHealthFlat, StatCategory.Survival, -8, -15, 1f),
        new SpecialOption(RunStat.MoveSpeed, StatCategory.Survival, -4, -8),
        new SpecialOption(RunStat.DodgeCooldown, StatCategory.Survival, 10, 20),
        new SpecialOption(RunStat.DamageTaken, StatCategory.Survival, 4, 8),
        new SpecialOption(RunStat.ExpGain, StatCategory.Utility, -8, -15),
        new SpecialOption(RunStat.CoinDrop, StatCategory.Utility, -10, -20),
    };

    [Header("# 특수 젬 획득·재설정·분해")]
    [Header("# 보너스 선택 젬 (게임 시작 시 레벨업 카드 선택을 먼저 진행)")]
    [Min(0)] public int bonusPickPerGem = 1;                     // 젬 1개당 선택 횟수

    [Min(1)] public int specialCombineCount = 4;                 // 특수 젬 합성 재료 (일반 젬 개수)
    [Range(0f, 1f)] public float specialDropClear = 0.10f;       // 클리어 시 특수 젬 드롭 확률
    [Range(0f, 1f)] public float specialDropFail = 0.03f;        // 실패 시 (버틴 밤 specialDropMinNights 이상) 드롭 확률
    [Min(0)] public int shardPerGem = 5;                         // 분해: 특수 젬 1개 → 조각
    [Min(0)] public int rerollCost = 10;                         // 수치 재설정 비용 (조각)
    [Min(1)] public int lockCostMul = 2;                         // 한 줄 잠금 재설정 비용 배율
    [Min(1)] public int specialSlotCount = 3;
    [Min(0)] public int specialSlotsOpenAtStart = 1;
    [Min(0)] public int specialSlotsPerClear = 1;

    [Header("# 특수 젬 그림 (Unique_Gem_1: 공격 / 생존 / 유틸)")]
    public Sprite specialAttack, specialSurvival, specialUtility;          // 광채 포함 (메인 화면 장착 칸)
    public Sprite specialAttackPlain, specialSurvivalPlain, specialUtilityPlain;   // 광채 없음 (보관함·합성·재설정·분해·결과)
    public Color upColor = new Color(0.25f, 0.58f, 0.2f, 1f);      // 증가 옵션 글자 (초록)
    public Color downColor = new Color(0.78f, 0.2f, 0.16f, 1f);    // 감소 옵션 글자 (빨강)
    [Header("# 경고 문구 (본문과 떨어진 줄, 빨갛고 조금 작게)")]
    public Color warningColor = new Color(0.78f, 0.16f, 0.12f, 1f);
    [Range(0.5f, 1f)] public float warningFontScale = 0.78f;      // 본문 글자 크기 대비
    [Min(0f)] public float warningGap = 18f;                       // 본문과 경고 사이 빈 칸

    public Sprite SpecialIconGlow(StatCategory c) => c == StatCategory.Attack ? specialAttack : c == StatCategory.Survival ? specialSurvival : specialUtility;
    public Sprite SpecialIcon(StatCategory c)
    {
        Sprite s = c == StatCategory.Attack ? specialAttackPlain : c == StatCategory.Survival ? specialSurvivalPlain : specialUtilityPlain;
        return s != null ? s : SpecialIconGlow(c);
    }

    [Header("# 슬롯 (분야별 칸 수, 처음 열린 칸, 클리어마다 추가)")]
    [Min(1)] public int slotsPerCategory = 3;
    [Min(0)] public int slotsOpenAtStart = 1;
    [Min(0)] public int slotsPerClear = 1;

    [Header("# 분야 색 (공격 / 생존 / 유틸)")]
    public Color attackColor = new Color(0.78f, 0.25f, 0.2f, 1f);
    public Color survivalColor = new Color(0.36f, 0.62f, 0.25f, 1f);
    public Color utilityColor = new Color(0.25f, 0.45f, 0.8f, 1f);
    public Color specialColor = new Color(0.55f, 0.32f, 0.78f, 1f);

    [Header("# 스프라이트 (메뉴 MonNyang > 젬 UI 에셋 설정 으로 채움)")]
    public Sprite shard;                 // 조각 (다음 작업)
    public Sprite lockBig;
    public Sprite equippedBadge;         // E
    public Sprite unknownAttack, unknownSurvival, unknownUtility, unknownSpecial;   // ? 젬
    public Sprite unknownSpecialPlain, specialGlow;              // ? 특수 젬 엠블렘만 / 보라 광채만 (Gem_Unknown_Special_Split)
    [Min(0f)] public float specialGlowSize = 230f;               // 특수 합성 결과 뒤 광채 크기
    [Min(0.05f)] public float specialGlowInTime = 0.25f;         // 광채가 피어나는 시간 (초)
    [Range(0f, 1f)] public float specialGlowRestAlpha = 0.75f;   // 피어난 뒤 남는 밝기
    [Min(0f)] public float specialGlowPulse = 0.15f;             // 남아 있는 동안 밝기가 오르내리는 폭
    public Sprite woodPanel;             // 목재 큰 판넬
    public Sprite paperPanel;            // 양피지 판 (위 제목 띠)
    public Sprite plainPanel;            // 양피지 판 (띠 없음)
    public Sprite darkPanel;             // 어두운 판
    public Sprite slotDark;              // 어두운 칸
    public Sprite slotSpecial;           // 보라 빛 칸
    public Sprite banner;                // 발바닥 띠
    public Sprite tabAttack, tabSurvival, tabUtility;   // 분야 표시판 (아이콘 포함)
    public Sprite slotAttack, slotSurvival, slotUtility, slotSpecialFrame;   // 분야별 젬 칸
    public Sprite slotLocked;
    public Sprite slotPlus;
    public Sprite buttonGreen, buttonBrown, buttonBeige, buttonGrey;
    public Sprite arrowRight;
    public Sprite divider;
    public Sprite resultBar;             // 결과 안내 아래 판 (Object_Panel_54)
    public Sprite questClear;            // Logo_Title 'Quest Clear'
    public Sprite questFailed;           // Logo_Title 'Quest Failed'

    [Header("# 결과 화면")]
    [Range(0f, 1f)] public float resultDimAlpha = 0.55f;
    [Min(0.01f)] public float resultIntroTime = 0.6f;            // 아이콘이 스르륵 나타나는 시간
    public Vector2 resultIconPosition = new Vector2(0f, 110f);
    [Min(10f)] public float resultIconHeight = 380f;
    public Vector2 resultBarPosition = new Vector2(0f, -250f);
    public Vector2 resultBarSize = new Vector2(600f, 110f);
    [Min(1)] public int resultBarFontSize = 40;
    [Min(10f)] public float resultFailedIconHeight = 460f;      // Quest Failed 아이콘 높이 (Clear보다 크게)

    [Header("# 판 제목 (양피지 위 띠 / 발바닥 띠)")]
    public float paperBandCenter = 35f;                          // 판 위 끝에서 제목 띠 가운데까지 (제목 글자 세로 위치)
    public Vector2 bannerTextOffset = Vector2.zero;              // 발바닥 띠 글자 위치 보정

    [Header("# 젬 상인 (Gem_shopper 6x3, 젬 관리 왼쪽)")]
    public Sprite[] shopperFrames;                               // 0~5 대기, 6~11 손 흔들기·젬, 12~17 생각·반짝
    // 대기: 한 그림에 멈춘 채 숨쉬기(세로로 살짝 늘었다 줄었다)만 → 가끔 동작 하나를 짧게
    public int shopperIdleFrame = 0;
    public Vector2 shopperIdleTime = new Vector2(5f, 9f);        // 대기 시간 (최소, 최대 초) 뒤 무작위 동작 하나
    [Range(0f, 10f)] public float shopperBreathPixels = 2f;      // 숨쉬기: 위아래로 움직이는 거리 (화면 픽셀, 정수로 끊어 움직여 꾸물거리지 않게)
    [Min(0.5f)] public float shopperBreathPeriod = 3.2f;         // 숨쉬기 한 번 걸리는 시간 (초)
    [Min(0.5f)] public float shopperFps = 8f;                    // 동작 재생 속도 (초당 그림 수)

    [Serializable]
    public class ShopperAction
    {
        public string name;
        public int[] frames;
        public ShopperAction() { }
        public ShopperAction(string name, params int[] frames) { this.name = name; this.frames = frames; }
    }

    public List<ShopperAction> shopperActions = new List<ShopperAction>
    {
        new ShopperAction("인사", 6, 6, 7, 7, 8, 8, 7, 7, 8, 8, 6, 6),
        new ShopperAction("젬 자랑", 9, 9, 10, 10, 9, 9, 10, 10, 10, 10),
        new ShopperAction("고민", 13, 13, 13, 14, 14, 14, 14, 14, 14, 13, 13),
        new ShopperAction("반짝", 15, 15, 16, 16, 15, 15, 16, 16, 16),
    };
    public bool shopperFaceRight = true;                         // 그림은 왼쪽을 봄 → 좌우 반전
    public Vector2 shopperPosition = new Vector2(43f, 8f);        // 그림 가운데 위치. x: 프레임 안에서 발이 옆에 있어 보정, y: 테이블이 가슴 바로 아래
    public Vector2 shopperSize = new Vector2(330f, 220f);
    public Sprite shopperTable;                                  // 앞 테이블 (Table.png)
    public Vector2 shopperTablePosition = new Vector2(0f, -100f);
    public Vector2 shopperTableSize = new Vector2(300f, 175f);

    public Color CategoryColor(StatCategory c) => c == StatCategory.Attack ? attackColor : c == StatCategory.Survival ? survivalColor : utilityColor;

    public static string CategoryName(StatCategory c) => c == StatCategory.Attack ? "공격" : c == StatCategory.Survival ? "생존" : "유틸";

    public Sprite SlotFrame(StatCategory c) => c == StatCategory.Attack ? slotAttack : c == StatCategory.Survival ? slotSurvival : slotUtility;
    public Sprite Tab(StatCategory c) => c == StatCategory.Attack ? tabAttack : c == StatCategory.Survival ? tabSurvival : tabUtility;
    public Sprite Unknown(StatCategory c) => c == StatCategory.Attack ? unknownAttack : c == StatCategory.Survival ? unknownSurvival : unknownUtility;

    // 특수 젬 옵션 찾기 (능력치로)
    public SpecialOption UpOption(RunStat stat) => specialUpOptions.Find(o => o.stat == stat);
    public SpecialOption DownOption(RunStat stat) => specialDownOptions.Find(o => o.stat == stat);

    // 특수 젬 옵션 수치 문구 (예: +16%, +32)
    public string OptionValue(SpecialOption o, int value) => o == null ? value.ToString() : StatCatalog.Instance.FormatDelta(o.stat, value * o.scale);

    // 효과 문구 (예: 공격력 +4%)
    // 보너스 선택 젬 수치는 bonusPickPerGem을 따름
    private void OnValidate()
    {
        foreach (GemDef d in gems)
            if (d != null && d.stat == RunStat.BonusPicks) d.value = bonusPickPerGem;
    }

    public string EffectText(int gem)
    {
        GemDef d = gems[gem];
        return StatCatalog.Instance.Name(d.stat) + " " + StatCatalog.Instance.FormatDelta(d.stat, d.value);
    }
}
