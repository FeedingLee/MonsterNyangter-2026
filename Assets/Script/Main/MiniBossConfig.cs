using System;
using UnityEngine;

// 중간보스 설정 (Resources/MiniBossConfig). 단계 시간, 종별 능력치·애니메이션 프레임, 보상, 위치 표시 수치를 인스펙터에서 조정
// 능력치 배율 기준: 그 시점의 일반 몬스터 (Spawner의 현재 단계 SpawnData 체력·이동속도)
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "MonNyang/Mini Boss Config")]
public class MiniBossConfig : ScriptableObject
{
    private static MiniBossConfig instance;

    public static MiniBossConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<MiniBossConfig>("MiniBossConfig");
            }
            return instance;
        }
    }

    public enum Kind { Moss, Kestodon, Wasp, Beetle }

    [Serializable]
    public class Species
    {
        public string displayName;
        public Kind kind;
        [Min(1)] public int count = 1;
        [Min(0.1f)] public float healthMul = 6f;         // 일반 몬스터 체력 대비
        [Min(0f)] public float attackMul = 1.2f;         // 접촉 피해 배율
        [Min(0f)] public float speedMul = 1.1f;          // 일반 몬스터 이동속도 대비
        [Range(0f, 1f)] public float knockbackResist = 0.3f;
        [Min(0.1f)] public float size = 1.3f;            // 크기 배율 (메인 보스의 0.8배 이하로 제한)
        public bool useSizeBoost = true;                 // 전체 크기 강화(sizeBoost) 적용 여부
        public bool facesRight = true;                   // 시트 그림이 오른쪽을 보고 있음 (틀리면 문워크)
        public Color tint = Color.white;                 // 몸 색 (비드론: Vespoid 색을 바꿔 구분)
        public bool crispBody = true;                    // 몸 재질(bodyMaterial)로 흐린 테두리 자르기 (그림자가 그림에 붙은 Vespoid 는 끔)
        public Vector2Int rewardCoins = new Vector2Int(20, 30);   // 무리 전멸 보상 코인 (최소~최대)
        [Min(0.1f)] public float animSpeed = 1f;         // 애니메이션 속도 배율 (animFps x)

        [Header("스프라이트 (종별 시트에서 잘라 둔 프레임, MonNyang > 중간보스 에셋 다시 설정)")]
        public Sprite portrait;                          // 등장 문구 옆 대형 초상화
        public Sprite[] idle;
        public Sprite[] move;
        public Sprite[] attack;
        public Sprite[] alert;                           // '!' 경계
        public Sprite[] hit;
        public Sprite[] death;
    }

    [Header("# 단계")]
    public float[] stageTimes = { 60f, 210f, 390f, 570f };      // 1~4단계 시작 시각 (경과 초)
    [Min(0f)] public float spawnFxTime = 1f;                    // 소환 이펙트 후 등장
    [Min(0f)] public float announceTime = 2f;                   // "[ 우두머리 등장!! ]" 표시 시간
    [Min(1f)] public float animFps = 8f;

    [Header("# 전체 강화 (종별 수치에 곱함)")]
    [Min(0.1f)] public float sizeBoost = 1f;                    // 크기 (useSizeBoost 켠 종만)
    [Min(0.1f)] public float healthBoost = 2f;                  // 체력
    [Min(0.1f)] public float speedBoost = 1.3f;                 // 이동속도
    [Min(0f)] public float spawnMargin = 4.5f;                   // 화면 밖 등장 위치 여유 최소 (몸이 화면에 걸리지 않게)
    [Min(0f)] public float spawnMarginRandom = 4.5f;             // 여기에 0~이 값만큼 더해 거리를 무작위로
    public Material bodyMaterial;                               // 몸 재질 (반투명 테두리를 잘라 또렷하게, MiniBossSprite.mat)
    [Min(0f)] public float patternShake = 0.08f;                // 패턴(돌진·소환) 때 약한 화면 흔들림 세기
    [Min(0f)] public float patternShakeTime = 0.2f;
    [Range(0f, 180f)] public float spawnAngleGap = 100f;        // 화면 밖 등장 방향이 직전 방향과 이만큼(도) 이상 벌어지게
    public string sortingLayer = "Bottom Shadow";               // 그리는 레이어 (캠프 Shadow 레이어보다 위)
    public Color hitFlashColor = new Color(1f, 0.55f, 0.55f, 1f); // 피격 시 잠깐 물드는 색 (프레임을 바꾸지 않아 꿈틀거림 없음)
    [Min(0f)] public float hitFlashTime = 0.1f;
    [Range(0f, 1f)] public float knockbackTaken = 0.35f;        // 넉백을 받는 비율 (종별 넉백 저항 적용 후 곱함)
    [Min(0f)] public float separationPush = 8f;                 // 중간보스끼리 겹치지 않게 밀어내는 속도
    [Range(0.2f, 1.2f)] public float bodyRadiusRate = 0.4f;     // 몸 반지름 = 그림 가로 x 이 값

    [Header("# 움직임 연출 (먼지·파편)")]
    [Min(0.05f)] public float stepDustInterval = 0.3f;          // 걸을 때 먼지 간격 (초)
    [Min(0.1f)] public float stepDustSize = 0.5f;              // 먼지 크기 = 몸 가로 x 이 값
    [Min(0.02f)] public float chargeDebrisInterval = 0.07f;     // 케스킹 돌진 중 파편 간격 (초)

    [Header("# 등장 이펙트 (소환 지점 파티클)")]
    public Color spawnSmokeColor = new Color(0.3f, 0.1f, 0.18f, 0.5f);
    public Color spawnEmberColor = new Color(1f, 0.45f, 0.15f, 1f);
    [Min(0.1f)] public float spawnFxRadius = 2.5f;
    [Min(0)] public int spawnEmberCount = 40;

    [Header("# 등장 문구 뒤 불씨 파티클")]
    [Min(0)] public int announceEmberCount = 36;
    public Color announceEmberColor = new Color(1f, 0.55f, 0.2f, 1f);

    [Header("# 종 (단계 순서: 모스, 케스토돈, 말벌, 사슴벌레)")]
    public Species[] species =
    {
        new Species { displayName = "거대 멧돼지", kind = Kind.Moss, count = 5, healthMul = 6f, attackMul = 1.2f, speedMul = 1.1f, knockbackResist = 0.3f, size = 1.6f, rewardCoins = new Vector2Int(20, 30) },
        new Species { displayName = "케스킹", kind = Kind.Kestodon, count = 3, healthMul = 12f, attackMul = 2f, speedMul = 1.1f, knockbackResist = 0.5f, size = 1.7f, rewardCoins = new Vector2Int(30, 45) },
        new Species { displayName = "비드론", kind = Kind.Wasp, count = 20, healthMul = 10f, attackMul = 1.5f, speedMul = 1f, knockbackResist = 1f, size = 1.5f, useSizeBoost = false, crispBody = false, tint = new Color(0.75f, 0.65f, 1f, 1f), rewardCoins = new Vector2Int(45, 60) },
        new Species { displayName = "갑각군주", kind = Kind.Beetle, count = 1, healthMul = 60f, attackMul = 3f, speedMul = 0.6f, knockbackResist = 0.8f, size = 2f, rewardCoins = new Vector2Int(60, 80) },
    };

    [Header("# 케스토돈 돌진")]
    [Min(0.5f)] public float chargeInterval = 5f;
    [Min(0f)] public float chargeWarnTime = 1.2f;
    [Min(1f)] public float chargeSpeedMul = 10f;                // 기본 이속 x
    public bool chargeRunFrames = true;                         // 돌진 중 달리기 그림을 빠르게 재생 (끄면 공격 그림 한 장 → 미끄러지는 느낌)
    [Min(1f)] public float chargeRunAnimMul = 3.5f;             // 돌진 중 달리기 그림 속도 배율
    public bool chargeIgnoresMonsters = true;                   // 돌진 중 일반 몬스터와 충돌하지 않음
    public Sprite[] chargeWarnFrames;                           // 돌진 예고 (Attack_Effect_2 둘째 줄 겹화살표)
    [Min(0.1f)] public float chargeWarnWidth = 1.4f;            // 화살표 하나 크기 (월드)
    [Min(0.2f)] public float chargeWarnSpacing = 1.3f;          // 화살표 간격 (월드)
    public Color chargeWarnColor = new Color(1f, 0.3f, 0.2f, 1f);
    [Min(0.5f)] public float chargeDistance = 8f;
    [Min(0f)] public float chargeBlockedWait = 2.5f;           // 텐트 울타리에 가려 돌진 못 할 때 돌아 들어가며 기다리는 최대 시간 (지나면 모서리로 질러가는 돌진)
    [Min(0f)] public float chargeLosMargin = 0.3f;             // 시야 확인 시 울타리를 이만큼 넓게 봄 (스치는 각도도 막힌 것으로)
    [Min(0.2f)] public float chargeCornerMargin = 1.5f;        // 질러가는 돌진의 목표 모서리가 울타리에서 떨어진 거리
    public bool chargeReaimOnRelease = false;                  // 켜면 예고가 끝나는 순간 방향을 플레이어 쪽으로 다시 맞춤 (예고 중 벽 뒤로 숨기 방지, 대신 피하기 어려움)
    [Min(0f)] public float chargeChainWarnTime = 0.5f;         // 모서리 돌진 직후 이어지는 돌진의 예고 시간 (짧게)
    [Min(1f)] public float chargeWallRideDistanceMul = 1.6f;   // 벽 타기 돌진(예고 중 울타리 뒤로 숨은 플레이어를 향함)의 거리 배율

    [Header("# 말벌 원 대형")]
    [Min(0.5f)] public float waspStartRadius = 6f;
    [Min(0.3f)] public float waspMinRadius = 0.8f;
    [Min(0.3f)] public float waspChaseRadius = 2.5f;              // 원 반경이 이만큼 줄면 대형을 풀고 각자 플레이어 추적                 // 끝까지 조여 오는 최소 반경
    [Min(0f)] public float waspShrinkSpeed = 0.3f;
    [Min(0.05f)] public float waspHitInterval = 0.5f;
    [Min(0.5f)] public float waspWallOverlap = 1.15f;           // 몸 충돌 폭 = 처음 원에서 옆 말벌까지 간격 x 이 값 (1 이상이면 빈틈 없음)
    [Min(0f)] public float waspSpinSpeed = 0f;                  // 원을 따라 도는 속도 (도/초, 0이면 돌지 않음)
    [Min(0f)] public float waspCenterFollowSpeed = 0f;          // 원 중심이 플레이어를 따라가는 속도 (0이면 등장 위치에 고정)

    [Header("# 사슴벌레")]
    [Min(0f)] public float rallyRadius = 5f;
    [Range(0f, 3f)] public float rallyHpRate = 0.3f;            // 주변 일반 몬스터 최대 체력 +30%
    [Min(0.5f)] public float summonInterval = 8f;
    [Min(1)] public int summonCount = 8;
    [Min(1)] public int summonMax = 24;
    [Min(0.1f)] public float summonHealthMul = 3f;              // 소환 몬스터 체력 = 그 시점 일반 몬스터 x 이 값
    [Min(0.1f)] public float summonCastTime = 1f;               // 소환 준비 시간 (발밑 마법진 표시)
    public Sprite[] summonCircleFrames;                         // 소환 마법진 (Attack_Effect_2 넷째 줄)
    [Min(0.1f)] public float summonCircleSize = 2.2f;           // 마법진 크기 = 몸 가로 x 이 값
    [Range(0f, 1f)] public float warnMarkCutoff = 0.08f;        // 예고 화살표·마법진: 이 알파 이상은 진하게 (흐린 빛 그림도 또렷하게)
    [Min(0.1f)] public float summonSpotSize = 1.6f;             // 소환될 자리 표시 마법진 크기 (월드)
    public Color summonCircleColor = new Color(1f, 0.3f, 0.15f, 1f);

    [Header("# 접촉 피해")]
    [Min(0f)] public float contactDps = 50f;                    // 기존 몬스터 접촉 피해와 같은 기준 (초당)

    [Header("# 보상 상자 (Reward_box 시트)")]
    public Sprite[] boxIdle;
    public Sprite[] boxOpen;
    public Sprite[] boxBurst;
    [Min(0.1f)] public float boxSize = 2.3f;                     // 상자 가로 크기 (월드)
    [Min(0.1f)] public float coinScatterRadius = 2.2f;

    [Header("# 화면 밖 위치 표시")]
    public Sprite indicatorIcon;                                 // 초상화가 없을 때 쓰는 아이콘 (해골)
    public Sprite indicatorFrame;                                // 테두리 (Object_Panel_64 나침반 고리)
    public Sprite indicatorArrow;                                // 방향 화살표 (Attack_Effect_2 위 화살표)
    public float indicatorScaleNear = 1.2f;
    public float indicatorScaleFar = 0.6f;
    public float indicatorDistNear = 5f;
    public float indicatorDistFar = 25f;
    [Min(1)] public int indicatorMax = 5;
    [Min(10f)] public float indicatorBaseSize = 62f;
    [Range(0.1f, 1f)] public float indicatorAlpha = 0.55f;          // 위치 표시 전체 투명도 (시선을 덜 뺏게)             // 원 크기 (UI 단위, 1배 기준)
    public Color indicatorColor = new Color(0.55f, 0.08f, 0.08f, 0.9f);
}
