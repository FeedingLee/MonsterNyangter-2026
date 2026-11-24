using UnityEngine;

// 레벨업 전투 기믹 카드 14종의 단계별 수치, 레벨업 선택 규칙 수치, 레벨업 화면 그림·배치
// Assets/Resources/GimmickCatalog 에셋을 실행 중에 불러와 사용한다. (메뉴 MonNyang > 레벨업 UI 에셋 설정 으로 생성·스프라이트 연결)
// 밀어내기·관통 화살촉·한계 돌파 수치는 기존대로 RunConfig의 표를 쓴다.
[CreateAssetMenu(fileName = "GimmickCatalog", menuName = "Scriptble Object/GimmickCatalog")]
public class GimmickCatalog : ScriptableObject
{
    private static GimmickCatalog instance;

    public static GimmickCatalog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GimmickCatalog>("GimmickCatalog");
                if (instance == null)
                {
                    Debug.LogWarning("Resources/GimmickCatalog 에셋이 없어 기본값으로 실행합니다. (메뉴 MonNyang > 레벨업 UI 에셋 설정)");
                    instance = CreateInstance<GimmickCatalog>();
                }
            }
            return instance;
        }
    }

    // 기믹 14종 (C 항목 번호 = 아이콘 순서)
    public static readonly ItemData.ItemType[] Order =
    {
        ItemData.ItemType.Knockback, ItemData.ItemType.Pierce, ItemData.ItemType.LastStand,
        ItemData.ItemType.DodgeCounter, ItemData.ItemType.CounterInstinct, ItemData.ItemType.Focus,
        ItemData.ItemType.ChainLightning, ItemData.ItemType.KillExplosion, ItemData.ItemType.HuntInstinct,
        ItemData.ItemType.TwinWeapon, ItemData.ItemType.Shield, ItemData.ItemType.FrostStep,
        ItemData.ItemType.Escape, ItemData.ItemType.LifeStrike,
    };

    [Header("# 레벨업 선택 규칙")]
    [Range(0f, 1f)] public float consumableSlotRate = 0.15f;      // 무작위 칸이 소모품이 될 확률
    [Min(1)] public int consumableOnlyEvery = 17;                 // 이 배수 레벨은 소모품만
    [Min(1)] public int weaponOnlyEvery = 5;                      // 이 배수 레벨은 무기만
    [Min(0f)] public float netRangeAdd = 0.3f;                    // 포획용 그물: 경험치 흡수 범위 증가

    [Header("# 기믹 아이콘 (C 항목 번호 순서, Ingame_Levelup_icon)")]
    public Sprite[] icons = new Sprite[14];

    [Header("# 4. 회피 반격: 구르기 직후 첫 공격 피해")]
    public float[] dodgeCounterBonus = { 0.3f, 0.4f, 0.5f };
    [Min(0f)] public float dodgeCounterWindow = 1f;

    [Header("# 5. 반격 본능: 우클릭 기술 후 공격속도")]
    public float[] counterInstinctSpeed = { 0.1f, 0.15f, 0.2f };
    [Min(0f)] public float counterInstinctDuration = 2f;

    [Header("# 6. 집중: 피격 없이 일정 시간마다 피해 증가 (중첩)")]
    public float[] focusPerStack = { 0.03f, 0.04f, 0.05f };
    [Min(0.1f)] public float focusInterval = 10f;
    [Min(1)] public int focusMaxStacks = 3;

    [Header("# 7. 연쇄 번개: 적중 N번마다 주변 적 M명에게")]
    public int[] chainHitsPer = { 20, 18, 16, 14, 12 };
    public int[] chainTargets = { 3, 3, 4, 4, 5 };
    [Min(0f)] public float lightningDamageMul = 0.5f;            // 메인무기 기준 피해 x 이 값
    [Min(0.1f)] public float chainRadius = 4f;                   // 번개가 옮겨 가는 거리
    public Color lightningColor = new Color(0.55f, 0.85f, 1f, 1f);
    [Min(0.05f)] public float lightningLifetime = 0.3f;          // 번개 이펙트가 화면에 남는 시간 (지나면 삭제)
    [Min(0.05f)] public float lightningWidth = 1.8f;             // 번개 줄기 두께 (월드)

    [Header("# 8. 처치 폭발")]
    public float[] explodeChance = { 0.1f, 0.15f, 0.2f };
    [Min(0.1f)] public float explodeRadius = 1.5f;
    [Min(0f)] public float explodeDamageMul = 0.5f;              // 처치한 공격 피해 x 이 값

    [Header("# 9. 사냥 본능: 엘리트·중간보스 피해 (메인 보스 제외)")]
    public float[] huntBonus = { 0.1f, 0.2f, 0.3f };

    [Header("# 10. 쌍둥이 무기 (보조 활·헤비보우건 투사체, 보조 쌍검 칼, 레벨별 고정값)")]
    public int[] twinProjectiles = { 1, 2 };
    public int[] twinBlades = { 1, 1 };
    [Min(0f)] public float twinSpreadAngle = 10f;                // 추가 투사체 사이 각도
    [Range(0f, 1f)] public float twinSameTargetMul = 0.5f;       // 같은 발사의 투사체가 같은 적을 또 맞히면 2번째부터 피해 배율
    [Min(1f)] public float twinBladeSizeMul = 1.3f;              // 쌍둥이 무기 Lv2: 보조 쌍검 칼 판정·그림 크기
    [Min(1f)] public float rerollFxSize = 51f;                   // 리롤 반짝임 크기 (px)
    [Range(0f, 1f)] public float rerollFxAlpha = 0.5f;

    [Header("# 11. 보호막: 일정 시간마다 피해 1회 막음")]
    public float[] shieldInterval = { 15f, 12f, 10f };
    public Color shieldColor = new Color(0.45f, 0.8f, 1f, 0.35f);
    [Min(0.1f)] public float shieldSize = 1.6f;

    [Header("# 12. 냉기 발자국: 주변 일반 몬스터 이동속도 감소 (보스·중간보스 제외)")]
    public float[] frostSlow = { 0.1f, 0.15f, 0.2f };
    [Min(0.1f)] public float frostRadius = 2f;
    public Color frostColor = new Color(0.6f, 0.9f, 1f, 0.55f);

    [Header("# 13. 위기 탈출: 체력이 기준 이하가 되면 무적 + 밀쳐내기 (낮·밤마다 1회)")]
    public int escapeMaxLevel = 1;
    [Range(0.01f, 1f)] public float escapeHealthRatio = 0.25f;
    [Min(0f)] public float escapeInvincibleTime = 2f;
    [Min(0.1f)] public float escapePushRadius = 3f;
    [Min(0f)] public float escapePushForce = 8f;
    [Min(1f)] public float escapeBlinkSpeed = 14f;
    [Min(0f)] public float crisisPushRangeMul = 1.5f;            // 밀쳐내기 범위 배율 (escapePushRadius x 이 값)
    [Min(0f)] public float crisisPushForceMul = 2.25f;            // 밀쳐내기 힘 배율 (escapePushForce x 이 값)

    [Header("# 14. 생명의 일격")]
    public int[] lifeKillsPer = { 100, 80, 60 };                // 처치 N마리마다 최대 체력 +lifeMaxHpStep
    [Min(0f)] public float lifeMaxHpStep = 0.01f;
    [Min(0f)] public float lifeMaxHpCap = 0.2f;                 // 이 카드로 오르는 분량 최대
    public float[] lifeStrikeRatio = { 0.15f, 0.25f, 0.35f };   // 최종 최대 체력 대비 추가 피해
    [Min(0.1f)] public float lifeStrikeInterval = 5f;
    public Color lifeStrikeGlow = new Color(1f, 0.35f, 0.3f, 1f);

    [Header("# 넉백 (적중 시 확률로 발동, 거리 = knockbackBaseDist x (1 + 밀어내기 단계))")]
    [Min(0f)] public float knockbackBaseDist = 1f;
    [Min(0.01f)] public float knockbackPushTime = 0.15f;          // 밀려나는 시간
    [Min(0f)] public float knockbackCooldown = 0.5f;             // 같은 몬스터가 다시 밀리기까지
    [Min(0f)] public float subKnockbackMul = 0.5f;               // 보조무기 = 같은 무기 좌클릭 확률 x 이 값
    [Min(0f)] public float miniBossKnockbackMul = 0.5f;          // 중간보스 확률 배율 (보스는 면역)
    [Range(0f, 1f)] public float knockDualLeft = 0.05f, knockDualRight = 0.2f, knockDualSkill = 0.1f;
    [Range(0f, 1f)] public float knockBowLeft = 0.1f, knockBowRight = 0.2f, knockBowRain = 0.05f;
    [Range(0f, 1f)] public float knockHbgLight = 0.08f, knockHbgHeavy = 0.35f, knockHbgSniper = 1f;
    [Range(0f, 1f)] public float knockGsLeft = 0.3f, knockGsJust = 1f, knockGsSkill = 0.6f;
    [Range(0f, 1f)] public float knockLanceLeft = 0.2f, knockLanceParry = 1f, knockLanceZone = 0.1f;
    [Range(0f, 1f)] public float knockExplosion = 0.3f;

    // 무기 번호(0 쌍검, 1 활, 2 헤비보우건, 3 대검, 4 랜스)의 좌클릭 넉백 확률 (헤비보우건은 중량탄)
    public float KnockLeft(int weaponId)
    {
        switch (weaponId)
        {
            case 0: return knockDualLeft;
            case 1: return knockBowLeft;
            case 2: return knockHbgHeavy;
            case 3: return knockGsLeft;
            case 4: return knockLanceLeft;
            default: return 0f;
        }
    }

    [Header("# 기믹 이펙트 프레임 (Shield&ice_effact, lightning_effect)")]
    public Sprite[] shieldFrames;     // 보호막 (반복)
    public Sprite[] frostFrames;      // 냉기 발자국 (느려진 몬스터 발밑, 반복)
    public Sprite[] lightningFrames;  // 연쇄 번개
    [Min(1f)] public float shieldFps = 8f;
    [Min(1f)] public float frostFps = 8f;
    [Min(0.1f)] public float frostSize = 1.1f;
    public Color counterInstinctColor = new Color(1f, 0.75f, 0.35f, 0.9f);   // 반격 본능 발동 중 오라 색
    [Min(0.1f)] public float counterInstinctAuraSize = 1.4f;
    public Color frostBodyTint = new Color(0.7f, 0.88f, 1f, 1f);   // 냉기 발자국에 걸린 몬스터 몸 색 (곱함)
    [Range(0f, 1f)] public float shieldPulse = 0.2f;               // 보호막 숨쉬기 (투명도)

    [Header("# 활성 기믹 표시 (한계 돌파·위기 탈출·생명의 일격·집중)")]
    [Min(1f)] public float buffIconSize = 42f;                     // px (캔버스 1px = 0.01 월드)
    [Min(0f)] public float buffIconSpacing = 4f;
    [Range(0f, 1f)] public float buffAlphaMin = 0.22f;
    [Range(0f, 1f)] public float buffAlphaMax = 1f;
    [Min(0f)] public float buffHeadOffsetY = 1.2f;                 // 우클릭 쿨타임 표시가 없을 때 캐릭터 위치에서 위로 (월드)
    [Min(0f)] public float buffAboveChargeGap = 0.08f;
    [Range(0f, 1f)] public float buffInactiveGray = 0.6f;          // 비발동 아이콘 회색 정도 (0 = 원래 색)
    [Min(1f)] public float buffActiveScale = 1.12f;                // 발동 중 아이콘 크기
    public float frostOffsetY = -0.12f;                            // 냉기 발자국 오라 위치 (몬스터 기준, 월드)              // 우클릭 쿨타임 표시 위 간격 (월드)

    [Header("# 레벨업 화면 스프라이트 (메뉴 MonNyang > 레벨업 UI 에셋 설정 으로 채움)")]
    public Sprite frame;              // 갈색 목재 프레임 큰 판넬
    public Sprite header;             // 제목 띠 (잎 장식)
    public Sprite box;                // 양피지 칸 묶음 판
    public Sprite card;               // 카드 (위 이름판, 아래 단계판 포함)
    public Sprite slot;               // 무기·기믹 칸
    public Sprite levelTag;           // 칸 아래 레벨 표시 (작은 갈색 판)
    public Sprite badgeWeapon;        // 무기 묶음 표시 (교차한 검)
    public Sprite badgeGimmick;       // 기믹 묶음 표시 (발바닥)
    public Sprite divider;
    public Sprite leaf;
    public Sprite buttonGreen;
    public Sprite buttonBrown;
    public Sprite buttonGrey;
    public Sprite rerollIcon;
    public Sprite superMagnetIcon;    // 슈퍼 그물망 전용 붉은 그물 (Object_Net 복사본, 포획용 그물은 원래 색)
    public Sprite selectIcon;

    [Header("# 레벨업 화면 배치 (1920x1080 기준, 가운데 0,0)")]
    public Vector2 panelSize = new Vector2(1060f, 1010f);
    public Vector2 panelPosition = new Vector2(0f, -4f);
    [Min(0.05f)] public float pixelScale = 1f;                   // 9-slice 테두리 굵기 배율 (작을수록 두껍게)
    public Vector2 headerSize = new Vector2(880f, 92f);
    [Min(1)] public int headerFontSize = 32;
    public Vector2 headerTextOffset = new Vector2(0f, 4f);         // 제목 글자 위치 보정 (띠 그림의 판이 위쪽에 있음)
    public Vector2 weaponBoxPosition = new Vector2(0f, 300f);
    public Vector2 weaponSlotSize = new Vector2(96f, 96f);
    public Vector2 weaponSlotGap = new Vector2(16f, 34f);
    public Vector2 gimmickBoxPosition = new Vector2(0f, 26f);
    [Min(1)] public int gimmickSlotCount = 14;
    [Min(1)] public int gimmickColumns = 7;
    public Vector2 gimmickSlotSize = new Vector2(80f, 80f);
    public Vector2 gimmickSlotGap = new Vector2(14f, 34f);
    [Min(1)] public int slotLevelFontSize = 20;
    public Vector2 cardSize = new Vector2(300f, 362f);
    public float cardY = -294f;
    public float cardSpacing = 322f;
    public float cardNameFromTop = 62f;                          // 카드 위 이름판 가운데 (카드 위쪽에서, 그림 픽셀)
    public float cardStepFromBottom = 66f;                       // 카드 아래 단계판 가운데 (카드 아래쪽에서, 그림 픽셀)
    [Min(1)] public int cardNameFontSize = 28;
    [Min(1)] public int cardDescFontSize = 20;
    [Min(1)] public int cardStepFontSize = 22;
    public Vector2 confirmSize = new Vector2(270f, 60f);
    public float confirmY = -494f;
    [Min(1)] public int confirmFontSize = 30;
    public Vector2 rerollSize = new Vector2(190f, 60f);
    public float rerollX = 300f;
    [Min(1f)] public float cardHoverScale = 1.03f;
    [Min(1f)] public float cardSelectedScale = 1.06f;
    public Color cardDimColor = new Color(0.8f, 0.77f, 0.73f, 1f);
    public Color textDark = new Color(0.27f, 0.15f, 0.07f, 1f);
    public Color textLight = new Color(0.97f, 0.91f, 0.79f, 1f);
    public Color textAccent = new Color(0.2f, 0.5f, 0.12f, 1f);
    public Color emptySlotTint = new Color(1f, 1f, 1f, 0.55f);
    public Color tagColorWeapon = new Color(0.86f, 0.55f, 0.28f, 1f);       // 카드 이름표: 무기 (주황 갈색)
    public Color tagColorConsumable = new Color(0.45f, 0.72f, 0.38f, 1f);   // 소모품 (초록)
    public Color tagColorGimmick = new Color(0.42f, 0.58f, 0.92f, 1f);      // 기믹 (파랑)
    public Vector2 cardTagSize = new Vector2(214f, 66f);           // 카드 그림 속 이름판을 덮을 만큼
    [Min(0.1f)] public float goldBoxIconScale = 1.3f;            // 금화 상자 아이콘 표시 크기
    public float cardIconGap = 12f;                              // 이름표 ↔ 아이콘 간격
    [Min(0.1f)] public float cardIconSize = 84f;
    public float cardDescGap = 10f;                              // 설명 위아래 여백

    [Header("# 메인무기 획득 안내 창")]
    [Min(0.1f)] public float mainWeaponPanelScale = 0.8f;
    [Range(0f, 1f)] public float mainWeaponBgAlpha = 0.5f;
}
