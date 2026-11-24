using System.Collections;
using UnityEngine;

public class BossPattern : MonoBehaviour
{
    Rigidbody2D BossRigid;                   // 발사 오브젝트(Boss) 의 Rigidbody2D
    Animator anim;
    SpriteRenderer spriter;
    Collider2D coll;

    public Rigidbody2D targetRigid;          // 플레이어 추적
    public Spawner spawner;                  // 보스스폰을 위해 선언

    [Header("# Multi-Directional Firing")]   // 전방향 발사 관련 변수
    public int MBulletCount;                 // 발사 갯수
    public float MBossBulletDamage;          // 발사 데미지
    public float MFireSpeed;                 // 발사 속도

    [Header("# One-Directional Firing")]     // 단일방향 발사 관련 변수
    public int OBulletCount;                 // 발사 갯수
    public float OBossBulletDamage;          // 발사 데미지
    public float OFireSpeed;                 // 발사 속도

    [Header("# BossDash")]
    public float DashSpeed;                  // 돌진 속도
    public bool IsBossDashing;               // 보스 돌진 상태 체크
    public float BossDashDamage;             // 보스 돌진 데미지
    public float BossDashDelay;              // 보스 돌진 대기 시간

    [Header("# BossState")]
    public float interval;                   // 다음 패턴까지의 쿨타임
    public float BossSpeed;                  // 보스 이동 속도
    public float currentHp;                  // 보스 현재 체력
    public float maxHp;                      // 보스 최대 체력
    public bool isBossLive;                  // 보스 생존 확인
    public bool isBossTired;                 // 보스 지침 상태 확인
    public bool isBossAttacking;             // 보스 공격 상태 확인
    public float MaxDamage;                  // 보스가 한번에 입을 수 있는 최대 피해량

    public Coroutine repeatActionCoroutine;  // 실행 중인 코루틴을 저장할 변수

    [Header("# Hit Flash")]
    [Min(0f)] public float hitFlashTime = 0.12f; // 피격 시 붉게 표시되는 시간(초)

    public static BossPattern Current { get; private set; } // 현재 활성화된 보스

    // 보스가 향할 위치 (미끼의 보스 적용 시간 동안은 미끼 위치)
    public Vector2 TargetPosition => Decoy.GetBossTarget(targetRigid.position);

    private Coroutine hitColorCoroutine;         // 진행 중인 피격 색상 복구 코루틴
    private RigidbodyConstraints2D defaultConstraints; // 돌진 대기 전 Rigidbody 제약

    private void Awake()
    {
        coll = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
        BossRigid = GetComponent<Rigidbody2D>();
        targetRigid = GameManager.instance.player.GetComponent<Rigidbody2D>();
        defaultConstraints = BossRigid.constraints;
        baseMaxHp = maxHp;
    }

    private float baseMaxHp;                     // 인스펙터에 설정한 원래 최대 체력 (맹수의 분노 배율 적용 전)
    private float stunEndTime;                   // 마비 두꺼비로 이동이 멈춘 시각까지

    // seconds초 동안 이동만 멈춤 (돌진 등 패턴은 그대로)
    public void Stun(float seconds)
    {
        if (isBossLive && seconds > 0f)
        {
            stunEndTime = Mathf.Max(stunEndTime, Time.time + seconds);
        }
    }

    private void OnEnable()
    {
        IsBossDashing = false;
        maxHp = baseMaxHp * RunStats.Get(RunStat.BossHp);   // 맹수의 분노: 보스 체력 증가 (밤 강화는 적용 안 됨)
        currentHp = maxHp;
        isBossLive = true;
        isBossTired = false;
        isBossAttacking = false;
        coll.enabled = true;
        BossRigid.simulated = true;
        BossRigid.constraints = defaultConstraints;
        spriter.color = Color.white;
        spriter.sortingOrder = 2;
        anim.SetBool("Dead", false);
        anim.SetBool("isBossDash", false);
        anim.SetBool("isBossDashReady", false);
        anim.SetBool("isBossTired", false);

        // 이미 나와 있는 잡몹과 충돌하지 않도록 설정 (이후 스폰되는 잡몹은 Enemy.OnEnable에서 처리)
        Current = this;
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            IgnoreCollisionWith(enemy.GetComponent<Collider2D>());
        }
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    // 보스와 지정한 콜라이더의 충돌을 무시
    public void IgnoreCollisionWith(Collider2D other)
    {
        Physics2D.IgnoreCollision(coll, other, true);
    }

    private void FixedUpdate()
    {
        if (!GameManager.instance.isLive)
            return;

        // 보스가 돌진 패턴시 or 보스 사망시 이동 막음
        if (IsBossDashing || !isBossLive ||
            !transform.GetComponent<BossReposition>().isBossMove)
            return;

        // 마비 두꺼비: 이동만 멈춤 (원거리 패턴은 계속)
        if (Time.time < stunEndTime)
            return;

        // 보스 이동 로직
        Vector2 dirVec = TargetPosition - BossRigid.position;   // 큰 몬스터라 텐트 울타리는 그냥 지나감 (CampZone이 충돌을 꺼 줌)
        Vector2 nextVec = dirVec.normalized * BossSpeed * Time.fixedDeltaTime;
        BossRigid.MovePosition(BossRigid.position + nextVec);
    }

    public IEnumerator RepeatAction()
    {
        while (isBossLive)
        {
            // 소환되자마자 발사하는것을 막음, 보스패턴 대기시간 / 2 초 대기
            yield return new WaitForSeconds(interval * RunStats.Get(RunStat.BossInterval));   // 맹수의 분노: 패턴 간격 감소

            // 패턴을 랜덤으로 선택 @@ 테스트 0~1
            int number = Random.Range(0, 3);
            if (number == 2)
            {
                // 돌진의 빈도는 화염보다 적게
                number = Random.Range(0, 3);
            }

            if (gameObject.GetComponent<BossReposition>().IsBossFalling)
            {
                // 보스가 텔포중일 때 공격기능 막음
                StopCoroutine(repeatActionCoroutine);
            }

            // 테스트용 패턴 반복 변수
            //number = 2;

            // 반복할 동작
            switch (number)
            {
                case 0:
                    isBossAttacking = true;
                    // 사방으로 발사
                    Fire();
                    isBossAttacking = false;
                    break;
                case 1:
                    isBossAttacking = true;
                    // 일직선으로 연속 발사
                    for (int i = 0; i < OBulletCount; i++)
                    {
                        // 0.1초(밸런스에 따라 조절) 안에 일정 간격으로 발사횟수만큼 발사
                        yield return new WaitForSeconds(1.0f / OBulletCount);
                        Fires();
                    }
                    isBossAttacking = false;
                    break;
                case 2:
                    // 돌진대기중에는 충돌 판정을 줄임
                    gameObject.GetComponent<CapsuleCollider2D>().size = new Vector2(2.0f, 4.0f);

                    // 돌진 대기 n초
                    // 돌진 대기 중에는 플레이어나 다른 물체에 밀리지 않도록 위치 고정
                    BossRigid.velocity = Vector2.zero;
                    BossRigid.constraints = RigidbodyConstraints2D.FreezeAll;

                    IsBossDashing = true;
                    isBossAttacking = true;

                    // 보스 돌진 애니메이션 재생
                    anim.SetBool("isBossDashReady", true);

                    // 보스 돌진대기 포효 사운드 재생
                    AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_DashYelling);
                    SpriteFx.BossWarning(transform, BossDashDelay);                 // 머리 위 "!!" 돌진 예고

                    // BossDashDelay 만큼 시간이 지난후 Setbool false
                    yield return new WaitForSeconds(BossDashDelay);

                    BossDash();

                    // 2초동안 플레이어와 충돌하지 않으면 멈춤
                    yield return new WaitForSeconds(3.0f);

                    // 보스가 2초가 지나기 전에 플레이어와 충돌하면 아래 코드를 스킵
                    if (IsBossDashing)
                    {
                        BossStop();
                    }

                    break;
            }
        }
    }

    public void StartAttack()
    {
        if (repeatActionCoroutine == null) // 이미 실행 중인 경우 중복 실행 방지
        {
            repeatActionCoroutine = StartCoroutine(RepeatAction());
        }
    }

    public void StopAttack()
    {
        if (repeatActionCoroutine != null) // 실행 중인 코루틴이 있는 경우에만 종료
        {
            StopCoroutine(repeatActionCoroutine);
            repeatActionCoroutine = null;
        }
    }

    // 보스 사망 모션 후 n초동안 대기, 이후 보스 오브젝트 비활성화
    private IEnumerator BossDeadsec(float time)
    {
        yield return new WaitForSeconds(time);
        Dead();
    }

    private IEnumerator WaitHitChange(float time)
    {
        yield return new WaitForSeconds(time);
        spriter.color = Color.white;
    }

    // 발사체 생성 함수
    public void Fire()
    {
        // 피격판정 복구
        gameObject.GetComponent<CapsuleCollider2D>().size = new Vector2(6.5f, 10.0f);

        // 보스 화염 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_FireShoot);
        SpriteFx.BossFire(transform.position + new Vector3(0f, -2.5f, 0f));    // 발사 지점 불꽃
        CameraShake.Play(0.08f, 0.12f);                                        // 화염 발사: 약하게

        anim.SetTrigger("BossFire");

        // 사방으로 발사하는 패턴
        float angleStep = 360f / MBulletCount;  // 각 발사체 사이의 각도 차이

        for (int i = 0; i < MBulletCount; i++)
        {
            float angle = i * angleStep;        // 각 발사체의 각도
            float rad = angle * Mathf.Deg2Rad;  // 각도를 라디안으로 변환

            // 방향 벡터 계산
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;

            // 발사체 생성 및 초기화          
            SpawnFireActor(MBossBulletDamage, 3, new Vector3(0, -2.5f, 0), dir, MFireSpeed, true);
        }
    }

    public void Fires()
    {
        // 보스 칼날 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Blade);

        anim.SetTrigger("BossFire");

        // 발사체 발사 방향 계산
        Vector2 dir = (TargetPosition - BossRigid.position).normalized;
        SpriteFx.BossSlash(transform.position + new Vector3(0f, -2.5f, 0f) + (Vector3)(dir * 1.2f), dir);   // 칼날 궤적

        // 발사체 생성 및 초기화
        SpawnFireActor(OBossBulletDamage, 2, new Vector3(0, -2.5f, 0), dir, OFireSpeed, false);
    }

    public void SpawnFireActor(
        float bulletDamage, int index, Vector3 spawnPosition, Vector3 dir, float addspeed, bool isFlip)
    {
        /* 
         * 매개변수 설명
         * bulletDamage : 탄환 데미지
         * spawnmPosition : 탄환 스폰 위치 조절값
         * dir : 탄환의 발사 방향
         * addSpeed : 탄환 속도 조절값
         * isFlip: 탄환 회전(0도 or 180도) 조절값
         */
        // 발사체 생성 및 초기화          
        Transform bullet = GameManager.instance.pool.GetEnemy(2).transform;
        bullet.position = transform.position + spawnPosition;  // 총알의 시작 위치
        if (!isFlip)
        {
            bullet.rotation = Quaternion.FromToRotation(Vector3.up, dir); // 회전 설정
        }
        else
        {
            bullet.rotation = Quaternion.FromToRotation(Vector3.up, -dir); // 회전 설정
        }
        bullet.GetComponent<BossBullet>().Init(bulletDamage, dir * addspeed, index);   // 총알 초기화
    }

    private void BossDash()
    {
        // 대쉬중에는 판정 살짝 줄이기
        gameObject.GetComponent<CapsuleCollider2D>().size = new Vector2(4.5f, 8.0f);

        // 보스 대쉬중 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Dash);
        SpriteFx.GroundBurst(transform.position + new Vector3(0f, -2.5f, 0f), 7f);   // 출발 지점 땅 튀김 (크게)
        CameraShake.Play(0.15f, 0.25f);                                              // 돌진 출발: 중간
        // 돌진 시작시 플레이어 외 다른 오브젝트와의 충돌을 막기위해 isTrigger 활성화
        gameObject.GetComponent<CapsuleCollider2D>().isTrigger = true;

        // 보스 돌진 애니메이션 재생
        anim.SetBool("isBossDashReady", false);
        anim.SetBool("isBossDash", true);

        // 보스 돌진 패턴
        IsBossDashing = true;

        // 발사체 발사 방향 계산
        Vector2 dirVec = TargetPosition - BossRigid.position;
        Vector2 nextVec = dirVec.normalized;

        // 돌진
        BossRigid.constraints = defaultConstraints;
        BossRigid.velocity = nextVec * DashSpeed;
    }

    public void BossStop()
    {
        // Player 충돌 부분에서 선언하기위해 public으로 설정
        if (IsBossDashing)
        {
            // 돌진이 종료되었을 때 isTrigger 비활성화
            gameObject.GetComponent<CapsuleCollider2D>().isTrigger = false;

            BossRigid.velocity = Vector3.zero;
            isBossAttacking = false;
            IsBossDashing = false;
            anim.SetBool("isBossDash", false);
            anim.SetTrigger("isBossDashDone");
        }
    }

    private void Dead()
    {
        // 보스 사망 시 비활성화
        //gameObject.SetActive(false);

        // 승리 이벤트 시작
        GameManager.instance.GameVictory();
    }

    private void HitColorChange()
    {
        // 피격색상(빨간색)으로 변경
        spriter.color = new Color(1f, 0.54f, 0.54f, 1f);

        // 이전 복구 예약을 취소해 연속 피격 시 앞선 타격의 타이머가 색상을 먼저 되돌리지 않도록 함
        if (hitColorCoroutine != null)
        {
            StopCoroutine(hitColorCoroutine);
        }
        hitColorCoroutine = StartCoroutine(WaitHitChange(hitFlashTime));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 접촉한 오브젝트가 bullet일 경우
        if (collision.CompareTag("Bullet") && !collision.GetComponent<Bullet>().IsHarmless)
        {
            Bullet bullet = collision.GetComponent<Bullet>();
            if (!bullet.TryHitTarget(gameObject))
                return;                                      // 같은 적 재타격 간격 안
            float damage = GimmickCards.ModifyHit(bullet.HitDamage * bullet.VolleyScale(gameObject), !bullet.SubWeapon, GimmickCards.TargetKind.Boss, bullet);   // 전투 기믹 (1회 상한은 TakeDamage에서 그대로)
            TakeDamage(damage);
            GimmickCards.AfterHit(transform.position, damage, false);
            bullet.RegisterHit(gameObject);                  // 관통 횟수 소모 (발사체 쪽 이벤트와 중복되지 않음)
            bullet.NotifyHit(gameObject);
        }
    }

    // 피해 처리 (발사체 충돌, 화살비처럼 범위 안에 있는 동안 반복해서 주는 피해 공용)
    // ignoreCap: 1회 피해 상한(MaxDamage)을 무시 (폭탄 통)
    public void TakeDamage(float damage, bool ignoreCap = false)
    {
        if (!isBossLive)
            return;

        {
            // 플레이어의 무기 데미지가 최대 데미지보다 높을경우 최대 데미지까지만 피해를 입음
            if (!ignoreCap && damage >= MaxDamage)
            {
                damage = MaxDamage;
            }

            currentHp -= damage;
            DamageNumber.Add(spriter, damage);
            SpriteFx.HitSpark(transform.position + (Vector3)(Random.insideUnitCircle * 2f), 3f);   // 보스가 커서 별도 크게
            HitColorChange();

            if (currentHp > 0)
            {
                if (!isBossTired && currentHp <= maxHp / 2)
                {
                    // 보스 지침상태로 전환
                    isBossTired = true;
                    anim.SetBool("isBossTired", true);

                    // 이동속도 반으로 감소
                    BossSpeed /= 2f;

                    // 광폭화
                    gameObject.GetComponent<BossBerserkMode>().Init();
                }
            }
            else
            {
                StopAttack();
                isBossLive = false;

                // 클리어가 확정되었으므로 이후 데미지 숫자는 표시하지 않음
                DamageNumber.HideAll();
                DamageNumber.Suppressed = true;
                coll.enabled = false;
                BossRigid.simulated = false;
                spriter.sortingOrder = 1;
                isBossTired = false;
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Dead);
                anim.SetBool("isBossDashReady", false);
                anim.SetBool("isBossDash", false);
                anim.SetBool("Dead", true);
                StartCoroutine(BossDeadsec(2.0f));
            }
        }
    }
}