using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// 밤 디펜스 포탑 (DayNightCycle이 붙임, 지시서 1119_포탑_시스템)
// - 발판 5곳: 귀환 준비 시작 ~ 밤 끝에만 보임(가격 표시, 코인 부족 시 붉게). 낮에는 돌 타일을 옆 풀 타일로 바꿔 숨김
// - 가격 이상 가진 채 발판 위에 buildStayTime 서 있으면 건설 (아래→위 게이지, 벗어나면 초기화), 한 발판에 한 번, 지으면 발판 타일 제거
// - 포탑: 귀환 준비 ~ 밤 활성, 낮에는 천막 휴면, 보스전이 시작되면 발판과 함께 철거
public class TurretSystem : MonoBehaviour
{
    public static TurretSystem Instance { get; private set; }
    public static bool ShowRange;                            // [임시] 테스트 모드 사거리 원 표시

    private class Pad
    {
        public int index;
        public Vector2 center;
        public TurretConfig.Kind kind;
        public int cost;
        public Turret turret;
        public GameObject marker;                            // 발판 이미지 + 가격 + 게이지 (월드 캔버스)
        public Text price;
        public Image gauge;
        public float stay;
        public Vector3Int[] cells;                           // 2x2 타일 칸
        public TileBase[] stone;                             // 원래 돌 타일 (밤에 되돌림)
        public TileBase[] grass;                             // 낮에 채울 옆 풀 타일
    }

    private readonly List<Pad> pads = new List<Pad>();
    private TurretConfig cfg;
    private GameManager gm;
    private Tilemap tilemap;
    private bool active;                                     // 귀환 준비 ~ 밤
    private bool removed;                                    // 보스전 철거·무방비 캠프 봉인 뒤

    public bool Sealed => removed;
    public bool HasUnbuiltPad => !removed && pads.Exists(p => p.turret == null);

    // 건설 가격 (아침 선택지 공병 고양이: 오늘 밤 비용 배율)
    private static int Cost(int baseCost) => Mathf.Max(0, Mathf.RoundToInt(baseCost * RunStats.Get(RunStat.TurretCost)));

    // 아침 선택지 무방비 캠프: 지은 포탑 철거, 발판 영구 봉인 (보스전 철거와 같은 처리)
    public void Seal() => OnBossStart();

    private void Awake()
    {
        Instance = this;
        cfg = TurretConfig.Instance;
        gm = GetComponent<GameManager>();
        DayNightCycle.ReturnPrepStarted += OnReturnPrep;
        DayNightCycle.NightEnded += OnNightEnd;
        DayNightCycle.BossStarted += OnBossStart;
    }

    private void OnDestroy()
    {
        DayNightCycle.ReturnPrepStarted -= OnReturnPrep;
        DayNightCycle.NightEnded -= OnNightEnd;
        DayNightCycle.BossStarted -= OnBossStart;
        if (Instance == this) Instance = null;
    }

    // 새 판 (낮으로 시작): 포탑 없음, 발판은 풀로
    public void ResetRun()
    {
        foreach (Pad p in pads)
        {
            if (p.turret != null) Destroy(p.turret.gameObject);
            if (p.marker != null) Destroy(p.marker);
        }
        pads.Clear();
        removed = false;
        active = false;
        BuildPads();
        SetActive(false);
    }

    private void BuildPads()
    {
        tilemap = FindGroundTilemap();
        for (int i = 0; i < cfg.padCenters.Length; i++)
        {
            Pad p = new Pad
            {
                index = i,
                center = cfg.padCenters[i],
                kind = i < cfg.padKinds.Length ? cfg.padKinds[i] : TurretConfig.Kind.Crossbow,
                cost = i < cfg.padCosts.Length ? cfg.padCosts[i] : 50,
            };
            CaptureTiles(p);
            BuildMarker(p);
            pads.Add(p);
        }
    }

    private static Tilemap FindGroundTilemap()
    {
        foreach (Tilemap t in FindObjectsOfType<Tilemap>())
            if (t.name == "Tilemap") return t;              // Ground/Map Object/Tilemap (돌 발판이 있는 위 타일맵)
        return null;
    }

    // 발판 2x2 칸의 돌 타일과, 바로 옆(같은 줄·열 바깥쪽) 풀 칸 타일을 기억 (칸마다 다른 이웃에서 복사)
    private void CaptureTiles(Pad p)
    {
        if (tilemap == null) return;
        Vector3Int c = tilemap.WorldToCell(new Vector3(p.center.x - 0.5f, p.center.y + 0.5f, 0f));   // 왼쪽 위 칸
        p.cells = new[] { c, c + Vector3Int.right, c + Vector3Int.down, c + Vector3Int.right + Vector3Int.down };
        // 칸마다 가까운 바깥 이웃: 왼쪽 위 → 왼쪽, 오른쪽 위 → 오른쪽, 왼쪽 아래 → 아래, 오른쪽 아래 → 오른쪽 아래 대각
        Vector3Int[] outward = { Vector3Int.left, Vector3Int.right, Vector3Int.down, Vector3Int.right + Vector3Int.down };
        Vector3Int[] fallback = { Vector3Int.up, Vector3Int.up, Vector3Int.left, Vector3Int.down };
        p.stone = new TileBase[4];
        p.grass = new TileBase[4];
        for (int k = 0; k < 4; k++)
        {
            p.stone[k] = tilemap.GetTile(p.cells[k]);
            TileBase g = tilemap.GetTile(p.cells[k] + outward[k]);
            if (g == null || IsStone(g)) g = tilemap.GetTile(p.cells[k] + fallback[k]);
            if (g != null && IsStone(g)) g = null;               // 이웃도 없으면 비움 (아래 풀 바닥이 보임)
            p.grass[k] = g;
        }
    }

    private static bool IsStone(TileBase t) => t != null && t.name.Contains("Stone");

    private void SetTiles(Pad p, bool stone)
    {
        if (tilemap == null || p.cells == null) return;
        for (int k = 0; k < 4; k++) tilemap.SetTile(p.cells[k], stone ? p.stone[k] : p.grass[k]);
    }

    // 발판 표시: Build_turret 발판 이미지 + 가격 + 아래→위로 차오르는 게이지 (월드 캔버스)
    private void BuildMarker(Pad p)
    {
        GameObject go = new GameObject("Turret Pad " + (p.index + 1));
        go.transform.position = p.center;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "ForeGround";              // 땅 타일(ForeGround 0) 바로 위, 캐릭터 아래
        canvas.sortingOrder = 5;
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(200f, 200f);
        rt.localScale = Vector3.one * (cfg.padHalfSize * 2f / 200f);

        Vector2 padSize = new Vector2(196f, 196f);
        if (cfg.padSprite != null)
        {
            Image pad = RunUI.Panel("Pad", go.transform, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, padSize);
            pad.sprite = cfg.padSprite;
            pad.type = Image.Type.Simple;
            pad.preserveAspect = true;
            pad.raycastTarget = false;
        }
        else
        {
            Image outline = RunUI.Panel("Outline", go.transform, cfg.padOutlineColor, new Vector2(0.5f, 0.5f), Vector2.zero, padSize);
            outline.sprite = null;
            outline.raycastTarget = false;
            outline.color = new Color(cfg.padOutlineColor.r, cfg.padOutlineColor.g, cfg.padOutlineColor.b, cfg.padOutlineColor.a * 0.35f);
        }

        // 게이지: 발판 안쪽을 아래에서 위로 채움. 하위 캔버스로 캐릭터보다 위에 그려 밟고 있어도 보이게
        RectTransform gaugeLayer = RunUI.Rect("Gauge Layer", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, padSize);
        Canvas gaugeCanvas = gaugeLayer.gameObject.AddComponent<Canvas>();
        gaugeCanvas.overrideSorting = true;
        gaugeCanvas.sortingLayerName = cfg.gaugeSortingLayer;
        gaugeCanvas.sortingOrder = cfg.gaugeSortingOrder;
        p.gauge = RunUI.Panel("Gauge", gaugeLayer, cfg.gaugeColor, new Vector2(0.5f, 0.5f), Vector2.zero, padSize * cfg.gaugeInset);
        p.gauge.sprite = WhiteSprite();                      // 단색 (채움 방식은 그림이 있어야 동작, 발판 그림과 섞여 탁해지지 않게)
        p.gauge.type = Image.Type.Filled;
        p.gauge.fillMethod = Image.FillMethod.Vertical;
        p.gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
        p.gauge.fillAmount = 0f;
        p.gauge.raycastTarget = false;

        p.price = RunUI.Label("Price", go.transform, p.cost.ToString(), 64, cfg.priceColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 90f), 4f);
        p.marker = go;
    }

    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }

    private void OnReturnPrep()
    {
        if (!removed) SetActive(true);
    }

    private void OnNightEnd()
    {
        if (!removed) SetActive(false);
    }

    // 보스전: 포탑·발판 철거, 발판은 풀로 바꾼 채 되돌리지 않음
    private void OnBossStart()
    {
        if (removed) return;
        removed = true;
        foreach (Pad p in pads)
        {
            if (p.turret != null) Destroy(p.turret.gameObject);
            p.turret = null;
            if (p.marker != null) p.marker.SetActive(false);
            SetTiles(p, false);
        }
        active = false;
    }

    // 활성(귀환 준비 ~ 밤): 돌 발판·가격 표시, 포탑 깨어남 / 낮: 풀, 표시 숨김, 포탑 천막
    private void SetActive(bool on)
    {
        active = on;
        foreach (Pad p in pads)
        {
            SetTiles(p, on && p.turret == null);              // 지은 발판은 풀로 (발판이 사라짐)
            p.stay = 0f;
            if (p.marker != null) p.marker.SetActive(on && p.turret == null);
            if (p.turret != null) p.turret.SetAwake(on);
        }
    }

    private void Update()
    {
        if (!active || removed || gm == null || !gm.isLive || gm.player == null)
            return;
        Vector2 pos = gm.player.transform.position;
        foreach (Pad p in pads)
        {
            if (p.turret != null || p.marker == null)
                continue;
            int cost = Cost(p.cost);
            p.price.text = cost.ToString();
            bool rich = CoinWallet.Coins >= cost;
            p.price.color = rich ? cfg.priceColor : cfg.pricePoorColor;   // 코인 부족 시 붉게
            bool on = Mathf.Abs(pos.x - p.center.x) <= cfg.padHalfSize && Mathf.Abs(pos.y - p.center.y) <= cfg.padHalfSize;
            p.stay = on && rich ? p.stay + Time.deltaTime : 0f;         // 벗어나거나 코인이 모자라면 초기화
            p.gauge.fillAmount = Mathf.Clamp01(p.stay / cfg.buildStayTime);
            if (p.stay >= cfg.buildStayTime && CoinWallet.TrySpend(cost))
                Build(p, true);
        }
    }

    private void Build(Pad p, bool sound)
    {
        p.stay = 0f;
        p.turret = Turret.Create(p.kind, p.center, cfg);
        p.turret.SetAwake(active);
        if (p.marker != null) p.marker.SetActive(false);
        SetTiles(p, false);                                  // 건설 완료: 아래 발판 타일 제거
        if (sound && cfg.buildSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(cfg.buildSound);
    }

    // ---------------- 테스트 모드 ----------------
    public void BuildAllFree()
    {
        if (removed) return;
        foreach (Pad p in pads)
            if (p.turret == null) Build(p, false);
    }

    public void ResetTurrets()
    {
        foreach (Pad p in pads)
        {
            if (p.turret != null) Destroy(p.turret.gameObject);
            p.turret = null;
            p.stay = 0f;
            if (p.marker != null) p.marker.SetActive(active && !removed);
            SetTiles(p, active && !removed);
            p.gauge.fillAmount = 0f;
        }
    }
}
