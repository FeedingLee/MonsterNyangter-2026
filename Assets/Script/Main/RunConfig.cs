using UnityEngine;

// 한 판 동안 변하는 능력치 종류 (상점 강화, 아침 선택지가 공통으로 사용)
public enum RunStat
{
    Damage,             // 공격력 배율
    AttackSpeed,        // 공격 속도(스테미나) 배율
    Cooldown,           // 공격 쿨타임 배율 (작을수록 빠름)
    MoveSpeed,          // 이동속도 배율
    MaxHealth,          // 최대 체력 배율
    DodgeCooldown,      // 회피 쿨타임 배율
    ExpGain,            // 경험치 획득량 배율
    ExpRange,           // 경험치 획득 범위 배율
    HealScale,          // 회복 효과 배율 (0이면 회복 불가)
    UltCooldown,        // 필살기 쿨타임 배율
    UltPower,           // 필살기 위력 배율
    UltEnabled,         // 0이면 필살기 사용 불가
    SpawnRate,          // 몬스터 스폰 속도 배율
    EliteChance,        // 엘리트(강화 몬스터) 등장 확률 배율
    NightMonsterBuff,   // 밤 몬스터 강화량 배율
    CoinDrop,           // 코인 드롭 배율 (낮, 밤 모두)
    NightCoinDrop,      // 밤 코인 드롭 배율
    NightVision,        // 밤 시야 배율
    BossHp,             // 보스 체력 배율
    BossInterval,       // 보스 패턴 간격 배율
    Regen,              // 초당 체력 회복 (더하기)
    EliteBonusCoins,    // 엘리트 처치 시 추가 코인 (더하기)
    Rerolls,            // 상점에서 산 리롤 횟수 (더하기, 표시용)
    EliteHealth,        // 엘리트 체력 배율 (보스 제외)
    EliteDamage,        // 엘리트 공격력 배율 (보스 제외)
    EnemyHealth,        // 모든 적 체력 배율 (보스 제외)
    NightDarkness,      // 밤 어둠 추가 (더하기, 0.6이면 시야 안까지 거의 안 보임)
    SkillCooldown,      // 무기 전용 스킬 쿨타임 배율 (상점 「스킬 연마」)
    SkillPower,         // 무기 전용 스킬 위력 배율 (상점 「스킬 위력」)
    SubWeaponDamage,    // 보조무기(자동 공격) 피해 배율
    DamageTaken,        // 받는 피해 배율 (작을수록 덜 아픔)
    WeaponSize,         // 무기 근접 판정·투사체 크기 배율
    MaxHealthFlat,      // 최대 체력 고정값 (기준 100에 더함, 더하기)
    StartCoins,         // 시작 냥냥코인 (젬 전용, 더하기)
    BonusPicks,         // 게임 시작 시 보너스 선택 횟수 (젬 전용, 더하기)
    TurretCost,         // 포탑 건설 비용 배율 (아침 선택지 공병 고양이)
    NightTravelTime,    // 밤 몬스터 도착 시간 배율 (아침 선택지 질주하는 밤)
    MiniBossHealthToday,// 새로 나오는 중간보스 체력 배율 (아침 선택지 우두머리 현상금)
    MiniBossBounty,     // 중간보스 보상 상자 코인 배율 (1보다 크면 무리 전멸 시 레벨업, 우두머리 현상금)
    BaseMaxHp,          // 기지 최대 체력 배율 (아침 선택지 배수진)
    NightHpAdd,         // 밤 웨이브 체력 증가 합 (더하기: 피의 계약, 군락 미처치 페널티)
    NightBossExtra,     // 밤마다 추가 대장 몬스터 수 (더하기: 피의 계약)
    EliteScale,         // 낮 엘리트 크기 배율 (엘리트의 행진)
    ColonyRaid,         // 오늘 둥지 습격 적용 중 (더하기, 0보다 크면 적용, 아침에 해제)
}

// 낮/밤, 코인, 상점, 아침 선택지 설정
// Assets/Resources/RunConfig 에셋을 실행 중에 불러와 사용하므로 씬에 배치할 필요가 없다.
[CreateAssetMenu(fileName = "RunConfig", menuName = "Scriptble Object/RunConfig")]
public class RunConfig : ScriptableObject
{
    private static RunConfig instance;

    public static RunConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<RunConfig>("RunConfig");
                if (instance == null)
                {
                    Debug.LogWarning("Resources/RunConfig 에셋이 없어 기본값으로 실행합니다. (스프라이트 없음)");
                    instance = CreateInstance<RunConfig>();
                }
            }
            return instance;
        }
    }

    [Header("# 공통 UI")]
    public Font font;                                            // 비우면 기본 폰트

    [Header("# UI 에셋 (Object_Panel 양피지, 버튼 바). 테두리는 9-slice 기준 픽셀 (왼쪽, 아래, 오른쪽, 위)")]
    public Sprite panelSprite;                                   // 큰 양피지 판넬 (상점)
    public Vector4 panelBorder = new Vector4(40f, 110f, 40f, 115f);
    public Sprite rowSprite;                                     // 테두리만 있는 깔끔한 양피지 (상점 항목 한 줄, 상점 열기 버튼)
    public Vector4 rowBorder = new Vector4(14f, 14f, 14f, 14f);
    public Color rowTint = new Color(1f, 0.96f, 0.9f, 1f);
    public Sprite dividerSprite;                                 // 구분선 (아침 선택지 카드)
    public Sprite slotSprite;                                    // 아이콘 칸 (작은 네모 틀)
    public Vector4 slotBorder = new Vector4(12f, 12f, 12f, 12f);
    public Sprite titleSprite;                                   // 상점 제목 판 (테두리 양피지)
    public Vector4 titleBorder = new Vector4(14f, 14f, 14f, 14f);
    public Sprite cardSprite;                                    // 네모 양피지 (아침 선택지 카드, 상점 표지)
    public Vector4 cardBorder = new Vector4(25f, 25f, 25f, 25f);
    public Sprite buttonGreen;
    public Sprite buttonRed;
    public Sprite buttonYellow;
    public Sprite buttonBrown;
    public Sprite rerollPanel;                                   // 아침 선택지 다시 뽑기 판넬 (Object_Panel_54 무늬 양피지 바)
    public Vector4 buttonBorder = new Vector4(10f, 10f, 10f, 10f);
    public Sprite pawSprite;                                     // 발바닥 (상점 표지)
    [Min(0.1f)] public float uiPixelScale = 1.4f;                // 클수록 판넬 테두리가 얇게 표시됨
    public Color parchmentTextColor = new Color(0.25f, 0.14f, 0.07f, 1f);  // 양피지 위 글자 색 (진한 갈색)
    public Color effectTextColor = new Color(0.1f, 0.36f, 0.08f, 1f);      // 상점 효과 글자 색 (진한 초록)
    public Color titleTextColor = new Color(0.25f, 0.14f, 0.07f, 1f);      // 상점 제목 글자 색

    [Header("# 낮 / 밤 (밤 디펜스: 낮 100 + 귀환 준비 15(타이머 정지) + 밤 80 = 4사이클, 5일 차 낮 60 → 보스, 총 13:00)")]
    [Min(5f)] public float dayDuration = 100f;                   // dayTime: 낮 길이 (1~4일 차)
    [Min(5f)] public float nightDuration = 80f;                  // nightTime: 밤 길이
    [Min(0.1f)] public float transitionTime = 3f;                // 어두워지고 밝아지는 시간
    public Color nightColor = new Color(0.01f, 0.02f, 0.08f, 0.93f);  // 시야 밖 어둠 색 (알파 = 어둠 정도)
    [Range(0.05f, 1.5f)] public float nightVisionRadius = 0.286f; // 밤 시야 반경 (화면 높이 대비, 0.22에서 30% 넓힘)
    [Range(0f, 1f)] public float nightInnerDarkness = 0.35f;     // 시야 안쪽도 이만큼 어둡게 (밤 분위기)

    [Header("# 밤 디펜스 (1119_밤디펜스_기반)")]
    [Min(10f)] public float totalTime = 780f;                    // 한 판 카운트다운 (보스 등장까지, 13:00)
    [Min(5f)] public float lastDayTime = 60f;                    // 마지막 날(5일 차) 낮 길이 → 끝나면 보스
    [Min(0f)] public float returnPrepTime = 15f;                 // 낮 → 밤 사이 귀환 준비 (전체 타이머·낮 스폰 정지)
    [Min(0f)] public float returnSpeedBonus = 0.5f;              // 귀환 준비 중 기지 쪽으로 움직이면 이동속도 +50%
    [Range(0f, 180f)] public float returnAngle = 60f;            // 기지 방향 판정 각도 (이동 방향과 기지 방향 사이)
    public string returnPrepText = "몬스터가 몰려옵니다! 준비하세요!";
    public AudioClip returnPrepSound;                            // 귀환 준비 경고음 ([MHS] Beep~)
    [Min(0f)] public float miniBossDelayFirst = 50f;             // 1일 차 낮 시작 후 중간보스까지
    [Min(0f)] public float miniBossDelay = 30f;                  // 2~4일 차 낮 시작 후 중간보스까지
    public float[] nightSpawnInterval = { 1.2f, 1.0f, 0.8f, 0.6f };   // 1~4일 차 밤 웨이브 스폰 간격 (초)
    public int[] nightSpecies = { 0, 1, 2, 3 };                  // 1~4일 차 밤 몬스터 (0 Mosswine / 1 Kestodon / 2 Vespoid / 3 Hercudrome)
    [Min(1f)] public float nightTravelTime = 25f;                // 경로 시작 → 입구까지 걸리는 시간 (이동속도 = 경로 길이 / 이 값)
    [Min(0f)] public float nightBuffRate = 0.2f;                 // (1120 야간 강화 제거로 쓰지 않음, 값만 남김)
    public float[] nightHp = { 6f, 60f, 96f, 120f };             // 1~4밤 밤 웨이브 일반 몬스터 체력 (x (1 + 체력 증가 합))

    [Header("# 대장 몬스터 (밤 웨이브, 1120)")]
    public NightBossPlan[] bossUnitPlans =                       // 1~4밤: 대장마다 등장 구간 (밤 시작부터 초, 구간 수 = 마리 수, 끝은 스폰 마감을 넘지 않음)
    {
        new NightBossPlan { windows = new[] { new Vector2(5f, 15f) } },
        new NightBossPlan { windows = new[] { new Vector2(5f, 15f) } },
        new NightBossPlan { windows = new[] { new Vector2(5f, 10f), new Vector2(12f, 15f) } },
        new NightBossPlan { windows = new[] { new Vector2(5f, 10f), new Vector2(12f, 15f) } },
    };
    [Min(1f)] public float bossArrivalMargin = 1.3f;             // 대장 스폰 마감 = 밤 길이 - 대장 이동 시간 x 이 값 (지나면 남은 대장 즉시 스폰)
    public bool bossIgnoreHitStall = true;                       // 대장은 맞아도(피격 애니메이션 중에도) 계속 이동 (감속은 적용)
    public Vector2 bossUnitExtraWindow = new Vector2(5f, 25f);   // 피의 계약 추가 대장 등장 구간
    [Min(1f)] public float bossUnitHpMul = 20f;                  // 체력 = 그날 밤 일반 몬스터 최종 체력 x 이 값
    [Min(0.1f)] public float bossUnitTravelMul = 2f;             // 이동 시간 = 밤 도착 시간 x 이 값 (질주하는 밤 반영)
    [Min(0.1f)] public float bossUnitScale = 2.08f;              // 1.6 x 1.3
    [Min(0)] public int bossUnitGateDamage = 10;
    public int bossUnitSortingOrder = 3;                         // 그리기 순서 (일반 몬스터 2보다 앞)                 // 입구 도달 시 기지 체력 감소
    public Color bossUnitTint = new Color(0.62f, 0.48f, 0.85f, 1f);       // 어두운 보라 (엘리트·처벌의 붉은색과 구분)
    public Color bossUnitAuraColor = new Color(0.55f, 0.3f, 0.95f, 0.85f);
    public AudioClip bossUnitSound;                              // 등장 효과음 ([MHS] King_wave Sound)
    public string bossUnitText = "대장 몬스터 출현!!";
    public Sprite bossUnitIcon;                                  // 문구 오른쪽 아이콘 (Ingame_shop_icon_2 special_icon_3, 깜빡이지 않음)
    [Min(0.1f)] public float bossUnitTextTime = 2.5f;
    public float bossUnitTextY = 170f;                           // 알림 위치 (화면 가운데 기준, 'N일차 밤이 찾아왔다냥!' 알림과 같은 자리)
    [Min(8)] public int bossUnitFontSize = 40;
    [Min(8f)] public float bossUnitIconSize = 54f;
    [Min(0.1f)] public float bossUnitBlinkSpeed = 6f;            // 문구 깜빡임 속도 (초당 라디안 x 이 값)
    public Color bossUnitTextColor = new Color(0.85f, 0.7f, 1f, 1f);

    [Header("# 귀환 버프 표시 (귀환 준비 중 기지 쪽으로 달릴 때)")]
    public Sprite returnBuffIcon;                                // 달리기 아이콘 (Ingame_shop_icon_2 special_icon_7)
    [Min(4f)] public float returnBuffIconSize = 34f;              // 1080p 화면 픽셀 기준
    [Min(0f)] public float returnBuffIconGap = 4f;               // 체력바(헤비보우건이면 탄창 게이지) 아래 간격

    [Header("# 밤 웨이브 움직임")]
    [Range(0.1f, 2f)] public float nightWalkAnimMul = 0.7f;      // 걷기 애니메이션 속도 배율 (실제 이동 속도는 그대로)
    [Min(0.01f)] public float nightKnockbackMinTime = 0.3f;      // 밤 넉백 최소 시간 (짧으면 순간이동처럼 보임)
    [Header("# 밤 웨이브 변주 (1123): 이동 속도 3종, 스폰 단계")]
    public Vector3 speedTypeRate = new Vector3(0.65f, 0.2f, 0.15f);   // 일반 / 돌격 / 굼벵이 비율 (가중 평균 이동 시간 = nightTravelTime)
    [Range(0f, 0.5f)] public float normalSpeedJitter = 0.1f;      // 일반: 기본 속도 x (1 ± 이 값)
    [Min(0.1f)] public float chargerWalkMul = 0.8f;               // 돌격: 평소 걷는 속도
    [Min(0.1f)] public float chargerDashMul = 2.5f;               // 돌격: 돌진 속도
    [Min(0f)] public float chargerDashTime = 1.5f;                // 돌격: 돌진 시간 (초)
    public Vector2 chargerDashRange = new Vector2(0.3f, 0.7f);    // 돌격: 돌진을 시작하는 경로 위치 (비율)
    [Min(0.1f)] public float slowpokeMul = 0.6f;                  // 굼벵이 속도
    public Vector3 phaseIntervalMul = new Vector3(1.6f, 1f, 0.4f);   // 잔잔 / 보통 / 러시 스폰 간격 배율 (밤별 기존 간격 x 이 값)
    public Vector2 phaseCalmTime = new Vector2(4f, 6f);           // 잔잔 유지 시간
    public Vector2 phaseNormalTime = new Vector2(6f, 10f);        // 보통 유지 시간
    public Vector2 phaseRushTime = new Vector2(3f, 5f);           // 러시 유지 시간
    public Vector3 phaseWeight = new Vector3(1f, 1f, 1f);         // 다음 단계 고를 확률 (러시는 두 번 연속 금지, 첫 단계는 보통)
    [Min(0f)] public float noRushAfterBoss = 3f;                  // 대장 스폰 직후 러시 금지 시간
    [Min(0f)] public float phasePaceSlack = 4f;                   // 예정(기존 간격)보다 이만큼 앞서 내보냈으면 다음 단계는 잔잔 (스폰이 밤 끝까지 이어지게)
    [Min(0f)] public float colonyPenaltyRate = 0.3f;             // 군락 미처치 1곳마다 이번 밤 웨이브 체력 '증가 합'에 더함

    [Header("# 군락 (지시서 1120_군락_시스템)")]
    [Min(1)] public int colonyFirstDay = 2;                      // 생성하는 일차 (2~4일 차 낮)
    [Min(1)] public int colonyLastDay = 4;
    [Min(0)] public int colonyCount = 1;                         // 하루 생성 수 (둥지 습격이면 morningCards.nestCount)
    [Min(0f)] public float colonyMinDist = 15f;                  // 기지에서, 그리고 둥지끼리 최소 거리
    [Min(0f)] public float colonyHpMul = 30f;                    // 둥지 체력 = 그날 낮 일반 몬스터 체력 x 이 값
    [Min(0)] public int colonySpawnCount = 12;                   // 호위 몬스터 수
    [Min(0f)] public float colonySpawnRadius = 3f;               // 호위 생성·대기 반경
    [Min(0f)] public float colonyGuardMinRadius = 2.2f;          // 호위 자리: 둥지에서 이 거리 ~ colonyGuardMaxRadius 사이 무작위 (둥지 그림을 덮지 않게)
    [Min(0f)] public float colonyGuardMaxRadius = 4.5f;
    [Min(0f)] public float colonyGuardWanderRadius = 1.2f;       // 대기 중 자기 자리 근처에서 서성이는 반경
    [Min(0f)] public float colonyAggroRange = 6f;                // 플레이어가 둥지에서 이 안에 들어오면 호위 추적 시작
    [Range(0f, 1f)] public float colonyIdleSpeedMul = 0.35f;     // 대기 중 서성이는 속도 배율
    [Min(0f)] public float colonyExpMul = 20f;                   // 파괴 시 경험치 구슬 수 (일반 몬스터 1마리 경험치 x 이 값)
    [Min(0)] public int colonyCoinMin = 40;                      // 파괴 시 냥냥코인
    [Min(0)] public int colonyCoinMax = 60;
    [Min(0f)] public float colonyEdgeMargin = 4f;                // 맵 벽 범위에서 이만큼 안쪽 = 외곽 한계선
    [Min(0f)] public float colonyCampClear = 6f;                 // 캠프·루핑 경고 영역 바깥 여유
    [Min(0f)] public float colonyPathClear = 4f;                 // 밤 웨이브 길에서 떨어질 거리
    [Min(0f)] public float colonyPadClear = 4f;                  // 포탑 발판에서 떨어질 거리
    [Min(0f)] public float colonyPlayerClear = 10f;              // 생성 순간 플레이어 바로 옆에는 생기지 않게
    public Sprite colonySheet;                                   // 둥지 그림 시트 (Mob_colony, 실행 중에 잘라 씀)
    [Min(1)] public int colonySheetRows = 4;                     // 시트 줄 수 (둥지 종류)
    [Min(1)] public int colonySheetCols = 6;                     // 한 줄 프레임 수
    [Min(0.1f)] public float colonyNestWidth = 6.6f;             // 둥지 그림 칸 가로 크기 (월드, 칸에 여백 포함)
    public bool colonyShadow = false;                            // 둥지 발밑 그림자
    public int[] colonyRowBySpriteType = { 2, 1, 3, 0, -1 };     // 몬스터 종류(spriteType) → 시트 줄 (0 Mosswine→흙 3번째, 1 Kestodon→돌 2번째, 2 Vespoid→벌집 4번째, 3 Hercudrome→나무 1번째, -1 무작위)
    [Range(0f, 1f)] public float colonyPivotY = 0.15f;           // 그림 안 발 위치 (아래에서)
    [Min(0f)] public float colonyNestFps = 0f;                   // 0이면 첫 프레임 고정 (시트 칸 위치가 프레임마다 달라 꿈틀거려 보임)
    public string colonySortingLayer = "Enemy";
    public int colonyNestSortingOrder = 1;                       // 몬스터(2)보다 뒤
    public Vector2 colonyHitboxScale = new Vector2(0.7f, 0.6f);  // 피격 범위 / 그림 크기
    public Vector2 colonyShadowSize = new Vector2(2.6f, 0.7f);
    public float colonyShadowOffsetY = 0.1f;
    public Vector2 colonyBarSize = new Vector2(2.2f, 0.22f);     // 머리 위 체력 바 (월드)
    public float colonyBarGap = 0.2f;
    [Range(0.2f, 1.2f)] public float colonyBarHeight = 0.72f;    // 체력 바 높이 = 그림 칸 높이 x 이 값 (칸 위쪽 여백 제외)
    public Color colonyBarColor = new Color(0.9f, 0.22f, 0.16f, 1f);
    public Color colonyHitFlashColor = new Color(1f, 0.75f, 0.75f, 1f);
    [Min(0f)] public float colonyBreakFxSize = 2.5f;
    [Min(0f)] public float colonyBreakFadeTime = 0.6f;
    public AudioClip colonyBreakSound;                           // 파괴 효과음 ([MHS] Anj_Rock)
    [Min(0f)] public float colonyScatterRadius = 2.2f;           // 보상 흩뿌림 반경
    [Min(0f)] public float colonyScatterTime = 0.8f;             // 보상을 나눠 뿌리는 시간
    public Color colonyFailColor = new Color(1f, 0.2f, 0.15f, 1f);   // 미처치 붉은 기운
    [Min(0f)] public float colonyFailGlowTime = 1.2f;
    public AudioClip colonyWarningSound;                         // 미처치 경고 ([MHS] Warning)
    [Min(0.1f)] public float colonyWarningTime = 3f;
    [Min(8)] public int colonyWarningFontSize = 38;
    public Color colonyWarningColor = new Color(1f, 0.35f, 0.3f, 1f);    // 파괴 실패 문구 (빨강, 검정 외곽선)
    public string colonyFailText = "군락지 파괴 실패 : 이번 밤 몬스터 체력 +{0}%";
    public Color colonyPanelColor = new Color(0.55f, 0.04f, 0.03f, 1f);  // 알림 판 (진한 빨강, 불투명)
    public Vector2 colonyPanelSize = new Vector2(880f, 96f);
    [Min(0f)] public float colonyTextOutline = 2.5f;             // 글자 외곽선 (불투명 검정 두 겹)
    public float colonyWarningY = 60f;                           // 화면 가운데 기준
    public Color colonyIndicatorArrowColor = new Color(1f, 0.85f, 0.2f, 1f);    // 둥지 방향 화살표 (노랑, 중간보스 빨강 화살표와 구분)
    public Color colonyIndicatorFrameColor = new Color(1f, 0.85f, 0.35f, 1f);   // 둥지 방향 고리 (금색)
    public Color colonyIndicatorBackColor = new Color(0.5f, 0.04f, 0.03f, 1f);  // 고리 안 바탕 (진한 빨강, 불투명)
    [Range(0.1f, 1f)] public float colonyIndicatorAlpha = 1f;                    // 반투명 없이
    [Min(0.5f)] public float colonyIndicatorScale = 1.35f;                       // 중간보스 표시 크기 대비
    public string colonyAppearText = "어디선가 군락이 등장했다냥!";
    public Color colonyAppearColor = new Color(1f, 0.88f, 0.3f, 1f);     // 등장 문구 (붉은 판 위 노란 글자)
    public AudioClip colonyAppearSound;                          // 군락 등장 효과음 (비우면 소리 없음)
    [Min(0f)] public float colonyAppearTime = 3f;                // 등장 문구 표시 시간
    [Min(0f)] public float colonyAppearPulseTime = 5f;           // 등장 후 방향 표시가 커졌다 작아졌다 하는 시간
    public Sprite colonyGuardMark;                               // 호위 머리 위 표시 (Ingame_shop_icon_1 stat_icon_1)
    public Color colonyGuardMarkColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Min(0.05f)] public float colonyGuardMarkSize = 0.6f;        // 월드 가로 크기
    public float colonyGuardMarkGap = 0.12f;

    [Header("# 기지 체력 판 피격 효과")]
    [Min(0.01f)] public float baseHitPanelTime = 0.35f;          // 붉게 번쩍·좌우 흔들림 시간
    [Min(0f)] public float baseHitPanelShake = 8f;               // 좌우 흔들림 폭 (UI)
    [Min(0f)] public float baseHitPanelShakeSpeed = 60f;
    public Color baseHitPanelColor = new Color(1f, 0.15f, 0.1f, 0.55f);
    [Min(1)] public int baseMaxHp = 100;                         // 기지 최대 체력 (밤 몬스터가 입구에 닿을 때마다 -1)
    [Min(0f)] public float nightKnockbackMul = 0.5f;             // 밤 몬스터 넉백 거리 배율 (경로를 거슬러 뒤로)
    [Min(0f)] public float hideFxTime = 0.6f;                   // 낮 몬스터 은신: 땅속으로 가라앉으며 투명해지는 시간
    [Min(0f)] public float hideSinkDistance = 0.6f;             // 가라앉는 깊이
    [Min(0f)] public float nightLaneWidth = 0.6f;                // 길 폭 안에서 몬스터마다 흩어지는 폭 (길 밖으로는 안 나감)
    [Min(0.05f)] public float gateFadeTime = 0.45f;             // 밤 몬스터가 입구 문에 닿으면 스르르 사라지는 시간
    [Min(0f)] public float gateHitFxSize = 1.4f;                // 문에 부딪힐 때 '쿵' 효과 크기
    [Min(0f)] public float gateShakeTime = 0.3f;                // 기지 문 흔들림 시간
    [Min(0f)] public float gateShakeAmount = 0.12f;             // 기지 문 흔들림 폭 (월드)
    [Min(0f)] public float baseHitShakePower = 0.08f;           // 기지 체력이 깎일 때 화면 흔들림 세기 (월드)
    [Min(0f)] public float baseHitShakeTime = 0.18f;            // 기지 피해 화면 흔들림 시간 (초)
    public string[] gateObjectNames = { "Gate_Open_0", "Gate_Close_4" };   // 흔들 문 오브젝트 (Camp Zone 아래)
    [Min(400f)] public float morningContentHeight = 860f;        // 아침 선택지 내용 높이 (화면이 이보다 낮으면 그만큼 축소)
    public Vector2 morningCardSize = new Vector2(280f, 470f);    // 아침 카드 크기 (레벨업 카드 그림)
    [Min(100f)] public float morningCardSpacing = 300f;          // 아침 카드 간격
    public int bannerSortingOrder = 90;                          // 'N일차 낮/밤' 알림 문구 정렬 (최상단)
    [Min(4f)] public float shopSignPawSize = 18f;                // 상인 머리 위 '상점' 표지 발바닥 아이콘 크기
    [Range(0f, 1f)] public float shopPromptAlpha = 0.6f;         // '상점 열기 [F]' 판 배경 투명도
    public Vector2 baseHpPanelOffset = new Vector2(-10f, -112f);  // 기지 체력 판 위치 (화면 오른쪽 위 기준, 능력치 판 위)
    public Vector2 baseHpPanelSize = new Vector2(300f, 110f);    // 기지 체력 판 크기 (양피지, 축소 전)
    [Range(0.3f, 1.5f)] public float baseHpPanelScale = 0.8f;    // 기지 체력 판 전체 배율
    [Min(8f)] public float baseHpIconSize = 52f;                 // 판 왼쪽 위 천막 아이콘 크기
    [Min(8)] public int baseHpFontSize = 22;                     // 제목·현재 체력 글자 크기
    public Vector2 baseHpBarSize = new Vector2(160f, 20f);       // 게이지 높이(y)만 사용, 너비는 판 너비에 맞춤
    [Range(0f, 1f)] public float baseHpPanelAlpha = 0.45f;       // 기지 체력 판 배경 투명도
    public Color baseHpBackColor = new Color(1f, 1f, 1f, 1f);    // 게이지 빈 칸 색 (바탕 그림에 곱함)
    public Font panelFont;                                       // 기지 체력·능력치 판 글꼴 (MHS FONT 나눔스퀘어라운드 EB, 비우면 기본 글꼴)
    public FontStyle panelFontStyle = FontStyle.Normal;          // 판 글꼴 굵기 (굵은 글꼴 파일이면 Normal)
    public Sprite baseHpFillSprite;                              // 기지 체력 게이지 채움 (Object_Panel_69 초록 바)
    public Sprite baseHpBackSprite;                              // 기지 체력 게이지 바탕 (Object_Panel_70)
    public Color baseHpColor = new Color(0.9f, 0.3f, 0.25f, 1f);
    public float returnTextY = -215f;                            // 귀환 준비 문구 위치 (화면 위쪽 기준)

    [Header("# 아침 선택지 카드 수치 (1119_아침선택지_재구성)")]
    public MorningCardValues morningCards = new MorningCardValues();

    [Header("# 낮 하늘 (보이지 않는 태양·구름 그림자·노을, DaySky)")]
    [Min(0f)] public float sunsetTime = 20f;                                    // 밤이 되기 전 노을이 시작되는 시간 (초)
    public Color sunsetColor = new Color(1f, 0.55f, 0.3f, 0.13f);              // 노을 색 (a = 가장 짙을 때 투명도, 옅게)
    [Range(0f, 1f)] public float duskDarkness = 0.3f;                           // 노을 끝무렵 미리 어두워지는 정도 (밤 어둠 대비)
    public Color duskNightColor = new Color(0.25f, 0.1f, 0.12f, 1f);           // 막 어두워질 때 어둠 색 (노을빛이 섞인 어둠)
    public Color sunGlowColor = new Color(1f, 0.92f, 0.7f, 1f);                // 해 쪽 가장자리 빛 색
    [Range(0f, 1f)] public float sunGlowAlpha = 0f;                             // 해 쪽 빛 (뿌옇게 보여 기본 끔)
    [Range(0f, 1f)] public float sunShadeAlpha = 0f;                            // 해 반대쪽 그늘 (기본 끔)
    [Range(0f, 0.5f)] public float dayDimMax = 0.12f;                           // 아침·저녁 햇빛이 약할 때 어두워지는 정도 (정오 = 0)
    [Min(0)] public int cloudCount = 14;                                         // 구름 그림자 수
    [Range(0f, 1f)] public float cloudAlpha = 0.22f;
    public Vector2 cloudSize = new Vector2(20f, 11f);                           // 구름 그림자 크기 (월드)
    [Min(0f)] public float cloudSpeed = 1.6f;
    public string cloudSortingLayer = "ForeGround";                             // 땅 위, 캐릭터·나무 아래
    public int cloudSortingOrder = 50;

    [Header("# 밤 몬스터 강화 (보스 제외)")]
    [Min(0f)] public float nightMonsterBuff = 0.3f;              // 첫 밤 강화량 (+30%: 체력, 공격력)
    [Min(0f)] public float nightMonsterBuffPerNight = 0.15f;     // 밤이 거듭될수록 추가되는 강화량
    [Range(0f, 1f)] public float nightSpeedRatio = 0.4f;         // 이동속도에는 강화량의 이 비율만 적용
    public Color nightMonsterTint = new Color(1f, 0.42f, 0.42f, 1f); // 밤에 나온 몬스터 색 (강화 표시)

    [Header("# 시계 UI (화면 왼쪽 위 모서리 기준, 처치 수 아래)")]
    public Sprite clockSprite;
    public Sprite hourHandSprite;
    public Sprite minuteHandSprite;
    public Vector2 clockOffset = new Vector2(70f, -235f);        // 시계 중앙
    [Min(10f)] public float clockSize = 100f;
    public Vector2 clockLabelOffset = new Vector2(245f, -170f);  // "1일차 낮 1:26" 글자 중앙 (왼쪽 정렬)
    [Min(1f)] public float toadRangeStretchY = 1.5f;              // (사용 안 함) 마비 두꺼비 예고 고리 세로 늘림
    [Min(4)] public int toadZapEdgeCount = 8;
    public Color toadRangeColor = new Color(1f, 0.92f, 0.3f, 0.28f);   // 마비 두꺼비 예고 범위 원 (옅은 반투명 노란색)
    public Color bombRangeColor = new Color(1f, 0.25f, 0.2f, 0.28f);   // 폭탄통 예고 범위 원 (옅은 반투명 붉은색)
    [Range(0f, 1f)] public float rangeBlinkMin = 0.35f;                 // 깜빡일 때 가장 옅은 투명도 비율
    [Min(0.1f)] public float demonFlameSize = 2.8f;                     // 가쟈부 필살기: 뒤에서 타오르는 불꽃 크기 (가로, 월드)
    public float demonFlameBaseOffset = 0f;                             // 가쟈부 불꽃 아랫단 높이 보정 (+ 위, - 아래)
    public Color demonFlameColor = new Color(1f, 0.22f, 0.12f, 0.95f);     // 가쟈부 필살기 불꽃 색
    [Min(0.1f)] public float rainDustSize = 1.3f;                       // 화살비 가장자리 흙먼지 구름 크기                     // 마비 폭발: 범위 가장자리를 따라 터지는 번개 수
    [Min(1f)] public float rangeSpikeScale = 1.2f;                // (사용 안 함) 폭탄 범위 그림: 가시 포함 폭 / 실제 범위
    public Vector2 clockLabelUnderOffset = new Vector2(70f, -315f);   // "1일차 낮 / 1:26": 시계 바로 밑 (화면 왼쪽 위 기준, 두 줄)
    public Vector2 clockLabelCenterOffset = new Vector2(0f, -122f);  // "1일차 낮 1:26": 화면 위 가운데(중앙 타이머 아래) 기준 위치
    [Min(1)] public int clockFontSize = 20;
    [Min(0f)] public float clockLabelOutline = 1f;
    [Min(1)] public int coinFontSize = 34;                         // 냥냥코인 수 글자 크기
    [Min(0f)] public float coinOutline = 1.5f;                     // 냥냥코인 수 외곽선 (두 겹)                 // 시계 밑 글자 외곽선 두께
    public Color nightClockTint = new Color(0.6f, 0.7f, 1f, 1f);
    [Range(0.2f, 1.5f)] public float clockZoneSize = 1.1f;          // 낮·밤 빛 고리 지름 / 시계 크기 (1보다 크면 시계판 바깥 테두리로만 보임)
    public Color clockDayZoneColor = new Color(1f, 0.86f, 0.1f, 0.82f);   // 낮 구간 (시침이 여기 있으면 낮): 햇빛 노랑, 살짝 반투명
    public Color clockNightZoneColor = new Color(0.6f, 0.35f, 0.95f, 0.68f);  // 밤 구간: 보라, 살짝 반투명

    [Header("# 코인")]
    public Sprite coinSprite;                                    // 필드에 떨어지는 코인
    public Sprite coinIcon;                                      // UI 코인 아이콘
    [Min(0.01f)] public float coinScale = 0.6f;
    [Range(0f, 1f)] public float dayCoinChance = 0.04f;          // 낮 처치당 코인 확률
    [Range(0f, 3f)] public float nightCoinChance = 0.25f;        // 밤 처치당 코인 확률 (1 이상이면 여러 개)
    [Min(0)] public int eliteCoins = 2;                          // 엘리트 처치 시 추가 코인
    [Min(1f)] public float coinLifetime = 45f;                   // 줍지 않으면 사라지는 시간
    public AudioClip coinSound;                                  // 비우면 경험치 효과음
    public Vector2 coinCounterOffset = new Vector2(118f, -132f); // 화면 왼쪽 위 기준 (킬 수 바로 아래, 레퍼런스 배치)
    public Vector2 questLabelOffset = new Vector2(245f, -200f);  // 토벌 의뢰 진행 표시 (시계 오른쪽)

    [Header("# 상점 (맵 중앙 텐트, 낮에만 이용)")]
    public Sprite tentSprite;                                    // 이 스프라이트를 쓰는 맵의 텐트를 상점으로 사용

    [Header("# 타이틀 화면 (레터박스, 흩날리는 나뭇잎, 태양 빛)")]
    public Color titleLetterboxColor = Color.black;              // 배경 그림 좌우 바깥
    public Sprite[] titleLeafFrames;                             // 나뭇잎 회전 프레임 (leaf_effect 초록 줄)
    [Min(0)] public int titleLeafCount = 6;                      // 화면에 동시에 있는 나뭇잎 수 (과하지 않게)
    [Min(0.1f)] public float titleLeafInterval = 1.6f;           // 새 나뭇잎이 들어오는 간격 (초)
    public Vector2 titleLeafSize = new Vector2(0.035f, 0.055f);  // 크기 범위 (배경 높이 대비)
    public Vector2 titleLeafWind = new Vector2(0.11f, -0.045f);  // 바람 (배경 높이 대비 초당 이동)
    [Min(0f)] public float titleLeafSway = 0.035f;               // 위아래 살랑임 (배경 높이 대비)
    [Min(1f)] public float titleLeafFps = 9f;                    // 나뭇잎 회전 프레임 속도
    [Range(0f, 1f)] public float titleLeafAlpha = 0.95f;
    public Vector2 titleSunPosition = new Vector2(0.236f, 0.831f); // 배경 그림 안 태양 위치 (왼쪽 아래 0, 오른쪽 위 1)
    public Color titleSunColor = new Color(1f, 0.93f, 0.62f, 0.75f);
    [Min(0f)] public float titleSunGlowSize = 0.6f;             // 빛무리 지름 (배경 높이 대비)
    [Min(0f)] public float titleSunRaySize = 1.15f;              // 빛줄기 지름 (배경 높이 대비)
    [Range(0f, 1f)] public float titleSunRayAlpha = 0.32f;
    public float titleSunRaySpeed = 0f;                          // (사용 안 함) 빛줄기는 돌지 않음
    [Min(0f)] public float titleSunPulseSpeed = 1.2f;            // 빛 숨쉬기 속도

    [Header("# 텐트 울타리 (초록 땅 출입 금지, 몬스터 우회)")]
    public Vector2 campZoneMin = new Vector2(-10f, 2f);          // 막을 영역 왼쪽 아래 (돌 테두리 바깥 끝)
    public Vector2 campZoneMax = new Vector2(10f, 13f);          // 막을 영역 오른쪽 위
    public Sprite[] fenceRailSprites;                            // 위·아래 가로 울타리 (같은 폭 조각을 번갈아)
    public Sprite fenceCornerSprite;                             // 네 모서리 큰 기둥 (방향별 모서리 조각이 없을 때)
    public Sprite fenceCornerTL, fenceCornerTR, fenceCornerBL, fenceCornerBR;   // 방향별 모서리 조각 (왼쪽 위, 오른쪽 위, 왼쪽 아래, 오른쪽 아래)
    public float fenceCornerTopDrop = 0.45f;                     // 위쪽 모서리 조각을 내리는 거리 (가로 울타리와 높이 맞춤)
    public Sprite[] fenceSideSprites;                            // 좌우 세로 줄 기둥 (번갈아)
    public Sprite fenceLanternSprite;                            // 좌우 가운데 등불 기둥 (그림은 오른쪽에 등불)
    public Sprite gateArchSprite;                                // 문틀 (아치)
    public Sprite gateClosedSprite;                              // 닫힌 문 (밤)
    public Sprite gateOpenLeftSprite;                            // 열린 왼쪽 문 (낮)
    public Sprite gateOpenRightSprite;                           // 열린 오른쪽 문 (낮)
    public Sprite[] gateOpenFrames;                              // 문 열림 애니메이션 (닫힘 → 열림). 있으면 위 문 그림 대신 사용
    public Sprite[] gateCloseFrames;                             // 문 닫힘 애니메이션 (열림 → 닫힘)
    [Min(1f)] public float gateFps = 10f;                        // 문 여닫이 속도
    [Min(0.05f)] public float fenceScale = 1.1f;
    [Min(0f)] public float fenceInset = 0.5f;                    // 영역 가장자리에서 안쪽으로 (돌 테두리 가운데)
    [Min(0.05f)] public float fencePostSpacing = 1.9f;           // 좌우 기둥 간격 (촘촘할수록 이어진 벽처럼)
    [Min(0.05f)] public float fenceGateScale = 1.6f;
               // 문 크기 (울타리 크기와 따로)
    public string fenceSortingLayer = "Shadow";                  // 텐트·상자와 같은 층
    public int fenceSortingOrder = 1;
    [Min(0f)] public float campSteerMargin = 0.8f;               // 몬스터가 영역을 돌아갈 때 여유
    [Min(0f)] public float campEjectMargin = 0.6f;               // 영역 안에 들어간 대상을 밀어낼 때 여유
    [Min(0f)] public float campSlidePadding = 0.05f;             // 일반 몬스터가 울타리 벽에서 몸 크기보다 이만큼 더 떨어져 미끄러짐
    [Min(1f)] public float flowMargin = 14f;                     // 흐름장 길찾기 범위 (울타리 영역 바깥으로)
    [Min(0.2f)] public float flowCellSize = 0.5f;                // 흐름장 칸 크기
    public Vector2 flowInflate = new Vector2(0.8f, 0.45f);       // 몬스터 몸 크기만큼 장애물을 넓게 (벽에 비비지 않게)
    public Vector2 flowBigInflate = new Vector2(1.6f, 1.1f);     // 중간보스·보스용 (몸이 커서 벽에서 더 떨어진 길)
    [Min(0.05f)] public float flowInterval = 0.3f;               // 흐름장 다시 계산 간격

    [Header("# 캠프 비비기 처벌 (낮에 텐트 울타리 근처를 계속 돌면 일반 몬스터 압박 강화)")]
    public string loiterZoneTilePrefix = "TX Tileset Grass";     // 경고 영역: 캠프 바닥 타일(이름이 이걸로 시작) 전체 범위
    [Min(1f)] public float loiterZoneTileSearch = 6f;           // 바닥 타일을 찾는 범위 (울타리 영역에서 이만큼 바깥까지)
    [Min(0.5f)] public float loiterZoneWidth = 3f;              // 바닥 타일을 못 찾았을 때: 울타리 출입 금지 영역에서 이만큼 바깥까지
    public float[] loiterThresholds = { 20f, 10f, 5f };          // 그날 n번째 처벌이 발동하는 영역 안 누적 시간 (마지막 값이 최소, 밤에 처음으로)
    [Min(0f)] public float loiterStartGrace = 30f;              // 게임 시작 유예: 캠프 안에서 시작하므로 처음 영역을 벗어나거나 이 시간이 지날 때까지 쌓지 않음
    [Min(0.1f)] public float penaltyStartGauge = 10f;           // 처벌 발동 시 게이지 (밖에 있으면 감소 속도만큼 줄어 0이 되면 해제)
    [Min(0f)] public float penaltyGainPerSec = 1f;              // 처벌 중 영역 안에 있으면 초당 게이지 증가
    [Min(0.01f)] public float penaltyDecayPerSec = 0.5f;        // 처벌 중 영역 밖에 있으면 초당 게이지 감소
    [Min(0.1f)] public float penaltyMaxGauge = 30f;             // 게이지 상한
    public Vector2 penaltySpawnMargin = new Vector2(0.8f, 1.8f); // 처벌 중 일반 몬스터 소환 위치: 화면 테두리에서 이만큼 바깥 (평소보다 가까이)
    [Min(0.2f)] public float penaltyTeleportInterval = 2.5f;    // 처벌 중 화면 밖 일반 몬스터를 앞쪽으로 옮기는 간격 (초)
    [Min(0)] public int penaltyTeleportCount = 3;               // 한 번에 옮기는 수
    [Min(0f)] public float penaltyTeleportOffscreen = 1.2f;     // 옮길 위치: 화면 테두리에서 이만큼 바깥
    [Min(0f)] public float penaltyTeleportSpread = 1.2f;        // 옮길 위치끼리 흩어지는 정도
    public Sprite[] penaltyEyeFrames;                            // 처벌 중 일반 몬스터 머리 위 표시 (Enemy_Eye 시트: 눈 뜨기 → 빛남 → 감기)
    [Min(0)] public int penaltyEyeLoopStart = 8;                // 눈을 다 뜬 뒤 반복할 구간 시작 프레임
    [Min(0)] public int penaltyEyeLoopEnd = 19;                 // 반복 구간 끝 프레임
    [Min(1f)] public float penaltyEyeFps = 14f;                 // 눈 애니메이션 속도
    [Min(0.05f)] public float penaltyEyeSize = 0.9f;            // 표시 크기 (월드, 가로. 그림 여백 포함)
    [Min(0f)] public float penaltyEyeBob = 0.1f;                // 머리 위에서 위아래로 떠다니는 폭
    [Min(0f)] public float penaltyEyeBobSpeed = 6.28f;          // 떠다니는 속도 (라디안/초, 6.28 = 1초 주기)
    public string penaltyEyeSortingLayer = "Weapon";             // 다른 몬스터에 가려지지 않게 위쪽 층
    public float penaltyEyeOffsetY = 0.15f;                      // 몬스터 그림 위쪽에서 이만큼 위
    public AudioClip penaltySound;                               // 처벌 발동 효과음 (powerup_1)
    [Min(1f)] public float penaltyOvertimeInterval = 10f;      // 처벌 중 영역 안에서 버틴 시간 이만큼마다 1스택
    [Min(0f)] public float penaltyHealthPerStack = 0.2f;        // 스택당 새로 소환되는 일반 몬스터 최대 체력 증가 (0.2 = +20%)
    [Min(1)] public int penaltyMaxStack = 5;                    // 최대 스택 (5 x 20% = +100%)
    public Color penaltyStackTint = new Color(1f, 0.22f, 0.22f, 1f);   // 최대 스택일 때 몸 색 (스택에 비례해 진해짐)
    [Min(0f)] public float penaltyRecoverTime = 4f;             // 처벌 해제 후 강화된 몬스터의 추가 체력·붉은 색이 원래대로 돌아가는 시간 (초)
    [Min(1f)] public float penaltyEyeStackScale = 1.5f;         // 최대 스택일 때 눈 크기 배율
    public Color penaltyEyeStackColor = new Color(1f, 0.35f, 0.3f, 1f);   // 최대 스택일 때 눈 색 (진하게)

    [System.Serializable]
    public class LeafSet
    {
        public string name;                                      // 종류 이름 (확인용)
        public Sprite[] frames;                                  // 회전 프레임
    }

    [Header("# 맵 나뭇잎 (게임 시작 때 한 종류를 무작위로 골라 맵 화면에 흩날림)")]
    public LeafSet[] mapLeafSets;                                // leaf_effect: 분홍 벚꽃잎 / 흰 벚꽃잎 / 초록 나뭇잎 / 노란 잎 / 꽃송이
    [Min(0)] public int mapLeafCount = 11;                        // 화면에 동시에 있는 수 (살짝만)
    public Vector2 mapLeafSize = new Vector2(0.364f, 0.546f);    // 크기 범위 (월드, 가로. 그림 칸 여백 포함이라 실제 잎은 약 60%)
    public Vector2 mapLeafWind = new Vector2(0.55f, -0.45f);     // 바람 (월드 초당 이동, 왼쪽 위 → 오른쪽 아래)
    [Min(0f)] public float mapLeafSway = 0.35f;                  // 좌우 살랑임 폭 (월드 초당)
    [Min(1f)] public float mapLeafFps = 7f;                      // 회전 프레임 속도
    [Range(0f, 1f)] public float mapLeafAlpha = 1f;
    [Min(0f)] public float mapLeafResumeSpread = 4f;           // 아침에 나뭇잎이 다시 떨어지기 시작할 때 이 시간(초)에 걸쳐 나눠 들어옴
    public string mapLeafSortingLayer = "Weapon";                // 캐릭터·몬스터 위 (UI 아래)
    public int mapLeafSortingOrder = 200;

    [Header("# 상점 NPC (Shopper, 낮에만 입구 옆)")]
    public Vector2 shopperOffset = new Vector2(2.4f, -1.1f);     // 문(영역 아래 가운데) 기준 위치
    [Min(0.05f)] public float shopperHeightRatio = 1.1f;           // 크기 (1 = 플레이어 캐릭터와 같은 키)
    [Min(0.1f)] public float shopperPlayerHeight = 1.1f;         // 플레이어 캐릭터 그림 높이 (월드, 기준값)
    public Sprite[] shopperIdleFrames;
    public Sprite[] shopperGreetFrames;
    public Sprite[] shopperEnterFrames;
    public Sprite[] shopperLeaveFrames;
    [Min(1f)] public float shopperFps = 8f;
    [Min(0.1f)] public float shopperGreetRadius = 2.6f;          // 이 거리 안이면 인사
    [Min(1)] public int shopperGreetLoops = 2;                   // 다가왔을 때 손 흔드는 횟수
    [Min(0f)] public float shopperRunDistance = 2.5f;            // 등장·퇴장 때 달리는 거리
    public Vector2 shopperShadowSize = new Vector2(0.75f, 0.22f);
    public float shopperShadowOffsetY = 0.04f;
    [Min(0f)] public float shopperMarkerHeight = 0.25f;          // "상점" 표지판 높이 (머리 위)

    [Header("# 시작 기본무기 (쌍검 1레벨 대신, 다른 무기를 얻으면 사라짐)")]
    public Sprite woodStickSprite;                               // 비우면 기존처럼 쌍검 1레벨로 시작
    [Min(0.05f)] public float woodStickScale = 1f;               // 크기 배율 (1 = 쌍검 칼 길이)
    public float woodStickAngle = 0f;                            // 그림 회전 보정 (그림의 막대가 위를 향하면 0)
    public Vector2 woodStickHitSize = new Vector2(1.6f, 11.2f);  // 피격 판정 크기 (그림 단위, Wood_stick 막대 부분)
    public Vector2 woodStickHitOffset = new Vector2(-0.2f, -0.44f);   // 피격 판정 위치 (그림 가운데 기준)
    public Sprite chestSprite;                                   // (사용 안 함)
    public Sprite[] campfireFrames;                              // (사용 안 함)
    [Min(1f)] public float campfireFps = 10f;                    // (사용 안 함)
    public Vector2 campOffset = new Vector2(0f, 3f);             // 맵에 텐트가 없을 때만: 시작 위치 기준 텐트 위치
    [Min(0.01f)] public float tentScale = 2.2f;                  // 맵에 텐트가 없을 때만 사용
    [Min(0.01f)] public float chestScale = 1.6f;                 // (사용 안 함)

    [Header("# 바닥 그림자 (보물상자, 아이루 꿀벌레, 보와보와 미끼)")]
    public Color groundShadowColor = new Color(0f, 0f, 0f, 0.35f);
    public Vector2 rewardBoxShadowSize = new Vector2(1.3f, 0.36f);   // 월드 크기 (가로, 세로)
    public float rewardBoxShadowOffsetY = 0.45f;                 // 스프라이트 아래 끝 기준
    public Vector2 honeyBugShadowSize = new Vector2(0.8f, 0.26f);
    public float honeyBugShadowOffsetY = 0.06f;
    public Vector2 decoyShadowSize = new Vector2(1.1f, 0.32f);   // 보와보와 미끼
    public float decoyShadowOffsetY = 0.12f;
    [Min(0.01f)] public float campfireScale = 1.2f;              // (사용 안 함)
    [Min(0.3f)] public float interactRadius = 1.5f;              // 텐트 가장자리에서 이 거리 안이면 상점 이용
    public Vector2 shopMarkerOffset = new Vector2(0.024f, 0.02f); // "상점" 표지판 위치 보정 (텐트 크기 대비, 텐트 장대 끝 기준)
    public KeyCode interactKey = KeyCode.F;
    public AudioClip shopOpenSound;                              // 상점 열기 (비우면 기존 선택 효과음)
    public AudioClip shopBuySound;                               // 카드 선택(구매) (비우면 레벨업 효과음)
    public AudioClip shopClickSound;                             // 카드 고르기·나가기 (잔잔한 딸깍, 비우면 선택 효과음)
    // 상점 카드·가격·등급 수치는 Resources/StatCatalog 에셋

    [Header("# 레벨업 추가 카드: 능력치 (레벨별 누적값)")]
    public float[] knockbackPerLevel = { 0.15f, 0.3f, 0.45f, 0.6f, 0.75f };      // 밀어내기: 적 넉백 증가 (보스 제외)
    public float[] weaponMasteryPerLevel = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };     // 무기 숙련: 메인 무기 전용 스킬 피해 증가
    public int[] piercePerLevel = { 1, 2, 3 };                                 // 관통 화살촉: 투사체 관통 수 증가
    public float[] lastStandPerLevel = { 0.2f, 0.3f, 0.4f };                   // 한계 돌파: 체력이 낮을 때 공격력 증가
    [Range(0.05f, 1f)] public float lastStandThreshold = 0.3f;                 // 한계 돌파 발동 체력 비율
    public Sprite knockbackIcon;                                               // 비어 있으면 아래 원본에서 잘라 씀
    public Sprite knockbackIconSource;                                         // Swing 원본 (한 장짜리 스프라이트)
    public Rect knockbackIconRect = new Rect(231f, 238f, 1029f, 663f);         // 원본에서 잘라 쓸 영역 (픽셀, 왼쪽 아래 기준, 여백 제외)
    public Sprite masteryIcon;
    public Sprite pierceIcon;
    public Sprite lastStandIcon;

    [Header("# 레벨업 추가 카드: 소모성")]
    [Min(0)] public int coinPouchAmount = 30;                                  // 코인 주머니
    public Sprite coinPouchIcon;
    public AudioClip coinPouchSound;
    public KeyCode useItemKey = KeyCode.Q;                                     // 보관 슬롯 사용 키 (클릭도 가능)
    public AudioClip placeSound;                                               // 폭탄 통/마비 두꺼비 설치 소리 (비우면 선택 효과음)
    public AudioClip uiPickSound;                                              // 레벨업 카드, 아침 선택지 고를 때 (기존 선택음은 옵션/상점 전용)
    public AudioClip skillActivateSound;                                       // 메인무기 전용 스킬 발동 (연사 모드, 모아 베기, 돌진 모드)
    [Range(0f, 1f)] public float emptySlotAlpha = 0.25f;                       // 빈 보관 슬롯 투명도

    [Min(0.5f)] public float bombDelay = 3f;                                   // 폭탄 통: 설치 후 폭발까지
    [Min(0.1f)] public float bombRadius = 2.5f;
    [Min(0f)] public float bombDamage = 600f;                                  // 보스 1회 피해 상한을 무시
    public Sprite bombIcon;
    public Sprite bombSprite;
    [Min(0.01f)] public float bombScale = 0.8f;
    public AudioClip bombSound;
    public Color bombFlashColor = new Color(1f, 0.55f, 0.15f, 0.7f);

    [Min(0.5f)] public float toadDelay = 3f;                                   // 마비 두꺼비: 설치 후 발동까지
    [Min(0.1f)] public float toadRadius = 2.5f;
    [Min(0f)] public float toadMobStun = 4f;                                   // 일반 적 이동 불가 시간
    [Min(0f)] public float toadBossStun = 2.5f;                                // 보스 이동 불가 시간 (원거리 패턴은 계속)
    public Sprite toadIcon;
    public Sprite toadSprite;
    [Min(0.01f)] public float toadScale = 0.8f;
    public AudioClip toadSound;
    public Color toadFlashColor = new Color(0.4f, 0.85f, 1f, 0.7f);
    public Color stunnedTint = new Color(0.6f, 0.8f, 1f, 1f);                 // 마비된 적 색

    [Min(0.5f)] public float foodDuration = 20f;                               // 음식 섭취: 지속 시간
    [Min(0f)] public float foodDamage = 0.3f;                                  // 공격력 +30%
    [Min(0f)] public float foodMoveSpeed = 0.15f;                              // 이동속도 +15%
    public Sprite foodIcon;
    public AudioClip foodSound;

    [Header("# 왼쪽 능력치 현황 / 보관 슬롯 (화면 왼쪽 위, 왼쪽 아래 기준)")]
    public bool showStatPanel = true;
    [Range(0f, 1f)] public float statPanelAlpha = 0.4f;                        // 판 투명도 (게임을 가리지 않도록 반투명)
    public Vector2 statPanelOffset = new Vector2(-10f, -208f);                  // 화면 오른쪽 위 기준 (옵션 버튼·헌터랭크 아래)
    [Min(100f)] public float statPanelWidth = 300f;
    [Min(1)] public int statFontSize = 19;
    [Range(0.3f, 1.5f)] public float statPanelScale = 0.8f;                   // 능력치 판 전체 배율 (화면을 덜 가리게)
    [Min(4f)] public float statIconSize = 24f;                                 // 능력치 줄 앞 아이콘 크기
    [Min(0f)] public float statIconGap = 6f;                                   // 아이콘과 글자 사이
    [Min(0f)] public float statRowGap = 3f;                                    // 줄 사이 간격
    public Color statBuffColor = new Color(0.1f, 0.45f, 0.1f, 1f);
    public Color statRiskColor = new Color(0.75f, 0.1f, 0.08f, 1f);
    public Vector2 storageSlotOffset = new Vector2(70f, 430f);
    [Min(20f)] public float storageSlotSize = 100f;
    public bool alignSlotToSkillButton = true;                                  // 보관 슬롯을 오른쪽 회피 버튼과 같은 높이·크기로 자동 정렬

    [Header("# 이펙트 (effect_1, effect_2 시트에서 프레임을 잘라 애니메이션으로 재생). 영역은 픽셀, 왼쪽 아래 기준")]
    public Sprite effectSheet1;
    public Sprite effectSheet2;
    public Sprite effectSheet3;
    public Sprite effectSheet4;                                                // Effect_2 (금빛 고리, 반짝임 별, 속도선)
    public Sprite effectSheet5;
    public Sprite effectSlash;                                                 // Effect_3_slash (Effect_3 베기를 가로로 다듬은 프레임)                                                // Effect_3 (X 베기, 빛 폭발)                                                // sub_effect_1 (반짝임, 마법진, 검기 등)
    [Header("# 새 이펙트 시트 (11.13 교체). 같은 크기 칸 격자, 검은 배경 시트는 검은색을 투명으로, 빛 안개 시트는 옅은 부분을 걷어 내고 사용")]
    public Sprite fxVfx1;        // Attack_vfx_1 (6x8, 검은 배경): 초승달, 휘두름+먼지, 땅 가시, 속도선+먼지, 번개, 흙폭발, 바위 가시, 충격 고리
    public Sprite fxVfx2;        // Attack_vfx_2 (7x8, 검은 배경): 흰 초승달, 금빛 회전 고리, 땅 터짐, 바람+구름, 충격 구덩이, 불꽃 튐, 땅 균열 번개, 돌무더기
    public Sprite fxVfx3;        // Attack_vfx_3 (7x8, 검은 배경): 푸른 초승달, 찌르기 섬광, 흰 폭발, 먼지구름, 충격 고리, 돌 튐, 사선 불꽃, 회오리 고리
    public Sprite fxVfx4;        // Attack_vfx_4 (6x10, 빛 안개)
    public Sprite heavySkillSheet; // Heavy_Skill (검은 배경): 정밀 저격 스코프·탄 아이콘
    public Sprite fxVfx5;        // Attack_vfx_5 (빛 안개)
    public Sprite fxObject1;     // Object_effect_1 (6x5): 붉은 조준 원, 불꽃 폭발, 노란 번개 고리(마비), 독 구름, 땅 고리 폭발
    public Sprite fxParticle1;   // Particle_effect_1 (7x6): 별 폭발, 불꽃, 돌 튐, 연기, 어두운 초승달, 땅 가시+고리
    public Sprite fxParticle2;   // Particle_effect_2 (7x8, 빛 안개)
    public Sprite fxSub2;        // sub_effect_2 (8x6, 빛 안개): 반짝임, 금빛 회전, 금빛 기둥, 수정 조각, 나뭇잎, 먼지 퍼프
    public Sprite fxSub3;        // sub_effect_3 (8x8, 빛 안개): 반짝임, 금빛 고리, 금빛 부채, 금빛 기둥, 나뭇잎, 조각, 먼지, 빛 구슬
    [Header("# UI 에셋 (Morning_card, Skill_button). 메뉴 MonNyang > UI 에셋 다시 설정 으로 채움")]
    public Sprite morningCardPaper;
    public Sprite morningBarGreen;
    public Sprite morningBarYellow;
    public Sprite morningBarRed;
    public Sprite morningBoxLight;
    public Sprite morningBoxGreen;
    public Sprite morningBoxRed;
    public Sprite morningSlot;
    public Sprite morningRibbon;
    public Sprite skillButtonFrame;
    public Sprite ultimateButtonFrame;
    public Sprite dodgeButtonFrame;
    public Sprite threatIndicatorFrame;
    public Vector2Int coinPouchRange = new Vector2Int(20, 40);                 // 금화 상자: 강화 코인 최소~최대 (균등 확률)
    public Color eliteTint = new Color(1f, 0.72f, 0.68f, 1f);                  // 강화 몬스터 몸 색 (약간 붉게)
    public Color eliteAuraColor = new Color(1f, 0.45f, 0.3f, 0.95f);          // 강화 몬스터 발밑 오라 색
    [Range(0, 200)] public int fxHazeCut = 90;                                 // 빛 안개 시트: 이 알파 이하는 투명으로
    [Range(0, 120)] public int fxBlackCut = 24;                                // 검은 배경 시트: 이 밝기 이하는 투명으로
    [Min(1f)] public float effectFps = 16f;
    [Min(0f)] public float effectEdgeFeather = 6f;                              // 잘라 쓴 이펙트 가장자리를 서서히 투명하게 (픽셀, 0이면 끔). 네모 경계 방지
    public Rect[] explosionFrames =                                            // 시트1: 불꽃 → 폭발 → 연기 (폭탄 통)
    {
        new Rect(20f, 869f, 68f, 60f), new Rect(116f, 847f, 118f, 104f), new Rect(263f, 819f, 172f, 160f),
        new Rect(452f, 813f, 222f, 188f), new Rect(688f, 820f, 246f, 196f), new Rect(946f, 825f, 192f, 164f),
        new Rect(1144f, 825f, 180f, 138f), new Rect(1338f, 829f, 180f, 134f),
    };
    public Rect[] dustFrames =                                                 // 시트1: 흙먼지 (회피)
    {
        new Rect(13f, 330f, 262f, 130f), new Rect(296f, 330f, 240f, 110f), new Rect(526f, 310f, 212f, 118f),
    };
    public Rect[] zapFrames =                                                  // 시트2: 전기 터짐 (마비 두꺼비)
    {
        new Rect(352f, 297f, 132f, 142f), new Rect(1040f, 463f, 182f, 122f),
    };
    public Rect stunSparkFrame = new Rect(1029f, 53f, 62f, 82f);               // 시트2: 마비된 적 위 작은 파란 반짝임
    public Rect coinSparkFrame = new Rect(8f, 165f, 142f, 148f);               // 시트1: 금빛 반짝임 (코인 획득, 음식)
    public Rect[] healFrames = { new Rect(1101f, 36f, 108f, 122f), new Rect(980f, 33f, 98f, 94f) };   // 시트1: 초록 십자, 하트 (회복)
    public Rect shieldDomeFrame = new Rect(1217f, 22f, 178f, 136f);            // 시트1: 파란 보호막 (랜스 돌진 모드)
    public Rect hitSparkFrame = new Rect(202f, 299f, 138f, 136f);              // 시트2: 무기에 맞을 때 노란 별 (작게)
    [Min(0.05f)] public float hitSparkSize = 0.45f;
    [Min(1)] public int hitSparksPerSecond = 40;                               // 초당 최대 개수 (많은 적이 동시에 맞아도 가볍게)
    public Rect bossWarningFrame = new Rect(619f, 49f, 126f, 90f);             // 시트2: "!!" (보스 돌진 예고)
    public Rect bossFireFrame = new Rect(114f, 808f, 140f, 146f);              // 시트2: 불꽃 (보스 화염 발사)
    public Rect[] bossSlashFrames =                                            // 시트2: 칼날 궤적 (보스 칼날 발사)
    {
        new Rect(10f, 598f, 200f, 164f), new Rect(199f, 598f, 216f, 180f), new Rect(404f, 614f, 200f, 164f),
    };
    public Rect[] groundBurstFrames = { new Rect(15f, 640f, 118f, 144f), new Rect(553f, 636f, 230f, 154f) };   // 시트1: 땅 튀김 (보스 돌진, 점프)
    public Rect landingRingFrame = new Rect(809f, 630f, 290f, 154f);           // 시트1: 흙먼지 고리 (보스 착지)
    [Min(0.1f)] public float dodgeDustSize = 1.3f;
    public AudioClip dodgeSound;

    [Header("# 밤 배경음 (밤이 되면 소리를 먹먹하게)")]
    [Range(200f, 22000f)] public float nightBgmCutoff = 3500f;                  // 밤 저역 통과 필터 주파수 (낮을수록 어둡고 먹먹함)

    [Header("# 아침 선택지")]
    public Sprite growthIcon;
    public Sprite challengeIcon;
    public Sprite hellIcon;
    public Color growthColor = new Color(0.3f, 0.7f, 0.4f, 1f);
    public Color challengeColor = new Color(0.95f, 0.6f, 0.2f, 1f);
    public Color hellColor = new Color(0.8f, 0.15f, 0.2f, 1f);
    public bool allowMorningReroll = true;                       // 레벨업 리롤 횟수를 같이 사용해 다시 뽑기
    [Min(1)] public int questKillGoal = 150;                     // 토벌 의뢰 목표 처치 수
}

// 아침 선택지 18장 수치 (성장 6 / 도전 6 / 지옥 6)
[System.Serializable]
public class MorningCardValues
{
    [Header("성장")]
    [Min(0)] public int stretchLevels = 1;                   // 기지개 켜기
    [Min(0)] public int pocketMoneyCoins = 30;               // 쌈짓돈
    [Min(0)] public int baseRepair = 25;                     // 기지 수리
    [Range(0f, 1f)] public float engineerCostMul = 0.7f;     // 공병 고양이 (오늘 밤 포탑 비용 -30%)

    [Header("도전 (오늘)")]
    [Min(1f)] public float captureExpMul = 1.5f;             // 생포 의뢰 보상
    [Range(0f, 1f)] public float captureDamageMul = 0.75f;   // 생포 의뢰 대가 (플레이어 공격력, 포탑 미적용)
    [Min(0)] public int eliteHuntCoins = 3;                  // 엘리트 토벌
    [Min(1f)] public float eliteHuntChanceMul = 3f;
    [Min(1f)] public float eliteHuntHealthMul = 2f;
    [Min(0)] public int heavyBagCoins = 80;                  // 무거운 배낭
    [Range(0f, 1f)] public float heavyBagMoveMul = 0.85f;
    [Min(1f)] public float nestRewardMul = 2f;               // 둥지 습격 (군락이 생기면 사용)
    [Min(0)] public int nestCount = 2;
    [Min(1f)] public float bountyBoxCoinMul = 2f;            // 우두머리 현상금
    [Min(0)] public int bountyLevels = 1;
    [Min(1f)] public float bountyHealthMul = 1.5f;
    [Min(1f)] public float rushCoinMul = 2f;                 // 질주하는 밤
    [Min(1f)] public float rushTravelTime = 18f;             // 밤 몬스터 도착 시간 (기본 nightTravelTime 25)

    [Header("지옥 (영구)")]
    [Min(0)] public int bloodLevels = 4;                     // 피의 계약
    [Min(0)] public int bloodCoins = 200;
    [Min(1f)] public float bloodNightBuffMul = 1.5f;             // (1120 이후 쓰지 않음)
    [Min(0f)] public float bloodPactHpAdd = 0.3f;                // 피의 계약: 밤 웨이브 체력 +30% (합산)
    [Min(0)] public int bloodPactExtraUnit = 1;                  // 피의 계약: 밤마다 대장 +1
    [Min(1f)] public float elitesMarchScale = 1.5f;              // 엘리트의 행진: 낮 엘리트 크기
    [Min(1f)] public float marchEliteChanceMul = 2f;         // 엘리트의 행진
    [Min(1f)] public float marchEliteHealthMul = 2f;
    [Min(1f)] public float marchEliteDamageMul = 2f;
    [Min(0)] public int rageLevels = 4;                      // 맹수의 분노
    [Min(1f)] public float rageBossHpMul = 1.25f;
    [Min(0)] public int cursedCoins = 180;                   // 저주받은 금화
    [Min(1)] public int cursedChoices = 2;
    [Min(0)] public int lastStandLevels = 2;                 // 배수진
    [Min(0)] public int lastStandCoins = 150;
    [Range(0.05f, 1f)] public float lastStandBaseMul = 0.5f;
    [Min(0)] public int openCampLevels = 2;                  // 무방비 캠프
    [Min(0)] public int openCampCoins = 200;
}

// 밤 하나의 대장 몬스터 등장 구간들
[System.Serializable]
public class NightBossPlan
{
    public Vector2[] windows;
}
