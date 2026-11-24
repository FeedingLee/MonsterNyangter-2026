using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 낮 외곽 군락 (지시서 1120_군락_시스템, 기획서 v2.1 2.3~2.5)
// A 생성: 2~4일 차 낮 시작(아침 선택이 닫힌 뒤) colonyCount곳, 둥지 습격이면 nestCount곳
// C 호위: 둥지 주변 대기 → 플레이어 접근·공격받으면 무리 전체 추적
// E 낮 종료(귀환 준비 시작): 못 깬 둥지는 붉은 기운 후 사라지고 이번 밤 체력 '증가 합'에 페널티, 호위는 보상 없이 소멸
public class ColonySystem : MonoBehaviour
{
    public static ColonySystem Instance { get; private set; }

    // 오늘 둥지 습격 적용 중 (아침 선택지, 아침에 해제)
    public static bool RaidActive => RunStats.Get(RunStat.ColonyRaid) > 0f;

    // 둥지 하나의 호위 무리
    public class Group
    {
        public readonly List<Enemy> guards = new List<Enemy>();
        public Vector2 home;
        public bool alerted;

        public void Alert()
        {
            if (alerted) return;
            alerted = true;
            foreach (Enemy e in guards)
                if (e != null) e.WakeGuard();
        }
    }

    private RunConfig rc;
    private GameManager gm;
    private int generatedDay = -1;
    private readonly List<Group> groups = new List<Group>();
    private Sprite[][] nestFrames;

    private Canvas canvas;
    private Text warning;
    private float warningUntil;
    private readonly List<Image> indicators = new List<Image>();
    private readonly List<Image> indicatorIcons = new List<Image>();
    private readonly List<Image> indicatorArrows = new List<Image>();

    // [임시] 생성 제외 구역 표시 (테스트 후 삭제)
    public static bool ShowZones;
    private GameObject zoneRoot;

    private void Awake()
    {
        Instance = this;
        rc = RunConfig.Instance;
        ColonyNest.Alive.Clear();
        DayNightCycle.ReturnPrepStarted += OnDayEnd;
        DayNightCycle.BossStarted += OnDayEnd;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DayNightCycle.ReturnPrepStarted -= OnDayEnd;
        DayNightCycle.BossStarted -= OnDayEnd;
    }

    private void Start()
    {
        gm = GameManager.instance;
        BuildUi();
    }

    private void Update()
    {
        DayNightCycle dn = DayNightCycle.Instance;
        if (gm != null && gm.isLive && dn != null && !MorningChoice.IsOpen && dn.CurrentPhase == DayNightCycle.Phase.Day
            && generatedDay != dn.DayCount && dn.DayCount >= rc.colonyFirstDay && dn.DayCount <= rc.colonyLastDay && !gm.isBossSpawn)
        {
            generatedDay = dn.DayCount;
            Generate(RaidActive ? RunConfig.Instance.morningCards.nestCount : rc.colonyCount);
        }
        // 귀환 준비 없이 밤이 된 경우(끝없는 밤 등)에도 정리
        if (DayNightCycle.IsNight && (ColonyNest.Alive.Count > 0 || groups.Count > 0)) OnDayEnd();
        UpdateUi();
        UpdateZones();
    }

    // ---------------------------------------------------------------
    // A. 생성
    // ---------------------------------------------------------------
    public void SpawnNow()                                   // 테스트 모드: 현재 일차 기준 즉시 생성
    {
        DayNightCycle dn = DayNightCycle.Instance;
        if (dn != null) generatedDay = dn.DayCount;
        Generate(RaidActive ? RunConfig.Instance.morningCards.nestCount : rc.colonyCount);
    }

    public void BreakAllNow()                                // 테스트 모드: 보상 확인
    {
        foreach (ColonyNest n in new List<ColonyNest>(ColonyNest.Alive)) n.Break();
    }

    private void Generate(int count)
    {
        Spawner spawner = gm.player.GetComponentInChildren<Spawner>(true);
        if (spawner == null || spawner.spawnData == null || spawner.spawnData.Length == 0)
            return;
        SpawnData data = spawner.CurrentData;
        List<Vector2> placed = new List<Vector2>();
        foreach (ColonyNest n in ColonyNest.Alive) placed.Add(n.transform.position);
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            if (!FindSpot(placed, out Vector2 pos))
            {
                Debug.LogWarning("[군락] 생성 위치를 찾지 못했습니다");
                break;
            }
            placed.Add(pos);
            Group group = new Group { home = pos };
            groups.Add(group);
            ColonyNest.Create(pos, data.health * rc.colonyHpMul, FramesFor(data.spriteType), group);   // 그날 몬스터 종류의 둥지 그림
            made++;
            for (int k = 0; k < rc.colonySpawnCount; k++)
            {
                // 둥지 주변에 자연스럽게 흩어진 자기 자리 (같은 간격 고리가 아니라 방향·거리 모두 무작위)
                Vector2 spot = RingPoint(rc.colonyGuardMinRadius, rc.colonyGuardMaxRadius);
                Vector2 p = pos + spot;
                p = OffscreenSpawn.ClampToMap(p);
                p = CampZone.FreeSpot(p, 0.4f);
                Enemy e = spawner.SpawnDayMonsterAt(p);       // 그날 낮 일반 몬스터 (엘리트 확률은 낮 스폰 규칙 그대로)
                if (e == null) continue;
                e.MakeColonyGuard(group, spot);                    // 은신 보관 제외 (DayMonsterStash), 처벌 스택 없음 (ApplyToSpawn 안 거침)
                group.guards.Add(e);
            }
        }
        if (made > 0) Announce();
    }

    // 군락 등장 알림: 문구 + 효과음 + 방향 표시 강조
    private float appearUntil, pulseUntil;
    private void Announce()
    {
        appearUntil = Time.unscaledTime + rc.colonyAppearTime;
        pulseUntil = Time.unscaledTime + rc.colonyAppearPulseTime;
        if (rc.colonyAppearSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(rc.colonyAppearSound);
    }

    // 맵 경계·외곽 한계선 안쪽, 기지에서 colonyMinDist 이상, 캠프·루핑 경고 영역·밤 길·포탑 발판 주변 제외, 둥지끼리 colonyMinDist 이상
    private bool FindSpot(List<Vector2> others, out Vector2 result)
    {
        result = Vector2.zero;
        if (!OffscreenSpawn.TryGetMapRect(out Rect map))
            return false;
        Rect inner = Rect.MinMaxRect(map.xMin + rc.colonyEdgeMargin, map.yMin + rc.colonyEdgeMargin, map.xMax - rc.colonyEdgeMargin, map.yMax - rc.colonyEdgeMargin);
        if (inner.width <= 0f || inner.height <= 0f)
            return false;
        for (int relax = 0; relax < 2; relax++)
        {
            float k = relax == 0 ? 1f : 0.6f;                // 못 찾으면 제외 여유만 줄여 한 번 더
            for (int tries = 0; tries < 400; tries++)
            {
                Vector2 p = new Vector2(Random.Range(inner.xMin, inner.xMax), Random.Range(inner.yMin, inner.yMax));
                if (Blocked(p, others, k)) continue;
                result = p;
                return true;
            }
        }
        return false;
    }

    private bool Blocked(Vector2 p, List<Vector2> others, float k)
    {
        DayNightCycle dn = DayNightCycle.Instance;
        if (dn != null && Vector2.Distance(p, dn.MapCenter) < rc.colonyMinDist) return true;
        foreach (Vector2 o in others)
            if (Vector2.Distance(p, o) < rc.colonyMinDist) return true;
        if (gm != null && gm.player != null && Vector2.Distance(p, gm.player.transform.position) < rc.colonyPlayerClear * k) return true;
        Rect camp = CampRect();
        float c = rc.colonyCampClear * k;
        if (Rect.MinMaxRect(camp.xMin - c, camp.yMin - c, camp.xMax + c, camp.yMax + c).Contains(p)) return true;
        WavePath path = WavePath.Instance;
        if (path != null)
            for (float d = 0f; d <= path.Length; d += 0.5f)
                if (Vector2.Distance(p, path.PositionAt(d)) < rc.colonyPathClear * k) return true;
        TurretConfig tc = TurretConfig.Instance;
        if (tc != null && tc.padCenters != null)
            foreach (Vector2 pad in tc.padCenters)
                if (Vector2.Distance(p, pad) < rc.colonyPadClear * k) return true;
        return false;
    }

    // 둥지 그림을 덮지 않는 고리 안 무작위 점
    public static Vector2 RingPoint(float min, float max)
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Random.Range(min * min, Mathf.Max(min, max) * Mathf.Max(min, max)));
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    // 고리 위 자기 자리 근처 점 (각도 ±spread, 반지름 min~max)
    public static Vector2 SlotPoint(float angleDeg, float min, float max, float spread)
    {
        float a = (angleDeg + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
        float r = Random.Range(min, Mathf.Max(min, max));
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    private static Rect CampRect() => CampLoiterPenalty.Instance != null ? CampLoiterPenalty.Instance.Zone : CampZone.Area;

    // 둥지 그림: Mob_colony 시트(4종 x 6프레임)를 실행 중에 잘라 씀 (원본 이미지·메타 수정 없음)
    private Sprite[] FramesFor(int spriteType)
    {
        LoadFrames();
        int[] map = rc.colonyRowBySpriteType;
        int row = map != null && spriteType >= 0 && spriteType < map.Length ? map[spriteType] : -1;
        if (row < 0 || row >= nestFrames.Length) row = Random.Range(0, nestFrames.Length);
        return nestFrames[row];
    }

    private void LoadFrames()
    {
        if (nestFrames == null)
        {
            Texture2D tex = rc.colonySheet != null ? rc.colonySheet.texture : null;
            int rows = Mathf.Max(1, rc.colonySheetRows), cols = Mathf.Max(1, rc.colonySheetCols);
            nestFrames = new Sprite[rows][];
            for (int r = 0; r < rows; r++)
            {
                nestFrames[r] = new Sprite[tex != null ? cols : 0];
                if (tex == null) continue;
                float w = tex.width / (float)cols, h = tex.height / (float)rows;
                for (int c = 0; c < cols; c++)
                {
                    Rect rect = new Rect(c * w, (rows - 1 - r) * h, w, h);   // 위 줄부터
                    nestFrames[r][c] = Sprite.Create(tex, rect, new Vector2(0.5f, rc.colonyPivotY), w / Mathf.Max(0.1f, rc.colonyNestWidth));
                    nestFrames[r][c].name = "Mob_colony_" + r + "_" + c;
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // E. 낮 종료
    // ---------------------------------------------------------------
    private void OnDayEnd()
    {
        int failed = 0;
        foreach (ColonyNest n in new List<ColonyNest>(ColonyNest.Alive))
        {
            n.FailAndVanish();
            failed++;
        }
        foreach (Group g in groups)
            foreach (Enemy e in g.guards)
                if (e != null && e.ColonyGuard && e.gameObject.activeSelf) e.Vanish();   // 보상 없이 즉시 소멸
        groups.Clear();

        bool boss = gm != null && gm.isBossSpawn;
        if (failed > 0 && !boss)
        {
            // 이번 밤 웨이브 체력 '증가 합'에 더함 (피의 계약과 합산, 대장 체력에도 반영), 아침에 해제
            RunStats.Add(RunStat.NightHpAdd, rc.colonyPenaltyRate * failed, ModifierLife.UntilMorning);
            warning.text = string.Format(rc.colonyFailText, Mathf.RoundToInt(rc.colonyPenaltyRate * failed * 100f));
            warningUntil = Time.unscaledTime + rc.colonyWarningTime;
            if (rc.colonyWarningSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(rc.colonyWarningSound);
        }
    }

    // ---------------------------------------------------------------
    // 화면: 경고 문구, 화면 밖 둥지 방향 표시 (중간보스 표시와 같은 방식·그림)
    // ---------------------------------------------------------------
    private void BuildUi()
    {
        canvas = RunUI.CreateCanvas("Colony UI", 15, false);
        // 문구만 (판 없음, 화면을 덜 가림): 불투명 검정 외곽선 두 겹 (RunUI.Label 큰 글씨 규칙)
        warning = RunUI.Label("Colony Notice", canvas.transform, "", rc.colonyWarningFontSize, rc.colonyWarningColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, rc.colonyWarningY), new Vector2(900f, 70f), rc.colonyTextOutline);
        warning.gameObject.SetActive(false);
    }

    private void UpdateUi()
    {
        if (warning == null) return;
        bool fail = Time.unscaledTime < warningUntil;
        bool appear = !fail && Time.unscaledTime < appearUntil && gm != null && gm.isLive;   // 등장 문구 (미처치 경고가 우선)
        bool show = fail || appear;
        if (warning.gameObject.activeSelf != show) warning.gameObject.SetActive(show);
        if (show)
        {
            if (appear) warning.text = rc.colonyAppearText;
            float left = (fail ? warningUntil : appearUntil) - Time.unscaledTime;
            float fade = Mathf.Clamp01(left / 0.5f);           // 끝날 때만 함께 사라짐 (그 전에는 완전히 불투명)
            Color c = fail ? rc.colonyWarningColor : rc.colonyAppearColor;
            warning.color = new Color(c.r, c.g, c.b, c.a * fade);   // 외곽선도 글자 알파를 따라감
        }
        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        MiniBossConfig mc = MiniBossConfig.Instance;
        Camera cam = Camera.main;
        List<ColonyNest> targets = new List<ColonyNest>();
        if (cam != null && gm != null && gm.player != null && gm.isLive && Time.timeScale > 0f)
        {
            foreach (ColonyNest n in ColonyNest.Alive)
            {
                Vector3 vp = cam.WorldToViewportPoint(n.transform.position);
                if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f) continue;   // 화면 안이면 숨김
                targets.Add(n);
            }
        }
        float sz = mc.indicatorBaseSize * rc.colonyIndicatorScale;
        while (indicators.Count < targets.Count)
        {
            Sprite circleSprite = UiCircleSprite.Create();
            Image circle = RunUI.Icon("Colony Indicator", canvas.transform, mc.indicatorFrame != null ? mc.indicatorFrame : circleSprite,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz);
            circle.color = rc.colonyIndicatorFrameColor;
            if (mc.indicatorFrame == null)                       // 고리 그림이 없을 때만 둥근 바탕 (그림 고리 안에 원을 겹치면 어색함)
            {
                Image back = RunUI.Icon("Back", circle.transform, circleSprite, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.8f);
                back.color = rc.colonyIndicatorBackColor;
                back.raycastTarget = false;
            }
            Image face = RunUI.Icon("Nest", circle.transform, null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.7f);
            face.preserveAspect = true;
            Image arrow = RunUI.Icon("Arrow", circle.transform, mc.indicatorArrow, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.42f);
            arrow.preserveAspect = true;
            arrow.color = rc.colonyIndicatorArrowColor;
            arrow.gameObject.SetActive(mc.indicatorArrow != null);
            circle.gameObject.AddComponent<CanvasGroup>().alpha = rc.colonyIndicatorAlpha;
            indicators.Add(circle);
            indicatorIcons.Add(face);
            indicatorArrows.Add(arrow);
        }
        RectTransform area = (RectTransform)canvas.transform;
        Vector2 half = area.rect.size * 0.5f;
        for (int i = 0; i < indicators.Count; i++)
        {
            bool show = i < targets.Count;
            if (indicators[i].gameObject.activeSelf != show) indicators[i].gameObject.SetActive(show);
            if (!show) continue;
            ColonyNest n = targets[i];
            Vector3 vp = cam.WorldToViewportPoint(n.transform.position);
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            Vector2 inner = half - Vector2.one * (sz * 0.8f);
            float tx = Mathf.Abs(dir.x) > 0.0001f ? inner.x / Mathf.Abs(dir.x * area.rect.width) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.0001f ? inner.y / Mathf.Abs(dir.y * area.rect.height) : float.MaxValue;
            indicators[i].rectTransform.anchoredPosition = new Vector2(dir.x * area.rect.width, dir.y * area.rect.height) * Mathf.Min(tx, ty);
            float distance = Vector2.Distance(gm.player.transform.position, n.transform.position);
            float k = Mathf.InverseLerp(mc.indicatorDistNear, mc.indicatorDistFar, distance);
            float appearPulse = Time.unscaledTime < pulseUntil ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;   // 등장 직후 강조
            indicators[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(mc.indicatorScaleNear, mc.indicatorScaleFar, k) * appearPulse;
            indicatorIcons[i].sprite = n.Icon;
            indicatorIcons[i].enabled = n.Icon != null;
            Vector2 nd = new Vector2(dir.x * area.rect.width, dir.y * area.rect.height).normalized;
            float bob = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
            indicatorArrows[i].rectTransform.anchoredPosition = nd * sz * 0.62f * bob;
            indicatorArrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(nd.y, nd.x) * Mathf.Rad2Deg - 90f);
        }
    }

    // [임시] 생성 제외 구역 표시: 외곽 한계선(흰), 기지 최소 거리(노랑), 캠프(주황), 밤 길(빨강), 포탑 발판(하늘)
    private void UpdateZones()
    {
        if (ShowZones == (zoneRoot != null)) return;
        if (!ShowZones) { Destroy(zoneRoot); zoneRoot = null; return; }
        zoneRoot = new GameObject("Colony Zones (임시)");
        if (OffscreenSpawn.TryGetMapRect(out Rect map))
        {
            float m = rc.colonyEdgeMargin;
            DrawRect(Rect.MinMaxRect(map.xMin + m, map.yMin + m, map.xMax - m, map.yMax - m), Color.white);
        }
        DayNightCycle dn = DayNightCycle.Instance;
        if (dn != null) DrawCircle(dn.MapCenter, rc.colonyMinDist, Color.yellow);
        Rect camp = CampRect();
        float c = rc.colonyCampClear;
        DrawRect(Rect.MinMaxRect(camp.xMin - c, camp.yMin - c, camp.xMax + c, camp.yMax + c), new Color(1f, 0.6f, 0.1f));
        WavePath path = WavePath.Instance;
        if (path != null)
        {
            List<Vector3> pts = new List<Vector3>();
            for (float d = 0f; d <= path.Length; d += 0.5f) pts.Add(path.PositionAt(d));
            Line(pts.ToArray(), new Color(1f, 0.15f, 0.15f, 0.35f), rc.colonyPathClear * 2f, false);
        }
        TurretConfig tc = TurretConfig.Instance;
        if (tc != null && tc.padCenters != null) foreach (Vector2 pad in tc.padCenters) DrawCircle(pad, rc.colonyPadClear, new Color(0.4f, 0.85f, 1f));
    }

    private void DrawRect(Rect r, Color color)
    {
        Line(new Vector3[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) }, color, 0.15f, true);
    }

    private void DrawCircle(Vector2 center, float radius, Color color)
    {
        Vector3[] pts = new Vector3[48];
        for (int i = 0; i < pts.Length; i++) { float a = i * Mathf.PI * 2f / pts.Length; pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius; }
        Line(pts, color, 0.15f, true);
    }

    private void Line(Vector3[] pts, Color color, float width, bool loop)
    {
        GameObject go = new GameObject("Zone");
        go.transform.SetParent(zoneRoot.transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.sortingLayerName = "JOY STICK";
        lr.sortingOrder = 50;
    }
}
