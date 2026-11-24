using System.Collections.Generic;
using UnityEngine;

// 중간보스 한 마리
// - 상태별 프레임 애니메이션 (대기, 이동, 공격, 경계, 피격, 사망), 플레이어 쪽을 바라봄
// - 무기 발사체(Bullet 태그)와 범위 피해(CombatUtil 등)로 피해를 받음. 넉백 저항만큼 덜 밀림
// - 종별 행동: 모스(추적), 케스토돈(추적 + 예고 후 직선 돌진), 말벌(원 대형 조임, 통과 불가), 사슴벌레(느린 추적 + 격려 + 소환)
public class MiniBoss : MonoBehaviour
{
    public static readonly List<MiniBoss> Alive = new List<MiniBoss>();

    public MiniBossConfig.Species Data { get; private set; }
    public MiniBossDirector.Group Group { get; private set; }
    public bool IsLive { get; private set; }
    public float MaxHealth { get; private set; }
    public float Health { get; private set; }

    // 플레이어 접촉 피해 배율 (말벌은 자체 간격 피해라 0)
    public float ContactDamageScale => Data != null && Data.kind != MiniBossConfig.Kind.Wasp ? Data.attackMul : 0f;

    private enum State { Chase, Warn, Charge, Summon }

    private MiniBossConfig config;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Transform player;
    private float speed;
    private float baseMonsterHealth;
    private float baseMonsterSpeed;
    private bool facesRight;

    private State state = State.Chase;
    private float stateTime;
    private float nextAction;
    private Vector2 chargeDir;
    private float chargeLeft;
    private SpriteRenderer chargeLine;
    private float blockedSince = -1f;                          // 돌진 차례인데 텐트 울타리에 가려 기다리기 시작한 시각
    private bool cornerCharge;                                 // 이번 돌진이 모서리로 질러가는 돌진인지
    private float cornerChargeLength;
    private bool chainCharge;                                  // 모서리 돌진 직후: 쿨타임 없이 짧은 예고로 바로 이어서 돌진
    private bool wallRide;                                     // 예고 중 울타리 뒤로 숨은 플레이어를 향한 돌진: 울타리에 닿으면 벽을 따라 미끄러지며 계속
    private float warnDuration;                                // 이번 예고 시간
    private float warnLength;                                  // 이번 돌진이 실제로 이동할 거리 (예고 화살표 길이)
    private float bodyRadius = 0.5f;

    private Vector2 knockVelocity;
    private float hitUntil;
    private float animTime;
    private Sprite[] currentFrames;
    private float deathTime = -1f;
    private float nextWaspHit;
    private float nextRally;
    private float stunUntil;

    // 마비 두꺼비: 이동만 멈춤
    public void Stun(float seconds)
    {
        stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
    }

    private readonly List<Enemy> summoned = new List<Enemy>();
    private int herculesType = -1;

    // 말벌 원 대형에서의 자리 (0 ~ count-1)
    public int FormationIndex { get; set; }

    public static MiniBoss Create(MiniBossConfig.Species data, MiniBossDirector.Group group, Vector3 position, float monsterHealth, float monsterSpeed)
    {
        MiniBossConfig c = MiniBossConfig.Instance;
        GameObject prefab = MonsterPrefab();

        GameObject go = new GameObject("MiniBoss " + data.displayName);
        go.tag = "Enemy";
        go.layer = prefab != null ? prefab.layer : 0;
        go.transform.position = position;

        MiniBoss boss = go.AddComponent<MiniBoss>();
        boss.config = c;
        boss.Data = data;
        boss.Group = group;
        boss.baseMonsterHealth = monsterHealth;
        boss.baseMonsterSpeed = monsterSpeed;
        boss.facesRight = data.facesRight;

        boss.sr = go.AddComponent<SpriteRenderer>();
        SpriteRenderer monsterRenderer = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
        if (monsterRenderer != null)
        {
            boss.sr.sharedMaterial = monsterRenderer.sharedMaterial;
            boss.sr.sortingLayerID = monsterRenderer.sortingLayerID;
            boss.sr.sortingOrder = monsterRenderer.sortingOrder + 1;
        }
        if (!string.IsNullOrEmpty(c.sortingLayer) && SortingLayer.NameToID(c.sortingLayer) != 0)
        {
            boss.sr.sortingLayerName = c.sortingLayer;         // 캠프보다 위
            boss.sr.sortingOrder = 2;
        }
        if (c.bodyMaterial != null && data.crispBody)
        {
            boss.sr.sharedMaterial = c.bodyMaterial;           // 반투명 테두리 잘라 또렷하게
        }
        boss.sr.color = data.tint;
        Sprite first = First(data.idle) ?? First(data.move);
        boss.sr.sprite = first;

        // 크기: 일반 몬스터 키 x size (메인 보스 키의 0.8배 이하)
        float monsterHeight = monsterRenderer != null && monsterRenderer.sprite != null
            ? monsterRenderer.sprite.bounds.size.y * Mathf.Abs(prefab.transform.localScale.y) : 1f;
        float targetHeight = monsterHeight * data.size * (data.useSizeBoost ? c.sizeBoost : 1f);
        float bossLimit = BossHeight() * 0.8f;
        if (bossLimit > 0f)
        {
            targetHeight = Mathf.Min(targetHeight, bossLimit);
        }
        float frameHeight = first != null ? first.bounds.size.y : 1f;
        go.transform.localScale = Vector3.one * (targetHeight / Mathf.Max(0.01f, frameHeight));
        boss.baseScale = go.transform.localScale.x;
        boss.bodyRadius = (first != null ? first.bounds.size.x : 1f) * go.transform.localScale.x * c.bodyRadiusRate;

        boss.rb = go.AddComponent<Rigidbody2D>();
        boss.rb.gravityScale = 0f;
        boss.rb.freezeRotation = true;
        // 보간은 첫 물리 프레임 뒤에 켬: 생성 직후 보간이 원점에서 시작해 화면 안(원점과 등장 위치 사이)에 한 프레임 보였다가 사라지는 문제
        boss.rb.interpolation = RigidbodyInterpolation2D.None;
        boss.rb.position = position;
        boss.interpolatePending = true;
        if (data.kind == MiniBossConfig.Kind.Wasp)
        {
            boss.rb.bodyType = RigidbodyType2D.Kinematic;          // 플레이어가 밀고 지나갈 수 없음 (구르기·돌진 중에도)
            boss.rb.useFullKinematicContacts = true;
        }
        else
        {
            boss.rb.mass = 3f + data.knockbackResist * 20f;
        }

        boss.col = go.AddComponent<CapsuleCollider2D>();
        if (first != null)
        {
            Vector2 size = first.bounds.size;
            boss.col.size = new Vector2(size.x * 0.65f, size.y * 0.55f);
            if (data.kind == MiniBossConfig.Kind.Wasp && data.count > 0)
            {
                // 처음 원에서 옆 말벌까지 빈틈이 없도록 몸 충돌 폭을 넓힘
                float spacing = 2f * Mathf.PI * c.waspStartRadius / data.count * c.waspWallOverlap;
                boss.col.size = new Vector2(Mathf.Max(boss.col.size.x, spacing / Mathf.Max(0.01f, go.transform.localScale.x)), boss.col.size.y);
            }
            boss.col.offset = new Vector2(first.bounds.center.x, first.bounds.center.y - size.y * 0.1f);
            boss.colOffsetX = boss.col.offset.x;              // 그림을 좌우로 뒤집을 때 판정도 같이 뒤집음 (머리 쪽이 비지 않게)
            boss.col.direction = boss.col.size.x > boss.col.size.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
        }

        // 능력치 (그 시점 일반 몬스터 기준)
        boss.MaxHealth = Mathf.Max(1f, monsterHealth * data.healthMul * c.healthBoost * RunStats.Get(RunStat.EnemyHealth));
        boss.Health = boss.MaxHealth;
        boss.speed = monsterSpeed * data.speedMul * c.speedBoost;
        boss.player = GameManager.instance.player.transform;
        boss.IsLive = true;
        boss.nextAction = Time.time + c.chargeInterval * Random.Range(0.6f, 1f);
        if (data.kind == MiniBossConfig.Kind.Beetle)
        {
            boss.nextAction = Time.time + c.summonInterval * 0.5f;
        }

        // 메인 보스와는 서로 밀지 않음
        if (BossPattern.Current != null)
        {
            BossPattern.Current.IgnoreCollisionWith(boss.col);
        }

        Alive.Add(boss);
        return boss;
    }

    private static Sprite First(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    private static GameObject MonsterPrefab()
    {
        PoolManager pool = GameManager.instance.pool;
        foreach (GameObject p in pool.prefabs)
        {
            if (p != null && p.GetComponent<Enemy>() != null)
                return p;
        }
        return null;
    }

    private static float BossHeight()
    {
        PoolManager pool = GameManager.instance.pool;
        foreach (GameObject p in pool.enemyPrefabs)
        {
            if (p != null && p.GetComponent<BossPattern>() != null && p.TryGetComponent(out SpriteRenderer r) && r.sprite != null)
                return r.sprite.bounds.size.y * Mathf.Abs(p.transform.localScale.y);
        }
        return 0f;
    }

    private void OnDestroy()
    {
        Alive.Remove(this);
        if (chargeLine != null)
        {
            Destroy(chargeLine.gameObject);
        }
        if (summonCircle != null)
        {
            Destroy(summonCircle.gameObject);
        }
        foreach (SpriteRenderer r in spotCircles)
        {
            if (r != null) Destroy(r.gameObject);
        }
    }

    // ---------------------------------------------------------------
    // 애니메이션
    // ---------------------------------------------------------------
    private void Animate(Sprite[] frames, bool loop, float speedMul = 1f)
    {
        if (frames == null || frames.Length == 0)
            frames = Data.idle;
        if (frames == null || frames.Length == 0)
            return;
        if (frames != currentFrames)
        {
            currentFrames = frames;
            animTime = 0f;
        }
        animTime += Time.deltaTime * speedMul;
        int index = Mathf.FloorToInt(animTime * config.animFps * Mathf.Max(0.1f, Data.animSpeed));
        index = loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1);
        sr.sprite = frames[index];
    }

    private void Face(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > 0.05f)
        {
            sr.flipX = facesRight ? dir.x < 0f : dir.x > 0f;
            if (col != null && colOffsetX != 0f)
            {
                col.offset = new Vector2(sr.flipX ? -colOffsetX : colOffsetX, col.offset.y);
            }
        }
    }

    private float colOffsetX;                                // 뒤집지 않았을 때 몸 판정 가로 위치

    // ---------------------------------------------------------------
    // 낮 몬스터 은신 보관 (DayMonsterStash): 귀환 준비 시작 때 땅속으로 사라졌다가 다음 날 낮에 카메라 밖에서 다시 등장
    // 오브젝트를 그대로 꺼 두므로 체력·무리(처치 수) 판정이 날짜를 넘어도 이어짐
    // ---------------------------------------------------------------
    private float hideStart = -1f;
    private float hideDuration;
    private Vector3 hideFrom;

    public bool Hiding => hideStart >= 0f;

    public void HideForNight(float duration, float sink)
    {
        if (!IsLive || Hiding)
            return;
        IsLive = false;                                      // 숨는 동안 공격·이동·피격 없음
        Alive.Remove(this);                                  // 위치 표시·대형 계산에서 빠짐
        hideStart = Time.time;
        hideDuration = Mathf.Max(0.01f, duration);
        hideFrom = transform.position;
        hideSink = sink;
        col.enabled = false;
        if (rb != null) rb.velocity = Vector2.zero;
        ShowChargeLine(false);
        ShowSummonCircle(false);
        HideSummonSpots();
    }

    private float hideSink;

    private void UpdateHide()
    {
        float k = Mathf.Clamp01((Time.time - hideStart) / hideDuration);
        transform.position = hideFrom + Vector3.down * hideSink * k;   // 아래로 가라앉으며
        sr.color = new Color(Data.tint.r, Data.tint.g, Data.tint.b, 1f - k);   // 투명해짐
        if (k >= 1f)
        {
            hideStart = -1f;
            gameObject.SetActive(false);
        }
    }

    // 다음 날 낮: 같은 체력·무리로 카메라 밖에서 다시 추격 (비드론은 원 진형을 풀고 각자 추적)
    public void Reappear(Vector2 position)
    {
        hideStart = -1f;
        transform.position = position;
        gameObject.SetActive(true);
        if (rb != null)
        {
            rb.position = position;
            rb.velocity = Vector2.zero;
        }
        col.enabled = true;
        sr.color = Data.tint;
        state = State.Chase;
        stateTime = 0f;
        chargeLeft = 0f;
        chainCharge = false;
        wallRide = false;
        knockVelocity = Vector2.zero;
        hitUntil = 0f;
        stunUntil = 0f;
        nextAction = Time.time + config.chargeInterval * Random.Range(0.6f, 1f);
        if (Data.kind == MiniBossConfig.Kind.Wasp && Group != null) Group.waspChasing = true;
        IsLive = true;
        if (!Alive.Contains(this)) Alive.Add(this);
    }

    private void Update()
    {
        if (Hiding)
        {
            UpdateHide();
            return;
        }
        if (deathTime >= 0f)
        {
            // 사망: 쓰러지는 프레임 후 서서히 사라짐
            Animate(Data.death, false);
            KeepDeathSize();
            float t = Time.time - deathTime;
            float fade = Mathf.Clamp01((t - 0.6f) / 0.6f);
            sr.color = new Color(Data.tint.r, Data.tint.g, Data.tint.b, 1f - fade);
            ShowSummonCircle(false);
            HideSummonSpots();
            if (fade >= 1f)
            {
                Destroy(gameObject);
            }
            return;
        }

        GameManager gm = GameManager.instance;
        if (gm == null || !gm.isLive || !IsLive)
            return;

        Vector2 toPlayer = player.position - transform.position;
        stateTime += Time.deltaTime;

        // 피격: 프레임은 그대로 두고 색만 잠깐 물듦 (자주 맞아도 꿈틀거리지 않게)
        sr.color = Time.time < hitUntil ? config.hitFlashColor * Data.tint : Data.tint;
        {
            switch (state)
            {
                case State.Warn:
                case State.Summon:
                    Animate(Data.alert, true);
                    break;
                case State.Charge:
                    if (config.chargeRunFrames && Data.move != null && Data.move.Length > 1)
                        Animate(Data.move, true, config.chargeRunAnimMul);   // 돌진: 달리기 그림을 빠르게 (한 장짜리 공격 그림이면 미끄러지듯 보임)
                    else
                        Animate(Data.attack, true);
                    break;
                default:
                    Animate(Data.move, true);
                    break;
            }
        }
        if (state != State.Charge)
        {
            Face(toPlayer);
        }

        // 걸을 때 발밑 먼지구름 (비드론 제외), 돌진 중에는 더 자주
        bool charging = state == State.Charge && config.chargeRunFrames;
        if (Data.kind != MiniBossConfig.Kind.Wasp && (state == State.Chase || charging) && Time.time >= nextStepDust && Time.time >= stunUntil)
        {
            nextStepDust = Time.time + config.stepDustInterval * (charging ? 1f / Mathf.Max(1f, config.chargeRunAnimMul) : 1f) * Random.Range(0.8f, 1.2f);
            SpriteFx.BossStepDust(transform.position + Vector3.right * Random.Range(-0.2f, 0.2f) * BodyWidth, BodyWidth * config.stepDustSize, sr.flipX);
        }

        switch (Data.kind)
        {
            case MiniBossConfig.Kind.Kestodon: UpdateKestodon(toPlayer); break;
            case MiniBossConfig.Kind.Beetle: UpdateBeetle(); break;
        }
    }

    private float nextStepDust;
    private float baseScale = 1f;

    // 사망 프레임이 걷는 그림보다 작게 그려져 있으면 줄어드는 것처럼 보이므로, 가로 크기를 걷는 그림에 맞춰 키움 (부드럽게)
    private void KeepDeathSize()
    {
        Sprite walk = Data.move != null && Data.move.Length > 0 ? Data.move[0] : null;
        if (walk == null || sr.sprite == null) return;
        float k = Mathf.Clamp(walk.rect.width / Mathf.Max(1f, sr.sprite.rect.width), 1f, 1.4f);
        float target = baseScale * k;
        float s = Mathf.MoveTowards(transform.localScale.x, target, baseScale * 4f * Time.deltaTime);
        transform.localScale = new Vector3(s, s, 1f);
    }
    private float nextDebris;
    private float BodyWidth => sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * Mathf.Abs(transform.localScale.x) : 2f;

    private bool interpolatePending;

    private void FixedUpdate()
    {
        if (interpolatePending)
        {
            interpolatePending = false;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;   // 등장 위치에서 한 번 물리 갱신된 뒤부터 부드럽게
        }
        GameManager gm = GameManager.instance;
        if (gm == null || !gm.isLive || !IsLive || deathTime >= 0f)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.velocity = Vector2.zero;
            return;
        }

        Vector2 position = rb.position;
        Vector2 step = Vector2.zero;

        if (Data.kind == MiniBossConfig.Kind.Wasp && Group.waspChasing)
        {
            // 다 조인 뒤: 각자 플레이어 추적, 서로(다른 중간보스 포함) 겹치지 않게 밀어냄
            if (rb.bodyType != RigidbodyType2D.Dynamic)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.mass = 20f;
                col.size = new Vector2(Mathf.Min(col.size.x, sr.sprite != null ? sr.sprite.bounds.size.x * 0.65f : col.size.x), col.size.y);   // 벽처럼 넓혔던 몸을 원래대로
            }
            Vector2 toTarget = CampZone.Steer(position, player.position, false, true) - position;   // 텐트 울타리는 돌아서 (큰 몸 길)
            Vector2 chase = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized * speed : Vector2.zero;
            rb.MovePosition(CampZone.SlideAround(position, (chase + Separation()) * Time.fixedDeltaTime, CampRadius));   // 울타리를 따라 미끄러짐
            rb.velocity = Vector2.zero;
            return;
        }

        if (Data.kind == MiniBossConfig.Kind.Wasp)
        {
            // 원 대형: 무리의 중심(플레이어를 따라감) 둘레 자기 자리로
            Vector2 slot = Group.WaspSlot(FormationIndex);
            rb.MovePosition(CampZone.SlideOutside(Vector2.MoveTowards(position, slot, Mathf.Max(speed * 2f, 6f) * Time.fixedDeltaTime), BodyRadius));   // 울타리 안으로 들어가지 않게
            return;
        }

        if (Time.time < stunUntil && state != State.Charge)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        switch (state)
        {
            case State.Chase:
                Vector2 target = CampZone.Steer(position, Decoy.GetEnemyTarget(player.position), false, true);   // 텐트 울타리는 돌아서 (큰 몸 길)
                step = (target - position).normalized * speed * Time.fixedDeltaTime;
                break;
            case State.Charge:
                float move = Mathf.Min(chargeLeft, baseMonsterSpeed * config.chargeSpeedMul * Time.fixedDeltaTime);
                step = chargeDir * move;
                chargeLeft -= move;
                break;
        }

        // 중간보스끼리 겹치지 않게 (돌진 중 제외)
        if (state != State.Charge)
        {
            step += Separation() * Time.fixedDeltaTime;
        }

        // 넉백 (저항만큼 덜 밀리고 금방 멈춤)
        step += knockVelocity * Time.fixedDeltaTime;
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, 30f * Time.fixedDeltaTime);

        // 벽 타기 돌진: 울타리 모서리를 돌아 플레이어가 보이면 그 순간 한 번만 플레이어 쪽으로 다시 겨눔
        if (state == State.Charge && wallRide && !CampZone.LineBlocked(position, player.position, config.chargeLosMargin))
        {
            Vector2 toP = (Vector2)player.position - position;
            if (toP.sqrMagnitude > 0.01f) chargeDir = toP.normalized;
            step = chargeDir * step.magnitude;
            wallRide = false;
        }

        // 돌진 중 텐트 울타리에 닿으면: 벽 타기 돌진은 벽을 따라 플레이어 쪽으로 방향을 틀어 계속, 그 외에는 그 자리에서 돌진 끝
        if (state == State.Charge && CampZone.Overlaps(position + step, BodyRadius * 0.9f))
        {
            if (wallRide)
            {
                Vector2 next = position + step;
                bool sideWall = !Mathf.Approximately(CampZone.SlideOutside(next, BodyRadius).x, next.x);   // 좌우 벽이면 위아래로, 위아래 벽이면 좌우로
                Vector2 toP = (Vector2)player.position - position;
                chargeDir = sideWall ? new Vector2(0f, toP.y >= 0f ? 1f : -1f) : new Vector2(toP.x >= 0f ? 1f : -1f, 0f);
                step = chargeDir * step.magnitude;
            }
            else
            {
                chargeLeft = 0f;
            }
        }
        rb.MovePosition(CampZone.SlideAround(position, step, CampRadius));   // 울타리에 박히지 않고 따라 미끄러짐
        rb.velocity = Vector2.zero;
    }

    // 울타리 미끄러짐용 몸 반지름: 위치(기준점)에서 충돌체 끝까지 가장 먼 거리 (큰 몸이 울타리에 겹쳐 보이지 않게)
    private float CampRadius
    {
        get
        {
            if (col == null) return BodyRadius;
            Bounds b = col.bounds;
            Vector2 p = rb.position;
            float x = Mathf.Max(Mathf.Abs(b.max.x - p.x), Mathf.Abs(p.x - b.min.x));
            float y = Mathf.Max(Mathf.Abs(b.max.y - p.y), Mathf.Abs(p.y - b.min.y));
            return Mathf.Max(x, y) + RunConfig.Instance.campSlidePadding;
        }
    }

    // 울타리 미끄러짐용 몸 반지름 (콜라이더 기준)
    private float BodyRadius => col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.y) * 0.9f : 0.5f;

    // 다른 중간보스와 몸이 겹친 만큼 바깥으로 미는 속도
    private Vector2 Separation()
    {
        Vector2 push = Vector2.zero;
        Vector2 me = rb.position;
        foreach (MiniBoss other in Alive)
        {
            if (other == null || other == this || !other.IsLive)
                continue;
            Vector2 diff = me - (Vector2)other.transform.position;
            float min = bodyRadius + other.bodyRadius;
            float dist = diff.magnitude;
            if (dist >= min)
                continue;
            Vector2 dir = dist > 0.001f ? diff / dist : Random.insideUnitCircle.normalized;
            push += dir * config.separationPush * (min - dist) / Mathf.Max(0.01f, min);
        }
        return push;
    }

    // ---------------------------------------------------------------
    // 케스토돈: 일정 간격마다 멈춰 '!' + 방향선 → 방향 고정 직선 돌진
    // ---------------------------------------------------------------
    private void UpdateKestodon(Vector2 toPlayer)
    {
        switch (state)
        {
            case State.Chase:
                if (Time.time >= nextAction)
                {
                    // 돌진 예약: 울타리에 가려 있으면 돌아 들어가며 시야가 트이길 기다림 (다른 행동으로 넘어가지 않음)
                    Vector2 pos = transform.position;
                    Vector2 target = pos + toPlayer;
                    cornerCharge = false;
                    if (CampZone.LineBlocked(pos, target, config.chargeLosMargin))
                    {
                        if (blockedSince < 0f) blockedSince = Time.time;
                        if (!chainCharge && Time.time - blockedSince < config.chargeBlockedWait) break;   // 모서리 돌진을 잇는 중이면 기다리지 않고 다음 모서리로
                        // 오래 가려져 있으면 플레이어가 보이는 모서리로 질러가는 돌진
                        if (!CampZone.ChargeCorner(pos, target, config.chargeCornerMargin, out Vector2 corner)) break;
                        cornerCharge = true;
                        cornerChargeLength = Vector2.Distance(pos, corner) + BodyRadius;
                        toPlayer = corner - pos;
                    }
                    blockedSince = -1f;
                    warnDuration = chainCharge ? config.chargeChainWarnTime : config.chargeWarnTime;   // 모서리 돌진에 이어지는 예고는 짧게
                    chainCharge = false;
                    warnLength = cornerCharge ? Mathf.Min(cornerChargeLength, config.chargeDistance * 2f) : config.chargeDistance;
                    state = State.Warn;
                    stateTime = 0f;
                    chargeDir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector2.right;
                    ShowChargeLine(true);
                    SpriteFx.BossDustBurst(transform.position, BodyWidth * 1.2f);   // 돌진 전 먼지구름이 크게 퍼짐
                }
                break;
            case State.Warn:
                UpdateChargeLine();
                if (stateTime >= warnDuration)
                {
                    state = State.Charge;                        // 방향 고정 (돌진 중 방향 전환 없음)
                    stateTime = 0f;
                    if (config.chargeReaimOnRelease && !cornerCharge && toPlayer.sqrMagnitude > 0.01f) chargeDir = toPlayer.normalized;
                    // 예고 중 플레이어가 울타리 뒤로 숨었으면: 플레이어 쪽으로 다시 겨누고 벽을 타고 들이받음
                    Vector2 here = transform.position;
                    wallRide = !cornerCharge && toPlayer.sqrMagnitude > 0.01f && CampZone.LineBlocked(here, here + toPlayer, config.chargeLosMargin);
                    if (wallRide)
                    {
                        chargeDir = toPlayer.normalized;
                        chargeLeft = config.chargeDistance * config.chargeWallRideDistanceMul;   // 벽을 돌아가야 하니 더 멀리
                    }
                    if (!wallRide) chargeLeft = cornerCharge ? Mathf.Min(cornerChargeLength, config.chargeDistance * 2f) : config.chargeDistance;
                    ShowChargeLine(false);
                    CameraShake.Play(config.patternShake, config.patternShakeTime);   // 돌진 시작: 약한 흔들림
                    if (config.chargeIgnoresMonsters)
                    {
                        col.excludeLayers = 1 << gameObject.layer;   // 돌진 중 일반 몬스터·다른 중간보스와 부딪히지 않음
                    }
                    SpriteFx.GroundBurst(transform.position, 1.6f);
                }
                break;
            case State.Charge:
                if (Time.time >= nextDebris)
                {
                    nextDebris = Time.time + config.chargeDebrisInterval;
                    SpriteFx.BossDebris(transform.position, chargeDir, BodyWidth * 0.55f);   // 돌진하며 작은 파편이 튐
                }
                // 벽에 막혀 거리가 안 줄어도 일정 시간이 지나면 끝
                float length = cornerCharge ? Mathf.Min(cornerChargeLength, config.chargeDistance * 2f) : config.chargeDistance * config.chargeWallRideDistanceMul;   // 벽 타기 돌진 길이까지 여유
                float maxTime = length / Mathf.Max(0.1f, baseMonsterSpeed * config.chargeSpeedMul) + 0.3f;
                if (chargeLeft <= 0.01f || stateTime > maxTime)
                {
                    col.excludeLayers = 0;
                    state = State.Chase;
                    stateTime = 0f;
                    // 모서리 돌진은 이동일 뿐: 쿨타임 없이 바로 다음 돌진 판단 (보이면 짧은 예고 후 돌진)
                    chainCharge = cornerCharge;
                    nextAction = cornerCharge ? Time.time : Time.time + config.chargeInterval;
                    cornerCharge = false;
                    wallRide = false;
                }
                break;
        }
    }

    private readonly List<SpriteRenderer> chargeMarks = new List<SpriteRenderer>();

    // 예고 표시: 몸 재질을 쓰되 자르는 기준을 낮춰 흐린 빛 부분까지 진하게
    private void ApplyWarnMaterial(SpriteRenderer r)
    {
        if (config.bodyMaterial == null)
            return;
        r.sharedMaterial = config.bodyMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetFloat("_Cutoff", config.warnMarkCutoff);
        r.SetPropertyBlock(block);
    }

    private void ShowChargeLine(bool show)
    {
        if (show && chargeLine == null)
        {
            // chargeLine = 화살표들의 부모 (자체 그림 없음)
            GameObject go = new GameObject("Charge Line");
            chargeLine = go.AddComponent<SpriteRenderer>();
            chargeMarks.Clear();
        }
        if (chargeLine != null)
        {
            chargeLine.gameObject.SetActive(show);
        }
        if (show)
        {
            // 실제 돌진 범위(몸 앞부분이 닿는 곳까지)를 화살표 간격으로 채움
            int count = Mathf.Max(1, Mathf.RoundToInt(warnLength / Mathf.Max(0.2f, config.chargeWarnSpacing)) + 1);
            while (chargeMarks.Count < count)
            {
                SpriteRenderer mark = new GameObject("Arrow").AddComponent<SpriteRenderer>();
                mark.transform.SetParent(chargeLine.transform, false);
                mark.sortingLayerID = sr.sortingLayerID;
                mark.sortingOrder = sr.sortingOrder - 1;            // 몸 바로 아래
                ApplyWarnMaterial(mark);
                chargeMarks.Add(mark);
            }
            for (int i = 0; i < chargeMarks.Count; i++)
            {
                chargeMarks[i].gameObject.SetActive(i < count);
            }
            UpdateChargeLine();
        }
    }

    // 돌진 방향으로 몸 앞부분까지의 거리 (콜라이더 기준)
    private float BodyFront(Vector2 dir)
    {
        if (col == null) return BodyRadius;
        Vector3 e = col.bounds.extents;
        return Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y;
    }

    // 돌진 예고: 돌진 방향으로 겹화살표(>>)가 줄지어 깔리고, 앞쪽으로 차례차례 켜지는 물결
    private void UpdateChargeLine()
    {
        if (chargeLine == null)
            return;
        Vector2 start = transform.position;
        float angle = Mathf.Atan2(chargeDir.y, chargeDir.x) * Mathf.Rad2Deg;
        Sprite[] frames = config.chargeWarnFrames;
        bool hasFrames = frames != null && frames.Length > 0 && frames[0] != null;
        float k = Mathf.Clamp01(stateTime / Mathf.Max(0.05f, warnDuration));
        Color c = config.chargeWarnColor;
        int count = 0;
        while (count < chargeMarks.Count && chargeMarks[count].gameObject.activeSelf) count++;
        // 화살표: 몸 앞부분에서 시작해, 돌진이 끝났을 때 몸 앞부분이 닿는 곳까지 고르게
        float front = BodyFront(chargeDir);
        float half = config.chargeWarnWidth * 0.5f;
        float from = front + half;
        float to = Mathf.Max(from, warnLength + front - half);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer mark = chargeMarks[i];
            float along = count > 1 ? Mathf.Lerp(from, to, i / (float)(count - 1)) : (from + to) * 0.5f;
            mark.transform.position = start + chargeDir * along;
            mark.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            // 앞쪽 화살표일수록 늦게 켜짐 (뒤에서 앞으로 흐르는 물결)
            float wave = Mathf.Repeat(stateTime * 2.5f - i / (float)count, 1f);
            if (hasFrames)
            {
                Sprite s = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(wave * frames.Length))];
                mark.sprite = s;
                float size = Mathf.Max(0.01f, s.bounds.size.x);
                mark.transform.localScale = Vector3.one * (config.chargeWarnWidth / size);
            }
            else
            {
                mark.sprite = CombatUtil.Circle;
                mark.transform.localScale = Vector3.one * 0.4f;
            }
            float appear = Mathf.Clamp01(k * count * 1.5f - i);       // 예고 동안 뒤에서부터 하나씩 나타남
            mark.color = new Color(c.r, c.g, c.b, c.a * appear * (0.55f + 0.45f * (1f - wave)));
        }
    }

    // 갑각군주 소환 준비: 발밑 마법진 (Attack_Effect_2 넷째 줄)
    private SpriteRenderer summonCircle;

    private void ShowSummonCircle(bool show)
    {
        if (show && summonCircle == null && config.summonCircleFrames != null && config.summonCircleFrames.Length > 0)
        {
            summonCircle = new GameObject("Summon Circle").AddComponent<SpriteRenderer>();
            summonCircle.sortingLayerID = sr.sortingLayerID;
            summonCircle.sortingOrder = sr.sortingOrder - 1;
            ApplyWarnMaterial(summonCircle);
        }
        if (summonCircle != null)
        {
            summonCircle.gameObject.SetActive(show);
        }
    }

    private void UpdateSummonCircle()
    {
        if (summonCircle == null || !summonCircle.gameObject.activeSelf)
            return;
        Sprite[] frames = config.summonCircleFrames;
        float k = Mathf.Clamp01(stateTime / Mathf.Max(0.1f, config.summonCastTime));
        // 앞 프레임은 점만 흩어져 있어 잘 안 보임 → 절반부터 시작해 끝까지 채워짐
        Sprite s = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(Mathf.Lerp(frames.Length * 0.5f, frames.Length, k)))];
        summonCircle.sprite = s;
        float width = sr.sprite != null ? sr.sprite.bounds.size.x * transform.localScale.x : 2f;
        float size = Mathf.Max(0.01f, s != null ? s.bounds.size.x : 1f);
        summonCircle.transform.position = transform.position;
        float pulse = 1f + 0.06f * Mathf.Sin(stateTime * 14f);
        summonCircle.transform.localScale = new Vector3(1f, 0.55f, 1f) * (width * config.summonCircleSize / size * pulse);   // 바닥에 깔린 느낌으로 납작하게
        Color c = config.summonCircleColor;
        summonCircle.color = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(k * 8f));

        // 소환될 자리: 같은 마법진을 작게, 빠르게 깜빡이며
        for (int i = 0; i < spotCircles.Count && i < summonSpots.Count; i++)
        {
            SpriteRenderer r = spotCircles[i];
            r.sprite = s;
            r.transform.position = summonSpots[i];
            r.transform.localScale = new Vector3(1f, 0.6f, 1f) * (config.summonSpotSize / size) * (0.9f + 0.1f * Mathf.Sin(stateTime * 20f + i));
            r.color = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(k * 2f) * (0.7f + 0.3f * Mathf.Sin(stateTime * 18f + i)));
        }
    }

    // ---------------------------------------------------------------
    // 사슴벌레: 격려 (주변 일반 몬스터 최대 체력 증가) + 주기적으로 Hercudrome 소환
    // ---------------------------------------------------------------
    private void UpdateBeetle()
    {
        if (Time.time >= nextRally)
        {
            nextRally = Time.time + 0.5f;
            float r2 = config.rallyRadius * config.rallyRadius;
            foreach (Enemy enemy in FindObjectsOfType<Enemy>())
            {
                if ((enemy.transform.position - transform.position).sqrMagnitude <= r2)
                {
                    enemy.ApplyRally(config.rallyHpRate);
                }
            }
        }

        summoned.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy || !e.IsAlive);
        switch (state)
        {
            case State.Chase:
                // 보스전 진입 후에는 추가 소환 안 함
                if (Time.time >= nextAction && !GameManager.instance.isBossSpawn)
                {
                    state = State.Summon;
                    stateTime = 0f;
                    ShowSummonCircle(true);
                    PrepareSummonSpots();
                    SpriteFx.BossSlam(transform.position, BodyWidth * 1.3f);         // 땅을 찍으며 먼지구름
                    CameraShake.Play(config.patternShake * 0.7f, config.patternShakeTime);
                }
                break;
            case State.Summon:
                UpdateSummonCircle();
                if (stateTime >= config.summonCastTime)
                {
                    ShowSummonCircle(false);
                    MiniBossFx.AppearBurst(transform.position, 1.5f);
                    CameraShake.Play(config.patternShake, config.patternShakeTime);
                    Summon();
                    state = State.Chase;
                    stateTime = 0f;
                    nextAction = Time.time + config.summonInterval;
                }
                break;
        }
    }

    // 소환 준비: 소환될 자리를 정하고 그 자리에 작은 마법진 표시
    private readonly List<Vector2> summonSpots = new List<Vector2>();
    private readonly List<SpriteRenderer> spotCircles = new List<SpriteRenderer>();

    private void PrepareSummonSpots()
    {
        summonSpots.Clear();
        int count = Mathf.Min(config.summonCount, config.summonMax - summoned.Count);
        float radius = (sr.sprite != null ? sr.sprite.bounds.size.x * transform.localScale.x : 2f) * 0.8f + 0.8f;
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float a = (start + 360f * i / Mathf.Max(1, count) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            summonSpots.Add((Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
        while (spotCircles.Count < summonSpots.Count)
        {
            SpriteRenderer r = new GameObject("Summon Spot").AddComponent<SpriteRenderer>();
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder - 1;
            ApplyWarnMaterial(r);
            spotCircles.Add(r);
        }
        for (int i = 0; i < spotCircles.Count; i++)
        {
            spotCircles[i].gameObject.SetActive(i < summonSpots.Count);
        }
    }

    private void HideSummonSpots()
    {
        foreach (SpriteRenderer r in spotCircles)
        {
            if (r != null) r.gameObject.SetActive(false);
        }
    }

    private void Summon()
    {
        HideSummonSpots();
        if (herculesType < 0)
        {
            GameObject prefab = MonsterPrefab();
            Enemy enemyPrefab = prefab != null ? prefab.GetComponent<Enemy>() : null;
            if (enemyPrefab != null)
            {
                for (int i = 0; i < enemyPrefab.animCon.Length; i++)
                {
                    if (enemyPrefab.animCon[i] != null && enemyPrefab.animCon[i].name.Contains("Hercudrome"))
                    {
                        herculesType = i;
                        break;
                    }
                }
            }
            if (herculesType < 0)
                return;
        }

        int index = GameManager.instance.pool.prefabs.Length > 0 ? 0 : -1;
        for (int i = 0; i < summonSpots.Count && summoned.Count < config.summonMax; i++)
        {
            GameObject go = GameManager.instance.pool.Get(index);
            go.transform.position = summonSpots[i];
            Enemy enemy = go.GetComponent<Enemy>();
            SpawnData data = new SpawnData
            {
                spriteType = herculesType,
                health = Mathf.Max(1, Mathf.RoundToInt(baseMonsterHealth * config.summonHealthMul)),
                speed = baseMonsterSpeed,
            };
            enemy.Init(data, 1);
            enemy.NoReward = true;                              // 소환된 몬스터는 경험치·코인 없음
            summoned.Add(enemy);
            MiniBossFx.AppearBurst(go.transform.position, 1f);           // 소환된 자리에 흙먼지·불씨
            SpriteFx.SummonSparkle(go.transform.position);              // 소환될 때 반짝임
        }
    }

    // ---------------------------------------------------------------
    // 피해 / 사망
    // ---------------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsLive || !collision.CompareTag("Bullet"))
            return;
        Bullet bullet = collision.GetComponent<Bullet>();
        if (bullet == null || bullet.IsHarmless || !bullet.TryHitTarget(gameObject))
            return;
        float damage = GimmickCards.ModifyHit(bullet.HitDamage * bullet.VolleyScale(gameObject), !bullet.SubWeapon, GimmickCards.TargetKind.MiniBoss, bullet);   // 전투 기믹
        TakeDamage(damage, bullet.ResolveKnockChance());
        GimmickCards.AfterHit(transform.position, damage, !IsLive);
        bullet.RegisterHit(gameObject);
        bullet.NotifyHit(gameObject);
    }

    private float nextKnockTime;

    // knockChance: 넉백 확률 (중간보스는 x miniBossKnockbackMul, 0 이하면 넉백 없음)
    public void TakeDamage(float damage, float knockChance)
    {
        GimmickCatalog c = GimmickCatalog.Instance;
        bool knock = knockChance > 0f && Time.time >= nextKnockTime && Random.value < knockChance * c.miniBossKnockbackMul;
        if (knock) nextKnockTime = Time.time + c.knockbackCooldown;
        TakeDamage(damage, knock);
    }

    public void TakeDamage(float damage, bool knockback = false)
    {
        if (!IsLive || damage <= 0f)
            return;

        Health -= damage;
        DamageNumber.Add(sr, damage);
        SpriteFx.HitSpark(transform.position + Vector3.up * sr.bounds.extents.y, 1.3f);
        hitUntil = Time.time + config.hitFlashTime;

        if (knockback && Data.knockbackResist < 1f && Data.kind != MiniBossConfig.Kind.Wasp)
        {
            Vector2 away = (transform.position - player.position).normalized;
            knockVelocity += away * 6f * (1f + ExtraCards.KnockbackBonus) * (1f - Data.knockbackResist) * config.knockbackTaken;
        }

        if (Health <= 0f)
        {
            Die();
        }
        else
        {
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Enemy_Hit);
        }
    }

    // 말벌: 붙어 있는 동안 일정 간격으로 피해
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsLive || Data.kind != MiniBossConfig.Kind.Wasp)
            return;
        Player p = collision.gameObject.GetComponent<Player>();
        if (p == null)
            return;
        GameManager gm = GameManager.instance;
        if (!gm.isLive || Time.time < nextWaspHit)
            return;
        nextWaspHit = Time.time + config.waspHitInterval;
        if (p.IsInvincible || GimmickCards.TryShield())          // 보호막이면 이번 1회는 막음
            return;
        gm.health -= config.contactDps * Data.attackMul * config.waspHitInterval * MainWeaponController.DamageTakenScale;
    }

    private void Die()
    {
        IsLive = false;
        GimmickCards.OnEnemyKilled();                        // 생명의 일격 처치 수
        deathTime = Time.time;
        col.enabled = false;
        rb.simulated = false;
        ShowChargeLine(false);
        Alive.Remove(this);
        SpriteFx.BossDeathDust(new Vector3(transform.position.x, sr.bounds.min.y, 0f), sr.bounds.size.x * 1.1f);   // 발밑 흙먼지 한 번
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Enemy_Dead);
        GameManager.instance.kill++;
        Group?.OnMemberDied(this);
    }
}
