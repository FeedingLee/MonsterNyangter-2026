using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// ================================================================
// 메인무기 설정 (GameManager 인스펙터의 Main Weapon 항목에서 조정)
// 피해량은 모두 "무기 공격력 x 배율"이라 보조무기 단계에서 얻은 능력치가 그대로 이어진다.
// ================================================================
[System.Serializable]
public class MainWeaponSettings
{
    [Min(1)] public int mainWeaponLevel = 5;                 // 이 레벨에 처음 도달한 무기가 메인무기가 됨
    public KeyCode skillKey = KeyCode.R;                     // 전용 스킬 입력 키 (랜스 방어는 우클릭)

    [Header("전환 안내 UI")]
    public Font guideFont;                                   // 비우면 기본 폰트
    [Min(1)] public int guideTitleSize = 40;
    [Min(1)] public int guideBodySize = 26;
    [Min(0f)] public float guideShowTime = 8f;               // 표시 시간(초), 이후 서서히 사라짐
    public Color guideBackgroundColor = new Color(0f, 0f, 0f, 0.75f);
    public Color guideTextColor = Color.white;

    [Header("들고 있는 무기 스프라이트 (무기마다 따로 설정)")]
    public HeldWeaponSettings bowHeld = new HeldWeaponSettings();
    public HeldWeaponSettings heavyBowgunHeld = new HeldWeaponSettings();

    [Header("스킬 게이지 (캐릭터 아래)")]
    public Vector2 gaugeSize = new Vector2(1f, 0.1f);
    public float gaugeOffsetY = -0.75f;
    public bool gaugeBelowHpBar = true;                      // 켜면 게이지를 체력바 바로 아래에 (화면 비율·카메라 확대와 상관없이), 끄면 gaugeOffsetY
    [Min(0f)] public float gaugeHpGap = 0.05f;               // 체력바와 게이지 사이 간격 (월드)
    public Color gaugeBackColor = new Color(0f, 0f, 0f, 0.6f);
    public Color cooldownColor = new Color(0.3f, 0.75f, 1f, 1f);
    public Color ammoColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Color activeColor = new Color(1f, 0.4f, 0.3f, 1f);

    [Header("전용 스킬 버튼 (화면 오른쪽 아래 모서리 기준, 게임 중 변경도 바로 반영)")]
    public Vector2 skillButtonOffset = new Vector2(-195f, 330f);
    [Min(1f)] public float skillButtonSize = 120f;
    [Range(0.2f, 2f)] public float skillButtonScale = 0.7f;  // 버튼 전체 배율 (아이콘·글자 정렬을 유지한 채 함께 축소)
    [Range(0.1f, 1f)] public float skillButtonIconScale = 0.65f;   // 버튼 대비 아이콘 크기
    public Sprite skillButtonSprite;                         // 버튼 모양 (비우면 원)

    [Header("전용 스킬 버튼 글자")]
    [Min(1)] public int skillButtonFontSize = 24;
    public Font skillButtonFont;                             // 비우면 안내 UI 폰트 또는 기본 폰트
    public Vector2 skillButtonLabelOffset = new Vector2(0f, -84f);   // 버튼 중앙 기준 글자 위치
    public Color skillButtonLabelColor = Color.white;
    [Min(0f)] public float skillButtonOutlineWidth = 2f;     // 글자 테두리 두께 (0이면 없음)
    public Color skillButtonOutlineColor = Color.black;
    public Color skillButtonColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
    public Color skillButtonActiveColor = new Color(0.8f, 0.35f, 0.2f, 0.9f);  // 스킬 사용 중
    public Color skillButtonOverlayColor = new Color(0f, 0f, 0f, 0.65f);       // 쿨타임 표시

    [Header("능력치 적용 한계 (메인무기 기획서 v2.1 2.5절)")]
    [Min(1f)] public float attackSpeedMax = 2f;               // 공격속도 배율 상한 (동작 시간 = 기본 / 배율)
    [Range(0.05f, 1f)] public float attackCooldownMin = 0.4f;  // 공격 대기·충전 시간 하한 (기본 대비)
    [Range(0.05f, 1f)] public float skillCooldownMin = 0.5f;   // 무기 스킬(R) 쿨타임 하한 (기본 대비)

    [Header("무기별 설정")]
    public DualBladesMainSettings dualBlades = new DualBladesMainSettings();
    public BowMainSettings bow = new BowMainSettings();
    public HeavyBowgunMainSettings heavyBowgun = new HeavyBowgunMainSettings();
    public GreatSwordMainSettings greatSword = new GreatSwordMainSettings();
    public LanceMainSettings lance = new LanceMainSettings();
}

// 캐릭터 위에 겹쳐 그려 조준 방향으로 돌아가는 무기 스프라이트 설정
[System.Serializable]
public class HeldWeaponSettings
{
    public Sprite sprite;                                    // 비우면 아이템 아이콘
    [Min(0f)] public float distance = 0.4f;                  // 캐릭터 중심에서 떨어진 거리
    public float offsetY = 0f;                               // 위아래 위치 (+ 위, - 아래)
    public float angleOffset = 0f;                           // 오른쪽 조준 기준으로 맞추면 왼쪽은 자동으로 좌우 대칭
    [Min(0.01f)] public float scale = 1f;
    public int sortingOffset = 1;                            // 캐릭터보다 위에 그리기
}

[System.Serializable]
public class DualBladesMainSettings
{
    [Header("기본 공격: 왼쪽 베기 → 오른쪽 베기 → 회전")]
    [Min(0f)] public float basicDamageMultiplier = 1f;
    [Min(0f)] public float bladeDistance = 1.5f;             // 캐릭터에서 칼까지 거리
    [Range(0f, 360f)] public float slashArc = 150f;          // 베기 각도
    [Min(0.01f)] public float slashTime = 0.18f;             // 베기 한 번에 걸리는 시간
    [Min(0f)] public float stepDelay = 0.2f;                 // 다음 단계까지 대기 (쿨타임 감소 영향)
    [Min(0f)] public float spinDuration = 1f;                // 회전 지속시간
    [Min(0f)] public float spinSpeed = 1080f;                // 회전 속도(도/초)
    [Min(0f)] public float comboCooldown = 0.8f;             // 3단계 이후 다시 공격 가능까지 (무기 쿨타임 영향)
    [Min(1f)] public float returnSpeed = 1080f;              // 원위치로 돌아갈 때 회전 속도(도/초)

    [Header("대기 자세: 날개 모양 한 세트가 조준 방향에 맞춰 캐릭터 주위를 함께 회전")]
    [Range(-180f, 180f)] public float restReferenceAngle = 90f;   // 아래 자세 그대로 보일 조준 방향 (90 = 위, 0 = 오른쪽)
    public Vector2 restOffset = new Vector2(1.1f, -0.15f);   // 기준 방향일 때 오른쪽 칼 위치 (왼쪽 칼은 좌우 대칭)
    [Range(-90f, 90f)] public float restTilt = 20f;          // 기준 방향일 때 칼 윗부분이 바깥쪽으로 기우는 각도
    public bool restUpsideDown = false;                      // 칼이 거꾸로 보이면 체크
    public bool mirrorLeftBlade = true;                      // 왼쪽 칼 스프라이트를 좌우 반전해 대칭으로 보이게
    [Min(0.1f)] public float returnMoveSpeed = 12f;          // 원위치로 돌아가는 이동 속도

    [Header("전용 스킬: 돌진 회전베기")]
    public Sprite skillIcon;                                 // 스킬 버튼 아이콘 (비우면 무기 최대 레벨 아이콘)
    [Min(0f)] public float dashCooldown = 6f;
    [Min(0f)] public float dashDistance = 5f;
    [Min(0.01f)] public float dashTime = 0.3f;               // 돌진에 걸리는 시간 (이동 중 무적)
    [Min(0f)] public float dashDamageMultiplier = 2f;
    [Min(0.1f)] public float dashHitRadius = 1.6f;           // 지나간 경로에서 피해를 주는 반경
    [Min(0f)] public float dashSpinSpeed = 1800f;

    [Header("우클릭: 백대쉬 (마우스 반대 방향, 무적 없음, 공격을 끊지 않음)")]
    [Min(0f)] public float backDashDistance = 2f;
    [Min(0.01f)] public float backDashTime = 0.08f;          // 이동에 걸리는 시간 (짧을수록 즉시 이동처럼 보임)
    [Min(1)] public int backDashMaxCharge = 3;
    [Min(0.01f)] public float backDashChargeTime = 4f;       // 1회 충전 시간
    public AudioClip backDashSound;                          // 비우면 기존 효과음
    public AudioClip backDashReadySound;                     // 충전 완료
}

[System.Serializable]
public class BowMainSettings
{
    [Header("기본 공격: 좌클릭 단발 (누르고 있으면 자동)")]
    [Min(0f)] public float basicDamageMultiplier = 1f;
    [Min(0.05f)] public float baseCooldown = 0.45f;          // 발사 간격 (무기 쿨타임 영향)
    [Min(1)] public int fanCount = 5;                        // 부채꼴 발수
    [Range(0f, 180f)] public float fanAngle = 50f;           // 부채꼴 전체 각도

    [Header("우클릭: 부채꼴 사격 (충전형)")]
    [Min(1)] public int fanMaxCharge = 3;
    [Min(0.01f)] public float fanChargeTime = 3f;            // 1회 충전 시간
    [Min(0f)] public float fanDelay = 1f;                    // 우클릭 연속 발사 사이 딜레이
    public AudioClip fanSound;                               // 비우면 활 발사음
    public AudioClip fanReadySound;                          // 충전 완료

    [Header("전용 스킬: 화살비")]
    public Sprite skillIcon;                                 // 스킬 버튼 아이콘 (비우면 무기 최대 레벨 아이콘)
    [Min(0f)] public float rainCooldown = 10f;
    [Min(0.1f)] public float rainRadius = 2.5f;
    [Min(0f)] public float rainDuration = 3f;
    [Min(0f)] public float rainDelay = 1f;                   // '피융' 소리 뒤 화살비가 쏟아지기까지 (초)
    public AudioClip rainLaunchSound;                        // 화살을 하늘로 쏘아 올리는 소리 (비우면 활 발사음)
    [Min(0.02f)] public float rainDustInterval = 0.12f;      // 범위 가장자리 흙먼지 구름이 피어오르는 간격 (초)
    [Min(0f)] public float rainTickDamageMultiplier = 0.5f;
    [Min(0.05f)] public float rainTickInterval = 0.25f;
    public Color indicatorColor = new Color(1f, 1f, 1f, 0.35f); // 위치 지정 중 범위 표시
    public Color rainAreaColor = new Color(1f, 0.35f, 0.3f, 0.4f);
    public int rainAreaSortingOrder = 1;                     // 범위 표시 정렬 순서 (몬스터와 같은 정렬 레이어에서, 살아 있는 몬스터 2보다 낮아야 몬스터가 위에 보임)

    [Header("화살비 연출")]
    public Sprite rainArrowSprite;                           // 비우면 화살 발사체 스프라이트
    [Min(0f)] public float rainArrowsPerSecond = 30f;
    [Min(0.01f)] public float rainArrowScale = 0.8f;
    [Min(0f)] public float rainFallHeight = 3f;
    [Min(0.01f)] public float rainFallTime = 0.15f;
}

[System.Serializable]
public class HeavyBowgunMainSettings
{
    [Header("중량탄 (기본 장착): 무한 관통")]
    [Min(1)] public int heavyMax = 2;                        // 최대 탄 수
    [Min(0f)] public float heavyDamage = 150f;               // 1발 피해 (캐릭터 공격력 배율 적용)
    [Min(0f)] public float heavyInterval = 1f;               // 발사 후 다음 발사까지 (초)
    [Min(0.1f)] public float heavyRechargeTime = 5f;         // 1발 충전 시간 (초)
    [Min(0.1f)] public float heavyBulletSpeed = 40f;         // 탄속

    [Header("연사탄")]
    [Min(1)] public int rapidMax = 50;                       // 최대 탄 수
    [Min(0f)] public float rapidDamage = 10f;                // 1발 피해 (캐릭터 공격력 배율 적용)
    [Min(0)] public int rapidPierce = 0;                     // 기본 관통 (관통 화살촉 효과가 더해짐)
    [Min(0.1f)] public float rapidPerSecond = 10f;           // 연사 속도 (발/초)
    [Min(0f)] public float rapidRechargePerSecond = 5f;      // 충전 속도 (발/초)
    [Min(0.1f)] public float rapidBulletSpeed = 20f;

    [Header("탄 변경 (우클릭)")]
    [Min(0f)] public float switchTime = 1f;                  // 변경에 걸리는 시간 (그동안 발사 불가). 탄을 다 써도 자동으로 바뀌지 않음 (우클릭으로만)
    public AudioClip switchSound;                            // 찰칵 재장전 (비우면 기본 효과음)

    [Header("R 스킬: 정밀 저격")]
    public Sprite skillIcon;                                 // 스킬 버튼 아이콘 (비우면 무기 최대 레벨 아이콘)
    [Min(0f)] public float sniperCooldown = 30f;             // 스킬이 끝난 뒤부터
    [Min(0.5f)] public float sniperDuration = 10f;           // 최대 지속
    [Min(0f)] public float stealthDuration = 5f;             // 발동 즉시 은신 시간
    [Min(1)] public int sniperShots = 3;
    [Min(0.1f)] public float sniperRadius = 2f;              // 조준 지점 광역 반경
    [Min(0f)] public float sniperMobDamage = 500f;           // 일반 몬스터 피해
    [Min(0f)] public float sniperBossDamage = 150f;          // 보스·중간보스 피해 (1회 상한 160)
    [Range(0f, 1f)] public float stealthAlpha = 0.45f;       // 은신 중 캐릭터 투명도
    [Range(0f, 1f)] public float scopeMoveScale = 0.2f;      // 스코프 중 이동속도 배율 (원래의 20%까지 감소)

    [Header("정밀 저격 화면 (Heavy_Skill 시트)")]
    public int scopeSortingOrder = 26;
    [Range(0.2f, 1.2f)] public float scopeScreenFraction = 0.72f;   // 스코프 지름 / 화면 짧은 변 (scopeFitRange를 끄면 사용)
    public bool scopeFitRange = true;                                // 스코프 렌즈(구멍) 지름을 실제 피해 범위(sniperRadius)에 맞춤
    [Min(0.1f)] public float scopeRangeFit = 1f;                     // 렌즈 지름 / 피해 지름 (1 = 정확히 일치)
    [Range(0.5f, 1f)] public float scopeHoleRatio = 0.86f;          // 어둠 구멍 지름 / 스코프 지름
    [Range(0f, 1f)] public float scopeDarkAlpha = 0.9f;            // 스코프 밖 어둠
    [Min(1f)] public float scopeOpenFps = 26f;
    [Min(1f)] public float scopeFireFps = 22f;
    [Min(1f)] public float scopeCloseFps = 14f;
    public float scopeBulletRightMargin = 80f;               // 오른쪽 탄 아이콘: 화면 오른쪽에서
    public float scopeBulletSpacing = 62f;
    public Vector2 scopeBulletSize = new Vector2(120f, 66f);

    [Header("정밀 저격 효과음 (비우면 기본 효과음)")]
    public AudioClip scopeInSound;
    public AudioClip sniperShotSound;
    [Min(0f)] public float sniperShotSoundStart = 0.5f;     // 발사음 재생 시작 위치(초): 파일 앞 무음을 건너뛰어 발사와 맞춤
    [Min(0f)] public float shellDropDelay = 0.6f;           // 발사 뒤 장전음(shellDropSound)까지 걸리는 시간 (남은 탄이 있을 때만)
    public AudioClip glassBreakSound;
    public AudioClip shellDropSound;
    public AudioClip scopeOutSound;

    [Header("탄 표시 (화면 왼쪽, 보관 슬롯 위에 줄로)")]
    public Vector2 ammoRowSize = new Vector2(118f, 40f);     // 한 줄 크기
    [Min(0f)] public float ammoRowGap = 6f;                  // 줄 사이 간격
    public float ammoRowLeft = 12f;                          // 보관 슬롯을 못 찾을 때 위치 (왼쪽 아래 기준)
    public float ammoRowBottom = 300f;
    [Range(0f, 1f)] public float ammoRowIdleAlpha = 0.55f;   // 장착하지 않은 탄 줄 투명도
    [Min(8)] public int ammoHudFontSize = 24;
    public Color rapidGaugeColor = new Color(1f, 0.55f, 0.2f, 1f);
    public Color ammoFullColor = new Color(0.35f, 0.85f, 0.3f, 1f);   // 탄창이 가득 찼을 때 (초록)
    public Color ammoEmptyColor = new Color(0.9f, 0.22f, 0.18f, 1f);  // 탄창이 거의 비었을 때 (빨강). 그 사이는 노랑을 거쳐 자연스럽게

    // 남은 탄 비율(0~1)에 따른 탄창 색: 가득 = 초록, 줄어들수록 노랑 → 빨강
    public Color AmmoColor(float ratio)
    {
        Color.RGBToHSV(ammoEmptyColor, out float h0, out float s0, out float v0);
        Color.RGBToHSV(ammoFullColor, out float h1, out float s1, out float v1);
        float k = Mathf.Clamp01(ratio);
        return Color.HSVToRGB(Mathf.Lerp(h0, h1, k), Mathf.Lerp(s0, s1, k), Mathf.Lerp(v0, v1, k));
    }
    [Min(1)] public int rapidSegments = 10;                  // 연사탄 탄창 칸 수 (캐릭터 게이지·왼쪽 표시, 한 칸 = 최대 탄 수 / 칸 수)
    [Min(0.04f)] public float magazineHeight = 0.13f;        // 캐릭터 아래 탄창 게이지 높이 (월드)
    [Min(0f)] public float magazineGap = 0.022f;             // 탄창 칸 사이 간격 (월드)
    public Color magazineFrameColor = new Color(0.16f, 0.1f, 0.06f, 1f);   // 탄창 테두리
    public Color magazineEmptyColor = new Color(0.3f, 0.27f, 0.24f, 1f);   // 빈 칸

    [Header("시너지 (쌍검)")]
    [Min(1)] public int bladeEveryRapidShots = 25;           // 공명 「칼날 연사」: 연사탄 누적 발수마다 칼날 발사
    [Min(0)] public int awakenBounces = 3;                   // 각성 「칼날 중량탄」: 화면 테두리에서 튕기는 횟수
    [Min(1)] public int awakenBossHits = 2;                  // 각성: 보스는 탄 1발당 최대 피격 수
    [Min(0.1f)] public float awakenSlugLength = 0.9f;        // 각성 날카로운 총알 길이 (월드)

    [Header("연사탄 모양 / 효과음")]
    public Sprite rapidBulletSprite;                         // 비우면 빛나는 탄 궤적
    public Color rapidBulletColor = new Color(1f, 0.55f, 0.2f, 1f);
    [Min(0.01f)] public float rapidBulletScale = 0.7f;       // 기본 탄환 대비 크기
    public AudioClip rapidSound;                             // 비우면 활 발사음 사용
}

[System.Serializable]
public class GreatSwordChargeStage
{
    [Min(0f)] public float holdTime;                         // 이 단계가 되기까지 누르고 있어야 하는 시간
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(1)] public int spins = 1;                           // 회전 횟수
    [Min(0f)] public float distance = 1f;                    // 전진 거리
    public Color color = Color.white;                        // 기 모으기 표시 색

    public GreatSwordChargeStage() { }

    public GreatSwordChargeStage(float holdTime, float damageMultiplier, int spins, float distance, Color color)
    {
        this.holdTime = holdTime;
        this.damageMultiplier = damageMultiplier;
        this.spins = spins;
        this.distance = distance;
        this.color = color;
    }
}

[System.Serializable]
public class GreatSwordMainSettings
{
    [Header("기본 공격: 회전 후 검기 (누르고 있으면 쿨타임마다 반복)")]
    [Min(0f)] public float basicDamageMultiplier = 1.5f;
    [Min(1)] public int spinCount = 1;                       // 회전 횟수
    [Min(0.1f)] public float spinSpeedMultiplier = 2f;       // 회전 속도 = 무기 회전 속도(공격속도) x 배율
    [Min(0.05f)] public float baseCooldown = 1f;             // 다음 공격까지 (무기 쿨타임 영향)
    [Min(0.1f)] public float hitWidthScale = 1f;             // 공격 판정 폭 (1 = 대검 스프라이트 폭)
    [Min(0.1f)] public float hitLengthScale = 1f;            // 공격 판정 길이 (1 = 대검 스프라이트 길이)

    [Header("기본 공격: 내려찍기 → 후리기 (피해 배율 기준 = 위 기본 공격 피해)")]
    [Min(0f)] public float slamDamageMul = 2f;
    [Min(0.1f)] public float slamRange = 2f;                 // 캐릭터 앞 범위 (원 중심 = 조준 방향 0.6배 지점)
    [Min(0.01f)] public float slamRaiseTime = 0.14f;         // 들어 올리기 (공격속도 영향)
    [Min(0.01f)] public float slamStrikeTime = 0.08f;        // 내려찍기 (공격속도 영향)
    [Min(0f)] public float sweepDamageMul = 1.2f;
    [Min(0.1f)] public float sweepRange = 2.5f;              // 캐릭터 주위 반경
    [Min(0.01f)] public float sweepTime = 0.28f;             // 한 바퀴 (공격속도 영향)
    [Min(0f)] public float recoverTime = 1.5f;               // 찍힌 상태에서 원위치까지

    [Header("우클릭: 저스트 올려치기 (찍힌 상태에서만, 1회 판정)")]
    [Min(0f)] public float justStart = 0.2f;                 // 찍힌 뒤 저스트 구간 시작
    [Min(0.01f)] public float justWindow = 0.4f;             // 저스트 구간 길이
    [Min(0f)] public float upperDamageMul = 1.5f;
    [Min(0.1f)] public float upperRange = 2.5f;
    [Min(0.01f)] public float upperSwingTime = 0.1f;         // 올려치기 동작 (공격속도 영향)
    [Min(0f)] public float justRecoverTime = 0.2f;           // 성공 시 원위치
    [Min(0f)] public float failRecoverTime = 0.5f;           // 실패 시 원위치
    public Color justFlashColor = new Color(1f, 0.95f, 0.55f, 1f);   // 저스트 구간 대검 반짝임 색
    [Min(0.02f)] public float justGlintInterval = 0.12f;     // 저스트 구간 칼끝 큰 반짝임 간격
    [Min(0.1f)] public float justGlintSize = 1.8f;           // 칼끝 큰 반짝임 크기
    [Min(1f)] public float justGlowScale = 1.25f;            // 저스트 구간 대검 뒤 금빛 후광 크기 (대검 대비)

    [Header("내려찍기 범위 표시 (바닥 원)")]
    public Color slamRangeColor = new Color(1f, 0.85f, 0.4f, 1f);
    [Range(0f, 1f)] public float slamRangeIdleAlpha = 0.12f; // 평상시 (0이면 숨김)
    [Range(0f, 1f)] public float slamRangeWindupAlpha = 0.4f; // 들어 올리는 동안
    public int slamRangeSortingOrder = 1;                    // 몬스터보다 아래

    [Header("끌고 다니기 (연출만, 이동속도 변화 없음)")]
    [Range(0f, 3f)] public float dragDown = 0.9f;            // 칼끝이 아래로 기우는 정도 (뒤쪽 1 기준)
    [Min(0.02f)] public float dragDustInterval = 0.15f;
    [Min(0.05f)] public float dragSoundInterval = 0.6f;
    [Min(0.1f)] public float spinExpandEase = 2f;
    [Min(30f)] public float chargeSpinSpeed = 1162f;        // 모아 베기 대회전 속도 (도/초, 공격속도 영향 없음)            // 모아 베기 대회전 반경이 넓어지는 곡선 (클수록 처음에 빨리)

    [Header("기본 공격 효과음 (비우면 기존 효과음, 끄는 소리·원위치는 비우면 없음)")]
    public AudioClip slamSound;
    public AudioClip sweepSound;
    public AudioClip dragSound;
    public AudioClip justSparkSound;
    public AudioClip justSuccessSound;
    public AudioClip recoverSound;

    [Header("검기")]
    [Min(0f)] public float waveDamageMultiplier = 1.5f;
    public Sprite waveSprite;                                // 비우면 대검 스프라이트
    public Color waveColor = new Color(0.6f, 0.9f, 1f, 0.8f);
    public float waveAngleOffset = 0f;                       // 스프라이트 방향 보정
    [Min(0.01f)] public float waveScale = 1f;
    [Min(0.1f)] public float waveSpeed = 12f;
    [Min(0f)] public float waveStartDistance = 1f;
    [Min(0.1f)] public float waveLifetime = 1.2f;

    [Header("전용 스킬: 모아 베기")]
    public Sprite skillIcon;                                 // 스킬 버튼 아이콘 (비우면 무기 최대 레벨 아이콘)
    [Min(0f)] public float chargeCooldown = 8f;
    public Sprite chargeSprite;                              // 기 모으기 스프라이트 (비우면 원)
    [Min(0.1f)] public float chargeEffectSize = 2.5f;
    public GreatSwordChargeStage[] chargeStages =
    {
        new GreatSwordChargeStage(0f,   2f,   1, 2f, new Color(1f, 1f, 1f, 0.45f)),
        new GreatSwordChargeStage(0.8f, 3.5f, 2, 3.5f, new Color(1f, 0.85f, 0.3f, 0.55f)),
        new GreatSwordChargeStage(1.6f, 6f,   3, 5f, new Color(1f, 0.3f, 0.2f, 0.65f)),
    };
}

[System.Serializable]
public class LanceMainSettings
{
    [Header("기본 공격: 짧게 → 짧게 → 길게")]
    [Min(0f)] public float basicDamageMultiplier = 1f;
    public float[] thrustDistances = { 0.6f, 0.6f, 1.5f };
    [Min(0.01f)] public float thrustTime = 0.15f;            // 찌르기 동작 시간 (공격속도가 높을수록 짧아짐)
    [Min(0f)] public float thrustInterval = 0.08f;           // 찌르기 사이 간격 (쿨타임 감소 영향)
    [Min(0.05f)] public float baseCooldown = 0.8f;           // 찌르기 연계 전체의 대기 시간 (무기 쿨타임 영향)

    [Header("패링 (우클릭)")]
    [Min(0f)] public float parryCooldown = 5f;               // 패링 쿨타임
    [Min(0.01f)] public float parryWindow = 0.24f;           // 누른 뒤 이 시간 안에 피해를 받으면 패링
    [Min(0.1f)] public float parryRadius = 2.5f;             // 패링 광역 피해 범위
    [Min(0f)] public float parryDamageMultiplier = 3f;
    [Min(0f)] public float parryInvincibleTime = 0.4f;       // 패링 직후 연속 피격 방지
    public Color parryColor = new Color(1f, 0.9f, 0.4f, 0.6f);

    [Header("철벽 스택")]
    [Min(1)] public int maxStacks = 9;
    [Min(0)] public int mobParryStacks = 1;                  // 일반 몬스터, 중간보스 패링
    [Min(0)] public int bossParryStacks = 3;                 // 보스 접촉, 패턴 패링
    public Color stackGainColor = new Color(1f, 0.85f, 0.35f, 1f);   // 스택 증가 연출 색
    public Color stackResetColor = new Color(1f, 0.35f, 0.3f, 1f);   // 헛방어 초기화 연출 색
    public Vector2 stackRowSize = new Vector2(132f, 44f);    // 스택 표시 크기 (헤비보우건 잔탄 표시와 같은 자리)
    public float stackRowLeft = 12f;                         // 보관 슬롯을 못 찾을 때 위치 (왼쪽 아래 기준)
    public float stackRowBottom = 300f;
    [Min(8)] public int stackFontSize = 24;
    [Min(0f)] public float stackTextRightPadding = 14f;       // 스택 숫자 오른쪽 여백 (테두리에 잘리지 않게)

    [Header("전용 스킬: 강철의 영역")]
    public Sprite skillIcon;                                 // 스킬 버튼 아이콘 (비우면 무기 최대 레벨 아이콘)
    [Min(0f)] public float zoneCooldown = 15f;
    [Min(0.5f)] public float zoneRange = 6f;                 // 영역을 지정할 수 있는 거리
    [Min(0f)] public float chargeTime = 1f;                  // 기 모으기 (공격 불가, 이동 가능)
    [Min(0f)] public float dropDelay = 2f;                   // 투창 후 거대한 창이 꽂힐 때까지
    [Min(0.1f)] public float zoneDuration = 6f;              // 영역 유지 시간
    [Min(0.1f)] public float waveInterval = 2f;              // 파장 간격
    [Min(0.1f)] public float zoneRadius = 2.5f;              // 0스택 영역 반지름
    [Min(0.1f)] public float spearLength = 3f;               // 0스택 거대한 창 길이 (월드)
    [Min(0f)] public float impactDamageMultiplier = 5f;      // 꽂힘 피해 (랜스 기본 피해 x)
    [Min(0f)] public float waveDamageMultiplier = 1.5f;      // 파장 피해 (랜스 기본 피해 x)
    [Min(1f)] public float maxSizeMul = 2f;                  // 최대 스택일 때 창·영역 크기 배율 (0스택 1배에서 선형 증가)
    [Min(1f)] public float maxDamageMul = 3f;                // 최대 스택일 때 꽂힘·파장 피해 배율
    [Min(0.05f)] public float spearFallTime = 0.25f;         // 거대한 창이 떨어지는 시간 (dropDelay에 포함)
    [Min(0f)] public float spearFallHeight = 10f;            // 떨어지기 시작하는 높이
    [Range(0f, 0.9f)] public float spearSinkRatio = 0.25f;   // 창끝이 땅에 박혀 안 보이는 비율
    [Range(0f, 80f)] public float throwAngle = 35f;          // 투창 각도 (수직에서 보는 방향으로 기울임)
    [Min(0.1f)] public float throwDistance = 14f;            // 던진 창이 날아가는 거리
    [Min(0f)] public float redrawTime = 0.15f;               // 투창 후 새 창을 꺼내 들 때까지
    public Color chargeGlowColor = new Color(1f, 0.85f, 0.4f, 1f);   // 기 모으는 동안 창 색
    public Color zoneColor = new Color(0.55f, 0.8f, 1f, 0.32f);
    public Color zoneWaveColor = new Color(0.75f, 0.9f, 1f, 0.55f);
    public int zoneSortingOrder = 1;                         // 범위 표시 정렬 순서 (몬스터보다 아래)
    public AudioClip throwSound;                             // 비우면 기존 효과음
    public AudioClip impactSound;
    public AudioClip waveSound;

    [Header("각성 「대검 해방」: 대검 3단 연계")]
    [Min(1f)] public float gsSwingAngle = 150f;              // 1·2단 부채꼴 베기 각도
    [Min(0.1f)] public float gsSwingRangeMul = 1.2f;         // 1·2단 베기 범위 배율 (대검 길이·폭)
    [Min(0.01f)] public float gsSwingTime = 0.22f;           // 1·2단 베기 시간 (공격속도 영향)
    [Min(0f)] public float gsDashDistance = 1.6f;            // 3단 돌진 거리
    [Min(0.01f)] public float gsDashTime = 0.15f;            // 3단 돌진 시간
    [Min(0f)] public float gsDashInvincible = 0.15f;         // 3단 돌진 무적 시간
    [Min(1f)] public float gsSpinSpeed = 1080f;              // 3단 회전 베기 속도 (도/초, 공격속도 영향)
    [Min(0f)] public float gsStepInterval = 0.12f;           // 연계 사이 대기 (무기 쿨타임 영향)
    [Min(0f)] public float gsComboCooldown = 0.6f;           // 3단 후 대기 (무기 쿨타임 영향)
    [Min(0f)] public float gsComboResetTime = 1.2f;          // 이 시간 동안 공격하지 않으면 1단부터
}

// ================================================================
// 메인무기 조작. 메인무기가 된 Weapon 오브젝트에 실행 중 추가된다.
// ================================================================
public class MainWeaponController : MonoBehaviour
{
    public static MainWeaponController Current { get; private set; }

    // 플레이어 이동속도, 받는 피해 배율 (헤비보우건 연사 모드, 랜스 돌진 모드)
    public static float MoveSpeedScale { get; private set; } = 1f;
    public static float DamageTakenScale
    {
        get => damageTakenBase * ChargeGuardScale * RunStats.Get(RunStat.DamageTaken);   // 능력치 「받는 피해 감소」
        private set => damageTakenBase = value;
    }
    private static float damageTakenBase = 1f;

    // 무기 스킬 모드 중 (헤비보우건 연사·정밀 저격, 랜스 돌진, 대검 철벽 모으기): 능력치 「받는 피해 감소」는 포함하지 않음
    public static bool SkillModeActive =>
        !Mathf.Approximately(MoveSpeedScale, 1f) || !Mathf.Approximately(damageTakenBase, 1f) || !Mathf.Approximately(ChargeGuardScale, 1f);

    // 공명 「철벽 모으기」: 대검 메인이 기를 모으는 동안 받는 피해 배율 (그 외 1)
    public static float ChargeGuardScale =>
        Current != null && Current.resonance && Current.weapon != null && Current.weapon.id == 3 && Current.gsCharging
            ? WeaponSynergy.Data.chargeDamageTakenRate : 1f;

    // 메인 무기 (시너지 판정용)
    public Weapon MainWeapon => weapon;
    public SpriteRenderer HeldRenderer => heldRenderer;    // 손에 든 메인무기 그림 (생명의 일격 준비 표시)

    // 시너지 공명 / 각성 효과가 켜졌는지 (각성은 공명 위에 추가)
    private bool resonance;
    private bool awakening;
    private float stormAmmoSpent;             // 헤비보우건 공명: 누적 연사탄 발수

    // 대검 모아 베기 중에는 보조무기 공격도 멈춤
    public static bool SubWeaponsPaused => Current != null && Current.gsCharging;

    // 마우스 조준 방향 (캐릭터 기준)
    public Vector3 AimDirection => aimDir;

    private static MainWeaponSettings Settings => GameManager.instance.mainWeapon;

    private Weapon weapon;
    private Player player;
    private SpriteRenderer playerSprite;
    private int hitLayer;

    // 입력
    private Vector3 aimDir = Vector3.right;
    private Vector3 mouseWorld;
    private bool mouseHoldValid;              // UI가 아닌 곳에서 눌러 계속 누르고 있는 중
    private bool mouseClicked;                // 이번 프레임에 UI가 아닌 곳을 좌클릭
    private bool rightClicked;                // 이번 프레임에 UI가 아닌 곳을 우클릭
    private bool skillButtonPressed;          // 이번 프레임에 화면 스킬 버튼을 누름
    private MainWeaponSkillButton skillButton;

    // 공통 상태
    private Coroutine attackRoutine;
    private bool isAttacking;
    private float attackReadyTime;
    private float skillReadyTime;
    private float skillCooldownLength;

    // 생성한 리소스
    private Sprite circleSprite;
    private Sprite whiteSprite;
    private SpriteRenderer heldRenderer;
    private Transform gaugeRoot;
    private SpriteRenderer gaugeBack;
    private SpriteRenderer gaugeFill;

    // 쌍검
    private Bullet[] blades;
    private readonly float[] bladeAngles = new float[2];
    private int dualStep;

    // 활
    private bool rainTargeting;
    private SpriteRenderer rainIndicator;
    private Sprite arrowSprite;
    private int enemySortingLayer;            // 몬스터 정렬 레이어 (화살비 범위를 몬스터 아래에 그리기 위함)

    // 대검
    private Bullet sword;
    private SpriteRenderer swordRenderer;
    private SpriteRenderer chargeVisual;
    private bool gsCharging;
    private Vector3 swordFarLocal;             // 대검 기존 위치 (모아 베기 회전 반경)
    private Vector3 swordHoldLocal;            // 손잡이를 쥔 위치 (기본 공격, 끌기)
    private float swordHalf = 1.3f;            // 대검 손잡이 끝~칼끝 절반 길이 (월드)
    private float swordSizeAtSetup = 1f;       // 쥔 자리를 계산할 때의 「무기·총알 크기」 (이후 크기가 바뀌면 비율만큼 바깥으로)
    private static float WeaponSizeScale => RunStats.Get(RunStat.WeaponSize);   // 「무기·총알 크기」 배율 (기본 공격 범위에 곱함)
    private int swordBaseLayer, swordBaseOrder;
    private bool swordBehind;
    private bool gsStuck;                      // 찍힌 상태
    private float gsStuckStart, gsStuckEnd;
    private bool gsCanSweep;
    private bool gsJudged;                     // 이번 찍힘에서 우클릭 판정을 했는지
    private bool gsJustSoundPlayed;
    private float nextJustSpark, nextDragDust, nextDragSound, nextJustGlint;
    private float dragLockX;                   // 원위치 중 고정한 뒤쪽 방향 (0 = 고정 없음)
    private SpriteRenderer slamRange;          // 내려찍기 범위 바닥 원
    private SpriteRenderer justGlow;           // 저스트 구간 대검 후광
    private Material justGlowMaterial;
    private bool slamWindup;                   // 들어 올리는 중 (범위 진하게)
    private Vector3 slamDir = Vector3.right;   // 들어 올린 순간의 내려찍기 방향
    private OverheadChargeUI overhead;         // 머리 위 우클릭 충전·쿨타임 표시
    private readonly ChargeCounter backDash = new ChargeCounter();
    private readonly ChargeCounter fanCharge = new ChargeCounter();
    private float fanReadyTime;
    private float gsChargeStart;
    private int gsStage;
    private float chargePulse;

    // 헤비보우건: 중량탄 / 연사탄, 정밀 저격
    private bool rapidEquipped;               // false = 중량탄 장착
    private float heavyAmmo, rapidAmmo;
    private float switchStartTime, switchEndTime = -1f;
    private bool Switching => switchEndTime > 0f;
    private bool scoping;
    private float scopeEndTime, stealthEndTime;
    private int scopeShotsLeft;
    private HeavyScopeUI scopeUI;
    private HeavyAmmoHud ammoHud;
    private SpriteFxAnimator stealthLeaves;
    private float rapidReadyTime;
    public static bool Scoping => Current != null && Current.scoping;   // 스코프 중: 이동·회피 불가
    private Sprite bulletSprite;              // 기본 탄환 모양 (연사 탄환과 번갈아 쓰므로 기억)
    private Color bulletColor = Color.white;
    private Vector3 bulletScale = Vector3.one;

    // 랜스
    private static readonly Vector3 LanceBase = new Vector3(0f, 0.8f, 0f);
    private Bullet lance;
    private Transform lanceTransform;
    private CircleCollider2D lanceCollider;
    private SpriteRenderer lanceRenderer;
    private bool guarding;
    private float guardEndTime;
    private float guardReadyTime;
    private bool parriedThisGuard;            // 이번 방어에서 막았는지 (못 막으면 헛방어)
    private int ironStacks;                   // 철벽 스택
    private LanceStackHud stackHud;
    private Vector3 lanceBaseScale = Vector3.one;
    private Vector3 lanceRootScale = Vector3.one;  // 랜스 오브젝트 원래 크기 (그림이 자식일 때)
    private static Vector3 LanceHold => LanceBase * WeaponSizeScale;   // 창을 쥔 자리: 크기만큼 바깥으로 (손잡이는 몸통, 창끝은 더 멀리)
    private float lanceHiddenUntil;           // 투창 직후 빈손
    private float lancePop;                   // 새 창을 꺼내 드는 연출
    private bool zoneTargeting;               // 강철의 영역 위치 지정 중
    private bool zoneCharging;                // 강철의 영역 기 모으기 중
    private float zoneChargeEnd;
    private Vector3 zoneTarget;
    private float nextZoneSpark;
    private SpriteRenderer zoneIndicator;
    private bool zoneIndicatorFx;
    private int zoneSortingLayer;
    private GameObject zoneChargeAura;
    private readonly List<GameObject> zoneObjects = new List<GameObject>();
    private bool gsHeld;                      // 각성 「대검 해방」: 대검을 든 중
    private float gsHeldUntil;
    private Weapon partnerSword;              // 숨긴 보조 대검
    private GameObject gsRoot;
    private Transform gsPivot;
    private Bullet gsHit;
    private bool gsSwinging;
    private int gsStep;
    private float gsLastAttackTime;
    private float gsReach = 2.5f;              // 대검 끝까지 거리 (베기 궤적 위치)
    private SpriteRenderer gsBlade;            // 대검 그림 (오른쪽이 날)
    private BoxCollider2D gsBox;
    private float gsBoxOffsetX;
    private GameObject guardAura;             // 랜스 방어 자세 오라
    private GameObject gsChargeAura;          // 대검 기 모으기 오라
    private bool usingRainFx;                 // 화살비 범위를 이펙트로 표시하는지
    private float lastHealth;

    private bool SkillPressed => Input.GetKeyDown(Settings.skillKey) || skillButtonPressed;
    private bool SkillHeld => Input.GetKey(Settings.skillKey) || (skillButton != null && skillButton.IsHeld);
    private bool SkillReady => Time.time >= skillReadyTime;
    private float AimAngle => Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

    // 메인무기 전용 데이터 (보조무기 레벨 수치와 분리, Resources/MainWeapon)
    private MainWeaponData data;

    // 메인무기 기본 피해 = 데이터 기본 피해 x 캐릭터 공격력 x 레벨업 공격력 카드 (공격력 능력치는 맞을 때 RunStats 규칙으로)
    private float MainDamage => data.baseDamage * Character.Damage * (1f + Gear.damage_stat);

    // 공격 속도 배율: 근접 무기의 회전 속도, 휘두르는 동작 속도
    // 쌍검, 대검은 무기 회전 속도 성장(공격 속도 증가 포함)을, 랜스는 공격 속도 증가 능력치를 그대로 사용
    private float AttackSpeedFactor
    {
        get
        {
            // 메인무기 데이터의 성장 x 캐릭터 공격속도(1회) x 레벨업 스태미나 카드 x 공격속도 능력치·버프·기믹
            float factor = data.attackSpeedGrowth * Character.WeaponSpeed * (1f + Gear.stamina_stat);
            return Mathf.Clamp(factor * PlayerBuff.Stamina, 0.1f, Settings.attackSpeedMax);   // 상한 2배
        }
    }

    // 전용 스킬 쿨타임 배율 (쿨타임 감소 능력치, 필살기 버프 반영)
    // R 쿨타임 = 기본 x 캐릭터 쿨타임 배율 x 공격 쿨타임 감소 x 무기 스킬 쿨타임 감소 (출처마다 곱), 하한 50%
    private static float SkillCooldownFactor => Mathf.Max(Settings.skillCooldownMin,
        Character.WeaponRate * Mathf.Max(0.05f, 1f - Gear.rate_stat) * PlayerBuff.RateScale * RunStats.Get(RunStat.SkillCooldown));   // 상점 「스킬 연마」

    // 공격 간격 배율 (쿨타임 감소, 보조무기 단계의 연사속도 성장, 필살기 버프 반영)
    private float CooldownFactor
    {
        get
        {
            // 메인무기 데이터의 성장 x 캐릭터 쿨타임 배율(1회) x 레벨업 쿨감 카드 x 쿨타임 감소 능력치·버프
            float factor = data.cooldownGrowth * Character.WeaponRate * (1f - Gear.rate_stat);
            return Mathf.Max(Settings.attackCooldownMin, Mathf.Max(0.05f, factor) * PlayerBuff.RateScale);   // 하한 40%
        }
    }

    // 최초로 메인무기 레벨에 도달한 무기를 메인무기로 지정 (이미 있으면 무시)
    public static bool TryDesignate(Weapon weapon, int level)
    {
        if (Current != null || weapon == null || weapon.IsMain || level < Settings.mainWeaponLevel)
            return false;

        weapon.gameObject.AddComponent<MainWeaponController>().Setup(weapon);
        return true;
    }

    private void Setup(Weapon target)
    {
        Current = this;
        weapon = target;
        data = MainWeaponData.Load(target.id);
        if (data == null)
        {
            Debug.LogError("[메인무기] Resources/MainWeapon/MainWeapon_" + target.id + " 데이터가 없습니다 (MonNyang > 메인무기 데이터 만들기)");
            data = ScriptableObject.CreateInstance<MainWeaponData>();
        }
        player = GameManager.instance.player;
        playerSprite = player.GetComponent<SpriteRenderer>();
        hitLayer = GameManager.instance.pool.prefabs[weapon.prefabId].layer;

        circleSprite = UiCircleSprite.Create();
        Texture2D white = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0f, 0.5f), white.width);

        weapon.PrepareMainWeapon();

        switch (weapon.id)
        {
            case 0: SetupDualBlades(); break;
            case 1: SetupBow(); break;
            case 2: SetupHeavyBowgun(); break;
            case 3: SetupGreatSword(); break;
            case 4: SetupLance(); break;
        }

        if (weapon.id == 0 || weapon.id == 1 || weapon.id == 4)
        {
            overhead = OverheadChargeUI.Create(player.transform, playerSprite);   // 우클릭 충전·쿨타임 (머리 위)
        }
        CreateGauge();
        skillButton = MainWeaponSkillButton.Create();
        lastHealth = GameManager.instance.health;
        MainWeaponGuide.Show(GuideTitle(), GuideBody(), Settings);
        WeaponSynergy.Evaluate();                            // 짝 무기가 이미 조건을 넘었으면 바로 판정
    }

    // 시너지 각성 효과 켜기 (WeaponSynergy.OnAwakening에서 호출)
    public void EnableAwakening()
    {
        awakening = true;
    }

    // 시너지 공명 효과 켜기 (WeaponSynergy.OnResonance에서 호출)
    public void EnableResonance()
    {
        resonance = true;
    }

    private Sprite SkillIcon()
    {
        MainWeaponSettings s = Settings;
        Sprite icon = null;
        switch (weapon.id)
        {
            case 0: icon = s.dualBlades.skillIcon; break;
            case 1: icon = s.bow.skillIcon; break;
            case 2: icon = s.heavyBowgun.skillIcon; break;
            case 3: icon = s.greatSword.skillIcon; break;
            case 4: icon = s.lance.skillIcon; break;
        }
        Sprite maxIcon = weapon.Data.maxLevelIcon;

        // 따로 지정한 아이콘이 있으면 사용 (단, 기본 무기 아이콘을 넣어 둔 경우는 최대 레벨 아이콘으로 대체)
        if (icon != null && (icon != weapon.Data.itemIcon || maxIcon == null))
            return icon;

        // 없으면 무기 최대 레벨 아이콘(예: DualBlades_Icon_2), 그것도 없으면 무기 아이콘
        return maxIcon != null ? maxIcon : weapon.Data.itemIcon;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
            MoveSpeedScale = 1f;
            DamageTakenScale = 1f;
        }

        if (player != null)
        {
            player.SetWeaponRooted(false);
        }

        if (heldRenderer != null) Destroy(heldRenderer.gameObject);
        if (gaugeRoot != null) Destroy(gaugeRoot.gameObject);
        if (chargeVisual != null) Destroy(chargeVisual.gameObject);
        if (rainIndicator != null) Destroy(rainIndicator.gameObject);
        if (stackHud != null) Destroy(stackHud.gameObject);
        if (zoneIndicator != null) Destroy(zoneIndicator.gameObject);
        foreach (GameObject z in zoneObjects)
        {
            if (z != null) Destroy(z);
        }
        if (gsRoot != null) Destroy(gsRoot);
        if (partnerSword != null && gameObject.scene.isLoaded) partnerSword.gameObject.SetActive(true);   // 숨긴 보조 대검 복귀
        if (overhead != null) Destroy(overhead.gameObject);
        if (slamRange != null) Destroy(slamRange.gameObject);
        if (justGlowMaterial != null) Destroy(justGlowMaterial);
        StopGuardBlades();
        SpriteFx.StopAura(zoneChargeAura);
        SpriteFx.StopAura(guardAura);
        SpriteFx.StopAura(gsChargeAura);
        if (skillButton != null) Destroy(skillButton.gameObject);
        if (ammoHud != null) Destroy(ammoHud.gameObject);
        if (scopeUI != null) scopeUI.Close();
        if (scoping) { HeavyStealth.End(); SpriteFx.Stop(stealthLeaves); }

        UiCircleSprite.Release(circleSprite);
        if (whiteSprite != null) Destroy(whiteSprite);
        if (pillSprite != null)
        {
            Destroy(pillSprite.texture);
            Destroy(pillSprite);
        }
    }

    private void Update()
    {
        if (!GameManager.instance.isLive)
        {
            mouseHoldValid = false;
            skillButton.ConsumePress();              // 멈춘 동안 누른 입력은 버림 (버튼은 계속 표시)
            UpdateSkillButton();
            return;
        }

        ReadInput();

        switch (weapon.id)
        {
            case 0: UpdateDualBlades(); break;
            case 1: UpdateBow(); break;
            case 2: UpdateHeavyBowgun(); break;
            case 3: UpdateGreatSword(); break;
            case 4: UpdateLance(); break;
        }

        UpdateHeldSprite();
        UpdateGauge();
        UpdateSkillButton();
        lastHealth = GameManager.instance.health;
    }

    private void UpdateSkillButton()
    {
        bool active = scoping || zoneTargeting || zoneCharging || rainTargeting || gsCharging;
        float remain = skillReadyTime - Time.time;
        float fill = !SkillReady && skillCooldownLength > 0f ? remain / skillCooldownLength : 0f;

        // 버튼 안: 헤비보우건 저격 중에는 남은 저격 수, 그 외에는 쿨타임 남은 초 (사용 가능하면 비움)
        string inner;
        if (weapon.id == 2 && scoping)
        {
            inner = scopeShotsLeft.ToString();
        }
        else if (weapon.id == 0 && awakening && dashCharges >= 0)
        {
            inner = dashCharges.ToString();                  // 쌍검 각성: 남은 돌진 충전 수
            fill = dashCharges < MaxDashCharges && !SkillReady && skillCooldownLength > 0f ? remain / skillCooldownLength : 0f;
        }
        else if (active)
        {
            inner = "ON";
        }
        else
        {
            inner = SkillReady ? "" : Mathf.CeilToInt(remain).ToString();
        }

        // 버튼 아래: 사용 키
        skillButton.SetState(true, fill, active, inner, "[" + Settings.skillKey + "]", SkillIcon(), Settings);
        skillButton.SetSynergy(WeaponSynergy.Current, WeaponSynergy.Data);
    }

    // ---------------------------------------------------------------
    // 공통
    // ---------------------------------------------------------------
    private void ReadInput()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            Vector3 diff = mouseWorld - transform.position;
            diff.z = 0f;
            if (diff.sqrMagnitude > 0.0001f)
            {
                aimDir = diff.normalized;
            }
        }

        // 필살기, 회피 버튼 등 UI를 누른 클릭은 공격으로 치지 않음
        bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || TestMode.IsPointerOverPanel;

        mouseClicked = Input.GetMouseButtonDown(0) && !overUI;
        if (Input.GetMouseButtonDown(0))
        {
            mouseHoldValid = mouseClicked;
        }
        if (!Input.GetMouseButton(0))
        {
            mouseHoldValid = false;
        }

        rightClicked = Input.GetMouseButtonDown(1) && !overUI;
        skillButtonPressed = skillButton.ConsumePress();
    }

    private void BeginAttack(IEnumerator routine)
    {
        isAttacking = true;
        attackRoutine = StartCoroutine(RunAttack(routine));
    }

    private IEnumerator RunAttack(IEnumerator routine)
    {
        yield return routine;
        isAttacking = false;
        attackRoutine = null;
    }

    private void StopAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }
        attackRoutine = null;
        isAttacking = false;
    }

    private void StartSkillCooldown(float length)
    {
        length = TestMode.NoSkillCooldown ? 0f : length * SkillCooldownFactor;   // 쿨타임 감소 능력치가 전용 스킬에도 적용
        skillCooldownLength = length;
        skillReadyTime = Time.time + length;
    }

    private static void SetHit(Bullet bullet, float damage, bool harmful)
    {
        bullet.damage = damage;
        bullet.IsHarmless = !harmful;
    }

    // 이미 겹쳐 있는 적도 이번 공격에 다시 맞도록 충돌을 새로 시작
    // (관통 값을 -100으로 둔 공격만 사용. 콜라이더를 꺼도 Area 이탈로 꺼지지 않음)
    private void Rehit(Bullet bullet)
    {
        foreach (Collider2D col in bullet.GetComponents<Collider2D>())
        {
            StartCoroutine(RehitRoutine(col));
        }
    }

    // 같은 프레임에 껐다 켜면 물리 엔진이 새 충돌로 보지 않으므로, 물리 업데이트 한 번을 꺼진 채로 보낸 뒤 다시 켬
    private static IEnumerator RehitRoutine(Collider2D col)
    {
        col.enabled = false;
        yield return new WaitForFixedUpdate();
        if (col != null)
        {
            col.enabled = true;
        }
    }

    private static Vector3 AngleToDir(float degree)
    {
        float rad = degree * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
    }

    // 스프라이트의 위쪽이 해당 각도를 향하도록 하는 회전
    private static Quaternion UpTowards(float degree)
    {
        return Quaternion.Euler(0f, 0f, degree - 90f);
    }

    private SpriteRenderer CreateVisual(string objectName, Sprite sprite, Transform parent, int sortingOffset)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = playerSprite.sharedMaterial;
        renderer.sortingLayerID = playerSprite.sortingLayerID;
        renderer.sortingOrder = playerSprite.sortingOrder + sortingOffset;
        return renderer;
    }

    private static void SetWorldDiameter(SpriteRenderer renderer, float diameter)
    {
        float size = renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
        renderer.transform.localScale = Vector3.one * (diameter / Mathf.Max(0.0001f, size));
    }

    // 기존 피격 처리(Enemy, BossPattern)를 그대로 쓰는 원형 공격 판정
    private Bullet CreateHitArea(string objectName, Vector3 position, Transform parent, float radius, float damage)
    {
        GameObject go = new GameObject(objectName);
        go.tag = "Bullet";
        go.layer = hitLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        Bullet bullet = go.AddComponent<Bullet>();
        bullet.Init(damage, -100, Vector3.zero);
        return bullet;
    }

    private IEnumerator FlashCircle(Vector3 position, float diameter, Color color, float duration)
    {
        SpriteRenderer flash = CreateVisual("Flash", circleSprite, null, 4);
        flash.transform.position = position;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            SetWorldDiameter(flash, Mathf.Lerp(diameter * 0.3f, diameter, p));
            flash.color = new Color(color.r, color.g, color.b, color.a * (1f - p));
            yield return null;
        }

        Destroy(flash.gameObject);
    }

    private void UpdateHeldSprite()
    {
        if (heldRenderer == null)
            return;

        HeldWeaponSettings s = HeldSettings;
        // 왼쪽을 볼 때는 오른쪽 모습을 좌우 대칭한 모양이 되도록 각도 보정을 반대로 적용하고 위아래를 뒤집음
        // (보정 각도를 그대로 더한 채 뒤집으면 왼쪽에서 엉뚱한 방향으로 돌아가 보임)
        bool aimLeft = aimDir.x < 0f;
        heldRenderer.transform.localPosition = aimDir * s.distance + new Vector3(0f, s.offsetY, 0f);
        heldRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, AimAngle + (aimLeft ? -s.angleOffset : s.angleOffset));
        heldRenderer.transform.localScale = Vector3.one * s.scale;
        heldRenderer.flipY = aimLeft;
        heldRenderer.sortingOrder = playerSprite.sortingOrder + s.sortingOffset;
        if (s.sprite != null)
        {
            heldRenderer.sprite = s.sprite;                  // 게임 중 바꿔도 바로 반영
        }
    }

    private HeldWeaponSettings HeldSettings => weapon.id == 1 ? Settings.bowHeld : Settings.heavyBowgunHeld;

    private void CreateHeldSprite(string objectName)
    {
        HeldWeaponSettings s = HeldSettings;
        Sprite sprite = s.sprite != null ? s.sprite : weapon.Data.itemIcon;
        heldRenderer = CreateVisual(objectName, sprite, player.transform, s.sortingOffset);
        gameObject.AddComponent<AimLaser>();                 // 조준 방향 레이저
    }

    // 둥근 끝 막대 스프라이트 (9-slice, 왼쪽 가운데 기준) — 게이지용
    private Sprite pillSprite;
    private SpriteRenderer gaugeOutline;
    // 헤비보우건 탄창 게이지 (네모 칸)
    private Transform magRoot;
    private SpriteRenderer magFrame, magBack;
    private readonly List<SpriteRenderer> magSlots = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> magFills = new List<SpriteRenderer>();
    private SpriteRenderer gaugeShine;
    private float gaugeDisplayFill;
    private float readyFlash;
    private bool wasSkillReady = true;

    private static Sprite CreatePill(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - (x + 0.5f), 0f, (x + 0.5f) - (size - radius));
                float dy = Mathf.Max(radius - (y + 0.5f), 0f, (y + 0.5f) - (size - radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    // 게이지 높이: 체력바(화면 UI) 바로 아래. 체력바는 화면 픽셀 기준이라 카메라 확대에 따라 월드 거리가 달라짐 → 매번 계산
    private RectTransform hpBarRect;
    private float GaugeLocalY(MainWeaponSettings s)
    {
        if (!s.gaugeBelowHpBar)
            return s.gaugeOffsetY;
        if (hpBarRect == null)
        {
            foreach (UnityEngine.UI.Slider slider in FindObjectsOfType<UnityEngine.UI.Slider>())
                if (slider.name == "Health Slider") { hpBarRect = (RectTransform)slider.transform; break; }
        }
        Camera cam = Camera.main;
        if (hpBarRect == null || cam == null || !hpBarRect.gameObject.activeInHierarchy)
            return s.gaugeOffsetY;
        float half = Mathf.Max(0.06f, s.gaugeSize.y) * 0.5f + 0.03f;
        Canvas hpCanvas = hpBarRect.GetComponentInParent<Canvas>();
        if (hpCanvas != null && hpCanvas.renderMode == RenderMode.WorldSpace)
        {
            // 체력바가 플레이어 자식 월드 캔버스(Follow)에 있음 → 월드 아래 끝 그대로
            Vector3[] c = new Vector3[4];
            hpBarRect.GetWorldCorners(c);
            return c[0].y - player.transform.position.y - s.gaugeHpGap - half;
        }
        // (화면 캔버스일 때) 체력바 부모는 플레이어 화면 위치에 붙으므로, 체력바 아래 끝 = 플레이어 화면 위치 + 부모 안 아래 끝
        Vector3 ps = cam.WorldToScreenPoint(player.transform.position);
        float localBottom = hpBarRect.localPosition.y + hpBarRect.rect.yMin;
        float scaleY = hpBarRect.parent != null ? hpBarRect.parent.lossyScale.y : 1f;
        Vector3 bottom = cam.ScreenToWorldPoint(new Vector3(ps.x, ps.y + localBottom * scaleY, -cam.transform.position.z));
        return bottom.y - player.transform.position.y - s.gaugeHpGap - half;
    }

    private SpriteRenderer CreateBar(string name, int sortingOffset)
    {
        SpriteRenderer bar = CreateVisual(name, pillSprite, gaugeRoot, sortingOffset);
        bar.drawMode = SpriteDrawMode.Sliced;                  // 길이를 바꿔도 둥근 끝이 찌그러지지 않음
        return bar;
    }

    private void CreateGauge()
    {
        pillSprite = CreatePill(16, 6);
        gaugeRoot = new GameObject("Main Weapon Gauge").transform;
        gaugeRoot.SetParent(player.transform, false);
        gaugeOutline = CreateBar("Outline", 7);
        gaugeBack = CreateBar("Back", 8);
        gaugeFill = CreateBar("Fill", 9);
        gaugeShine = CreateBar("Shine", 10);
        gaugeRoot.gameObject.SetActive(false);
    }

    // 헤비보우건 탄창 게이지: 진한 테두리 안에 칸이 나란히, 탄이 있는 칸은 탄 색으로 (차는 중인 칸은 왼쪽부터 일부만)
    private void DrawMagazine(MainWeaponSettings s, int cells, float filled, Color color, bool dimPartial)
    {
        HeavyBowgunMainSettings h = s.heavyBowgun;
        if (magRoot == null)
        {
            magRoot = new GameObject("Magazine").transform;
            magRoot.SetParent(gaugeRoot, false);
            magFrame = CreateVisual("Frame", whiteSprite, magRoot, 7);
            magBack = CreateVisual("Back", whiteSprite, magRoot, 8);
        }
        magRoot.gameObject.SetActive(true);
        while (magSlots.Count < cells)
        {
            magSlots.Add(CreateVisual("Slot", whiteSprite, magRoot, 9));
            magFills.Add(CreateVisual("Round", whiteSprite, magRoot, 10));
        }

        float width = s.gaugeSize.x;
        float height = h.magazineHeight;
        float gap = h.magazineGap;
        float border = gap * 1.2f;
        float left = -width * 0.5f;
        // whiteSprite: 왼쪽 가운데 기준점, 1 x 1 월드
        magFrame.transform.localPosition = new Vector3(left - border, 0f, 0f);
        magFrame.transform.localScale = new Vector3(width + border * 2f, height + border * 2f, 1f);
        magFrame.color = h.magazineFrameColor;
        magBack.transform.localPosition = new Vector3(left, 0f, 0f);
        magBack.transform.localScale = new Vector3(width, height, 1f);
        magBack.color = h.magazineFrameColor * 0.75f + new Color(0f, 0f, 0f, 0.25f);

        float cellW = (width - gap * (cells + 1)) / cells;
        float cellH = height - gap * 2f;
        Color round = Color.Lerp(color, Color.white, readyFlash);
        for (int i = 0; i < magSlots.Count; i++)
        {
            bool used = i < cells;
            magSlots[i].enabled = used;
            magFills[i].enabled = false;
            if (!used) continue;
            float x = left + gap + i * (cellW + gap);
            magSlots[i].transform.localPosition = new Vector3(x, 0f, 0f);
            magSlots[i].transform.localScale = new Vector3(cellW, cellH, 1f);
            magSlots[i].color = h.magazineEmptyColor;
            float k = Mathf.Clamp01(filled - i);                  // 이 칸에 찬 정도
            if (k <= 0.001f) continue;
            magFills[i].enabled = true;
            magFills[i].transform.localPosition = new Vector3(x, 0f, 0f);
            magFills[i].transform.localScale = new Vector3(cellW * k, cellH, 1f);
            magFills[i].color = k >= 0.999f || !dimPartial ? round : new Color(round.r, round.g, round.b, 0.55f);   // 잔탄: 차는 중인 칸은 흐리게
        }
    }

    // 헤비보우건은 탄 게이지, 랜스 돌진 모드는 남은 시간, 그 외에는 스킬 쿨타임 표시
    // 둥근 테두리 막대 + 윗부분 광택, 값은 부드럽게 변하고 쿨타임이 끝나면 하얗게 번쩍인 뒤 사라짐
    private void UpdateGauge()
    {
        MainWeaponSettings s = Settings;
        bool show = true;
        float fill = 0f;
        Color color = s.cooldownColor;
        int magCells = 0;                                      // 0이 아니면 탄창 모양 (헤비보우건 잔탄)
        float magFilled = 0f;

        // 쿨타임이 막 끝난 순간 잠깐 번쩍임
        if (SkillReady && !wasSkillReady)
        {
            readyFlash = 1f;
        }
        wasSkillReady = SkillReady;
        readyFlash = Mathf.MoveTowards(readyFlash, 0f, Time.deltaTime / 0.35f);

        if (weapon.id == 2 && Switching)
        {
            fill = Mathf.Clamp01((Time.time - switchStartTime) / Mathf.Max(0.01f, switchEndTime - switchStartTime));   // 탄 변경 진행
            color = s.activeColor;
        }
        else if (weapon.id == 2 && !scoping && SkillReady)
        {
            HeavyBowgunMainSettings h = s.heavyBowgun;
            fill = rapidEquipped ? rapidAmmo / h.rapidMax : heavyAmmo / h.heavyMax;   // 장착 탄 남은 양
            color = h.AmmoColor(fill);                         // 가득 = 초록, 줄어들수록 빨강
            magCells = rapidEquipped ? Mathf.Max(1, h.rapidSegments) : h.heavyMax;   // 중량탄은 한 칸 = 한 발
            magFilled = Mathf.Clamp01(fill) * magCells;
        }
        else if (weapon.id == 4 && zoneCharging)
        {
            fill = 1f - (zoneChargeEnd - Time.time) / Mathf.Max(0.01f, s.lance.chargeTime);   // 강철의 영역 기 모으기 진행
            color = s.activeColor;
        }
        else if (!SkillReady && skillCooldownLength > 0f)
        {
            fill = 1f - (skillReadyTime - Time.time) / skillCooldownLength;
        }
        else if (readyFlash > 0f)
        {
            fill = 1f;                                         // 준비 완료 번쩍임
        }
        else
        {
            show = false;
        }

        gaugeRoot.gameObject.SetActive(show);
        if (!show)
        {
            gaugeDisplayFill = 0f;
            return;
        }

        // 헤비보우건: 네모난 탄창 모양으로 통일 (잔탄은 칸마다 탄, 탄 변경·쿨타임은 한 칸짜리 막대)
        bool magazine = weapon.id == 2;
        gaugeOutline.enabled = gaugeBack.enabled = !magazine;
        if (magazine)
        {
            gaugeFill.enabled = gaugeShine.enabled = false;
            gaugeRoot.localPosition = new Vector3(0f, GaugeLocalY(s), 0f);
            if (magCells > 0)
            {
                DrawMagazine(s, magCells, magFilled, color, true);
                gaugeDisplayFill = Mathf.Clamp01(fill);
            }
            else
            {
                float target = Mathf.Clamp01(fill);
                gaugeDisplayFill = target < gaugeDisplayFill ? target : Mathf.MoveTowards(gaugeDisplayFill, target, Time.deltaTime * 4f);
                DrawMagazine(s, 1, gaugeDisplayFill, color, false);
            }
            return;
        }
        if (magRoot != null) magRoot.gameObject.SetActive(false);

        fill = Mathf.Clamp01(fill);
        gaugeDisplayFill = Mathf.MoveTowards(gaugeDisplayFill, fill, Time.deltaTime * 4f);
        if (fill < gaugeDisplayFill)
        {
            gaugeDisplayFill = fill;                           // 줄어들 때는 바로 반영 (탄 사용 등)
        }

        float width = s.gaugeSize.x;
        float height = Mathf.Max(0.06f, s.gaugeSize.y);
        float border = 0.025f;
        float left = -width * 0.5f;
        gaugeRoot.localPosition = new Vector3(0f, GaugeLocalY(s), 0f);

        gaugeOutline.transform.localPosition = new Vector3(left - border, 0f, 0f);
        gaugeOutline.size = new Vector2(width + border * 2f, height + border * 2f);
        gaugeOutline.color = new Color(0.08f, 0.06f, 0.05f, 0.85f);
        RunConfig rc = RunConfig.Instance;
        bool barAsset = false;                                // 바 그림을 늘리면 깨져 보여서 둥근 막대 그대로, 색만 체력바에 맞춤
        if (barAsset)
        {
            gaugeOutline.enabled = false;
            gaugeBack.sprite = rc.buttonBrown;
            gaugeFill.sprite = rc.buttonYellow;
        }

        gaugeBack.transform.localPosition = new Vector3(barAsset ? left + width * 0.5f : left, 0f, 0f);   // 바 에셋은 가운데 기준점
        gaugeBack.size = new Vector2(width, height);
        gaugeBack.color = new Color(0.33f, 0.33f, 0.33f, 1f);   // 체력바 바탕과 같은 색
        gaugeOutline.color = new Color(0.2f, 0.13f, 0.09f, 1f);

        float fillWidth = width * gaugeDisplayFill;
        bool hasFill = fillWidth > 0.005f;
        gaugeFill.enabled = hasFill;
        gaugeShine.enabled = false;                              // 광택 없이 체력바처럼 단색
        if (hasFill)
        {
            if (!barAsset) fillWidth = Mathf.Max(fillWidth, height);          // 아주 짧아도 둥근 모양 유지
            gaugeFill.transform.localPosition = new Vector3(barAsset ? left + fillWidth * 0.5f : left, 0f, 0f);
            gaugeFill.size = new Vector2(fillWidth, height);
            gaugeFill.color = barAsset ? Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1.6f, 1.6f, 1.6f, 1f), readyFlash) : Color.Lerp(color, Color.white, readyFlash);

            // 윗부분 광택
            gaugeShine.transform.localPosition = new Vector3(left + height * 0.25f, height * 0.18f, 0f);
            gaugeShine.size = new Vector2(Mathf.Max(0.01f, fillWidth - height * 0.5f), height * 0.32f);
            gaugeShine.color = new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private string GuideTitle()
    {
        return weapon.Data.itemName + " 메인무기 해금!";
    }

    private string GuideBody()
    {
        string key = Settings.skillKey.ToString();
        string common = "오른쪽 스킬 버튼이나 " + key + " 키로 전용 스킬 사용\n";

        switch (weapon.id)
        {
            case 0:
                return "좌클릭 : 3연속 베기\n우클릭 : 백대쉬 (최대 " + Settings.dualBlades.backDashMaxCharge + "회)\n" + key + " : 돌진 회전베기 (이동 중 무적)";
            case 1:
                return "좌클릭 : 화살 발사\n우클릭 : 부채꼴 발사 (최대 " + Settings.bow.fanMaxCharge + "회)\n" + key + " : 화살비 (좌클릭으로 위치 지정)";
            case 2:
                return "좌클릭 : 장착한 탄 발사\n우클릭 : 탄 변경 (중량탄 ↔ 연사탄)\n" + key + " : 정밀 저격 ( 은신하여 적을 저격 )";
            case 3:
                return "좌클릭 : 내려찍기 → 후리기\n우클릭 : 반짝일 때 올려치기\n" + key + "(꾹) : 모아 베기 (최대 3단계, 떼면 발동)";
            case 4:
                return "좌클릭 : 3연속 찌르기\n우클릭 : 패링 (연속 성공 시 철벽 스택)\n" + key + " : 강철의 영역 (스택만큼 거대해지는 창)";
            default:
                return common;
        }
    }

    // ---------------------------------------------------------------
    // 쌍검
    // ---------------------------------------------------------------
    private void SetupDualBlades()
    {
        PoolManager pool = GameManager.instance.pool;
        blades = new Bullet[2];

        for (int i = 0; i < 2; i++)
        {
            Transform blade;
            if (i < transform.childCount)
            {
                blade = transform.GetChild(i);
            }
            else
            {
                blade = pool.Get(weapon.prefabId).transform;
                blade.parent = transform;
            }

            blades[i] = blade.GetComponent<Bullet>();
            blades[i].Init(MainDamage, -100, Vector3.zero);
            blades[i].ApplyWeaponSize();                         // 능력치 「무기·총알 크기」 (기본 공격)
            blades[i].IsHarmless = true;

            // 판정: 칼 그림보다 조금 넓게, 캐릭터 몸 가까이까지 닿도록 (칼 중심에서 안쪽으로 늘림)
            BoxCollider2D box = blade.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                DualBladesMainSettings ds = Settings.dualBlades;
                float outer = ds.bladeDistance + 1.1f;           // 칼 끝 바깥 조금 더
                float inner = 0.2f;                              // 몸 바로 앞
                box.size = new Vector2(0.9f, outer - inner);
                box.offset = new Vector2(0f, (outer + inner) * 0.5f - ds.bladeDistance);
            }
        }

        // 메인무기 쌍검은 두 자루만 사용
        for (int i = transform.childCount - 1; i >= 2; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }

        if (weapon.Data.weaponimage != null && weapon.Data.weaponimage.Length > 0)
        {
            weapon.changesprite();
        }

        for (int i = 0; i < 2; i++)
        {
            GetRestPose(i, out Vector3 position, out Quaternion rotation);
            blades[i].transform.localPosition = position;
            blades[i].transform.localRotation = rotation;
        }
    }

    // 대기 자세: 기준 방향일 때의 날개 모양(양옆 위치 + 바깥쪽 기울기)을 한 세트로 보고,
    // 조준 방향이 기준에서 돌아간 만큼 세트 전체를 캐릭터 중심으로 함께 회전
    private void GetRestPose(int index, out Vector3 position, out Quaternion rotation)
    {
        DualBladesMainSettings s = Settings.dualBlades;
        float side = index == 0 ? -1f : 1f;                  // 0: 왼쪽, 1: 오른쪽
        float turn = Mathf.DeltaAngle(s.restReferenceAngle, AimAngle);
        position = Quaternion.Euler(0f, 0f, turn) * new Vector3(s.restOffset.x * side, s.restOffset.y, 0f);

        float angle = 90f + s.restTilt * -side + turn;       // 기준 자세의 칼 방향을 같이 회전
        if (s.restUpsideDown)
        {
            angle += 180f;
        }
        rotation = UpTowards(angle);
    }

    // 회전 공격을 시작할 때 칼의 현재 위치를 각도로 환산
    private float BladeAngle(int index)
    {
        Vector3 p = blades[index].transform.localPosition;
        return p.sqrMagnitude > 0.0001f ? Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg : bladeAngles[index];
    }

    private void PlaceBlade(int index, float angle)
    {
        bladeAngles[index] = angle;
        Transform blade = blades[index].transform;
        // 「무기·총알 크기」로 칼이 커진 만큼 바깥으로 (손잡이가 캐릭터를 가리지 않게)
        blade.localPosition = AngleToDir(angle) * Settings.dualBlades.bladeDistance * RunStats.Get(RunStat.WeaponSize);
        blade.localRotation = UpTowards(angle);
    }

    private void MoveBladeToRest(int index)
    {
        DualBladesMainSettings s = Settings.dualBlades;
        GetRestPose(index, out Vector3 position, out Quaternion rotation);

        Transform blade = blades[index].transform;
        blade.localPosition = Vector3.MoveTowards(blade.localPosition, position, s.returnMoveSpeed * Time.deltaTime);
        blade.localRotation = Quaternion.RotateTowards(blade.localRotation, rotation, s.returnSpeed * Time.deltaTime);
    }

    private void ApplyBladeMirror()
    {
        SpriteRenderer left = blades[0].GetComponentInChildren<SpriteRenderer>();
        if (left != null)
        {
            left.flipX = Settings.dualBlades.mirrorLeftBlade;
        }
    }

    private void UpdateDualBlades()
    {
        UpdateDashCharges(data.dashCooldown);
        if (SkillPressed && DashReady && !player.IsForcedMoving)
        {
            StopAttack();
            BeginAttack(DualBladeDash());
            return;
        }

        UpdateBackDash();
        ApplyBladeMirror();

        if (isAttacking)
            return;

        MoveBladeToRest(0);
        MoveBladeToRest(1);

        if (mouseHoldValid && Time.time >= attackReadyTime && !player.IsForcedMoving)
        {
            BeginAttack(DualBladeBasic());
        }
    }

    // 우클릭 백대쉬: 마우스 반대 방향으로 짧게 이동 (공격 중에도 가능, 무적 없음, 벽에서는 멈춤)
    private void UpdateBackDash()
    {
        DualBladesMainSettings s = Settings.dualBlades;
        if (backDash.Tick(s.backDashMaxCharge, s.backDashChargeTime))
        {
            if (s.backDashReadySound != null) ExtraCards.PlayClip(s.backDashReadySound);   // 충전 완료 (비우면 소리 없음)
        }
        if (overhead != null) overhead.ShowCharges(backDash.Count, s.backDashMaxCharge, backDash.Progress);

        if (rightClicked && backDash.Count > 0 && !player.IsForcedMoving)
        {
            Vector2 dir = -(Vector2)aimDir;
            if (player.ForceMove(dir, s.backDashDistance, s.backDashTime, 0f, true))   // 물리 이동이라 벽·경계에서 멈춤, 시선 유지
            {
                backDash.Use();
                GimmickCards.OnRightClickSkill();            // 반격 본능 (쌍검 백대시)
                SpriteFx.ChargeTrail(player.transform.position, dir);
                RunUI.PlayOr(s.backDashSound, AudioManager.Sfx.Weapon_Melee);
            }
        }
    }

    private IEnumerator DualBladeBasic()
    {
        DualBladesMainSettings s = Settings.dualBlades;
        float speed = AttackSpeedFactor;
        float damage = MainDamage * s.basicDamageMultiplier;
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Melee);

        if (dualStep < 2)
        {
            // 1단계 왼쪽 칼, 2단계 오른쪽 칼로 베고 원위치
            int index = dualStep;
            int other = 1 - index;
            float sign = index == 0 ? 1f : -1f;
            float aim = AimAngle;
            SpriteFx.DualSlash(transform.position + (Vector3)((Vector2)aimDir * 0.9f), aimDir, index == 1);   // 베기 궤적

            SetHit(blades[index], damage, true);
            Rehit(blades[index]);

            // 공명 「추격의 깃털」: 1·2단계 베기를 휘두를 때마다 (적중 여부와 무관) 정면으로 작은 화살 1발
            if (resonance)
            {
                FireFeather(aimDir, damage);
            }

            float duration = data.slashTime / speed;
            float t = 0f;
            float nextTrail = 0f;
            float reach = (s.bladeDistance + 1.1f) * WeaponSizeScale;   // 궤적도 커진 칼 위치에 맞춤
            while (t < duration)
            {
                t += Time.deltaTime;
                float half = s.slashArc * 0.5f;
                float k = Mathf.Clamp01(t / duration);
                float bladeAngle = aim + sign * Mathf.Lerp(half, -half, k);
                PlaceBlade(index, bladeAngle);
                MoveBladeToRest(other);

                // 칼이 지나간 자리에 바람 궤적 (실제 판정 범위만큼)
                if (t >= nextTrail)
                {
                    nextTrail += duration / 6f;
                    Vector2 radial = AngleToDir(bladeAngle);
                    Vector2 swing = AngleToDir(bladeAngle - sign * 90f);
                    SpriteFx.BladeTrail(player.transform.position + (Vector3)(radial * reach * 0.55f), swing, reach * 0.9f);
                }
                yield return null;
            }

            SetHit(blades[index], damage, false);
            dualStep++;
            attackReadyTime = Time.time + data.stepDelay / speed;   // 연계 단계 사이 (공격하는 중) = 공격속도
        }
        else
        {
            // 3단계 두 칼이 캐릭터 주위를 회전
            for (int i = 0; i < 2; i++)
            {
                SetHit(blades[i], damage, true);
                Rehit(blades[i]);
            }

            float angle = BladeAngle(0);
            float elapsed = 0f;
            float nextWhirl = 0f;
            float nextFeather = 0f;
            while (elapsed < data.spinDuration / speed)                // 3단 회전 동작 시간 = 공격속도
            {
                // 공명 「추격의 깃털」: 회전 중 일정 간격으로 무작위 방향 작은 화살
                if (resonance && elapsed >= nextFeather)
                {
                    nextFeather += WeaponSynergy.Data.arrowInterval;
                    FireFeather(UnityEngine.Random.insideUnitCircle.normalized, damage);
                }
                if (elapsed >= nextWhirl)
                {
                    SpriteFx.DualWhirl(player.transform, (s.bladeDistance + 1.1f) * WeaponSizeScale * 2f);   // 회전 판정 범위 크기의 소용돌이 (캐릭터 뒤, 크기 반영)
                    nextWhirl += 0.12f;
                }
                elapsed += Time.deltaTime;
                angle -= data.spinSpeed * speed * Time.deltaTime;
                PlaceBlade(0, angle);
                PlaceBlade(1, angle + 180f);
                yield return null;
            }

            for (int i = 0; i < 2; i++)
            {
                SetHit(blades[i], damage, false);
            }

            dualStep = 0;
            attackReadyTime = Time.time + data.comboCooldown * CooldownFactor;
        }
    }

    private IEnumerator DualBladeDash()
    {
        DualBladesMainSettings s = Settings.dualBlades;
        for (int i = 0; i < 2; i++)
        {
            blades[i].IsHarmless = true;
        }

        // 각성 「섬광 돌격」: 거리, 속도, 피해 강화 (무적은 기존처럼 돌진 시간 동안만)
        WeaponSynergyData sd = WeaponSynergy.Data;
        float dashDistance = awakening ? s.dashDistance * sd.dashRangeMul : s.dashDistance;
        float dashTime = awakening ? s.dashTime * sd.dashRangeMul / Mathf.Max(0.1f, sd.dashSpeedMul) : s.dashTime;
        float dashDamage = MainDamage * s.dashDamageMultiplier * ExtraCards.WeaponSkillScale * (awakening ? sd.dashDamageMul : 1f);
        Vector3 dashStart = player.transform.position;

        // 무적: 돌진 중 + 끝난 뒤 dashPostInvincible초 (각성 여부와 무관)
        if (!player.ForceMove(aimDir, dashDistance, dashTime, dashTime + sd.dashPostInvincible))
            yield break;

        UseDashCharge(data.dashCooldown);                       // 각성이면 충전 1회 소모 (최대 2회 연속)
        CameraShake.Weak();
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Melee);
        SpriteFx.DualDash(transform.position, aimDir);           // 돌진 베기 궤적 + 흙먼지
        if (awakening)
        {
            SynergyAwakening.PlaySound(sd.flashDashSound, AudioManager.Sfx.Weapon_ChargeMod);
            SynergyAwakening.ScreenDim(0.3f, 0.08f, 0.4f);                         // 화면이 살짝 어두워졌다가 밝아짐
            SpriteFx.LightningStrikes(sd.lightningCount);                          // 화면 안 번개 내리꽂힘 (effect_2)
            SynergyAwakening.ScreenFlash(sd.lightningFlashTime);                    // 화면 흰 번쩍임
            CameraShake.Strong();
        }

        // 칼이 「무기·총알 크기」만큼 바깥에서 도므로 회전 판정도 같은 비율로 (보이는 칼 위치와 일치)
        Bullet hit = CreateHitArea("Dash Spin Hit", transform.position, transform, s.dashHitRadius * WeaponSizeScale, dashDamage);   // 무기 숙련
        hit.KnockChance = GimmickCatalog.Instance.knockDualSkill;

        float angle = BladeAngle(0);
        float elapsed = 0f;
        float nextImage = 0f;
        while (player.IsForcedMoving && elapsed < dashTime + 0.5f)
        {
            if (awakening && elapsed >= nextImage)
            {
                nextImage += 0.025f;
                SynergyAwakening.Afterimage(playerSprite);   // 돌진 경로 잔상
            }
            elapsed += Time.deltaTime;
            angle -= s.dashSpinSpeed * Time.deltaTime;
            PlaceBlade(0, angle);
            PlaceBlade(1, angle + 180f);
            yield return null;
        }

        Destroy(hit.gameObject);

        if (awakening)
        {
            StartCoroutine(FlashDashExplode(dashStart, player.transform.position));   // 경로 직선이 잠시 뒤 폭발
        }
    }

    // ---------------------------------------------------------------
    // 활
    // ---------------------------------------------------------------
    private void SetupBow()
    {
        CreateHeldSprite("Held Bow");

        SpriteRenderer prefabRenderer = GameManager.instance.pool.prefabs[weapon.prefabId].GetComponentInChildren<SpriteRenderer>(true);
        arrowSprite = prefabRenderer != null ? prefabRenderer.sprite : null;

        // 몬스터 프리팹의 정렬 레이어를 찾아 둠 (못 찾으면 플레이어 레이어)
        enemySortingLayer = playerSprite.sortingLayerID;
        foreach (GameObject prefab in GameManager.instance.pool.prefabs)
        {
            if (prefab != null && prefab.GetComponent<Enemy>() != null)
            {
                SpriteRenderer enemyRenderer = prefab.GetComponent<SpriteRenderer>();
                if (enemyRenderer != null)
                {
                    enemySortingLayer = enemyRenderer.sortingLayerID;
                }
                break;
            }
        }

        Sprite indicatorSprite = SpriteFx.RainIndicatorSprite;              // 원 대신 조준경
        rainIndicator = CreateVisual("Arrow Rain Indicator", indicatorSprite != null ? indicatorSprite : circleSprite, null, 5);
        usingRainFx = indicatorSprite != null;
        rainIndicator.gameObject.SetActive(false);
    }

    private void UpdateBow()
    {
        BowMainSettings s = Settings.bow;
        if (fanCharge.Tick(s.fanMaxCharge, data.fanChargeTime * CooldownFactor))   // 부채꼴 충전 = 쿨타임 감소
        {
            if (s.fanReadySound != null) ExtraCards.PlayClip(s.fanReadySound);   // 충전 완료 (비우면 소리 없음)
        }
        if (overhead != null) overhead.ShowCharges(fanCharge.Count, s.fanMaxCharge, fanCharge.Progress);

        if (rainTargeting)
        {
            rainIndicator.transform.position = mouseWorld;
            SetWorldDiameter(rainIndicator, s.rainRadius * 2f);
            rainIndicator.color = usingRainFx ? new Color(1f, 1f, 1f, 0.7f + 0.2f * Mathf.Sin(Time.time * 8f)) : s.indicatorColor;
            rainIndicator.sortingLayerID = enemySortingLayer;
            rainIndicator.sortingOrder = s.rainAreaSortingOrder;

            if (mouseClicked)
            {
                EndRainTargeting();
                mouseHoldValid = false;              // 위치 지정 클릭으로 기본 공격이 나가지 않도록
                StartSkillCooldown(data.rainCooldown);
                CameraShake.Weak();
                StartCoroutine(ArrowRain(mouseWorld));
            }
            else if (rightClicked || SkillPressed)
            {
                EndRainTargeting();                  // 취소 (쿨타임 없음)
            }
            return;
        }

        if (SkillPressed && SkillReady)
        {
            rainTargeting = true;
            rainIndicator.gameObject.SetActive(true);
            return;
        }

        // 우클릭 부채꼴: 자동 공격 중에도 끼워 발사, 발사 후 자동 공격은 좌클릭을 다시 눌러야 재개
        if (rightClicked && fanCharge.Count > 0 && Time.time >= fanReadyTime)
        {
            FireFan();
            return;
        }

        if (mouseHoldValid && Time.time >= attackReadyTime)
        {
            BowBasic();
        }
    }

    private void FireFan()
    {
        GimmickCards.OnRightClickSkill();                    // 반격 본능 (활 부채꼴)
        BowMainSettings s = Settings.bow;
        float damage = MainDamage * s.basicDamageMultiplier;
        int n = Mathf.Max(1, s.fanCount);
        for (int k = 0; k < n; k++)
        {
            float offset = n == 1 ? 0f : Mathf.Lerp(-s.fanAngle * 0.5f, s.fanAngle * 0.5f, k / (float)(n - 1));
            FireArrow(Quaternion.Euler(0f, 0f, offset) * aimDir, damage, GimmickCatalog.Instance.knockBowRight);
        }
        SpriteFx.ArrowShot(transform.position, aimDir, true);   // 부채꼴 발사 궤적
        fanCharge.Use();
        fanReadyTime = Time.time + s.fanDelay;
        mouseHoldValid = false;                              // 자동 공격 중지
        attackReadyTime = Time.time + data.fireInterval / AttackSpeedFactor;   // 다음 화살까지 = 공격속도
        RunUI.PlayOr(s.fanSound, AudioManager.Sfx.Weapon_Range);
    }

    private void EndRainTargeting()
    {
        rainTargeting = false;
        rainIndicator.gameObject.SetActive(false);
    }

    private void BowBasic()
    {
        BowMainSettings s = Settings.bow;
        float damage = MainDamage * s.basicDamageMultiplier;

        FireArrow(aimDir, damage);                           // 단발 (부채꼴은 우클릭)
        SpriteFx.ArrowShot(transform.position, aimDir, false);
        attackReadyTime = Time.time + data.fireInterval / AttackSpeedFactor;   // 다음 화살까지 = 공격속도 (쿨감 영향 없음)
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Range);
    }

    private void FireArrow(Vector3 dir, float damage, float knockChance = -1f)
    {
        Transform arrow = GameManager.instance.pool.Get(weapon.prefabId).transform;
        arrow.position = transform.position;
        arrow.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        int pierce = resonance ? UnlimitedPierce : data.pierce + ExtraCards.PierceBonus;   // 공명 「꿰뚫는 화살」: 관통 무제한
        arrow.GetComponent<Bullet>().Init(damage, pierce, dir);   // 관통력 = 무기 관통력 + 관통 화살촉
        arrow.GetComponent<Bullet>().KnockChance = knockChance;     // 음수면 활 좌클릭 확률
        arrow.GetComponent<Bullet>().ApplyWeaponSize();         // 능력치 「무기·총알 크기」 (기본 공격)
    }

    private IEnumerator ArrowRain(Vector3 center)
    {
        BowMainSettings s = Settings.bow;

        SpriteRenderer area = CreateVisual("Arrow Rain", circleSprite, null, 4);
        area.transform.position = center;
        area.color = s.rainAreaColor;
        area.sortingLayerID = enemySortingLayer;             // 몬스터와 같은 정렬 레이어에서
        area.sortingOrder = s.rainAreaSortingOrder;          // 몬스터보다 아래에 그림
        SetWorldDiameter(area, s.rainRadius * 2f);

        // '피융' (하늘로 쏘아 올림) → rainDelay초 뒤 그 자리에 쏟아짐. 기다리는 동안 범위 원은 옅게 미리 표시
        if (s.rainLaunchSound != null) ExtraCards.PlayClip(s.rainLaunchSound);
        else AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Range);
        area.enabled = true;
        Color areaColor = s.rainAreaColor;
        float waitEnd = Time.time + s.rainDelay;
        while (Time.time < waitEnd)
        {
            float k = 1f - (waitEnd - Time.time) / Mathf.Max(0.01f, s.rainDelay);
            area.color = new Color(areaColor.r, areaColor.g, areaColor.b, areaColor.a * Mathf.Lerp(0.5f, 0.8f, k));
            yield return null;
        }
        area.color = areaColor;

        // 실제 범위: 옅은 원 바탕 + 가장자리에서 흙먼지 구름이 계속 피어오름 (화살과 흙먼지는 이 안에서만)
        for (int i = 0; i < 6; i++) SpriteFx.RainEdgeDust(center, s.rainRadius);
        float nextDust = Time.time + s.rainDustInterval;


        float endTime = Time.time + s.rainDuration;
        float nextTick = Time.time;
        float arrowBudget = 0f;

        while (Time.time < endTime)
        {
            if (Time.time >= nextTick)
            {
                // 틱마다 범위 안에 있는 모든 적에게 직접 피해 (충돌 이벤트에 의존하지 않아 머무는 동안 계속 맞음)
                CombatUtil.DamageInCircle(center, s.rainRadius, MainDamage * s.rainTickDamageMultiplier * ExtraCards.WeaponSkillScale, true, false, GimmickCatalog.Instance.knockBowRain);
                nextTick += s.rainTickInterval;
            }

            if (Time.time >= nextDust)
            {
                nextDust += s.rainDustInterval;
                SpriteFx.RainEdgeDust(center, s.rainRadius);
            }

            arrowBudget += s.rainArrowsPerSecond * Time.deltaTime;
            while (arrowBudget >= 1f)
            {
                arrowBudget -= 1f;
                Vector2 point = UnityEngine.Random.insideUnitCircle * (s.rainRadius * 0.8f);   // 화살 그림이 범위 밖으로 나가지 않게 안쪽에
                StartCoroutine(FallingArrow(center + (Vector3)point));
            }

            yield return null;
        }

        Destroy(area.gameObject);

        if (awakening)
        {
            FirePiercingFlash(aimDir);                       // 각성 「관통 섬광」: 화살비 직후 조준 방향 거대 관통 화살
        }
    }

    // 화살비 한 발: 비스듬히 빠르게 떨어져 땅에 박히고 흙먼지가 튐. 박힌 화살은 잠깐 남았다가 사라짐
    private IEnumerator FallingArrow(Vector3 target)
    {
        BowMainSettings s = Settings.bow;
        Sprite sprite = s.rainArrowSprite != null ? s.rainArrowSprite : arrowSprite;
        if (sprite == null)
            yield break;

        Vector3 fall = new Vector3(0.25f, -1f, 0f).normalized;          // 오른쪽 아래로 살짝 비스듬히
        SpriteRenderer arrow = CreateVisual("Rain Arrow", sprite, null, 6);
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, fall);   // 화살 그림은 위를 향함
        arrow.transform.localScale = Vector3.one * s.rainArrowScale * 0.7f;

        // 화살 중심이 아니라 화살촉이 목표 지점에 닿도록 (화살 길이 절반만큼 뒤로)
        float halfLength = sprite.bounds.extents.y * s.rainArrowScale * 0.7f;
        target -= fall * halfLength * 0.9f;
        Vector3 start = target - fall * s.rainFallHeight;
        float fallTime = s.rainFallTime * 0.75f;
        float t = 0f;
        while (t < fallTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallTime);
            arrow.transform.position = Vector3.Lerp(start, target, k * k);   // 점점 빨라짐
            arrow.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
            yield return null;
        }

        arrow.transform.position = target;
        SpriteFx.ArrowLand(target + fall * halfLength * 0.9f);    // 화살촉이 박힌 자리에 흙먼지

        // 박힌 채로 잠깐 남았다가 사라짐
        float stay = 0.35f;
        t = 0f;
        while (t < stay)
        {
            t += Time.deltaTime;
            arrow.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t / stay));
            yield return null;
        }
        Destroy(arrow.gameObject);
    }

    // ---------------------------------------------------------------
    // 헤비보우건
    // ---------------------------------------------------------------
    private void SetupHeavyBowgun()
    {
        CreateHeldSprite("Held Heavy Bowgun");
        HeavyBowgunMainSettings s = Settings.heavyBowgun;
        heavyAmmo = s.heavyMax;
        rapidAmmo = s.rapidMax;
        rapidEquipped = false;                               // 중량탄 기본 장착
        ammoHud = HeavyAmmoHud.Create(s);

        GameObject prefab = GameManager.instance.pool.prefabs[weapon.prefabId];
        SpriteRenderer prefabRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (prefabRenderer != null)
        {
            bulletSprite = prefabRenderer.sprite;
            bulletColor = prefabRenderer.color;
        }
        bulletScale = prefab.transform.localScale;
    }

    // 캐릭터 공격력 배율 (다른 무기 피해와 같은 기준)
    private static float AttackScale => Character.Damage * (1f + Gear.damage_stat);

    private void UpdateHeavyBowgun()
    {
        HeavyBowgunMainSettings s = Settings.heavyBowgun;
        RechargeHeavyAmmo(s);

        if (scoping)
        {
            UpdateScope(s);
            return;
        }

        if (SkillPressed && SkillReady && !Switching)
        {
            StartScope(s);
            return;
        }

        if (Switching)
        {
            if (Time.time >= switchEndTime)
            {
                switchEndTime = -1f;
                rapidEquipped = !rapidEquipped;
            }
            return;                                          // 변경 중에는 발사 불가
        }

        if (rightClicked)
        {
            BeginSwitch(s);                                  // 잔탄과 관계없이 중량탄 ↔ 연사탄
            return;
        }

        if (!rapidEquipped)
        {
            if (mouseHoldValid && Time.time >= attackReadyTime && heavyAmmo >= 1f)
            {
                FireHeavySlug(s);
                heavyAmmo -= 1f;
                attackReadyTime = Time.time + data.heavyInterval / AttackSpeedFactor;   // 중량탄 발사 후 대기 = 공격속도
            }
        }
        else if (mouseHoldValid && Time.time >= rapidReadyTime && rapidAmmo >= 1f)
        {
            FireHeavyBullet(data.rapidDamage * AttackScale, s.rapidBulletSpeed, true, data.rapidPierce + ExtraCards.PierceBonus);
            rapidAmmo -= 1f;
            rapidReadyTime = Time.time + 1f / (data.rapidPerSecond * AttackSpeedFactor);   // 경량탄 연사 간격 = 공격속도

            // 공명 「칼날 연사」: 연사탄 누적 발수마다 칼날 발사 (탄을 바꿔 써도 누적 유지, 각성 중에도 유지)
            if (resonance)
            {
                stormAmmoSpent += 1f;
                if (stormAmmoSpent >= s.bladeEveryRapidShots)
                {
                    stormAmmoSpent -= s.bladeEveryRapidShots;
                    LaunchBladeStorm();
                }
            }
        }
    }

    // 탄 변경 시작 (변경 시간 동안 발사 불가, 찰칵 소리)
    private void BeginSwitch(HeavyBowgunMainSettings s)
    {
        GimmickCards.OnRightClickSkill();                    // 반격 본능 (헤비보우건 탄 변경)
        if (Switching) return;
        switchStartTime = Time.time;
        switchEndTime = Time.time + Mathf.Max(0.01f, s.switchTime);
        if (s.switchSound != null) ExtraCards.PlayClip(s.switchSound);
        else AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_ChargeMod);
    }

    // 중량탄은 들고 있어도 항상 회복, 연사탄은 장착하지 않았을 때만 회복
    private void RechargeHeavyAmmo(HeavyBowgunMainSettings s)
    {
        bool heavyEquipped = !rapidEquipped;
        heavyAmmo = Mathf.Min(s.heavyMax, heavyAmmo + Time.deltaTime / (data.heavyRechargeTime * CooldownFactor));   // 탄 충전 = 쿨타임 감소
        if (heavyEquipped)
        {
            rapidAmmo = Mathf.Min(s.rapidMax, rapidAmmo + data.rapidRechargePerSecond / CooldownFactor * Time.deltaTime);   // 탄 충전 = 쿨타임 감소
        }

        if (ammoHud != null)
        {
            bool visible = DayNightCycle.Instance == null || DayNightCycle.Instance.HudVisible;
            ammoHud.Refresh(visible, heavyEquipped, Mathf.FloorToInt(heavyAmmo), Mathf.FloorToInt(rapidAmmo), Switching,
                resonance, Mathf.FloorToInt(stormAmmoSpent), s.bladeEveryRapidShots);   // 칼날 누적은 공명 활성 때만
        }
    }

    // 중량탄: 무한 관통. 각성 「칼날 중량탄」이면 날카로운 총알 모양 탄이 화면 테두리에서 튕김
    private void FireHeavySlug(HeavyBowgunMainSettings s)
    {
        float damage = data.heavyDamage * AttackScale;
        if (!awakening)
        {
            FireHeavyBullet(damage, s.heavyBulletSpeed, false, -1);
            return;
        }

        Sprite sharp = HeavySheet.Bullet(true);              // 끝이 뾰족한 총알 (오른쪽을 향함)
        SynergyProjectile slug = SynergyProjectile.Spawn("Sharp Slug", sharp != null ? sharp : bulletSprite, MuzzlePoint(), aimDir * s.heavyBulletSpeed,
            s.awakenSlugLength * WeaponSizeScale, sharp != null ? 0f : -90f, damage, s.awakenBounces, hitLayer, playerSprite);   // 「무기·총알 크기」 (기본 공격)
        slug.maxHits = 0;                                    // 무한 관통 (튕기는 동안에도)
        slug.bossHits = Mathf.Max(1, s.awakenBossHits);
        slug.bossHitSparks = true;
        slug.sparkTrail = true;                              // 빛 입자 꼬리
        slug.gameObject.AddComponent<SlugTrailFx>();         // 금빛 궤적 + 바람결
        SpriteFx.Muzzle(MuzzlePoint(), aimDir, false);
        SpriteFx.BoostSpark(MuzzlePoint(), 0.6f);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Sniper);
    }

    // 보조 쌍검 칼날 그림 (보조 쌍검이 만렙이면 만렙 칼날)
    private Sprite DualBladeSprite()
    {
        int prefab = PartnerPrefab(0);
        SpriteRenderer bladeRenderer = prefab >= 0 ? GameManager.instance.pool.prefabs[prefab].GetComponentInChildren<SpriteRenderer>(true) : null;
        Sprite sprite = bladeRenderer != null ? bladeRenderer.sprite : null;
        foreach (Weapon w in player.GetComponentsInChildren<Weapon>(true))
        {
            if (w.id == 0 && w.Data != null && w.Data.weaponimage != null && w.Data.weaponimage.Length > 0 && Weapon.DB_level >= 5)
            {
                sprite = w.Data.weaponimage[0];
            }
        }
        return sprite;
    }

    // ---------------- 정밀 저격 ----------------
    private void StartScope(HeavyBowgunMainSettings s)
    {
        scoping = true;
        scopeShotsLeft = s.sniperShots;
        scopeEndTime = Time.time + s.sniperDuration;
        stealthEndTime = Time.time + s.stealthDuration;
        mouseHoldValid = false;
        MoveSpeedScale = s.scopeMoveScale;                   // 스코프 중에는 느리게 이동
        if (s.stealthDuration > 0f)
        {
            HeavyStealth.Begin(player.transform.position);
            stealthLeaves = SpriteFx.StealthLeaves(player.transform);
        }
        scopeUI = HeavyScopeUI.Open(s, s.sniperShots);
        scopeUI.SetAim(Input.mousePosition);
        if (s.scopeInSound != null) ExtraCards.PlayClip(s.scopeInSound);   // 조준 시작: 장전 소리 (비우면 소리 없음)
    }

    private void UpdateScope(HeavyBowgunMainSettings s)
    {
        if (HeavyStealth.Active && Time.time >= stealthEndTime)
        {
            EndStealth();                                    // 은신은 5초까지 (스코프는 유지 → 제자리에서 노출)
        }
        if (HeavyStealth.Active)
        {
            SetPlayerAlpha(s.stealthAlpha);
        }
        if (scopeUI != null)
        {
            scopeUI.SetAim(Input.mousePosition);
        }

        if (SkillPressed || Time.time >= scopeEndTime)
        {
            EndScope(s);
            return;
        }

        if (mouseClicked && scopeShotsLeft > 0)
        {
            SniperShot(s, mouseWorld);
            scopeShotsLeft--;
            if (scopeUI != null) scopeUI.Fire();
            if (scopeShotsLeft <= 0)
            {
                scopeEndTime = Time.time + 0.45f;            // 마지막 섬광을 보여 준 뒤 종료
            }
        }
    }

    private void SniperShot(HeavyBowgunMainSettings s, Vector3 point)
    {
        float scale = ExtraCards.WeaponSkillScale;
        CombatUtil.DamageInCircleSplit(point, s.sniperRadius, data.sniperMobDamage * AttackScale * scale, data.sniperBossDamage * scale, GimmickCatalog.Instance.knockHbgSniper);
        CameraShake.Strong();
        SpriteFx.StarBurst(point, s.sniperRadius * 1.3f);
        SpriteFx.HitSpark(point, 2f);
        if (s.sniperShotSound != null) AudioManager.instance.PlayClipFrom(s.sniperShotSound, s.sniperShotSoundStart);
        else AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Sniper);
        if (s.glassBreakSound != null) ExtraCards.PlayClip(s.glassBreakSound);
        if (s.shellDropSound != null && scopeShotsLeft > 1) StartCoroutine(PlayLater(s.shellDropSound, s.shellDropDelay));   // 한 발 쏜 뒤 장전음 (마지막 탄 뒤에는 없음)
    }

    private IEnumerator PlayLater(AudioClip clip, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (clip != null) ExtraCards.PlayClip(clip);
        else AudioManager.instance.PlaySfx(AudioManager.Sfx.Select);   // 탄피 떨어지는 소리 대신
    }

    private static void PlayOr(AudioClip clip, AudioManager.Sfx fallback)
    {
        if (clip != null) ExtraCards.PlayClip(clip);
        else AudioManager.instance.PlaySfx(fallback);
    }

    private void EndScope(HeavyBowgunMainSettings s)
    {
        if (!scoping) return;
        scoping = false;
        EndStealth();
        MoveSpeedScale = 1f;
        mouseHoldValid = false;
        if (scopeUI != null) scopeUI.Close();
        scopeUI = null;
        if (s.scopeOutSound != null) ExtraCards.PlayClip(s.scopeOutSound);   // 종료음 없음 (비우면 대체 효과음도 내지 않음)
        StartSkillCooldown(data.sniperCooldown);                // 쿨타임은 스킬이 끝난 때부터
    }

    private void EndStealth()
    {
        HeavyStealth.End();
        SetPlayerAlpha(1f);
        SpriteFx.Stop(stealthLeaves);
        stealthLeaves = null;
    }

    private void SetPlayerAlpha(float a)
    {
        if (playerSprite != null)
        {
            Color c = playerSprite.color;
            playerSprite.color = new Color(c.r, c.g, c.b, a);
        }
        if (heldRenderer != null)
        {
            Color c = heldRenderer.color;
            heldRenderer.color = new Color(c.r, c.g, c.b, a);
        }
    }

    // 들고 있는 헤비보우건의 총구 끝 위치 (총 그림 길이의 절반만큼 조준 방향 앞)
    private Vector3 MuzzlePoint()
    {
        if (heldRenderer != null && heldRenderer.sprite != null)
        {
            float half = heldRenderer.sprite.bounds.extents.x * Mathf.Abs(heldRenderer.transform.lossyScale.x);
            return heldRenderer.transform.position + aimDir * half * 0.95f;
        }
        return transform.position + aimDir * 0.9f;
    }

    // 연사탄: 탄 그림 대신 빛나는 탄 궤적 이펙트를 자식으로 붙여 표시 (일반탄으로 다시 쓰일 때는 원래대로)
    private void ApplyRapidBulletLook(Transform bullet, SpriteRenderer renderer, bool useTrail)
    {
        Transform trail = bullet.Find("Rapid Trail");
        Sprite trailSprite = useTrail ? SpriteFx.RapidBulletSprite : null;
        useTrail = trailSprite != null;

        if (useTrail && trail == null)
        {
            GameObject go = new GameObject("Rapid Trail");
            go.transform.SetParent(bullet, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 오른쪽을 향한 그림을 탄의 위쪽(진행 방향)으로
            SpriteRenderer trailRenderer = go.AddComponent<SpriteRenderer>();
            trailRenderer.sprite = trailSprite;
            if (renderer != null)
            {
                trailRenderer.sharedMaterial = renderer.sharedMaterial;
                trailRenderer.sortingLayerID = renderer.sortingLayerID;
                trailRenderer.sortingOrder = renderer.sortingOrder + 1;
            }
            trail = go.transform;
        }

        if (trail != null)
        {
            trail.gameObject.SetActive(useTrail);
            if (useTrail)
            {
                float width = Mathf.Max(0.0001f, trailSprite.bounds.size.x);
                Vector3 parentScale = bullet.lossyScale;
                float scale = 1.1f * WeaponSizeScale / width / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));   // 월드 길이 약 1.1 x 「무기·총알 크기」
                trail.localScale = new Vector3(scale, scale, 1f);
            }
        }
        if (renderer != null)
        {
            renderer.enabled = !useTrail;
        }
    }

    private void FireHeavyBullet(float damage, float bulletSpeed, bool rapid, int pierce)
    {
        HeavyBowgunMainSettings s = Settings.heavyBowgun;
        Transform bullet = GameManager.instance.pool.Get(weapon.prefabId).transform;
        bullet.position = transform.position;
        bullet.rotation = Quaternion.FromToRotation(Vector3.up, aimDir);
        bullet.GetComponent<Bullet>().Init(damage, pierce, aimDir);   // 관통 -1 = 무한 관통
        bullet.GetComponent<Bullet>().KnockChance = rapid ? GimmickCatalog.Instance.knockHbgLight : GimmickCatalog.Instance.knockHbgHeavy;
        SpriteFx.Muzzle(MuzzlePoint(), aimDir, rapid);           // 총구 끝에서 불꽃 + 연기 + 탄 궤적

        // 풀의 탄환을 일반탄과 연사탄이 같이 쓰므로 발사할 때마다 모양을 다시 지정
        bullet.localScale = rapid ? bulletScale * s.rapidBulletScale : bulletScale;
        bullet.GetComponent<Bullet>().ApplyWeaponSize();        // 능력치 「무기·총알 크기」 (기본 공격, 모양을 정한 뒤)
        SpriteRenderer renderer = bullet.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = rapid && s.rapidBulletSprite != null ? s.rapidBulletSprite : bulletSprite;
            renderer.color = rapid ? s.rapidBulletColor : bulletColor;
        }
        ApplyRapidBulletLook(bullet, renderer, rapid && s.rapidBulletSprite == null);

        Rigidbody2D rigid = bullet.GetComponent<Rigidbody2D>();
        if (rigid != null)
        {
            rigid.velocity = aimDir * bulletSpeed;
        }

        if (!rapid)
        {
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Sniper);
        }
        else if (s.rapidSound != null)
        {
            AudioManager.instance.PlayClip(s.rapidSound);
        }
        else
        {
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Range);
        }
    }

    // ---------------------------------------------------------------
    // 대검
    // ---------------------------------------------------------------
    private void SetupGreatSword()
    {
        Transform child = transform.GetChild(0);
        sword = child.GetComponent<Bullet>();
        swordRenderer = child.GetComponentInChildren<SpriteRenderer>();
        sword.Init(MainDamage, -100, Vector3.zero);
        sword.ApplyWeaponSize();                             // 능력치 「무기·총알 크기」 (기본 공격)
        sword.IsHarmless = true;

        if (weapon.Data.weaponimage != null && weapon.Data.weaponimage.Length > 0)
        {
            weapon.changesprite();
        }

        FitSwordHitbox();
        transform.localRotation = UpTowards(AimAngle);

        // 기본 공격·끌기는 손잡이를 쥔 자리, 모아 베기는 기존 위치
        swordFarLocal = sword.transform.localPosition;
        if (swordRenderer != null && swordRenderer.sprite != null)
        {
            Bounds b = swordRenderer.sprite.bounds;
            swordHalf = (b.extents.y - b.center.y) * Mathf.Abs(swordRenderer.transform.lossyScale.y);
            swordBaseLayer = swordRenderer.sortingLayerID;
            swordBaseOrder = swordRenderer.sortingOrder;
        }
        float hold = Mathf.Max(LanceBase.y, swordHalf - 0.15f);
        swordHoldLocal = Vector3.up * (hold / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y)));
        swordSizeAtSetup = WeaponSizeScale;
        BeginAttack(GsEquipLift());                          // 전환 순간 들어 올림

        GreatSwordMainSettings s = Settings.greatSword;
        chargeVisual = CreateVisual("Charge Effect", s.chargeSprite != null ? s.chargeSprite : circleSprite, player.transform, -1);
        chargeVisual.gameObject.SetActive(false);
    }

    // 대검 판정을 스프라이트 크기와 같은 사각형으로 바꿈 (기존 판정이 얇아 회전 중 빗나가던 문제)
    private void FitSwordHitbox()
    {
        if (swordRenderer == null || swordRenderer.sprite == null)
            return;

        GreatSwordMainSettings s = Settings.greatSword;
        Transform swordTransform = sword.transform;
        Bounds bounds = swordRenderer.sprite.bounds;

        // 스프라이트가 자식 오브젝트에 있어도 대검 오브젝트 기준 위치, 크기로 환산
        Vector3 center = swordTransform.InverseTransformPoint(swordRenderer.transform.TransformPoint(bounds.center));
        Vector3 rendererScale = swordRenderer.transform.lossyScale;
        Vector3 swordScale = swordTransform.lossyScale;
        Vector2 size = new Vector2(
            bounds.size.x * Mathf.Abs(rendererScale.x / swordScale.x),
            bounds.size.y * Mathf.Abs(rendererScale.y / swordScale.y));

        foreach (Collider2D old in sword.GetComponents<Collider2D>())
        {
            Destroy(old);
        }

        BoxCollider2D box = sword.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.offset = center;
        box.size = new Vector2(size.x * s.hitWidthScale, size.y * s.hitLengthScale);
    }

    private float GreatSwordSpinSpeed => Settings.greatSword.chargeSpinSpeed;   // 모아 베기 대회전: 고정 속도 (공격속도 영향 없음)

    private void UpdateGreatSword()
    {
        if (gsCharging)
        {
            UpdateGreatSwordCharge();
            return;
        }

        if (SkillPressed && SkillReady && !player.IsForcedMoving && Settings.greatSword.chargeStages.Length > 0)
        {
            EndStuck(false);                                 // 찍힌 상태에서도 모아 베기로 넘어감
            SetSwordFar(false);                              // 칼을 쥔 자리에서 기 모으기 (대회전에서 점점 넓어짐)
            slamWindup = false;
            if (slamRange != null) slamRange.gameObject.SetActive(false);
            SetSwordBehind(false);
            if (swordRenderer != null) swordRenderer.color = Color.white;
            StartGreatSwordCharge();
            return;
        }

        UpdateSlamRange(!gsStuck && (!isAttacking || slamWindup));
        if (!gsStuck) SetJustGlow(false);

        if (gsStuck)
        {
            UpdateGsStuck();
            return;
        }

        if (isAttacking)
            return;

        bool back = UpdateDragPose();

        // 누르고 있으면 내려찍기 → 원위치 → 내려찍기만 반복 (후리기는 새로 눌렀을 때만). 대검이 뒤로 다 돌아온 뒤에만 찍음
        if ((mouseClicked || mouseHoldValid) && back && Time.time >= attackReadyTime && !player.IsForcedMoving)
        {
            BeginAttack(GsSlam());
        }
    }



    // ---------------------------------------------------------------
    // 대검 기본 공격: 내려찍기 → 후리기, 찍힌 상태에서 저스트 올려치기
    // ---------------------------------------------------------------
    private float GsBasicDamage => MainDamage * Settings.greatSword.basicDamageMultiplier;   // 피해 배율 기준 = 기존 대검 기본 공격 피해

    // 대검 위치: 기본 공격·끌기 = 손잡이를 쥔 자리, 모아 베기(R) = 기존 회전 반경
    private void SetSwordFar(bool far)
    {
        // 쥔 자리는 「무기·총알 크기」만큼 바깥으로 (칼이 커져도 손잡이는 캐릭터 몸통, 칼끝은 더 멀리)
        Vector3 local = far ? swordFarLocal : swordHoldLocal * (WeaponSizeScale / Mathf.Max(0.01f, swordSizeAtSetup));
        if (sword.transform.localPosition != local)
        {
            sword.transform.localPosition = local;
        }
    }

    // 끄는 동안은 캐릭터 뒤에 그림
    private void SetSwordBehind(bool behind)
    {
        if (swordRenderer == null || swordBehind == behind)
            return;
        swordBehind = behind;
        swordRenderer.sortingLayerID = behind ? playerSprite.sortingLayerID : swordBaseLayer;
        swordRenderer.sortingOrder = behind ? playerSprite.sortingOrder - 1 : swordBaseOrder;
    }

    private float PivotAngle => transform.localEulerAngles.z + 90f;    // 대검이 향한 각도 (UpTowards 역변환)

    private Vector3 SwordTip => transform.position + transform.up * (swordHoldLocal.magnitude * Mathf.Abs(transform.lossyScale.y) + swordHalf);

    // 대검 그림은 오른쪽이 날: 시계 방향으로 휘두르면 그대로, 반시계 방향이면 좌우 반전해 항상 날이 앞장섬
    private void SetSwordEdge(bool flip)
    {
        if (swordRenderer != null) swordRenderer.flipX = flip;
    }

    // 대검을 fromAngle → toAngle로 휘두름 (앞쪽이 빠른 곡선). edgeFlip을 주면 그 방향 칼날로, 없으면 회전 방향에 맞춤
    private IEnumerator SwingSword(float fromAngle, float toAngle, float duration, bool? edgeFlip = null)
    {
        SetSwordEdge(edgeFlip ?? toAngle > fromAngle);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
            p = p * p * (3f - 2f * p);
            transform.localRotation = UpTowards(Mathf.Lerp(fromAngle, toAngle, p));
            yield return null;
        }
        transform.localRotation = UpTowards(toAngle);
    }

    // 메인무기 전환 순간: 대검을 짧게 머리 위로 들어 올림 → 이후 끌고 다님
    private IEnumerator GsEquipLift()
    {
        SetSwordFar(false);
        SetSwordBehind(false);
        float cur = PivotAngle;
        yield return SwingSword(cur, cur + Mathf.DeltaAngle(cur, 90f), 0.15f);   // 가까운 쪽으로 들어 올림
        SpriteFx.BoostSpark(SwordTip, 0.8f);
        yield return new WaitForSeconds(0.35f);
    }

    // 평상시: 칼끝을 땅에 대고 캐릭터 뒤로 끌고 다님 (이동속도 변화 없음)
    private bool UpdateDragPose()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        sword.transform.localPosition = Vector3.MoveTowards(sword.transform.localPosition, swordHoldLocal, 10f * Time.deltaTime);   // 대회전 뒤 부드럽게 쥔 자리로
        SetSwordBehind(false);                               // 손잡이가 캐릭터 손 위에 보이게 (캐릭터 앞에 그림)
        float behindX = playerSprite.flipX ? -1f : 1f;      // flipX = 오른쪽을 봄 → 대검은 왼쪽 뒤
        if (dragLockX != 0f)
        {
            behindX = dragLockX;                             // 원위치 중에는 시작할 때의 뒤쪽으로만 (방향을 바꿔도 늦어지지 않게)
        }
        SetSwordEdge(behindX < 0f);                          // 칼날이 땅을 향하게
        float dragAngle = Mathf.Atan2(-s.dragDown, behindX) * Mathf.Rad2Deg;
        float maxStep = GameManager.instance.greatSwordReturnSpeed * Time.deltaTime;
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, UpTowards(dragAngle), maxStep);
        bool arrived = Quaternion.Angle(transform.localRotation, UpTowards(dragAngle)) < 2f
            && (sword.transform.localPosition - swordHoldLocal).sqrMagnitude < 0.0004f;   // 뒤로 다 돌아왔는지

        if (player.inputVec.sqrMagnitude > 0.01f)
        {
            if (Time.time >= nextDragDust)
            {
                nextDragDust = Time.time + s.dragDustInterval;
                SpriteFx.ChargeTrail(SwordTip, -player.inputVec);   // 끌리는 칼끝 흙먼지
            }
            if (s.dragSound != null && Time.time >= nextDragSound)
            {
                nextDragSound = Time.time + s.dragSoundInterval;
                ExtraCards.PlayClip(s.dragSound);
            }
        }
        if (arrived)
        {
            dragLockX = 0f;                                  // 다 돌아오면 다시 지금 방향의 뒤쪽을 따라감
        }
        return arrived;
    }

    // 원위치 시작: 지금 뒤쪽 방향을 고정
    private void LockDragSide()
    {
        dragLockX = playerSprite.flipX ? -1f : 1f;
    }

    // 저스트 구간: 대검 뒤에 같은 그림을 크게 금빛으로 겹쳐 후광처럼 맥동
    private void SetJustGlow(bool on)
    {
        if (swordRenderer == null)
            return;
        if (justGlow == null)
        {
            if (!on) return;
            justGlow = CreateVisual("Just Glow", swordRenderer.sprite, swordRenderer.transform, 0);
            Shader solid = Shader.Find("GUI/Text Shader");   // 그림 모양 그대로 단색 실루엣 (어두운 부분도 금빛)
            if (solid != null)
            {
                justGlowMaterial = new Material(solid);
                justGlow.sharedMaterial = justGlowMaterial;
            }
            justGlow.transform.localPosition = Vector3.zero;
            justGlow.transform.localRotation = Quaternion.identity;
        }
        if (justGlow.enabled != on) justGlow.enabled = on;
        if (!on) return;
        GreatSwordMainSettings s = Settings.greatSword;
        justGlow.sprite = swordRenderer.sprite;
        justGlow.flipX = swordRenderer.flipX;
        justGlow.sortingLayerID = swordRenderer.sortingLayerID;
        justGlow.sortingOrder = swordRenderer.sortingOrder - 1;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
        justGlow.transform.localScale = Vector3.one * (s.justGlowScale + 0.08f * pulse);
        Color c = s.justFlashColor;
        justGlow.color = new Color(c.r, c.g, c.b, 0.55f + 0.4f * pulse);
    }

    // 내려찍기 범위: 캐릭터 앞 바닥 원 (평상시 옅게, 들어 올리는 동안 진하게)
    private void UpdateSlamRange(bool show)
    {
        GreatSwordMainSettings s = Settings.greatSword;
        float alpha = slamWindup ? s.slamRangeWindupAlpha : s.slamRangeIdleAlpha;
        show = show && alpha > 0f && !gsCharging;
        if (slamRange == null)
        {
            if (!show) return;
            slamRange = CreateVisual("Slam Range", circleSprite, null, 4);
            slamRange.sortingLayerID = CombatUtil.EnemySortingLayer;
        }
        if (slamRange.gameObject.activeSelf != show) slamRange.gameObject.SetActive(show);
        if (!show) return;
        Vector3 dir = slamWindup ? slamDir : aimDir;      // 들어 올린 뒤에는 방향 고정
        float slamR = s.slamRange * WeaponSizeScale;
        slamRange.transform.position = player.transform.position + dir * slamR * 0.6f;
        slamRange.sortingOrder = s.slamRangeSortingOrder;
        SetWorldDiameter(slamRange, slamR * 2f);
        Color c = s.slamRangeColor;
        slamRange.color = new Color(c.r, c.g, c.b, alpha);
    }

    // 찍힌 상태 시작 (canSweep: 후리기 가능, recover초 뒤 원위치)
    private void BeginStuck(bool canSweep, float recover)
    {
        gsStuck = true;
        gsStuckStart = Time.time;
        gsStuckEnd = Time.time + recover;
        gsCanSweep = canSweep;
        gsJudged = false;
        gsJustSoundPlayed = false;
    }

    private void EndStuck(bool playSound)
    {
        if (!gsStuck)
            return;
        sword.IsHarmless = true;
        gsStuck = false;
        if (swordRenderer != null) swordRenderer.color = Color.white;
        SetJustGlow(false);
        attackReadyTime = Time.time + data.nextAttackDelay * CooldownFactor;   // 다음 기본 공격까지 대기 = 쿨타임 감소
        LockDragSide();
        if (playSound && Settings.greatSword.recoverSound != null)
        {
            ExtraCards.PlayClip(Settings.greatSword.recoverSound);
        }
    }

    // 찍힌 상태: 좌클릭 후리기, 우클릭 저스트 판정 (찍힐 때마다 첫 입력 1회만), recover 후 원위치
    private void UpdateGsStuck()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        float t = Time.time - gsStuckStart;
        bool inJust = t >= s.justStart && t < s.justStart + s.justWindow;

        // 저스트 구간: 대검이 반짝임
        if (inJust && !gsJudged)
        {
            Vector3 tip = SwordTip;
            if (!gsJustSoundPlayed)
            {
                gsJustSoundPlayed = true;
                if (s.justSparkSound != null) ExtraCards.PlayClip(s.justSparkSound);   // 비우면 소리 없음
                SynergyAwakening.ShockRing(tip, 2.2f, 0.25f);  // 구간 시작: 칼끝에서 퍼지는 고리
                nextJustGlint = 0f;
            }
            if (Time.time >= nextJustGlint)
            {
                nextJustGlint = Time.time + s.justGlintInterval;
                SpriteFx.StarBurst(tip, s.justGlintSize);     // 칼끝 큰 반짝임 (구간 동안 반복)
            }
            if (Time.time >= nextJustSpark)
            {
                nextJustSpark = Time.time + 0.04f;
                SpriteFx.BoostSpark(Vector3.Lerp(transform.position, tip, UnityEngine.Random.Range(0.3f, 1f)), 0.7f);
            }
            if (swordRenderer != null)
            {
                swordRenderer.color = Color.Lerp(Color.white, s.justFlashColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
            }
            SetJustGlow(true);
        }
        else
        {
            if (swordRenderer != null) swordRenderer.color = Color.white;
            SetJustGlow(false);
        }

        if (isAttacking)
            return;

        if (rightClicked && !gsJudged)
        {
            gsJudged = true;                                 // 첫 입력으로 판정, 이후 입력 무시
            GimmickCards.OnRightClickSkill();                // 반격 본능 (대검 저스트 입력)
            if (inJust)
            {
                BeginAttack(GsUpper());
                return;
            }
            gsStuckEnd = Mathf.Min(gsStuckEnd, Time.time + data.failRecoverTime * CooldownFactor);   // 실패: 빠른 원위치 (쿨타임 감소)
        }
        else if (mouseClicked && gsCanSweep)
        {
            BeginAttack(GsSweep());
            return;
        }

        if (Time.time >= gsStuckEnd)
        {
            EndStuck(true);
        }
    }

    // 1타 내려찍기: 대검을 들어 올려 앞쪽으로 내려찍음 (강타)
    private IEnumerator GsSlam()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        float speed = AttackSpeedFactor;
        Vector3 dir = aimDir;
        float aim = AimAngle;
        float side = dir.x >= 0f ? 1f : -1f;
        SetSwordFar(false);
        SetSwordBehind(false);

        float cur = PivotAngle;
        float raised = aim + 160f * side;                     // 머리 위 뒤쪽으로 들어 올림
        raised = cur + Mathf.DeltaAngle(cur, raised);         // 지금 각도에서 가까운 쪽으로 (갑자기 한 바퀴 돌지 않게)
        float strike = raised - 160f * side;                  // 머리 위를 넘어 앞으로 내려찍음
        bool edge = strike > raised;                          // 내려찍는 방향으로 칼날
        slamDir = dir;
        slamWindup = true;
        yield return SwingSword(cur, raised, data.slamRaiseTime / speed, edge);
        yield return SwingSword(raised, strike, data.slamStrikeTime / speed, edge);
        slamWindup = false;

        float slamR = s.slamRange * WeaponSizeScale;         // 「무기·총알 크기」만큼 범위 증가
        Vector3 center = player.transform.position + dir * slamR * 0.6f;
        CombatUtil.DamageInCircle(center, slamR, GsBasicDamage * s.slamDamageMul, true, false, GimmickCatalog.Instance.knockGsLeft);   // 관통 무한
        HitFeedback.Strong(center, slamR * 1.2f);
        SpriteFx.GreatCharge(center, dir, 1);                // 땅 갈라짐
        StartCoroutine(FlashCircle(center, slamR * 2f, new Color(s.slamRangeColor.r, s.slamRangeColor.g, s.slamRangeColor.b, 0.5f), 0.25f));   // 맞은 범위
        RunUI.PlayOr(s.slamSound, AudioManager.Sfx.Weapon_GreatSword);
        BeginStuck(true, s.recoverTime);
    }

    // 2타 후리기: 캐릭터 주위 360도 (약타)
    private IEnumerator GsSweep()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        gsStuck = false;
        float speed = AttackSpeedFactor;
        float from = PivotAngle;
        RunUI.PlayOr(s.sweepSound, AudioManager.Sfx.Weapon_GreatSword);
        HitFeedback.Weak(player.transform.position, s.sweepRange * WeaponSizeScale);
        SynergyAwakening.ShockRing(player.transform.position, s.sweepRange * WeaponSizeScale * 2f, 0.25f);
        bool flipped = swordRenderer != null && swordRenderer.flipX;
        SetHit(sword, GsBasicDamage * s.sweepDamageMul, true);   // 대검 그림 판정이 지나가는 적에게 피해 (관통 무한)
        Rehit(sword);
        yield return SwingSword(from, from + (flipped ? 360f : -360f), data.sweepTime / speed, flipped);   // 칼날이 앞장서는 방향으로 한 바퀴
        sword.IsHarmless = true;
        BeginStuck(false, s.recoverTime);
    }

    // 저스트 성공: 캐릭터 앞 크게 올려치기 (강타) → 빠른 원위치, 바로 내려찍기 가능
    private IEnumerator GsUpper()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        gsStuck = false;
        if (swordRenderer != null) swordRenderer.color = Color.white;
        float speed = AttackSpeedFactor;
        Vector3 dir = aimDir;
        float aim = AimAngle;
        float side = dir.x >= 0f ? 1f : -1f;

        float upperR = s.upperRange * WeaponSizeScale;       // 「무기·총알 크기」만큼 범위 증가
        Vector3 center = player.transform.position + dir * upperR * 0.6f;
        CombatUtil.DamageInCircle(center, upperR, GsBasicDamage * s.upperDamageMul, true, false, GimmickCatalog.Instance.knockGsJust);
        HitFeedback.Strong(center, upperR * 1.3f);
        SpriteFx.GreatSlash(center, dir, upperR * 1.6f);
        SpriteFx.BoostSpark(center, 1.2f);
        RunUI.PlayOr(s.justSuccessSound, AudioManager.Sfx.Weapon_Sniper);   // 삐융!

        float cur = PivotAngle;
        float start = cur + Mathf.DeltaAngle(cur, aim);
        yield return SwingSword(start, start + 160f * side, data.upperSwingTime / speed);   // 회전 방향에 맞춰 칼날
        yield return new WaitForSeconds(data.justRecoverTime * CooldownFactor);   // 원위치 복귀 = 쿨타임 감소
        if (s.recoverSound != null) ExtraCards.PlayClip(s.recoverSound);
        attackReadyTime = Time.time;
        LockDragSide();
    }

    private IEnumerator SpinSword(int spins, float degreePerSecond)
    {
        SetSwordEdge(false);                                 // 시계 방향 회전 = 오른쪽 날이 앞장
        float total = 360f * Mathf.Max(1, spins);
        float done = 0f;
        float nextSound = 0f;

        while (done < total)
        {
            if (done >= nextSound)
            {
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_GreatSword);
                nextSound += 360f;
            }

            float step = Mathf.Min(degreePerSecond * Time.deltaTime, total - done);
            transform.Rotate(Vector3.back * step);
            done += step;
            if (weapon.id == 3)
            {
                // 칼을 쥔 자리에서 시작해 회전하며 점점 기존 반경까지 넓어짐
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(done / total), Settings.greatSword.spinExpandEase);
                sword.transform.localPosition = Vector3.Lerp(swordHoldLocal, swordFarLocal, k);
            }
            yield return null;
        }
    }

    private void FireSwordWave(Vector3 dir, float damage)
    {
        GreatSwordMainSettings s = Settings.greatSword;
        Sprite effectWave = SpriteFx.SwordWaveSprite;               // 흰 초승달 검기 (오른쪽을 향함)
        Sprite sprite = effectWave != null ? effectWave : s.waveSprite != null ? s.waveSprite : (swordRenderer != null ? swordRenderer.sprite : null);
        if (sprite == null)
            return;

        SpriteRenderer wave = CreateVisual("Sword Wave", sprite, null, -1);   // 캐릭터 뒤 (몬스터보다는 앞)
        wave.color = effectWave != null ? new Color(0.85f, 0.95f, 1f, 0.95f) : s.waveColor;

        GameObject go = wave.gameObject;
        go.tag = "Bullet";
        go.layer = hitLayer;
        // 대검 끝(칼날 가장 먼 곳)에서 출발
        float reach = s.waveStartDistance;
        if (swordRenderer != null)
        {
            Bounds b = swordRenderer.bounds;
            reach = Mathf.Max(reach, Vector2.Distance(transform.position, b.center) + Mathf.Max(b.extents.x, b.extents.y) * 0.8f);
        }
        go.transform.position = transform.position + dir * reach;
        if (effectWave != null)
        {
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            wave.flipY = dir.x < 0f;
            SetWorldDiameter(wave, 2.4f * s.waveScale);
        }
        else
        {
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 0f, s.waveAngleOffset);
            go.transform.localScale = Vector3.one * s.waveScale;
        }

        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = sprite.bounds.size;
        col.offset = sprite.bounds.center;

        Bullet bullet = go.AddComponent<Bullet>();
        bullet.Init(damage, -100, Vector3.zero);    // 적을 관통

        rigid.velocity = dir * s.waveSpeed;
        Destroy(go, s.waveLifetime);

    }

    private void StartGreatSwordCharge()
    {
        StopAttack();
        sword.IsHarmless = true;

        gsCharging = true;
        gsChargeStart = Time.time;
        gsStage = 0;
        chargePulse = 1f;

        player.SetWeaponRooted(true);               // 모으는 동안 이동 불가
        CameraShake.Weak();
        gsChargeAura = SpriteFx.ChargeAura(player.transform, 1.6f);   // 발밑 금빛 기둥 (캐릭터 뒤)
        chargeVisual.gameObject.SetActive(gsChargeAura == null);       // 시트가 없을 때만 원 표시
        chargeVisual.color = Settings.greatSword.chargeStages[0].color;
        RunUI.PlayOr(RunConfig.Instance.skillActivateSound, AudioManager.Sfx.Weapon_ChargeMod);   // 스킬 발동 소리
    }

    private void UpdateGreatSwordCharge()
    {
        GreatSwordMainSettings s = Settings.greatSword;
        GreatSwordChargeStage[] stages = s.chargeStages;
        float held = Time.time - gsChargeStart;

        int stage = gsStage;
        while (stage + 1 < stages.Length && held >= stages[stage + 1].holdTime)
        {
            stage++;
        }

        if (stage != gsStage)
        {
            // 단계가 오르면 색이 바뀌며 크게 번쩍임
            gsStage = stage;
            chargePulse = 1f;
            chargeVisual.color = stages[stage].color;
            SpriteFx.ChargeUp(player.transform, stage);         // 단계 상승 금빛 고리
            if (gsChargeAura != null)
            {
                SpriteFx.StopAura(gsChargeAura);                // 단계가 오르면 오라가 커지고 단계 색이 섞임
                gsChargeAura = SpriteFx.ChargeAura(player.transform, 1.6f + stage * 0.5f);
                SpriteFx.TintAura(gsChargeAura, Color.Lerp(Color.white, stages[stage].color, 0.45f));
            }
            RunUI.PlayOr(RunConfig.Instance.skillActivateSound, AudioManager.Sfx.Weapon_ChargeMod);   // 스킬 발동 소리
        }

        chargePulse = Mathf.MoveTowards(chargePulse, 0f, Time.deltaTime / 0.25f);
        SetWorldDiameter(chargeVisual, s.chargeEffectSize * (1f + 0.15f * gsStage) * (1f + 0.4f * chargePulse));
        transform.localRotation = UpTowards(AimAngle);

        if (!SkillHeld)
        {
            ReleaseGreatSwordCharge();
        }
    }

    private void ReleaseGreatSwordCharge()
    {
        gsCharging = false;
        player.SetWeaponRooted(false);
        chargeVisual.gameObject.SetActive(false);
        SpriteFx.StopAura(gsChargeAura);
        gsChargeAura = null;

        StartSkillCooldown(data.chargeCooldown);
        BeginAttack(ChargedSlash(Settings.greatSword.chargeStages[gsStage]));
    }

    private IEnumerator ChargedSlash(GreatSwordChargeStage stage)
    {
        float spinSpeed = GreatSwordSpinSpeed;
        float moveTime = 360f * Mathf.Max(1, stage.spins) / spinSpeed;
        float damage = MainDamage * stage.damageMultiplier * ExtraCards.WeaponSkillScale;   // 무기 숙련

        transform.localRotation = UpTowards(AimAngle);
        SetHit(sword, damage, true);
        sword.KnockChance = GimmickCatalog.Instance.knockGsSkill;
        Rehit(sword);

        int stageIndex = gsStage;
        SpriteFx.GreatCharge(transform.position, aimDir, stageIndex);   // 출발 지점 땅 갈라짐
        player.ForceMove(aimDir, stage.distance, moveTime, 0f);   // 회전하며 에임 방향으로 전진
        yield return SpinSword(stage.spins, spinSpeed);

        SetHit(sword, damage, false);
        sword.KnockChance = -1f;                             // 다시 좌클릭 확률
        SpriteFx.GreatCharge(transform.position + aimDir, aimDir, stageIndex);   // 도착 지점 땅 솟음
        SpriteFx.GreatSlash(transform.position + aimDir * 1.2f, aimDir, 3.5f + stageIndex);

        // 각성 「천지 가르기」: 최대 차지일 때만 기존 베기 직후 추가 발동
        if (awakening && stageIndex == Settings.greatSword.chargeStages.Length - 1)
        {
            StartCoroutine(HeavenSplit());
        }
    }

    // ---------------------------------------------------------------
    // 랜스
    // ---------------------------------------------------------------
    private void SetupLance()
    {
        lanceTransform = transform.GetChild(0);
        lance = lanceTransform.GetComponent<Bullet>();
        lanceCollider = lanceTransform.GetComponent<CircleCollider2D>();
        lanceRenderer = lanceTransform.GetComponentInChildren<SpriteRenderer>();
        lance.Init(MainDamage, -100, Vector3.zero);
        // 「무기·총알 크기」는 UpdateHeldLance에서 직접 곱함 (창일 때만, 패링 방패는 그대로)
        lance.RehitInterval = 0f;                            // 보조 랜스용 재타격 간격은 메인무기에서 쓰지 않음
        lanceRootScale = lanceTransform.localScale;
        if (lanceRenderer != null)
        {
            lanceBaseScale = lanceRenderer.transform.localScale;
        }

        ApplyLanceForm();
        transform.localRotation = UpTowards(AimAngle);

        zoneSortingLayer = CombatUtil.EnemySortingLayer;
        stackHud = LanceStackHud.Create(Settings.lance);    // 화면 왼쪽 철벽 스택 표시
    }

    private Sprite GetLanceImage(int index)
    {
        Sprite[] images = weapon.Data.weaponimage;
        return images != null && index < images.Length ? images[index] : null;
    }

    private void ApplyLanceForm()
    {
        Sprite sprite = GetLanceImage(2) != null ? GetLanceImage(2) : GetLanceImage(0);
        if (sprite != null && lanceRenderer != null)
        {
            lanceRenderer.sprite = sprite;
        }

        if (lanceCollider != null)
        {
            lanceCollider.offset = new Vector2(0f, 0.47f);
            lanceCollider.radius = 1.36f;
        }
        lanceTransform.localPosition = LanceHold;
        lanceTransform.localRotation = Quaternion.identity;
        RefreshLanceHarm();
        ApplyLanceScale();                                   // 창은 바로 크기 배율 적용
    }

    private void ApplyShieldForm()
    {
        Sprite sprite = GetLanceImage(1);
        if (sprite != null && lanceRenderer != null)
        {
            lanceRenderer.sprite = sprite;
        }

        if (lanceCollider != null)
        {
            lanceCollider.offset = Vector2.zero;
            lanceCollider.radius = 1f;
        }
        lanceTransform.localPosition = Vector3.zero;
        lanceTransform.localRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
        lance.IsHarmless = true;
        ApplyLanceScale();                                   // 방패는 바로 원래 크기로

        SpriteFx.StopAura(guardAura);
        guardAura = SpriteFx.GuardAura(player.transform);   // 방어 자세: 발밑 파란 마법진
    }

    // 찌르지 않을 때 랜스는 판정 없음
    private void RefreshLanceHarm()
    {
        if (guarding)
            return;

        SetHit(lance, MainDamage * Settings.lance.basicDamageMultiplier, false);
    }

    // 패링 쿨타임 표시 (머리 위 공용 UI): 쿨타임 중에는 원형 게이지가 차오르고, 다 차면 방패 아이콘
    private void UpdateParryIcon()
    {
        if (overhead == null)
            return;
        if (guarding)
        {
            overhead.Hide();
            return;
        }
        LanceMainSettings s = Settings.lance;
        float progress = s.parryCooldown > 0f ? 1f - Mathf.Clamp01((guardReadyTime - Time.time) / s.parryCooldown) : 1f;
        bool ready = Time.time >= guardReadyTime;
        Sprite shield = SpriteFx.ShieldIconSprite != null ? SpriteFx.ShieldIconSprite : GetLanceImage(1);   // Weapon_effect_1 방패
        overhead.ShowCooldown(progress, ready, shield);
    }

    private void UpdateLance()
    {
        LanceMainSettings s = Settings.lance;
        UpdateParryIcon();

        if (guarding && Time.time >= guardEndTime)
        {
            EndGuard();                                      // parryWindow 안에 아무것도 막지 못함 = 헛방어
        }
        if (gsHeld && Time.time >= gsHeldUntil)
        {
            EndLiberation();                                 // 영역이 끝나면 대검을 넣고 창을 다시 듦
        }
        UpdateHeldLance();
        if (stackHud != null)
        {
            stackHud.Refresh(DayNightCycle.Instance == null || DayNightCycle.Instance.HudVisible, ironStacks, s.maxStacks);
        }

        // R: 영역 위치 지정 중 (좌클릭 확정, 우클릭·R 취소)
        if (zoneTargeting)
        {
            UpdateZoneTargeting();
            return;
        }

        if (SkillPressed && SkillReady && !zoneCharging)
        {
            BeginZoneTargeting();
            return;
        }

        if (rightClicked && !guarding && Time.time >= guardReadyTime)
        {
            StartGuard();
        }

        // 기 모으기: 공격 불가 (이동, 패링은 가능)
        if (zoneCharging)
        {
            UpdateZoneCharge();
            return;
        }

        if (guarding || isAttacking)
            return;

        transform.localRotation = UpTowards(AimAngle);

        if (mouseHoldValid && Time.time >= attackReadyTime)
        {
            BeginAttack(gsHeld ? LiberatedCombo() : LanceBasic());
        }
    }

    private IEnumerator LanceBasic()
    {
        LanceMainSettings s = Settings.lance;
        float speed = AttackSpeedFactor;

        foreach (float distance in s.thrustDistances)
        {
            float damage = MainDamage * s.basicDamageMultiplier;
            transform.localRotation = UpTowards(AimAngle);
            SetHit(lance, damage, true);
            Rehit(lance);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Melee);
            SpriteFx.LanceThrust(transform.position, aimDir, (1.6f + distance) * WeaponSizeScale);   // 찌르기 궤적

            float duration = data.thrustTime / speed;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float offset = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI) * distance * WeaponSizeScale;   // 앞으로 찌르고 돌아옴 (크기만큼 더 깊게)
                lanceTransform.localPosition = LanceHold + Vector3.up * offset;
                yield return null;
            }
            lanceTransform.localPosition = LanceHold;

            yield return new WaitForSeconds(data.thrustInterval / speed);   // 찌르기 사이 (공격하는 중) = 공격속도
        }

        RefreshLanceHarm();
        attackReadyTime = Time.time + data.thrustComboCooldown * CooldownFactor;
    }

    // 우클릭 패링 시작: parryWindow초 동안 방패를 듦
    private void StartGuard()
    {
        GimmickCards.OnRightClickSkill();                    // 반격 본능 (랜스 가드)
        StopAttack();
        ResetLiberatedSwing();
        guarding = true;
        parriedThisGuard = false;
        guardEndTime = Time.time + Settings.lance.parryWindow;
        ApplyShieldForm();
    }

    private void EndGuard()
    {
        guarding = false;
        SpriteFx.StopAura(guardAura);
        guardAura = null;
        guardReadyTime = Time.time + (TestMode.NoSkillCooldown ? 0f : Settings.lance.parryCooldown);   // 테스트 모드 쿨타임 없음
        if (!parriedThisGuard)
        {
            ResetStacks();                                   // 헛방어: 철벽 스택 초기화
        }
        ApplyLanceForm();
        transform.localRotation = UpTowards(AimAngle);
    }

    // 철벽 스택 증가 (최대 maxStacks)
    private void AddStacks(int amount)
    {
        int before = ironStacks;
        ironStacks = Mathf.Clamp(ironStacks + amount, 0, Settings.lance.maxStacks);
        if (stackHud != null && ironStacks > before)
        {
            stackHud.Pulse(Settings.lance.stackGainColor, false);
        }
    }

    private void ResetStacks()
    {
        if (ironStacks <= 0)
            return;
        ironStacks = 0;
        if (stackHud != null)
        {
            stackHud.Pulse(Settings.lance.stackResetColor, true);
        }
    }

    // 랜스 크기: 「무기·총알 크기」는 창일 때만 (패링 방패는 크기 그대로: 막는 범위가 헷갈리지 않게)
    // 창 ↔ 방패 그림을 바꾸는 순간에도 바로 불러 한 프레임이라도 큰 방패가 보이지 않게 함
    private void ApplyLanceScale()
    {
        if (lanceTransform == null || lanceRenderer == null)
            return;
        float size = guarding ? 1f : WeaponSizeScale;
        float pop = 1f + 0.3f * lancePop;
        if (lanceRenderer.transform == lanceTransform)
        {
            lanceTransform.localScale = lanceBaseScale * size * pop;
        }
        else
        {
            lanceTransform.localScale = lanceRootScale * size;
            lanceRenderer.transform.localScale = lanceBaseScale * pop;
        }
    }

    // 손에 든 무기 표시: 패링 중 방패, 대검 해방 중 대검, 투창 직후 잠깐 빈손 → 새 창이 튀어나오듯 나타남
    private void UpdateHeldLance()
    {
        if (lanceRenderer == null)
            return;

        bool show = guarding || (!gsHeld && Time.time >= lanceHiddenUntil);
        if (show && !lanceRenderer.enabled && !guarding)
        {
            lancePop = 1f;
        }
        lanceRenderer.enabled = show;
        lancePop = Mathf.MoveTowards(lancePop, 0f, Time.deltaTime / 0.2f);
        ApplyLanceScale();

        if (gsRoot != null && gsRoot.activeSelf != (gsHeld && !guarding))
        {
            gsRoot.SetActive(gsHeld && !guarding);
        }
        if (gsBlade != null && !gsSwinging)
        {
            SetBladeEdge(aimDir.x < 0f);                     // 들고 있을 때는 왼쪽을 보면 좌우 반전 (날이 아래로)
        }
    }

    // ---------------------------------------------------------------
    // 랜스 전용 스킬 「강철의 영역」
    // R → 위치 지정 → 기 모으기 → 투창 → dropDelay초 뒤 거대한 창이 꽂힘 → 영역 유지 + 파장
    // ---------------------------------------------------------------
    // 스택 비율 (0스택 0 → 최대 스택 1, 선형)
    private float StackRate(int stacks)
    {
        int max = Settings.lance.maxStacks;
        return max > 0 ? Mathf.Clamp01(stacks / (float)max) : 0f;
    }

    // 마우스 위치를 사거리 안으로
    private Vector3 ZoneAimPoint()
    {
        Vector3 center = player.transform.position;
        Vector3 offset = mouseWorld - center;
        offset.z = 0f;
        return center + Vector3.ClampMagnitude(offset, Settings.lance.zoneRange);
    }

    private void BeginZoneTargeting()
    {
        if (zoneIndicator == null)
        {
            Sprite sprite = SpriteFx.RainIndicatorSprite;   // 화살비와 같은 조준경
            zoneIndicator = CreateVisual("Steel Zone Indicator", sprite != null ? sprite : circleSprite, null, 5);
            zoneIndicatorFx = sprite != null;
        }
        zoneTargeting = true;
        zoneIndicator.gameObject.SetActive(true);
        UpdateZoneIndicator(ZoneAimPoint());
    }

    // 범위 표시: 지금 스택의 영역 크기로
    private void UpdateZoneIndicator(Vector3 position)
    {
        LanceMainSettings s = Settings.lance;
        zoneIndicator.transform.position = position;
        SetWorldDiameter(zoneIndicator, s.zoneRadius * Mathf.Lerp(1f, s.maxSizeMul, StackRate(ironStacks)) * 2f);
        zoneIndicator.color = zoneIndicatorFx ? new Color(1f, 1f, 1f, 0.7f + 0.2f * Mathf.Sin(Time.time * 8f)) : s.zoneColor;
        zoneIndicator.sortingLayerID = zoneSortingLayer;
        zoneIndicator.sortingOrder = s.zoneSortingOrder;
    }

    private void UpdateZoneTargeting()
    {
        Vector3 point = ZoneAimPoint();
        UpdateZoneIndicator(point);

        if (mouseClicked)
        {
            zoneTargeting = false;
            mouseHoldValid = false;                          // 위치 지정 클릭으로 찌르기가 나가지 않도록
            zoneTarget = point;
            zoneCharging = true;
            zoneChargeEnd = Time.time + Settings.lance.chargeTime;
            nextZoneSpark = 0f;
            if (!guarding)
            {
                StopAttack();
                ResetLiberatedSwing();
                ApplyLanceForm();
            }
            zoneChargeAura = SpriteFx.ChargeAura(player.transform, 1.6f);   // 발밑 바람 + 흙먼지
            CameraShake.Weak();
            RunUI.PlayOr(RunConfig.Instance.skillActivateSound, AudioManager.Sfx.Weapon_ChargeMod);   // 스킬 발동 소리
        }
        else if (rightClicked || SkillPressed)
        {
            zoneTargeting = false;                           // 취소 (쿨타임 없음)
            zoneIndicator.gameObject.SetActive(false);
        }
    }

    // 기 모으기: 창끝으로 빛이 모여들고 창이 금빛으로 맥동
    private void UpdateZoneCharge()
    {
        LanceMainSettings s = Settings.lance;
        UpdateZoneIndicator(zoneTarget);
        if (!guarding)
        {
            transform.localRotation = UpTowards(AimAngle);
        }

        if (Time.time >= nextZoneSpark)
        {
            nextZoneSpark = Time.time + 0.3f;
            Vector3 tip = transform.position - player.transform.position + aimDir * 1.8f;
            SpriteFx.BeamCharge(player.transform, tip, 0.3f);
        }
        if (lanceRenderer != null && !guarding)
        {
            float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            lanceRenderer.color = Color.Lerp(Color.white, s.chargeGlowColor, glow);
        }

        if (Time.time >= zoneChargeEnd && !guarding)
        {
            ThrowSpear();
        }
    }

    // 기존 창을 하늘로 던짐 → 새 창(각성: 대검)을 꺼내 듦. 투창 순간의 스택으로 위력 결정
    private void ThrowSpear()
    {
        LanceMainSettings s = Settings.lance;
        zoneCharging = false;
        if (zoneIndicator != null) zoneIndicator.gameObject.SetActive(false);
        SpriteFx.StopAura(zoneChargeAura);
        zoneChargeAura = null;
        if (lanceRenderer != null) lanceRenderer.color = Color.white;

        int stacks = ironStacks;
        ironStacks = 0;                                       // 사용 시 철벽 스택 전부 소모
        if (stackHud != null && stacks > 0)
        {
            stackHud.Pulse(Color.white, false);
        }
        StartSkillCooldown(data.zoneCooldown);

        StartCoroutine(ThrownLanceFx());
        StartCoroutine(SteelZone(zoneTarget, StackRate(stacks)));
        lanceHiddenUntil = Time.time + s.redrawTime;          // 잠깐 빈손 → 새 창을 꺼내 듦
        if (awakening)
        {
            BeginLiberation(s.dropDelay + s.zoneDuration);   // 각성 「대검 해방」: 투창부터 영역 종료까지 대검
        }
        CameraShake.Weak();
        RunUI.PlayOr(s.throwSound, AudioManager.Sfx.Weapon_Melee);
    }

    // 던진 창: 창끝이 하늘을 향하며 위로 빠르게 날아가 사라짐
    private IEnumerator ThrownLanceFx()
    {
        if (lanceRenderer == null || lanceRenderer.sprite == null)
            yield break;

        SpriteRenderer thrown = CreateVisual("Thrown Lance", lanceRenderer.sprite, null, 3);
        zoneObjects.Add(thrown.gameObject);
        Transform t = thrown.transform;
        t.position = lanceRenderer.transform.position;
        t.rotation = lanceRenderer.transform.rotation;
        t.localScale = lanceRenderer.transform.lossyScale;
        Quaternion from = t.rotation;
        Vector3 start = t.position;
        float side = aimDir.x < 0f ? 1f : -1f;              // 보는 쪽으로 기울임 (z 회전 +는 왼쪽)
        Quaternion to = Quaternion.Euler(0f, 0f, side * Settings.lance.throwAngle);
        Vector3 flight = to * Vector3.up;
        SpriteFx.BoostSpark(start, 0.8f);

        const float time = 0.4f;
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / time);
            t.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(p * 4f));   // 창 그림은 위를 향함
            t.position = start + flight * (Settings.lance.throwDistance * p * p + p);
            thrown.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((p - 0.6f) / 0.4f));
            yield return null;
        }
        zoneObjects.Remove(thrown.gameObject);
        Destroy(thrown.gameObject);
    }

    // 꽂힐 자리 미리 표시 → 거대한 창 낙하 → 꽂힘 피해 → zoneDuration초 동안 waveInterval초마다 파장
    private IEnumerator SteelZone(Vector3 center, float rate)
    {
        LanceMainSettings s = Settings.lance;
        float sizeMul = Mathf.Lerp(1f, s.maxSizeMul, rate);
        float damageMul = Mathf.Lerp(1f, s.maxDamageMul, rate);
        float radius = s.zoneRadius * sizeMul;
        float baseDamage = MainDamage * ExtraCards.WeaponSkillScale;
        Color c = s.zoneColor;

        SpriteRenderer area = CreateVisual("Steel Zone", circleSprite, null, 4);
        zoneObjects.Add(area.gameObject);
        area.transform.position = center;
        area.sortingLayerID = zoneSortingLayer;              // 몬스터보다 아래
        area.sortingOrder = s.zoneSortingOrder;
        SetWorldDiameter(area, radius * 2f);

        float impactTime = Time.time + s.dropDelay;
        float fallStart = impactTime - Mathf.Min(s.spearFallTime, s.dropDelay);
        while (Time.time < fallStart)
        {
            area.color = new Color(c.r, c.g, c.b, c.a * (0.55f + 0.3f * Mathf.Sin(Time.time * 9f)));   // 옅게 깜빡임
            yield return null;
        }

        // 거대한 창 (창끝이 아래로)
        Sprite spearSprite = GetLanceImage(2) != null ? GetLanceImage(2) : GetLanceImage(0);
        float length = s.spearLength * sizeMul;
        Vector3 planted = center + Vector3.up * (length * (0.5f - s.spearSinkRatio));   // 창끝이 땅속 sink 비율만큼
        SpriteRenderer spear = null;
        if (spearSprite != null)
        {
            spear = CreateVisual("Giant Spear", spearSprite, null, 3);
            zoneObjects.Add(spear.gameObject);
            spear.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            spear.transform.localScale = Vector3.one * (length / Mathf.Max(0.01f, spearSprite.bounds.size.y));
            spear.transform.position = planted + Vector3.up * s.spearFallHeight;
        }
        while (Time.time < impactTime)
        {
            float p = 1f - (impactTime - Time.time) / Mathf.Max(0.01f, impactTime - fallStart);
            if (spear != null) spear.transform.position = planted + Vector3.up * (s.spearFallHeight * (1f - p * p));
            area.color = new Color(c.r, c.g, c.b, c.a * 0.85f);
            yield return null;
        }
        Sprite buried = null;
        if (spear != null)
        {
            // 땅 위로 나온 부분만 남긴 그림 (잘린 면이 땅, 창 그림은 위가 창끝이라 위쪽을 잘라냄)
            Rect r = spearSprite.rect;
            float keep = r.height * (1f - s.spearSinkRatio);
            buried = Sprite.Create(spearSprite.texture, new Rect(r.x, r.y, r.width, keep), new Vector2(0.5f, 1f), spearSprite.pixelsPerUnit);
            spear.sprite = buried;
            spear.transform.position = center;
            SpriteFx.RainEdgeDust(center, 0.4f);               // 박힌 자리 흙먼지
        }

        // 꽂히는 순간 큰 피해 (보스·중간보스 1회 피해는 160 이하)
        float impact = baseDamage * s.impactDamageMultiplier * damageMul;
        CombatUtil.DamageInCircleSplit(center, radius, impact, impact, GimmickCatalog.Instance.knockLanceZone);
        SpriteFx.StarBurst(center, radius * 1.2f);
        SynergyAwakening.ShockRing(center, radius * 2.6f, 0.4f);
        StartCoroutine(FlashCircle(center, radius * 2f, s.zoneWaveColor, 0.35f));
        CameraShake.Weak();
        RunUI.PlayOr(s.impactSound, AudioManager.Sfx.Weapon_GreatSword);

        // 영역 유지 + 파장
        float wave = baseDamage * s.waveDamageMultiplier * damageMul;
        float start = Time.time;
        int waves = Mathf.FloorToInt(s.zoneDuration / s.waveInterval + 0.001f);
        int fired = 0;
        while (Time.time < start + s.zoneDuration || fired < waves)
        {
            if (fired < waves && Time.time >= start + s.waveInterval * (fired + 1))
            {
                fired++;
                CombatUtil.DamageInCircleSplit(center, radius, wave, wave, GimmickCatalog.Instance.knockLanceZone);
                SynergyAwakening.ShockRing(center, radius * 2f, 0.45f);
                StartCoroutine(FlashCircle(center, radius * 2f, s.zoneWaveColor, 0.4f));
                RunUI.PlayOr(s.waveSound, AudioManager.Sfx.Weapon_GreatSword);
            }
            area.color = new Color(c.r, c.g, c.b, c.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 4f)));
            yield return null;
        }

        // 사라짐
        const float fade = 0.35f;
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(elapsed / fade);
            area.color = new Color(c.r, c.g, c.b, c.a * a);
            if (spear != null) spear.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        zoneObjects.Remove(area.gameObject);
        Destroy(area.gameObject);
        if (spear != null)
        {
            zoneObjects.Remove(spear.gameObject);
            Destroy(spear.gameObject);
        }
        if (buried != null) Destroy(buried);
    }

    // ---------------------------------------------------------------
    // 랜스 각성 「대검 해방」: 투창 후 영역이 끝날 때까지 새 창 대신 대검 (보조 대검은 숨김)
    // ---------------------------------------------------------------
    private Weapon FindSubWeapon(int id)
    {
        foreach (Weapon w in player.GetComponentsInChildren<Weapon>(true))
        {
            if (w.id == id && !w.IsMain)
                return w;
        }
        return null;
    }

    // 기본 피해 = 메인무기 대검 기본 공격 피해 (대검 레벨 피해 x 대검 기본 공격 배율)
    private float LiberatedDamage => (partnerSword != null ? partnerSword.BaseDamage : MainDamage) * Settings.greatSword.basicDamageMultiplier;

    // 공격 속도: 메인무기 대검과 같은 계산 (대검 회전 속도 성장 + 공격속도 증가)
    private float LiberatedSpeed
    {
        get
        {
            float factor = Character.WeaponSpeed * (1f + Gear.stamina_stat);
            if (partnerSword != null && partnerSword.Data != null && partnerSword.Data.baseSpeed > 0f)
            {
                factor = partnerSword.speed / partnerSword.Data.baseSpeed;
            }
            return Mathf.Clamp(factor * PlayerBuff.Stamina, 0.1f, Settings.attackSpeedMax);   // 상한 2배
        }
    }

    private void BeginLiberation(float duration)
    {
        gsHeldUntil = Mathf.Max(gsHeldUntil, Time.time + duration);
        if (gsHeld)
            return;

        Weapon partner = FindSubWeapon(3);
        if (partner == null || !CreateLiberatedSword(partner))
            return;                                          // 대검을 못 찾으면 그대로 새 창

        partnerSword = partner;
        gsHeld = true;
        gsStep = 0;
        partner.gameObject.SetActive(false);                 // 보조 대검은 화면에서 사라짐
        SpriteFx.BoostSpark(player.transform.position + Vector3.up * 0.8f, 1f);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_GreatSword);
    }

    // 손에 드는 대검: 메인무기 대검 그림, 보조 대검과 같은 거리·크기, 관통 무한 근접 판정
    private bool CreateLiberatedSword(Weapon partner)
    {
        SpriteRenderer source = partner.GetComponentInChildren<SpriteRenderer>(true);
        Sprite sprite = partner.Data != null && partner.Data.weaponimage != null && partner.Data.weaponimage.Length > 0 ? partner.Data.weaponimage[0] : null;
        if (sprite == null && source != null)
        {
            sprite = source.sprite;
        }
        if (sprite == null)
            return false;

        gsRoot = new GameObject("Liberated Sword");
        gsRoot.transform.SetParent(transform, false);
        gsPivot = gsRoot.transform;

        SpriteRenderer blade = CreateVisual("Blade", sprite, gsPivot, 1);
        Vector3 parentScale = gsPivot.lossyScale;
        if (source != null)
        {
            blade.sharedMaterial = source.sharedMaterial;
            blade.sortingLayerID = source.sortingLayerID;
            blade.sortingOrder = source.sortingOrder;
            Vector3 scale = source.transform.lossyScale;
            blade.transform.localScale = new Vector3(Mathf.Abs(scale.x / parentScale.x), Mathf.Abs(scale.y / parentScale.y), 1f);
        }
        // 랜스를 쥐는 자리처럼 손잡이 끝이 캐릭터 몸통에 오도록 (그림 아래쪽이 손잡이)
        float half = (sprite.bounds.extents.y - sprite.bounds.center.y) * Mathf.Abs(blade.transform.lossyScale.y);
        float hold = Mathf.Max(LanceBase.y, half - 0.15f);
        blade.transform.localPosition = Vector3.up * (hold / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)));
        gsReach = hold + half;

        GameObject go = blade.gameObject;
        go.tag = "Bullet";
        go.layer = hitLayer;
        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = sprite.bounds.size;
        box.offset = sprite.bounds.center;
        gsBlade = blade;
        gsBox = box;
        gsBoxOffsetX = sprite.bounds.center.x;
        gsHit = go.AddComponent<Bullet>();
        gsHit.Init(LiberatedDamage, -100, Vector3.zero);     // 관통 무한
        gsHit.IsHarmless = true;
        return true;
    }

    private void EndLiberation()
    {
        gsHeld = false;
        if (gsSwinging)
        {
            StopAttack();
        }
        ResetLiberatedSwing();
        if (gsRoot != null) Destroy(gsRoot);
        gsRoot = null;
        gsPivot = null;
        gsHit = null;
        gsBlade = null;
        gsBox = null;
        if (partnerSword != null)
        {
            partnerSword.gameObject.SetActive(true);         // 보조 대검 다시 나타나 원래대로
        }
        partnerSword = null;
        if (!guarding)
        {
            ApplyLanceForm();
        }
        SpriteFx.BoostSpark(player.transform.position + Vector3.up * 0.8f, 0.8f);
    }

    // 대검 그림 좌우 반전 (그림은 오른쪽이 날). 판정 위치도 함께 반전
    private void SetBladeEdge(bool flip)
    {
        if (gsBlade == null)
            return;
        gsBlade.flipX = flip;
        if (gsBox != null)
        {
            gsBox.offset = new Vector2(flip ? -gsBoxOffsetX : gsBoxOffsetX, gsBox.offset.y);
        }
    }

    // 휘두르던 대검을 멈추고 제자리로
    private void ResetLiberatedSwing()
    {
        gsSwinging = false;
        if (gsHit != null) gsHit.IsHarmless = true;
        if (gsPivot != null)
        {
            gsPivot.localRotation = Quaternion.identity;
            gsPivot.localScale = Vector3.one;
        }
    }

    // 대검 3단 연계: 1. 왼쪽 → 오른쪽 베기  2. 오른쪽 → 왼쪽 베기  3. 짧은 돌진 + 360도 회전 베기
    private IEnumerator LiberatedCombo()
    {
        LanceMainSettings s = Settings.lance;
        if (gsHit == null || gsPivot == null)
            yield break;
        if (Time.time - gsLastAttackTime > s.gsComboResetTime)
        {
            gsStep = 0;
        }
        int step = gsStep;
        gsStep = (gsStep + 1) % 3;
        float speed = LiberatedSpeed;
        Vector3 dir = aimDir;

        gsSwinging = true;
        transform.localRotation = UpTowards(AimAngle);
        SetHit(gsHit, LiberatedDamage, true);
        Rehit(gsHit);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_GreatSword);

        if (step < 2)
        {
            float half = s.gsSwingAngle * 0.5f;
            float from = step == 0 ? half : -half;           // 왼쪽(+) → 오른쪽(-), 2단은 반대
            SetBladeEdge(step == 1);                         // 베는 쪽으로 날이 앞장서게 (1단 시계 방향 = 오른쪽 날, 2단 반대)
            float duration = s.gsSwingTime / speed;
            float range = s.gsSwingRangeMul;
            gsPivot.localScale = Vector3.one * range;            // 대검 길이·폭(판정 포함)을 늘려 베기 범위 증가
            SpriteFx.GreatSlash(player.transform.position + dir * gsReach * 0.55f * range, dir, Mathf.Max(2.4f, gsReach * 1.1f) * range);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                p = 1f - (1f - p) * (1f - p);                // 처음에 빠르게
                gsPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, -from, p));
                yield return null;
            }
        }
        else
        {
            SetBladeEdge(false);                             // 시계 방향 회전 = 오른쪽 날이 앞장
            player.ForceMove(dir, s.gsDashDistance, s.gsDashTime, s.gsDashInvincible);   // 돌진하는 동안만 무적
            float spin = s.gsSpinSpeed * speed;
            float done = 0f;
            while (done < 360f)
            {
                float angle = Mathf.Min(spin * Time.deltaTime, 360f - done);
                gsPivot.Rotate(Vector3.back * angle);
                done += angle;
                yield return null;
            }
            SpriteFx.GreatSlash(player.transform.position + dir * gsReach * 0.5f, dir, Mathf.Max(2.6f, gsReach * 1.2f));
        }

        ResetLiberatedSwing();
        gsLastAttackTime = Time.time;
        attackReadyTime = Time.time + (step == 2 ? data.gsComboCooldown * CooldownFactor : s.gsStepInterval / speed);   // 단계 사이 = 공격속도, 3단 후 = 쿨감
    }

    // ---------------------------------------------------------------
    // 시너지 공명 효과
    // ---------------------------------------------------------------
    private const int UnlimitedPierce = 99999;               // 관통 무제한 (-1은 화살 속도가 지정되지 않아 큰 값 사용)

    // 짝 무기(보조)의 발사체 프리팹 번호 (보조 무기를 그대로 재사용)
    private static int PartnerPrefab(int weaponId)
    {
        foreach (Weapon w in GameManager.instance.player.GetComponentsInChildren<Weapon>(true))
        {
            if (w.id == weaponId)
                return w.prefabId;
        }
        return -1;
    }

    // 짝 무기의 현재 피해량 (없으면 메인 무기 피해량)
    private float PartnerDamage(int weaponId)
    {
        foreach (Weapon w in GameManager.instance.player.GetComponentsInChildren<Weapon>(true))
        {
            if (w.id == weaponId && !w.IsMain)
                return w.damage;
        }
        return MainDamage;
    }

    // 풀에서 꺼낸 보조 무기 발사체를 다 쓰고 돌려놓음
    private static void ReturnToPool(Transform blade)
    {
        if (blade == null)
            return;
        blade.gameObject.SetActive(false);
        blade.SetParent(GameManager.instance.pool.transform, false);
    }

    // 헤비보우건+쌍검 「칼날 호위」: 보조 쌍검 배치 방식 그대로 칼날을 플레이어 주위에 고르게 두고 회전
    private GameObject guardPivot;
    private readonly List<Transform> guardBlades = new List<Transform>();
    private readonly List<SpriteFxAnimator> guardWinds = new List<SpriteFxAnimator>();

    private void StartGuardBlades()
    {
        StopGuardBlades();
        int prefab = PartnerPrefab(0);
        if (prefab < 0)
            return;

        WeaponSynergyData d = WeaponSynergy.Data;
        guardPivot = new GameObject("Guard Blades");
        guardPivot.transform.SetParent(player.transform, false);
        guardPivot.AddComponent<GuardBladeSpin>().speed = d.guardBladeSpeed;
        SpriteFxAnimator swirl = SpriteFx.GuardWind(guardPivot.transform, d.guardBladeDistance * 2.6f);   // 몸 주위를 도는 회오리 바람
        if (swirl != null) guardWinds.Add(swirl);

        float damage = PartnerDamage(0);
        for (int index = 0; index < d.guardBladeCount; index++)
        {
            Transform blade = GameManager.instance.pool.Get(prefab).transform;
            blade.SetParent(guardPivot.transform, false);
            blade.localPosition = Vector3.zero;
            blade.localRotation = Quaternion.identity;
            blade.Rotate(Vector3.forward * 360f * index / d.guardBladeCount);
            blade.Translate(blade.up * d.guardBladeDistance, Space.World);
            Bullet hit = blade.GetComponent<Bullet>();
            hit.Init(damage, -100, Vector3.zero);           // 잡몹 피해 + 넉백 (적 피격 처리), 보스 탄환은 막지 않음
            hit.IsHarmless = false;
            foreach (SpriteRenderer r in blade.GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;   // 칼 그림 대신 칼바람
            SpriteFxAnimator wind = SpriteFx.GuardWindBlade(blade, d.guardBladeDistance * 0.9f);
            if (wind != null) guardWinds.Add(wind);
            guardBlades.Add(blade);
        }
    }

    private void StopGuardBlades()
    {
        foreach (SpriteFxAnimator w in guardWinds)
        {
            if (w != null) Destroy(w.gameObject);
        }
        guardWinds.Clear();
        foreach (Transform blade in guardBlades)
        {
            if (blade == null) continue;
            foreach (SpriteRenderer r in blade.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = true;   // 풀에 돌려줄 때 원래대로
            ReturnToPool(blade);
        }
        guardBlades.Clear();
        if (guardPivot != null)
        {
            Destroy(guardPivot);
            guardPivot = null;
        }
    }

    // 쌍검+활 「추격의 깃털」: 활 화살 스프라이트를 축소한 작은 화살 (사거리 제한, 1명 적중)
    private Sprite featherSprite;

    private void FireFeather(Vector3 dir, float slashDamage)
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        if (featherSprite == null)
        {
            int prefab = PartnerPrefab(1);
            GameObject arrowPrefab = prefab >= 0 ? GameManager.instance.pool.prefabs[prefab] : null;
            SpriteRenderer arrowRenderer = arrowPrefab != null ? arrowPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            featherSprite = arrowRenderer != null ? arrowRenderer.sprite : null;
            if (featherSprite == null)
                return;
        }

        dir = ((Vector2)dir).sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
        SpriteRenderer feather = CreateVisual("Feather Arrow", featherSprite, null, 2);
        GameObject go = feather.gameObject;
        go.tag = "Bullet";
        go.layer = hitLayer;
        go.transform.position = player.transform.position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        go.transform.localScale = Vector3.one * d.arrowScale;

        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = featherSprite.bounds.size;

        Bullet bullet = go.AddComponent<Bullet>();
        bullet.Init(slashDamage * d.arrowDamageRate, 0, Vector3.zero);   // 1명 적중 후 사라짐
        rigid.velocity = dir * d.arrowSpeed;
        Destroy(go, d.arrowRange / Mathf.Max(0.1f, d.arrowSpeed));    // 사거리
    }

    // ---------------------------------------------------------------
    // 시너지 각성 효과 (보스 1회 피해는 SynergyAwakening.DamageBoss에서 160 이하)
    // ---------------------------------------------------------------
    // 대검+랜스 「천지 가르기」: 시간이 멈춘 사이 화면을 대각선으로 가르는 금빛 베기 2줄 (effect_3)
    // → 정지가 풀리며 흰 번쩍임과 함께 판정 (잡몹 즉사 + 보스 2연타)
    private IEnumerator HeavenSplit()
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        float previousScale = Time.timeScale;
        Time.timeScale = 0f;
        SynergyAwakening.PlaySound(d.cleaveSound, AudioManager.Sfx.Weapon_GreatSword);

        Rect screen = SynergyAwakening.ScreenRect();
        Vector3 c = screen.center;
        float reach = Mathf.Sqrt(screen.width * screen.width + screen.height * screen.height) * 0.6f;
        Vector3 dir1 = new Vector3(1f, 0.55f, 0f).normalized;
        Vector3 dir2 = new Vector3(1f, -0.45f, 0f).normalized;
        float cutTime = d.freezeTime + 0.3f;
        Vector3 c1 = c + Vector3.up * screen.height * 0.08f;
        Vector3 c2 = c + Vector3.down * screen.height * 0.1f;
        Vector3[] cutStart = { c1 - dir1 * reach, c2 - dir2 * reach };
        Vector3[] cutEnd = { c1 + dir1 * reach, c2 + dir2 * reach };
        SpriteFx.ScreenCut(cutStart[0], cutEnd[0], 0.9f, cutTime);   // 얇은 금빛 베기
        yield return new WaitForSecondsRealtime(d.freezeTime * 0.35f);
        SpriteFx.ScreenCut(cutStart[1], cutEnd[1], 0.9f, cutTime);
        yield return new WaitForSecondsRealtime(d.freezeTime * 0.65f);

        // 실제 화면도 두 베기 선을 따라 잘려 어긋났다가 다시 붙음 (정지된 화면을 찍어 4조각으로)
        ScreenSlice.Play(cutStart, cutEnd, 0.25f);
        yield return null;                                   // 이번 프레임(정지 화면)을 찍은 뒤 정지 해제

        if (Time.timeScale == 0f && GameManager.instance.isLive)
        {
            Time.timeScale = previousScale > 0f ? previousScale : 1f;
        }

        SynergyAwakening.ScreenFlash(d.awakenFlashTime);
        CameraShake.Strong();

        // 잘린 화면(정지 화면 사진)이 다시 붙을 때까지 기다린 뒤 몬스터가 두 동강 (사진에 가려 안 보이던 문제)
        yield return new WaitForSecondsRealtime(ScreenSlice.TotalTime(0.25f));
        Enemy.SliceKill = true;                                 // 이 피해로 쓰러진 몬스터는 두 동강 (숫자는 실제 피해)
        Enemy.SliceDuration = d.sliceKillDuration;
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            if (SynergyAwakening.OnScreen(enemy.transform.position, 0.3f))
            {
                enemy.TakeDamage(GimmickCards.SynergyDamage(d.cleaveBossDamage * 2f, enemy.HuntKind));   // 중간보스·보스와 같은 피해, 피해 증가만
            }
        }
        Enemy.SliceKill = false;
        CameraShake.Weak();
        SynergyAwakening.DamageMiniBossesOnScreen(d.cleaveBossDamage * 2f);   // 모두 한 대 (일반·중간보스·보스 같은 피해)
        BossPattern boss = BossPattern.Current;
        if (boss != null && boss.isActiveAndEnabled && SynergyAwakening.OnScreen(boss.transform.position, 2f))
        {
            SpriteFx.StarBurst(boss.transform.position, 5f);
            SynergyAwakening.DamageBoss(boss, d.cleaveBossDamage * 2f);   // 한 대 (2연타 없음)
        }
    }

    // 활+헤비보우건 「관통 섬광」: 활 끝에 빛을 모은 뒤 거대 관통 화살 (굵은 빛 궤적 + 빛 입자 꼬리 + 지나간 자리 빛줄기)
    private void FirePiercingFlash(Vector3 dir)
    {
        if (arrowSprite == null)
            return;
        dir = ((Vector2)dir).sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
        StartCoroutine(PiercingFlashRoutine(dir));
    }

    private IEnumerator PiercingFlashRoutine(Vector3 dir)
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        SpriteFx.BeamCharge(player.transform, dir * 0.9f, d.beamChargeTime);   // 활 끝에 빛이 모임 (effect_3 빛 폭발이 커짐)
        yield return new WaitForSeconds(d.beamChargeTime);

        Vector3 start = player.transform.position + dir * 0.9f;
        SynergyProjectile beam = SynergyProjectile.Spawn("Piercing Flash", arrowSprite, start, dir * d.beamSpeed,
            d.beamLength, -90f, d.beamHitDamage, 0, hitLayer, playerSprite);   // 화살 그림은 위를 향함
        beam.bossHits = d.beamHitCount;
        beam.bossHitSparks = true;                           // 다단히트마다 타격 불꽃
        beam.sparkTrail = true;                              // 빛 입자 꼬리
        beam.GetComponent<SpriteRenderer>().color = new Color(1f, 0.95f, 0.7f, 1f);
        SpriteFx.AttachArrowStreak(beam.transform, dir, d.beamLength);   // 화살 뒤로 뻗는 빛 줄기 (effect_2)

        SpriteFx.StarBurst(start, 1.6f);
        SynergyAwakening.PlaySound(d.beamSound, AudioManager.Sfx.Weapon_Sniper);
        CameraShake.Strong();

        // 지나간 자리: 화살을 따라 늘어나는 속도선 줄기(effect_2) → beamAfterTime 동안 사라짐
        StreakLine wake = SpriteFx.Streak(start, start, 0.9f, new Color(1f, 0.95f, 0.75f, 0.85f));
        while (beam != null)
        {
            wake.SetEnd(beam.transform.position);
            yield return null;
        }
        wake.Fade(d.beamAfterTime);
    }

    // 헤비보우건+쌍검 공명 「칼날 연사」: 칼날(검풍)을 사방으로 → 가장 가까운 몬스터에게 유도, stormPierce회 관통 후 소멸
    private void LaunchBladeStorm()
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        Sprite bladeSprite = DualBladeSprite();
        Sprite wind = SpriteFx.WindBladeSprite;                 // 칼 대신 날카로운 검풍
        if (bladeSprite == null && wind == null)
            return;

        for (int i = 0; i < d.stormBladeCount; i++)
        {
            float a = 360f * i / d.stormBladeCount + 15f;
            Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            SynergyProjectile blade = SynergyProjectile.Spawn("Storm Blade", wind != null ? wind : bladeSprite, player.transform.position + (Vector3)(dir * 0.8f), dir * d.stormBladeSpeed,
                1.6f, wind != null ? 0f : -90f, d.stormBladeDamage, 0, hitLayer, playerSprite);   // 검풍은 오른쪽, 칼날 그림은 위를 향함
            blade.homing = true;
            blade.turnSpeed = d.stormTurnSpeed;
            blade.maxHits = d.stormPierce + 1;               // 유도 대상 포함 최대 pierce+1마리 (보스는 1자루당 1회)
        }
        SpriteFx.DualWhirl(player.transform, 3f);
        CameraShake.Strong();
        SynergyAwakening.PlaySound(d.bladeStormSound, AudioManager.Sfx.Weapon_Melee);
    }

    // 쌍검+활 「섬광 돌격」: 시작점~끝점을 잇는 번개 줄기 하나(effect_2) → slashDelay초 뒤 줄기 전체가 함께 터짐 (경로 위 적에게 1회)
    private IEnumerator FlashDashExplode(Vector3 start, Vector3 end)
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        StreakLine line = SpriteFx.Streak(start, end, d.dashExplodeWidth, new Color(0.8f, 0.92f, 1f, 0.9f));
        yield return new WaitForSeconds(d.slashDelay);

        line.Burst(0.3f);                                    // 줄기 전체가 굵어지며 번쩍인 뒤 사라짐
        SpriteFx.StarBurst(end, 2.2f);
        SynergyAwakening.HitLine(start, end, d.dashExplodeWidth, d.dashExplodeDamage);
        SynergyAwakening.PlaySound(d.flashDashSound, AudioManager.Sfx.Weapon_Sniper);
        CameraShake.Strong();
    }

    // 쌍검 각성: 돌진 충전 (최대 dualDashCharge회, 쿨타임마다 1회 회복)
    private int dashCharges = -1;

    private int MaxDashCharges => awakening ? Mathf.Max(1, WeaponSynergy.Data.dualDashCharge) : 1;

    private bool DashReady => awakening ? dashCharges > 0 : SkillReady;

    private void UpdateDashCharges(float cooldown)
    {
        if (!awakening)
            return;
        if (dashCharges < 0)
        {
            dashCharges = MaxDashCharges;                    // 각성 직후 가득 충전
        }
        if (dashCharges < MaxDashCharges && SkillReady)
        {
            dashCharges++;
            if (dashCharges < MaxDashCharges)
            {
                StartSkillCooldown(cooldown);               // 다음 1회 충전
            }
        }
    }

    private void UseDashCharge(float cooldown)
    {
        if (!awakening)
        {
            StartSkillCooldown(cooldown);
            return;
        }
        bool wasFull = dashCharges >= MaxDashCharges;
        dashCharges = Mathf.Max(0, dashCharges - 1);
        if (wasFull || SkillReady)
        {
            StartSkillCooldown(cooldown);                    // 충전 중이 아니었으면 충전 시작
        }
    }

    // 테스트 모드: 지금 메인 무기의 각성 효과를 바로 발동 (조건과 관계없이 확인용)
    public void TestAwakening()
    {
        if (!awakening)
            return;
        switch (weapon.id)
        {
            case 0: BeginAttack(DualBladeDash()); break;
            case 1: FirePiercingFlash(aimDir); break;
            case 2: LaunchBladeStorm(); break;
            case 3: StartCoroutine(HeavenSplit()); break;
            case 4: zoneTarget = ZoneAimPoint(); ThrowSpear(); break;
        }
    }

    // 랜스 패링: parryWindow 안에 공격을 받으면 피해 무효 + 주위 광역 피해, 철벽 스택 획득 (보스 접촉·패턴은 더 많이)
    public bool TryParry(bool fromBoss)
    {
        if (!guarding || weapon.id != 4)
            return false;

        LanceMainSettings s = Settings.lance;
        Vector3 center = transform.position;
        CombatUtil.DamageInCircle(center, s.parryRadius, MainDamage * s.parryDamageMultiplier, true, false, GimmickCatalog.Instance.knockLanceParry);
        StartCoroutine(FlashCircle(center, s.parryRadius * 2f, s.parryColor, 0.25f));
        SpriteFx.Parry(center, s.parryRadius * 2f);             // 방패 + 충격 별

        player.GrantInvincibility(s.parryInvincibleTime);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Melee);

        parriedThisGuard = true;
        AddStacks(fromBoss ? s.bossParryStacks : s.mobParryStacks);

        if (resonance)
        {
            Weapon partner = FindSubWeapon(3);               // 공명 「대검 공명」: 보조 대검이 1회 회전 (숨긴 동안은 없음)
            if (partner != null)
            {
                partner.RequestBonusSpin();
            }
        }

        EndGuard();
        return true;
    }
}
