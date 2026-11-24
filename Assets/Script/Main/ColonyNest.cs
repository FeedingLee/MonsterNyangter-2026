using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 군락 둥지 구조물 (지시서 1120_군락_시스템 B·D)
// - 움직이지 않고 넉백 면역, 접촉 피해 없음 (트리거 충돌체라 플레이어와 부딪히지 않음)
// - 플레이어 무기(발사체·범위 공격·연쇄 번개)로 피해, 보조무기 자동 조준 대상 (Enemy 레이어)
// - 처치 폭발·생명의 일격 처치 수·사냥 본능은 적용하지 않음
public class ColonyNest : MonoBehaviour
{
    public static readonly List<ColonyNest> Alive = new List<ColonyNest>();

    public bool IsLive { get; private set; }
    public Sprite Icon => frames != null && frames.Length > 0 ? frames[0] : null;
    public ColonySystem.Group Guards { get; private set; }

    private float maxHealth, health;
    private Sprite[] frames;
    private float frameTime;
    private SpriteRenderer body;
    private Transform barFill;
    private SpriteRenderer barFillRenderer;
    private GameObject barRoot;
    private float barWidth;
    private float flashUntil;
    private RunConfig rc;

    public static ColonyNest Create(Vector2 position, float hp, Sprite[] frames, ColonySystem.Group guards)
    {
        RunConfig rc = RunConfig.Instance;
        GameObject go = new GameObject("Colony Nest");
        go.transform.position = position;
        go.tag = "Enemy";                                    // 발사체 관통 처리 (Bullet)
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) go.layer = enemyLayer;          // 보조무기 자동 조준(Scanner) 대상 레이어

        ColonyNest nest = go.AddComponent<ColonyNest>();
        nest.rc = rc;
        nest.frames = frames;
        nest.maxHealth = nest.health = Mathf.Max(1f, hp);
        nest.Guards = guards;
        nest.IsLive = true;

        nest.body = go.AddComponent<SpriteRenderer>();
        nest.body.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        nest.body.sortingLayerName = rc.colonySortingLayer;   // 몬스터와 같은 층 (몬스터보다 뒤 순서)
        nest.body.sortingOrder = rc.colonyNestSortingOrder;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;              // 움직이지 않음, 밀리지 않음
        rb.useFullKinematicContacts = true;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;                                // 접촉 피해·몸 충돌 없음
        Vector2 size = nest.body.sprite != null ? (Vector2)nest.body.sprite.bounds.size : Vector2.one * 2f;
        box.size = new Vector2(size.x * rc.colonyHitboxScale.x, size.y * rc.colonyHitboxScale.y);
        box.offset = new Vector2(0f, nest.body.sprite != null ? nest.body.sprite.bounds.center.y : 0f);

        nest.BuildBar(size.y * rc.colonyBarHeight);
        if (rc.colonyShadow) GroundShadow.Attach(go, rc.colonyShadowSize, rc.colonyShadowOffsetY);   // 기본 끔
        Alive.Add(nest);
        return nest;
    }

    // 머리 위 체력 바 (게임 에셋 게이지 그림)
    private void BuildBar(float height)
    {
        barRoot = new GameObject("Hp Bar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = new Vector3(0f, height + rc.colonyBarGap, 0f);
        barWidth = rc.colonyBarSize.x;
        Sprite white = WhiteSprite();
        SpriteRenderer back = MakeBarPart("Back", white, new Color(0.08f, 0.05f, 0.04f, 0.85f), 0);
        back.drawMode = SpriteDrawMode.Simple;
        back.transform.localScale = new Vector3(barWidth + 0.08f, rc.colonyBarSize.y + 0.08f, 1f);
        Sprite fill = white;                                 // 붉은 체력 (테두리는 어두운 바탕)
        barFillRenderer = MakeBarPart("Fill", fill, rc.colonyBarColor, 1);
        barFill = barFillRenderer.transform;
        if (fill != white && fill.border.sqrMagnitude > 0f)
        {
            barFillRenderer.drawMode = SpriteDrawMode.Sliced;
            barFillRenderer.size = new Vector2(barWidth, rc.colonyBarSize.y);
        }
        else
        {
            barFill.localScale = new Vector3(barWidth / Mathf.Max(0.0001f, fill.bounds.size.x), rc.colonyBarSize.y / Mathf.Max(0.0001f, fill.bounds.size.y), 1f);
        }
        UpdateBar();
    }

    // 1 x 1 칸 흰 그림 (체력 바 바탕)
    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }

    private SpriteRenderer MakeBarPart(string name, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(barRoot.transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingLayerID = body.sortingLayerID;
        r.sortingOrder = rc.colonyNestSortingOrder + 20 + order;
        return r;
    }

    private void UpdateBar()
    {
        float k = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        // 왼쪽 끝 고정으로 줄어듦
        if (barFillRenderer.drawMode == SpriteDrawMode.Sliced)
        {
            barFillRenderer.size = new Vector2(Mathf.Max(0.001f, barWidth * k), rc.colonyBarSize.y);
            barFill.localPosition = new Vector3(-barWidth * 0.5f * (1f - k), 0f, 0f);
        }
        else
        {
            Vector3 s = barFill.localScale;
            float full = barWidth / Mathf.Max(0.0001f, barFillRenderer.sprite.bounds.size.x);
            barFill.localScale = new Vector3(full * k, s.y, 1f);
            barFill.localPosition = new Vector3(-barWidth * 0.5f * (1f - k), 0f, 0f);
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || body == null)
            return;
        if (IsLive)
        {
            if (rc.colonyNestFps > 0f)                       // 0이면 첫 프레임 고정 (꿈틀거림 없음)
            {
                frameTime += Time.deltaTime * rc.colonyNestFps;
                body.sprite = frames[(int)frameTime % frames.Length];
            }
            body.color = Time.time < flashUntil ? rc.colonyHitFlashColor : Color.white;   // 맞으면 잠깐 밝게
        }
    }

    // 발사체 피해 (중간보스와 같은 방식). 사냥 본능 없음(일반 몬스터 취급), 처치 폭발 없음
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsLive || !collision.CompareTag("Bullet"))
            return;
        Bullet bullet = collision.GetComponent<Bullet>();
        if (bullet == null || bullet.IsHarmless || !bullet.TryHitTarget(gameObject))
            return;
        float damage = GimmickCards.ModifyHit(bullet.HitDamage * bullet.VolleyScale(gameObject), !bullet.SubWeapon, GimmickCards.TargetKind.Mob, bullet);
        TakeDamage(damage);
        GimmickCards.AfterHit(transform.position, damage, false);   // 연쇄 번개 적중 수만 (처치 폭발 없음)
        bullet.RegisterHit(gameObject);
        bullet.NotifyHit(gameObject);
    }

    public void TakeDamage(float damage)
    {
        if (!IsLive || damage <= 0f)
            return;
        health -= damage;
        DamageNumber.Add(body, damage);
        SpriteFx.HitSpark(transform.position + Vector3.up * 0.5f);
        flashUntil = Time.time + 0.08f;
        if (Guards != null) Guards.Alert();                  // 둥지를 공격하면 호위 무리도 추적 시작
        UpdateBar();
        if (health <= 0f) Break();
    }

    // D: 파괴 연출 + 효과음 + 경험치 구슬·냥냥코인 흩뿌림 (둥지 습격이면 x2)
    public void Break()
    {
        if (!IsLive)
            return;
        IsLive = false;
        health = 0f;
        Alive.Remove(this);
        GetComponent<Collider2D>().enabled = false;
        if (barRoot != null) barRoot.SetActive(false);
        Vector3 pos = transform.position;
        SpriteFx.Explosion(pos + Vector3.up * 0.4f, rc.colonyBreakFxSize);
        SpriteFx.EnemyPop(pos);
        if (rc.colonyBreakSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(rc.colonyBreakSound);

        float mul = ColonySystem.RaidActive ? RunConfig.Instance.morningCards.nestRewardMul : 1f;
        int orbs = Mathf.RoundToInt(rc.colonyExpMul * mul);
        int coins = Mathf.RoundToInt(Random.Range(rc.colonyCoinMin, rc.colonyCoinMax + 1) * mul);
        if (ColonySystem.Instance != null) ColonySystem.Instance.StartCoroutine(Scatter(pos, orbs, coins));   // 둥지가 사라져도 끝까지 뿌리게
        StartCoroutine(FadeOut(Color.white, rc.colonyBreakFadeTime));
    }

    // 둥지 주변에 조금씩 시간을 두고 흩뿌림 (보상 상자처럼)
    private static IEnumerator Scatter(Vector3 pos, int orbs, int coins)
    {
        RunConfig rc = RunConfig.Instance;
        int total = Mathf.Max(1, orbs + coins);
        float perSec = total / Mathf.Max(0.05f, rc.colonyScatterTime);
        float budget = 0f;
        int o = orbs, c = coins;
        while (o > 0 || c > 0)
        {
            budget += Time.deltaTime * perSec;
            if (budget < 1f) { yield return null; continue; }
            budget -= 1f;
            Vector2 offset = Random.insideUnitCircle * rc.colonyScatterRadius;
            if (o > 0 && (c == 0 || Random.value < (float)o / (o + c)))
            {
                Transform exp = GameManager.instance.pool.GetEnemy(0).transform;
                exp.parent = GameManager.instance.pool.transform;
                exp.position = CampZone.FreeSpot(pos + (Vector3)offset, 0.3f);
                o--;
            }
            else
            {
                Coin.Drop(pos + (Vector3)offset, 1);
                c--;
            }
        }
    }

    // E-1: 낮이 끝날 때 못 깬 둥지 → 붉은 기운 후 사라짐 (보상 없음)
    public void FailAndVanish()
    {
        if (!IsLive)
            return;
        IsLive = false;
        Alive.Remove(this);
        GetComponent<Collider2D>().enabled = false;
        if (barRoot != null) barRoot.SetActive(false);
        StartCoroutine(FailRoutine());
    }

    private IEnumerator FailRoutine()
    {
        float t = 0f, dur = Mathf.Max(0.05f, rc.colonyFailGlowTime);
        Vector3 baseScale = transform.localScale;
        while (t < dur)
        {
            t += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 14f);
            body.color = Color.Lerp(Color.white, rc.colonyFailColor, 0.5f + 0.5f * pulse);
            transform.localScale = baseScale * (1f + 0.05f * pulse);
            yield return null;
        }
        transform.localScale = baseScale;
        yield return FadeOut(rc.colonyFailColor, rc.colonyBreakFadeTime);
    }

    private IEnumerator FadeOut(Color tint, float time)
    {
        float t = 0f;
        time = Mathf.Max(0.01f, time);
        while (t < time)
        {
            t += Time.deltaTime;
            body.color = new Color(tint.r, tint.g, tint.b, 1f - t / time);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Alive.Remove(this);
    }
}
