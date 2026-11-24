using System.Collections.Generic;
using UnityEngine;

// 맵 중앙 텐트의 초록 땅 영역: 울타리로 막아 플레이어와 모든 몬스터가 들어가지 못함
// - 돌 테두리를 따라 나무 울타리, 중앙 길 쪽 문은 닫힌 상태
// - 몬스터는 영역에 막히면 모서리로 돌아서 추적 (Steer)
// - 보스 낙하·순간이동, 중간보스(키네마틱) 등이 영역 안에 들어가 있으면 밖으로 밀어냄
// - 드롭 아이템·설치물은 장애물(텐트, 동굴, 울타리 등)에 겹치지 않는 가까운 자리로 옮김 (FreeSpot)
// 수치는 RunConfig의 "텐트 울타리" 항목
// 울타리·문 그림은 씬에 직접 배치 (코드는 출입 금지 벽만 만들고, 직접 배치한 오브젝트는 지우지 않음)
[ExecuteAlways]
public class CampZone : MonoBehaviour
{
    public static CampZone Instance { get; private set; }

    private RunConfig rc;
    private Rect area;                                         // 막힌 영역 (월드)
    private readonly List<Bounds> coverBounds = new List<Bounds>();   // 아이템이 가려지는 큰 맵 그림 (텐트)
    private float nextScan;
    private readonly FlowField flow = new FlowField();         // 울타리 주변 몬스터 길찾기
    private readonly FlowField flowBig = new FlowField();      // 중간보스·보스처럼 몸이 큰 대상용 (벽에서 더 떨어진 길)
    private float nextFlow, nextFlowBig;
    public static FlowField Flow => Instance != null ? Instance.flow : null;

    public static Rect Area => Instance != null ? Instance.area : Rect.zero;

    // 씬에 Camp Zone이 없을 때만 새로 만듦 (게임 시작 때 호출)
    public static void Create()
    {
        if (Instance != null) return;
        new GameObject("Camp Zone").AddComponent<CampZone>();
    }

    private void OnEnable()
    {
        ClearChildren();
        rc = RunConfig.Instance;
        if (rc == null) return;
        area = FenceArea();
        if (Application.isPlaying) Build();                  // 울타리 그림은 씬에 직접 배치한 것을 그대로 사용
    }

    [Tooltip("켜면 출입 금지 영역을 이 오브젝트 아래에 직접 배치한 울타리 위치(발밑 기준점)에 맞춤. 끄면 RunConfig의 campZoneMin/Max 사용")]
    public bool fitAreaToFence = true;
    [Tooltip("울타리 기준점에서 영역을 넓힐 양 (x: 좌우, y: 위아래). 기둥 두께만큼 넓히면 캐릭터가 기둥에 파묻히지 않음")]
    public Vector2 fenceAreaPadding = new Vector2(0.35f, 0.05f);

    // 출입 금지 영역: 직접 배치한 울타리 그림들의 기준점을 감싸는 사각형 (울타리가 없으면 RunConfig 값)
    private Rect FenceArea()
    {
        Rect fallback = Rect.MinMaxRect(rc.campZoneMin.x, rc.campZoneMin.y, rc.campZoneMax.x, rc.campZoneMax.y);
        if (!fitAreaToFence) return fallback;
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.GetComponentInParent<CampZoneGenerated>() != null) continue;
            Vector2 p = r.transform.position;
            if (!any) { min = max = p; any = true; }
            else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        }
        if (!any) return fallback;
        return Rect.MinMaxRect(min.x - fenceAreaPadding.x, min.y - fenceAreaPadding.y, max.x + fenceAreaPadding.x, max.y + fenceAreaPadding.y);
    }

    // 씬 뷰에서 출입 금지 영역을 빨간 사각형으로 표시
    private void OnDrawGizmosSelected()
    {
        if (rc == null) rc = RunConfig.Instance;
        if (rc == null) return;
        Rect a = Application.isPlaying ? area : FenceArea();
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(a.center, a.size);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) ClearChildren();
    }

    // 코드가 만든 오브젝트만 지움 (직접 배치한 울타리는 절대 지우지 않음)
    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.GetComponent<CampZoneGenerated>() == null) continue;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private Collider2D wallCollider;                           // 출입 금지 벽
    private BossPattern passBoss;                              // 벽 충돌을 꺼 둔 보스

    private void Build()
    {
        Instance = this;

        // 출입 금지 벽 (플레이어·몬스터 모두 막는 Default 레이어, 장애물로 인식되도록 Wall 태그)
        GameObject wall = new GameObject("Camp Fence Wall");
        wall.AddComponent<CampZoneGenerated>();
        wall.transform.SetParent(transform, false);
        wall.layer = 0;
        wall.tag = "Wall";
        BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
        wallCollider = box;
        box.offset = area.center;
        box.size = area.size;

        // 텐트 그림은 울타리 위로 솟아 있어 그 뒤에 떨어진 아이템이 가려짐 → 드롭 금지 범위에 포함
        foreach (SpriteRenderer r in FindObjectsOfType<SpriteRenderer>())
        {
            if (rc.tentSprite != null && r.sprite == rc.tentSprite) coverBounds.Add(r.bounds);
        }
    }

    private void Start()
    {
        if (Application.isPlaying && Instance == this)
        {
            // 장애물 지도는 맵 충돌체가 다 준비된 뒤 한 번만 (몬스터가 부딪히는 층만, 몬스터끼리는 제외)
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int mask = enemyLayer >= 0 ? Physics2D.GetLayerCollisionMask(enemyLayer) & ~(1 << enemyLayer) : Physics2D.DefaultRaycastLayers;
            flow.Init(Expand(area, rc.flowMargin), rc.flowCellSize, mask, rc.flowInflate);
            flowBig.Init(Expand(area, rc.flowMargin), rc.flowCellSize, mask, rc.flowBigInflate);
            nextFlowBig = Time.time + rc.flowInterval * 0.5f;     // 두 흐름장 계산이 같은 프레임에 몰리지 않게
            ShopKeeper.Create(new Vector3(area.center.x, area.yMin, 0f) + (Vector3)rc.shopperOffset);   // 낮이 되면 등장
            CampLoiterPenalty.Create(transform);               // 낮에 울타리 근처를 계속 돌면 일반 몬스터 압박 강화
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    // ---------------- 영역 밖으로 밀어내기 (보스 낙하·순간이동, 키네마틱 중간보스 등) ----------------
    private void LateUpdate()
    {
        if (!Application.isPlaying || GameManager.instance == null || !GameManager.instance.isLive)
            return;

        // 흐름장 갱신 (플레이어 위치 기준, 일정 간격)
        if (Time.time >= nextFlow && GameManager.instance.player != null)
        {
            nextFlow = Time.time + rc.flowInterval;
            flow.Build(GameManager.instance.player.transform.position);
        }
        else if (Time.time >= nextFlowBig && GameManager.instance.player != null)
        {
            nextFlowBig = Time.time + rc.flowInterval;
            flowBig.Build(GameManager.instance.player.transform.position);
        }

        if (Time.time < nextScan)
            return;
        nextScan = Time.time + 0.1f;

        Rect inner = Expand(area, -0.05f);
        Player p = GameManager.instance.player;
        if (p != null) Eject(p.GetComponent<Rigidbody2D>(), p.transform, inner);
        foreach (Enemy e in FindObjectsOfType<Enemy>())
            Eject(e.GetComponent<Rigidbody2D>(), e.transform, inner);
        foreach (MiniBoss m in new List<MiniBoss>(MiniBoss.Alive))
            if (m != null) Eject(m.GetComponent<Rigidbody2D>(), m.transform, inner);
        // 보스는 큰 몬스터라 텐트 울타리를 그냥 지나감 (벽 충돌 끔, 밀어내지 않음)
        BossPattern boss = BossPattern.Current;
        if (boss != null && boss != passBoss && wallCollider != null)
        {
            foreach (Collider2D c in boss.GetComponentsInChildren<Collider2D>(true))
            {
                Physics2D.IgnoreCollision(c, wallCollider, true);
            }
            passBoss = boss;
        }
    }

    private void Eject(Rigidbody2D rb, Transform t, Rect inner)
    {
        Vector2 pos = rb != null ? rb.position : (Vector2)t.position;
        if (!inner.Contains(pos)) return;
        Vector2 outside = PushOut(pos, rc.campEjectMargin);
        if (rb != null) rb.position = outside;
        t.position = new Vector3(outside.x, outside.y, t.position.z);
    }

    private static Rect Expand(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

    // 다음 위치가 울타리 영역(몸 반지름만큼 넓힌)에 들어가면 가장자리 바깥으로 밀어 벽을 따라 미끄러지게
    // (충돌을 무시하고 움직이는 키네마틱 중간보스가 울타리에 박혔다 튕겨 나오며 끼는 것 방지)
    public static Vector2 SlideOutside(Vector2 next, float radius)
    {
        if (Instance == null) return next;
        Rect r = Expand(Instance.area, radius);
        if (!r.Contains(next)) return next;
        float dl = next.x - r.xMin, dr = r.xMax - next.x, db = next.y - r.yMin, dt = r.yMax - next.y;
        float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
        if (m == dl) return new Vector2(r.xMin, next.y);
        if (m == dr) return new Vector2(r.xMax, next.y);
        if (m == db) return new Vector2(next.x, r.yMin);
        return new Vector2(next.x, r.yMax);
    }

    // 지금 위치(pos)에서 step만큼 움직일 때 울타리 영역(몸 반지름만큼 넓힌)에 막히면, 붙어 있는 변을 따라 미끄러짐
    // - 벽을 정면으로 밀면(변을 따라가는 성분이 거의 없으면) 가까운 모서리 쪽으로 돌아 나감 (모서리에서 멈춰 끼지 않게)
    // - 속도는 줄지 않음 (벽을 따라 같은 거리만큼 이동)
    public static Vector2 SlideAround(Vector2 pos, Vector2 step, float radius)
    {
        if (Instance == null) return pos + step;
        Rect r = Expand(Instance.area, radius);
        Vector2 next = pos + step;
        if (!r.Contains(next)) return next;
        if (r.Contains(pos)) return SlideOutside(next, radius);   // 이미 안에 들어와 있으면 가장 가까운 바깥으로

        float len = step.magnitude;
        bool left = pos.x <= r.xMin, right = pos.x >= r.xMax, below = pos.y <= r.yMin, above = pos.y >= r.yMax;
        // 모서리 바깥(두 변 모두 바깥): 한 축만 움직여 보고 바깥에 남는 쪽
        if ((left || right) && (below || above))
        {
            Vector2 onlyX = new Vector2(next.x, pos.y), onlyY = new Vector2(pos.x, next.y);
            bool xOk = !r.Contains(onlyX), yOk = !r.Contains(onlyY);
            if (xOk && (!yOk || Mathf.Abs(step.x) >= Mathf.Abs(step.y))) return onlyX;
            if (yOk) return onlyY;
            return pos;
        }
        if (left || right)
        {
            // 좌우 벽: 위아래로 미끄러짐
            float dir = Mathf.Abs(step.y) > len * 0.2f ? Mathf.Sign(step.y) : (pos.y - r.yMin < r.yMax - pos.y ? -1f : 1f);
            return new Vector2(left ? r.xMin : r.xMax, pos.y + dir * len);
        }
        // 위아래 벽: 좌우로 미끄러짐
        float d = Mathf.Abs(step.x) > len * 0.2f ? Mathf.Sign(step.x) : (pos.x - r.xMin < r.xMax - pos.x ? -1f : 1f);
        return new Vector2(pos.x + d * len, below ? r.yMin : r.yMax);
    }

    // 영역 안(여유 margin 포함)이면 가장 가까운 가장자리 바깥으로
    public static Vector2 PushOut(Vector2 p, float margin)
    {
        if (Instance == null) return p;
        Rect r = Expand(Instance.area, margin);
        if (!r.Contains(p)) return p;
        float dl = p.x - r.xMin, dr = r.xMax - p.x, db = p.y - r.yMin, dt = r.yMax - p.y;
        float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
        if (m == dl) return new Vector2(r.xMin - 0.01f, p.y);
        if (m == dr) return new Vector2(r.xMax + 0.01f, p.y);
        if (m == db) return new Vector2(p.x, r.yMin - 0.01f);
        return new Vector2(p.x, r.yMax + 0.01f);
    }

    // ---------------- 몬스터 우회 ----------------
    // from → target 직선이 영역에 막히면, 영역 모서리를 거쳐 돌아가도록 중간 지점을 돌려줌
    // alternate: 가장 가까운 모서리 쪽이 다른 지형에 막혀 멈췄을 때 반대쪽(두 번째) 모서리로
    // big: 중간보스·보스처럼 몸이 큰 대상 (벽에서 더 떨어진 흐름장 사용)
    public static Vector2 Steer(Vector2 from, Vector2 target, bool alternate = false, bool big = false)
    {
        if (Instance == null) return target;
        Rect r = Expand(Instance.area, Instance.rc.campSteerMargin);
        // 실제 막힌 영역을 지나지 않으면 그대로 (플레이어가 울타리에 붙어 여유 띠 안에 있어도 곧장 다가감)
        if (!SegmentHitsRect(from, target, Expand(Instance.area, -0.05f))) return target;

        // 흐름장: 격자에서 플레이어까지 가장 짧은 길을 따라 (서로 다른 길로 자연스럽게 나뉘어 돌아감)
        FlowField field = big ? Instance.flowBig : Instance.flow;
        if (field.TryDirection(from, out Vector2 flowDir)) return from + flowDir * 2f;

        Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
        Rect inner = Expand(Instance.area, -0.05f);              // 실제 막힌 영역 (여유 띠 안에 있어도 모서리로 갈 수 있게)
        float best = float.MaxValue, second = float.MaxValue;
        Vector2 pick = target, pick2 = target;
        foreach (Vector2 c in corners)
        {
            Vector2 corner = c + (c - r.center).normalized * 0.4f;   // 모서리 살짝 바깥
            if (SegmentHitsRect(from, corner, inner)) continue;      // 지금 위치에서 바로 갈 수 있는 모서리만
            float cost = Vector2.Distance(from, corner) + Vector2.Distance(corner, target);
            if (cost < best) { second = best; pick2 = pick; best = cost; pick = corner; }
            else if (cost < second) { second = cost; pick2 = corner; }
        }
        if (alternate && second < float.MaxValue) pick = pick2;
        // 모서리에 거의 다 왔으면 다음 모서리 대신 목표 쪽으로 (모서리에서 맴돌지 않게)
        return Vector2.Distance(from, pick) < 0.3f ? target : pick;
    }

    // ---------------- 돌진용 ----------------
    // a → b 직선이 울타리 영역(radius만큼 넓힌)을 지나는지 (돌진 전 시야 확인)
    public static bool LineBlocked(Vector2 a, Vector2 b, float radius)
    {
        return Instance != null && SegmentHitsRect(a, b, Expand(Instance.area, radius));
    }

    // p가 울타리 영역(radius만큼 넓힌) 안에 있는지 (돌진 중 벽에 닿았는지)
    public static bool Overlaps(Vector2 p, float radius)
    {
        return Instance != null && Expand(Instance.area, radius).Contains(p);
    }

    // 시야가 막혔을 때 질러갈 모서리: 지금 위치에서 곧장 갈 수 있고, 거기서 목표가 보이는 모서리 중 가장 짧은 길
    // (목표가 보이는 모서리가 없으면 갈 수 있는 모서리 중 가장 짧은 길)
    public static bool ChargeCorner(Vector2 from, Vector2 target, float margin, out Vector2 corner)
    {
        corner = target;
        if (Instance == null) return false;
        Rect r = Expand(Instance.area, margin);
        Rect inner = Expand(Instance.area, -0.05f);
        Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
        float best = float.MaxValue;
        bool bestSees = false, found = false;
        foreach (Vector2 c in corners)
        {
            if (SegmentHitsRect(from, c, inner)) continue;
            if (Vector2.Distance(from, c) < 0.5f) continue;            // 이미 모서리에 있으면 제외
            bool sees = !SegmentHitsRect(c, target, inner);
            float cost = Vector2.Distance(from, c) + Vector2.Distance(c, target);
            if ((sees && !bestSees) || (sees == bestSees && cost < best))
            {
                best = cost; bestSees = sees; corner = c; found = true;
            }
        }
        return found;
    }

    private static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Approximately(p, 0f)) return q >= 0f;
        float t = q / p;
        if (p < 0f) t0 = Mathf.Max(t0, t);
        else t1 = Mathf.Min(t1, t);
        return t0 <= t1;
    }

    // 선분이 사각형 내부를 지나는지 (Liang–Barsky)
    private static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
    {
        Vector2 d = b - a;
        float t0 = 0f, t1 = 1f;
        return Clip(-d.x, a.x - r.xMin, ref t0, ref t1) && Clip(d.x, r.xMax - a.x, ref t0, ref t1) &&
               Clip(-d.y, a.y - r.yMin, ref t0, ref t1) && Clip(d.y, r.yMax - a.y, ref t0, ref t1);   // 몬스터마다 매 프레임 불리므로 배열 할당 없이
    }

    // ---------------- 드롭·설치 위치 보정 ----------------
    // 맵 장애물(텐트·상자·모닥불·동굴·울타리 등, 플레이어가 지나갈 수 없는 것)과 겹치면 가까운 빈자리로
    public static Vector3 FreeSpot(Vector3 position, float radius)
    {
        if (!Blocked(position, radius)) return position;
        for (float dist = 0.4f; dist <= 10f; dist += 0.4f)
        {
            int n = Mathf.Max(8, Mathf.RoundToInt(dist * 8f));
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float a = (start + 360f * i / n) * Mathf.Deg2Rad;
                Vector3 p = position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * dist;
                if (!Blocked(p, radius)) return p;
            }
        }
        return position;
    }

    private static readonly int BlockPlayerLayer = 3;

    public static bool Blocked(Vector3 position, float radius)
    {
        if (Instance != null)
        {
            if (Expand(Instance.area, radius).Contains(position)) return true;
            foreach (Bounds b in Instance.coverBounds)
            {
                if (Expand(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y), radius).Contains(position)) return true;
            }
        }
        foreach (Collider2D c in Physics2D.OverlapCircleAll(position, radius))
        {
            if (c.isTrigger || c.CompareTag("Ground")) continue;
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) continue;   // 캐릭터·몬스터는 장애물 아님
            if (c.gameObject.layer == BlockPlayerLayer || c.CompareTag("Wall")) return true;
        }
        return false;
    }
}
