using UnityEngine;
using UnityEngine.UI;

// 낮 하늘 연출 (성능 부담 거의 없음: 화면 이미지 2장 + 구름 그림자 스프라이트 몇 장)
// - 보이지 않는 태양: 아침마다 빛이 오는 방향이 바뀌고, 그쪽 화면 가장자리가 따뜻하게 밝음 / 반대쪽은 살짝 어두움
// - 구름 그림자: 큰 반투명 그림자가 바람 방향으로 천천히 땅 위를 지나감 (지형·오브젝트를 하나하나 계산하지 않음)
// - 노을: 밤이 되기 전 sunsetTime 초 동안 화면이 점점 주황빛으로 물들고, 밤이 되며 자연스럽게 어둠으로 이어짐
// 수치는 RunConfig 의 "낮 하늘" 항목에서 조정
public class DaySky : MonoBehaviour
{
    private static DaySky instance;

    private RunConfig config;
    private RawImage sunGlow;
    private RawImage sunShade;
    private Image sunset;
    private Image dayDim;
    private RectTransform canvasRect;
    private SpriteRenderer[] clouds;
    private Vector2 sunDir = Vector2.up;
    private Vector2 wind = Vector2.right;
    private Texture2D glowTex;
    private Sprite cloudSprite;

    public static void Create()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = new GameObject("Day Sky").AddComponent<DaySky>();
    }

    // 아침마다 해 방향·바람 방향을 새로 정함
    public static void NewDay()
    {
        if (instance == null) return;
        float a = Random.Range(0f, 360f);
        instance.sunDir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
        float w = a + 180f + Random.Range(-60f, 60f);                 // 구름은 대체로 해 반대쪽으로 흘러감
        instance.wind = new Vector2(Mathf.Cos(w * Mathf.Deg2Rad), Mathf.Sin(w * Mathf.Deg2Rad));
    }

    private void Awake()
    {
        config = RunConfig.Instance;
        Canvas canvas = RunUI.CreateCanvas("Day Sky", -11, false);    // 밤 어둠(-10)보다 아래, 게임 화면 위
        canvas.transform.SetParent(transform, false);
        canvasRect = (RectTransform)canvas.transform;

        glowTex = RadialTexture(128);
        sunGlow = CreateRaw("Sun Glow", glowTex);
        sunShade = CreateRaw("Sun Shade", glowTex);

        RectTransform dimRect = RunUI.Rect("Day Dim", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 4000f);
        dayDim = dimRect.gameObject.AddComponent<Image>();
        dayDim.raycastTarget = false;
        dayDim.color = new Color(0f, 0f, 0f, 0f);

        RectTransform r = RunUI.Rect("Sunset", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 4000f);
        sunset = r.gameObject.AddComponent<Image>();
        sunset.raycastTarget = false;
        sunset.color = new Color(0f, 0f, 0f, 0f);

        cloudSprite = CloudSprite(128);
        clouds = new SpriteRenderer[Mathf.Max(0, config.cloudCount)];
        for (int i = 0; i < clouds.Length; i++)
        {
            SpriteRenderer sr = new GameObject("Cloud Shadow " + i).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = cloudSprite;
            sr.sortingLayerName = config.cloudSortingLayer;
            sr.sortingOrder = config.cloudSortingOrder;
            sr.color = new Color(0f, 0f, 0f, 0f);
            clouds[i] = sr;
        }
        NewDay();
        ScatterClouds(true);
    }

    private RawImage CreateRaw(string name, Texture2D tex)
    {
        RectTransform rt = RunUI.Rect(name, canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 100f);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        img.color = new Color(0f, 0f, 0f, 0f);
        return img;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (glowTex != null) Destroy(glowTex);
        if (cloudSprite != null) { Destroy(cloudSprite.texture); Destroy(cloudSprite); }
    }

    private void Update()
    {
        float day = DayNightCycle.Daylight;                 // 1 = 한낮, 0 = 밤
        float dusk = DayNightCycle.SunsetProgress;          // 0 → 1 노을이 짙어짐

        // 해 쪽 가장자리 따뜻한 빛, 반대쪽 옅은 그늘 (노을이 짙어지면 빛이 붉고 낮게)
        Vector2 half = canvasRect.rect.size * 0.5f;
        float glowSize = Mathf.Max(half.x, half.y) * 2.6f;
        Color warm = Color.Lerp(config.sunGlowColor, config.sunsetColor, dusk);
        Place(sunGlow, sunDir * half * 1.15f, glowSize, new Color(warm.r, warm.g, warm.b, config.sunGlowAlpha * day));   // sunGlowAlpha 0 이면 꺼짐
        Place(sunShade, -sunDir * half * 1.15f, glowSize, new Color(0.05f, 0.05f, 0.15f, config.sunShadeAlpha * day));

        // 햇빛: 정오(낮의 가운데)에 가장 밝고, 아침·저녁으로 갈수록 아주 살짝 어두움 (뿌옇게 밝히지 않고 어둡게만)
        float noon = Mathf.Sin(Mathf.Clamp01(DayNightCycle.DayProgress) * Mathf.PI);
        float dimA = config.dayDimMax * (1f - noon) * day;
        dayDim.color = new Color(0.05f, 0.04f, 0.1f, dimA);
        dayDim.enabled = dimA > 0.002f;

        // 노을: 낮이 끝나 갈수록 옅게 물들고, 밤 어둠이 내려오는 동안 서서히 사그라듦 (어둠도 노을빛에서 밤빛으로 바뀜)
        float sunsetAlpha = config.sunsetColor.a * Mathf.SmoothStep(0f, 1f, dusk);
        sunset.color = new Color(config.sunsetColor.r, config.sunsetColor.g, config.sunsetColor.b, sunsetAlpha);
        sunset.enabled = sunsetAlpha > 0.002f;

        // 구름 그림자: 바람 방향으로 흐르고 카메라에서 멀어지면 바람 반대편으로 다시 들어옴
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector2 c = cam.transform.position;
        float range = cam.orthographicSize * Mathf.Max(1f, cam.aspect) * 2.2f + config.cloudSize.x;
        float alpha = config.cloudAlpha * day * (1f - dusk * 0.5f);
        foreach (SpriteRenderer sr in clouds)
        {
            Vector2 p = (Vector2)sr.transform.position + wind * config.cloudSpeed * Time.deltaTime;
            if ((p - c).sqrMagnitude > range * range)
            {
                Vector2 side = new Vector2(-wind.y, wind.x);
                p = c - wind * range * 0.95f + side * Random.Range(-range, range) * 0.7f;
            }
            sr.transform.position = new Vector3(p.x, p.y, 0f);
            sr.color = new Color(0f, 0.02f, 0.08f, alpha);
            sr.enabled = alpha > 0.002f;
        }
        if (!started && cam != null)
        {
            started = true;
            ScatterClouds(true);
        }
    }

    private bool started;

    private void ScatterClouds(bool anywhere)
    {
        Camera cam = Camera.main;
        Vector2 c = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        float range = cam != null ? cam.orthographicSize * 2f : 12f;
        foreach (SpriteRenderer sr in clouds)
        {
            sr.transform.position = c + Random.insideUnitCircle * range;
            float s = Random.Range(0.75f, 1.25f);
            Vector2 size = config.cloudSize * s;
            sr.transform.localScale = new Vector3(size.x / cloudSprite.bounds.size.x, size.y / cloudSprite.bounds.size.y, 1f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
        }
    }

    private static void Place(RawImage img, Vector2 pos, float size, Color color)
    {
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = Vector2.one * size;
        img.color = color;
        img.enabled = color.a > 0.002f;
    }

    // 가운데가 진하고 바깥으로 부드럽게 사라지는 원
    private static Texture2D RadialTexture(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
        t.SetPixels(px);
        t.Apply(false, true);
        return t;
    }

    // 구름 그림자: 겹친 원 몇 개로 만든 부드러운 덩어리
    private static Sprite CloudSprite(int size)
    {
        Texture2D t = new Texture2D(size, size / 2, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        int w = size, h = size / 2;
        Vector3[] blobs = { new Vector3(0.3f, 0.5f, 0.28f), new Vector3(0.5f, 0.58f, 0.34f), new Vector3(0.7f, 0.48f, 0.27f), new Vector3(0.5f, 0.38f, 0.3f) };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float a = 0f;
                foreach (Vector3 b in blobs)
                {
                    float dx = (u - b.x) / b.z, dy = (v - b.y) / (b.z * 1.6f);
                    float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = Mathf.Max(a, k * k * (3f - 2f * k));
                }
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(px);
        t.Apply(false, true);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
