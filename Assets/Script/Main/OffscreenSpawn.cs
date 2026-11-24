using UnityEngine;

// 메인 카메라 화면 바로 밖 랜덤 위치 (일반 몬스터·중간보스 공용)
// - 방향은 360도 무작위, 맵 벽에 막혀 위치가 크게 밀리는 방향은 피함
// - 위치는 맵 벽 안쪽으로 맞춤 (벽 범위는 한 번 계산해 두고 재사용)
public static class OffscreenSpawn
{
    private static Bounds wallBounds;
    private static bool hasWalls;
    private static int cachedFrame = -1;
    private static float lastAngle = float.NaN;

    // 화면 밖 랜덤 위치 (일반 몬스터)
    public static Vector2 RandomPoint(Vector2 playerPos, float margin)
    {
        float best = Random.Range(0f, 360f);
        float bestPush = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            float a = i == 0 ? best : Random.Range(0f, 360f);
            Vector2 p = Edge(playerPos, a, margin);
            float push = Vector2.Distance(p, ClampToMap(p));
            if (push < bestPush) { bestPush = push; best = a; }
            if (push < 0.01f) break;
        }
        return ClampToMap(Edge(playerPos, best, margin));
    }

    // 등장 방향 (도): 직전 방향과 gap 이상 벌어지고 벽에 막히지 않는 쪽 우선 (중간보스)
    public static float PickAngle(Vector2 playerPos, float margin, float gap)
    {
        float best = Random.Range(0f, 360f);
        float bestScore = float.MinValue;
        for (int tryCount = 0; tryCount < 12; tryCount++)
        {
            float a = Random.Range(0f, 360f);
            Vector2 p = Edge(playerPos, a, margin);
            float pushed = Vector2.Distance(p, ClampToMap(p));
            float diff = float.IsNaN(lastAngle) ? 180f : Mathf.Abs(Mathf.DeltaAngle(a, lastAngle));
            float score = -pushed * 3f + (diff >= gap ? 10f : diff / Mathf.Max(1f, gap) * 5f) + Random.value;
            if (score > bestScore) { bestScore = score; best = a; }
        }
        lastAngle = best;
        return best;
    }

    // 플레이어 기준 angle(도) 방향, 화면 테두리에서 margin 만큼 바깥
    public static Vector2 Edge(Vector2 playerPos, float angle, float margin)
    {
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Camera cam = Camera.main;
        float halfH = cam != null ? cam.orthographicSize : 8f;
        float halfW = cam != null ? halfH * cam.aspect : 5f;
        float tx = Mathf.Abs(dir.x) > 0.001f ? (halfW + margin) / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 0.001f ? (halfH + margin) / Mathf.Abs(dir.y) : float.MaxValue;
        return playerPos + dir * Mathf.Min(tx, ty);
    }

    // 맵 벽 안쪽으로 (벽 밖에서 나오지 않게)
    public static Vector2 ClampToMap(Vector2 p)
    {
        // 새 판(프레임 번호가 처음부터)이거나 처음이면 벽 범위 다시 계산
        if (cachedFrame < 0 || Time.frameCount < cachedFrame)
        {
            hasWalls = false;
            foreach (Collider2D col in Object.FindObjectsOfType<Collider2D>())
            {
                if (!col.CompareTag("Wall"))
                    continue;
                if (!hasWalls) { wallBounds = col.bounds; hasWalls = true; } else wallBounds.Encapsulate(col.bounds);
            }
            cachedFrame = Time.frameCount;
        }
        if (!hasWalls)
            return p;
        const float m = 2f;
        return new Vector2(Mathf.Clamp(p.x, wallBounds.min.x + m, wallBounds.max.x - m), Mathf.Clamp(p.y, wallBounds.min.y + m, wallBounds.max.y - m));
    }

    // 맵 벽 안쪽 범위 (ClampToMap과 같은 기준, 군락 생성 위치 계산용)
    public static bool TryGetMapRect(out Rect rect)
    {
        ClampToMap(Vector2.zero);                            // 벽 범위 계산 (캐시)
        const float m = 2f;
        rect = hasWalls ? Rect.MinMaxRect(wallBounds.min.x + m, wallBounds.min.y + m, wallBounds.max.x - m, wallBounds.max.y - m) : Rect.zero;
        return hasWalls;
    }

    // 새 판 시작 시 (벽 범위·직전 방향 초기화)
    public static void ResetRun()
    {
        cachedFrame = -1;
        lastAngle = float.NaN;
    }
}
