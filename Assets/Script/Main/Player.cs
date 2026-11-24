using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public Vector2 inputVec;
    public float speed;
    public float memoryspeed; 
    public Scanner scanner;
    public RuntimeAnimatorController[] animCon;
    public bool ismove;                       // 플레이어 이동불가상태(ex: 넉백)을 위한 변수

    private float hitSoundCooldown = 0.3f;    // 0.3초 소리 쿨타임
    private float lastHitSoundTime;           // 마지막 재생 시간 기억

    public JoystickController joystick;       // JoystickController를 연결합니다.

    [Header("# Dodge")]
    [Min(0f)] public float dodgeDistance = 3f;          // 회피 이동 거리
    [Min(0.01f)] public float dodgeMoveTime = 0.15f;    // 회피 이동에 걸리는 시간(초)
    [Min(0f)] public float dodgeInvincibleTime = 0.3f;  // 회피 무적 시간(초)
    [Min(0f)] public float dodgeCooldown = 3f;          // 회피 쿨타임(초)

    private bool isDodging;                   // 회피 이동 중인지 여부
    private float dodgeSpeed;                 // 이번 회피의 이동 속도
    private float dodgeRemaining;             // 이번 회피의 남은 이동 거리
    private Vector2 dodgeDir;                 // 이번 회피의 이동 방향
    private Vector2 lastMoveDir = Vector2.left; // 마지막 이동 방향 (첫 입력 전에는 스프라이트 기본 방향)
    private float invincibleEndTime;          // 무적이 끝나는 시각
    private float dodgeReadyTime;             // 다음 회피가 가능한 시각
    private float appliedCooldown;            // 진행 중인 쿨타임의 전체 길이
    private CollisionDetectionMode2D defaultCollisionMode; // 회피 전 충돌 감지 모드
    private bool isRooted;                    // 저격모드 등으로 제자리에 고정된 상태인지 여부
    private bool ultimateRooted;              // 필살기(저격모드)로 고정 요청
    private bool weaponRooted;                // 메인무기(대검 모아 베기)로 고정 요청
    private RigidbodyConstraints2D defaultConstraints;     // 고정 전 Rigidbody 제약

    // 회피 무적 상태 여부 (테스트 모드의 피격 판정 OFF 포함)
    public bool IsInvincible => Time.time < invincibleEndTime || TestMode.NoHit || GimmickCards.EscapeInvincible;   // 위기 탈출 무적 포함

    // 회피나 메인무기 돌진처럼 조작 없이 강제로 이동 중인지 여부
    public bool IsForcedMoving => isDodging;

    // 플레이어가 마지막으로 이동한 방향
    public Vector2 LastMoveDir => lastMoveDir;

    // 쿨타임 진행도 (0: 방금 사용, 1: 사용 가능)
    public float DodgeCooldownProgress
    {
        get
        {
            if (appliedCooldown <= 0f)
                return 1f;

            return 1f - Mathf.Clamp01((dodgeReadyTime - Time.time) / appliedCooldown);
        }
    }

    Rigidbody2D rigid;
    SpriteRenderer spriter;
    Animator anim;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        scanner = GetComponent<Scanner>();
        ismove = true;
    }

    private void OnEnable()
    {
        speed *= Character.Speed;
        memoryspeed = speed;
        anim.runtimeAnimatorController = animCon[GameManager.instance.playerId];
    }

    private float nextMoveDustTime;

    private void Update()
    {
        // 사망로직
        if (GameManager.instance.health <= 0)
        {
            Die();
        }

        if (isDead || !GameManager.instance.isLive)
            return;               
        inputVec = joystick.GetInputVector(); // JoystickController에서 inputVec을 가져옵니다.

        if (inputVec != Vector2.zero)
        {
            lastMoveDir = inputVec.normalized;

            // 걸을 때 발밑 작은 흙먼지
            if (!isDodging && Time.time >= nextMoveDustTime)
            {
                nextMoveDustTime = Time.time + 0.22f;
                SpriteFx.MoveDust(transform.position, inputVec);
            }
        }

        // 클릭으로 선택된 UI 버튼이 스페이스바 입력에 눌리지 않도록 게임 진행 중에는 선택을 해제
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space))
        {
            TryDodge();
        }
    }

    private void FixedUpdate()
    {
        if (!GameManager.instance.isLive || !ismove)
            return;

        if (isDodging)
        {
            float step = Mathf.Min(dodgeSpeed * Time.fixedDeltaTime, dodgeRemaining);
            rigid.MovePosition(rigid.position + dodgeDir * step);   // 일반 이동과 같은 방식이라 벽 등의 충돌 판정은 유지됨
            dodgeRemaining -= step;

            if (dodgeRemaining <= 0f)
            {
                EndDodge();
            }
            return;
        }

        if (isRooted)
            return;

        Vector2 nextVec = inputVec * speed * PlayerBuff.MoveSpeed * MainWeaponController.MoveSpeedScale
            * NightDefense.ReturnSpeedScale(inputVec, rigid.position) * Time.fixedDeltaTime;   // 귀환 준비 중 기지 쪽 이동 버프
        rigid.MovePosition(rigid.position + nextVec);
    }

    private void LateUpdate()
    {
        if (!GameManager.instance.isLive || !ismove)
            return;

        Vector2 animVec = isDodging ? dodgeDir : inputVec;   // 회피 중에는 회피 방향 기준으로 애니메이션 표시
        anim.SetFloat("Speed", isRooted ? 0f : animVec.magnitude);
        if (animVec.x != 0 && !(isDodging && keepFacing))   // 방향에 따른 스프라이트 뒤집기 (백대쉬는 보던 방향 유지)
        {
            spriter.flipX = animVec.x > 0;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ContactHit(collision.gameObject);
    }

    // 몬스터 접촉 피해 (충돌 중 매 물리 프레임). 밤 웨이브 몬스터는 밀지 않도록 충돌을 끄고 Enemy가 겹침으로 직접 호출
    public void ContactHit(GameObject source)
    {
        Collision2DSource collision = new Collision2DSource(source);
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Boss"))
        {
            if (!GameManager.instance.isLive)
                return;

            if (IsInvincible)                 // 회피 무적 중에는 피해, 피격음, 피격 애니메이션 없음
                return;

            if (TryBlockHit(collision.gameObject.CompareTag("Boss")))   // 랜스 방어 중이면 패링 (피해 무효, 보스 접촉은 스택 더 많이)
                return;

            // 밤에 강화된 몬스터는 더 아프게 (보스는 강화 없음)
            float contactScale = collision.gameObject.TryGetComponent(out Enemy enemy) ? enemy.ContactDamageScale : 1f;
            if (collision.gameObject.TryGetComponent(out MiniBoss miniBoss))
            {
                contactScale = miniBoss.ContactDamageScale;
                if (contactScale <= 0f)
                    return;                       // 말벌: 접촉 간격마다 따로 피해 (MiniBoss)
            }
            GameManager.instance.health -= Time.deltaTime * 50 * MainWeaponController.DamageTakenScale * contactScale;

            PlayHitSound();

            if (GameManager.instance.health <= 0) // 사망 로직
            {
                Die();
            }
            else
            {
                anim.SetTrigger("Damage");
            }
        }
    }


    // ContactHit 본문이 collision.gameObject를 그대로 쓰도록 감싼 것
    private struct Collision2DSource
    {
        public readonly GameObject gameObject;
        public Collision2DSource(GameObject go) { gameObject = go; }
    }

    // 사망 처리 (한 번만). 매 프레임 사망 트리거를 다시 걸면 쓰러졌다 일어나는 모션이 반복되므로 막음
    private bool isDead;

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        gameObject.GetComponent<CapsuleCollider2D>().enabled = false;
        anim.ResetTrigger("Damage");
        anim.SetTrigger("Dead");
        GameManager.instance.GameOver();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Boss") && !isDead)
        {
            if (collision.gameObject.GetComponent<BossPattern>().IsBossDashing)
            {
                if (IsInvincible || TryBlockHit(true)) // 회피 무적, 패링 중에는 돌진 충돌만 처리하고 피해와 넉백은 없음
                {
                    collision.gameObject.GetComponent<BossPattern>().BossStop();
                    return;
                }

                EndDodge();                   // 무적이 끝난 뒤 맞으면 남은 회피 이동을 취소하고 넉백
                ismove = false;
                StartCoroutine(PlayerKnockBack(collision));
                collision.gameObject.GetComponent<BossPattern>().BossStop();
            }
        }
    }

    // 제자리 고정 설정 (필살기용). 고정 중에는 몬스터에게 밀리지 않음
    public void SetRooted(bool rooted)
    {
        ultimateRooted = rooted;
        ApplyRooted();
    }

    // 제자리 고정 설정 (메인무기용). 필살기 고정과 따로 관리해 서로 풀어 버리지 않도록 함
    public void SetWeaponRooted(bool rooted)
    {
        weaponRooted = rooted;
        ApplyRooted();
    }

    private void ApplyRooted()
    {
        bool rooted = ultimateRooted || weaponRooted;
        if (isRooted == rooted)
            return;

        isRooted = rooted;

        if (rooted)
        {
            EndDodge();
            defaultConstraints = rigid.constraints;
            rigid.velocity = Vector2.zero;
            rigid.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            rigid.constraints = defaultConstraints;
        }
    }

    // 피격음 재생 (여러 공격에 동시에 맞아도 쿨타임마다 한 번만 재생)
    public void PlayHitSound()
    {
        if (Time.time - lastHitSoundTime < hitSoundCooldown)
            return;

        AudioManager.instance.PlaySfxPriority(AudioManager.Sfx.Player_hit);
        lastHitSoundTime = Time.time;
    }

    // 회피 시작 (게임 정지, 넉백 중, 회피 중, 고정 중, 쿨타임 중에는 무시)
    public void TryDodge()
    {
        if (!GameManager.instance.isLive || !ismove || isDodging || isRooted || Time.time < dodgeReadyTime || MainWeaponController.Scoping)
            return;

        isDodging = true;
        dodgeDir = lastMoveDir;               // 입력이 있으면 현재 방향, 없으면 마지막 이동 방향
        dodgeRemaining = dodgeDistance;
        dodgeSpeed = dodgeDistance / dodgeMoveTime;
        invincibleEndTime = Time.time + dodgeInvincibleTime;
        GimmickCards.OnDodge();                              // 회피 반격
        float cooldown = dodgeCooldown * RunStats.Get(RunStat.DodgeCooldown);   // 상점 강화, 아침 선택지 반영
        dodgeReadyTime = Time.time + cooldown;
        appliedCooldown = cooldown;

        // 빠른 이동 중 얇은 벽을 통과하지 않도록 회피 동안만 연속 충돌 감지 사용
        defaultCollisionMode = rigid.collisionDetectionMode;
        rigid.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 회피 연출: 출발 지점 흙먼지 + 휙 소리
        SpriteFx.DodgeDust(transform.position, dodgeDir);
        AudioClip dodgeSound = RunConfig.Instance.dodgeSound;
        if (dodgeSound != null)
        {
            AudioManager.instance.PlayClip(dodgeSound);
        }
    }

    // 메인무기 스킬용 강제 이동 (회피 쿨타임은 쓰지 않음). 이동 중에는 조작할 수 없음
    // faceKeep: 이동하는 동안 캐릭터가 보던 방향을 그대로 유지 (쌍검 백대쉬)
    private bool keepFacing;

    public bool ForceMove(Vector2 direction, float distance, float moveTime, float invincibleTime, bool faceKeep = false)
    {
        if (!GameManager.instance.isLive || !ismove || isDodging || isRooted || direction == Vector2.zero)
            return false;

        keepFacing = faceKeep;
        isDodging = true;
        dodgeDir = direction.normalized;
        dodgeRemaining = distance;
        dodgeSpeed = distance / Mathf.Max(0.01f, moveTime);
        GrantInvincibility(invincibleTime);

        defaultCollisionMode = rigid.collisionDetectionMode;
        rigid.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        return true;
    }

    // 지금부터 duration초 동안 무적 (이미 더 긴 무적이 있으면 유지)
    public void GrantInvincibility(float duration)
    {
        invincibleEndTime = Mathf.Max(invincibleEndTime, Time.time + duration);
    }

    // 랜스 방어 중 공격을 받으면 패링하고 true를 돌려줌 (fromBoss: 보스 접촉·패턴)
    public bool TryBlockHit(bool fromBoss = false)
    {
        if (MainWeaponController.Current != null && MainWeaponController.Current.TryParry(fromBoss))
            return true;
        return GimmickCards.TryShield();                     // 보호막: 피해 1회를 막음
    }

    // 회피 이동 종료
    private void EndDodge()
    {
        if (!isDodging)
            return;

        isDodging = false;
        dodgeRemaining = 0f;
        rigid.collisionDetectionMode = defaultCollisionMode;
    }

    private IEnumerator PlayerKnockBack(Collider2D collision)
    {
        // 보스 돌진 피격음 재생
        PlayHitSound();

        // 피격 애니메이션 재생
        anim.SetTrigger("Damage");

        // 보스가 플레이어와 충돌 시 데미지를 받음
        GameManager.instance.health -= collision.gameObject.GetComponent<BossPattern>().BossDashDamage * MainWeaponController.ChargeGuardScale;   // 공명 「철벽 모으기」
        
        // 보스 위치 계산
        Rigidbody2D target = collision.gameObject.GetComponent<Rigidbody2D>();
        
        // 플레이어 반동 방향 계산
        Vector2 dirVec = rigid.position - target.position;
        Vector2 nextVec = dirVec.normalized;
        
        // 보스와 충돌 시 플레이어가 일정한 힘으로 밀려남
        rigid.velocity = Vector2.zero;
        rigid.velocity = nextVec * 15.0f;
        //rigid.AddForce(nextVec * 5f, ForceMode2D.Impulse);
        
        // 보스 돌진패턴에 피격당할 시 0.8초간 이동불가        
        yield return new WaitForSeconds(0.8f);
        rigid.velocity = Vector2.zero;
        ismove = true;
    }
}
