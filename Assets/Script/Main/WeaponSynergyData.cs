using System;
using UnityEngine;

// 무기 시너지 데이터 (짝 구성, 이름, 설명 문구, 수치를 한 곳에 모음)
// Resources/WeaponSynergyData 에셋을 읽고, 없으면 아래 기본값을 사용한다.
// 짝: 메인 무기 ← 시너지 무기 (메인 확정 + 짝 무기 Lv3 이상 = 공명, Lv5 = 각성)
[CreateAssetMenu(fileName = "WeaponSynergyData", menuName = "MonNyang/Weapon Synergy Data")]
public class WeaponSynergyData : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ItemData.ItemType main;                       // 메인 무기
        public ItemData.ItemType partner;                    // 짝(시너지) 무기
        public string resonanceName;
        [TextArea] public string resonanceDesc;
        public string awakeningName;
        [TextArea] public string awakeningDesc;
    }

    [Header("# 단계 판정")]
    [Min(1)] public int resonanceLevel = 3;                  // 공명: 짝 무기 레벨
    [Min(1)] public int awakeningLevel = 5;                  // 각성: 짝 무기 레벨

    [Header("# 달성 팝업")]
    [Min(0.01f)] public float popupScaleTime = 0.3f;         // 확대 연출 시간 (unscaled)
    public AudioClip popupSound;                             // 비우면 레벨업 효과음
    public Sprite popupPanelSprite;                          // 비우면 상점 양피지 판넬
    [Min(0f)] public float popupTextGap = 20f;               // 팝업 이름과 설명 사이 간격
    public Vector2 popupGlowOffset = new Vector2(0f, -150f); // 카드 뒤 금빛 고리 위치 (카드 가운데 기준, 아래쪽 가장자리와 맞물리게)
    public Color resonanceColor = new Color(0.35f, 0.75f, 1f, 1f);   // 공명: 스킬 버튼 테두리, 팝업 강조색
    public Color awakeningColor = new Color(1f, 0.78f, 0.25f, 1f);   // 각성

    [Header("# 공명 효과 수치")]
    [Range(0f, 1f)] public float chargeDamageTakenRate = 0.5f;       // 대검+랜스: 기 모으는 동안 받는 피해 배율
    [Min(1)] public int guardBladeCount = 2;                         // 헤비보우건+쌍검: 호위 칼날 수
    [Min(0.1f)] public float guardBladeDistance = 1.5f;              // 호위 칼날 거리 (보조 쌍검과 같음)
    [Min(1f)] public float guardBladeSpeed = 300f;                   // 호위 칼날 회전 속도 (도/초)
    [Min(0.05f)] public float arrowInterval = 0.3f;                  // 쌍검+활: 회전 중 화살 간격
    [Min(0.05f)] public float arrowScale = 0.6f;                     // 쌍검+활: 작은 화살 크기 배율
    [Min(0.1f)] public float arrowRange = 6f;                        // 쌍검+활: 작은 화살 사거리
    [Min(0f)] public float arrowDamageRate = 0.5f;                   // 쌍검+활: 베기 피해 대비 화살 피해
    [Min(0.1f)] public float arrowSpeed = 15f;                       // 쌍검+활: 작은 화살 속도

    [Header("# 각성 효과 수치 (보스 1회 피해는 160 이하로 잘림)")]
    [Min(0f)] public float freezeTime = 0.4f;                        // 대검: 천지 가르기 정지 시간
    [Min(0f)] public float cleaveBossDamage = 100f;                  // 대검: 피해 기준 (일반·중간보스·보스 모두 이 값 x2를 한 대)
    [Min(0f)] public float cleaveSecondHitDelay = 0.15f;             // 대검: 2타 간격
    [Min(0.1f)] public float sliceKillDuration = 0.9f;               // 대검: 즉사한 몬스터가 두 동강 나 사라지는 시간
    [Min(1)] public int beamHitCount = 5;                            // 활: 보스 다단히트 횟수
    [Min(0f)] public float beamHitDamage = 35f;                      // 활: 1회 피해 (잡몹은 1회)
    [Min(1f)] public float beamSpeed = 28f;                          // 활: 거대 화살 속도
    [Min(0.5f)] public float beamLength = 4f;                        // 활: 거대 화살 길이 (월드)
    [Min(1)] public int stormBladeCount = 6;                         // 헤비보우건: 칼날 수
    [Min(0f)] public float stormBladeDamage = 35f;                   // 헤비보우건: 칼날 1자루 피해
    [Min(0)] public int stormPierce = 2;                             // 헤비보우건: 관통 횟수 (유도 대상 포함 최대 pierce+1마리)
    [Min(0f)] public float stormTurnSpeed = 540f;                    // 헤비보우건: 유도 회전 속도 (도/초)
    [Min(1f)] public float stormBladeSpeed = 14f;                    // 헤비보우건: 칼날 속도
    [Min(1f)] public float dashRangeMul = 1.5f;                      // 쌍검: 돌진 거리 배율
    [Min(0.1f)] public float dashSpeedMul = 2f;                      // 쌍검: 돌진 속도 배율
    [Min(0f)] public float dashDamageMul = 1.5f;                     // 쌍검: 돌진 피해 배율
    [Min(0f)] public float slashDelay = 0.2f;                        // 쌍검: 경로 폭발까지
    [Min(0f)] public float dashExplodeDamage = 100f;                 // 쌍검: 경로 폭발 피해
    [Min(0.1f)] public float dashExplodeWidth = 0.9f;                // 쌍검: 경로 폭발 폭
    [Min(1)] public int dualDashCharge = 2;                          // 쌍검: 각성 시 돌진 최대 충전 수
    [Min(0f)] public float dashPostInvincible = 0.25f;               // 쌍검: 돌진 종료 후 추가 무적 (각성 무관)
    [Min(0)] public int lightningCount = 3;                          // 쌍검: 각성 돌진 번개 줄기 수
    [Min(0f)] public float lightningFlashTime = 0.08f;               // 쌍검: 화면 흰 번쩍임 시간
    [Min(0f)] public float beamChargeTime = 0.3f;                    // 활: 발사 전 빛 모으기 시간
    [Min(0f)] public float beamAfterTime = 0.5f;                     // 활: 지나간 자리 빛줄기 잔상 시간
    [Min(0f)] public float awakenFlashTime = 0.1f;                   // 대검·랜스: 화면 흰 번쩍임 시간

    [Header("# 화면 흔들림")]
    [Min(0f)] public float shakeWeakPower = 0.1f;                    // 약: 무기 전용 스킬, 필살기
    [Min(0f)] public float shakeWeakTime = 0.15f;
    [Min(0f)] public float shakeStrongPower = 0.25f;                 // 강: 각성 효과
    [Min(0f)] public float shakeStrongTime = 0.3f;

    [Header("# 각성 효과음 (비우면 기존 효과음)")]
    public AudioClip cleaveSound;
    public AudioClip beamSound;
    public AudioClip bladeStormSound;
    public AudioClip flashDashSound;

    [Header("# 짝 구성")]
    public Entry[] entries =
    {
        new Entry { main = ItemData.ItemType.GreatSword, partner = ItemData.ItemType.Lance,
            resonanceName = "견디기", resonanceDesc = "차지 중 받는 피해 감소",
            awakeningName = "천지 가르기", awakeningDesc = "최대로 모아 베면 화면 안의 적을 모두 벤다" },
        new Entry { main = ItemData.ItemType.HuntingBow, partner = ItemData.ItemType.SniperHBG,
            resonanceName = "꿰뚫는 화살", resonanceDesc = "화살이 끝없이 관통",
            awakeningName = "관통 섬광", awakeningDesc = "화살비 후 거대 관통 화살 발사" },
        new Entry { main = ItemData.ItemType.Lance, partner = ItemData.ItemType.GreatSword,
            resonanceName = "대검 공명", resonanceDesc = "패링하면 대검이 함께 돈다",
            awakeningName = "대검 해방", awakeningDesc = "창을 던지면 대검을 뽑아 든다" },
        new Entry { main = ItemData.ItemType.SniperHBG, partner = ItemData.ItemType.DualBlades,
            resonanceName = "칼날 연사", resonanceDesc = "연사탄 25발마다 칼날이 적을 추격",
            awakeningName = "칼날 중량탄", awakeningDesc = "중량탄이 화면 끝에서 튕긴다" },
        new Entry { main = ItemData.ItemType.DualBlades, partner = ItemData.ItemType.HuntingBow,
            resonanceName = "추격의 깃털", resonanceDesc = "베기마다 작은 화살 발사",
            awakeningName = "섬광 돌격", awakeningDesc = "돌진이 더 멀리, 더 빠르게, 더 강하게" },
    };

    public Entry Find(ItemData.ItemType main)
    {
        foreach (Entry entry in entries)
        {
            if (entry.main == main)
                return entry;
        }
        return null;
    }
}
