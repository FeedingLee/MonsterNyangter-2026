using System.Collections.Generic;
using UnityEngine;

// 맵 환경 장식 생성기 (매 판 다른 시드, 같은 시드 = 같은 배치)
// - Forest_object에서 잘라 둔 스프라이트를 무작위로 배치하되, 자연스럽게 보이도록 "장소" 단위로 묶음
//   숲 군락(같은 계열 나무 + 둘레 덤불·버섯·풀) / 바위 무더기 / 쓰러진 통나무 / 옛 사냥터 뼈
//   + 홀로 난 덤불, 풀·꽃 군락, 낙엽·작은 돌 흩뿌리기
// - Perlin 노이즈로 숲이 짙은 구역과 빈 구역을 나눔. 분류별 최소 간격으로 서로 겹치지 않게
// - 기존 맵 경계(Wall 태그 벽 4개) 안쪽에만, 시작 위치 주변·기존 나무·울타리·천막·덤불 타일은 피함
// - 충돌체 없음. 플레이어·몬스터보다 위(기존 씬 나무와 같은 레이어)에 그려 캐릭터가 나무를 뚫고 지나가 보이지 않음
public static class MapDecorGenerator
{
    public static int CurrentSeed { get; private set; }
    public static bool Generated => root != null;

    private static GameObject root;

    private struct Placed
    {
        public Vector2 position;
        public float spacing;
    }

    private static readonly List<Placed> placed = new List<Placed>();
    private static readonly List<Vector2> features = new List<Vector2>();
    private static readonly List<Rect> blockedRects = new List<Rect>();
    private static readonly List<Vector2> blockedPoints = new List<Vector2>();
    private static System.Random rng;
    private static Vector2 noiseOffset;
    private static Rect area;
    private static Vector2 spawnPoint;
    private static Vector2 campPoint;
    private static bool hasCamp;
    private static bool hasSpawnPoint;
    private static int objectCount;

    // 새 판 시작
    public static void NewRun()
    {
        hasSpawnPoint = false;
        GenerateRandom();
    }

    public static void GenerateRandom()
    {
        Generate(System.Environment.TickCount ^ Random.Range(int.MinValue, int.MaxValue));
    }

    // 지정한 시드로 생성 (기존 장식은 지우고 다시)
    public static void Generate(int seed)
    {
        MapDecorConfig c = MapDecorConfig.Instance;
        if (c == null)
            return;

        Clear();
        CurrentSeed = seed;
        rng = new System.Random(seed);
        noiseOffset = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
        objectCount = 0;
        placed.Clear();
        features.Clear();
        if (!FindArea(c.wallMargin))
            return;

        // 시작 위치는 처음 생성할 때 한 번만 기록 (같은 시드로 다시 만들 때 캐릭터가 움직였어도 같은 배치)
        if (!hasSpawnPoint)
        {
            GameManager gm = GameManager.instance;
            spawnPoint = gm != null ? (Vector2)gm.player.transform.position : Vector2.zero;
            GameObject camp = GameObject.Find(MapDecorConfig.Instance.campObjectName);   // 가운데 캠프
            hasCamp = camp != null;
            campPoint = Vector2.zero;
            if (hasCamp)
            {
                // 오브젝트 기준점이 아니라 캠프 그림(텐트·의자·모닥불 등) 전체 영역의 가운데
                Renderer[] parts = camp.GetComponentsInChildren<Renderer>();
                Bounds b = parts.Length > 0 ? parts[0].bounds : new Bounds(camp.transform.position, Vector3.zero);
                foreach (Renderer r in parts) b.Encapsulate(r.bounds);
                campPoint = b.center;
            }
            hasSpawnPoint = true;
        }
        CollectBlocked();

        root = new GameObject("Map Decor");
        Transform t = root.transform;

        // 키 큰 장식: 드물게 하나씩 (서로 lonelySpacing 이상 떨어짐)
        Lonely(c, t, c.tree, c.treeCount, c.treeSpacing, c.treeScale);
        Lonely(c, t, c.rock, c.rockCount, c.propSpacing, c.propScale);
        Lonely(c, t, c.log, c.logCount, c.propSpacing, c.propScale);
        Scatter(c, t, c.bush, c.looseBushCount, c.bushSpacing, c.bushScale, true, false);

        // 바닥 장식: 캐릭터가 위로 지나감
        for (int i = 0; i < c.boneSpotCount; i++) Feature(c, t, BoneSpot, false);
        PlacePlants(c, t);
        Scatter(c, t, c.mushroom, c.mushroomCount, c.propSpacing, c.propScale, true, true);
        Scatter(c, t, c.randomScatter, c.tinyCount, c.tinySpacing, c.tinyScale, false, true);
    }

    public static void Clear()
    {
        if (root != null)
        {
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
            root = null;
        }
    }

    // ---------------------------------------------------------------
    // 영역 / 막힌 곳
    // ---------------------------------------------------------------
    private static readonly List<Rect> interiorWalls = new List<Rect>();

    private static bool FindArea(float margin)
    {
        float left = float.MinValue, right = float.MaxValue, bottom = float.MinValue, top = float.MaxValue;
        Bounds all = new Bounds();
        bool any = false;
        List<Collider2D> walls = new List<Collider2D>();
        foreach (Collider2D col in Object.FindObjectsOfType<Collider2D>())
        {
            if (!col.CompareTag("Wall"))
                continue;
            walls.Add(col);
            if (!any) { all = col.bounds; any = true; } else all.Encapsulate(col.bounds);
        }
        if (!any)
            return false;

        interiorWalls.Clear();
        foreach (Collider2D col in walls)
        {
            Bounds b = col.bounds;
            // 맵 가장자리 벽(가늘고 긴 벽)만 영역을 정함. 캠프 울타리·숲 입구처럼 안쪽에 있는 벽은 피할 곳으로만
            // (안쪽 벽까지 가장자리로 보면 영역이 몇 칸 높이로 줄어들어 식생이 거의 안 나옴)
            float thin = Mathf.Min(b.size.x, b.size.y), length = Mathf.Max(b.size.x, b.size.y);
            float span = b.size.y > b.size.x ? all.size.y : all.size.x;
            if (thin > 3f || length < span * 0.5f)
            {
                interiorWalls.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
                continue;
            }
            if (b.size.y > b.size.x)
            {
                if (b.center.x < all.center.x) left = Mathf.Max(left, b.max.x);
                else right = Mathf.Min(right, b.min.x);
            }
            else
            {
                if (b.center.y < all.center.y) bottom = Mathf.Max(bottom, b.max.y);
                else top = Mathf.Min(top, b.min.y);
            }
        }
        if (left == float.MinValue) left = all.min.x;
        if (right == float.MaxValue) right = all.max.x;
        if (bottom == float.MinValue) bottom = all.min.y;
        if (top == float.MaxValue) top = all.max.y;

        area = Rect.MinMaxRect(left + margin, bottom + margin, right - margin, top - margin);
        return area.width > 1f && area.height > 1f;
    }

    // 기존 나무·울타리, 천막, 덤불 타일 자리는 피함
    private static void CollectBlocked()
    {
        blockedRects.Clear();
        blockedPoints.Clear();
        blockedRects.AddRange(interiorWalls);                // 캠프 울타리·숲 입구 벽 안쪽에는 놓지 않음
        foreach (SpriteRenderer r in Object.FindObjectsOfType<SpriteRenderer>())
        {
            string layer = r.sortingLayerName;
            bool big = layer.StartsWith("Tree") || layer.Contains("Fence");
            string lower = r.gameObject.name.ToLower();
            bool camp = lower.Contains("tent") || lower.Contains("camp") || r.gameObject.name.Contains("천막");
            if (!big && !camp)
                continue;
            Bounds b = r.bounds;
            if (b.size.x > 30f || b.size.y > 30f)
                continue;
            blockedRects.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
        }

        foreach (UnityEngine.Tilemaps.Tilemap map in Object.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>())
        {
            UnityEngine.Tilemaps.TilemapRenderer tr = map.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            if (tr == null || tr.sortingLayerName != "ForeGround")
                continue;
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (map.HasTile(cell))
                {
                    blockedPoints.Add(map.GetCellCenterWorld(cell));
                }
            }
        }
    }

    private static float Noise(Vector2 p)
    {
        MapDecorConfig c = MapDecorConfig.Instance;
        return Mathf.PerlinNoise(noiseOffset.x + p.x * c.noiseScale, noiseOffset.y + p.y * c.noiseScale);
    }

    private static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private static Vector2 RandomPoint() => new Vector2(Range(area.xMin, area.xMax), Range(area.yMin, area.yMax));

    private static Vector2 Random2()
    {
        double a = rng.NextDouble() * Mathf.PI * 2.0;
        double r = System.Math.Sqrt(rng.NextDouble());
        return new Vector2((float)(System.Math.Cos(a) * r), (float)(System.Math.Sin(a) * r));
    }

    private static bool CanPlace(Vector2 p, float spacing)
    {
        MapDecorConfig c = MapDecorConfig.Instance;
        if (!area.Contains(p))
            return false;
        if ((p - spawnPoint).sqrMagnitude < c.spawnClearRadius * c.spawnClearRadius)
            return false;
        if (hasCamp && (p - campPoint).sqrMagnitude < c.campClearRadius * c.campClearRadius)
            return false;                                         // 가운데 캠프 주변은 비움

        float pad = spacing * 0.5f;
        foreach (Rect r in blockedRects)
        {
            if (p.x > r.xMin - pad && p.x < r.xMax + pad && p.y > r.yMin - pad && p.y < r.yMax + pad)
                return false;
        }
        float tilePad = Mathf.Max(0.6f, pad);
        foreach (Vector2 q in blockedPoints)
        {
            if ((p - q).sqrMagnitude < tilePad * tilePad)
                return false;
        }
        foreach (Placed other in placed)
        {
            float need = Mathf.Max(spacing, other.spacing);   // 다른 분류와는 둘 중 큰 간격
            if ((p - other.position).sqrMagnitude < need * need)
                return false;
        }
        return true;
    }

    // ---------------------------------------------------------------
    // 장소 (무작위 위치)
    // ---------------------------------------------------------------
    private delegate void FeatureBuilder(MapDecorConfig c, Transform t, Vector2 at);

    // 빈 자리(다른 장소와 featureSpacing 이상, 짙은 구역 우선)를 골라 장소를 만듦
    private static void Feature(MapDecorConfig c, Transform t, FeatureBuilder build, bool preferDense)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector2 p = RandomPoint();
            if (preferDense && Noise(p) < c.denseThreshold && attempt < 60)
                continue;
            bool far = true;
            foreach (Vector2 f in features)
            {
                if ((p - f).sqrMagnitude < 9f * 9f) { far = false; break; }
            }
            if (!far || !CanPlace(p, c.tinySpacing))
                continue;
            build(c, t, p);
            return;
        }
    }

    private static Sprite Pick(Sprite[] list)
    {
        return list == null || list.Length == 0 ? null : list[rng.Next(list.Length)];
    }

    private static void Try(Transform parent, Sprite sprite, Vector2 p, float spacing, float scale, bool ground, float jitter = 0.8f)
    {
        if (sprite == null)
            return;
        for (int i = 0; i < 8; i++)
        {
            Vector2 q = i == 0 ? p : p + Random2() * jitter * (1f + i * 0.3f);
            if (CanPlace(q, spacing))
            {
                Spawn(parent, sprite, q, spacing, scale, ground);
                return;
            }
        }
    }

    // 키 큰 장식을 하나씩 따로 (다른 키 큰 장식과 lonelySpacing 이상)
    private static void Lonely(MapDecorConfig c, Transform t, Sprite[] sprites, int count, float spacing, float scale)
    {
        if (sprites == null || sprites.Length == 0)
            return;
        int made = 0;
        for (int attempt = 0; attempt < count * 80 && made < count; attempt++)
        {
            Vector2 p = RandomPoint();
            if (p.x < area.xMin + 3f || p.x > area.xMax - 3f || p.y < area.yMin + 2f || p.y > area.yMax - 5f)
                continue;                                    // 키 큰 장식이 테두리 숲에 겹치지 않게 가장자리는 피함
            bool far = true;
            foreach (Vector2 f in features)
            {
                if ((p - f).sqrMagnitude < c.lonelySpacing * c.lonelySpacing) { far = false; break; }
            }
            if (!far || !CanPlace(p, spacing))
                continue;
            features.Add(p);
            Spawn(t, Pick(sprites), p, spacing, scale, false);
            made++;
        }
    }

    // 옛 사냥터: 뼈·해골·나뭇가지
    private static void BoneSpot(MapDecorConfig c, Transform t, Vector2 at)
    {
        int count = Mathf.Min(3, c.bones != null ? c.bones.Length : 0);
        for (int i = 0; i < count; i++) Try(t, c.bones[i], at + Random2() * 1.4f, c.tinySpacing, c.tinyScale, true);
    }

    // 흩뿌리기 (dense면 짙은 구역 위주)
    private static void Scatter(MapDecorConfig c, Transform t, Sprite[] sprites, int count, float spacing, float scale, bool dense, bool ground)
    {
        if (sprites == null || sprites.Length == 0)
            return;
        int made = 0;
        for (int attempt = 0; attempt < count * 30 && made < count; attempt++)
        {
            Vector2 p = RandomPoint();
            if (dense && Noise(p) < c.denseThreshold && rng.NextDouble() > 0.2)
                continue;
            if (!CanPlace(p, spacing))
                continue;
            if (!Spawn(t, Pick(sprites), p, spacing, scale, ground))
                return;
            made++;
        }
    }

    // 풀·꽃: 한 지점 주변에 clusterSize개씩 군락 (한 군락은 같은 종류 위주)
    private static void PlacePlants(MapDecorConfig c, Transform t)
    {
        Sprite[] sprites = c.smallPlant;
        if (sprites == null || sprites.Length == 0)
            return;
        int made = 0;
        for (int attempt = 0; attempt < c.smallPlantCount * 20 && made < c.smallPlantCount; attempt++)
        {
            Vector2 center = RandomPoint();
            if (Noise(center) < c.denseThreshold && rng.NextDouble() > 0.3)
                continue;
            if (!CanPlace(center, c.smallPlantSpacing))
                continue;

            int size = rng.Next(c.clusterSize.x, Mathf.Max(c.clusterSize.x, c.clusterSize.y) + 1);
            int main = rng.Next(sprites.Length);
            for (int i = 0; i < size && made < c.smallPlantCount; i++)
            {
                Vector2 p = center;
                bool ok = i == 0;
                for (int tries = 0; tries < 6 && !ok; tries++)
                {
                    p = center + Random2() * c.clusterRadius;
                    ok = CanPlace(p, c.smallPlantSpacing);
                }
                if (!ok)
                    continue;
                Sprite sprite = rng.NextDouble() < 0.8 ? sprites[main] : sprites[rng.Next(sprites.Length)];
                if (!Spawn(t, sprite, p, c.smallPlantSpacing, c.smallPlantScale, true))
                    return;
                made++;
            }
        }
    }

    private static bool Spawn(Transform parent, Sprite sprite, Vector2 p, float spacing, float scale, bool ground)
    {
        MapDecorConfig c = MapDecorConfig.Instance;
        if (sprite == null || objectCount >= c.maxObjects)
            return false;

        GameObject go = new GameObject(sprite.name);
        go.transform.SetParent(parent, false);
        float s = Range(c.scaleRange.x, c.scaleRange.y) * scale;
        go.transform.localScale = Vector3.one * s;
        // p는 장식의 밑동(발 위치). 스프라이트 기준점이 가운데여도 밑동이 p에 오도록 올림
        float pivotToBottom = (sprite.pivot.y / sprite.pixelsPerUnit) * s;
        go.transform.position = new Vector3(p.x, p.y + pivotToBottom, 0f);

        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.flipX = rng.NextDouble() < 0.5;
        if (ground)
        {
            // 바닥 장식: 캐릭터·몬스터·보스가 위로 지나감
            r.sortingLayerName = c.groundLayer;
            r.sortingOrder = c.groundOrder;
        }
        else
        {
            // 키 큰 장식: 플레이어·몬스터보다 위. 장식끼리는 화면 아래쪽(밑동이 낮은 것)이 앞에
            r.sortingLayerName = c.decorLayer;
            r.sortingOrder = c.decorOrder + Mathf.Clamp(Mathf.RoundToInt((area.yMax - p.y) * 2f), 0, 400);
        }

        placed.Add(new Placed { position = p, spacing = spacing });
        objectCount++;
        return true;
    }
}
