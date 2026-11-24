using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animCon;
    public Rigidbody2D target;
    public float expSpawnRange;                         // 경험치 스폰 범위
    public int expSpawnIndex;                           // 경험치구슬 드랍 갯수

    bool isLive;

    // 살아 있는지 (중간보스 소환 수 세기 등)
    public bool IsAlive => isLive;

    // 사슴벌레가 소환한 몬스터: 경험치·코인 없음
    public bool NoReward { get; set; }

    // 사슴벌레 격려: 최대 체력 증가 (한 번만) + 머리 위 표시
    private bool rallied;
    private GameObject rallyFx;
    private SpriteFxAnimator eliteAura;
    public static bool SliceKill;                         // 대검 각성 「천지 가르기」: 쓰러진 몬스터는 잘려서 사라짐 (숫자는 실제 피해)
    public static float SliceDuration = 0.9f;             // 잘린 몸이 사라지는 시간

    public void ApplyRally(float rate)
    {
        if (!isLive || rallied)
            return;
        rallied = true;
        float ratio = maxHealth > 0f ? health / maxHealth : 1f;
        maxHealth *= 1f + rate;
        health = maxHealth * ratio;
        rallyFx = SpriteFx.RallyMark(transform);
    }

    Rigidbody2D rigid;
    Collider2D coll;
    Animator anim;
    SpriteRenderer spriter;
    WaitForFixedUpdate wait;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
        wait = new WaitForFixedUpdate();
    }

    private Vector2 steerCheckPos;                       // 텐트 울타리 우회 중 멈춤 판정
    private float steerCheckTime;
    private float steerAltUntil;                         // 이 시각까지 반대쪽 모서리로 우회

    private void FixedUpdate()
    {
        if (!GameManager.instance.isLive)
            return;

        if (!isLive)
            return;

        if (NightWave && !hiding)
            NightContact();                                  // 밤 웨이브: 밀지 않고 겹침으로 접촉 피해

        // 마비 두꺼비: 마비된 동안 완전히 정지 (위치를 고정해 밀리거나 넉백으로 미끄러지지 않음)
        if (Time.time < stunEndTime)
        {
            rigid.velocity = Vector2.zero;
            rigid.constraints = RigidbodyConstraints2D.FreezeAll;
            return;
        }
        if (isStunTinted)
        {
            isStunTinted = false;
            spriter.color = colorBeforeStun;
            rigid.constraints = constraintsBeforeStun;
        }

        if (hiding)                                          // 땅속으로 사라지는 중 (낮 몬스터 은신 보관)
        {
            rigid.velocity = Vector2.zero;
            return;
        }

        // 밤 웨이브: 플레이어를 쫓지 않고 경로를 따라 입구로만 (길 밖으로 나가지 않음)
        if (NightWave)
        {
            NightMove();
            return;
        }

        // 넉백이 끝나면 미끄러짐을 멈춤 (피격 모션 중에도 계속 미끄러지지 않게)
        if (pushing && Time.time >= pushedUntil)
        {
            pushing = false;
            rigid.velocity = Vector2.zero;
        }

        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Hit") || Time.time < pushedUntil)
            return;

        if (ColonyGuard && guardIdle)
        {
            GuardIdleMove();                                 // 군락 호위: 둥지 근처에서 대기
            return;
        }

        Vector2 goal = Decoy.GetEnemyTarget(target.position);
        Vector2 waypoint = CampZone.Steer(rigid.position, goal, Time.time < steerAltUntil);   // 텐트 울타리는 돌아서
        if (waypoint != goal)
        {
            // 돌아가는 길이 다른 지형에 막혀 멈추면 반대쪽 모서리로 돌아감
            if ((rigid.position - steerCheckPos).sqrMagnitude > 0.09f)
            {
                steerCheckPos = rigid.position;
                steerCheckTime = Time.time;
            }
            else if (Time.time - steerCheckTime > 0.7f)
            {
                steerAltUntil = Time.time < steerAltUntil ? 0f : Time.time + 3f;
                steerCheckTime = Time.time;
            }
        }
        Vector2 dirVec = waypoint - rigid.position;
        if (StealthBlind())
        {
            dirVec = stealthDir;                             // 은신: 은신 시작 때 향하던 방향으로 그대로 (닿으면 피해는 그대로)
        }
        Vector2 nextVec = dirVec.normalized * speed * GimmickCards.EnemySpeedScale(this) * Time.fixedDeltaTime;   // 냉기 발자국
        // 텐트 울타리 벽에 몸을 밀어 넣지 않고 벽을 따라 미끄러짐 (모서리·문 앞에서 끼어 꿈틀거리지 않게)
        Vector2 next = CampZone.SlideAround(rigid.position, nextVec, CampBodyRadius());
        rigid.MovePosition(next);
        rigid.velocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!GameManager.instance.isLive)
            return;

        if (!isLive)
            return;

        if (hiding)
            return;

        UpdateTurretBurn();                                  // 화룡포 화상
        UpdatePenaltyRecover();                                // 캠프 처벌 해제 후 강화가 서서히 풀림

        if (NightWave && nightPath != null)
            spriter.flipX = nightPath.DirectionAt(pathDist).x < -0.01f;   // 밤 웨이브: 진행 방향을 봄
        else
            spriter.flipX = ColonyGuard && guardIdle ? guardWander.x < rigid.position.x
                : StealthBlind() ? stealthDir.x < 0f : Decoy.GetEnemyTarget(target.position).x < rigid.position.x;
    }

    void OnEnable()
    {
        target = GameManager.instance.player.GetComponent<Rigidbody2D>();
        isLive = true;
        NoReward = false;
        rallied = false;
        if (rallyFx != null)
        {
            Destroy(rallyFx);
        }
        stunEndTime = 0f;
        if (isStunTinted) rigid.constraints = constraintsBeforeStun;   // 마비 중 쓰러진 몬스터가 다시 나올 때 원래대로
        isStunTinted = false;
        coll.enabled = true;
        rigid.simulated = true;
        rigid.bodyType = RigidbodyType2D.Dynamic;             // 밤 웨이브에서 바꾼 물리를 원래대로
        rigid.useFullKinematicContacts = false;
        rigid.interpolation = RigidbodyInterpolation2D.None;
        spriter.sortingOrder = 2;
        anim.SetBool("Dead", false);
        health = maxHealth;

        // 보스가 있는 동안 스폰된 잡몹도 보스와 충돌하지 않도록 설정
        if (BossPattern.Current != null)
        {
            BossPattern.Current.IgnoreCollisionWith(coll);
        }
    }

    private float stunEndTime;
    private int stealthVersion = -1;
    private Vector2 stealthDir;

    // 헤비보우건 은신 중이면 플레이어를 쫓지 않음 (미끼가 있으면 미끼를 따라감)
    private bool StealthBlind()
    {
        if (!HeavyStealth.Active || target == null)
            return false;
        Vector2 aim = Decoy.GetEnemyTarget(target.position);
        if (aim != target.position)
            return false;
        if (stealthVersion != HeavyStealth.Version)
        {
            stealthVersion = HeavyStealth.Version;
            stealthDir = (aim - rigid.position).normalized;
        }
        return true;
    }
    private bool isStunTinted;
    private Color colorBeforeStun = Color.white;
    private RigidbodyConstraints2D constraintsBeforeStun = RigidbodyConstraints2D.FreezeRotation;

    // seconds초 동안 이동 불가
    public void Stun(float seconds)
    {
        if (!isLive || seconds <= 0f)
            return;

        stunEndTime = Mathf.Max(stunEndTime, Time.time + seconds);
        if (!isStunTinted)
        {
            colorBeforeStun = spriter.color;
            constraintsBeforeStun = rigid.constraints;
            isStunTinted = true;
        }
        rigid.velocity = Vector2.zero;
        rigid.constraints = RigidbodyConstraints2D.FreezeAll;      // 즉시 정지
        spriter.color = RunConfig.Instance.stunnedTint;
    }

    // 플레이어에게 닿았을 때 주는 피해 배율 (밤 강화)
    public float ContactDamageScale { get; private set; } = 1f;

    // 체력 강화 몬스터(엘리트)인지
    public bool IsElite => expSpawnIndex > 1;

    // 울타리 미끄러짐용 몸 반지름: 위치(발밑 기준점)에서 충돌체 끝까지 가장 먼 거리
    private float CampBodyRadius()
    {
        Bounds b = coll.bounds;
        Vector2 p = rigid.position;
        float x = Mathf.Max(Mathf.Abs(b.max.x - p.x), Mathf.Abs(p.x - b.min.x));
        float y = Mathf.Max(Mathf.Abs(b.max.y - p.y), Mathf.Abs(p.y - b.min.y));
        return Mathf.Max(x, y) + RunConfig.Instance.campSlidePadding;
    }

    public int PenaltyStack { get; private set; }               // 캠프 비비기 처벌 스택 (소환될 때 고정)

    // 캠프 비비기 처벌: 버틴 스택만큼 최대 체력 증가, 몸이 붉어짐 (Init 직후, 처벌 중 소환된 몬스터만)
    public void ApplyPenaltyStack(int stack)
    {
        RunConfig rc = RunConfig.Instance;
        PenaltyStack = Mathf.Clamp(stack, 0, rc.penaltyMaxStack);
        if (PenaltyStack <= 0)
            return;
        penaltyHealthMul = 1f + rc.penaltyHealthPerStack * PenaltyStack;
        maxHealth *= penaltyHealthMul;
        health = maxHealth;
        float k = PenaltyStack / (float)Mathf.Max(1, rc.penaltyMaxStack);
        penaltyTint = Color.Lerp(Color.white, rc.penaltyStackTint, k);
        spriter.color *= penaltyTint;                          // 엘리트·밤 색 위에 곱해서 진하게
    }

    // 처벌 스택 강화가 현재 걸려 있는 정도 (체력 배율, 몸 색 배율)
    private float penaltyHealthMul = 1f;
    private Color penaltyTint = Color.white;
    private float recoverStartMul = 1f;
    private Color recoverStartTint = Color.white;
    private float recoverTime = -1f;                           // 처벌 해제 후 원래대로 돌아가는 중 (경과 시간, -1이면 아님)
    private float recoverDuration;

    // 처벌이 해제되면: 추가 체력·붉은 색이 duration초에 걸쳐 원래대로 (남은 체력 비율은 유지)
    public void BeginPenaltyRecover(float duration)
    {
        if (PenaltyStack <= 0 || recoverTime >= 0f)
            return;
        recoverStartMul = penaltyHealthMul;
        recoverStartTint = penaltyTint;
        recoverDuration = Mathf.Max(0.01f, duration);
        recoverTime = 0f;
    }

    private void UpdatePenaltyRecover()
    {
        if (recoverTime < 0f)
            return;
        recoverTime += Time.deltaTime;
        float k = Mathf.Clamp01(recoverTime / recoverDuration);
        float mul = Mathf.Lerp(recoverStartMul, 1f, k);
        Color tint = Color.Lerp(recoverStartTint, Color.white, k);

        // 다른 효과로 바뀐 체력은 그대로 두고 처벌 배율만 바꿈
        float scale = mul / Mathf.Max(0.01f, penaltyHealthMul);
        maxHealth *= scale;
        health *= scale;
        penaltyHealthMul = mul;

        // 몸 색도 처벌 색만 걷어 냄 (마비 중이면 마비가 풀린 뒤 돌아갈 색에 반영)
        Color ratio = new Color(tint.r / Mathf.Max(0.01f, penaltyTint.r), tint.g / Mathf.Max(0.01f, penaltyTint.g), tint.b / Mathf.Max(0.01f, penaltyTint.b), 1f);
        if (isStunTinted) colorBeforeStun *= ratio;
        else spriter.color *= ratio;
        penaltyTint = tint;

        if (k >= 1f)
        {
            recoverTime = -1f;
            PenaltyStack = 0;
        }
    }

    public void Init(SpawnData data, int index)
    {
        IgnorePlayer(false);
        NightWave = false;
        nightPath = null;
        anim.speed = 1f;
        turretSlowUntil = 0f;
        burnUntil = 0f;
        burnAccum = 0f;
        PenaltyStack = 0;
        penaltyHealthMul = 1f;
        penaltyTint = Color.white;
        recoverTime = -1f;
        anim.runtimeAnimatorController = animCon[data.spriteType];
        spriteType = data.spriteType;
        BossUnit = false;
        if (!baseScaleSet) { baseScale = transform.localScale; baseScaleSet = true; }
        transform.localScale = baseScale;                    // 대장·행진 크기를 원래대로
        ClearGuard();                                        // 군락 호위 해제 (풀 재사용)
        hiding = false;
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;
        expSpawnIndex = index;

        // 밤에 나온 몬스터는 체력, 공격력, 이동속도 강화 (보스는 Enemy가 아니라 영향 없음)
        float buff = DayNightCycle.MonsterBuff;
        ContactDamageScale = 1f + buff;
        if (buff > 0f)
        {
            maxHealth *= 1f + buff;
            health = maxHealth;
            speed *= 1f + buff * RunConfig.Instance.nightSpeedRatio;
            if (spriter.color == Color.white)
            {
                spriter.color = RunConfig.Instance.nightMonsterTint;   // 강화된 몬스터 표시 (엘리트 색은 유지)
            }
        }

        // 아침 선택지: 모든 적 체력 배율 (필살기 봉인), 엘리트 체력·공격력 배율 (엘리트 토벌, 엘리트의 행진)
        maxHealth *= RunStats.Get(RunStat.EnemyHealth);
        if (IsElite)
        {
            maxHealth *= RunStats.Get(RunStat.EliteHealth);
            ContactDamageScale *= RunStats.Get(RunStat.EliteDamage);
        }
        health = maxHealth;

        // 강화(엘리트) 몬스터: 발밑에 붉은 금빛 오라가 돎
        if (IsElite)
        {
            transform.localScale = baseScale * RunStats.Get(RunStat.EliteScale);   // 엘리트의 행진: 낮 엘리트 크기
            spriter.color = RunConfig.Instance.eliteTint;                 // 몸도 약간 붉게
            if (eliteAura == null)
                eliteAura = SpriteFx.EliteAura(transform, RunConfig.Instance.eliteAuraColor);
        }
        else if (!IsElite && eliteAura != null)
        {
            SpriteFx.Stop(eliteAura);
            eliteAura = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Bullet") || !isLive || collision.GetComponent<Bullet>().IsHarmless)
            return;

        Bullet bullet = collision.GetComponent<Bullet>();
        if (!bullet.TryHitTarget(gameObject))
            return;                                          // 같은 적 재타격 간격 안
        float damage = GimmickCards.ModifyHit(bullet.HitDamage * bullet.VolleyScale(gameObject), !bullet.SubWeapon, HuntKind, bullet);   // 전투 기믹
        TakeDamage(damage, bullet.ResolveKnockChance());
        GimmickCards.AfterHit(transform.position, damage, !isLive);
        bullet.RegisterHit(gameObject);                      // 한 방에 죽어도 관통 횟수가 줄도록
        bullet.NotifyHit(gameObject);
    }

    // 위기 탈출 밀쳐내기 (잠깐 이동을 멈추고 밀려남)
    private float pushedUntil;

    public void Push(Vector2 direction, float force)
    {
        if (!isLive)
            return;
        if (BossUnit)
            return;                                          // 대장 몬스터: 밀쳐내기도 면역
        if (NightWave)
        {
            GimmickCatalog gc = GimmickCatalog.Instance;
            PathKnockback(gc.knockbackBaseDist * RunConfig.Instance.nightKnockbackMul, 0.25f);
            return;
        }
        pushedUntil = Time.time + 0.25f;
        pushing = true;
        rigid.velocity = Vector2.zero;
        rigid.AddForce(direction * force, ForceMode2D.Impulse);
    }

    // 넉백: 공격별 확률로 발동, 같은 몬스터는 knockbackCooldown 안에 다시 밀리지 않음
    private float nextKnockTime;
    private bool pushing;

    private void TryKnockback(float chance)
    {
        GimmickCatalog c = GimmickCatalog.Instance;
        if (chance <= 0f || !isLive || Time.time < nextKnockTime || Random.value >= chance)
            return;
        if (BossUnit)
            return;                                          // 대장 몬스터: 넉백 면역
        nextKnockTime = Time.time + c.knockbackCooldown;
        Vector2 away = ((Vector2)(transform.position - GameManager.instance.player.transform.position)).normalized;
        float distance = c.knockbackBaseDist * (1f + ExtraCards.KnockbackBonus);   // 밀어내기 카드 단계만큼 멀리
        float time = Mathf.Max(0.01f, c.knockbackPushTime);
        if (NightWave)
        {
            PathKnockback(distance * RunConfig.Instance.nightKnockbackMul, time);
            return;
        }
        pushedUntil = Time.time + time;
        pushing = true;
        rigid.velocity = away * distance / time;
    }

    // 피해 처리 (발사체 충돌, 화살비처럼 범위 안에 있는 동안 반복해서 주는 피해 공용)
    // knockChance: 넉백 확률 (0 이하면 넉백 없음)
    public void TakeDamage(float hitDamage, float knockChance = 0f)
    {
        if (!isLive)
            return;
        if (ColonyGuard && guardIdle && GuardGroup != null) GuardGroup.Alert();   // 호위가 공격받으면 그 무리 전체 추적

        bool slice = SliceKill && hitDamage >= health;      // 화면 가르기로 쓰러짐
        health -= hitDamage;
        if (slice)
        {
            SliceFx.Play(spriter, SliceDuration);             // 몸이 두 동강 나며 사라짐
            DamageNumber.Add(spriter, hitDamage);             // 실제 피해
            spriter.enabled = false;
        }
        else
        {
            DamageNumber.Add(spriter, hitDamage);
        }
        SpriteFx.HitSpark(transform.position);                 // 맞은 자리에 작은 별
        if (health > 0) TryKnockback(knockChance);

        if (health > 0)
        {
            anim.SetTrigger("Hit");
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Enemy_Hit);
        }
        else
        {
            isLive = false;
            coll.enabled = false;
            rigid.simulated = false;
            spriter.sortingOrder = 1;
            if (guardMark != null) guardMark.gameObject.SetActive(false);
            anim.SetBool("Dead", true);
            SpriteFx.EnemyPop(transform.position);                // 처치 흙먼지 + 별
            GameManager.instance.kill++;
            if (!turretHit) GimmickCards.OnEnemyKilled();         // 생명의 일격 처치 수 (포탑 처치는 제외)

            if (rallyFx != null)
            {
                Destroy(rallyFx);
            }
            if (eliteAura != null)
            {
                SpriteFx.Stop(eliteAura);
                eliteAura = null;
            }

            // 상점 강화용 코인 드롭 (밤에 더 많이, 엘리트는 추가). 소환된 몬스터는 없음
            if (GameManager.instance.isLive && !NoReward)
            {
                if (NightWave) NightDefense.DropCoins(transform.position);   // 밤 웨이브: 냥냥코인 100%
                else CoinWallet.OnEnemyKilled(transform.position, IsElite);
            }
            if (NightWave)
            {
                NightDefense.Remove(this);
                anim.speed = 1f;
            }

            // 몬스터가 Exp구슬을 드랍하므로 직접 경험치가 증가되는 코드 주석처리
            //GameManager.instance.GetExp();
            
            if (NoReward)
            {
                // 소환된 몬스터: 경험치 없음
            }
            else if (expSpawnIndex > 1)
            {
                // 강화몬스터인 경우
                SpawnExp(expSpawnIndex);
            }
            else
            {
                // 일반 몬스터인 경우
                Transform exp = GameManager.instance.pool.GetEnemy(0).transform;
                exp.parent = GameManager.instance.pool.transform;
                exp.position = CampZone.FreeSpot(transform.position, 0.3f);   // 텐트·동굴 등에 끼지 않게
            }

            if (GameManager.instance.isLive)
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Enemy_Dead);
        }
    }

    private IEnumerator KnockBack()
    {
        yield return wait;
        Vector3 playerPos = GameManager.instance.player.transform.position;
        Vector3 dirVec = transform.position - playerPos;
        rigid.AddForce(dirVec.normalized * 2 * (1f + ExtraCards.KnockbackBonus), ForceMode2D.Impulse);   // 밀어내기 카드만큼 넉백 증가
    }

    private void Dead()
    {
        spriter.enabled = true;
        // 빨간색의 강화몬스터의 색상을 초기화하기 위한 코드
        gameObject.GetComponent<SpriteRenderer>().color = Color.white;

        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    // 밤 웨이브 (NightDefense)
    // ---------------------------------------------------------------
    public bool NightWave { get; private set; }
    private WavePath nightPath;
    private float pathDist;                                    // 경로 진행도 (시작점에서 간 거리)
    private float nightSpeed;
    private float dashAt = -1f, dashSpeed, dashLeft;             // 돌격형: 경로 이 위치에서 dashLeft초 동안 dashSpeed로 돌진
    public int NightSpeedType { get; private set; }              // 0 일반 / 1 돌격 / 2 굼벵이 / -1 대장
    public bool Dashing => dashAt < 0f && dashLeft > 0f;
    private float laneOffset;                                  // 길 폭 안에서 옆으로 벗어난 정도
    private float knockFrom, knockTo, knockStart, knockDuration;
    public float PathProgress => pathDist;

    // 경로 시작점에서 출발. 체력·공격 +buffRate (이동속도는 경로 길이 / 도착 시간으로 고정, 걷기 애니메이션도 비례)
    // ---------------------------------------------------------------
    // 포탑 (TurretSystem): 넉백·기믹 없이 피해, 경로 감속, 화상
    // ---------------------------------------------------------------
    private bool turretHit;
    private float turretSlowRate, turretSlowUntil;
    private float burnDps, burnUntil, burnAccum;

    // 포탑 피해: 넉백 없음, 플레이어 기믹(연쇄 번개·처치 폭발·생명의 일격) 연동 없음, 처치 보상은 평소대로
    public void TakeTurretDamage(float damage)
    {
        if (!isLive || damage <= 0f)
            return;
        turretHit = true;
        TakeDamage(damage, 0f);
        turretHit = false;
    }

    // 감속 장판: 중첩 없이 가장 강한 감속 하나 (밤 몬스터 경로 이동에만 적용)
    public void ApplyTurretSlow(float rate, float duration)
    {
        if (Time.time >= turretSlowUntil || rate >= turretSlowRate)
            turretSlowRate = rate;
        turretSlowUntil = Mathf.Max(turretSlowUntil, Time.time + duration);
    }

    private float TurretSlowScale => Time.time < turretSlowUntil ? 1f - Mathf.Clamp01(turretSlowRate) : 1f;

    // 화상: 다시 맞으면 시간만 갱신 (중첩 없음)
    public void ApplyTurretBurn(float dps, float duration)
    {
        burnDps = dps;
        burnUntil = Time.time + duration;
    }

    private void UpdateTurretBurn()
    {
        if (burnUntil <= 0f)
            return;
        if (Time.time >= burnUntil)
        {
            burnUntil = 0f;
            burnAccum = 0f;
            return;
        }
        burnAccum += burnDps * Time.deltaTime;
        if (burnAccum >= burnDps * 0.5f)                     // 0.5초마다 한 번씩 (피해 숫자가 너무 잦지 않게)
        {
            float d = burnAccum;
            burnAccum = 0f;
            TakeTurretDamage(d);
        }
    }

    // 대장 몬스터 (밤 웨이브): 넉백 면역, 감속은 적용, 낮 엘리트가 아님(IsElite 아님)
    public bool BossUnit { get; private set; }
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleSet;

    // 사냥 본능 피해 계산용 종류: 엘리트 또는 대장 (엘리트 보상·배율·크기 판정은 IsElite만 씀)
    public GimmickCards.TargetKind HuntKind => IsElite || BossUnit ? GimmickCards.TargetKind.Elite : GimmickCards.TargetKind.Mob;

    public void MakeBossUnit(float scale, Color tint, Color auraColor)
    {
        BossUnit = true;
        transform.localScale = baseScale * scale;
        spriter.sortingOrder = RunConfig.Instance.bossUnitSortingOrder;   // 일반 몬스터(2)와 같은 순서면 겹칠 때 번갈아 그려져 깜빡임 (Init에서 2로 되돌림)
        spriter.color = tint;
        if (eliteAura != null) SpriteFx.Stop(eliteAura);
        eliteAura = SpriteFx.EliteAura(transform, auraColor);   // 발밑 보라 오라
    }

    // 최종 체력 (밤 체력 표 x (1 + 증가 합), 대장은 x bossUnitHpMul)으로 출발. 접촉 피해는 낮과 같음
    public int SpawnSerial { get; private set; }               // 밤 웨이브로 꺼낼 때마다 증가 (포탑 화살이 다시 소환된 몬스터를 쫓지 않게)

    public void BeginNightWave(WavePath path, float speedPerSec, float finalHealth, float lane)
    {
        SpawnSerial++;
        NightWave = true;
        dashAt = -1f; dashLeft = 0f; NightSpeedType = 0;
        IgnorePlayer(true);                                  // 경로로 강제 이동하므로 플레이어를 밀지 않게 물리 충돌 끔
        nightPath = path;
        pathDist = 0f;
        knockDuration = 0f;
        laneOffset = lane;
        float tableSpeed = Mathf.Max(0.01f, speed);
        nightSpeed = speedPerSec;
        speed = speedPerSec;
        anim.speed = Mathf.Clamp(nightSpeed / tableSpeed, 0.5f, 4f) * RunConfig.Instance.nightWalkAnimMul;   // 걷기 애니만 느리게 (이동 속도는 그대로)
        // 경로로만 움직이므로 물리로 서로 밀지 않게 (키네마틱: 겹쳐도 밀리지 않음, 적중 판정은 그대로 받음)
        rigid.velocity = Vector2.zero;
        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.useFullKinematicContacts = true;
        rigid.interpolation = RigidbodyInterpolation2D.Interpolate;   // 화면에서 부드럽게
        maxHealth = finalHealth * RunStats.Get(RunStat.EnemyHealth);
        health = maxHealth;
        ContactDamageScale = 1f;
        Vector2 start = path.LanePositionAt(0f, laneOffset);
        transform.position = start;
        rigid.position = start;
    }

    // 플레이어와의 물리 충돌 끄기/켜기 (밤 웨이브 동안만 끔)
    private void IgnorePlayer(bool ignore)
    {
        Player p = GameManager.instance != null ? GameManager.instance.player : null;
        Collider2D pc = p != null ? p.GetComponent<Collider2D>() : null;
        if (pc != null && coll != null) Physics2D.IgnoreCollision(coll, pc, ignore);
    }

    // 충돌을 끈 대신 겹쳐 있으면 평소와 같은 접촉 피해 (Player.ContactHit)
    private void NightContact()
    {
        Player p = GameManager.instance.player;
        Collider2D pc = p != null ? p.GetComponent<Collider2D>() : null;
        if (pc == null || coll == null || !coll.enabled || !pc.enabled)
            return;
        if (Physics2D.Distance(coll, pc).isOverlapped)
            p.ContactHit(gameObject);
    }

    private void NightMove()
    {
        if (nightPath == null)
            return;
        if (knockDuration > 0f)
        {
            float k = Mathf.Clamp01((Time.time - knockStart) / knockDuration);
            pathDist = Mathf.Lerp(knockFrom, knockTo, 1f - (1f - k) * (1f - k));
            if (k >= 1f) knockDuration = 0f;
        }
        else if ((BossUnit && RunConfig.Instance.bossIgnoreHitStall) || !anim.GetCurrentAnimatorStateInfo(0).IsName("Hit"))   // 대장은 맞아도 계속 이동
        {
            float v = nightSpeed;
            if (dashAt >= 0f && pathDist >= dashAt) { dashAt = -1f; }   // 돌격 시작 지점 도착 → 돌진 (예고 없음)
            if (dashAt < 0f && dashLeft > 0f) { v = dashSpeed; dashLeft -= Time.fixedDeltaTime; }
            pathDist += v * Mathf.Min(GimmickCards.EnemySpeedScale(this), TurretSlowScale) * Time.fixedDeltaTime;   // 냉기 발자국·끈적이 장판 중 가장 강한 감속 하나만 (중첩 없음)
        }
        pathDist = Mathf.Max(0f, pathDist);                       // 경로 시작점보다 뒤로는 가지 않음
        if (pathDist >= nightPath.Length)
        {
            NightDefense.ReachGate(this);
            return;
        }
        Vector2 pos = nightPath.LanePositionAt(pathDist, laneOffset);   // 꺾이는 곳에서도 옆줄 위치가 끊기지 않음
        rigid.MovePosition(pos);
        rigid.velocity = Vector2.zero;
    }

    // 밤 이동 속도 종류 (NightDefense가 스폰할 때): 걷는 속도, 돌격이면 돌진 시작 위치·속도·시간
    public void SetNightSpeedType(int type, float walkSpeed, float dashStart, float dashSpd, float dashTime)
    {
        NightSpeedType = type;
        nightSpeed = walkSpeed;
        speed = walkSpeed;
        if (type == 1 && dashTime > 0f)
        {
            dashAt = Mathf.Max(0.001f, dashStart);
            dashSpeed = dashSpd;
            dashLeft = dashTime;
        }
    }

    // 경로를 거슬러 distance만큼 뒤로 (길 밖으로 밀지 않음)
    private void PathKnockback(float distance, float time)
    {
        knockFrom = pathDist;
        knockTo = Mathf.Max(0f, pathDist - distance);
        knockStart = Time.time;
        knockDuration = Mathf.Max(RunConfig.Instance.nightKnockbackMinTime, time);   // 너무 짧으면 순간이동처럼 보임
    }

    // 입구 문에 닿음: 스르르 투명해진 뒤 사라짐 (보상 없음, 그동안 움직임·피격·포탑 조준 없음)
    public void FadeVanish(float duration)
    {
        if (!isLive || hiding)
            return;
        hiding = true;
        coll.enabled = false;
        rigid.velocity = Vector2.zero;
        NightDefense.Remove(this);
        StartCoroutine(FadeVanishRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeVanishRoutine(float duration)
    {
        Color c = spriter.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            spriter.color = new Color(c.r, c.g, c.b, c.a * (1f - Mathf.Clamp01(t / duration)));
            yield return null;
        }
        Vanish();
    }

    // 보상 없이 바로 사라짐 (입구 도달, 밤 종료)
    public void Vanish()
    {
        if (!gameObject.activeSelf)
            return;
        isLive = false;
        NightDefense.Remove(this);
        ClearGuard();
        IgnorePlayer(false);
        NightWave = false;
        nightPath = null;
        anim.speed = 1f;
        if (rallyFx != null) Destroy(rallyFx);
        if (eliteAura != null)
        {
            SpriteFx.Stop(eliteAura);
            eliteAura = null;
        }
        spriter.enabled = true;
        spriter.color = Color.white;
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    // 낮 몬스터 은신 보관 (DayMonsterStash)
    // 풀 오브젝트는 밤 웨이브가 다시 꺼내 쓰므로 상태를 기록으로 저장하고, 다음 날 새로 꺼내 되돌림
    // ---------------------------------------------------------------
    public bool ColonyGuard { get; set; }                     // 군락 호위 몬스터 (은신 보관 제외)

    // ---------------------------------------------------------------
    // 군락 호위 (ColonySystem): 플레이어가 다가오거나 무리가 공격받기 전까지 둥지 근처에서 서성임, 머리 위 붉은 표시
    // ---------------------------------------------------------------
    public ColonySystem.Group GuardGroup { get; private set; }
    public bool GuardIdle => ColonyGuard && guardIdle;
    private bool guardIdle;
    private Vector2 guardWander;
    private float guardWanderAt;
    private SpriteRenderer guardMark;

    private Vector2 guardSpot;                                // 둥지 기준 자기 자리

    public void MakeColonyGuard(ColonySystem.Group group, Vector2 spot)
    {
        guardSpot = spot;
        RunConfig rc = RunConfig.Instance;
        ColonyGuard = true;
        GuardGroup = group;
        guardIdle = true;
        guardWander = rigid.position;
        guardWanderAt = 0f;
        if (rc.colonyGuardMark == null)
            return;
        if (guardMark == null)
        {
            GameObject go = new GameObject("Colony Guard Mark");
            go.transform.SetParent(transform, false);
            guardMark = go.AddComponent<SpriteRenderer>();
        }
        guardMark.sprite = rc.colonyGuardMark;
        guardMark.color = rc.colonyGuardMarkColor;
        guardMark.sortingLayerID = spriter.sortingLayerID;
        guardMark.sortingOrder = spriter.sortingOrder + 5;
        float top = spriter.sprite != null ? spriter.sprite.bounds.max.y : 0.6f;
        guardMark.transform.localPosition = new Vector3(0f, top + rc.colonyGuardMarkGap / Mathf.Max(0.0001f, transform.lossyScale.y), 0f);
        float w = Mathf.Max(0.0001f, rc.colonyGuardMark.bounds.size.x);
        guardMark.transform.localScale = Vector3.one * (rc.colonyGuardMarkSize / w / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)));
        guardMark.gameObject.SetActive(true);
    }

    // 추적 시작 (일반 낮 몬스터처럼 행동)
    public void WakeGuard()
    {
        guardIdle = false;
    }

    private void ClearGuard()
    {
        ColonyGuard = false;
        GuardGroup = null;
        guardIdle = false;
        if (guardMark != null) guardMark.gameObject.SetActive(false);
    }

    private void GuardIdleMove()
    {
        RunConfig rc = RunConfig.Instance;
        rigid.velocity = Vector2.zero;
        if (GuardGroup == null) { guardIdle = false; return; }
        if (Vector2.Distance(target.position, GuardGroup.home) <= rc.colonyAggroRange)
        {
            GuardGroup.Alert();                              // 플레이어가 둥지 인식 범위 안으로
            return;
        }
        if (Time.time >= guardWanderAt || (guardWander - rigid.position).sqrMagnitude < 0.04f)
        {
            guardWander = GuardGroup.home + guardSpot + Random.insideUnitCircle * rc.colonyGuardWanderRadius;   // 자기 자리 근처에서 서성임
            guardWanderAt = Time.time + Random.Range(2f, 4.5f);
        }
        Vector2 d = guardWander - rigid.position;
        if (d.sqrMagnitude < 0.04f)
            return;
        Vector2 step = d.normalized * speed * rc.colonyIdleSpeedMul * Time.fixedDeltaTime;
        rigid.MovePosition(CampZone.SlideAround(rigid.position, step, CampBodyRadius()));
    }
    private int spriteType;
    private bool hiding;
    public bool Hiding => hiding;

    public class DayState
    {
        public int spriteType, expIndex, penaltyStack;
        public float maxHealth, health, speed, contactScale, penaltyHealthMul;
        public float recoverTime, recoverDuration, recoverStartMul;
        public Color color, penaltyTint, recoverStartTint;
        public bool rallied, noReward;
    }

    public DayState CaptureDayState()
    {
        return new DayState
        {
            spriteType = spriteType, expIndex = expSpawnIndex, penaltyStack = PenaltyStack,
            maxHealth = maxHealth, health = health, speed = speed, contactScale = ContactDamageScale,
            penaltyHealthMul = penaltyHealthMul, recoverTime = recoverTime, recoverDuration = recoverDuration, recoverStartMul = recoverStartMul,
            color = isStunTinted ? colorBeforeStun : spriter.color, penaltyTint = penaltyTint, recoverStartTint = recoverStartTint,
            rallied = rallied, noReward = NoReward,
        };
    }

    // 풀에서 꺼내 Init 한 뒤 저장한 체력·엘리트·처벌 외형 등을 그대로 되돌림
    public void RestoreDayState(DayState s)
    {
        maxHealth = s.maxHealth;
        health = s.health;
        speed = s.speed;
        ContactDamageScale = s.contactScale;
        PenaltyStack = s.penaltyStack;
        penaltyHealthMul = s.penaltyHealthMul;
        penaltyTint = s.penaltyTint;
        recoverTime = s.recoverTime;
        recoverDuration = s.recoverDuration;
        recoverStartMul = s.recoverStartMul;
        recoverStartTint = s.recoverStartTint;
        spriter.color = s.color;
        NoReward = s.noReward;
        if (s.rallied)
        {
            rallied = true;
            if (rallyFx == null) rallyFx = SpriteFx.RallyMark(transform);
        }
    }

    // 땅속으로 가라앉으며 투명해진 뒤 꺼짐 (보상 없음, 풀로 돌아감)
    public void HideForNight(float duration, float sink)
    {
        if (!isLive || hiding)
            return;
        hiding = true;
        coll.enabled = false;
        rigid.velocity = Vector2.zero;
        StartCoroutine(HideRoutine(Mathf.Max(0.01f, duration), sink));
    }

    private IEnumerator HideRoutine(float duration, float sink)
    {
        Vector3 from = transform.position;
        Color c = spriter.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rigid.MovePosition(from + Vector3.down * sink * k);
            spriter.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            yield return null;
        }
        hiding = false;
        isLive = false;
        if (rallyFx != null) Destroy(rallyFx);
        if (eliteAura != null)
        {
            SpriteFx.Stop(eliteAura);
            eliteAura = null;
        }
        spriter.color = Color.white;
        gameObject.SetActive(false);
    }

    private void SpawnExp(int expIndex)
    {
        for (int i=0; i< expIndex; i++)
        {
            Transform exp = GameManager.instance.pool.GetEnemy(0).transform;
            exp.parent = GameManager.instance.pool.transform;

            float expSpawnX = Random.Range(-expSpawnRange / 2, expSpawnRange / 2 + 1);
            float expSpawnY = Random.Range(-expSpawnRange / 2, expSpawnRange / 2 + 1);

            exp.position = CampZone.FreeSpot(transform.position + new Vector3(expSpawnX, expSpawnY), 0.3f);   // 텐트·동굴 등에 끼지 않게
        }
    }
}
