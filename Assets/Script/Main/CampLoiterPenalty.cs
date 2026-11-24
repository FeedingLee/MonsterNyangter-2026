using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 캠프 비비기 처벌: 낮에 텐트 울타리 근처(경고 영역)를 계속 돌며 싸우면 일반 몬스터 압박이 강해짐
// - 낮에만 동작 (밤·보스전에는 쌓이지 않음, 밤이 되면 모두 초기화)
// - 상점 NPC를 이용할 수 있는 거리 안은 예외
// - 1단계: 영역 안 시간을 누적 (밖에 있어도 줄지 않음). 그날 n번째 처벌 기준(20 → 10 → 5초)에 닿으면 처벌 발동
// - 2단계(처벌): 게이지가 영역 안에서는 오르고 밖에서는 내려감. 0이 되면 해제되고 누적은 처음부터
// - 처벌 효과 (일반 몬스터만): 머리 위 눈 표시, 화면 바로 밖에서 소환, 화면 밖 몬스터를 플레이어가 도는 방향 앞쪽으로 옮김
// - 처벌 중 버틴 시간 10초마다 스택: 새 일반 몬스터 체력 증가·붉은 몸. 처벌이 해제되면 강화는 몇 초에 걸쳐 원래대로
// 수치는 RunConfig의 "캠프 비비기 처벌" 항목
public class CampLoiterPenalty : MonoBehaviour
{
    public static CampLoiterPenalty Instance { get; private set; }
    public static bool Punished => Instance != null && Instance.punished;

    // 확인용 표시에서 읽는 값
    public float Accumulated => accum;
    public float Threshold => CurrentThreshold();
    public float Gauge => gauge;
    public int Stage => stage;
    public bool InZone => inZone;
    public bool Exempt => exempt;
    public bool Running => running;
    public float Overtime => overtime;
    public int Stack => Mathf.Min(rc.penaltyMaxStack, Mathf.FloorToInt(overtime / Mathf.Max(0.1f, rc.penaltyOvertimeInterval)));

    private RunConfig rc;
    private float accum;                                       // 영역 안 누적 시간 (처벌 전)
    private float startGrace;                                  // 게임 시작 유예 남은 시간
    private float gauge;                                       // 처벌 게이지 (0이 되면 해제)
    private float overtime;                                    // 처벌 중 영역 안에서 버틴 시간 (스택: 새 몬스터 체력 증가)
    private bool punished;
    private int stage;                                         // 오늘 처벌받은 횟수
    private bool inZone, exempt, running;
    private bool wasNight;

    // 플레이어가 캠프 둘레를 도는 방향 (+1 반시계, -1 시계)
    private float orbit;
    private float lastAngle = float.NaN;
    private Vector2 lastPos;
    private Vector2 moveDir;

    private float nextTeleport;
    private float nextScan;
    private class Eye { public SpriteRenderer r; public float born; public float phase; }
    private readonly Dictionary<Enemy, Eye> eyes = new Dictionary<Enemy, Eye>();
    private readonly List<Enemy> removeList = new List<Enemy>();
    private readonly List<Enemy> candidates = new List<Enemy>();

    public static void Create(Transform parent)
    {
        if (Instance != null) return;
        GameObject go = new GameObject("Camp Loiter Penalty");
        go.transform.SetParent(parent, false);
        go.AddComponent<CampLoiterPenalty>();
        go.AddComponent<CampLoiterDebug>();                    // [임시 확인용] 경고 영역·상점 예외 영역·수치 표시 (테스트 후 삭제)
    }

    private void Awake()
    {
        Instance = this;
        rc = RunConfig.Instance;
        startGrace = rc.loiterStartGrace;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearEyes();
    }

    // 경고 영역: 캠프 바닥 타일 전체 범위 (못 찾으면 울타리 출입 금지 영역 + 폭)
    private Rect tileZone;
    private bool tileZoneReady, tileZoneFound;

    public Rect Zone
    {
        get
        {
            if (!tileZoneReady) FindTileZone();
            if (tileZoneFound) return tileZone;
            Rect a = CampZone.Area;
            float w = rc.loiterZoneWidth;
            return Rect.MinMaxRect(a.xMin - w, a.yMin - w, a.xMax + w, a.yMax + w);
        }
    }

    // 울타리 주변에서 이름이 loiterZoneTilePrefix로 시작하는 타일들을 감싸는 사각형 (한 번만 계산)
    private void FindTileZone()
    {
        tileZoneReady = true;
        if (string.IsNullOrEmpty(rc.loiterZoneTilePrefix) || CampZone.Instance == null) return;
        Rect a = CampZone.Area;
        float s = rc.loiterZoneTileSearch;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (UnityEngine.Tilemaps.Tilemap map in FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>())
        {
            Vector3Int c0 = map.WorldToCell(new Vector3(a.xMin - s, a.yMin - s, 0f));
            Vector3Int c1 = map.WorldToCell(new Vector3(a.xMax + s, a.yMax + s, 0f));
            for (int x = c0.x; x <= c1.x; x++)
            {
                for (int y = c0.y; y <= c1.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    TileBase tile = map.GetTile(cell);
                    if (tile == null || !tile.name.StartsWith(rc.loiterZoneTilePrefix)) continue;
                    Vector3 lo = map.CellToWorld(cell);
                    Vector3 hi = map.CellToWorld(cell + new Vector3Int(1, 1, 0));
                    min = Vector2.Min(min, Vector2.Min(lo, hi));
                    max = Vector2.Max(max, Vector2.Max(lo, hi));
                    tileZoneFound = true;
                }
            }
        }
        if (tileZoneFound) tileZone = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private float CurrentThreshold()
    {
        float[] t = rc.loiterThresholds;
        if (t == null || t.Length == 0) return 20f;
        return t[Mathf.Clamp(stage, 0, t.Length - 1)];
    }

    // 처벌 중 새로 소환된 일반 몬스터: 버틴 스택만큼 체력 증가·붉은 몸 (Spawner에서 Init 직후 호출)
    public static void ApplyToSpawn(Enemy enemy)
    {
        if (enemy == null || !Punished)
            return;
        enemy.ApplyPenaltyStack(Instance.Stack);
    }

    // 테스트: 처벌 중 버틴 시간 +초 (스택 확인용)
    public void AddTestOvertime(float seconds)
    {
        if (punished) overtime += seconds;
    }

    // 테스트: 처벌 전이면 누적 +초, 처벌 중이면 게이지 +초
    public void AddTestSeconds(float seconds)
    {
        if (punished)
        {
            gauge = Mathf.Min(rc.penaltyMaxGauge, gauge + seconds);
            return;
        }
        accum += seconds;
        if (accum >= CurrentThreshold()) Activate();
    }

    private void Update()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null || CampZone.Instance == null) return;
        Vector2 pos = gm.player.transform.position;
        TrackMovement(pos);

        // 밤: 아예 쌓이지 않고, 밤이 되는 순간 모두 초기화
        if (DayNightCycle.IsNight)
        {
            if (!wasNight) ResetDay();
            wasNight = true;
            running = false;
            return;
        }
        wasNight = false;

        // 보스전: 동작하지 않음 (처벌도 해제)
        if (gm.isBossSpawn)
        {
            if (punished) Release();
            running = false;
            return;
        }

        running = DayNightCycle.ShopAvailable && gm.isLive;   // 낮 (게임 진행 중)
        if (!running) return;

        float dt = Time.deltaTime;
        inZone = Zone.Contains(pos);
        exempt = UpgradeShop.PlayerInRange;                    // 상점 이용 거리 안은 예외
        bool counting = inZone && !exempt;

        // 게임 시작 유예: 캠프 안에서 시작하므로, 처음 영역을 벗어나거나 loiterStartGrace초가 지날 때까지 쌓지 않음
        if (startGrace > 0f)
        {
            startGrace = inZone ? startGrace - dt : 0f;
            counting = false;
        }

        if (!punished)
        {
            if (counting)
            {
                accum += dt;
                if (accum >= CurrentThreshold()) Activate();
            }
            return;
        }

        gauge += counting ? rc.penaltyGainPerSec * dt : -rc.penaltyDecayPerSec * dt;
        if (counting) overtime += dt;                          // 처벌을 무시하고 버틴 시간 (밖에 있어도 줄지 않음)
        gauge = Mathf.Min(gauge, rc.penaltyMaxGauge);
        if (gauge <= 0f)
        {
            Release();
            return;
        }
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.2f;
            ScanEyes();
        }
        if (Time.time >= nextTeleport)
        {
            nextTeleport = Time.time + rc.penaltyTeleportInterval;
            TeleportAhead(pos);
        }
    }

    private void LateUpdate()
    {
        if (eyes.Count == 0) return;
        removeList.Clear();
        foreach (var kv in eyes)
        {
            Enemy e = kv.Key;
            SpriteRenderer eye = kv.Value.r;
            // 죽었거나 풀로 돌아간 몬스터는 바로 떼어 냄 (다시 꺼내 쓸 때 남지 않게)
            if (e == null || eye == null || !e.gameObject.activeInHierarchy || !e.IsAlive)
            {
                removeList.Add(e);
                if (eye != null) Destroy(eye.gameObject);
                continue;
            }
            SpriteRenderer body = e.GetComponent<SpriteRenderer>();
            if (body == null) continue;
            Bounds b = body.bounds;
            float bob = Mathf.Sin(Time.time * rc.penaltyEyeBobSpeed + kv.Value.phase) * rc.penaltyEyeBob;   // 머리 위에서 살짝 떠다님
            eye.transform.position = new Vector3(b.center.x, b.max.y + rc.penaltyEyeOffsetY + bob, 0f);
            eye.sprite = EyeFrame(Time.time - kv.Value.born);
        }
        foreach (Enemy e in removeList) eyes.Remove(e);
    }

    private void Activate()
    {
        punished = true;
        gauge = rc.penaltyStartGauge;
        stage++;
        accum = 0f;
        nextTeleport = Time.time + rc.penaltyTeleportInterval;
        if (AudioManager.instance != null) AudioManager.instance.PlayClip(rc.penaltySound);
        ScanEyes();
    }

    private void Release()
    {
        bool was = punished;
        punished = false;
        gauge = 0f;
        accum = 0f;
        overtime = 0f;
        if (was)
        {
            // 나가면 정상으로: 스택으로 강화된 몬스터가 서서히 원래 체력·색으로
            foreach (Enemy e in FindObjectsOfType<Enemy>())
            {
                if (e.IsAlive) e.BeginPenaltyRecover(rc.penaltyRecoverTime);
            }
        }
        ClearEyes();
    }

    private void ResetDay()
    {
        Release();
        stage = 0;
    }

    // ---------------- 눈 표시 ----------------
    // 눈 뜨기 프레임을 한 번 재생한 뒤 빛나는 구간을 반복
    private Sprite EyeFrame(float age)
    {
        Sprite[] f = rc.penaltyEyeFrames;
        int i = Mathf.FloorToInt(age * rc.penaltyEyeFps);
        int start = Mathf.Clamp(rc.penaltyEyeLoopStart, 0, f.Length - 1);
        int end = Mathf.Clamp(rc.penaltyEyeLoopEnd, start, f.Length - 1);
        if (i >= start) i = start + (i - start) % (end - start + 1);
        return f[i];
    }

    private void ScanEyes()
    {
        if (rc.penaltyEyeFrames == null || rc.penaltyEyeFrames.Length == 0) return;
        foreach (Enemy e in FindObjectsOfType<Enemy>())
        {
            if (!e.IsAlive || eyes.ContainsKey(e)) continue;
            SpriteRenderer eye = new GameObject("Penalty Eye").AddComponent<SpriteRenderer>();
            eye.transform.SetParent(e.transform, true);
            eye.sprite = rc.penaltyEyeFrames[0];
            float w = Mathf.Max(0.01f, rc.penaltyEyeFrames[0].bounds.size.x);
            float ps = Mathf.Max(0.01f, Mathf.Abs(e.transform.lossyScale.x));
            float k = e.PenaltyStack / (float)Mathf.Max(1, rc.penaltyMaxStack);   // 스택이 쌓인 뒤 나온 몬스터일수록 크고 진하게
            float size = rc.penaltyEyeSize * Mathf.Lerp(1f, rc.penaltyEyeStackScale, k);
            eye.transform.localScale = Vector3.one * (size / w / ps);
            eye.color = Color.Lerp(Color.white, rc.penaltyEyeStackColor, k);
            eye.sortingLayerName = rc.penaltyEyeSortingLayer;   // 다른 몬스터에 가려지지 않게
            eye.sortingOrder = 50;
            eyes[e] = new Eye { r = eye, born = Time.time, phase = Random.Range(0f, 6.28f) };
        }
    }

    private void ClearEyes()
    {
        foreach (var kv in eyes)
        {
            if (kv.Value.r != null) Destroy(kv.Value.r.gameObject);
        }
        eyes.Clear();
    }

    // ---------------- 도는 방향 ----------------
    private void TrackMovement(Vector2 pos)
    {
        Vector2 center = CampZone.Area.center;
        float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x) * Mathf.Rad2Deg;
        if (!float.IsNaN(lastAngle))
        {
            float d = Mathf.DeltaAngle(lastAngle, angle);
            if (Mathf.Abs(d) > 0.001f) orbit = Mathf.Lerp(orbit, Mathf.Sign(d), 0.05f);   // 최근 도는 방향 (부드럽게)
        }
        lastAngle = angle;
        Vector2 delta = pos - lastPos;
        if (delta.sqrMagnitude > 0.0001f) moveDir = Vector2.Lerp(moveDir, delta.normalized, 0.2f);
        lastPos = pos;
    }

    // ---------------- 화면 밖 몬스터를 앞쪽으로 ----------------
    private void TeleportAhead(Vector2 playerPos)
    {
        Camera cam = Camera.main;
        if (cam == null || rc.penaltyTeleportCount <= 0) return;

        // 화면에 안 보이는 일반 몬스터 중 플레이어에게서 먼 것부터
        candidates.Clear();
        foreach (Enemy e in FindObjectsOfType<Enemy>())
        {
            if (e.IsAlive && Offscreen(cam, e.transform.position, 0.5f)) candidates.Add(e);
        }
        if (candidates.Count == 0) return;
        candidates.Sort((a, b) => ((Vector2)b.transform.position - playerPos).sqrMagnitude.CompareTo(((Vector2)a.transform.position - playerPos).sqrMagnitude));

        Vector2 ahead = AheadPoint(cam, playerPos);
        int n = Mathf.Min(rc.penaltyTeleportCount, candidates.Count);
        for (int i = 0; i < n; i++)
        {
            Vector2 p = ahead + Random.insideUnitCircle * rc.penaltyTeleportSpread;
            p = OffscreenSpawn.ClampToMap(CampZone.FreeSpot(p, 0.4f));
            if (!Offscreen(cam, p, 0.3f)) continue;            // 화면 안이면 옮기지 않음 (갑자기 나타나지 않게)
            Enemy e = candidates[i];
            Rigidbody2D rb = e.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = p;
            e.transform.position = new Vector3(p.x, p.y, e.transform.position.z);
        }
    }

    // 플레이어가 가려는 방향 앞쪽, 화면 바로 밖 지점
    // 경고 영역 안: 캠프 둘레를 따라 도는 방향 앞쪽 / 밖: 움직이는 방향 앞쪽
    private Vector2 AheadPoint(Camera cam, Vector2 playerPos)
    {
        if (inZone && Mathf.Abs(orbit) > 0.2f)
        {
            Rect z = Zone;
            Rect a = CampZone.Area;
            Rect ring = Rect.MinMaxRect((z.xMin + a.xMin) * 0.5f, (z.yMin + a.yMin) * 0.5f, (z.xMax + a.xMax) * 0.5f, (z.yMax + a.yMax) * 0.5f);   // 경고 영역 한가운데 둘레
            float perimeter = 2f * (ring.width + ring.height);
            float t0 = PerimeterParam(ring, playerPos);
            float sign = Mathf.Sign(orbit);
            for (float d = 2f; d < perimeter * 0.5f; d += 0.5f)
            {
                Vector2 p = PerimeterPoint(ring, t0 + sign * d);
                if (Offscreen(cam, p, rc.penaltyTeleportOffscreen)) return p;
            }
        }
        Vector2 dir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Random.insideUnitCircle.normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return OffscreenSpawn.Edge(playerPos, angle, rc.penaltyTeleportOffscreen);
    }

    private static bool Offscreen(Camera cam, Vector2 p, float worldMargin)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 c = cam.transform.position;
        return Mathf.Abs(p.x - c.x) > halfW + worldMargin || Mathf.Abs(p.y - c.y) > halfH + worldMargin;
    }

    // 사각형 둘레 위치 (왼쪽 아래에서 시작해 반시계: 아래 → 오른쪽 → 위 → 왼쪽)
    private static Vector2 PerimeterPoint(Rect r, float t)
    {
        float w = r.width, h = r.height, per = 2f * (w + h);
        t = Mathf.Repeat(t, per);
        if (t < w) return new Vector2(r.xMin + t, r.yMin);
        t -= w;
        if (t < h) return new Vector2(r.xMax, r.yMin + t);
        t -= h;
        if (t < w) return new Vector2(r.xMax - t, r.yMax);
        t -= w;
        return new Vector2(r.xMin, r.yMax - t);
    }

    // 점에서 가장 가까운 둘레 위치의 매개변수
    private static float PerimeterParam(Rect r, Vector2 p)
    {
        float w = r.width, h = r.height;
        float x = Mathf.Clamp(p.x, r.xMin, r.xMax), y = Mathf.Clamp(p.y, r.yMin, r.yMax);
        float dl = Mathf.Abs(x - r.xMin), dr = Mathf.Abs(r.xMax - x), db = Mathf.Abs(y - r.yMin), dt = Mathf.Abs(r.yMax - y);
        float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
        if (m == db) return x - r.xMin;
        if (m == dr) return w + (y - r.yMin);
        if (m == dt) return w + h + (r.xMax - x);
        return 2f * w + h + (r.yMax - y);
    }
}
