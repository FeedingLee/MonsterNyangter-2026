using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI 위에서 이펙트 시트 프레임을 짧게 재생 (게임이 멈춰 있어도 실제 시간으로 재생)
// 레벨업 창, 리롤, 아침 선택지, 메인무기 해금 안내 등에 반짝임을 얹을 때 사용
public class UiFx : MonoBehaviour
{
    private Image image;
    private Sprite[] frames;
    private float fps;
    private float time;
    private float hold;
    private Color color;
    private Vector2 drift;
    private float life = -1f;

    // 한 장짜리 그림을 천천히 흘려보내며 나타났다 사라지게 (나비 등)
    public void Configure(Vector2 driftPerSecond, float lifeSeconds)
    {
        drift = driftPerSecond;
        life = lifeSeconds;
    }

    // 미리 정해 둔 연출 (sub_effect_2 / sub_effect_3)
    public enum Kind { Burst, Stars, Ring, Pillar, Orbs, Sparkle, Butterfly, Twinkle }

    // 작은 별이 커졌다가 다시 작아지며 사라짐 (sub_effect_1 반짝임 4장, 왕복)
    private static Sprite[] twinkle;
    private static Sprite[] Twinkle()
    {
        if (twinkle == null || twinkle.Length == 0 || twinkle[0] == null)
        {
            Sprite[] f = SpriteFx.TwinkleFrames;
            if (f == null || f.Length == 0) return f;
            List<Sprite> l = new List<Sprite>(f);
            for (int i = f.Length - 2; i >= 0; i--) l.Add(f[i]);
            twinkle = l.ToArray();
        }
        return twinkle;
    }

    public static UiFx Play(Kind kind, RectTransform parent, Vector2 position, float size, Color? tint = null, float fps = 14f, bool behind = false)
    {
        Sprite[] frames = kind == Kind.Butterfly ? new[] { SpriteFx.ButterflySprite } : kind == Kind.Twinkle ? Twinkle() : FramesOf(kind);
        if (frames == null || frames.Length == 0 || frames[0] == null || parent == null)
            return null;
        GameObject go = new GameObject("Ui Fx " + kind, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        // 판넬·카드에 가려지지 않게 맨 위 전용 캔버스에 그림 (위치·크기는 parent 기준으로 계산)
        // behind = true 면 parent 바로 뒤(같은 부모, 한 칸 앞 순서)에 그려 그림을 가리지 않음
        RectTransform top = behind && parent.parent is RectTransform holder ? holder : TopCanvas();
        rt.SetParent(top, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.position = parent.TransformPoint(position);
        float scale = parent.lossyScale.x / Mathf.Max(0.0001f, top.lossyScale.x);
        float aspect = frames[0].rect.height / Mathf.Max(1f, frames[0].rect.width);
        rt.sizeDelta = new Vector2(size, size * aspect) * scale;
        if (top == parent.parent) rt.SetSiblingIndex(parent.GetSiblingIndex());
        else rt.SetAsLastSibling();
        UiFx fx = go.AddComponent<UiFx>();
        fx.image = go.AddComponent<Image>();
        fx.image.raycastTarget = false;
        fx.image.sprite = frames[0];
        fx.frames = frames;
        fx.fps = fps;
        fx.hold = 0.05f;
        fx.color = tint ?? Color.white;
        fx.image.color = fx.color;
        return fx;
    }

    private static readonly Dictionary<Kind, Sprite[]> cache = new Dictionary<Kind, Sprite[]>();
    private static RectTransform topCanvas;

    private static RectTransform TopCanvas()
    {
        if (topCanvas == null)
        {
            Canvas c = RunUI.CreateCanvas("Ui Fx Top", 120, false);   // 레벨업·아침 선택지·팝업보다 위
            topCanvas = (RectTransform)c.transform;
        }
        return topCanvas;
    }

    private static Sprite[] FramesOf(Kind kind)
    {
        if (cache.TryGetValue(kind, out Sprite[] f) && f != null && f.Length > 0 && f[0] != null)
            return f;
        switch (kind)
        {
            case Kind.Burst: f = SpriteFx.Frames("S3", 2); break;     // 금빛 부채 빛
            case Kind.Stars: f = SpriteFx.Frames("S3", 0); break;     // 반짝임 무리
            case Kind.Ring: f = SpriteFx.Frames("S2", 1); break;      // 금빛 회전 고리
            case Kind.Pillar: f = SpriteFx.Frames("S2", 2); break;    // 금빛 솟구침
            case Kind.Orbs: f = SpriteFx.Frames("S3", 7); break;      // 빛 구슬
            default: f = SpriteFx.Frames("S2", 0); break;             // 큰 반짝임
        }
        cache[kind] = f;
        return f;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        if (life > 0f)
        {
            ((RectTransform)transform).anchoredPosition += drift * Time.unscaledDeltaTime;
            float k = time / life;
            float a = Mathf.Clamp01(Mathf.Min(k * 4f, (1f - k) * 3f));      // 서서히 나타났다 서서히 사라짐
            image.color = new Color(color.r, color.g, color.b, color.a * a);
            if (k >= 1f) Destroy(gameObject);
            return;
        }
        int index = Mathf.FloorToInt(time * fps);
        if (index >= frames.Length)
        {
            float k = (time - frames.Length / fps) / Mathf.Max(0.01f, hold);
            if (k >= 1f) { Destroy(gameObject); return; }
            image.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
            return;
        }
        image.sprite = frames[index];
    }
}

// 뒤쪽 광채: 커지며 확 피어났다가 조금 어두워진 채 은은하게 숨 쉼 (실제 시간)
public class UiGlowBloom : MonoBehaviour
{
    public float inTime = 0.25f;
    public float restAlpha = 0.75f;
    public float pulse = 0.15f;
    private Image image;
    private float time;

    public void Bloom()
    {
        if (image == null) image = GetComponent<Image>();
        time = 0f;
        gameObject.SetActive(true);
        Update();
    }

    private void Update()
    {
        if (image == null) image = GetComponent<Image>();
        time += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(time / Mathf.Max(0.01f, inTime));
        float settle = Mathf.Clamp01((time - inTime) / 0.5f);           // 피어난 뒤 0.5초 동안 남는 밝기로
        float a = time < inTime ? k : Mathf.Lerp(1f, restAlpha, settle);
        if (settle >= 1f) a += Mathf.Sin((time - inTime - 0.5f) * 2.4f) * pulse * 0.5f;
        float s = time < inTime ? Mathf.Lerp(0.6f, 1.08f, 1f - (1f - k) * (1f - k)) : Mathf.Lerp(1.08f, 1f, settle);
        transform.localScale = new Vector3(s, s, 1f);
        Color c = image.color;
        image.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }
}

// UI 그림이 톡 커졌다가 원래 크기로 돌아옴 (실제 시간)
public class UiPop : MonoBehaviour
{
    private float peak = 1.2f;
    private float duration = 0.3f;
    private float time;

    public static void Play(RectTransform target, float peak, float duration)
    {
        if (target == null) return;
        UiPop p = target.GetComponent<UiPop>();
        if (p == null) p = target.gameObject.AddComponent<UiPop>();
        p.peak = peak;
        p.duration = Mathf.Max(0.05f, duration);
        p.time = 0f;
        p.enabled = true;
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(time / duration);
        float s = Mathf.Lerp(peak, 1f, 1f - (1f - k) * (1f - k));   // 빠르게 커졌다가 부드럽게 돌아옴
        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(Mathf.Sign(scale.x) * s, s, 1f);
        if (k >= 1f) enabled = false;
    }
}

// 카드 묶음 테두리 둘레에서 작은 별이 하나씩 은은하게 반짝임 (카드 안 글자 위에는 나오지 않음)
public class UiTwinkleLoop : MonoBehaviour
{
    public Vector2 halfSize = new Vector2(350f, 245f);   // 카드 묶음 가장자리 (중심 기준)
    public Vector2 center = new Vector2(0f, -30f);
    public float interval = 0.35f;
    private float next;

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + interval * Random.Range(0.6f, 1.4f);
        // 테두리 위 임의의 점 (네 변 중 하나)
        Vector2 p;
        if (Random.value < 0.6f) p = new Vector2(Random.Range(-1f, 1f) * halfSize.x, (Random.value < 0.5f ? -1f : 1f) * halfSize.y);
        else p = new Vector2((Random.value < 0.5f ? -1f : 1f) * halfSize.x, Random.Range(-1f, 1f) * halfSize.y);
        p += center + Random.insideUnitCircle * 10f;
        UiFx.Play(UiFx.Kind.Twinkle, (RectTransform)transform, p, Random.Range(26f, 44f), new Color(1f, 0.95f, 0.75f, 0.9f), 12f);
    }
}

// 화면 가장자리에서 나비가 이따금 날아올라 사라짐 (글자를 가리지 않게 판넬 바깥쪽에서만)
public class UiButterflyLoop : MonoBehaviour
{
    public float interval = 1.4f;
    public float innerHalfWidth = 330f;      // 이 안쪽(카드·글자)에는 나오지 않음
    private float next;

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + interval * Random.Range(0.7f, 1.3f);
        RectTransform rt = (RectTransform)transform;
        float side = Random.value < 0.5f ? -1f : 1f;
        float x = side * Random.Range(innerHalfWidth, innerHalfWidth + 40f);
        float y = Random.Range(-420f, 300f);
        UiFx fx = UiFx.Play(UiFx.Kind.Butterfly, rt, new Vector2(x, y), Random.Range(110f, 150f), new Color(1f, 1f, 1f, 0.75f), 1f);
        if (fx != null) fx.Configure(new Vector2(-side * Random.Range(5f, 15f), Random.Range(25f, 45f)), Random.Range(2.5f, 3.5f));
    }
}

// 위치는 그대로 두고 투명도만 은은하게 오르내림 (범위 예고)
public class FxPulse : MonoBehaviour
{
    public float baseA = 0.85f;                          // 기준 투명도 (처음 나타날 때 0이던 값을 잡지 않게 고정)
    private SpriteRenderer sr;

    private void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Color c = sr.color;
        c.a = baseA * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
        sr.color = c;
    }
}

// UI 이미지 프레임 반복 재생 (실제 시간, 게임이 멈춰도 재생)
public class UiSpriteLoop : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 9f;
    public float offset;
    private Image image;

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        if (image == null) image = GetComponent<Image>();
        int i = Mathf.FloorToInt((Time.unscaledTime + offset) * fps) % frames.Length;
        if (frames[i] != null) image.sprite = frames[i];
    }
}

// 만렙 칸 등에 붙는 반복 반짝임 (실제 시간, 붙은 UI가 꺼지면 함께 멈춤)
public class UiFxLoop : MonoBehaviour
{
    private RectTransform parent;
    private Vector2 center;
    private float size;
    private float next;

    public static void Attach(RectTransform parent, Vector2 center, float size)
    {
        if (parent == null || parent.GetComponent<UiFxLoop>() != null) return;
        UiFxLoop l = parent.gameObject.AddComponent<UiFxLoop>();
        l.parent = parent;
        l.center = center;
        l.size = size;
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + Random.Range(0.6f, 1.1f);
        Vector2 p = center + Random.insideUnitCircle * size * 0.45f;
        UiFx.Play(Random.value < 0.6f ? UiFx.Kind.Sparkle : UiFx.Kind.Stars, parent, p, size * Random.Range(0.35f, 0.55f), new Color(1f, 0.95f, 0.7f, 0.9f), 12f);
    }
}
