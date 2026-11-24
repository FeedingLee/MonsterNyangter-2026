using System.Collections;
using UnityEngine;

public class BossReposition : MonoBehaviour
{
    [Header("#BossReposion")]
    public float fallingWaitTime;       // 낙하하기 전 대기 시간
    public float fallingDistance;       // 이 이상 플레이어와 멀어지면 낙하 시작
    public float fallingSpeed;          // 낙하 속도
    public Transform targetTransform;   // 플레이어 트랜스폼
    public bool isBossMove;             // 보스 이동 관련 bool 변수
    public bool IsBossFalling;          // 보스 순간이동 관련 bool 변수 
    public float WaitReposition;        // 보스 Reposition 쿨타임
    public BossPattern bossPattern;
    private float time;
    Transform bossTransform;            // 자신(보스) 트랜스폼
    Rigidbody2D bossRigid;              // 자신(보스) Rigidbody2D
    Vector2 landingPos;                 // 낙하 목표 위치
    bool isDescending;                  // 그림자를 향해 낙하 중인지 여부
    GameObject bossShadow;              // 그림자를 저장할 오브젝트
    Animator anim;                      // 보스 애니메이터 
    Coroutine test;

    [Header("# Landing Firing")]        // 착지 전방향 발사 관련 변수
    public int LBulletCount;            // 발사 갯수 20
    public float LBossBulletDamage;     // 발사 데미지
    public float LFireSpeed;            // 발사 속도 0.5

    private void Awake()
    {
        bossTransform = transform;
        bossRigid = GetComponent<Rigidbody2D>();
        isBossMove = true;
        IsBossFalling = false;
        targetTransform = GameManager.instance.player.transform;
        bossPattern = gameObject.GetComponent<BossPattern>();
        anim = GetComponent<Animator>();
        time = WaitReposition;
    }

    private void FixedUpdate()
    {
        // 낙하가 시작되면 쿨타임이나 공격 상태와 관계없이 선정된 위치까지 반드시 내려감
        if (isDescending)
        {
            Vector2 nextPos = Vector2.MoveTowards(bossRigid.position, landingPos, fallingSpeed * Time.fixedDeltaTime);
            bossRigid.MovePosition(nextPos);

            if (nextPos == landingPos)
            {
                Land();
            }
            return;
        }

        // Reposition 쿨타임 계산
        time += Time.deltaTime;
        if (time < WaitReposition)
        {
            //Debug.Log("time: " + time);
            return;
        }

        // 보스가 공격중일때는 Reposition을 막음
        if (bossPattern.isBossAttacking || !bossPattern.isBossLive)
        {
            return;
        }

        // 플레이어와 보스 사이의 거리 측정
        float distance = Vector2.Distance(bossPattern.TargetPosition, bossTransform.position);

        // 거리가 지정한 값보다 멀어졌을 경우 & BossReposion이 진행중이지 않을 경우
        // & 보스의 공격이 진행중이지 않을 경우
        if (distance > fallingDistance &&
            !IsBossFalling &&
            isBossMove &&                       // 착지 애니메이션 중에는 다음 낙하를 시작하지 않음
            !bossPattern.isBossAttacking)
        {
            // Reposition 시작
            StartCoroutine(StartBossReposition());
        }
    }

    private IEnumerator StartBossReposition()
    {
        // 점프 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Jump);
        SpriteFx.GroundBurst(bossTransform.position + new Vector3(0f, -2f, 0f), 8f);   // 뛰어오르는 자리 땅 튀김 (크게)

        // 하강중에는 충돌 판정을 줄임
        gameObject.GetComponent<CapsuleCollider2D>().size = new Vector2(1.2f, 1.2f);

        // 보스 공격 패턴 중지
        bossPattern.StopAttack();

        // 보스 이동 제한
        isBossMove = false;
        IsBossFalling = true;

        // Player와 Boss 의 방향벡터 계산
        Vector3 targetPosition = bossPattern.TargetPosition;
        Vector3 direction = (targetPosition - bossTransform.position).normalized;

        // BossReposion
        bossTransform.position = targetPosition + direction * 2.0f;

        // 위치에 그림자 생성
        bossShadow = GameManager.instance.pool.GetEnemy(3);
        bossShadow.transform.position = bossTransform.position - new Vector3(0, 1, 0);

        // 그림자 추적 허용
        bossShadow.GetComponent<BossShadow>().IsTraceTarget = true;

        // Boss 위치 하늘로 설정
        bossTransform.position += new Vector3(0, 5000, 0);

        // 하늘에서 떨어지는 시간 대기
        yield return new WaitForSeconds(fallingWaitTime - 0.5f);

        // 그림자 추적 종료
        bossShadow.GetComponent<BossShadow>().IsTraceTarget = false;

        // 그림자 추적 종료 후 0.5초 뒤 하강 시작
        yield return new WaitForSeconds(0.5f);

        // Boss 위치 그림자 위로 설정
        bossTransform.position = bossShadow.transform.position + new Vector3(0, 50, 0);

        // 하강 중에는 플레이어나 벽에 막히지 않도록 충돌을 끄고 그림자 위치를 목표로 고정
        landingPos = CampZone.PushOut(bossShadow.transform.position, 1.2f);   // 텐트 울타리 안에는 내려앉지 않음
        gameObject.GetComponent<CapsuleCollider2D>().isTrigger = true;
        isDescending = true;
    }

    private IEnumerator WaitAnim(float waitTime)
    {
        // 착지 애니메이션이 재생중인 동안 대기
        yield return new WaitForSeconds(waitTime);

        // 보스 이동 제한 해제
        isBossMove = true;

        //보스 패턴 재시작
        bossPattern.StartAttack();

        time = 0;
    }

    private void Fire()
    {
        // 보스 착지 후 돌튀는 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Rock);

        // 사방으로 발사하는 패턴
        float angleStep = 360f / LBulletCount;  // 각 발사체 사이의 각도 차이

        for (int i = 0; i < LBulletCount; i++)
        {
            float angle = i * angleStep;  // 각 발사체의 각도
            float rad = angle * Mathf.Deg2Rad;  // 각도를 라디안으로 변환

            // 방향 벡터 계산
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;

            // 발사체 생성 및 초기화          
            bossPattern.SpawnFireActor(LBossBulletDamage, 0, new Vector3(0, -1.5f, 0), dir, LFireSpeed, false);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDescending && collision.tag == "BossShadow")
        {
            Land();
        }
    }

    // 착지 처리 (그림자 접촉 또는 목표 위치 도달 시 한 번만 실행)
    private void Land()
    {
        isDescending = false;

        // 보스 착지 사운드 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Landing);
        SpriteFx.LandingRing(landingPos, 12f);                                   // 착지 흙먼지 고리
        CameraShake.Play(0.35f, 0.45f);                                          // 착지: 강하게
        SpriteFx.RockBurst((Vector3)landingPos + new Vector3(-3f, 0.5f, 0f), 3f);         // 튀는 파편
        SpriteFx.RockBurst((Vector3)landingPos + new Vector3(3f, 0.5f, 0f), 3f);

        // 착지 애니메이션 재생
        anim.SetTrigger("isBossRanding");

        // 그림자와 충돌시 보스 하강 멈춤
        bossRigid.velocity = Vector3.zero;

        // 하강 상태 비활성화
        IsBossFalling = false;

        // 충돌 판정 복구
        CapsuleCollider2D capsule = gameObject.GetComponent<CapsuleCollider2D>();
        capsule.isTrigger = false;
        capsule.size = new Vector2(6.5f, 10.0f);

        // 그림자 비활성화
        bossShadow.SetActive(false);

        // 사방으로 돌맹이 발사
        Fire();

        StartCoroutine(WaitAnim(0.5f));
    }
}