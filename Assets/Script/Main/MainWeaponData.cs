using UnityEngine;

// 메인무기 전용 데이터 (무기별 1개, Resources/MainWeapon/MainWeapon_{번호})
// 보조무기 레벨 표(damages, W_Speeds, W_Rates, counts)와 분리된 메인무기 수치.
// 값은 메인무기 전환 직후 실제로 쓰이던 값(캐릭터 배율 1.0 = 아이루 기준, 카드·상점·젬 0)을 그대로 옮긴 것.
// 실행 중에는 캐릭터 배율(공격력·공격속도·쿨타임)과 공통 능력치·버프·기믹, 상한·하한을 곱해서 씀.
[CreateAssetMenu(fileName = "MainWeapon", menuName = "Scriptble Object/MainWeaponData")]
public class MainWeaponData : ScriptableObject
{
    [Tooltip("0 쌍검 / 1 활 / 2 헤비보우건 / 3 대검 / 4 랜스")]
    public int weaponId;

    [Header("# 공통")]
    [Min(0f)] public float baseDamage = 10f;                 // 기본 피해 (공격별 배율은 메인무기 설정의 배율을 곱함). 헤비보우건은 탄별 피해 사용
    [Min(0.01f)] public float attackSpeedGrowth = 1f;        // 공격속도 성장 (1.0 = 아래 동작 시간에 이미 녹아 있음)
    [Min(0.01f)] public float cooldownGrowth = 1f;           // 쿨타임 성장 (1.0 = 아래 대기 시간에 이미 녹아 있음)

    [Header("# 쌍검")]
    [Min(0.01f)] public float slashTime = 0.09f;             // 1·2단 베기 동작 (공격속도)
    [Min(0f)] public float stepDelay = 0.1f;                 // 단계 사이 (공격속도)
    [Min(0.01f)] public float spinDuration = 0.5f;           // 3단 회전 동작 (공격속도)
    [Min(1f)] public float spinSpeed = 2160f;                // 3단 회전 속도 (도/초, 공격속도만큼 빨라짐)
    [Min(0f)] public float comboCooldown = 0.8f;             // 3연 후 다음 연계까지 (쿨타임 감소)
    [Min(0f)] public float dashCooldown = 6f;                // R 돌진 회전 베기 쿨타임

    [Header("# 활")]
    [Min(0.01f)] public float fireInterval = 0.315f;         // 화살 간격 (공격속도)
    [Min(0)] public int pierce = 1;                          // 기본 관통 (+ 관통 화살촉)
    [Min(0.01f)] public float fanChargeTime = 3f;            // 우클릭 부채꼴 충전 (쿨타임 감소)
    [Min(0f)] public float rainCooldown = 10f;               // R 화살비 쿨타임

    [Header("# 헤비보우건")]
    [Min(0f)] public float heavyDamage = 150f;               // 중량탄 피해 (x 캐릭터 공격력)
    [Min(0f)] public float rapidDamage = 10f;                // 경량탄 피해 (x 캐릭터 공격력)
    [Min(0.01f)] public float heavyInterval = 1f;            // 중량탄 발사 후 대기 (공격속도)
    [Min(0.1f)] public float rapidPerSecond = 10f;           // 경량탄 초당 연사 (공격속도)
    [Min(0.01f)] public float heavyRechargeTime = 3.25f;     // 중량탄 1발 충전 시간 (쿨타임 감소)
    [Min(0f)] public float rapidRechargePerSecond = 7.6923f; // 경량탄 초당 충전 (쿨타임 감소로 나눔, HANDOFF 버그 후보 참고)
    [Min(0)] public int rapidPierce = 0;                     // 경량탄 관통 (+ 관통 화살촉)
    [Min(0f)] public float sniperCooldown = 30f;             // R 정밀 저격 쿨타임
    [Min(0f)] public float sniperMobDamage = 500f;           // 정밀 저격 일반 몬스터 피해
    [Min(0f)] public float sniperBossDamage = 150f;          // 정밀 저격 보스 피해

    [Header("# 대검")]
    [Min(0.01f)] public float slamRaiseTime = 0.07f;         // 들어 올리기 (공격속도)
    [Min(0.01f)] public float slamStrikeTime = 0.04f;        // 내려찍기 (공격속도)
    [Min(0.01f)] public float sweepTime = 0.14f;             // 후리기 한 바퀴 (공격속도)
    [Min(0.01f)] public float upperSwingTime = 0.05f;        // 저스트 올려치기 동작 (공격속도)
    [Min(0f)] public float justRecoverTime = 0.1f;           // 저스트 성공 후 원위치 (쿨타임 감소)
    [Min(0f)] public float failRecoverTime = 0.25f;          // 저스트 실패 후 원위치 (쿨타임 감소)
    [Min(0f)] public float nextAttackDelay = 0.5f;           // 찍힘이 끝난 뒤 다음 기본 공격까지 (쿨타임 감소)
    [Min(0f)] public float chargeCooldown = 8f;              // R 모아 베기 쿨타임

    [Header("# 랜스")]
    [Min(0.01f)] public float thrustTime = 0.15f;            // 찌르기 동작 (공격속도)
    [Min(0f)] public float thrustInterval = 0.08f;           // 찌르기 사이 (공격속도)
    [Min(0f)] public float thrustComboCooldown = 0.56f;      // 3연 찌르기 후 다음 연계까지 (쿨타임 감소)
    [Min(0f)] public float gsComboCooldown = 0.42f;          // 각성 대검 3단 후 대기 (쿨타임 감소)
    [Min(0f)] public float zoneCooldown = 15f;               // R 강철의 영역 쿨타임

    private static readonly MainWeaponData[] cache = new MainWeaponData[5];

    public static MainWeaponData Load(int weaponId)
    {
        if (weaponId < 0 || weaponId >= cache.Length) return null;
        if (cache[weaponId] == null) cache[weaponId] = Resources.Load<MainWeaponData>("MainWeapon/MainWeapon_" + weaponId);
        return cache[weaponId];
    }
}
