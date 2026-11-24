using UnityEngine;

// 흐름장 길찾기: 격자 칸마다 "플레이어까지 가장 짧은 길" 거리를 미리 계산해 두고,
// 몬스터는 자기 칸에서 더 가까운 이웃 칸 쪽으로만 움직임 (울타리·나무 등 장애물을 자연스럽게 돌아감)
// - 장애물 지도는 한 번만 만들고, 거리 계산은 일정 간격으로 (배열은 재사용해 메모리 쓰레기 없음)
public class FlowField
{
    private const int Unreached = int.MaxValue;
    private const int StraightCost = 10, DiagonalCost = 14;
    private const int BlockedCost = 80;                        // 장애물 근처 칸: 지나갈 수는 있지만 매우 비쌈 (플레이어가 벽에 붙어 있어도 길이 생기게)
    private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private float cell;
    private Vector2 origin;                                    // 격자 왼쪽 아래 (월드)
    private int w, h;
    private bool[] blocked;                                    // 몸 크기만큼 넓힌 장애물 (비싸게 지나감)
    private bool[] solid;                                      // 장애물 한가운데 (절대 못 지나감)
    private int[] dist;
    private int[] heap;                                        // 다익스트라용 최소 힙 (칸 번호)
    private int heapCount;

    public bool Ready { get; private set; }
    public bool HasField { get; private set; }
    public int CellCount => w * h;
    public float LastMapMs { get; private set; }               // 장애물 지도 만드는 데 걸린 시간 (한 번)
    public float LastBuildMs { get; private set; }             // 거리 계산 1회 걸린 시간

    // bounds 안을 cellSize 칸으로 나누고, 몬스터 몸 크기(inflate)만큼 넓혀 장애물 칸을 표시
    public void Init(Rect bounds, float cellSize, int obstacleMask, Vector2 inflate)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        cell = Mathf.Max(0.1f, cellSize);
        origin = bounds.min;
        w = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cell));
        h = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cell));
        blocked = new bool[w * h];
        solid = new bool[w * h];
        dist = new int[w * h];
        heap = new int[w * h * 8];                             // 한 칸이 여러 번 들어갈 수 있음 (거리 갱신)

        Vector2 size = new Vector2(cell, cell) + inflate * 2f;
        Collider2D[] hits = new Collider2D[16];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 c = Center(x, y);
                int n = Physics2D.OverlapBoxNonAlloc(c, size, 0f, hits, obstacleMask);
                for (int i = 0; i < n; i++)
                {
                    Collider2D col = hits[i];
                    if (col.isTrigger) continue;
                    Rigidbody2D rb = col.attachedRigidbody;
                    if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) continue;   // 캐릭터·몬스터는 장애물 아님
                    blocked[y * w + x] = true;
                    break;
                }
                if (blocked[y * w + x])
                {
                    int m = Physics2D.OverlapBoxNonAlloc(c, new Vector2(cell * 0.5f, cell * 0.5f), 0f, hits, obstacleMask);
                    for (int i = 0; i < m; i++)
                    {
                        Collider2D col = hits[i];
                        if (col.isTrigger) continue;
                        Rigidbody2D rb = col.attachedRigidbody;
                        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) continue;
                        solid[y * w + x] = true;
                        break;
                    }
                }
            }
        }
        Ready = true;
        LastMapMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    private Vector2 Center(int x, int y) => origin + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);

    private bool ToCell(Vector2 p, out int x, out int y)
    {
        x = Mathf.FloorToInt((p.x - origin.x) / cell);
        y = Mathf.FloorToInt((p.y - origin.y) / cell);
        return x >= 0 && y >= 0 && x < w && y < h;
    }

    // target(플레이어)에서 퍼져 나가며 모든 칸의 거리를 계산
    public void Build(Vector2 target)
    {
        if (!Ready) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (!ToCell(target, out int tx, out int ty))
        {
            HasField = false;
            LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;
            return;
        }
        for (int i = 0; i < dist.Length; i++) dist[i] = Unreached;
        heapCount = 0;
        int start = ty * w + tx;
        dist[start] = 0;
        Push(start);

        while (heapCount > 0)
        {
            int cur = Pop();
            int cx = cur % w, cy = cur / w;
            int d = dist[cur];
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = ny * w + nx;
                if (solid[ni]) continue;
                if (k >= 4 && (solid[cy * w + nx] || solid[ny * w + cx])) continue;   // 모서리 깎아 지나가지 않게
                int nd = d + (k < 4 ? StraightCost : DiagonalCost) + (blocked[ni] ? BlockedCost : 0);
                if (nd < dist[ni])
                {
                    dist[ni] = nd;
                    Push(ni);
                }
            }
        }
        HasField = true;
        LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;
    }

    // pos에서 가야 할 방향 (거리가 더 짧은 이웃 칸 쪽). 계산 범위 밖이거나 길이 없으면 false
    public bool TryDirection(Vector2 pos, out Vector2 dir)
    {
        dir = Vector2.zero;
        if (!HasField || !ToCell(pos, out int x, out int y)) return false;
        int here = y * w + x;
        int best = dist[here];
        int bx = -1, by = -1;
        for (int k = 0; k < 8; k++)
        {
            int nx = x + Dx[k], ny = y + Dy[k];
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            int ni = ny * w + nx;
            if (solid[ni] || dist[ni] >= best) continue;
            if (k >= 4 && (solid[y * w + nx] || solid[ny * w + x])) continue;
            best = dist[ni];
            bx = nx; by = ny;
        }
        if (bx < 0) return false;                              // 길이 없거나 이미 목표 칸
        Vector2 to = Center(bx, by) - pos;
        if (to.sqrMagnitude < 0.0001f) return false;
        dir = to.normalized;
        return true;
    }

    private void Push(int v)
    {
        int i = heapCount++;
        heap[i] = v;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (dist[heap[p]] <= dist[heap[i]]) break;
            int t = heap[p]; heap[p] = heap[i]; heap[i] = t;
            i = p;
        }
    }

    private int Pop()
    {
        int top = heap[0];
        heap[0] = heap[--heapCount];
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, m = i;
            if (l < heapCount && dist[heap[l]] < dist[heap[m]]) m = l;
            if (r < heapCount && dist[heap[r]] < dist[heap[m]]) m = r;
            if (m == i) break;
            int t = heap[m]; heap[m] = heap[i]; heap[i] = t;
            i = m;
        }
        return top;
    }
}
