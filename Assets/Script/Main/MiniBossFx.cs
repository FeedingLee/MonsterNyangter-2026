using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 중간보스 등장 연출 (파티클)
// - 소환 지점: 어두운 연기가 소용돌이치며 모이고 불씨가 피어오름 → 등장 순간 흙먼지·불씨가 터짐
// - 등장 문구 뒤: 불씨 입자가 사방으로 흩날림 (UI)
// 수치·색은 MiniBossConfig 에서 조정
public static class MiniBossFx
{
    private static Sprite softDot;

    // 가장자리가 부드럽게 사라지는 흰 점 (연기·불씨 입자용)
    public static Sprite SoftDot
    {
        get
        {
            if (softDot == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                Color[] px = new Color[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                        float a = Mathf.Clamp01(1f - d);
                        px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
                tex.SetPixels(px);
                tex.Apply(false, true);
                softDot = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
                softDot.name = "Soft Dot";
            }
            return softDot;
        }
    }

    // 소환 지점: duration 동안 연기 소용돌이 + 피어오르는 불씨. 반환한 오브젝트는 알아서 사라짐
    public static GameObject SpawnPortal(Vector3 position, float radius, float duration)
    {
        MiniBossConfig c = MiniBossConfig.Instance;
        GameObject root = new GameObject("Mini Boss Spawn Fx");
        root.transform.position = position;

        // 연기: 원 둘레에서 생겨 안쪽으로 빨려 들며 돎
        ParticleSystem smoke = Create(root.transform, "Smoke", -1);
        var main = smoke.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = c.spawnSmokeColor;
        main.maxParticles = 300;
        var emission = smoke.emission;
        emission.rateOverTime = 40f;
        var shape = smoke.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.3f;
        var vel = smoke.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(2.5f);
        vel.radial = new ParticleSystem.MinMaxCurve(-radius * 0.9f);
        FadeInOut(smoke, 0.8f);
        var sizeOver = smoke.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        // 불씨: 바닥에서 위로 흔들리며 피어오름
        ParticleSystem embers = Create(root.transform, "Embers", 1);
        main = embers.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = c.spawnEmberColor;
        main.gravityModifier = -0.2f;
        emission = embers.emission;
        emission.rateOverTime = c.spawnEmberCount / Mathf.Max(0.1f, duration);
        shape = embers.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.8f;
        shape.rotation = Vector3.zero;
        var noise = embers.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 1.5f;
        FadeInOut(embers, 1f);

        smoke.Play();
        embers.Play();
        Object.Destroy(root, duration + 1.5f);
        return root;
    }

    // 등장 순간: 흙먼지가 원형으로 퍼지고 불씨가 튐
    public static void AppearBurst(Vector3 position, float radius)
    {
        MiniBossConfig c = MiniBossConfig.Instance;
        GameObject root = new GameObject("Mini Boss Appear Fx");
        root.transform.position = position;

        ParticleSystem dust = Create(root.transform, "Dust", -1);
        var main = dust.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 2f, radius * 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.4f, radius * 0.8f);
        main.startColor = new Color(0.55f, 0.45f, 0.35f, 0.7f);
        var emission = dust.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.3f;
        var limit = dust.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.15f;
        FadeInOut(dust, 0.1f);

        ParticleSystem sparks = Create(root.transform, "Sparks", 1);
        main = sparks.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 2.5f, radius * 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = c.spawnEmberColor;
        main.gravityModifier = 0.6f;
        emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, c.spawnEmberCount)) });
        shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.2f;
        FadeInOut(sparks, 0.05f);

        dust.Play();
        sparks.Play();
        Object.Destroy(root, 2f);
    }

    private static ParticleSystem Create(Transform parent, string name, int sortingOffset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(SoftDot);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        GameManager gm = GameManager.instance;
        SpriteRenderer reference = gm != null && gm.player != null ? gm.player.GetComponent<SpriteRenderer>() : null;
        if (reference != null)
        {
            r.sharedMaterial = reference.sharedMaterial;
        }
        r.sortingLayerID = CombatUtil.EnemySortingLayer;
        r.sortingOrder = 10 + sortingOffset;
        return ps;
    }

    // 나타날 때 서서히, 사라질 때 서서히
    private static void FadeInOut(ParticleSystem ps, float fadeIn)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(fadeIn * 0.3f, 0.02f, 0.4f)), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    // ---------------------------------------------------------------
    // UI 불씨 (등장 문구 뒤)
    // ---------------------------------------------------------------
    public static void UiEmbers(RectTransform parent, Vector2 area, float duration)
    {
        UiEmberBurst burst = new GameObject("Embers", typeof(RectTransform)).AddComponent<UiEmberBurst>();
        RectTransform rt = (RectTransform)burst.transform;
        rt.SetParent(parent, false);
        rt.SetAsFirstSibling();                                      // 글자·초상화 뒤
        rt.anchoredPosition = Vector2.zero;
        burst.Init(area, duration);
    }
}

// UI 위에서 도는 간단한 입자 (Screen Space Overlay 캔버스에는 ParticleSystem이 안 그려져서 Image로 직접 움직임)
public class UiEmberBurst : MonoBehaviour
{
    private class Ember
    {
        public Image image;
        public Vector2 velocity;
        public Vector2 baseVelocity;
        public float delay;
        public float life;
        public float age;
        public float size;
    }

    private readonly List<Ember> embers = new List<Ember>();
    private float duration;
    private float time;

    public void Init(Vector2 area, float duration)
    {
        this.duration = duration;
        MiniBossConfig c = MiniBossConfig.Instance;
        int count = c != null ? c.announceEmberCount : 30;
        Color color = c != null ? c.announceEmberColor : new Color(1f, 0.55f, 0.2f);
        for (int i = 0; i < count; i++)
        {
            Image img = new GameObject("Ember", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(transform, false);
            img.sprite = MiniBossFx.SoftDot;
            img.raycastTarget = false;
            Color tint = Color.Lerp(color, new Color(1f, 0.9f, 0.5f), Random.value * 0.5f);
            img.color = new Color(tint.r, tint.g, tint.b, 0f);

            // 띠 모양 영역 안 어딘가에서 생겨 바깥·위쪽으로 흩날림
            Vector2 start = new Vector2(Random.Range(-0.5f, 0.5f) * area.x, Random.Range(-0.5f, 0.5f) * area.y * 0.6f);
            img.rectTransform.anchoredPosition = start;
            Vector2 dir = new Vector2(start.x / Mathf.Max(1f, area.x * 0.5f), 0.6f + Random.value * 0.8f);
            Ember e = new Ember
            {
                image = img,
                velocity = dir.normalized * Random.Range(60f, 200f),
                delay = Random.value * duration * 0.6f,
                life = Random.Range(0.6f, 1.2f),
                size = Random.Range(18f, 42f),
            };
            e.baseVelocity = e.velocity;
            img.rectTransform.sizeDelta = Vector2.one * e.size;
            embers.Add(e);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;
        foreach (Ember e in embers)
        {
            if (time < e.delay)
                continue;
            e.age += dt;
            float k = e.age / e.life;
            if (k >= 1f)
            {
                // 문구가 떠 있는 동안 다시 생김
                if (time < duration * 0.75f)
                {
                    e.age = 0f;
                    e.velocity = e.baseVelocity;
                    e.image.rectTransform.anchoredPosition = new Vector2(Random.Range(-0.45f, 0.45f) * 600f, Random.Range(-40f, 40f));
                }
                else
                {
                    e.image.color = new Color(e.image.color.r, e.image.color.g, e.image.color.b, 0f);
                }
                continue;
            }
            RectTransform rt = e.image.rectTransform;
            rt.anchoredPosition += e.velocity * dt;
            e.velocity += new Vector2(Mathf.Sin((time + e.size) * 6f) * 40f, 30f) * dt;   // 흔들리며 위로
            float alpha = Mathf.Sin(k * Mathf.PI);
            Color col = e.image.color;
            e.image.color = new Color(col.r, col.g, col.b, alpha);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, k);
        }
    }
}
