using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 화면 연출 (씬 파일 수정 없이 Title 씬이 열릴 때 자동 생성)
// - 배경 그림 좌우 바깥은 검은 레터박스
// - 초록 나뭇잎이 바람에 몇 장씩 흩날림 (leaf_effect 3번째 줄)
// - 배경의 태양을 중심으로 은은한 빛무리 + 천천히 도는 빛줄기
// 수치는 RunConfig의 "타이틀 화면" 항목
public class TitleAmbience : MonoBehaviour
{
    private const string TitleScene = "Title";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TitleScene || FindObjectOfType<TitleAmbience>() != null)
            return;
        new GameObject("Title Ambience").AddComponent<TitleAmbience>();
    }

    private class Leaf
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos, vel;
        public float frame, fps, swayPhase, swaySpeed, size;
    }

    private RunConfig rc;
    private SpriteRenderer background;
    private RectTransform glowRoot, sunGlow, sunCore, rays;
    private Image sunGlowImage, sunCoreImage, raysImage;
    private RectTransform leftBar, rightBar;
    private RectTransform leafRoot;
    private readonly List<Leaf> leaves = new List<Leaf>();
    private float nextLeaf;
    private Rect bgRect;                                         // 배경 그림의 화면 영역 (픽셀)

    private void Start()
    {
        rc = RunConfig.Instance;
        GameObject bg = GameObject.Find("Title BG");
        background = bg != null ? bg.GetComponent<SpriteRenderer>() : null;

        // 그리는 순서: 빛(-30) → 나뭇잎(-20) → 레터박스(-10) → 기존 타이틀 UI(0)
        glowRoot = NewCanvas("Title Sun Glow", -30);
        leafRoot = NewCanvas("Title Leaves", -20);
        RectTransform boxRoot = NewCanvas("Title Letterbox", -10);

        rays = NewImage("Sun Rays", glowRoot, RaysSprite(), out raysImage);
        sunGlow = NewImage("Sun Glow", glowRoot, SoftCircle(), out sunGlowImage);
        sunCore = NewImage("Sun Core", glowRoot, SoftCircle(), out sunCoreImage);
        leftBar = NewImage("Left Bar", boxRoot, null, out Image l);
        rightBar = NewImage("Right Bar", boxRoot, null, out Image r);
        l.color = r.color = rc.titleLetterboxColor;
        foreach (RectTransform bar in new[] { leftBar, rightBar })
        {
            bar.pivot = Vector2.zero;
        }
        UpdateLayout();
        for (int i = 0; i < rc.titleLeafCount; i++) SpawnLeaf(true);   // 처음부터 화면에 몇 장
    }

    private RectTransform NewCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        return (RectTransform)go.transform;
    }

    private static RectTransform NewImage(string name, RectTransform parent, Sprite sprite, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;            // 화면 왼쪽 아래 기준 픽셀 좌표
        return rt;
    }

    // 배경 그림의 화면 영역 계산 → 레터박스, 태양 위치
    private void UpdateLayout()
    {
        Camera cam = Camera.main;
        if (background != null && cam != null)
        {
            Bounds b = background.bounds;
            Vector3 min = cam.WorldToScreenPoint(b.min), max = cam.WorldToScreenPoint(b.max);
            bgRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        else
        {
            bgRect = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        float left = Mathf.Max(0f, bgRect.xMin), right = Mathf.Max(0f, Screen.width - bgRect.xMax);
        leftBar.anchoredPosition = Vector2.zero;
        leftBar.sizeDelta = new Vector2(left + 1f, Screen.height);
        rightBar.anchoredPosition = new Vector2(Screen.width - right - 1f, 0f);
        rightBar.sizeDelta = new Vector2(right + 1f, Screen.height);

        Vector2 sun = new Vector2(bgRect.xMin + bgRect.width * rc.titleSunPosition.x, bgRect.yMin + bgRect.height * rc.titleSunPosition.y);
        sunGlow.anchoredPosition = sunCore.anchoredPosition = rays.anchoredPosition = sun;
    }

    private void Update()
    {
        UpdateLayout();
        float h = bgRect.height;
        float t = Time.unscaledTime;

        // 태양 빛: 빛무리가 천천히 숨 쉬고, 빛줄기는 천천히 돌며 깜빡임
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * rc.titleSunPulseSpeed);
        Color c = rc.titleSunColor;
        sunGlow.sizeDelta = Vector2.one * h * rc.titleSunGlowSize * (1f + 0.05f * pulse);
        sunGlowImage.color = new Color(c.r, c.g, c.b, c.a * (0.8f + 0.2f * pulse));
        sunCore.sizeDelta = Vector2.one * h * rc.titleSunGlowSize * 0.35f;
        sunCoreImage.color = new Color(1f, 1f, 0.92f, c.a * 1.2f);
        rays.sizeDelta = Vector2.one * h * rc.titleSunRaySize * (1f + 0.03f * pulse);   // 돌지 않고 아래로 내리쬐며 살짝 일렁임
        rays.localRotation = Quaternion.identity;
        raysImage.color = new Color(c.r, c.g, c.b, rc.titleSunRayAlpha * (0.75f + 0.25f * Mathf.Sin(t * rc.titleSunPulseSpeed * 0.7f + 1f)));

        UpdateLeaves(h);
    }

    // ---------------- 나뭇잎 ----------------
    private void SpawnLeaf(bool anywhere)
    {
        Sprite[] frames = rc.titleLeafFrames;
        if (frames == null || frames.Length == 0)
            return;
        Leaf leaf = new Leaf();
        leaf.rect = NewImage("Leaf", leafRoot, frames[0], out leaf.image);
        float h = Mathf.Max(1f, bgRect.height);
        leaf.size = h * Random.Range(rc.titleLeafSize.x, rc.titleLeafSize.y);
        leaf.rect.sizeDelta = Vector2.one * leaf.size;
        // 바람: 왼쪽 위에서 오른쪽 아래로 (바람 방향은 설정)
        Vector2 wind = rc.titleLeafWind * h;
        leaf.vel = wind * Random.Range(0.7f, 1.3f);
        if (anywhere)
        {
            leaf.pos = new Vector2(Random.Range(bgRect.xMin, bgRect.xMax), Random.Range(bgRect.yMin, bgRect.yMax));
        }
        else if (Random.value < 0.5f)
        {
            leaf.pos = new Vector2(bgRect.xMin - leaf.size, Random.Range(bgRect.center.y, bgRect.yMax));   // 왼쪽 바깥에서
        }
        else
        {
            leaf.pos = new Vector2(Random.Range(bgRect.xMin, bgRect.center.x + bgRect.width * 0.2f), bgRect.yMax + leaf.size);   // 위쪽 바깥에서
        }
        leaf.fps = Random.Range(rc.titleLeafFps * 0.7f, rc.titleLeafFps * 1.3f);
        leaf.frame = Random.Range(0f, frames.Length);
        leaf.swayPhase = Random.Range(0f, 6.28f);
        leaf.swaySpeed = Random.Range(1.2f, 2.2f);
        leaf.image.color = new Color(1f, 1f, 1f, rc.titleLeafAlpha);
        leaf.image.preserveAspect = true;
        leaves.Add(leaf);
    }

    private void UpdateLeaves(float h)
    {
        float dt = Time.unscaledDeltaTime;
        Sprite[] frames = rc.titleLeafFrames;
        if (frames == null || frames.Length == 0)
            return;

        if (leaves.Count < rc.titleLeafCount && Time.unscaledTime >= nextLeaf)
        {
            nextLeaf = Time.unscaledTime + Random.Range(rc.titleLeafInterval * 0.6f, rc.titleLeafInterval * 1.4f);
            SpawnLeaf(false);
        }

        for (int i = leaves.Count - 1; i >= 0; i--)
        {
            Leaf leaf = leaves[i];
            leaf.swayPhase += leaf.swaySpeed * dt;
            Vector2 sway = new Vector2(0f, Mathf.Sin(leaf.swayPhase) * h * rc.titleLeafSway);   // 위아래로 살랑
            leaf.pos += (leaf.vel + sway) * dt;
            leaf.frame += leaf.fps * dt;
            leaf.image.sprite = frames[(int)leaf.frame % frames.Length];
            leaf.rect.anchoredPosition = leaf.pos;
            leaf.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(leaf.swayPhase * 0.7f) * 25f);

            bool gone = leaf.pos.x > bgRect.xMax + leaf.size * 2f || leaf.pos.y < bgRect.yMin - leaf.size * 2f;
            if (gone)
            {
                Destroy(leaf.rect.gameObject);
                leaves.RemoveAt(i);
            }
        }
    }

    // ---------------- 빛 그림 (코드로 생성) ----------------
    private static Sprite softCircle, raysSprite;

    // 가운데가 밝고 바깥으로 부드럽게 사라지는 원
    private static Sprite SoftCircle()
    {
        if (softCircle != null) return softCircle;
        const int size = 256;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;                                           // 가장자리로 갈수록 빠르게 옅어짐
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        softCircle = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return softCircle;
    }

    // 빛 기둥 (각도, 퍼짐, 밝기) — 일정한 방사형이 아니라 몇 줄기만 불규칙하게
    private static readonly float[] Shafts = { -38f, 4f, 0.7f, -51f, 7f, 0.5f, -63f, 3f, 0.85f, -74f, 6f, 0.45f, -88f, 4.5f, 0.6f, -103f, 3.5f, 0.35f };

    // 태양에서 비스듬히 내리쬐는 빛 기둥
    private static Sprite RaysSprite()
    {
        if (raysSprite != null) return raysSprite;
        const int size = 512;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float r = d.magnitude / (size * 0.5f);
                if (r > 1f) continue;
                // 오른쪽 아래로 비스듬히 내려오는 부드러운 빛 기둥 몇 개 (간격·굵기·밝기 제각각, 가장자리는 흐릿하게)
                float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float a = 0f;
                for (int k = 0; k < Shafts.Length; k += 3)
                {
                    float off = Mathf.DeltaAngle(deg, Shafts[k]) / Shafts[k + 1];
                    a = Mathf.Max(a, Shafts[k + 2] * Mathf.Exp(-off * off));   // 가우스 모양 (딱 잘린 쐐기 없음)
                }
                float cone = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(deg, -62f) / 30f, 2f)) * 0.25f;   // 빛 기둥 사이 옅은 빛
                a = Mathf.Max(a, cone);
                a *= Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f) * Mathf.Clamp01(r * 5f);   // 멀어질수록 서서히 사라짐
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        raysSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return raysSprite;
    }
}
