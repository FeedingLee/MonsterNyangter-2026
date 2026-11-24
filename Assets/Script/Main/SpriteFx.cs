using System.Collections.Generic;
using UnityEngine;

// 이펙트 시트에서 프레임을 잘라 짧은 애니메이션 이펙트로 재생
// 시트 1 (effect_1): 흙먼지, 땅 터짐, 타격 별, 불꽃 폭발, 경고 원/기둥, 금빛 고리, 소용돌이, 물보라, "!!"
// 시트 2 (Weapon_effect_1): 무기별 이펙트 (쌍검 / 활 / 대검 / 헤비보우건 / 랜스 한 줄씩)
// 시트 3 (sub_effect_1): 반짝임, 금빛 궤적, 마법진(금/보라/파랑), 초승달 검기, 거품, 물결, 작은 불꽃, 빛줄기, 구름 등
// 시트는 한 장짜리 스프라이트라 실행 중에 영역을 잘라 쓰고, 잘라 둔 스프라이트는 다시 쓴다.
// 대부분의 이펙트는 오른쪽을 향해 그려져 있어 방향(각도)을 주면 그쪽으로 돌려서 재생한다.
//
// 그리는 순서 (sortingOffset)
//   0 이상  : 무기 레이어 위 (무기, 보스, 탄환보다 앞) - 공격 궤적, 폭발 등
//   음수    : 플레이어 레이어에서 캐릭터 바로 뒤 (몬스터보다는 앞) - 캐릭터에 붙는 오라, 발밑 이펙트
//   Ground  : 몬스터 레이어 맨 아래 (바닥 범위 표시)
// 캐릭터에 붙는 이펙트는 반드시 음수로 그려 캐릭터를 가리지 않게 한다.
public static class SpriteFx
{
    public const int Ground = -100;

    private static readonly Dictionary<(Texture2D, Rect), Sprite> cache = new Dictionary<(Texture2D, Rect), Sprite>();

    public static Sprite Crop(Sprite sheet, Rect rect)
    {
        if (sheet == null)
            return null;

        Texture2D texture = sheet.texture;
        Rect clamped = Rect.MinMaxRect(
            Mathf.Clamp(rect.xMin, 0f, texture.width - 1f), Mathf.Clamp(rect.yMin, 0f, texture.height - 1f),
            Mathf.Clamp(rect.xMax, 1f, texture.width), Mathf.Clamp(rect.yMax, 1f, texture.height));

        if (cache.TryGetValue((texture, clamped), out Sprite cached) && cached != null)
            return cached;

        Sprite sprite = SoftCrop(texture, clamped, sheet.pixelsPerUnit);
        if (sprite == null)
        {
            sprite = Sprite.Create(texture, clamped, new Vector2(0.5f, 0.5f), sheet.pixelsPerUnit);
        }
        sprite.name = "Fx " + clamped;
        cache[(texture, clamped)] = sprite;
        return sprite;
    }

    // 시트 픽셀 (텍스처당 한 번만 읽음)
    private static readonly Dictionary<Texture2D, Color32[]> sheetPixels = new Dictionary<Texture2D, Color32[]>();

    // 시트의 영역을 새 텍스처로 복사하면서 가장자리 알파를 서서히 0으로 만든다.
    // 시트의 반투명 배경·옆 프레임이 잘린 면에 걸려 네모 경계가 보이는 것을 막음
    private static Sprite SoftCrop(Texture2D texture, Rect rect, float ppu)
    {
        float feather = Config != null ? Config.effectEdgeFeather : 0f;
        if (feather <= 0f)
            return null;

        Color32[] src = ReadSheet(texture);
        if (src == null)
            return null;

        int x0 = Mathf.RoundToInt(rect.xMin), y0 = Mathf.RoundToInt(rect.yMin);
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        w = Mathf.Min(w, texture.width - x0);
        h = Mathf.Min(h, texture.height - y0);
        float f = Mathf.Min(feather, w * 0.25f, h * 0.25f);
        int mode = SheetMode(texture);
        bool hasMask = frameMask.TryGetValue((texture, rect), out RectInt mask);

        Color32[] dst = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float fy = Mathf.Min(y + 0.5f, h - y - 0.5f);
            for (int x = 0; x < w; x++)
            {
                Color32 c = KeyPixel(src[(y0 + y) * texture.width + x0 + x], mode);
                if (hasMask && !mask.Contains(new Vector2Int(x0 + x, y0 + y))) c.a = 0;   // 옆 프레임 그림 지움
                float edge = Mathf.Min(fy, Mathf.Min(x + 0.5f, w - x - 0.5f));
                if (f > 0f && edge < f)
                {
                    float k = edge / f;
                    c.a = (byte)(c.a * k * k * (3f - 2f * k));
                }
                dst[y * w + x] = c;
            }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "Fx " + texture.name;
        tex.filterMode = texture.filterMode;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(dst);
        tex.Apply(false, true);
        Vector2 pivot = framePivot.TryGetValue((texture, rect), out Vector2 pv) ? pv : new Vector2(0.5f, 0.5f);   // 바닥 기준 줄은 바닥 가운데
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        // 실제 그림이 차지하는 영역 (범위 표시를 실제 범위 크기에 맞출 때 사용)
        int ax0 = w, ay0 = h, ax1 = -1, ay1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (dst[y * w + x].a > 60) { ax0 = Mathf.Min(ax0, x); ax1 = Mathf.Max(ax1, x); ay0 = Mathf.Min(ay0, y); ay1 = Mathf.Max(ay1, y); }
        if (ax1 >= 0) artBounds[sprite] = new RectInt(ax0, ay0, ax1 - ax0 + 1, ay1 - ay0 + 1);
        return sprite;
    }

    // ---------------------------------------------------------------
    // 격자 칸 → 실제 그림 영역 맞춤
    // 같은 크기 칸으로 자르면 칸을 넘는 그림이 잘리고, 칸마다 그림 위치가 달라 재생할 때 울렁거림.
    // 줄의 각 프레임에서 그림 덩어리(가로 구간 + 세로로 이어진 범위)를 찾아, 줄 전체에서 가장 큰 크기로
    // 프레임마다 그림 중심에 맞춰 자르고, 옆 프레임 그림이 섞이지 않게 그 프레임 영역 밖은 지움
    // ---------------------------------------------------------------
    private static readonly Dictionary<Rect[], Rect[]> refitCache = new Dictionary<Rect[], Rect[]>();
    private static readonly Dictionary<(Texture2D, Rect), RectInt> frameMask = new Dictionary<(Texture2D, Rect), RectInt>();

    private static readonly Dictionary<Sprite, RectInt> artBounds = new Dictionary<Sprite, RectInt>();

    // 그림 가로 폭이 size가 되도록 하는 재생 크기 (자른 칸의 빈 여백 제외)
    private static float ArtFit(Sprite s, float size)
    {
        if (s == null || !artBounds.TryGetValue(s, out RectInt a) || a.width <= 0) return size;
        return size * s.rect.width / a.width;
    }

    // 그림 가로:세로 비 (납작한 고리를 원으로 늘릴 때)
    private static float ArtAspect(Sprite s)
    {
        if (s == null || !artBounds.TryGetValue(s, out RectInt a) || a.height <= 0) return s != null ? s.rect.width / Mathf.Max(1f, s.rect.height) : 1f;
        return (float)a.width / a.height;
    }

    private static Rect[] Refit(Sprite sheet, Rect[] cells)
    {
        if (refitCache.TryGetValue(cells, out Rect[] done))
            return done;
        Texture2D tex = sheet.texture;
        Color32[] src = ReadSheet(tex);
        if (src == null || cells.Length == 0)
            return cells;
        int mode = SheetMode(tex);
        int W = tex.width, H = tex.height;
        System.Func<int, int, bool> on = (x, y) => KeyPixel(src[y * W + x], mode).a > 40;

        int y0 = Mathf.Clamp((int)cells[0].y, 0, H - 1), h = Mathf.Clamp((int)cells[0].height, 1, H - y0);
        int xs = Mathf.Max(0, (int)cells[0].x), xe = Mathf.Min(W, (int)(cells[cells.Length - 1].x + cells[cells.Length - 1].width));
        // 1) 가로 구간
        bool[] col = new bool[W];
        for (int x = xs; x < xe; x++)
        {
            int n = 0;
            for (int y = y0; y < y0 + h && n < 2; y++) if (on(x, y)) n++;
            col[x] = n >= 2;
        }
        RectInt[] boxes = new RectInt[cells.Length];
        float maxW = 0f, maxH = 0f;
        for (int i = 0; i < cells.Length; i++)
        {
            int cx0 = (int)cells[i].x, cx1 = (int)(cells[i].x + cells[i].width);
            int bx0 = int.MaxValue, bx1 = -1;
            // 칸 안에서 시작한 구간을 옆으로 이어진 데까지 넓힘 (칸을 넘는 그림 포함)
            if (singleBlobRows.Contains(cells))
            {
                // 칸 안의 그림 덩어리(가로로 7px 이상 끊기면 다른 덩어리) 중 칸 안에 가장 많이 걸친 하나만
                int best = -1, run0 = -1, runN = 0, runEnd = -1, gapN = 0;
                for (int x = cx0; x <= cx1; x++)
                {
                    bool c = x < cx1 && x >= 0 && x < W && col[x];
                    if (c) { if (run0 < 0) { run0 = x; runN = 0; } runN++; runEnd = x; gapN = 0; }
                    else if (run0 >= 0 && (++gapN > 6 || x == cx1))
                    {
                        if (runN > best) { best = runN; bx0 = run0; bx1 = runEnd; }
                        run0 = -1;
                    }
                }
            }
            else
            {
                for (int x = cx0; x < cx1; x++) if (x >= 0 && x < W && col[x]) { bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x); }
            }
            if (bx1 < 0) { boxes[i] = new RectInt(cx0, y0, cx1 - cx0, h); continue; }
            int gap = 0;
            for (int x = bx1 + 1; x < Mathf.Min(W, cx1 + (int)(cells[i].width * 0.35f)); x++) { if (col[x]) { bx1 = x; gap = 0; } else if (++gap > 6) break; }
            gap = 0;
            for (int x = bx0 - 1; x >= Mathf.Max(0, cx0 - (int)(cells[i].width * 0.35f)); x--) { if (col[x]) { bx0 = x; gap = 0; } else if (++gap > 6) break; }
            // 2) 세로: 줄 안 + 위아래로 이어진 데까지
            int fy0 = int.MaxValue, fy1 = -1;
            // 칸 위·아래 가장자리에 걸친 옆 줄 그림은 제외 (rowTrim: x = 아래, y = 위 비율)
            Vector2 trim = rowTrim.TryGetValue(cells, out Vector2 tv) ? tv : Vector2.zero;
            int skip = (int)(h * trim.x), skipTop = (int)(h * trim.y);
            for (int y = y0 + skip; y < y0 + h - skipTop; y++) for (int x = bx0; x <= bx1; x++) if (on(x, y)) { fy0 = Mathf.Min(fy0, y); fy1 = Mathf.Max(fy1, y); break; }
            if (fy1 < 0) { fy0 = y0; fy1 = y0 + h - 1; }
            int limit = (int)(h * 0.4f);
            gap = 0;
            for (int y = fy1 + 1; y < Mathf.Min(H, y0 + h + (trim.y > 0f ? -skipTop : limit)); y++) { bool any = false; for (int x = bx0; x <= bx1 && !any; x++) any = on(x, y); if (any) { fy1 = y; gap = 0; } else if (++gap > 4) break; }
            gap = 0;
            int downLimit = trim.x > 0f ? -skip : limit;   // 아래 줄 그림이 붙어 있는 줄은 아래로 넓히지 않음
            for (int y = fy0 - 1; y >= Mathf.Max(0, y0 - downLimit); y--) { bool any = false; for (int x = bx0; x <= bx1 && !any; x++) any = on(x, y); if (any) { fy0 = y; gap = 0; } else if (++gap > 4) break; }
            boxes[i] = new RectInt(bx0, fy0, bx1 - bx0 + 1, fy1 - fy0 + 1);
            maxW = Mathf.Max(maxW, boxes[i].width);
            maxH = Mathf.Max(maxH, boxes[i].height);
        }
        // 프레임 마스크: 옅은 꼬리가 직선으로 잘리지 않게 넉넉히, 단 옆 프레임 그림과의 간격 절반까지만
        System.Func<int, RectInt> maskOf = k =>
        {
            RectInt b = boxes[k];
            int left = k > 0 ? Mathf.Clamp((b.xMin - boxes[k - 1].xMax) / 2 - 1, 2, 14) : 14;
            int right = k < boxes.Length - 1 ? Mathf.Clamp((boxes[k + 1].xMin - b.xMax) / 2 - 1, 2, 14) : 14;
            return new RectInt(b.xMin - left, b.yMin - 6, b.width + left + right, b.height + 12);
        };
        // 3) 줄 공통 크기, 프레임마다 그림 중심 (바닥 기준 줄은 바닥선을 줄 전체에서 하나로 고정)
        bool bottom = bottomRows.Contains(cells);
        int ground = int.MaxValue, top = int.MinValue;
        foreach (RectInt b in boxes) { ground = Mathf.Min(ground, b.yMin); top = Mathf.Max(top, b.yMax); }
        float rw = Mathf.Min(W, maxW + 16f), rh = Mathf.Min(H, (bottom ? top - ground : maxH) + 16f);
        Rect[] result = new Rect[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2 c = boxes[i].center;
            float x = Mathf.Clamp(Mathf.Round(c.x - rw * 0.5f), 0f, W - rw);
            float y = bottom ? Mathf.Clamp(ground - 8f, 0f, H - rh) : Mathf.Clamp(Mathf.Round(c.y - rh * 0.5f), 0f, H - rh);
            result[i] = new Rect(x, y, rw, rh);
            if (bottom) framePivot[(tex, result[i])] = new Vector2((c.x - x) / rw, (ground - y) / rh);
            frameMask[(tex, result[i])] = maskOf(i);
        }
        // 고리 그림: 프레임마다 기준점(무게중심 / 아래 고리 중심)이 같은 자리에 오도록 자른 영역을 옮김
        if (anchorMode.TryGetValue(cells, out int am))
        {
            Vector2[] centers = new Vector2[cells.Length];
            float half = rw * 0.5f, halfH = rh * 0.5f;
            for (int i = 0; i < cells.Length; i++)
            {
                RectInt b = boxes[i];
                int yTop = am == 2 ? b.yMin + Mathf.RoundToInt(b.height * 0.4f) : b.yMax;
                double sx = 0, sy = 0, n = 0;
                for (int y = b.yMin; y <= yTop; y++)
                    for (int x = b.xMin; x <= b.xMax; x++)
                    {
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        int a = KeyPixel(src[y * W + x], mode).a;
                        if (a <= 40) continue;
                        sx += x * a; sy += y * a; n += a;
                    }
                centers[i] = n > 0 ? new Vector2((float)(sx / n), (float)(sy / n)) : b.center;
                RectInt m = maskOf(i);
                half = Mathf.Max(half, Mathf.Max(centers[i].x - m.xMin, m.xMax - centers[i].x) + 2f);
                halfH = Mathf.Max(halfH, Mathf.Max(centers[i].y - m.yMin, m.yMax - centers[i].y) + 2f);
            }
            rw = Mathf.Min(W, Mathf.Ceil(half * 2f));
            rh = Mathf.Min(H, Mathf.Ceil(halfH * 2f));
            for (int i = 0; i < cells.Length; i++)
            {
                RectInt b = boxes[i];
                Vector2 c = centers[i];
                frameMask.Remove((tex, result[i]));
                float x0 = Mathf.Clamp(Mathf.Round(c.x - rw * 0.5f), 0f, W - rw);
                float y0r = Mathf.Clamp(Mathf.Round(c.y - rh * 0.5f), 0f, H - rh);
                result[i] = new Rect(x0, y0r, rw, rh);
                frameMask[(tex, result[i])] = maskOf(i);
                framePivot[(tex, result[i])] = new Vector2((c.x - x0) / rw, (c.y - y0r) / rh);
            }
        }
        refitCache[cells] = result;
        return result;
    }

    // 시트 배경 처리: 1 = 검은 배경(밝기를 투명도로), 2 = 빛 안개(옅은 부분 걷어 냄), 0 = 그대로
    private static int SheetMode(Texture2D texture)
    {
        if (Config == null || texture == null)
            return 0;
        if (Is(Config.fxVfx1, texture) || Is(Config.fxVfx2, texture) || Is(Config.fxVfx3, texture) || Is(Config.heavySkillSheet, texture))
            return 1;
        if (Is(Config.fxVfx4, texture) || Is(Config.fxVfx5, texture) || Is(Config.fxObject1, texture) || Is(Config.fxParticle2, texture)
            || Is(Config.fxSub2, texture) || Is(Config.fxSub3, texture))
            return 2;
        return 0;
    }

    private static bool Is(Sprite s, Texture2D t) => s != null && s.texture == t;

    private static Color32 KeyPixel(Color32 c, int mode)
    {
        if (mode == 1)
        {
            int m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int cut = Config.fxBlackCut;
            if (m <= cut) return new Color32(0, 0, 0, 0);
            float a = Mathf.Clamp01((m - cut) / (float)(200 - cut));
            float k = 255f / m;                                   // 밝기를 빼고 색만 남김 (검은 배경이 섞인 만큼 되돌림)
            return new Color32((byte)Mathf.Min(255f, c.r * k), (byte)Mathf.Min(255f, c.g * k), (byte)Mathf.Min(255f, c.b * k), (byte)(a * c.a));
        }
        if (mode == 2)
        {
            int cut = Config.fxHazeCut;
            c.a = (byte)(Mathf.Clamp01((c.a - cut) / (float)(255 - cut)) * 255f);
            return c;
        }
        return c;
    }

    private static Color32[] ReadSheet(Texture2D texture)
    {
        if (sheetPixels.TryGetValue(texture, out Color32[] pixels))
            return pixels;

        if (texture.isReadable)
        {
            pixels = texture.GetPixels32();
        }
        else
        {
            // 읽기 불가 텍스처는 GPU에서 복사해 읽음
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
            copy.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            pixels = copy.GetPixels32();
            Object.Destroy(copy);
        }
        sheetPixels[texture] = pixels;
        return pixels;
    }

    public static Sprite[] Crop(Sprite sheet, Rect[] rects)
    {
        if (sheet == null || rects == null)
            return new Sprite[0];
        if (rowSheet.ContainsKey(rects))
            rects = Refit(sheet, rects);                          // 새 격자 시트: 실제 그림 영역에 맞춰 다시 자름

        Sprite[] sprites = new Sprite[rects.Length];
        for (int i = 0; i < rects.Length; i++)
        {
            sprites[i] = Crop(sheet, rects[i]);
        }
        return sprites;
    }

    // 이펙트 오브젝트 + 그리는 순서 지정
    private static SpriteRenderer CreateRenderer(Vector3 position, Transform parent, bool flipX, int sortingOffset)
    {
        GameManager gm = GameManager.instance;
        SpriteRenderer playerSprite = gm != null ? gm.player.GetComponent<SpriteRenderer>() : null;

        GameObject go = new GameObject("Fx");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.flipX = flipX;
        if (playerSprite != null)
        {
            renderer.sharedMaterial = playerSprite.sharedMaterial;
            if (sortingOffset == Ground)
            {
                // 바닥 범위·먼지도 나무·캠프에 가려지지 않게 맨 위 레이어에 (무기·탄보다는 아래 순서)
                renderer.sortingLayerID = WeaponLayer(playerSprite);
                renderer.sortingOrder = -20;
            }
            else if (sortingOffset < 0)
            {
                renderer.sortingLayerID = playerSprite.sortingLayerID;
                renderer.sortingOrder = playerSprite.sortingOrder + sortingOffset;
            }
            else
            {
                renderer.sortingLayerID = WeaponLayer(playerSprite);
                renderer.sortingOrder = 5 + sortingOffset;      // 무기(3), 보스 탄(2)보다 앞
            }
        }
        return renderer;
    }

    private static int weaponLayer = int.MinValue;

    private static int WeaponLayer(SpriteRenderer fallback)
    {
        if (weaponLayer == int.MinValue)
        {
            int id = SortingLayer.NameToID("Weapon");
            weaponLayer = SortingLayer.IsValid(id) ? id : fallback.sortingLayerID;
        }
        return weaponLayer;
    }

    // 프레임 애니메이션 재생. size: 월드 기준 가로 크기, parent가 있으면 따라다님
    public static GameObject Play(Sprite[] frames, Vector3 position, float size, float fps,
        Transform parent = null, bool flipX = false, float holdLast = 0.15f, int sortingOffset = 4)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null)
            return null;

        SpriteRenderer renderer = CreateRenderer(position, parent, flipX, sortingOffset);
        renderer.gameObject.AddComponent<SpriteFxAnimator>().Init(renderer, frames, size, fps, holdLast);
        return renderer.gameObject;
    }

    public static GameObject Play(Sprite frame, Vector3 position, float size, float duration,
        Transform parent = null, int sortingOffset = 4)
    {
        return Play(new[] { frame }, position, size, 1f / Mathf.Max(0.05f, duration * 0.5f), parent, false, duration * 0.5f, sortingOffset);
    }

    // 색/투명도를 바꿔서 재생
    private static GameObject PlayTinted(Sprite[] frames, Vector3 position, float size, float fps, Color color,
        Transform parent = null, float holdLast = 0.15f, int sortingOffset = 4)
    {
        GameObject go = Play(frames, position, size, fps, parent, false, holdLast, sortingOffset);
        if (go != null)
        {
            go.GetComponent<SpriteFxAnimator>().SetColor(color);
        }
        return go;
    }

    // 반복 이펙트 (오라). lifetime이 음수면 Stop()을 부를 때까지 유지
    public static SpriteFxAnimator Loop(Sprite[] frames, Transform parent, Vector3 localOffset, float size, float fps,
        float lifetime, int sortingOffset, Color color, bool flipX = false)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null)
            return null;

        Vector3 position = parent != null ? parent.position + localOffset : localOffset;
        SpriteRenderer renderer = CreateRenderer(position, parent, flipX, sortingOffset);
        renderer.color = color;
        SpriteFxAnimator animator = renderer.gameObject.AddComponent<SpriteFxAnimator>();
        animator.InitLoop(renderer, frames, size, fps, lifetime);
        return animator;
    }

    public static void Stop(SpriteFxAnimator animator)
    {
        if (animator != null)
        {
            animator.Stop();
        }
    }

    // 방향(오른쪽 기준 각도)으로 돌려서 재생. 왼쪽을 볼 때는 위아래를 뒤집어 그림이 거꾸로 보이지 않게 함
    private static GameObject PlayDir(Sprite sheet, Rect[] rects, Vector3 position, Vector2 direction, float size,
        float fps, Transform parent = null, float holdLast = 0.12f, int sortingOffset = 5, float angleOffset = 0f)
    {
        GameObject go = Play(Crop(sheet, rects), position, size, fps, parent, false, holdLast, sortingOffset);
        if (go != null && direction.sqrMagnitude > 0.0001f)
        {
            bool left = direction.x < 0f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + (left ? -angleOffset : angleOffset);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.GetComponent<SpriteRenderer>().flipY = left;
        }
        return go;
    }

    // 캐릭터 발밑 위치 (오라 고리를 발에 맞추기 위해 target 기준 상대 위치로)
    private static Vector3 FeetOffset(Transform target, float lift = 0.05f)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        float bottom = renderer != null ? renderer.bounds.min.y - target.position.y : -0.5f;
        return Vector3.up * (bottom + lift);
    }

    // 캐릭터 머리 위 위치 (머리 위 이펙트가 캐릭터를 가리지 않도록)
    private static Vector3 AboveHead(Transform target, float extra = 0.25f)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        float top = renderer != null ? renderer.bounds.max.y - target.position.y : 0.6f;
        return target.position + Vector3.up * (top + extra);
    }

    private static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);

    // ---------------------------------------------------------------
    // 새 이펙트 시트 격자 (칸 번호: 위 줄부터 row, 왼쪽부터 col)
    // ---------------------------------------------------------------
    private enum Fx { V1, V2, V3, V4, V5, Obj, P1, P2, S2, S3 }

    private static Sprite SheetOf(Fx f)
    {
        switch (f)
        {
            case Fx.V1: return Config.fxVfx1;
            case Fx.V2: return Config.fxVfx2;
            case Fx.V3: return Config.fxVfx3;
            case Fx.V4: return Config.fxVfx4;
            case Fx.V5: return Config.fxVfx5;
            case Fx.Obj: return Config.fxObject1;
            case Fx.P1: return Config.fxParticle1;
            case Fx.P2: return Config.fxParticle2;
            case Fx.S2: return Config.fxSub2;
            default: return Config.fxSub3;
        }
    }

    // 시트 크기와 칸 수 (가로, 세로, 열, 줄)
    private static readonly int[,] GridDef =
    {
        { 1536, 1024, 6, 8 }, { 1774, 887, 7, 8 }, { 1774, 887, 7, 8 }, { 1536, 1024, 6, 10 }, { 1536, 1024, 6, 10 },
        { 1536, 1024, 6, 5 }, { 1472, 1264, 7, 6 }, { 1536, 1024, 7, 8 }, { 1536, 1024, 8, 6 }, { 1536, 1024, 8, 8 },
    };

    // 한 줄의 칸들 (from 부터 count 칸, count < 0 이면 끝까지)
    private static Rect[] Row(Fx f, int row, int from = 0, int count = -1)
    {
        int i = (int)f;
        float W = GridDef[i, 0], H = GridDef[i, 1];
        int cols = GridDef[i, 2], rows = GridDef[i, 3];
        float cw = W / cols, ch = H / rows;
        if (count < 0) count = cols - from;
        Rect[] r = new Rect[count];
        for (int k = 0; k < count; k++)
            r[k] = new Rect(Mathf.Round((from + k) * cw), Mathf.Round(H - (row + 1) * ch), Mathf.Floor(cw), Mathf.Floor(ch));
        return r;
    }

    // 프레임 배열 → 어느 시트인지 (지운 시트 1·4·5 의 프레임 이름은 그대로 두고 새 시트로 연결)
    private static readonly Dictionary<Rect[], Fx> rowSheet = new Dictionary<Rect[], Fx>();

    private static Rect[] Reg(Fx f, int row, int from = 0, int count = -1)
    {
        Rect[] r = Row(f, row, from, count);
        rowSheet[r] = f;
        int key = ((int)f) * 100 + row;
        if (BottomRows.Contains(key)) bottomRows.Add(r);
        if (CenterRows.Contains(key)) anchorMode[r] = 1;
        if (RingRows.Contains(key)) anchorMode[r] = 2;
        if (RowTrim.TryGetValue(key, out Vector2 t)) rowTrim[r] = t;
        if (key == (int)Fx.V1 * 100 + 3) singleBlobRows.Add(r);   // 돌진 속도선+먼지: 옆 프레임 구름이 칸 경계를 넘어옴
        return r;
    }

    // 아래에서 위로 솟는 그림 (폭발·땅 터짐·먼지·번개 고리): 프레임마다 위쪽으로 커져도 바닥이 고정되게
    private static readonly HashSet<int> BottomRows = new HashSet<int>
    {
        (int)Fx.Obj * 100 + 1, (int)Fx.Obj * 100 + 4,
        (int)Fx.V1 * 100 + 2, (int)Fx.V1 * 100 + 5, (int)Fx.V1 * 100 + 6,
        (int)Fx.V2 * 100 + 2, (int)Fx.V2 * 100 + 6, (int)Fx.V2 * 100 + 7,
        (int)Fx.P1 * 100 + 1, (int)Fx.P1 * 100 + 2, (int)Fx.P1 * 100 + 3,
        (int)Fx.S3 * 100 + 6, (int)Fx.S2 * 100 + 5, (int)Fx.P2 * 100 + 4,
    };
    private static readonly HashSet<Rect[]> bottomRows = new HashSet<Rect[]>();
    private static readonly Dictionary<Rect[], Vector2> rowTrim = new Dictionary<Rect[], Vector2>();
    private static readonly HashSet<Rect[]> singleBlobRows = new HashSet<Rect[]>();
    // 옆 줄 그림이 칸 안까지 들어와 있는 줄: (아래 제외 비율, 위 제외 비율)
    private static readonly Dictionary<int, Vector2> RowTrim = new Dictionary<int, Vector2>
    {
        { (int)Fx.P2 * 100 + 4, new Vector2(0.3f, 0f) },     // 번개 폭발: 바로 아래 독가스 구름
        { (int)Fx.Obj * 100 + 1, new Vector2(0f, 0.15f) },   // 불꽃 폭발: 바로 위 붉은 조준 원
        { (int)Fx.V3 * 100 + 1, new Vector2(0f, 0.38f) },    // 찌르기 섬광: 바로 위 줄 푸른 초승달 아랫부분
    };

    // 가운데를 기준으로 커지는 고리 (조준 원, 충격 고리): 그림 무게중심을 기준점으로
    private static readonly HashSet<int> CenterRows = new HashSet<int> { (int)Fx.Obj * 100 + 0, (int)Fx.V1 * 100 + 7, (int)Fx.V3 * 100 + 4, (int)Fx.S2 * 100 + 1, (int)Fx.S3 * 100 + 1, (int)Fx.V3 * 100 + 7 };
    // 바닥 고리 위로 번개가 솟는 그림: 아래쪽 고리 부분의 중심을 기준점으로
    private static readonly HashSet<int> RingRows = new HashSet<int> { (int)Fx.Obj * 100 + 2 };
    private static readonly Dictionary<Rect[], int> anchorMode = new Dictionary<Rect[], int>();   // 1 = 무게중심, 2 = 아래 고리
    private static readonly Dictionary<(Texture2D, Rect), Vector2> framePivot = new Dictionary<(Texture2D, Rect), Vector2>();

    private static Sprite S(Rect[] rects) => rects != null && rowSheet.TryGetValue(rects, out Fx f) ? SheetOf(f) : null;

    private static readonly Dictionary<string, Rect[]> namedRows = new Dictionary<string, Rect[]>();

    // 외부(UI 연출 등)에서 쓰는 새 시트 프레임
    public static Sprite[] Frames(string sheet, int row, int from = 0, int count = -1)
    {
        Fx f = (Fx)System.Enum.Parse(typeof(Fx), sheet);
        string key = sheet + "_" + row + "_" + from + "_" + count;
        if (!namedRows.TryGetValue(key, out Rect[] rects))
        {
            rects = Reg(f, row, from, count);                     // 등록해서 실제 그림 영역 맞춤이 적용되게
            namedRows[key] = rects;
        }
        return Crop(SheetOf(f), rects);
    }

    // 자주 쓰는 새 프레임
    private static readonly Rect[] VCrescent = Reg(Fx.V1, 0);          // 회색 초승달 베기
    private static readonly Rect[] VSwipeDust = Reg(Fx.V1, 1);         // 휘두름 + 먼지
    private static readonly Rect[] VSpike = Reg(Fx.V1, 2);             // 땅 가시 + 돌
    private static readonly Rect[] VDashDust = Reg(Fx.V1, 3);          // 속도선 + 먼지
    private static readonly Rect[] VDustBlast = Reg(Fx.V1, 5);         // 흙 폭발
    private static readonly Rect[] VWhite = Reg(Fx.V2, 0);             // 흰 초승달 (크게)
    private static readonly Rect[] VGoldSpin = Reg(Fx.V2, 1);          // 금빛 회전 고리
    private static readonly Rect[] VGround = Reg(Fx.V2, 2);            // 땅 터짐
    private static readonly Rect[] VCrater = Reg(Fx.V2, 4);            // 충격 구덩이 고리
    private static readonly Rect[] VSparks = Reg(Fx.V2, 5);            // 불꽃 튐
    private static readonly Rect[] VCrack = Reg(Fx.V2, 6);             // 땅 균열 번개
    private static readonly Rect[] VRocks = Reg(Fx.V2, 7);             // 돌무더기
    private static readonly Rect[] VBlue = Reg(Fx.V3, 0);              // 푸른 초승달
    private static readonly Rect[] VThrust = Reg(Fx.V3, 1);            // 찌르기 섬광
    private static readonly Rect[] VShock = Reg(Fx.V3, 4);             // 충격 고리
    private static readonly Rect[] VPebbles = Reg(Fx.V3, 5);           // 돌 튐
    private static readonly Rect[] VDiagSpark = Reg(Fx.V3, 6);         // 사선 불꽃
    private static readonly Rect[] VWhirl = Reg(Fx.V3, 7);             // 회오리 고리
    private static readonly Rect[] VChevron = Reg(Fx.V4, 1);           // 겹화살표
    private static readonly Rect[] OFire = Reg(Fx.Obj, 1);             // 불꽃 폭발
    private static readonly Rect[] OZap = Reg(Fx.Obj, 2);              // 노란 번개 고리 (마비)
    private static readonly Rect[] OGas = Reg(Fx.Obj, 3);              // 연기 구름
    private static readonly Rect[] ORing = Reg(Fx.Obj, 4);             // 땅 고리 폭발
    private static readonly Rect[] RangeCircle = Reg(Fx.Obj, 0, 2, 4); // 다 그려진 붉은 조준 원 (반복용)
    private static readonly Rect[] ZapRing = Reg(Fx.Obj, 2, 1, 4);     // 번개 고리 (반복용)
    private static readonly Rect[] RainRing = Reg(Fx.V1, 7);           // 흙빛 충격 고리 (화살비)
    private static readonly Rect[] PStar = Reg(Fx.P1, 0);              // 별 폭발
    private static readonly Rect[] PSmoke = Reg(Fx.P1, 3);             // 연기
    private static readonly Rect[] PDebris = Reg(Fx.P2, 7);            // 파편 조각
    private static readonly Rect[] PZap = Reg(Fx.P2, 4);               // 노란 번개 폭발 (바닥에서 솟음)
    private static readonly Rect[] VWindBlade = Reg(Fx.V4, 0);         // 큰 칼바람 초승달
    private static readonly Rect[] SSparkleA = Reg(Fx.S2, 0);          // 큰 반짝임
    private static readonly Rect[] SGoldSwirl = Reg(Fx.S2, 1);         // 금빛 회전 고리
    private static readonly Rect[] SGoldPillar = Reg(Fx.S2, 2);        // 금빛 기둥
    private static readonly Rect[] SLeaves = Reg(Fx.S2, 4);            // 나뭇잎
    private static readonly Rect[] SPuff = Reg(Fx.S2, 5);              // 먼지 퍼프
    private static readonly Rect[] SSparkle = Reg(Fx.S3, 0);           // 작은 반짝임 무리
    private static readonly Rect[] SRing = Reg(Fx.S3, 1);              // 금빛 고리
    private static readonly Rect[] SFan = Reg(Fx.S3, 2);               // 금빛 부채
    private static readonly Rect[] SRise = Reg(Fx.S3, 3);              // 금빛 솟구침
    private static readonly Rect[] SDust = Reg(Fx.S3, 6);              // 먼지 구름
    private static readonly Rect[] SOrbs = Reg(Fx.S3, 7);              // 빛 구슬

    // 시트 4: Effect_2 / 시트 5: Effect_3
    private static readonly Rect[] GoldRingE2 = Reg(Fx.S3, 1);
    private static readonly Rect[] GoldStarE2 = Reg(Fx.S3, 0);
    private static readonly Rect[] LightningE2 = Reg(Fx.V1, 4);
    private static readonly Rect[] SlashCutFrames = { R(0, 550, 344, 110), R(0, 440, 365, 110), R(0, 330, 434, 110), R(0, 220, 440, 110), R(0, 110, 408, 110), R(0, 0, 403, 110) };
    private static readonly Rect[] GoldSpikeE2 = Reg(Fx.S2, 2, 1, 6);
    private static readonly Rect[] ArrowStreakE2 = Reg(Fx.V4, 4, 3, 1);
    private static readonly Rect[] StreakLinesE2 = Reg(Fx.V3, 1, 3, 1);
    private static readonly Rect[] CrossE3 = Reg(Fx.V2, 0);
    private static readonly Rect[] StarBurstE3 = Reg(Fx.V3, 2);

    // ---------------------------------------------------------------
    // 프레임 목록 (픽셀, 왼쪽 아래 기준)
    // ---------------------------------------------------------------
    // 시트 1: effect_1
    private static readonly Rect[] DustPuff = Reg(Fx.S3, 6, 0, 7);
    private static readonly Rect[] SmallDust = Reg(Fx.S2, 5, 0, 7);
    private static readonly Rect[] SpeedLines = Reg(Fx.V3, 1, 1, 5);
    private static readonly Rect[] DustSwirl = Reg(Fx.V3, 7);
    private static readonly Rect[] GroundBurstFrames = Reg(Fx.V2, 2);
    private static readonly Rect[] HitStar = Reg(Fx.P1, 0);
    private static readonly Rect[] StarRing = Reg(Fx.V3, 4);
    private static readonly Rect[] FireBoom = Reg(Fx.Obj, 1);
    private static readonly Rect[] Crystal = Reg(Fx.V1, 6);
    private static readonly Rect[] RedCircle = Reg(Fx.Obj, 0);
    private static readonly Rect[] RedPillar = Reg(Fx.P1, 1);
    private static readonly Rect[] WhiteSlash = Reg(Fx.V2, 0);
    private static readonly Rect[] BlueSwirl = Reg(Fx.Obj, 2);
    private static readonly Rect[] DoubleExclaim = Reg(Fx.Obj, 0, 4, 1);
    private static readonly Rect[] Target = Reg(Fx.Obj, 0, 3, 1);

    // 시트 2: Weapon_effect_1 (줄마다 무기 하나)
    private static readonly Rect[] DBSlash = { R(33, 731, 151, 85), R(237, 709, 186, 138), R(477, 724, 171, 108) };
    private static readonly Rect[] DBCross = { R(682, 720, 185, 135) };
    private static readonly Rect[] DBWhirl = { R(896, 709, 187, 150), R(1111, 711, 206, 148) };
    private static readonly Rect[] DBDash = { R(1349, 739, 194, 92), R(1558, 719, 201, 134) };
    private static readonly Rect[] ArrowShotFrames = { R(24, 571, 172, 38), R(256, 573, 184, 39) };
    private static readonly Rect[] ArrowFan = { R(496, 524, 145, 140), R(697, 513, 177, 161) };
    private static readonly Rect[] GSCrescent = { R(30, 355, 104, 73), R(179, 322, 150, 149), R(330, 322, 162, 149) };
    private static readonly Rect[] GSGround = { R(905, 337, 176, 72), R(1108, 338, 209, 102), R(1334, 336, 213, 136) };
    private static readonly Rect[] MuzzleFrames = { R(20, 185, 176, 98), R(246, 191, 172, 139) };
    private static readonly Rect BulletTrail = R(479, 205, 151, 35);
    // 총구 불꽃만 (그림 왼쪽의 총신 부분은 잘라냄)
    private static readonly Rect[] MuzzleFlash = { R(88, 185, 108, 98), R(1417, 182, 96, 93) };
    private static readonly Rect[] Thrust = { R(37, 60, 159, 38), R(240, 56, 199, 51), R(468, 49, 200, 62) };
    private static readonly Rect GunSmoke = R(1156, 195, 137, 72);
    private static readonly Rect ShieldGlow = R(701, 30, 144, 108);
    private static readonly Rect[] ShieldParry = { R(891, 28, 181, 116), R(1108, 25, 181, 124) };
    private static readonly Rect[] ChargeDust = { R(1333, 34, 189, 77), R(1552, 36, 205, 94) };

    // 시트 3: sub_effect_1
    private static readonly Rect[] Sparkle = { R(52, 912, 24, 26), R(117, 908, 49, 48), R(187, 906, 58, 62), R(260, 903, 70, 73) };
    private static readonly Rect[] GoldArc = { R(375, 889, 118, 115), R(515, 889, 126, 115), R(648, 889, 153, 115), R(818, 889, 150, 115) };
    private static readonly Rect[] SparkleCluster = { R(972, 889, 150, 115), R(1135, 889, 130, 115), R(1268, 889, 240, 115) };
    private static readonly Rect[] Butterflies = { R(577, 762, 390, 100) };
    private static readonly Rect[] Petals = { R(368, 768, 182, 95) };
    private static readonly Rect[] GoldStreak = { R(60, 660, 50, 62), R(128, 660, 75, 62), R(216, 660, 88, 62) };
    private static readonly Rect[] Rocks = { R(335, 650, 235, 95) };
    private static readonly Rect[] DustCloud = { R(596, 655, 100, 80), R(700, 655, 95, 80), R(807, 655, 104, 70), R(925, 655, 87, 70), R(1025, 655, 84, 70), R(1122, 655, 95, 70) };
    private static readonly Rect[] Wind = { R(1235, 660, 96, 60), R(1338, 660, 81, 62), R(1424, 660, 94, 65) };
    private static readonly Rect[] GoldPillar = { R(14, 500, 140, 125), R(155, 500, 140, 125), R(297, 500, 140, 125) };
    private const float PillarRingDrop = 0.27f;     // 기둥 그림에서 고리 중심이 그림 중심보다 아래에 있는 비율 (그림 높이 기준)
    private static readonly Rect[] GoldRingSparkle = { R(455, 500, 140, 125), R(598, 500, 125, 125), R(727, 500, 106, 125) };
    private static readonly Rect[] PurpleCircle = { R(853, 500, 128, 125) };
    private static readonly Rect[] BlueCircle = { R(998, 500, 118, 125), R(1131, 500, 115, 125), R(1256, 500, 118, 125), R(1390, 500, 130, 125) };
    private static readonly Rect[] Crescent = { R(18, 392, 124, 82), R(146, 392, 126, 82), R(286, 388, 92, 90) };
    private static readonly Rect[] WindSwirl = { R(404, 390, 94, 85), R(509, 390, 101, 85), R(623, 390, 123, 85) };
    private static readonly Rect[] Bubbles = { R(911, 390, 72, 80), R(991, 387, 99, 101), R(1100, 395, 67, 75) };
    private static readonly Rect[] Ripple = { R(1196, 393, 103, 68), R(1310, 393, 100, 70), R(1418, 393, 101, 66) };
    private static readonly Rect[] SmallFire = { R(102, 270, 60, 66), R(176, 270, 74, 75), R(268, 270, 69, 72) };
    private static readonly Rect[] DemonFlame = { R(89, 272, 72, 80), R(174, 272, 72, 80), R(256, 272, 72, 80) };   // 같은 크기·같은 바닥 (흔들리지 않게)
    private static readonly Rect[] RainDust = { R(160, 522, 200, 82), R(390, 522, 200, 82), R(612, 522, 200, 82), R(835, 522, 200, 82) };   // Attack_vfx_5 흙먼지 구름 (같은 크기, 오른쪽 끝 기준)
    private static readonly Rect[] PurpleShards = { R(675, 275, 195, 80) };
    private static readonly Rect[] GoldCloud = { R(33, 94, 217, 122) };
    private static readonly Rect[] BlueCloud = { R(286, 92, 205, 131) };
    private static readonly Rect[] GreenOrbs = { R(715, 85, 230, 145) };
    private static readonly Rect[] LightRays = { R(969, 85, 236, 161) };

    private static RunConfig Config => RunConfig.Instance;
    private static Sprite Sheet1 => Config.effectSheet1;
    private static Sprite Sheet2 => Config.effectSheet2;
    private static Sprite Sheet3 => Config.effectSheet3;
    private static Sprite Sheet4 => Config.effectSheet4;   // Effect_2
    private static Sprite Sheet5 => Config.effectSheet5;   // Effect_3

    // ---------------------------------------------------------------
    // 공통 / 아이템
    // ---------------------------------------------------------------
    // 폭발 (폭탄 통): 불꽃 폭발 + 땅 터짐 + 튀는 돌 + 연기 구름
    public static void Explosion(Vector3 position, float size)
    {
        // position = 폭발 중심. 불꽃 폭발은 바닥에서 솟으므로 바닥선을 중심보다 조금 아래에
        Vector3 ground = position + Vector3.down * size * 0.3f;
        Play(Crop(S(FireBoom), FireBoom), ground, size * 1.4f, 14f, null, false, 0.15f, 6);
        PlayTinted(Crop(S(PSmoke), PSmoke), ground + Vector3.up * size * 0.1f, size * 1.1f, 10f, new Color(1f, 1f, 1f, 0.6f), null, 0.2f, 5);
        Play(Crop(S(VRocks), VRocks), ground, size * 0.9f, 14f, null, false, 0.1f, 7);
    }

    // 회피: 뒤쪽으로 뻗는 속도선 + 바람 + 출발 지점 흙먼지 (모두 캐릭터 뒤)
    public static void DodgeDust(Vector3 position, Vector2 direction)
    {
        // 회피: 속도선+먼지가 길게 남고, 출발점에 흙먼지 퍼프 (잘 보이게 크게)
        PlayDir(S(VDashDust), VDashDust, position - (Vector3)(direction.normalized * 0.3f) + Vector3.down * 0.2f, direction, Config.dodgeDustSize * 1.2f, 16f, null, 0.1f, -1);
        PlayTinted(Crop(S(SPuff), SPuff), position + Vector3.down * 0.3f, Config.dodgeDustSize * 1.3f, 16f, new Color(1f, 1f, 1f, 0.9f), null, 0.08f, -2);
        PlayDir(S(SpeedLines), SpeedLines, position, direction, Config.dodgeDustSize * 1.2f, 14f, null, 0.1f, -1);
        PlayDir(Sheet3, Wind, position - (Vector3)(direction.normalized * 0.4f), -direction, Config.dodgeDustSize, 16f, null, 0.1f, -2);
        Play(Crop(S(DustPuff), DustPuff), position + Vector3.down * 0.3f, Config.dodgeDustSize, 14f, null, direction.x > 0f, 0.15f, -1);
    }

    // 이동 중 발밑 작은 흙먼지
    public static void MoveDust(Vector3 position, Vector2 direction)
    {
        Play(Crop(S(SmallDust), SmallDust), position + Vector3.down * 0.35f, 0.45f, 10f, null, direction.x > 0f, 0.15f, -2);
    }

    // 마비 두꺼비 터짐: 파란 마법진이 솟고 물결이 퍼지며 거품과 푸른 빛 구름
    public static void Zap(Vector3 position, float size)
    {
        // 범위 예고(번개 고리)와 다른 그림: 가운데 큰 번개 폭발 + 실제 범위 가장자리를 따라 번개가 둘러 터지고, 안쪽에도 번개
        float r = size * 0.5f;
        Sprite[] zap = Crop(S(PZap), PZap);
        float k = zap.Length > 3 ? ArtFit(zap[3], 1f) : 1f;          // 칸 여백을 빼고 번개 그림 폭 기준으로
        Play(zap, position + Vector3.down * r * 0.15f, size * 0.6f * k, 16f, null, Random.value < 0.5f, 0.12f, 5);
        int edge = Mathf.Max(4, Config.toadZapEdgeCount);
        float turn = Random.Range(0f, 360f);
        for (int i = 0; i < edge; i++)
        {
            float a = (turn + 360f * i / edge) * Mathf.Deg2Rad;
            Vector3 p = position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r * 0.78f;
            Play(zap, p, size * Random.Range(0.3f, 0.36f) * k, Random.Range(15f, 19f), null, Random.value < 0.5f, 0.08f, 5);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = position + (Vector3)(Random.insideUnitCircle * r * 0.5f);
            Play(zap, p, size * Random.Range(0.26f, 0.32f) * k, Random.Range(15f, 19f), null, Random.value < 0.5f, 0.08f, 5);
        }
    }

    // 마비된 적 위 작은 파란 소용돌이
    public static void StunSpark(Transform target, float duration)
    {
        Play(Crop(S(BlueSwirl), BlueSwirl), AboveHead(target, 0.15f), 0.7f, 8f, target, false, Mathf.Min(duration, 1f), 6);
    }

    // 금빛 반짝임 (코인 획득) - 캐릭터 뒤
    public static void GoldSpark(Vector3 position, float size, Transform parent = null)
    {
        Play(Crop(Sheet3, Sparkle), position, size, 18f, parent, false, 0.1f, -1);
    }

    // 회복: 발밑에서 금빛 마법진이 솟고 초록 빛방울이 떠오름 + 머리 위 반짝임 (캐릭터는 가리지 않음)
    public static void Heal(Transform target)
    {
        Play(Crop(Sheet3, GoldPillar), target.position + FeetOffset(target) + Vector3.up * 0.45f, 1.5f, 8f, target, false, 0.3f, -1);
        GameObject orbs = PlayTinted(Crop(Sheet3, GreenOrbs), target.position + Vector3.up * 0.1f, 1.4f, 4f,
            new Color(1f, 1f, 1f, 0.9f), target, 0.5f, -2);
        if (orbs != null)
        {
            orbs.GetComponent<SpriteFxAnimator>().SetDrift(Vector3.up * 0.7f);
        }
        Play(Crop(Sheet3, Sparkle), AboveHead(target), 0.5f, 14f, target, false, 0.2f, 6);
    }

    // 레벨업: 금빛 기둥 + 머리 위 반짝임
    public static void LevelUp(Transform target)
    {
        Play(Crop(Sheet3, GoldPillar), target.position + FeetOffset(target) + Vector3.up * 0.55f, 1.8f, 8f, target, false, 0.3f, -1);
        Play(Crop(Sheet3, SparkleCluster), AboveHead(target, 0.35f), 1.1f, 8f, target, false, 0.2f, 6);
    }

    // 랜스 돌진 모드 보호막: 원 대신 오라(LanceChargeAura)를 사용
    public static Sprite ShieldDome => null;

    // 무기에 맞은 적 위치에 작은 별 (초당 개수 제한)
    private static float sparkWindowStart;
    private static int sparkCount;

    public static void HitSpark(Vector3 position, float sizeScale = 1f)
    {
        if (Time.time - sparkWindowStart >= 0.25f)
        {
            sparkWindowStart = Time.time;
            sparkCount = 0;
        }
        if (sparkCount >= Mathf.Max(1, Config.hitSparksPerSecond / 4))
            return;
        sparkCount++;

        Vector3 jitter = (Vector3)(Random.insideUnitCircle * 0.25f);
        GameObject fx = Play(Crop(S(HitStar), HitStar), position + jitter * sizeScale, Config.hitSparkSize * 1.4f * sizeScale, 28f, null, false, 0.05f);
        if (fx != null)
        {
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        }
    }

    // 몬스터 처치: 작은 흙먼지 + 반짝임 (한꺼번에 많이 죽어도 너무 많아지지 않게 제한)
    private static float popWindowStart;
    private static int popCount;

    public static void EnemyPop(Vector3 position)
    {
        if (Time.time - popWindowStart >= 0.25f)
        {
            popWindowStart = Time.time;
            popCount = 0;
        }
        if (popCount >= 6)
            return;
        popCount++;

        Play(Crop(S(DustPuff), DustPuff), position, 0.9f, 14f, null, Random.value < 0.5f, 0.1f, -3);
        Play(Crop(Sheet3, Sparkle), position, 0.5f, 18f, null, false, 0.05f, 5);
    }

    // 중간보스 쓰러짐: 발밑에서 흙먼지 한 번 (겹치지 않게 한 장만)
    public static void BossDeathDust(Vector3 feet, float size)
    {
        PlayTinted(Crop(S(VDustBlast), VDustBlast), feet, size, 13f, new Color(1f, 1f, 1f, 0.9f), null, 0.15f, 2);
    }

    // 중간보스 걸음 먼지 (발밑, 진행 반대쪽으로)
    public static void BossStepDust(Vector3 feet, float size, bool flip)
    {
        PlayTinted(Crop(S(SDust), SDust), feet + Vector3.down * 0.05f, size * 0.6f, 14f, new Color(1f, 1f, 1f, 0.7f), null, 0.08f, 2);   // 발 높이에서 낮게 (몸은 가리지 않음)
    }

    // 돌진 직전 크게 퍼지는 먼지구름
    public static void BossDustBurst(Vector3 feet, float size)
    {
        // 매번 다른 먼지 그림을 골라 크기·좌우·위치를 조금씩 다르게 (같은 모양 반복 방지)
        Rect[][] big = { VDustBlast, VRocks, VSpike };
        Rect[] main = big[Random.Range(0, big.Length)];
        Play(Crop(S(main), main), feet, size * Random.Range(0.85f, 1.1f), Random.Range(12f, 16f), null, Random.value < 0.5f, 0.1f, 2);
        Rect[][] side = { SDust, SPuff, VSwipeDust };
        for (int i = 0; i < 2; i++)
        {
            Rect[] r = side[Random.Range(0, side.Length)];
            float dir = i == 0 ? -1f : 1f;
            PlayTinted(Crop(S(r), r), feet + new Vector3(dir * size * Random.Range(0.25f, 0.45f), Random.Range(-0.05f, 0.1f), 0f),
                size * Random.Range(0.45f, 0.7f), Random.Range(11f, 15f), new Color(1f, 1f, 1f, 0.85f), null, 0.1f, 2);
        }
    }

    // 돌진 중 튀는 작은 파편
    public static void BossDebris(Vector3 feet, Vector2 direction, float size)
    {
        GameObject fx = PlayDir(S(VPebbles), VPebbles, feet, -direction, size, 18f, null, 0.05f, 2);
        if (fx == null) return;
        fx.GetComponent<SpriteRenderer>().flipY = false;
    }

    // 땅 찍기 (갑각군주 소환): 땅 터짐 + 균열 + 먼지구름
    public static void BossSlam(Vector3 feet, float size)
    {
        Play(Crop(S(VGround), VGround), feet + Vector3.up * size * 0.1f, size, 16f, null, false, 0.1f, 4);
        Play(Crop(S(VCrack), VCrack), feet, size * 1.1f, 14f, null, false, 0.15f, Ground);
        Play(Crop(S(VDustBlast), VDustBlast), feet + Vector3.up * size * 0.1f, size * 1.2f, 14f, null, false, 0.1f, 2);
    }

    // 소환된 몬스터 반짝임
    public static void SummonSparkle(Vector3 position)
    {
        Play(Crop(S(SSparkleA), SSparkleA), position + Vector3.up * 0.4f, 1.2f, 16f, null, false, 0.05f, 6);
    }

    // 강화(엘리트) 몬스터 오라: 발밑 붉은 회전 고리가 계속 돎 (몬스터 아래)
    public static SpriteFxAnimator EliteAura(Transform target, Color color)
    {
        return Loop(Crop(S(SGoldSwirl), SGoldSwirl), target, Vector3.down * 0.15f, 1.2f, 10f, -1f, Ground, color);   // 몬스터 중심 바로 아래
    }

    // 사슴벌레 격려를 받은 몬스터 머리 위 표시 (붉은 금빛 반짝임 반복)
    public static GameObject RallyMark(Transform target)
    {
        SpriteFxAnimator fx = Loop(Crop(S(SSparkle), SSparkle), target, AboveHead(target, 0.05f) - target.position, 0.7f, 10f, -1f, 6, new Color(1f, 0.7f, 0.5f, 1f));
        return fx != null ? fx.gameObject : null;
    }

    // 랜스 패링 표시용 방패 (Weapon_effect_1)
    public static Sprite ShieldIconSprite => Sheet2 != null ? Crop(Sheet2, ShieldGlow) : null;

    // 쌍검 각성: 화면 꼭대기부터 땅까지 길게 내리꽂히는 번개 (effect_2 번개 프레임을 세로로 늘림)
    public static void LightningStrikes(int count)
    {
        if (S(LightningE2) == null || count <= 0)
            return;
        Sprite[] all = Crop(S(LightningE2), LightningE2);
        Sprite[] frames = all;
        Rect screen = SynergyAwakening.ScreenRect();
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(screen.xMin + 1.5f, screen.xMax - 1.5f, (i + Random.Range(0.25f, 0.75f)) / count);
            float ground = Random.Range(screen.yMin + screen.height * 0.15f, screen.yMin + screen.height * 0.5f);
            float height = screen.yMax - ground + 0.5f;
            Stretched(frames, new Vector3(x, ground + height * 0.5f, 0f), Random.Range(3.2f, 4.2f), height, 0f, 20f, 0.12f, 9, false, Random.value < 0.5f);
        }
    }

    // 프레임 애니메이션을 가로(width) x 세로(height) 크기에 맞춰 늘려 재생 (angle: 회전, 부모를 늘려 자식은 그대로 재생)
    public static GameObject Stretched(Sprite[] frames, Vector3 center, float width, float height, float angle, float fps,
        float holdLast, int sortingOffset, bool unscaled, bool flipX = false)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null)
            return null;
        Sprite first = frames[frames.Length / 2];
        float naturalHeight = width * first.bounds.size.y / Mathf.Max(0.01f, first.bounds.size.x);

        GameObject holder = new GameObject("Fx Stretched");
        holder.transform.position = center;
        holder.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        holder.transform.localScale = new Vector3(1f, height / Mathf.Max(0.01f, naturalHeight), 1f);
        GameObject fx = Play(frames, center, width, fps, holder.transform, flipX, holdLast, sortingOffset);
        if (fx != null)
        {
            fx.transform.localRotation = Quaternion.identity;
            fx.GetComponent<SpriteFxAnimator>().unscaled = unscaled;
        }
        Object.Destroy(holder, frames.Length / Mathf.Max(1f, fps) + holdLast + 0.5f);
        return fx;
    }

    // 대검 각성: 화면을 가르는 금빛 베기 한 줄 (effect_3 베기를 다듬은 Effect_3_slash, a→b 길이로 늘림)
    public static void ScreenCut(Vector3 a, Vector3 b, float thickness, float duration)
    {
        Sprite sheet = Config.effectSlash;
        if (sheet == null)
            return;
        Sprite[] frames = Crop(sheet, SlashCutFrames);
        Vector3 diff = b - a;
        float length = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        // 가로로 긴 그림이라 가로를 길이에 맞추고, 세로(두께)는 holder 세로 배율로
        Sprite mid = frames[frames.Length / 2];
        float naturalThickness = length * mid.bounds.size.y / Mathf.Max(0.01f, mid.bounds.size.x);
        GameObject holder = new GameObject("Fx Screen Cut");
        holder.transform.position = (a + b) * 0.5f;
        holder.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        holder.transform.localScale = new Vector3(1f, thickness / Mathf.Max(0.01f, naturalThickness), 1f);
        GameObject fx = Play(frames, holder.transform.position, length, frames.Length / Mathf.Max(0.05f, duration), holder.transform, false, 0.25f, 12);
        if (fx != null)
        {
            fx.transform.localRotation = Quaternion.identity;
            fx.GetComponent<SpriteFxAnimator>().unscaled = true;
        }
        Object.Destroy(holder, duration + 1f);
    }

    // 천지 가르기 불기둥: effect_1 붉은 기둥을 세로로 길게 늘려 위에서 내리꽂힘 + 발밑 금빛 불꽃 (정지 중에도 재생)
    public static void FirePillar(Vector3 ground, float height, float width)
    {
        Sprite[] frames = Crop(S(RedPillar), RedPillar);
        if (frames.Length == 0 || frames[0] == null)
            return;

        float naturalHeight = width * frames[0].bounds.size.y / Mathf.Max(0.01f, frames[0].bounds.size.x);
        GameObject column = new GameObject("Fire Pillar");
        column.transform.position = ground + Vector3.up * height * 0.5f;
        column.transform.localScale = new Vector3(1f, height / Mathf.Max(0.1f, naturalHeight), 1f);   // 세로로만 늘림
        GameObject fx = Play(frames, column.transform.position, width, 16f, column.transform, false, 0.3f, 10);
        if (fx != null)
        {
            fx.GetComponent<SpriteFxAnimator>().unscaled = true;
            fx.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 0.85f, 0.7f, 1f));
        }
        Object.Destroy(column, 2f);

        if (S(GoldSpikeE2) != null)
        {
            GameObject flame = Play(Crop(S(GoldSpikeE2), GoldSpikeE2), ground, width * 1.4f, 14f, null, false, 0.3f, 11);
            if (flame != null)
            {
                flame.GetComponent<SpriteFxAnimator>().unscaled = true;
            }
        }
    }

    // 시너지 팝업용 금빛 고리 / 반짝임 별 프레임 (UI Image에 씀)
    public static Sprite[] PopupRingFrames => S(SRise) != null ? Crop(S(SRise), SRise) : null;          // 시너지 달성: 카드 뒤 금빛 솟구침
    public static Sprite[] PopupStarFrames => S(SSparkleA) != null ? Crop(S(SSparkleA), SSparkleA) : null; // 카드 둘레 큰 반짝임

    // effect_3 빛 폭발 (별 모양)
    public static void StarBurst(Vector3 position, float size)
    {
        Play(Crop(S(StarBurstE3), StarBurstE3), position, size, 16f, null, false, 0.12f, 8);
    }

    // 천지 가르기: 정지 중 X 베기가 그어짐 (앞 3프레임을 정지 시간 동안 unscaled로)
    public static void CrossSlash(Vector3 center, float size, float time)
    {
        Sprite[] frames = Crop(S(CrossE3), CrossE3);
        if (frames.Length < 6)
            return;
        GameObject fx = Play(new[] { frames[0], frames[1], frames[2] }, center, size, 3f / Mathf.Max(0.05f, time), null, false, 0.05f, 12);
        if (fx != null)
        {
            fx.GetComponent<SpriteFxAnimator>().unscaled = true;
        }
    }

    // 천지 가르기: 정지가 풀리며 X가 크게 터지고 잔상
    public static void CrossBurst(Vector3 center, float size)
    {
        Sprite[] frames = Crop(S(CrossE3), CrossE3);
        if (frames.Length < 6)
            return;
        Play(new[] { frames[3], frames[4], frames[5] }, center, size * 1.1f, 12f, null, false, 0.2f, 12);
        GameObject after = Play(new[] { frames[4], frames[5] }, center + new Vector3(0.3f, -0.3f, 0f), size * 1.25f, 6f, null, false, 0.35f, 11);   // 잔상
        if (after != null)
        {
            after.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 0.95f, 0.8f, 0.45f));
        }
    }

    // 활 각성: 화살 뒤로 뻗는 빛 줄기 (화살을 따라다님)
    public static void AttachArrowStreak(Transform arrow, Vector2 dir, float length)
    {
        if (S(ArrowStreakE2) == null)
            return;
        GameObject fx = Play(new[] { Crop(S(ArrowStreakE2), ArrowStreakE2)[0] }, arrow.position - (Vector3)(dir.normalized * length * 0.35f), length * 1.1f, 0.2f, null, false, 3f, 7);
        if (fx != null)
        {
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            fx.transform.SetParent(arrow, true);
            fx.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 0.95f, 0.75f, 0.95f));
        }
    }

    // 두 점을 잇는 속도선 줄기 하나 (effect_2 가로 줄기를 길이만큼 늘림)
    public static StreakLine Streak(Vector3 a, Vector3 b, float thickness, Color color)
    {
        Sprite sprite = S(StreakLinesE2) != null ? Crop(S(StreakLinesE2), StreakLinesE2)[0] : null;
        GameObject go = new GameObject("Streak");
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        GameManager gm = GameManager.instance;
        SpriteRenderer playerSprite = gm != null ? gm.player.GetComponent<SpriteRenderer>() : null;
        if (playerSprite != null)
        {
            renderer.sharedMaterial = playerSprite.sharedMaterial;
            renderer.sortingLayerID = WeaponLayer(playerSprite);
            renderer.sortingOrder = 9;
        }
        StreakLine line = go.AddComponent<StreakLine>();
        line.Init(renderer, a, b, thickness, color);
        return line;
    }

    // 거대 화살 꼬리 빛 입자
    public static void TrailSpark(Vector3 position)
    {
        GameObject fx = Play(Crop(Sheet3, Sparkle), position + (Vector3)(Random.insideUnitCircle * 0.25f), Random.Range(0.35f, 0.6f), 14f, null, false, 0.15f, 6);
        if (fx != null)
        {
            fx.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 0.95f, 0.7f, 0.9f));
        }
    }

    // 활 끝에 빛이 모이는 연출 (주위 반짝임이 한 점으로 모여들고, 가운데 effect_3 빛이 점점 커짐)
    public static void BeamCharge(Transform follow, Vector3 localTip, float time)
    {
        if (time <= 0f)
            return;
        Vector3 tip = follow.position + localTip;
        for (int i = 0; i < 8; i++)
        {
            Vector2 from = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.3f);
            GameObject fx = Play(Crop(Sheet3, Sparkle), tip + (Vector3)from, 0.35f, 1f / Mathf.Max(0.05f, time / Sparkle.Length), null, false, 0.05f, 6);
            if (fx != null)
            {
                fx.GetComponent<SpriteFxAnimator>().SetDrift(-(Vector3)from / time);
            }
        }
        Sprite[] burst = Crop(S(StarBurstE3), StarBurstE3);
        if (burst.Length >= 4)
        {
            Play(new[] { burst[0], burst[1], burst[2], burst[3] }, tip, 1f, 4f / Mathf.Max(0.05f, time), null, false, 0.05f, 7);
        }
    }

    // 반짝임 한 프레임 (UI 빛 입자용, 0이 가장 작음)
    public static Sprite SparkleSprite(int index)
    {
        return Crop(Sheet3, Sparkle[Mathf.Clamp(index, 0, Sparkle.Length - 1)]);
    }

    // 바닥 범위 표시 (폭탄 통: 붉은 경고 원, 마비 두꺼비: 파란 물결)
    public static SpriteFxAnimator BombRange(Transform device, float diameter)
    {
        // 프레임마다 가시 모양이 달라 꾸물거리므로, 다 그려진 조준 원 한 장만 크기 고정으로 (투명도만 은은하게)
        Sprite[] ring = Crop(S(RangeCircle), RangeCircle);
        Sprite still = ring.Length > 3 ? ring[3] : (ring.Length > 0 ? ring[0] : null);
        SpriteFxAnimator a = Loop(new[] { still }, device, Vector3.zero, ArtFit(still, diameter * Config.rangeSpikeScale), 1f, -1f, Ground, new Color(1f, 1f, 1f, 0.85f));
        PutUnder(a, device);
        if (a != null)
        {
            a.steady = true;
            a.gameObject.AddComponent<FxPulse>();
        }
        return a;
    }

    public static SpriteFxAnimator ToadRange(Transform device, float diameter)
    {
        Sprite[] ring = Crop(S(ZapRing), ZapRing);
        Sprite still = ring.Length > 2 ? ring[2] : (ring.Length > 0 ? ring[0] : null);   // 고리가 다 그려진 한 장 (기준점 = 고리 중심)
        // 두꺼비 몸 가운데가 고리 중심에 오게 (발밑에 붙어 보이지 않게)
        SpriteRenderer body = device.GetComponentInChildren<SpriteRenderer>();
        float lift = body != null ? body.bounds.center.y - device.position.y : 0f;
        SpriteFxAnimator a = Loop(new[] { still }, device, Vector3.up * lift, ArtFit(still, diameter), 1f, -1f, Ground, new Color(1f, 1f, 1f, 0.85f));   // 고리 폭 = 실제 범위
        PutUnder(a, device);
        if (a != null)
        {
            a.stretchY = Config.toadRangeStretchY;                 // 납작한 고리를 세로로 늘려 두꺼비를 감싸게
            a.steady = true;                                       // 크기 고정 (꾸물거리지 않게)
            a.gameObject.AddComponent<FxPulse>();                  // 위치는 고정, 투명도만 은은하게
        }
        return a;
    }

    // 범위 표시를 기기(폭탄·두꺼비) 바로 아래 순서로 (기기가 위에 보이게)
    private static void PutUnder(SpriteFxAnimator fx, Transform device)
    {
        if (fx == null || device == null) return;
        SpriteRenderer r = fx.GetComponent<SpriteRenderer>();
        SpriteRenderer dev = null;
        foreach (SpriteRenderer s in device.GetComponentsInChildren<SpriteRenderer>())
            if (s != r && s.GetComponent<SpriteFxAnimator>() == null) { dev = s; break; }
        if (dev == null || r == null) return;
        // 범위는 이펙트 레이어 바닥 순서, 기기는 그 바로 위 (둘 다 바닥·나무에 가려지지 않음)
        r.sortingLayerID = WeaponLayer(dev);
        r.sortingOrder = -21;
        dev.sortingLayerID = r.sortingLayerID;
        dev.sortingOrder = -20;
    }

    // ---------------------------------------------------------------
    // 버프 오라 (캐릭터 뒤에 반복 재생)
    // ---------------------------------------------------------------
    // 음식 섭취: 발밑 금빛 반짝임 고리 + 양옆 작은 불꽃 (지속 시간 동안)
    public static void FoodAura(Transform target, float duration)
    {
        Vector3 feet = FeetOffset(target);
        Loop(Crop(S(SGoldSwirl), SGoldSwirl), target, feet + Vector3.up * 0.15f, 1.6f, 10f, duration, -1, new Color(1f, 0.9f, 0.7f, 0.9f));
        Loop(Crop(S(SOrbs), SOrbs), target, feet + Vector3.up * 0.7f, 1.3f, 8f, duration, -2, new Color(1f, 1f, 1f, 0.85f));
        Loop(Crop(Sheet3, SmallFire), target, feet + new Vector3(-0.45f, 0.2f, 0f), 0.35f, 8f, duration, -2, Color.white);
        Loop(Crop(Sheet3, SmallFire), target, feet + new Vector3(0.45f, 0.2f, 0f), 0.35f, 8f, duration, -2, Color.white);
        Play(Crop(Sheet3, SparkleCluster), AboveHead(target), 0.9f, 8f, target, false, 0.2f, 6);
    }

    // 미니 데몬 (가쟈부): 발밑 붉은 마법진 + 양옆 불꽃 (버프가 끝나면 StopAura)
    public static GameObject DemonAura(Transform target)
    {
        GameObject root = AuraRoot(target, "Demon Aura");
        // 기둥 그림은 고리가 아래쪽에 있어, 고리 중심이 캐릭터 발(좌우 가운데)에 오도록 그림 중심을 올림
        const float ringSize = 1.4f;
        Vector3 feet = FeetOffset(target, 0.12f);
        float lift = ringSize * (125f / 140f) * PillarRingDrop;
        // 캐릭터 뒤에서 붉은 불꽃이 활활 타오름 (가운데 큰 불꽃 + 양옆 불꽃, 박자를 엇갈리게) + 앞쪽으로 작은 불씨
        Sprite[] f = Crop(Sheet3, DemonFlame);
        if (f.Length == 3)
        {
            float big = Config.demonFlameSize;
            Color fire = Config.demonFlameColor;
            // 불꽃 그림 바닥(칸 아래 4px 위)이 캐릭터 다리 끝에 오도록: 그림 중심 = 바닥 + 크기 x 0.5
            Vector3 legs = FeetOffset(target, 0f) + Vector3.up * Config.demonFlameBaseOffset;
            Loop(new[] { f[0], f[1], f[2], f[1] }, root.transform, legs + Vector3.up * big * 0.5f, big, 9f, -1f, -3, fire);
            float side = big * 0.75f;
            Loop(new[] { f[1], f[2], f[1], f[0] }, root.transform, legs + new Vector3(-big * 0.14f, side * 0.5f, 0f), side, 10f, -1f, -4, fire, true);
            Loop(new[] { f[2], f[1], f[0], f[1] }, root.transform, legs + new Vector3(big * 0.14f, side * 0.5f, 0f), side, 11f, -1f, -4, fire);
        }
        Loop(Crop(S(SSparkle), SSparkle), root.transform, feet + Vector3.up * 0.9f, ringSize * 0.9f, 8f, -1f, 7, new Color(1f, 0.6f, 0.45f, 0.7f));
        Play(Crop(Sheet3, SparkleCluster), AboveHead(target), 1f, 8f, target, false, 0.2f, 6);
        return root;
    }

    // 흔들흔들 저격 모드: 발밑 붉은 조준 원 + 머리 위 조준경 (저격 중이라는 표시)
    public static GameObject SniperAura(Transform target)
    {
        GameObject root = AuraRoot(target, "Sniper Aura");
        Loop(Crop(S(RedCircle), RedCircle), root.transform, FeetOffset(target), 1.9f, 5f, -1f, -1, new Color(1f, 1f, 1f, 0.85f));
        Sprite up = DamageUpIcon();
        Loop(new[] { up != null ? up : Crop(S(Target), Target)[0] }, root.transform, AboveHead(target, 0.3f) - target.position, 0.5f, 1f, -1f, 6, Color.white);
        return root;
    }

    // 공격력 증가 아이콘 (레벨업 '공격력 증가' 카드 그림)
    private static Sprite DamageUpIcon()
    {
        if (global::LevelUp.instance == null || global::LevelUp.instance.Items == null) return null;
        foreach (Item it in global::LevelUp.instance.Items)
            if (it != null && it.data != null && it.data.itemType == ItemData.ItemType.Damage_Up) return it.data.itemIcon;
        return null;
    }

    // 랜스 방어(패링) 자세: 방패 뒤로 은은한 방패 빛 + 발밑 흙먼지 (캐릭터 뒤)
    public static GameObject GuardAura(Transform target)
    {
        GameObject root = AuraRoot(target, "Guard Aura");
        Loop(new[] { Crop(Sheet2, ShieldGlow) }, root.transform, Vector3.zero, 1.9f, 1f, -1f, -1, new Color(1f, 1f, 1f, 0.35f));
        Loop(Crop(S(SRing), SRing), root.transform, FeetOffset(target, 0.1f), 1.8f, 10f, -1f, -2, new Color(1f, 1f, 1f, 0.8f));
        Play(Crop(S(DustPuff), DustPuff), target.position + Vector3.down * 0.35f, 1.3f, 14f, null, false, 0.1f, -2);
        return root;
    }

    // 랜스 돌진 모드 (피해 감소): 캐릭터 뒤 바람 소용돌이 + 발밑 흙먼지 소용돌이
    public static GameObject LanceChargeAura(Transform target, float size)
    {
        GameObject root = AuraRoot(target, "Charge Aura");
        Loop(Crop(Sheet3, WindSwirl), root.transform, Vector3.zero, size, 10f, -1f, -1, new Color(1f, 1f, 1f, 0.7f));
        Loop(Crop(S(DustSwirl), DustSwirl), root.transform, FeetOffset(target, 0.2f), size * 0.8f, 9f, -1f, -2, new Color(1f, 1f, 1f, 0.8f));
        return root;
    }

    // 대검 기 모으기: 캐릭터 주위로 풍압이 감도는 바람 소용돌이 + 발밑 흙먼지 (단계마다 커짐)
    public static GameObject ChargeAura(Transform target, float size)
    {
        GameObject root = AuraRoot(target, "Charge Aura");
        Loop(Crop(Sheet3, WindSwirl), root.transform, Vector3.zero, size, 11f, -1f, -1, new Color(1f, 1f, 1f, 0.75f));
        Loop(Crop(S(DustSwirl), DustSwirl), root.transform, FeetOffset(target, 0.2f), size * 0.85f, 10f, -1f, -2, new Color(1f, 1f, 1f, 0.85f));
        return root;
    }

    private static GameObject AuraRoot(Transform target, string name)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(target, false);
        return root;
    }

    // 오라 색 (피격 번쩍임, 단계 색 등)
    public static void TintAura(GameObject aura, Color color)
    {
        if (aura == null)
            return;
        foreach (SpriteFxAnimator animator in aura.GetComponentsInChildren<SpriteFxAnimator>())
        {
            animator.SetColor(color);
        }
    }

    // 오라를 서서히 끄고 제거
    public static void StopAura(GameObject aura)
    {
        if (aura == null)
            return;
        foreach (SpriteFxAnimator animator in aura.GetComponentsInChildren<SpriteFxAnimator>())
        {
            animator.Stop();
        }
        Object.Destroy(aura, 0.5f);
    }

    // ---------------------------------------------------------------
    // 무기
    // ---------------------------------------------------------------
    // 쌍검 베기 (조준 방향): 흰 초승달 + 잔상 초승달, 교차 베기는 푸른 초승달을 겹치고 불꽃 튐
    public static void DualSlash(Vector3 position, Vector2 direction, bool cross)
    {
        if (WeaponSynergy.IsAwakening)
        {
            // 각성: 큰 칼바람 초승달 + 뒤따르는 겹바람 (기본 베기 그림 대신)
            Vector2 d = direction.normalized;
            PlayDir(S(VWindBlade), VWindBlade, position + (Vector3)(d * 0.2f), direction, cross ? 3.4f : 3f, 24f, null, 0.04f, 5);
            GameObject wind = PlayDir(S(VChevron), VChevron, position - (Vector3)(d * 0.3f), direction, 2.2f, 24f, null, 0.03f, 4);
            if (wind != null) wind.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 1f, 1f, 0.7f));
        }
        else
        {
            // 초승달 한 장만 (잔상을 겹치지 않음), 교차 베기만 작은 불꽃
            PlayDir(S(VWhite), VWhite, position, direction, cross ? 2.4f : 2.1f, 26f, null, 0.03f, 5);
        }
        if (cross)
        {
            PlayDir(S(VDiagSpark), VDiagSpark, position + (Vector3)(direction.normalized * 0.9f), direction, 1f, 24f, null, 0.03f, 6);
        }
    }

    // 메인무기 공격 끝에 붙는 작은 금빛 반짝임 (강해진 느낌)
    private static float boostWindow;
    private static int boostCount;

    public static void BoostSpark(Vector3 position, float size)
    {
        if (!WeaponSynergy.IsAwakening) return;                     // 각성 시너지(메인무기·짝 무기 5레벨)일 때만
        if (Time.time - boostWindow > 0.2f) { boostWindow = Time.time; boostCount = 0; }
        if (++boostCount > 3) return;                               // 연속 공격에도 과하지 않게
        // 각성: 금빛 회전 고리가 휘감고, 부채꼴 빛이 터지며, 큰 별이 반짝임
        float s = size * 2.2f;
        if (boostCount == 1)
        {
            GameObject ring = PlayTinted(Crop(S(SGoldSwirl), SGoldSwirl), position, s * 1.2f, 22f, new Color(1f, 0.92f, 0.6f, 0.95f), null, 0.03f, 6);
            if (ring != null) ring.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Play(Crop(S(SFan), SFan), position + Vector3.up * s * 0.1f, s, 22f, null, false, 0.03f, 7);
        }
        Play(Crop(S(SSparkleA), SSparkleA), position + (Vector3)(Random.insideUnitCircle * s * 0.3f), s * 0.6f, 20f, null, false, 0.03f, 8);
        Play(Crop(S(SSparkle), SSparkle), position + (Vector3)(Random.insideUnitCircle * s * 0.4f), s * 0.7f, 18f, null, false, 0.03f, 8);
    }

    // 쌍검 칼이 지나간 자리 바람 궤적 (swing: 칼이 움직이는 방향, length: 판정 길이)
    public static void BladeTrail(Vector3 position, Vector2 swing, float length)
    {
        GameObject fx = PlayDir(S(VCrescent), VCrescent, position, swing, length * 1.2f, 26f, null, 0.05f, 4);
        if (fx != null)
        {
            fx.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 1f, 1f, 0.3f));
        }
    }

    // 쌍검 회전 베기 (회전하는 동안 계속 호출, 캐릭터 뒤): 회오리 고리 + 금빛 회전
    public static void DualWhirl(Transform target, float size)
    {
        GameObject whirl = Play(Crop(S(VWhirl), VWhirl), target.position, size * 1.1f, 18f, target, false, 0.05f, -1);
        if (whirl != null)
        {
            whirl.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
        }
    }

    // 쌍검 돌진 회전베기: 앞쪽 섬광 + 지나간 자리 속도선·흙먼지
    public static void DualDash(Vector3 position, Vector2 direction)
    {
        Vector2 dir = direction.normalized;
        PlayDir(S(VThrust), VThrust, position + (Vector3)(dir * 1.4f), direction, 3.2f, 18f, null, 0.06f, 5);
        PlayDir(S(VDashDust), VDashDust, position - (Vector3)(dir * 0.4f) + Vector3.down * 0.25f, direction, 1.4f, 16f, null, 0.1f, -2);
        PlayTinted(Crop(S(SDust), SDust), position + Vector3.down * 0.3f, 1.6f, 14f, new Color(1f, 1f, 1f, 0.8f), null, 0.1f, -3);
    }

    // 활 발사: 시위를 놓을 때 짧은 섬광과 바람 (부채꼴이면 조금 크게)
    public static void ArrowShot(Vector3 position, Vector2 direction, bool fan)
    {
        Vector2 dir = direction.normalized;
        GameObject flash = PlayDir(S(VThrust), VThrust, position + (Vector3)(dir * 0.9f), direction, fan ? 1.5f : 1.2f, 26f, null, 0.03f, 4);
        if (flash != null) flash.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 1f, 1f, 0.75f));
        PlayTinted(Crop(S(SPuff), SPuff), position + (Vector3)(dir * 0.3f), fan ? 0.8f : 0.6f, 20f, new Color(1f, 1f, 1f, 0.6f), null, 0.04f, 3);
        if (WeaponSynergy.IsAwakening) AwakenedShotWind(position, dir, fan ? 1.25f : 1f);
    }

    // 화살비 조준 표시 (조준경 모양)
    public static Sprite RainIndicatorSprite => Crop(S(Target), Target)[0];

    // 화살비 범위 (지속 시간 동안 바닥에 붉은 조준 원)
    public static SpriteFxAnimator RainArea(Vector3 center, float diameter, float duration)
    {
        // 쏟아지는 동안: 실제 공격 범위(지름)를 흙빛 고리 한 장으로 둥글게 표시 (크기 고정, 꾸물거리지 않음)
        Sprite[] ring = Crop(S(RainRing), RainRing);
        Sprite still = ring.Length > 2 ? ring[2] : (ring.Length > 0 ? ring[0] : null);   // 다 그려진 고리 (옆 프레임과 겹치지 않는 칸)
        SpriteFxAnimator a = Loop(new[] { still }, null, center, ArtFit(still, diameter), 1f, duration, Ground, new Color(1f, 0.95f, 0.85f, 0.95f));
        if (a != null && still != null)
        {
            a.steady = true;
            a.stretchY = ArtAspect(still);                         // 납작한 고리를 원으로 (고리 폭 = 실제 범위 지름)
        }
        return a;
    }

    // 화살비 범위 가장자리에서 흙먼지 구름이 피어오름 (Attack_vfx_5, 바깥쪽으로 흘러감)
    public static void RainEdgeDust(Vector3 center, float radius)
    {
        Sprite[] f = Crop(Config.fxVfx5, RainDust);
        if (f.Length == 0) return;
        float a = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector3 p = center + (Vector3)(dir * radius * Random.Range(0.85f, 1f));
        float size = Config.rainDustSize * Random.Range(0.85f, 1.15f);
        // 구름 그림은 오른쪽으로 흘러가므로, 왼쪽 가장자리에서는 좌우를 뒤집어 바깥쪽으로 흐르게
        GameObject fx = PlayTinted(f, p, size, Random.Range(9f, 12f), new Color(1f, 0.97f, 0.9f, 0.85f), null, 0.15f, -3);
        if (fx != null)
        {
            fx.GetComponent<SpriteRenderer>().flipX = dir.x < 0f;
            fx.GetComponent<SpriteFxAnimator>().SetDrift(new Vector3(dir.x * 0.3f, dir.y * 0.15f + 0.1f, 0f));
        }
    }

    // 각성 날카로운 총알 뒤에 붙는 빛 궤적 (오른쪽을 향함)
    public static Sprite SlugStreakSprite
    {
        get { Sprite[] f = Crop(S(VThrust), VThrust); return f.Length > 3 ? f[3] : null; }
    }

    // 각성 날카로운 총알이 지나간 자리: 가는 바람결 + 가끔 금빛 반짝임
    public static void SlugWake(Vector3 position, Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject wind = Stretched(Crop(S(VThrust), VThrust), position - (Vector3)(direction.normalized * 0.5f), 1.5f, 0.32f, angle, 26f, 0.03f, 5, false, false);
        if (wind != null)
        {
            SpriteFxAnimator a = wind.GetComponentInChildren<SpriteFxAnimator>();
            if (a != null) a.SetColor(new Color(1f, 0.88f, 0.5f, 0.8f));
        }
        if (Random.value < 0.6f)
            PlayTinted(Crop(S(SSparkle), SSparkle), position + (Vector3)(Random.insideUnitCircle * 0.2f), 0.45f, 20f, new Color(1f, 0.9f, 0.55f, 0.9f), null, 0.03f, 6);
    }

    // 헤비보우건 은신: 몸 주위로 나뭇잎이 휘날림 (Stop으로 끔)
    public static SpriteFxAnimator StealthLeaves(Transform target)
    {
        return Loop(Crop(S(SLeaves), SLeaves), target, Vector3.up * 0.1f, 1.6f, 10f, -1f, 5, new Color(1f, 1f, 1f, 0.9f));
    }

    // 화살이 땅에 박힐 때: 작은 땅 가시 + 흙먼지
    public static void ArrowLand(Vector3 position)
    {
        Play(Crop(S(VSpike), VSpike), position + Vector3.up * 0.1f, 0.7f, 18f, null, Random.value < 0.5f, 0.05f, -3);
    }

    // 대검 베기 궤적 (회전 공격이 끝날 때): 큰 흰 초승달 + 휘두름 먼지 + 불꽃
    public static void GreatSlash(Vector3 position, Vector2 direction, float size)
    {
        PlayDir(S(VWhite), VWhite, position, direction, size * 1.15f, 18f, null, 0.08f, 5);
        PlayDir(S(VSwipeDust), VSwipeDust, position + Vector3.down * 0.2f, direction, size * 0.9f, 16f, null, 0.1f, -2);
        PlayDir(S(VDiagSpark), VDiagSpark, position + (Vector3)(direction.normalized * size * 0.35f), direction, size * 0.5f, 20f, null, 0.04f, 6);
        BoostSpark(position + (Vector3)(direction.normalized * size * 0.3f), 0.8f);
    }

    // 대검 검기 스프라이트 (흰 초승달, 오른쪽을 향함)
    public static Sprite SwordWaveSprite => Crop(S(VWhite), VWhite)[3];

    // 대검 기 모으기 단계 상승: 발밑 회오리 + 흙먼지, 3단계는 금빛 기둥
    public static void ChargeUp(Transform target, int stage)
    {
        float size = 1.6f + stage * 0.4f;
        PlayTinted(Crop(S(VWhirl), VWhirl), target.position + Vector3.down * 0.2f, size * 1.2f, 16f, new Color(1f, 1f, 1f, 0.85f), target, 0.05f, -1);
        PlayTinted(Crop(S(SDust), SDust), target.position + new Vector3(-0.45f, -0.35f, 0f), size * 0.55f, 14f, new Color(1f, 1f, 1f, 0.8f), null, 0.1f, -2);
        PlayTinted(Crop(S(SDust), SDust), target.position + new Vector3(0.45f, -0.35f, 0f), size * 0.55f, 14f, new Color(1f, 1f, 1f, 0.8f), null, 0.1f, -2);
        if (stage >= 3)
        {
            Play(Crop(S(SGoldPillar), SGoldPillar), target.position + FeetOffset(target) + Vector3.up * 0.6f, size * 0.9f, 14f, target, false, 0.1f, -3);
        }
    }

    // 대검 모아 베기 발동: 땅이 터지고 갈라지며, 흙 폭발·돌무더기·충격 고리
    public static void GreatCharge(Vector3 position, Vector2 direction, int stage)
    {
        float size = 2.4f + stage * 0.8f;
        Vector2 dir = direction.normalized;
        Vector3 front = position + (Vector3)(dir * size * 0.35f);
        Vector3 floor = front + Vector3.down * 0.4f;              // 바닥 기준 그림은 이 높이에서 솟음
        Play(Crop(S(VGround), VGround), floor, size * 1.1f, 18f, null, false, 0.12f, 3);
        Play(Crop(S(VCrack), VCrack), floor, size * 1.1f, 16f, null, false, 0.15f, -3);
        Play(Crop(S(VDustBlast), VDustBlast), floor, size * 1.0f, 14f, null, false, 0.15f, 2);
        Play(Crop(S(VRocks), VRocks), floor, size * 0.8f, 14f, null, false, 0.15f, 4);
        Play(Crop(S(VShock), VShock), front + Vector3.down * 0.25f, size * 1.3f, 18f, null, false, 0.05f, -3);
    }

    // 헤비보우건 발사: muzzle = 총구 끝. 별 섬광 + 앞쪽 섬광 줄기, 기본 사격은 연기가 위로 흩어짐
    public static void Muzzle(Vector3 muzzle, Vector2 direction, bool rapid)
    {
        Vector2 dir = direction.normalized;
        // 총구에서 옅은 연기가 피어올라 흩어짐 (연사는 작게)
        float size = rapid ? 0.8f : 0.95f;
        GameObject smoke = PlayTinted(Crop(S(SPuff), SPuff), muzzle + (Vector3)(dir * size * 0.3f), size, rapid ? 14f : 12f,
            new Color(0.88f, 0.88f, 0.88f, rapid ? 0.6f : 0.6f), null, 0.1f, 3);
        if (WeaponSynergy.IsAwakening && (!rapid || Time.time - lastRapidWind > 0.18f))
        {
            if (rapid) lastRapidWind = Time.time;                  // 연사는 너무 겹치지 않게
            AwakenedShotWind(muzzle - (Vector3)(dir * 0.5f), dir, rapid ? 0.85f : 1.15f);
            PlayTinted(Crop(S(SDust), SDust), muzzle + (Vector3)(dir * 0.2f) + Vector3.down * 0.15f, rapid ? 0.9f : 1.3f, 14f,
                new Color(0.92f, 0.92f, 0.92f, 0.7f), null, 0.1f, 3);
        }
        if (smoke != null)
        {
            smoke.GetComponent<SpriteFxAnimator>().SetDrift(new Vector3(dir.x * 0.5f, 0.5f, 0f));
        }
    }

    private static float lastRapidWind;

    // 각성 사격: 발밑 뒤로 흙먼지가 튀고, 앞으로 겹바람이 뻗음 (활·헤비보우건)
    private static void AwakenedShotWind(Vector3 position, Vector2 dir, float scale)
    {
        GameObject dust = PlayDir(S(VDashDust), VDashDust, position - (Vector3)(dir * 0.35f) + Vector3.down * 0.2f, dir, 1.03f * scale, 18f, null, 0.08f, 3);
        if (dust != null) dust.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 1f, 1f, 0.8f));
        GameObject wind = PlayDir(S(VChevron), VChevron, position + (Vector3)(dir * 1.1f), dir, 1.6f * scale, 24f, null, 0.03f, 5);
        if (wind != null) wind.GetComponent<SpriteFxAnimator>().SetColor(new Color(1f, 1f, 1f, 0.75f));
    }

    // 날카로운 칼바람 초승달 한 장 (오른쪽을 향함) - 칼날 폭풍 유도 검풍
    public static Sprite WindBladeSprite => Crop(S(VWindBlade), VWindBlade)[3];

    // 칼날 호위 칼바람: 몸 주위를 도는 회오리 (target을 따라다님, Stop으로 끔)
    public static SpriteFxAnimator GuardWind(Transform target, float diameter)
    {
        return Loop(Crop(S(VWhirl), VWhirl), target, Vector3.zero, diameter, 14f, -1f, 3, new Color(1f, 1f, 1f, 0.75f));
    }

    // 칼날 호위 칼바람 날 하나 (blade를 따라 돌며 진행 방향을 향함)
    public static SpriteFxAnimator GuardWindBlade(Transform blade, float size)
    {
        Sprite[] f = Crop(S(VWindBlade), VWindBlade);
        if (f == null || f.Length < 5) return null;
        SpriteFxAnimator a = Loop(new[] { f[2], f[3], f[4], f[3] }, blade, Vector3.zero, size, 12f, -1f, 5, new Color(1f, 1f, 1f, 0.9f));
        if (a != null) a.transform.localRotation = Quaternion.identity;
        return a;
    }

    // 헤비보우건 연사탄 (빛나는 탄 궤적, 오른쪽을 향함)
    // 반짝임 (sub_effect_1, 작은 별이 커졌다 사라짐) - UI 장식용
    public static Sprite[] TwinkleFrames => Crop(Sheet3, Sparkle);

    // 나비 (sub_effect_1) - UI 장식용
    public static Sprite ButterflySprite => Crop(Sheet3, Butterflies)[0];

    // 작은 불꽃 (sub_effect_1) - UI 장식용
    public static Sprite[] SmallFireFrames => Crop(Sheet3, SmallFire);

    public static Sprite RapidBulletSprite => Crop(Sheet2, BulletTrail);

    // 랜스 찌르기: 창끝 앞으로 찌르기 섬광 + 끝에 겹화살표 풍압
    public static void LanceThrust(Vector3 position, Vector2 direction, float length)
    {
        Vector2 dir = direction.normalized;
        // 가늘고 날카로운 찌르기 섬광 한 줄 (풍압을 넓게 퍼뜨리지 않음)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Stretched(Crop(S(VThrust), VThrust), position + (Vector3)(dir * (0.9f + length * 0.55f)), length * 1.3f, 0.32f, angle, 24f, 0.03f, 5, false, false);
        BoostSpark(position + (Vector3)(dir * (1f + length)), 0.5f);
    }

    // 랜스 패링 성공: 반투명 방패 + 충격 고리 + 별 섬광 + 양옆 불꽃
    public static void Parry(Vector3 position, float size)
    {
        PlayTinted(Crop(Sheet2, ShieldParry), position, size * 0.75f, 10f, new Color(1f, 1f, 1f, 0.5f), null, 0.25f, -1);
        Play(Crop(S(VShock), VShock), position, size * 0.9f, 20f, null, false, 0.05f, -2);
        Play(Crop(S(PStar), PStar), position, size * 0.6f, 24f, null, false, 0.04f, 6);
        PlayDir(S(VDiagSpark), VDiagSpark, position + Vector3.left * size * 0.3f, Vector2.left, size * 0.4f, 22f, null, 0.04f, 5);
        PlayDir(S(VDiagSpark), VDiagSpark, position + Vector3.right * size * 0.3f, Vector2.right, size * 0.4f, 22f, null, 0.04f, 5);
    }

    // 랜스 돌진 모드 질주: 속도선 + 흙먼지 (이동 반대쪽)
    public static void ChargeTrail(Vector3 position, Vector2 direction)
    {
        PlayDir(S(VDashDust), VDashDust, position + Vector3.down * 0.25f, direction, 1.03f, 16f, null, 0.08f, -1);
    }

    // ---------------------------------------------------------------
    // 필살기 소환
    // ---------------------------------------------------------------
    // 꿀벌레 (아이루): 반짝이는 빛과 나비가 모이며 나타남
    public static void HoneyBugSpawn(Vector3 position)
    {
        position += Vector3.down * 0.2f;                          // 오라 위치 조금 아래로
        PlayTinted(Crop(Sheet3, GoldCloud), position, 1.6f, 4f, new Color(1f, 1f, 1f, 0.7f), null, 0.4f, Ground);
        Play(Crop(Sheet3, SparkleCluster), position + Vector3.up * 0.3f, 1.2f, 8f, null, false, 0.3f, 6);
        Play(Crop(S(SOrbs), SOrbs), position + Vector3.up * 0.4f, 1.6f, 10f, null, false, 0.2f, 7);
        Play(Crop(S(SLeaves), SLeaves), position, 1.8f, 10f, null, false, 0.2f, 6);
        GameObject butterflies = PlayTinted(Crop(Sheet3, Butterflies), position, 1.6f, 3f, new Color(1f, 1f, 1f, 0.9f), null, 0.5f, 7);
        if (butterflies != null)
        {
            butterflies.GetComponent<SpriteFxAnimator>().SetDrift(Vector3.up * 0.5f);
        }
    }

    // 미끼 설치 (보와보와): 흙먼지가 피어오르고 꽃잎이 흩날림
    // 미끼가 있는 동안 주위에 나뭇잎·반짝임이 계속 흩날림 (Stop 으로 끝냄)
    public static GameObject DecoyAura(Transform decoy)
    {
        GameObject root = AuraRoot(decoy, "Decoy Aura");
        Loop(Crop(S(SLeaves), SLeaves), root.transform, Vector3.down * 0.15f, 1.8f, 8f, -1f, 5, new Color(1f, 1f, 1f, 0.85f));
        Loop(Crop(S(SSparkle), SSparkle), root.transform, Vector3.up * 0.2f, 1.2f, 9f, -1f, 6, new Color(1f, 1f, 1f, 0.8f));
        return root;
    }

    public static void DecoySpawn(Vector3 position)
    {
        PlayTinted(Crop(Sheet3, DustCloud), position + Vector3.down * 0.2f, 1.8f, 12f, new Color(1f, 1f, 1f, 0.85f), null, 0.2f, -3);
        Play(Crop(S(SPuff), SPuff), position, 2f, 14f, null, false, 0.1f, 5);
        Play(Crop(S(SSparkleA), SSparkleA), position + Vector3.up * 0.5f, 1.2f, 14f, null, false, 0.1f, 6);
        Play(Crop(Sheet3, Ripple), position + Vector3.down * 0.3f, 1.8f, 8f, null, false, 0.25f, Ground);
        GameObject petals = PlayTinted(Crop(Sheet3, Petals), position + Vector3.up * 0.4f, 1.2f, 3f, new Color(1f, 1f, 1f, 0.9f), null, 0.5f, 6);
        if (petals != null)
        {
            petals.GetComponent<SpriteFxAnimator>().SetDrift(new Vector3(0.3f, 0.4f, 0f));
        }
    }

    // ---------------------------------------------------------------
    // 보스 (보스가 커서 이펙트도 크게)
    // ---------------------------------------------------------------
    // 돌진 예고: 머리 위 "!!" + 발밑 붉은 경고 원
    public static void BossWarning(Transform boss, float duration)
    {
        SpriteRenderer renderer = boss.GetComponent<SpriteRenderer>();
        float top = renderer != null ? renderer.bounds.extents.y : 2f;
        Play(Crop(S(DoubleExclaim), DoubleExclaim)[0], boss.position + Vector3.up * (top + 0.8f), 2.2f, duration, boss, 8);
        Play(Crop(S(RedCircle), RedCircle), boss.position + Vector3.down * (top * 0.7f), 9f, 3f / Mathf.Max(0.3f, duration), boss, false, 0.2f, -1);
    }

    // 화염 발사: 큰 불꽃 폭발
    public static void BossFire(Vector3 position)
    {
        Play(Crop(S(FireBoom), FireBoom), position, 7f, 14f, null, false, 0.25f, 8);
        Play(Crop(S(StarRing), StarRing), position, 4f, 14f, null, false, 0.1f, 9);
    }

    // 칼날 발사: 흰 칼날 궤적
    public static void BossSlash(Vector3 position, Vector2 direction)
    {
        PlayDir(S(WhiteSlash), WhiteSlash, position, direction, 6.5f, 16f, null, 0.15f, 8);
    }

    // 땅 튀김 (보스 돌진 출발, 점프)
    public static void GroundBurst(Vector3 position, float size)
    {
        Play(Crop(S(GroundBurstFrames), GroundBurstFrames), position, size, 22f, null, false, 0.2f, 1);
        Play(Crop(S(DustSwirl), DustSwirl), position, size * 0.8f, 14f, null, false, 0.1f, 0);
    }

    // 착지: 큰 땅 터짐 + 흙먼지 + 붉은 기둥
    public static void LandingRing(Vector3 position, float size)
    {
        Play(Crop(S(GroundBurstFrames), GroundBurstFrames), position, size, 20f, null, false, 0.25f, 1);
        Play(Crop(S(DustPuff), DustPuff), position + Vector3.left * size * 0.3f, size * 0.5f, 10f, null, false, 0.2f, 0);
        Play(Crop(S(DustPuff), DustPuff), position + Vector3.right * size * 0.3f, size * 0.5f, 10f, null, true, 0.2f, 0);
        Play(Crop(S(RedPillar), RedPillar), position, size * 0.35f, 14f, null, false, 0.1f, 2);
    }

    // 착지 직후 튀는 파편
    public static void RockBurst(Vector3 position, float size)
    {
        Play(Crop(S(Crystal), Crystal), position, size, 14f, null, false, 0.2f, 2);
        Play(Crop(Sheet3, Rocks), position + Vector3.up * 0.5f, size * 1.2f, 4f, null, false, 0.3f, 3);
    }
}
