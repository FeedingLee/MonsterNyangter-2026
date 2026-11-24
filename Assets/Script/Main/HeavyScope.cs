using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 헤비보우건 정밀 저격 (R 스킬): 은신 상태, 스코프 화면, 탄 HUD
// 그림은 RunConfig.heavySkillSheet (Heavy_Skill.png: 스코프 열림 8장 / 발사 섬광 8장 / 닫힘 8장 / 탄 아이콘 / 탄창)

// 은신: 일반 몬스터는 플레이어를 쫓지 못하고(은신 시작 때 향하던 방향으로 그대로 감), 보스는 마지막으로 본 위치를 노림
public static class HeavyStealth
{
    public static bool Active { get; private set; }
    public static Vector2 LastSeen { get; private set; }
    public static int Version { get; private set; }            // 은신을 새로 시작할 때마다 바뀜 (몬스터가 방향을 다시 정함)

    public static void Begin(Vector2 playerPosition)
    {
        Active = true;
        LastSeen = playerPosition;
        Version++;
    }

    public static void End()
    {
        Active = false;
    }
}

public static class HeavySheet
{
    private static Sprite Sheet => RunConfig.Instance != null ? RunConfig.Instance.heavySkillSheet : null;

    private static readonly float[][] ScopeCenters =
    {
        new[] { 115f, 340f, 558.5f, 777.5f, 996.5f, 1217f, 1437.5f, 1654.5f },
        new[] { 115f, 336f, 555.5f, 776.5f, 995f, 1217.5f, 1438.5f, 1658f },
        new[] { 114f, 335f, 555.5f, 776f, 997f, 1216.5f, 1437.5f, 1658f },
    };

    // 스코프 (row 0: 열림, 1: 발사 섬광, 2: 닫힘), 열 0~7
    public static Sprite Scope(int row, int col)
    {
        Sprite sheet = Sheet;
        if (sheet == null) return null;
        float h = sheet.texture.height;
        float cx = ScopeCenters[Mathf.Clamp(row, 0, 2)][Mathf.Clamp(col, 0, 7)];   // 칸마다 실제 그림 중심 (프레임이 흔들리지 않게)
        float cy = row == 0 ? 114f : row == 1 ? 333f : 551f;      // 위에서부터 픽셀
        return SpriteFx.Crop(sheet, new Rect(cx - 108f, h - (cy + 105f), 216f, 210f));
    }

    public static Sprite[] ScopeRow(int row, int from, int count)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = from; i < from + count && i < 8; i++) list.Add(Scope(row, i));
        return list.ToArray();
    }

    // 탄 아이콘 (true: 남은 탄 / false: 쓴 탄 회색)
    public static Sprite Bullet(bool full)
    {
        Sprite sheet = Sheet;
        if (sheet == null) return null;
        float h = sheet.texture.height;
        float cx = full ? 136f : 1640f;
        return SpriteFx.Crop(sheet, new Rect(cx - 50f, h - (709f + 28f), 100f, 56f));
    }

    // 작은 탄 6발이 든 탄창 (연사탄 아이콘)
    public static Sprite Magazine()
    {
        Sprite sheet = Sheet;
        if (sheet == null) return null;
        float h = sheet.texture.height;
        return SpriteFx.Crop(sheet, new Rect(136f - 105f, h - (823f + 40f), 210f, 80f));
    }
}

// 스코프 화면: 마우스 위치가 조준점, 스코프 밖은 어둡게, 오른쪽에 남은 탄
public class HeavyScopeUI : MonoBehaviour
{
    private enum Mode { Opening, Idle, Firing, Closing }

    private HeavyBowgunMainSettings s;
    private RectTransform canvasRect;
    private RectTransform scopeRoot;
    private Image scopeImage;
    private RawImage dark;
    private Image[] darkEdges;                                   // 구멍 그림 바깥 어둠 (위·아래·좌·우)
    private readonly List<Image> bullets = new List<Image>();
    private readonly List<Image> cracks = new List<Image>();
    private Sprite[] openFrames, fireFrames, closeFrames;
    private Sprite idleFrame;
    private Mode mode;
    private float modeTime;
    private float fade;                                         // 어두운 바깥 투명도 (0~1)
    private int shotsUsed;

    private static Texture2D holeTexture;
    private static Sprite crackSprite;
    private static Sprite shatterSprite;
    private static Sprite shardSprite;

    public static HeavyScopeUI Open(HeavyBowgunMainSettings settings, int shots)
    {
        Canvas canvas = RunUI.CreateCanvas("Heavy Scope", settings.scopeSortingOrder, false);   // 클릭은 막지 않음 (게임 화면 클릭으로 발사)
        HeavyScopeUI ui = canvas.gameObject.AddComponent<HeavyScopeUI>();
        ui.s = settings;
        ui.Build(canvas, shots);
        return ui;
    }

    private void Build(Canvas canvas, int shots)
    {
        canvasRect = (RectTransform)canvas.transform;
        openFrames = new List<Sprite>(HeavySheet.ScopeRow(0, 0, 8)).ToArray();
        idleFrame = HeavySheet.Scope(1, 0);
        List<Sprite> fire = new List<Sprite>(HeavySheet.ScopeRow(1, 1, 7));
        fire.AddRange(HeavySheet.ScopeRow(2, 0, 2));
        fireFrames = fire.ToArray();
        closeFrames = HeavySheet.ScopeRow(2, 2, 6);

        float diameter = ScopeDiameter();

        // 스코프 밖 어둠 (가운데가 뚫린 큰 그림을 조준점에 맞춰 움직임)
        dark = UltimateManager.CreateRect("Dark", canvasRect).gameObject.AddComponent<RawImage>();
        dark.texture = HoleTexture();
        dark.raycastTarget = false;
        dark.rectTransform.anchorMin = dark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dark.rectTransform.sizeDelta = Vector2.one * diameter * s.scopeHoleRatio / HoleFraction;
        dark.color = new Color(0f, 0f, 0f, 0f);
        darkEdges = new Image[4];
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < 4; i++)
        {
            Image edge = UltimateManager.CreateRect("Dark Edge", dark.rectTransform).gameObject.AddComponent<Image>();
            edge.raycastTarget = false;
            darkEdges[i] = edge;
            edge.color = dark.color;
            RectTransform er = edge.rectTransform;
            Vector2 d = dirs[i];
            er.anchorMin = er.anchorMax = new Vector2(0.5f, 0.5f) + d * 0.5f;          // 구멍 그림의 각 변
            er.pivot = new Vector2(0.5f, 0.5f) - d * 0.5f;                              // 바깥쪽으로 뻗음
            er.anchoredPosition = Vector2.zero;
            const float far = 20000f;                                                  // 화면 어디로 움직여도 덮는 크기
            er.sizeDelta = d.x != 0f ? new Vector2(far, far * 2f) : new Vector2(dark.rectTransform.sizeDelta.x, far);
        }

        scopeRoot = UltimateManager.CreateRect("Scope", canvasRect);
        scopeRoot.anchorMin = scopeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        scopeRoot.sizeDelta = Vector2.one * diameter;
        scopeImage = scopeRoot.gameObject.AddComponent<Image>();
        scopeImage.raycastTarget = false;
        scopeImage.preserveAspect = true;
        scopeImage.sprite = openFrames.Length > 0 ? openFrames[0] : idleFrame;

        // 오른쪽 남은 탄 (위에서부터, 쏠 때마다 아래부터 회색)
        Sprite full = HeavySheet.Bullet(true);
        for (int i = 0; i < shots; i++)
        {
            Image b = RunUI.Icon("Bullet " + i, canvasRect, full, new Vector2(1f, 0.5f),
                new Vector2(-s.scopeBulletRightMargin, (shots - 1) * 0.5f * s.scopeBulletSpacing - i * s.scopeBulletSpacing),
                s.scopeBulletSize);
            b.raycastTarget = false;
            b.preserveAspect = true;
            bullets.Add(b);
        }

        mode = Mode.Opening;
        modeTime = 0f;
        Update();
    }

    private float ScopeDiameter()
    {
        Rect r = canvasRect.rect;
        Camera cam = Camera.main;
        if (s.scopeFitRange && cam != null && cam.orthographic && cam.orthographicSize > 0f)
        {
            // 화면 세로 = 카메라 높이(월드) → 캔버스 단위/월드 단위, 렌즈(구멍) 지름이 피해 지름과 같게
            float perWorld = r.height / (cam.orthographicSize * 2f);
            return s.sniperRadius * 2f * s.scopeRangeFit * perWorld / Mathf.Max(0.1f, s.scopeHoleRatio);
        }
        return Mathf.Min(r.width, r.height) * s.scopeScreenFraction;
    }

    // 조준점 (화면 좌표)
    public void SetAim(Vector2 screenPosition)
    {
        if (canvasRect == null) return;
        screenPosition = new Vector2(Mathf.Clamp(screenPosition.x, 0f, Screen.width), Mathf.Clamp(screenPosition.y, 0f, Screen.height));   // 화면 밖으로 나가지 않게
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local);
        scopeRoot.anchoredPosition = local;
        dark.rectTransform.anchoredPosition = local;
    }

    // 한 발 발사: 섬광 + 유리 금 + 탄피가 튀어나가고 탄 아이콘이 회색으로
    public void Fire()
    {
        if (mode == Mode.Closing) return;
        mode = Mode.Firing;
        modeTime = 0f;

        Image crack = UltimateManager.CreateRect("Crack", scopeRoot).gameObject.AddComponent<Image>();
        crack.sprite = CrackSprite();
        crack.raycastTarget = false;
        crack.rectTransform.anchorMin = crack.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        crack.rectTransform.sizeDelta = scopeRoot.sizeDelta * Random.Range(0.55f, 0.7f);
        crack.rectTransform.anchoredPosition = Random.insideUnitCircle * scopeRoot.sizeDelta.x * 0.04f;
        crack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        crack.color = new Color(1f, 1f, 1f, 0.95f);
        cracks.Add(crack);

        int index = bullets.Count - 1 - shotsUsed;              // 아래부터 사용
        shotsUsed++;
        if (shotsUsed >= bullets.Count)
        {
            Shatter();                                          // 마지막 발: 유리가 완전히 깨져 뚫림
        }
        if (index >= 0 && index < bullets.Count)
        {
            Image b = bullets[index];
            Image shell = RunUI.Icon("Shell", canvasRect, b.sprite, b.rectTransform.anchorMin, b.rectTransform.anchoredPosition, b.rectTransform.sizeDelta);
            shell.raycastTarget = false;
            shell.preserveAspect = true;
            shell.gameObject.AddComponent<UiShell>();
            b.sprite = HeavySheet.Bullet(false);
        }
    }

    // 금간 유리를 지우고, 가운데가 뚫린 깨진 유리 + 사방으로 튀는 유리 파편
    private void Shatter()
    {
        foreach (Image c in cracks) if (c != null) Destroy(c.gameObject);
        cracks.Clear();

        Image broken = UltimateManager.CreateRect("Shattered Glass", scopeRoot).gameObject.AddComponent<Image>();
        broken.sprite = ShatterSprite();
        broken.raycastTarget = false;
        broken.rectTransform.anchorMin = broken.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        broken.rectTransform.sizeDelta = scopeRoot.sizeDelta * 0.9f;
        broken.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        broken.color = new Color(1f, 1f, 1f, 0.95f);
        cracks.Add(broken);                                     // 닫힐 때 함께 사라짐

        float size = scopeRoot.sizeDelta.x;
        for (int i = 0; i < 14; i++)
        {
            Image shard = UltimateManager.CreateRect("Glass Shard", scopeRoot).gameObject.AddComponent<Image>();
            shard.sprite = ShardSprite();
            shard.raycastTarget = false;
            shard.rectTransform.anchorMin = shard.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            shard.rectTransform.sizeDelta = Vector2.one * size * Random.Range(0.05f, 0.11f);
            shard.rectTransform.anchoredPosition = Random.insideUnitCircle * size * 0.12f;
            shard.color = new Color(0.85f, 0.95f, 1f, 0.9f);
            Vector2 dir = Random.insideUnitCircle.normalized;
            shard.gameObject.AddComponent<UiGlassShard>().Init(dir * size * Random.Range(0.9f, 1.6f), Random.Range(-720f, 720f));
        }
    }

    public void Close()
    {
        if (mode == Mode.Closing) return;
        mode = Mode.Closing;
        modeTime = 0f;
    }

    private Canvas ownCanvas;

    private void Update()
    {
        // 레벨업·아침 선택지·게임 결과 등으로 게임이 멈춘 동안에는 그 화면을 가리지 않게 숨김
        if (ownCanvas == null) ownCanvas = GetComponent<Canvas>();
        bool live = GameManager.instance == null || GameManager.instance.isLive;
        if (ownCanvas != null && ownCanvas.enabled != live) ownCanvas.enabled = live;
        if (!live) return;

        modeTime += Time.unscaledDeltaTime;
        switch (mode)
        {
            case Mode.Opening:
            {
                int i = Mathf.FloorToInt(modeTime * s.scopeOpenFps);
                fade = Mathf.Clamp01(modeTime / 0.25f);
                if (i >= openFrames.Length) { mode = Mode.Idle; scopeImage.sprite = idleFrame; }
                else scopeImage.sprite = openFrames[i];
                break;
            }
            case Mode.Firing:
            {
                int i = Mathf.FloorToInt(modeTime * s.scopeFireFps);
                if (i >= fireFrames.Length) { mode = Mode.Idle; scopeImage.sprite = idleFrame; }
                else scopeImage.sprite = fireFrames[i];
                break;
            }
            case Mode.Closing:
            {
                int i = Mathf.FloorToInt(modeTime * s.scopeCloseFps);
                float k = Mathf.Clamp01(modeTime * s.scopeCloseFps / Mathf.Max(1, closeFrames.Length));
                fade = 1f - k;                                // 스코프가 스르르 사라짐
                if (i < closeFrames.Length) scopeImage.sprite = closeFrames[i];
                scopeImage.color = new Color(1f, 1f, 1f, 1f - k * k);
                foreach (Image c in cracks) if (c != null) c.color = new Color(1f, 1f, 1f, 0.95f * (1f - k));
                foreach (Image b in bullets) if (b != null) b.color = new Color(1f, 1f, 1f, 1f - k);
                if (k >= 1f) { Destroy(gameObject); return; }
                break;
            }
        }

        // 금간 자국은 처음엔 또렷하다가 조금 옅어진 채로 남음
        foreach (Image c in cracks)
        {
            if (c == null || mode == Mode.Closing) continue;
            Color col = c.color;
            col.a = Mathf.MoveTowards(col.a, 0.6f, Time.unscaledDeltaTime * 0.6f);
            c.color = col;
        }
        // 열리는 중에 바로 쏘면(Firing으로 바뀜) 어두움이 중간에서 멈추던 문제: 닫히는 중이 아니면 끝까지 어두워짐
        if (mode == Mode.Firing || mode == Mode.Idle) fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / 0.25f);
        dark.color = new Color(0f, 0f, 0f, s.scopeDarkAlpha * fade);
        if (darkEdges != null) foreach (Image e in darkEdges) e.color = dark.color;
    }

    // 가운데가 뚫린 어둠 그림 (가장자리는 부드럽게)
    private const float HoleFraction = 0.16f;                   // 그림 폭 대비 구멍 지름

    private static Texture2D HoleTexture()
    {
        if (holeTexture != null) return holeTexture;
        const int size = 512;
        holeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        holeTexture.wrapMode = TextureWrapMode.Clamp;
        float r = size * HoleFraction * 0.5f;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01((d - r) / 3f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        holeTexture.SetPixels32(px);
        holeTexture.Apply();
        return holeTexture;
    }

    // 완전히 깨진 유리: 가운데는 들쭉날쭉하게 뚫리고, 가장자리에 남은 유리 조각과 금
    private static Sprite ShatterSprite()
    {
        if (shatterSprite != null) return shatterSprite;
        const int size = 256;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color32[] px = new Color32[size * size];
        System.Random rng = new System.Random(11);
        float[] jag = new float[48];
        for (int i = 0; i < jag.Length; i++) jag[i] = size * (0.2f + (float)rng.NextDouble() * 0.1f);   // 뚫린 구멍 반지름 (들쭉날쭉)
        float rim = size * 0.47f;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        int rays = 16;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - c;
                float r = d.magnitude;
                if (r > rim) continue;
                float ang = Mathf.Atan2(d.y, d.x);
                float u = (ang / (Mathf.PI * 2f) + 1f) * jag.Length;
                int i0 = Mathf.FloorToInt(u) % jag.Length;
                float hole = Mathf.Lerp(jag[i0], jag[(i0 + 1) % jag.Length], u - Mathf.Floor(u));
                if (r < hole) continue;                                    // 뚫린 곳
                byte a = 70;                                               // 남은 유리 (옅은 푸른빛)
                Color32 col = new Color32(210, 235, 255, a);
                if (r < hole + 3f) col = new Color32(255, 255, 255, 235);  // 깨진 테두리
                float rayPos = ang / (Mathf.PI * 2f) * rays;
                if (Mathf.Abs(rayPos - Mathf.Round(rayPos)) * (r * 0.4f) < 1.1f) col = new Color32(255, 255, 255, 200);   // 바깥으로 뻗는 금
                px[y * size + x] = col;
            }
        t.SetPixels32(px);
        t.Apply();
        shatterSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return shatterSprite;
    }

    // 유리 파편 (뾰족한 삼각형)
    private static Sprite ShardSprite()
    {
        if (shardSprite != null) return shardSprite;
        const int size = 32;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float half = (1f - y / (float)size) * size * 0.45f;            // 위로 갈수록 뾰족
                float dx = Mathf.Abs(x + 0.5f - size * 0.5f);
                if (dx > half) continue;
                px[y * size + x] = dx > half - 1.5f ? new Color32(255, 255, 255, 255) : new Color32(220, 240, 255, 150);
            }
        t.SetPixels32(px);
        t.Apply();
        shardSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return shardSprite;
    }

    // 유리 깨짐: 가운데에서 뻗는 갈라진 금 + 부서진 고리 (한 번 만들어 재사용)
    private static Sprite CrackSprite()
    {
        if (crackSprite != null) return crackSprite;
        const int size = 256;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color32[] px = new Color32[size * size];
        System.Random rng = new System.Random(7);
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        System.Action<int, int, byte> dot = (x, y, a) =>
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            int i = y * size + x;
            if (px[i].a < a) px[i] = new Color32(255, 255, 255, a);
        };
        // 가운데에서 뻗는 금 (지그재그)
        int rays = 11;
        for (int k = 0; k < rays; k++)
        {
            float ang = (k + (float)rng.NextDouble() * 0.6f) / rays * Mathf.PI * 2f;
            Vector2 p = c;
            float len = size * (0.32f + (float)rng.NextDouble() * 0.16f);
            int steps = 7;
            for (int st = 0; st < steps; st++)
            {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.5f;
                Vector2 q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / steps);
                for (float u = 0f; u <= 1f; u += 0.02f)
                {
                    Vector2 m = Vector2.Lerp(p, q, u);
                    byte a = (byte)(235 - st * 18);
                    dot((int)m.x, (int)m.y, a);
                    if (st < 3) dot((int)m.x + 1, (int)m.y, (byte)(a / 2));
                }
                p = q;
            }
        }
        // 부서진 고리 두 겹
        foreach (float rr in new[] { size * 0.12f, size * 0.24f })
        {
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.01f)
            {
                if (Mathf.Sin(a * 7f + rr) > 0.35f) continue;           // 군데군데 끊김
                float jitter = Mathf.Sin(a * 23f) * 2f;
                dot((int)(c.x + Mathf.Cos(a) * (rr + jitter)), (int)(c.y + Mathf.Sin(a) * (rr + jitter)), 170);
            }
        }
        t.SetPixels32(px);
        t.Apply();
        crackSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return crackSprite;
    }
}

// 유리 파편: 바깥으로 튀어나가며 돌고, 떨어지며 사라짐 (unscaled)
public class UiGlassShard : MonoBehaviour
{
    private Vector2 velocity;
    private float spin;
    private float life;
    private Image image;
    private RectTransform rect;

    public void Init(Vector2 velocity, float spin)
    {
        this.velocity = velocity;
        this.spin = spin;
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        life += dt;
        velocity += Vector2.down * 1400f * dt;                    // 떨어짐
        rect.anchoredPosition += velocity * dt;
        rect.localRotation *= Quaternion.Euler(0f, 0f, spin * dt);
        if (image != null)
        {
            Color c = image.color;
            c.a = Mathf.Clamp01(1f - (life - 0.35f) / 0.4f) * 0.9f;
            image.color = c;
        }
        if (life > 0.8f) Destroy(gameObject);
    }
}

// 탄피: 오른쪽 위로 튀어올랐다가 떨어지며 돌고 사라짐
public class UiShell : MonoBehaviour
{
    private Vector2 velocity;
    private float spin;
    private float time;
    private Image image;

    private void Start()
    {
        image = GetComponent<Image>();
        velocity = new Vector2(Random.Range(120f, 220f), Random.Range(260f, 360f));
        spin = Random.Range(-720f, -420f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;
        velocity.y -= 1100f * dt;
        RectTransform rt = (RectTransform)transform;
        rt.anchoredPosition += velocity * dt;
        rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spin * dt);
        if (image != null) image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (time - 0.5f) / 0.4f));
        if (time > 0.9f) Destroy(gameObject);
    }
}

// 탄 표시: 화면 왼쪽, 보관 슬롯 바로 위에 줄로 쌓음 (연사탄 / 중량탄은 항상, 칼날 누적은 공명 활성 때만)
// 줄마다 어두운 둥근 판 + 금색 테두리, 왼쪽 아이콘, 오른쪽 흰 숫자. 장착 중인 탄 줄은 또렷하게, 아닌 줄은 옅게
public class HeavyAmmoHud : MonoBehaviour
{
    private class Row
    {
        public RectTransform rect;
        public Image back;
        public Text text;
        public List<Image> icons = new List<Image>();
        public List<Image> cells = new List<Image>();           // 아래쪽 탄창 칸
        public List<Image> cellFills = new List<Image>();
    }

    private HeavyBowgunMainSettings s;
    private RectTransform canvasRect;
    private Row rapidRow, heavyRow, bladeRow;
    private static Sprite pillSprite, magazineSprite, whiteSprite;

    public static HeavyAmmoHud Create(HeavyBowgunMainSettings s)
    {
        Canvas canvas = RunUI.CreateCanvas("Heavy Ammo HUD", -3, false);
        HeavyAmmoHud hud = canvas.gameObject.AddComponent<HeavyAmmoHud>();
        hud.s = s;
        hud.canvasRect = (RectTransform)canvas.transform;

        Sprite bullet = HeavySheet.Bullet(true);
        // 연사탄: 세운 작은 탄 3개
        hud.rapidRow = hud.CreateRow("Rapid");
        hud.AddCells(hud.rapidRow, Mathf.Max(1, s.rapidSegments));
        for (int i = 0; i < 3; i++)
        {
            Image b = hud.AddIcon(hud.rapidRow, bullet, new Vector2(-s.ammoRowSize.x * 0.5f + 18f + i * 9f, 0f), new Vector2(26f, 14f));
            b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        // 중량탄: 누운 큰 탄 1개
        hud.heavyRow = hud.CreateRow("Heavy");
        hud.AddCells(hud.heavyRow, s.heavyMax);
        hud.AddIcon(hud.heavyRow, bullet, new Vector2(-s.ammoRowSize.x * 0.5f + 27f, 0f), new Vector2(40f, 22f));
        // 칼날 누적 (공명 「칼날 연사」)
        hud.bladeRow = hud.CreateRow("Blade");
        hud.AddIcon(hud.bladeRow, SpriteFx.WindBladeSprite, new Vector2(-s.ammoRowSize.x * 0.5f + 27f, 0f), new Vector2(34f, 28f));
        return hud;
    }

    private Row CreateRow(string name)
    {
        Row row = new Row();
        row.rect = UltimateManager.CreateRect(name, canvasRect);
        row.rect.anchorMin = row.rect.anchorMax = Vector2.zero;
        row.rect.sizeDelta = s.ammoRowSize;
        row.back = row.rect.gameObject.AddComponent<Image>();
        row.back.sprite = MagazineSprite();                    // 네모난 탄창 판 (금색 겉테 + 안쪽 선)
        row.back.type = Image.Type.Sliced;
        row.back.raycastTarget = false;
        row.text = RunUI.Label("Count", row.rect, "", s.ammoHudFontSize, Color.white, TextAnchor.MiddleRight,
            new Vector2(1f, 0.5f), new Vector2(-s.ammoRowSize.x * 0.32f, 0f), new Vector2(s.ammoRowSize.x * 0.58f, s.ammoRowSize.y), 0f);
        row.text.fontStyle = FontStyle.Bold;
        RunUI.ApplyPanelFont(row.text);                          // 능력치 판과 같은 글꼴 (나눔스퀘어라운드 EB)
        Outline line = row.text.gameObject.AddComponent<Outline>();
        line.effectColor = Color.black;
        line.effectDistance = new Vector2(1.5f, -1.5f);
        return row;
    }

    // 판 아래쪽에 탄창 칸을 나란히 (탄 수만큼 채워짐)
    private void AddCells(Row row, int count)
    {
        float inner = s.ammoRowSize.x - 16f;
        float gap = 2f;
        float w = (inner - gap * (count - 1)) / count;
        float h = 5f;
        float y = -s.ammoRowSize.y * 0.5f + 6f + h * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float x = -inner * 0.5f + w * 0.5f + i * (w + gap);
            Image slot = RunUI.Icon("Cell", row.rect, WhiteSprite(), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
            slot.raycastTarget = false;
            slot.preserveAspect = false;                       // 가로로 긴 칸 그대로
            Image fill = RunUI.Icon("Round", slot.rectTransform, WhiteSprite(), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(w, h));
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);  // 왼쪽부터 차오름
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;
            fill.preserveAspect = false;
            row.cells.Add(slot);
            row.cellFills.Add(fill);
        }
    }

    private Image AddIcon(Row row, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Image icon = RunUI.Icon("Icon", row.rect, sprite, new Vector2(0.5f, 0.5f), pos, size);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        row.icons.Add(icon);
        return icon;
    }

    public void Refresh(bool visible, bool heavyEquipped, int heavy, int rapid, bool switching, bool showBlade, int blade, int bladeNeed)
    {
        if (canvasRect.gameObject.activeSelf != visible) canvasRect.gameObject.SetActive(visible);
        if (!visible) return;

        // 보관 슬롯(빈 슬롯) 바로 위에 쌓음
        float baseY = s.ammoRowBottom, x = s.ammoRowLeft;
        GameObject slot = GameObject.Find(StatPanel.SlotButtonName);
        if (slot != null)
        {
            Vector3[] c = new Vector3[4];
            ((RectTransform)slot.transform).GetWorldCorners(c);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, c[1], null, out Vector2 topLeft);
            Vector2 offset = canvasRect.rect.size * 0.5f;
            baseY = topLeft.y + offset.y + s.ammoRowGap * 2f;
            x = topLeft.x + offset.x;
        }

        float step = s.ammoRowSize.y + s.ammoRowGap;
        int line = 0;
        if (showBlade)
        {
            Place(bladeRow, x, baseY + step * line++, blade + "/" + bladeNeed, true, false);
        }
        bladeRow.rect.gameObject.SetActive(showBlade);
        Place(heavyRow, x, baseY + step * line++, heavy.ToString(), heavyEquipped, switching);
        Place(rapidRow, x, baseY + step * line, rapid.ToString(), !heavyEquipped, switching);
        FillCells(heavyRow, heavy, heavyEquipped, s.AmmoColor(heavy / (float)Mathf.Max(1, s.heavyMax)));   // 가득 = 초록, 줄어들수록 빨강
        FillCells(rapidRow, rapid / (s.rapidMax / (float)Mathf.Max(1, s.rapidSegments)), !heavyEquipped, s.AmmoColor(rapid / (float)Mathf.Max(1, s.rapidMax)));
    }

    // 탄창 칸 채우기: 찬 칸은 탄 색, 차는 중인 칸은 일부만, 빈 칸은 어둡게
    private void FillCells(Row row, float filled, bool equipped, Color color)
    {
        float a = equipped ? 1f : s.ammoRowIdleAlpha;
        for (int i = 0; i < row.cells.Count; i++)
        {
            float k = Mathf.Clamp01(filled - i);
            row.cells[i].color = new Color(0.18f, 0.13f, 0.09f, 0.9f * a);
            RectTransform f = row.cellFills[i].rectTransform;
            f.sizeDelta = new Vector2(row.cells[i].rectTransform.sizeDelta.x * k, f.sizeDelta.y);
            row.cellFills[i].color = new Color(color.r, color.g, color.b, (k >= 0.999f ? 1f : 0.55f) * a);
        }
    }

    private void Place(Row row, float left, float bottom, string value, bool equipped, bool switching)
    {
        row.rect.anchoredPosition = new Vector2(left + s.ammoRowSize.x * 0.5f, bottom + s.ammoRowSize.y * 0.5f);
        row.text.text = value;
        float a = equipped ? (switching ? 0.6f : 1f) : s.ammoRowIdleAlpha;     // 장착 중인 탄은 또렷하게
        row.back.color = new Color(1f, 1f, 1f, a);
        row.text.color = new Color(1f, 1f, 1f, a);
        foreach (Image i in row.icons) i.color = new Color(1f, 1f, 1f, a);
    }

    // 네모난 탄창 판: 어두운 갈색 + 금색 겉테 + 안쪽 가는 선 (9칸 늘이기용)
    public static Sprite MagazineSprite()
    {
        if (magazineSprite != null) return magazineSprite;
        const int w = 32, h = 32, b = 8;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color32 fill = new Color32(46, 33, 23, 230), gold = new Color32(214, 170, 82, 255), dark = new Color32(28, 19, 12, 255),
                inner = new Color32(150, 112, 52, 200), clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));   // 가장자리까지 거리
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);          // 모서리 한 칸만 깎음
                px[y * w + x] = corner ? clear : d == 0 ? dark : d <= 2 ? gold : d == 3 ? dark : d == 5 ? inner : fill;
            }
        t.SetPixels32(px);
        t.Apply();
        magazineSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        return magazineSprite;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D t = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }

    // 어두운 갈색 둥근 판 + 금색 테두리 (9칸 늘이기용)
    public static Sprite PillSprite()
    {
        if (pillSprite != null) return pillSprite;
        const int w = 48, h = 32, r = 10;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color32 fill = new Color32(52, 38, 26, 225), border = new Color32(214, 170, 82, 255), clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * w + x] = d > r ? clear : d > r - 2f ? border : fill;
            }
        t.SetPixels32(px);
        t.Apply();
        pillSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 1, r + 1, r + 1, r + 1));
        return pillSprite;
    }
}

// 각성 날카로운 총알: 뒤로 금빛 궤적이 길게 남고, 바람결이 이어지며 반짝임이 흩날림
public class SlugTrailFx : MonoBehaviour
{
    private Rigidbody2D rigid;
    private Vector3 last;
    private float next;

    private void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
        last = transform.position;
        // 총알 뒤 빛나는 탄 궤적 (자식, 총알을 따라감)
        Sprite streak = SpriteFx.SlugStreakSprite;
        if (streak != null)
        {
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            SpriteRenderer r = new GameObject("Glow Trail").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            r.sprite = streak;
            r.color = new Color(1f, 0.82f, 0.35f, 1f);
            if (body != null)
            {
                r.sharedMaterial = body.sharedMaterial;
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder - 1;
            }
            float worldLen = 2.2f;
            float scale = worldLen / Mathf.Max(0.0001f, streak.bounds.size.x) / Mathf.Max(0.0001f, transform.lossyScale.x);
            r.transform.localScale = new Vector3(scale, scale * 1.6f, 1f);
            r.transform.localPosition = new Vector3(-worldLen * 0.42f / Mathf.Max(0.0001f, transform.lossyScale.x), 0f, 0f);   // 총알 뒤쪽 (그림은 오른쪽이 앞)
        }
    }

    private void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.035f;
        Vector3 now = transform.position;
        Vector2 dir = now - last;
        last = now;
        if (dir.sqrMagnitude < 0.0001f) return;
        SpriteFx.SlugWake(now, dir);
    }
}
