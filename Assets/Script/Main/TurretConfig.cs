using UnityEngine;

// 밤 디펜스 포탑 설정 (Resources/TurretConfig) - 코어루프 개선기획서 v2.0 4장, 지시서 1119_포탑_시스템
[CreateAssetMenu(fileName = "TurretConfig", menuName = "Scriptble Object/TurretConfig")]
public class TurretConfig : ScriptableObject
{
    private static TurretConfig instance;
    public static TurretConfig Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<TurretConfig>("TurretConfig");
            if (instance == null) instance = CreateInstance<TurretConfig>();
            return instance;
        }
    }

    public enum Kind { Crossbow, Sticky, Ballista, Dragon }

    [System.Serializable]
    public class TurretType
    {
        public string name;
        public bool rotates = true;                          // 회전 머리 (끈적이는 본체 하나라 회전 없음)
        [Min(0.1f)] public float range = 6f;
        [Min(0f)] public float damageMul = 0.5f;             // 피해 = turretBaseDamage x 캐릭터 공격력 x 이 값
        [Min(0.05f)] public float interval = 0.4f;           // 공격 주기 (초)
        [Min(0.05f)] public float scale = 0.6f;              // 포탑 크기 (A < B < C < D)
        public Vector2 headOffset = new Vector2(0f, 0.55f);  // 받침대 바닥에서 회전 머리 중심까지 (포탑 크기 적용 전, 머리가 받침 높이의 약 30% 위에 얹히게)
        public float footOffset = 0f;                        // 발판 위 세로 위치 보정 (월드, 아래로 = 음수)
        public bool spriteFacesRight = true;                 // 회전 없는 본체(끈적이)가 오른쪽을 보는 그림인지 (대상이 반대쪽이면 좌우 반전)
        public float muzzleDistance = 0.6f;                  // 머리 중심에서 발사 지점까지
        [Min(0.1f)] public float projectileSpeed = 14f;      // 직선 투사체 속도 / 포물선은 비행 시간으로
        [Min(0.05f)] public float flightTime = 0.6f;         // 포물선 비행 시간 (끈적이·화룡포)
        [Min(0.05f)] public float projectileSize = 0.6f;     // 투사체 그림 가로 크기 (월드)
        [Min(0.05f)] public float fxSize = 1f;               // 명중·관통·폭발 효과 크기 (월드)
        public AudioClip fireSound;

        [Header("그림 (Turret_A~D, 에디터 메뉴로 자동 지정)")]
        public Sprite baseSprite;                            // 받침대
        public Sprite[] idle;                                // 대기 (회전 머리 또는 본체)
        public Sprite[] attack;                              // 공격
        public Sprite sleep;                                 // 휴면 (천막)
        public Sprite[] projectile;                          // 화살 / 덩어리 / 대형 화살 / 불덩이
        public Sprite[] hitFx;                               // 명중 / 착지 튀김 / 관통 / 착탄 폭발
        public Sprite[] groundFx;                            // 감속 장판(퍼짐·유지·사라짐) / 바닥 불꽃
    }

    [Header("# 포탑 4종 (E 항목)")]
    // 크기: 그림 자체 크기가 달라 배율로 A < B < C < D (C·D는 다른 포탑보다 살짝 크게)
    public TurretType crossbow = new TurretType { name = "석궁", range = 6f, damageMul = 0.5f, interval = 0.4f, scale = 1.0f, headOffset = new Vector2(0f, 1.62f), projectileSpeed = 16f, projectileSize = 0.85f, fxSize = 0.8f };
    public TurretType sticky = new TurretType { name = "끈적이 투척기", rotates = false, range = 6f, damageMul = 0.2f, interval = 3f, scale = 0.95f, headOffset = Vector2.zero, flightTime = 0.7f, projectileSize = 0.75f, fxSize = 1.2f };
    public TurretType ballista = new TurretType { name = "관통 발리스타", range = 9f, damageMul = 3f, interval = 2.5f, scale = 0.74f, headOffset = new Vector2(0f, 1.77f), projectileSpeed = 20f, projectileSize = 1.8f, fxSize = 1.1f };
    public TurretType dragon = new TurretType { name = "화룡포", range = 7f, damageMul = 2f, interval = 3f, scale = 0.86f, headOffset = new Vector2(0f, 1.82f), flightTime = 0.55f, projectileSize = 1.3f, fxSize = 2.6f };

    public TurretType Get(Kind k) => k == Kind.Crossbow ? crossbow : k == Kind.Sticky ? sticky : k == Kind.Ballista ? ballista : dragon;

    [Header("# 발판 5곳 (번호 순서: 위 왼쪽, 위 오른쪽(모서리 옆), 아래 왼쪽, 아래 가운데, 아래 오른쪽(모서리))")]
    public Vector2[] padCenters = { new Vector2(-11f, -5.9f), new Vector2(-3f, -5.9f), new Vector2(-15f, -11.9f), new Vector2(-7f, -11.9f), new Vector2(1f, -11.9f) };
    public Kind[] padKinds = { Kind.Crossbow, Kind.Sticky, Kind.Crossbow, Kind.Dragon, Kind.Ballista };
    public int[] padCosts = { 50, 100, 50, 300, 200 };
    [Min(0.1f)] public float padHalfSize = 1f;               // 발판 위 판정 (2x2 칸 = 반쪽 1)

    [Header("# 건설")]
    [Min(0.1f)] public float buildStayTime = 1.5f;           // 발판 위에 이만큼 서 있으면 건설
    [Min(0.01f)] public float buildPopTime = 0.2f;           // 등장 연출 (크기 0 → 1)
    public AudioClip buildSound;
    public Color priceColor = new Color(1f, 0.9f, 0.45f, 1f);
    public Color pricePoorColor = new Color(1f, 0.35f, 0.3f, 1f);   // 코인 부족
    public Color padOutlineColor = new Color(1f, 0.85f, 0.35f, 0.55f);
    public Color gaugeColor = new Color(1f, 0.82f, 0.15f, 0.6f);   // 아래에서 위로 차오르는 건설 게이지 (발판 초록과 구분되는 노란색)
    public string gaugeSortingLayer = "ForeGround";          // 게이지 층 (발판 위, 캐릭터 아래)
    public int gaugeSortingOrder = 6;
    [Range(0.3f, 1f)] public float gaugeInset = 0.82f;       // 게이지 크기 / 발판 크기
    public Sprite padSprite;                                 // 포탑 제작 발판 (Build_turret)

    [Header("# 피해 (플레이어 기본 공격력 x 포탑 배율, 상점·젬·버프·아침 선택지 미적용)")]
    [Min(0f)] public float turretBaseDamage = 25f;

    [Header("# 끈적이 감속 장판")]
    [Range(0f, 1f)] public float slowRate = 0.4f;
    [Min(0.1f)] public float slowRadius = 1.5f;
    [Min(0.1f)] public float slowTime = 3f;

    [Header("# 화룡포 폭발·화상")]
    [Min(0.1f)] public float explodeRadius = 2f;
    [Min(0f)] public float burnDps = 0.3f;                    // 초당 피해 = 기본 공격력 x 이 값
    [Min(0.1f)] public float burnTime = 3f;
    [Min(0.1f)] public float flameTime = 3f;                  // 바닥 불꽃 (연출만)
    public AudioClip explodeSound;

    [Header("# 애니메이션")]
    public bool idleAnimate = false;                         // 대기 그림 두 장을 번갈아 (끄면 첫 장에 멈춤: 꾸물거림 없음)
    [Min(0.1f)] public float idleFps = 1.5f;
    [Min(1f)] public float attackFps = 14f;
    [Min(1f)] public float fxFps = 12f;
    public string sortingLayer = "Weapon";                   // 투사체·효과 층
}
