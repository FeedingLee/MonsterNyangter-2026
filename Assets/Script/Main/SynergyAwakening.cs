using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 시너지 각성 효과 공용 도구
// - 보스에게 주는 1회 피해는 BossPattern.TakeDamage(상한 적용)로만 넣고, 수치도 BossHitCap 이하로 자름
// - 화면(메인 카메라) 안 판정, 직선 판정, 화면 경계 반사 투사체, 화면 가르기 흰 선, 잔상
public static class SynergyAwakening
{
    public const float BossHitCap = 500f;                    // 보스·중간보스 1회 피해 상한 (보스 MaxDamage와 같음, 160 → 500으로 상향)

    // 메인 카메라가 보는 월드 영역
    public static Rect ScreenRect()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return new Rect(-10f, -10f, 20f, 20f);

        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - width * 0.5f, c.y - height * 0.5f, width, height);
    }

    public static bool OnScreen(Vector3 position, float margin = 0f)
    {
        Rect r = ScreenRect();
        return position.x >= r.xMin - margin && position.x <= r.xMax + margin &&
               position.y >= r.yMin - margin && position.y <= r.yMax + margin;
    }

    // 보스 피해 (1회 상한 160, 기존 상한 코드도 그대로 적용됨)
    public static void DamageBoss(BossPattern boss, float damage)
    {
        if (boss != null && boss.isActiveAndEnabled)
        {
            boss.TakeDamage(Mathf.Min(damage, BossHitCap));     // 피해 증가는 호출한 쪽(각성 피해)에서 이미 적용
        }
    }

    // 화면 안 중간보스에게 피해 (천지 가르기, 1회 160 이하)
    public static void DamageMiniBossesOnScreen(float damage)
    {
        foreach (MiniBoss b in new List<MiniBoss>(MiniBoss.Alive))
        {
            if (b != null && b.IsLive && OnScreen(b.transform.position, 0.5f))
            {
                b.TakeDamage(Mathf.Min(GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.MiniBoss), BossHitCap));   // 피해 증가만
            }
        }
    }

    // 화면 안 잡몹 전부 처치
    public static void KillMobsOnScreen()
    {
        foreach (Enemy enemy in Object.FindObjectsOfType<Enemy>())
        {
            if (OnScreen(enemy.transform.position, 0.3f))
            {
                enemy.TakeDamage(999999f);
            }
        }
    }

    // a → b 직선 위(폭 width) 적에게 피해 1회
    public static void HitLine(Vector3 a, Vector3 b, float width, float damage)
    {
        Vector2 diff = b - a;
        float length = Mathf.Max(0.1f, diff.magnitude);
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        Collider2D[] hits = Physics2D.OverlapBoxAll((a + b) * 0.5f, new Vector2(length + width, width), angle);

        HashSet<Object> done = new HashSet<Object>();
        foreach (Collider2D col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && done.Add(enemy))
            {
                enemy.TakeDamage(GimmickCards.SynergyDamage(damage, enemy.HuntKind));   // 피해 증가만
                continue;
            }
            ColonyNest nest = col.GetComponentInParent<ColonyNest>();
            if (nest != null)
            {
                if (done.Add(nest) && nest.IsLive) nest.TakeDamage(GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.Mob));   // 군락 둥지
                continue;
            }
            MiniBoss miniBoss = col.GetComponentInParent<MiniBoss>();
            if (miniBoss != null)
            {
                if (done.Add(miniBoss)) miniBoss.TakeDamage(Mathf.Min(GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.MiniBoss), BossHitCap));
                continue;
            }
            BossPattern boss = col.GetComponentInParent<BossPattern>();
            if (boss != null && done.Add(boss))
            {
                DamageBoss(boss, GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.Boss));
            }
        }
    }

    public static void PlaySound(AudioClip clip, AudioManager.Sfx fallback)
    {
        RunUI.PlayOr(clip, fallback);
    }

    // 화면을 가르는 흰 선 2줄 (unscaled time으로 번쩍였다 사라짐)
    public static void CleaveLines(float duration)
    {
        Canvas canvas = RunUI.CreateCanvas("Cleave Lines", 45, false);
        CleaveLinesFx fx = canvas.gameObject.AddComponent<CleaveLinesFx>();
        fx.Init(canvas.transform, duration);
    }

    // 화면 전체 흰 번쩍임 (unscaled)
    public static void ScreenFlash(float time, float alpha = 0.75f)
    {
        if (time <= 0f)
            return;
        Canvas canvas = RunUI.CreateCanvas("Screen Flash", 46, false);
        Image image = RunUI.Panel("Flash", canvas.transform, new Color(1f, 1f, 1f, alpha), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
        image.sprite = null;
        image.raycastTarget = false;
        canvas.gameObject.AddComponent<UiFade>().Init(image, time);
    }

    // 화면이 살짝 어두워졌다가 다시 밝아짐 (unscaled)
    public static void ScreenDim(float alpha, float darkenTime, float brightenTime)
    {
        Canvas canvas = RunUI.CreateCanvas("Screen Dim", 45, false);   // 화면 번쩍임(46) 아래
        Image image = RunUI.Panel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
        image.sprite = null;
        image.raycastTarget = false;
        canvas.gameObject.AddComponent<ScreenDimFx>().Init(image, alpha, darkenTime, brightenTime);
    }

    // 화면 안 번개 줄기 (지그재그 흰 선이 번쩍였다 사라짐)
    public static void Lightning(int count, float time)
    {
        if (count <= 0)
            return;
        Canvas canvas = RunUI.CreateCanvas("Lightning", 46, false);
        RectTransform root = (RectTransform)canvas.transform;
        UiFade fade = canvas.gameObject.AddComponent<UiFade>();
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-300f, 300f);
            Vector2 point = new Vector2(x, 700f);
            float endY = Random.Range(-500f, 0f);
            while (point.y > endY)
            {
                Vector2 next = point + new Vector2(Random.Range(-70f, 70f), -Random.Range(70f, 130f));
                Vector2 diff = next - point;
                Image seg = RunUI.Panel("Bolt", root, new Color(0.9f, 0.95f, 1f, 1f), new Vector2(0.5f, 0.5f), (point + next) * 0.5f, new Vector2(diff.magnitude + 6f, 9f));
                seg.sprite = null;
                seg.raycastTarget = false;
                seg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
                fade.Add(seg);
                point = next;
            }
        }
        fade.Init(null, Mathf.Max(0.12f, time * 2.5f));
    }

    // 이어진 빛줄기 (LineRenderer). 시간이 지나면 투명해지며 사라짐
    public static LightLine Line(Vector3 a, Vector3 b, float width, Color color, float life)
    {
        GameObject go = new GameObject("Light Line");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.useWorldSpace = true;
        line.numCapVertices = 6;
        line.material = LineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;

        SpriteRenderer playerSprite = GameManager.instance != null ? GameManager.instance.player.GetComponent<SpriteRenderer>() : null;
        int weaponLayer = SortingLayer.NameToID("Weapon");
        line.sortingLayerID = SortingLayer.IsValid(weaponLayer) ? weaponLayer : (playerSprite != null ? playerSprite.sortingLayerID : 0);
        line.sortingOrder = 20;

        LightLine light = go.AddComponent<LightLine>();
        light.Init(line, width, color, life);
        return light;
    }

    private static Material lineMaterial;
    public static Material LineMaterial
    {
        get
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                lineMaterial = new Material(shader);
            }
            return lineMaterial;
        }
    }

    // 충격파 링 (플레이어 중심으로 퍼지는 흰 고리)
    public static void ShockRing(Vector3 center, float maxDiameter, float time)
    {
        GameObject go = new GameObject("Shock Ring");
        go.transform.position = center;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = RingSprite;
        SpriteRenderer playerSprite = GameManager.instance.player.GetComponent<SpriteRenderer>();
        renderer.sharedMaterial = playerSprite.sharedMaterial;
        renderer.sortingLayerID = playerSprite.sortingLayerID;
        renderer.sortingOrder = playerSprite.sortingOrder - 1;
        go.AddComponent<RingExpand>().Init(renderer, maxDiameter, time);
    }

    private static Sprite ringSprite;
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                const int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float r = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.12f);   // 바깥쪽 고리
                        a = Mathf.Max(a, Mathf.Clamp01(1f - r) * 0.15f);               // 안쪽 옅은 빛
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return ringSprite;
        }
    }

    // 잔상 (캐릭터 모습을 반투명하게 남겼다 사라짐)
    public static void Afterimage(SpriteRenderer source, float life = 0.3f)
    {
        if (source == null || source.sprite == null)
            return;

        GameObject go = new GameObject("Afterimage");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.flipX = source.flipX;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 2;
        renderer.color = new Color(0.75f, 0.9f, 1f, 0.55f);
        go.AddComponent<FadeAndDestroy>().life = life;
    }
}

// 화면 가르기 흰 선 연출
public class CleaveLinesFx : MonoBehaviour
{
    private readonly List<Image> lines = new List<Image>();
    private float duration;
    private float time;

    public void Init(Transform parent, float duration)
    {
        this.duration = Mathf.Max(0.1f, duration);
        float[] angles = { 28f, -22f };
        float[] offsets = { 90f, -110f };
        for (int i = 0; i < 2; i++)
        {
            Image line = RunUI.Panel("Line " + i, parent, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, offsets[i]), new Vector2(3200f, 16f));
            line.sprite = null;
            line.raycastTarget = false;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            lines.Add(line);
        }
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(time / duration);
        for (int i = 0; i < lines.Count; i++)
        {
            // 선이 0.1초 차이로 번쩍 그어지고 굵어졌다 가늘어지며 사라짐
            float local = Mathf.Clamp01((time - i * 0.08f) / (duration * 0.6f));
            lines[i].rectTransform.localScale = new Vector3(Mathf.Lerp(0f, 1f, local * 3f), Mathf.Lerp(1.6f, 0.2f, local), 1f);
            lines[i].color = new Color(1f, 1f, 1f, local <= 0f ? 0f : 1f - Mathf.Clamp01((time - duration * 0.5f) / (duration * 0.6f)));
        }
        if (k >= 1f && time > duration * 1.2f)
        {
            Destroy(gameObject);
        }
    }
}

// 일정 시간 동안 투명해지며 사라짐 (잔상)
public class FadeAndDestroy : MonoBehaviour
{
    public float life = 0.3f;
    private SpriteRenderer target;
    private float startAlpha;
    private float time;

    private void Start()
    {
        target = GetComponent<SpriteRenderer>();
        startAlpha = target != null ? target.color.a : 1f;
    }

    private void Update()
    {
        time += Time.deltaTime;
        if (target != null)
        {
            Color c = target.color;
            target.color = new Color(c.r, c.g, c.b, startAlpha * (1f - Mathf.Clamp01(time / life)));
        }
        if (time >= life)
        {
            Destroy(gameObject);
        }
    }
}

// 각성 투사체 (거대 화살, 튕기는 검기, 칼날 폭풍)
// - 대상마다 1회만 피해 (보스 포함). 보스 다단히트가 지정되면 보스에게 그 횟수만큼 나눠서 피해
// - 화면 경계에 닿으면 남은 반사 횟수만큼 튕기고, 다 쓰면 화면 밖으로 나갈 때 사라짐
public class SynergyProjectile : MonoBehaviour
{
    public Vector2 velocity;
    public float damage;
    public int bounces;                       // 화면 경계 반사 횟수
    public int bossHits = 1;                  // 보스에게 들어가는 타수
    public float bossHitInterval = 0.08f;
    public float spriteAngle;                 // 그림이 오른쪽을 볼 때 0, 위를 볼 때 -90
    public int maxHits;                       // 0이면 제한 없음, 아니면 이 수만큼 맞히면 사라짐 (보스 포함)
    public bool homing;                       // 가장 가까운 몬스터에게 유도
    public float turnSpeed = 540f;
    public bool sparkTrail;                   // 빛 입자 꼬리
    public bool bossHitSparks;                // 보스 다단히트마다 타격 불꽃
    private int hitCount;
    private float nextTrail;

    private Rigidbody2D rigid;
    private readonly HashSet<Object> hitTargets = new HashSet<Object>();
    private float life;

    public static SynergyProjectile Spawn(string name, Sprite sprite, Vector3 position, Vector2 velocity, float worldLength,
        float spriteAngle, float damage, int bounces, int layer, SpriteRenderer sortingSource)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (sortingSource != null)
        {
            renderer.sharedMaterial = sortingSource.sharedMaterial;
            renderer.sortingLayerID = sortingSource.sortingLayerID;
            renderer.sortingOrder = sortingSource.sortingOrder + 2;
        }
        float spriteLength = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        go.transform.localScale = Vector3.one * (worldLength / Mathf.Max(0.01f, spriteLength));

        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.useFullKinematicContacts = true;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        if (sprite != null)
        {
            col.size = sprite.bounds.size;
            col.offset = sprite.bounds.center;
        }

        SynergyProjectile p = go.AddComponent<SynergyProjectile>();
        p.velocity = velocity;
        p.damage = damage;
        p.bounces = bounces;
        p.spriteAngle = spriteAngle;
        p.rigid = rigid;
        p.FaceVelocity();
        return p;
    }

    private void FaceVelocity()
    {
        if (velocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + spriteAngle);
        }
    }

    // 아직 맞히지 않은 가장 가까운 살아 있는 몬스터 (없으면 보스)
    private Transform FindTarget()
    {
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            Collider2D col = enemy.GetComponent<Collider2D>();
            if (col == null || !col.enabled || hitTargets.Contains(enemy))
                continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy.transform;
            }
        }
        if (best == null && !bossHitDone && BossPattern.Current != null && BossPattern.Current.isActiveAndEnabled)
        {
            best = BossPattern.Current.transform;
        }
        return best;
    }

    private void Update()
    {
        if (sparkTrail && Time.time >= nextTrail)
        {
            nextTrail = Time.time + 0.03f;
            SpriteFx.TrailSpark(transform.position - (Vector3)(velocity.normalized * 0.8f));
        }
    }

    private void FixedUpdate()
    {
        life += Time.fixedDeltaTime;
        if (homing && life > 0.12f)                          // 처음 잠깐 사방으로 퍼진 뒤 유도
        {
            Transform target = FindTarget();
            if (target != null)
            {
                Vector2 want = ((Vector2)(target.position - transform.position)).normalized;
                float speed = velocity.magnitude;
                Vector2 dir = Vector3.RotateTowards(velocity.normalized, want, turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                velocity = dir * speed;
                FaceVelocity();
            }
        }
        Vector2 next = rigid.position + velocity * Time.fixedDeltaTime;

        Rect screen = SynergyAwakening.ScreenRect();
        bool outX = (next.x < screen.xMin && velocity.x < 0f) || (next.x > screen.xMax && velocity.x > 0f);
        bool outY = (next.y < screen.yMin && velocity.y < 0f) || (next.y > screen.yMax && velocity.y > 0f);
        if (outX || outY)
        {
            if (bounces > 0)
            {
                bounces--;
                if (outX) velocity.x = -velocity.x;
                if (outY) velocity.y = -velocity.y;
                FaceVelocity();
                next = rigid.position + velocity * Time.fixedDeltaTime;
            }
            else if (!SynergyAwakening.OnScreen(next, 2f))
            {
                Destroy(gameObject);
                return;
            }
        }

        rigid.MovePosition(next);
        if (life > 10f)
        {
            Destroy(gameObject);
        }
    }

    private bool bossHitDone;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            if (hitTargets.Add(enemy))
            {
                enemy.TakeDamage(GimmickCards.SynergyDamage(damage, enemy.HuntKind));   // 피해 증가만
                CountHit();
            }
            return;
        }
        MiniBoss miniBoss = other.GetComponent<MiniBoss>();
        if (miniBoss != null)
        {
            if (hitTargets.Add(miniBoss))
            {
                miniBoss.TakeDamage(Mathf.Min(GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.MiniBoss), SynergyAwakening.BossHitCap));
                CountHit();
            }
            return;
        }
        ColonyNest nest = other.GetComponent<ColonyNest>();     // 군락 둥지
        if (nest != null)
        {
            if (nest.IsLive && hitTargets.Add(nest))
            {
                nest.TakeDamage(GimmickCards.SynergyDamage(damage, GimmickCards.TargetKind.Mob));
                CountHit();
            }
            return;
        }

        BossPattern boss = other.GetComponentInParent<BossPattern>();
        if (boss != null && !bossHitDone)                    // 보스는 투사체 1개당 1번만 (다단히트는 그 안에서)
        {
            bossHitDone = true;
            if (bossHits <= 1)
            {
                SynergyAwakening.DamageBoss(boss, damage);
            }
            else
            {
                boss.StartCoroutine(MultiHit(boss, damage, bossHits, bossHitInterval, bossHitSparks));
            }
            CountHit();
        }
    }

    private void CountHit()
    {
        hitCount++;
        if (maxHits > 0 && hitCount >= maxHits)
        {
            Destroy(gameObject);
        }
    }

    private static IEnumerator MultiHit(BossPattern boss, float damage, int count, float interval, bool sparks)
    {
        for (int i = 0; i < count; i++)
        {
            SynergyAwakening.DamageBoss(boss, damage);
            if (sparks && boss != null)
            {
                SpriteFx.Explosion(boss.transform.position + (Vector3)(Random.insideUnitCircle * 1.5f), 2.2f);   // 타격 불꽃
            }
            yield return new WaitForSeconds(interval);
        }
    }
}

// UI 이미지들을 unscaled time으로 서서히 투명하게 하고 캔버스째 제거 (화면 번쩍임, 번개)
public class UiFade : MonoBehaviour
{
    private readonly List<Image> images = new List<Image>();
    private readonly List<float> alphas = new List<float>();
    private float life = 0.1f;
    private float time;

    public void Add(Image image)
    {
        images.Add(image);
        alphas.Add(image.color.a);
    }

    public void Init(Image image, float life)
    {
        if (image != null)
        {
            Add(image);
        }
        this.life = Mathf.Max(0.02f, life);
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(time / life);
        for (int i = 0; i < images.Count; i++)
        {
            Color c = images[i].color;
            images[i].color = new Color(c.r, c.g, c.b, alphas[i] * (1f - k * k));
        }
        if (k >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

// 이어진 빛줄기: 처음엔 가늘게, Explode()를 부르면 굵어지며 번쩍인 뒤 사라짐 (life 동안 서서히 투명)
public class LightLine : MonoBehaviour
{
    private LineRenderer line;
    private float width;
    private Color color;
    private float life;
    private float time;
    private float burst;              // 폭발 연출 남은 정도 (1 → 0)
    private bool fading = true;

    public void Init(LineRenderer line, float width, Color color, float life)
    {
        this.line = line;
        this.width = width;
        this.color = color;
        this.life = Mathf.Max(0.05f, life);
    }

    // 끝점 갱신 (날아가는 화살을 따라가는 빛줄기)
    public void SetEnd(Vector3 end)
    {
        line.SetPosition(1, end);
    }

    // 사라지지 않고 유지 (Explode 또는 Release 전까지)
    public void Hold()
    {
        fading = false;
    }

    public void Release()
    {
        fading = true;
        time = 0f;
    }

    // 빛줄기 전체가 함께 굵어지며 번쩍
    public void Explode(float scale = 3f)
    {
        burst = 1f;
        width *= scale;
        color = Color.Lerp(color, Color.white, 0.6f);
        Release();
    }

    private void Update()
    {
        if (fading)
        {
            time += Time.unscaledDeltaTime;
        }
        float k = fading ? Mathf.Clamp01(time / life) : 0f;
        burst = Mathf.MoveTowards(burst, 0f, Time.unscaledDeltaTime / 0.15f);
        line.widthMultiplier = width * (1f + burst * 0.5f) * (1f - k * 0.6f);
        Color c = new Color(color.r, color.g, color.b, color.a * (1f - k));
        line.startColor = c;
        line.endColor = c;
        if (fading && k >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

// 퍼지는 고리 (충격파)
public class RingExpand : MonoBehaviour
{
    private SpriteRenderer target;
    private float maxDiameter;
    private float life;
    private float time;

    public void Init(SpriteRenderer target, float maxDiameter, float life)
    {
        this.target = target;
        this.maxDiameter = maxDiameter;
        this.life = Mathf.Max(0.05f, life);
    }

    private void Update()
    {
        time += Time.deltaTime;
        float k = Mathf.Clamp01(time / life);
        float eased = 1f - (1f - k) * (1f - k);
        transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxDiameter, eased);
        target.color = new Color(0.9f, 0.97f, 1f, 1f - k);
        if (k >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

// 두 점을 잇는 늘린 줄기 스프라이트 (속도선/번개 줄기). Burst로 굵어지며 번쩍, Fade로 서서히 사라짐
public class StreakLine : MonoBehaviour
{
    private SpriteRenderer target;
    private Vector3 a;
    private Vector3 b;
    private float thickness;
    private Color color;
    private float fadeTime = -1f;
    private float fadeElapsed;
    private float burst;              // 1 → 0
    private float shimmer;

    public void Init(SpriteRenderer renderer, Vector3 a, Vector3 b, float thickness, Color color)
    {
        target = renderer;
        this.a = a;
        this.b = b;
        this.thickness = thickness;
        this.color = color;
        Apply();
    }

    public void SetEnd(Vector3 end)
    {
        b = end;
        Apply();
    }

    public void Fade(float time)
    {
        fadeTime = Mathf.Max(0.05f, time);
        fadeElapsed = 0f;
    }

    // 줄기 전체가 굵어지며 하얗게 번쩍인 뒤 time초 동안 사라짐
    public void Burst(float time)
    {
        burst = 1f;
        Fade(time);
    }

    private void Apply()
    {
        if (target == null || target.sprite == null)
            return;
        Vector3 diff = b - a;
        float length = Mathf.Max(0.05f, diff.magnitude);
        Vector2 spriteSize = target.sprite.bounds.size;
        transform.position = (a + b) * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        float widen = 1f + burst * 2f;
        transform.localScale = new Vector3(length / spriteSize.x, thickness * widen / spriteSize.y, 1f);
    }

    private void Update()
    {
        shimmer += Time.deltaTime;
        burst = Mathf.MoveTowards(burst, 0f, Time.deltaTime / 0.18f);
        float alpha = color.a * (0.85f + 0.15f * Mathf.Sin(shimmer * 30f));
        if (fadeTime > 0f)
        {
            fadeElapsed += Time.deltaTime;
            alpha *= 1f - Mathf.Clamp01(fadeElapsed / fadeTime);
            if (fadeElapsed >= fadeTime)
            {
                Destroy(gameObject);
                return;
            }
        }
        Color c = Color.Lerp(color, Color.white, burst * 0.7f);
        target.color = new Color(c.r, c.g, c.b, alpha);
        Apply();
    }
}

// 시너지 짝 무기 레벨업 카드 위 은은한 금빛 반짝임
public class CardSparkles : MonoBehaviour
{
    private class Spark
    {
        public Image image;
        public float age;
        public float life;
        public float size;
    }

    private readonly List<Spark> sparks = new List<Spark>();
    private RectTransform root;
    private float nextSpawn;

    private void OnEnable()
    {
        if (root == null)
        {
            GameObject go = new GameObject("Synergy Sparkles", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }
        root.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        foreach (Spark p in sparks)
        {
            if (p.image != null) Destroy(p.image.gameObject);
        }
        sparks.Clear();
        if (root != null)
        {
            root.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Rect r = root.rect;
        root.SetAsLastSibling();

        if (Time.unscaledTime >= nextSpawn)
        {
            nextSpawn = Time.unscaledTime + Random.Range(0.12f, 0.28f);
            GameObject go = new GameObject("Spark", typeof(RectTransform));
            go.transform.SetParent(root, false);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = SpriteFx.SparkleSprite(Random.Range(1, 4));
            image.color = new Color(1f, 0.9f, 0.55f, 0f);
            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Random.Range(-0.45f, 0.45f) * r.width, Random.Range(-0.45f, 0.45f) * r.height);
            sparks.Add(new Spark { image = image, life = Random.Range(0.5f, 0.9f), size = r.width * Random.Range(0.08f, 0.15f) });
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark p = sparks[i];
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f || p.image == null)
            {
                if (p.image != null) Destroy(p.image.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            float wave = Mathf.Sin(k * Mathf.PI);
            p.image.rectTransform.sizeDelta = Vector2.one * p.size * (0.5f + 0.5f * wave);
            p.image.rectTransform.anchoredPosition += Vector2.up * r.height * 0.05f * dt;
            p.image.color = new Color(1f, 0.9f, 0.55f, 0.85f * wave);
        }
    }
}

// 화면 베기: 지금 화면을 찍어 두 선을 따라 4조각으로 나눈 뒤, 조각이 선을 따라 어긋났다가 다시 붙음
// (오버레이 캔버스 + 마스크로 반평면을 잘라 냄. 조각 사이 틈은 검게)
public class ScreenSlice : MonoBehaviour
{
    private Vector2[] starts;
    private Vector2[] ends;
    private float slide;
    private float gap;
    private float hold;

    private Canvas canvas;
    private Texture2D shot;
    private Image background;
    private readonly List<RectTransform> pieces = new List<RectTransform>();
    private readonly List<Vector2> offsets = new List<Vector2>();

    private const float OpenTime = 0.06f, CloseTime = 0.16f;

    // 화면이 잘렸다가 다시 붙을 때까지 걸리는 시간 (unscaled)
    public static float TotalTime(float hold) => OpenTime + hold + CloseTime;

    // 월드 좌표의 베기 선 2개 (a[i] → b[i])
    public static void Play(Vector3[] a, Vector3[] b, float hold)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        GameObject go = new GameObject("Screen Slice");
        ScreenSlice slice = go.AddComponent<ScreenSlice>();
        slice.starts = new Vector2[a.Length];
        slice.ends = new Vector2[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            slice.starts[i] = cam.WorldToScreenPoint(a[i]);
            slice.ends[i] = cam.WorldToScreenPoint(b[i]);
        }
        float unit = Screen.height / 1280f;
        slice.slide = 34f * unit;                    // 선을 따라 어긋나는 거리
        slice.gap = 7f * unit;                       // 선 수직으로 벌어지는 틈
        slice.hold = hold;
        slice.StartCoroutine(slice.Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForEndOfFrame();       // 이번 프레임 화면을 그대로 찍음 (정지 중에도 동작)
        shot = ScreenCapture.CaptureScreenshotAsTexture();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 44;                    // HUD 위, 화면 번쩍임(46) 아래

        background = NewRect<Image>("Gap", transform, Vector2.zero);
        background.color = Color.black;
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        background.raycastTarget = false;

        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        int lines = Mathf.Min(2, starts.Length);
        int count = 1 << lines;
        for (int mask = 0; mask < count; mask++)
        {
            RectTransform root = NewRect<RectTransform>("Piece", transform, Vector2.zero);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            Transform parent = root;
            Vector2 offset = Vector2.zero;
            for (int i = 0; i < lines; i++)
            {
                int side = (mask & (1 << i)) != 0 ? 1 : -1;
                Vector2 dir = (ends[i] - starts[i]).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (side > 0 ? 0f : 180f);

                // 선의 한쪽(반평면)만 보이게 하는 마스크
                Image half = NewRect<Image>("Half", parent, Vector2.zero);
                half.raycastTarget = false;
                RectTransform hr = half.rectTransform;
                hr.pivot = new Vector2(0.5f, 0f);
                hr.sizeDelta = new Vector2(12000f, 12000f);
                hr.position = root.TransformPoint((starts[i] + ends[i]) * 0.5f);
                hr.rotation = Quaternion.Euler(0f, 0f, angle);
                half.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                parent = hr;

                offset += side * (normal * gap + dir * slide);
            }

            RawImage raw = NewRect<RawImage>("Shot", parent, Vector2.zero);
            raw.texture = shot;
            raw.raycastTarget = false;
            raw.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
            raw.rectTransform.position = root.TransformPoint(center);
            raw.rectTransform.rotation = Quaternion.identity;

            pieces.Add(root);
            offsets.Add(offset);
        }

        // 벌어짐 → 잠깐 유지 → 다시 붙음 (unscaled)
        float open = OpenTime, close = CloseTime;
        float t = 0f;
        while (t < open + hold + close)
        {
            t += Time.unscaledDeltaTime;
            float k = t < open ? t / open : t < open + hold ? 1f : 1f - (t - open - hold) / close;
            k = Mathf.Clamp01(k);
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].anchoredPosition = offsets[i] * k;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private static T NewRect<T>(string name, Transform parent, Vector2 position) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).anchoredPosition = position;
        return typeof(T) == typeof(RectTransform) ? go.GetComponent<T>() : go.AddComponent<T>();
    }

    private void OnDestroy()
    {
        if (shot != null)
        {
            Destroy(shot);
        }
    }
}

// 화면 어두워짐 → 밝아짐
public class ScreenDimFx : MonoBehaviour
{
    private Image image;
    private float alpha;
    private float darken;
    private float brighten;
    private float time;

    public void Init(Image image, float alpha, float darken, float brighten)
    {
        this.image = image;
        this.alpha = alpha;
        this.darken = Mathf.Max(0.01f, darken);
        this.brighten = Mathf.Max(0.01f, brighten);
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        float k = time < darken ? time / darken : 1f - (time - darken) / brighten;
        image.color = new Color(0f, 0f, 0f, alpha * Mathf.Clamp01(k));
        if (time >= darken + brighten)
        {
            Destroy(gameObject);
        }
    }
}
