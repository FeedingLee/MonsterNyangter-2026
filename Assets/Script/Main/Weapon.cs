using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Weapon : MonoBehaviour
{
    [Header("# Weapon Data")]
    public int id;
    public int prefabId;
    public float damage;
    public float speed;                       // 무기의 회전 속도
    public float rate;                        // 무기의 연사 속도
    public int count;                         // 회전하는 무기 갯수

    [Header("# Etc Data")]
    public float memorydamage;                // 데미지 저장
    public float Critical_Damage;             // 크리티컬 데미지
    public float Chargespeed;                 // 랜스 돌진 스피드 저장
    Vector3 lastDirection;                    // 조이스틱이 마지막으로 향한 방향값 [대검]
    /* [ Damage 관련] */
    public float Gear_RemDmg;
    public float RemDmg;
    /* [ Speed(공격속도, 돌진유지 등) 관련] */
    public float Gear_RemSpd;
    public float RemSpd;
    /* [ Rate(연사속도, 쿨타임 등) 관련] */
    public float Gear_RemRate;
    public float RemRate;

    /* [ 무기 상태 확인 변수 ] */
    bool charm = false;                        // [대검] 참 모아베기상태 확인하는 변수
    bool cooldown = false;                     // 쿨타임을 확인하는 변수
    bool lancecharge = false;                  // [랜스] 돌진상태 확인하는 변수

    /* [ 스크립트 연결 ] */
    float timer;
    Player player;
    ItemData data;
    Item item;
    JoystickController joystickController;
    SpriteRenderer spriteRenderer;

    [Header("# Weapon Level")]
    public static int DB_level;               // 쌍검
    public static int HB_level;               // 활
    public static int SH_level;               // 헤보건
    public static int GS_level;               // 대검
    public static int LC_level;               // 랜스

    private void Awake()
    {
        player = GameManager.instance.player;
        joystickController = GameManager.instance.joystickController;

        Gear_RemRate = 100;
    }

    // 메인무기가 되면 보조무기로 작동하지 않고 MainWeaponController가 직접 조작
    public bool IsMain { get; private set; }

    // 시작 기본무기 나무 막대기: 쌍검 1레벨과 같은 동작, 다른 무기를 처음 얻으면 사라짐
    public bool IsStarter { get; private set; }
    private static Weapon starter;
    private readonly List<(SpriteRenderer r, Sprite sprite, Vector3 scale, Quaternion rot)> starterOriginals = new List<(SpriteRenderer, Sprite, Vector3, Quaternion)>();
    private readonly List<(Collider2D original, BoxCollider2D added)> starterColliders = new List<(Collider2D, BoxCollider2D)>();

    public static void CreateStarter(ItemData dualBlades, Sprite stickSprite)
    {
        RemoveStarter();
        Weapon w = new GameObject().AddComponent<Weapon>();
        w.Init(dualBlades);
        DB_level--;                                          // 쌍검 레벨로 치지 않음 (쌍검 카드는 새 무기로 나옴)
        WeaponSynergy.Evaluate();
        w.IsStarter = true;
        w.name = "Wood Stick (Starter)";
        if (stickSprite != null)
        {
            RunConfig rc = RunConfig.Instance;
            foreach (SpriteRenderer r in w.GetComponentsInChildren<SpriteRenderer>(true))
            {
                w.starterOriginals.Add((r, r.sprite, r.transform.localScale, r.transform.localRotation));   // 풀에 돌려줄 때 원래대로
                float bladeLength = r.bounds.size.y;                                      // 쌍검 칼 길이 (월드)
                r.sprite = stickSprite;
                float stickLength = rc.woodStickHitSize.y * Mathf.Abs(r.transform.lossyScale.y);   // 그림 속 막대 길이 (월드)
                r.transform.localScale *= bladeLength / Mathf.Max(0.0001f, stickLength) * rc.woodStickScale;
                r.transform.localRotation *= Quaternion.Euler(0f, 0f, rc.woodStickAngle);   // 그림의 막대를 바깥(위)으로 세움

                // 피격 판정을 막대 그림에 맞춤 (쌍검 칼날 판정은 꺼 두고 풀에 돌려줄 때 다시 켬)
                Collider2D original = r.GetComponent<Collider2D>();
                if (original != null)
                {
                    original.enabled = false;
                    BoxCollider2D hit = r.gameObject.AddComponent<BoxCollider2D>();
                    hit.isTrigger = original.isTrigger;
                    hit.size = rc.woodStickHitSize;
                    hit.offset = rc.woodStickHitOffset;
                    w.starterColliders.Add((original, hit));
                }
            }
        }
        starter = w;
    }

    // 다른 무기를 처음 얻었을 때: 나무 막대기 제거 (발사체는 풀에 돌려놓음)
    public static void RemoveStarter()
    {
        if (starter == null)
            return;
        foreach (var o in starter.starterOriginals)
        {
            if (o.r == null) continue;
            o.r.sprite = o.sprite;
            o.r.transform.localScale = o.scale;
            o.r.transform.localRotation = o.rot;
        }
        foreach (var c in starter.starterColliders)
        {
            if (c.added != null) Destroy(c.added);
            if (c.original != null) c.original.enabled = true;
        }
        Transform pool = GameManager.instance.pool.transform;
        for (int i = starter.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = starter.transform.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(pool, false);
        }
        Destroy(starter.gameObject);
        starter = null;
    }

    // 테스트 모드: 무기 제거 (발사체는 풀에 돌려놓음)
    public static void RemoveForTest(Weapon weapon)
    {
        if (weapon == null)
            return;
        Transform pool = GameManager.instance.pool.transform;
        for (int i = weapon.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = weapon.transform.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(pool, false);
        }
        Destroy(weapon.gameObject);
    }

    public ItemData Data => data;

    // 대검, 랜스는 damage가 모드에 따라 바뀌므로 기본 피해량을 따로 사용
    public float BaseDamage => (id == 3 || id == 4) ? memorydamage : damage;

    // 메인무기 전환: 진행 중이던 보조무기 동작을 멈추고 원래 상태로 되돌림
    public void PrepareMainWeapon()
    {
        IsMain = true;
        StopAllCoroutines();

        if (id == 3 || id == 4)
        {
            damage = memorydamage;
        }
        if (id == 4)
        {
            player.speed = player.memoryspeed;      // 랜스 돌진모드 속도 복구
        }

        charm = false;
        cooldown = false;
        lancecharge = false;
        transform.localRotation = Quaternion.identity;
    }

    // 랜스 각성으로 숨겼다가 다시 나타나면 대검 회전을 처음부터 (꺼지는 동안 코루틴이 멈춤)
    private void OnEnable()
    {
        if (id != 3 || IsMain)
            return;
        charm = false;
        spinning = false;
        bonusSpins = 0;
        if (transform.childCount > 0 && transform.GetChild(0).TryGetComponent(out Bullet sword))
        {
            sword.IsHarmless = false;
        }
    }

    // 랜스 공명 「대검 공명」: 패링하면 보조 대검이 즉시 1회 회전, 이미 돌고 있으면 끝난 직후 1회 더 (원래 쿨타임과 별개)
    bool spinning;                             // [대검] 실제로 돌고 있는지
    int bonusSpins;                            // [대검] 이어서 돌 추가 회전 수

    public void RequestBonusSpin()
    {
        if (id != 3 || IsMain || !isActiveAndEnabled)
            return;
        bonusSpins++;
        if (!spinning)
        {
            StartCoroutine(RunBonusSpins());
        }
    }

    private IEnumerator RunBonusSpins()
    {
        while (bonusSpins > 0)
        {
            bonusSpins--;
            yield return BonusSpin();
        }
    }

    private IEnumerator BonusSpin()
    {
        spinning = true;
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_GreatSword);
        float rotated = 0f;
        while (rotated < 360f)
        {
            float step = Mathf.Min(speed * PlayerBuff.Stamina * Time.deltaTime, 360f - rotated);
            transform.Rotate(Vector3.back * step);
            rotated += step;
            yield return null;
        }
        yield return MoveSwordToLastDirection();
        spinning = false;
    }

    private void Update()
    {
        if (!GameManager.instance.isLive || IsMain || MainWeaponController.SubWeaponsPaused)
            return;

        switch (id)
        {
            // 쌍검의 공격 방식
            case 0:
                transform.Rotate(Vector3.back * speed * PlayerBuff.Stamina * Time.deltaTime);   // speed에 스테미나가 이미 들어 있음 (한 번만)
                DualBlades();
                if (DB_level == 5)
                {
                    changesprite();
                }
                break;
            // 활의 공격 방식
            case 1:
                timer += Time.deltaTime;
                if (timer > rate * PlayerBuff.RateScale)
                {
                    timer = 0f;
                    HuntingBow();
                }
                if (HB_level == 5)
                {
                    changesprite();
                }
                break;
            // 저격용탄 헤비보우건의 공격 방식
            case 2:
                timer += Time.deltaTime;
                if (timer > rate * PlayerBuff.RateScale)
                {
                    timer = 0f;
                    Sniper();
                }
                if (SH_level == 5)
                {
                    changesprite();
                }
                break;
            // 대검의 공격 방식 - 회전 중이 아닐 때만 호출
            case 3:
                if (!charm)       // 회전 중이 아닐 때만 실행
                {
                    charm = true; // 회전 중으로 설정
                    GreatSword();
                    StartCoroutine(GreatSwordRotate());
                }
                if (GS_level == 5)
                {
                    changesprite();
                }
                break;
            // 랜스의 공격 방식
            case 4:
                LanceShieldMod();
                if (!lancecharge)
                {
                    lancecharge = true;
                    Lance();
                    StartCoroutine(LanceAttack());
                }

                if (damage == Critical_Damage && player.speed != Chargespeed)       // 돌진모드인데, 속도가 Up되지 않으면
                {
                    player.speed = GameManager.instance.player.memoryspeed + count; // 플레이어의 속도를 count만큼 올린다
                }

                if (LC_level == 5 && damage == Critical_Damage)
                {
                    spriteRenderer.sprite = data.weaponimage[2];
                }
                else if (LC_level != 5 && damage == Critical_Damage)
                {
                    spriteRenderer.sprite = data.weaponimage[0];
                }
                break;
        }
    }

    public void changesprite()
    {
        // 모든 자식 오브젝트의 SpriteRenderer 컴포넌트 가져오기
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            // 각 SpriteRenderer의 스프라이트를 변경
            spriteRenderer.sprite = data.weaponimage[0];
        }
    }

    // 레벨업 후 능력치 (LevelUp과 레벨업 선택지 설명이 같은 계산을 쓰도록 분리)
    public struct Stats
    {
        public float damage;          // 무기 공격력
        public float speed;           // 회전 속도 (랜스는 돌진 유지 시간)
        public float rate;            // 연사 간격, 쿨타임 (초)
        public int count;             // 갯수, 관통력, 베기 횟수, 돌진 속도 증가
        public float baseDamage;      // 대검, 랜스의 기본 피해량 (그 외는 damage와 같음)
        public float criticalDamage;  // 대검, 랜스 강화 상태 피해량
    }

    // 레벨업했을 때의 능력치를 계산만 함 (무기 상태는 바꾸지 않음)
    public Stats PreviewLevelUp(float damage, float speed, float rate, int count)
    {
        Stats next = new Stats();

        next.damage = damage * Character.Damage * (1 + Gear.damage_stat);

        next.speed = id == 4 ? speed : speed * Character.WeaponSpeed * (1 + Gear.stamina_stat);   // 랜스의 speed는 돌진 유지 시간이라 공격 속도 영향 없음

        next.rate = rate * Character.WeaponRate * (1 - Gear.rate_stat);

        next.count = count + Gear.upgrade_stat;

        next.baseDamage = next.damage;
        next.criticalDamage = Critical_Damage;
        // 대검, 랜스도 다른 무기처럼 캐릭터 기본 공격력 배율(Character.Damage)을 적용
        if (id == 3)
        {
            next.criticalDamage = damage * 1.5f * Character.Damage * (1 + Gear.damage_stat);
            next.baseDamage = next.criticalDamage / 1.5f;
        }
        else if (id == 4)
        {
            next.criticalDamage = damage * 3 * Character.Damage * (1 + Gear.damage_stat);
            next.baseDamage = next.criticalDamage / 4;
        }

        return next;
    }

    public void LevelUp(float damage, float speed, float rate, int count)
    {
        RemDmg = damage;
        RemSpd = speed;
        RemRate = rate;

        Stats next = PreviewLevelUp(damage, speed, rate, count);
        this.damage = next.damage;
        this.speed = next.speed;
        this.rate = next.rate;
        this.count = next.count;

        if (id == 0)
        {
            DualBlades();
        }
        else if (id == 3)
        {
            Critical_Damage = next.criticalDamage;
            memorydamage = next.baseDamage;
            GreatSword();
        }
        else if (id == 4)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();   // 획득 직후 같은 프레임에 레벨업해도 동작하도록
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = data.weaponimage[1];
            }
            Critical_Damage = next.criticalDamage;
            memorydamage = next.baseDamage;
            Lance();
        }

        switch (id)  // 무기
        {
            case 0:
                DB_level++;
                break;
            case 1:
                HB_level++;
                break;
            case 2:
                SH_level++;
                break;
            case 3:
                GS_level++;
                break;
            case 4:
                LC_level++;
                break;
        }
        WeaponSynergy.Evaluate();                            // 짝 무기 레벨이 오르면 시너지 판정
    }

    public void Init(ItemData data)
    {
        // Basic Set
        this.data = data;
        if (data.itemId == 0)
        {
            name = "Dual Blades [" + data.itemId + "]";
            DB_level++;
        }
        else if (data.itemId == 1)
        {
            name = "Hunting Bow [" + data.itemId + "]";
            HB_level++;
        }
        else if (data.itemId == 2)
        {
            name = "Sniper HBG [" + data.itemId + "]";
            SH_level++;
        }
        else if (data.itemId == 3)
        {
            name = "GreatSword [" + data.itemId + "]";
            GS_level++;
        }
        else if (data.itemId == 4)
        {
            name = "Lance [" + data.itemId + "]";
            LC_level++;
        }
        WeaponSynergy.Evaluate();                            // 무기 획득 시 시너지 판정
        transform.parent = player.transform;
        transform.localPosition = Vector3.zero;

        // 무기 능력치 셋팅
        id = data.itemId;
        damage = data.baseDamage * Character.Damage * (1 + Gear.damage_stat);
        speed = data.itemId == 4 ? data.baseSpeed : data.baseSpeed * Character.WeaponSpeed * (1 + Gear.stamina_stat);   // 랜스는 돌진 유지 시간
        rate = data.baseRate * Character.WeaponRate * (1 - Gear.rate_stat);
        count = (int)(data.baseCount + Gear.upgrade_stat);

        for (int index = 0; index < GameManager.instance.pool.prefabs.Length; index++)
        {
            if (data.projectile == GameManager.instance.pool.prefabs[index])
            {
                prefabId = index;
                break;
            }
        }

        // 각 근접무기 함수 호출하여 자식 오브젝트 생성
        if (id == 0)
        {
            DualBlades();  // 무기 초기화 시점에서 DualBlades 호출  
        }
        else if (id == 3)
        {
            Critical_Damage = damage * 1.5f;
            memorydamage = Critical_Damage / 1.5f;
            GreatSword();
        }
        else if (id == 4)
        {
            Critical_Damage = damage * 4;
            memorydamage = Critical_Damage / 4;
            Lance();
        }
    }

    // 보조무기(자동 공격) 표시: 보조무기 피해 배율과 무기·총알 크기 적용. 메인무기는 MainWeaponController가 따로 처리
    private void MarkSubWeapon(Bullet bullet)
    {
        if (bullet == null || IsMain)
            return;
        bullet.SubWeapon = true;
        bullet.TwinBlade = id == 0;                                   // 쌍둥이 무기 Lv2 칼 크기 (쌍검만)
        bullet.OrbitScaled = id == 0;                                 // 주위를 도는 칼: 크기가 바뀌면 거리도 (대검은 배치 뒤에 켬)
        bullet.ApplyWeaponSize();
        GimmickCatalog gc = GimmickCatalog.Instance;
        bullet.KnockChance = gc.KnockLeft(id) * gc.subKnockbackMul;   // 보조무기 넉백 = 같은 무기 좌클릭 x 배율
    }

    // 쌍둥이 무기: 보조 쌍검 칼 / 보조 활·헤비보우건 투사체 추가 (레벨별 고정값, 누적하지 않음)
    private int TwinBlades => IsMain ? 0 : TwinTable(GimmickCatalog.Instance.twinBlades);
    private int TwinProjectiles => IsMain ? 0 : TwinTable(GimmickCatalog.Instance.twinProjectiles);

    private static int TwinTable(int[] table)
    {
        int level = ExtraCards.Level(ItemData.ItemType.TwinWeapon);
        return level <= 0 || table == null || table.Length == 0 ? 0 : table[Mathf.Min(level, table.Length) - 1];
    }

    // 추가 투사체 방향: 원래 방향 좌우로 번갈아 벌림
    private static Vector3 TwinDirection(Vector3 dir, int index)
    {
        float step = GimmickCatalog.Instance.twinSpreadAngle * ((index + 2) / 2) * (index % 2 == 0 ? 1f : -1f);
        return Quaternion.Euler(0f, 0f, step) * dir;
    }

    // 쌍둥이 무기 카드를 고른 순간 보조 쌍검 칼 배치를 다시 함 (칼 수 반영)
    public void RefreshTwinBlades()
    {
        if (id == 0 && !IsMain && !IsStarter) DualBlades();
    }

    // 쌍검 공격, 배치 로직
    private void DualBlades()
    {
        int total = count + TwinBlades;
        for (int index = 0; index < total; index++)                         // 무기 갯수(count, 쌍둥이 무기 포함) 만큼 루프를 돌림
        {
            Transform bullet;

            if (index < transform.childCount)                               // 이미 존재하는 자식 오브젝트가 있다면 해당 오브젝트를 bullet에 할당
            {
                bullet = transform.GetChild(index);
            }
            else                                                            // 자식 오브젝트가 없다면 새롭게 bullet 생성
            {
                bullet = GameManager.instance.pool.Get(prefabId).transform; // Pool에서 발사체를 가져옴
                bullet.parent = transform;                                  // 새로운 bullet을 현재 Weapon의 자식으로 설정
            }

            bullet.localPosition = Vector3.zero;                            // bullet의 위치를 Weapon의 위치와 동일하게 설정
            bullet.localRotation = Quaternion.identity;                     // bullet의 회전을 초기화

            Vector3 rotVec = Vector3.forward * 360 * index / total;         // bullet을 Weapon 주위로 균일하게 회전 배치
            bullet.Rotate(rotVec);                                          // 회전 적용
            // 회전된 방향으로 1.5만큼 이동해 배치. 쌍둥이 무기 Lv2로 칼이 커지면 손잡이가 캐릭터를 가리지 않게 그만큼 바깥으로
            float twinSize = !IsMain && ExtraCards.Level(ItemData.ItemType.TwinWeapon) >= 2 ? GimmickCatalog.Instance.twinBladeSizeMul : 1f;
            float sizeMul = IsMain ? 1f : RunStats.Get(RunStat.WeaponSize);   // 「무기·총알 크기」로 칼이 커진 만큼도 바깥으로
            bullet.Translate(bullet.up * 1.5f * twinSize * sizeMul, Space.World);

            bullet.GetComponent<Bullet>().Init(damage, -1, Vector3.zero);   // bullet의 데미지 설정 및 방향 초기화
            MarkSubWeapon(bullet.GetComponent<Bullet>());
        }
    }

    // 대검 배치 로직
    private void GreatSword()
    {
        if (cooldown)                                          // 쿨다운 상태 (참모아 베기가 끝난 뒤) 
        {
            damage = memorydamage;                             // 데미지를 원래 데미지 값으로 변경
            Critical_Damage = damage * 1.5f;
            cooldown = false;                                  // 쿨다운 상태를 해제함
        }
        else if (!cooldown)                                    // 공격 가능 상태 (참모아 베기 중)
        {
            damage = Critical_Damage;                          // 카운트만큼 회전하면, 데미지를 크리티컬 데미지로 변경 ex) count가 3이면 3번째 참모아베기에 데미지 증가     
        }

        for (int i = 0; i < 1; i++)
        {
            Transform bullet;

            if (i < transform.childCount)
            {
                bullet = transform.GetChild(i);
            }
            else
            {
                bullet = GameManager.instance.pool.Get(prefabId).transform;
                bullet.parent = transform;
            }
            bullet.localPosition = Vector3.zero;
            bullet.localRotation = Quaternion.identity;

            float sizeMul = IsMain ? 1f : RunStats.Get(RunStat.WeaponSize);   // 「무기·총알 크기」로 칼이 커진 만큼 바깥으로
            bullet.Translate(bullet.up * 3.5f * sizeMul, Space.World);
            bullet.GetComponent<Bullet>().Init(damage, -1, Vector3.zero);
            MarkSubWeapon(bullet.GetComponent<Bullet>());
            bullet.GetComponent<Bullet>().OrbitScaled = !IsMain;
        }
    }

    // 랜스 배치 로직
    private void Lance()
    {
        for (int i = 0; i < 1; i++)
        {
            Transform bullet;

            if (i < transform.childCount)
            {
                bullet = transform.GetChild(i);
            }
            else
            {
                bullet = GameManager.instance.pool.Get(prefabId).transform;
                bullet.parent = transform;
            }
            // 창 모양일 때는 손잡이가 캐릭터 몸통에 오도록 앞으로 내밀어 캐릭터를 가리지 않게 함 (방패는 중앙)
            SpriteRenderer lanceSprite = bullet.GetComponentInChildren<SpriteRenderer>();
            bool isShield = lanceSprite != null && data.weaponimage != null && data.weaponimage.Length > 1 && lanceSprite.sprite == data.weaponimage[1];
            bullet.localPosition = isShield ? Vector3.zero : LanceHoldPosition(bullet);
            bullet.localRotation = Quaternion.identity;

            Bullet hit = bullet.GetComponent<Bullet>();
            hit.Init(damage, -1, Vector3.zero);
            MarkSubWeapon(hit);
            hit.RehitInterval = IsMain ? 0f : GameManager.instance.lanceRehitInterval;   // 보조 랜스: 같은 적 재타격 간격
        }
    }

    // 활 배치 + 공격 로직
    private void HuntingBow()
    {
        Transform target = player.scanner.NearestInRange(player.scanner.scanRange * player.scanner.subBowRangeScale);   // 보조 활 인식 범위
        if (!target)
            return;

        Vector3 targetPos = target.position;
        Vector3 dir = targetPos - transform.position;
        dir = dir.normalized;

        Transform bullet = GameManager.instance.pool.Get(prefabId).transform;
        bullet.position = transform.position;
        bullet.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        bullet.GetComponent<Bullet>().Init(damage, count + ExtraCards.PierceBonus, dir);   // 관통 화살촉 포함
        MarkSubWeapon(bullet.GetComponent<Bullet>());
        int volley = TwinProjectiles > 0 ? Bullet.NewVolley() : 0;   // 같은 발사 묶음 (같은 적 중복 적중 감소)
        bullet.GetComponent<Bullet>().VolleyId = volley;
        for (int i = 0; i < TwinProjectiles; i++)                // 쌍둥이 무기: 추가 화살
        {
            Vector3 d = TwinDirection(dir, i);
            Transform extra = GameManager.instance.pool.Get(prefabId).transform;
            extra.position = transform.position;
            extra.rotation = Quaternion.FromToRotation(Vector3.up, d);
            extra.GetComponent<Bullet>().Init(damage, count + ExtraCards.PierceBonus, d);
            MarkSubWeapon(extra.GetComponent<Bullet>());
            extra.GetComponent<Bullet>().VolleyId = volley;
        }
        SpriteFx.ArrowShot(transform.position, dir, false);     // 발사 궤적

        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Range);
    }

    // 헤비보우건 배치 + 공격 로직
    private void Sniper()
    {
        Vector3 dir = joystickController.GetInputVector();       // 이동 입력(조이스틱, WASD, 방향키) 방향으로 발사
        if (dir == Vector3.zero)                                 // 가만히 서 있다면
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f); // 360도 중 무작위 방향으로 발사
        }

        Transform bullet = GameManager.instance.pool.Get(prefabId).transform;
        bullet.position = transform.position;
        bullet.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        bullet.GetComponent<Bullet>().Init(damage, -1, dir);    // 헤비보우건은 관통력 무한 (-1)
        MarkSubWeapon(bullet.GetComponent<Bullet>());
        int volley = TwinProjectiles > 0 ? Bullet.NewVolley() : 0;
        bullet.GetComponent<Bullet>().VolleyId = volley;
        for (int i = 0; i < TwinProjectiles; i++)                // 쌍둥이 무기: 추가 탄 (관통 무한이라 속도를 직접 지정)
        {
            Vector3 d = TwinDirection(dir, i);
            Transform extra = GameManager.instance.pool.Get(prefabId).transform;
            extra.position = transform.position;
            extra.rotation = Quaternion.FromToRotation(Vector3.up, d);
            extra.GetComponent<Bullet>().Init(damage, -1, d);
            MarkSubWeapon(extra.GetComponent<Bullet>());
            extra.GetComponent<Bullet>().VolleyId = volley;
            Rigidbody2D extraRigid = extra.GetComponent<Rigidbody2D>();
            if (extraRigid != null) extraRigid.velocity = d * 25f;
        }
        SpriteFx.Muzzle(transform.position + dir * 0.6f, dir, false);   // 총구 불꽃 + 연기 + 탄 궤적

        Rigidbody2D bulletRigid = bullet.GetComponent<Rigidbody2D>();
        if (bulletRigid != null)
        {
            bulletRigid.velocity = dir * 25f;                     // 관통 무한 탄은 Init에서 속도를 주지 않으므로 직접 지정
        }

        AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_Sniper);
    }

    // 대검 회전 공격 코루틴
    private IEnumerator GreatSwordRotate()
    {
        int rotationCount = 0;                                                          // 회전 횟수를 추적하기 위한 변수

        while (rotationCount < count)                                                   // count 횟수만큼 회전을 반복
        {
            while (spinning)                                                            // 공명 추가 회전 중이면 끝날 때까지 대기
            {
                yield return null;
            }
            spinning = true;
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_GreatSword);          // 무기 사운드
            float rotatedAmount = 0f;                                                   // 회전 누적값을 초기화         

            while (rotatedAmount < (360f * count))                                      // count 수만큼(바퀴) 360도를 회전
            {
                float rotationStep = speed * PlayerBuff.Stamina * Time.deltaTime;                          // 매 프레임마다 회전할 양 계산
                transform.Rotate(Vector3.back * rotationStep);                          // 실제 회전 수행
                rotatedAmount += rotationStep;                                          // 누적 회전량 증가
                yield return null;                                                      // 한 프레임 대기
            }
            yield return MoveSwordToLastDirection();                                    // 마지막 조작 방향으로 대검을 이동
            spinning = false;
            if (bonusSpins > 0)
            {
                yield return RunBonusSpins();                                           // 회전 중 패링한 만큼 이어서 1회 더
            }

            rotationCount++;                                                            // 한 번의 360도 회전이 끝날 때마다 회전 횟수 증가

            cooldown = true;                                                            // 쿨다운 상태를 true로 만듬 (공격 중지)
            GreatSword();                                                               // 데미지 조정을 위해 GreatSword 함수를 다시 부름
            yield return new WaitForSeconds(rate * PlayerBuff.RateScale);                                      // 한 번 회전이 끝난 후 rate만큼 대기
        }
        charm = false;                                                                  // 참모아베기 정지상태로 변경   
    }

    // 회전이 끝난 대검을 마지막 조작 방향으로 빠르게 옮기는 코루틴 (이동 중에는 공격 판정 없음)
    private IEnumerator MoveSwordToLastDirection()
    {
        Bullet sword = transform.GetChild(0).GetComponent<Bullet>();
        sword.IsHarmless = true;

        while (true)
        {
            Quaternion target = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, player.LastMoveDir));
            float maxStep = GameManager.instance.greatSwordReturnSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, maxStep);

            if (Quaternion.Angle(transform.localRotation, target) < 0.1f)
                break;

            yield return null;
        }

        sword.IsHarmless = false;
    }

    // 창 모양일 때 위치: 창 손잡이 끝이 캐릭터 몸통에 오도록 창 길이의 절반만큼 앞으로
    private Vector3 LanceHoldPosition(Transform lance)
    {
        Sprite lanceImage = data.weaponimage != null && data.weaponimage.Length > 0 ? data.weaponimage[0] : null;
        if (lanceImage == null)
            return new Vector3(0f, 0.8f, 0f);

        float half = lanceImage.bounds.extents.y * Mathf.Abs(lance.localScale.y);
        return new Vector3(0f, Mathf.Max(0.8f, half - 0.15f), 0f);
    }

    // 랜스 움직이는 로직
    private void LanceRotate()
    {
        Vector3 dir = joystickController.GetInputVector();                              // Joystick 방향 벡터 받아옴

        if (dir != Vector3.zero)                                                        // dir이 (0, 0, 0)이 아닐 때만 lastDirection 업데이트
        {
            lastDirection = dir;                                                        // 마지막 방향 업데이트
        }
        else
        {
            dir = lastDirection;                                                        // dir이 (0, 0, 0)이면 마지막 방향 유지
        }
        transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);           // 조이스틱 방향 또는 마지막 방향으로 랜스를 배치함    
    }

    // 랜스 공격 로직
    private IEnumerator LanceAttack()
    {
        yield return new WaitForSeconds(rate * PlayerBuff.RateScale);                                          // rate 만큼 쿨타임을 기다려야 돌진모드 활성화

        if (lancecharge == true)
        {
            LanceShieldMod();
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Weapon_ChargeMod);           // 무기 사운드
            damage = Critical_Damage;

            Chargespeed = player.speed + count;                                         // 돌진모드의 속도 저장
            player.speed += count;                                                      // count만큼 속도 상승 [ 돌진모드 ] 
            Lance();

            yield return new WaitForSeconds(speed);                                     // speed 만큼 돌진모드 유지 (공격 속도 영향 없음)

            lancecharge = false;
            damage = memorydamage;
            player.speed = GameManager.instance.player.memoryspeed;                     // 그 후, 기억해둔 속도로 복구 [ 돌진종료 ]
            Lance();
        }
    }

    // 랜스 무기 변경 로직
    private void LanceShieldMod()
    {
        Transform childTransform = transform.GetChild(0);                               // 자식오브젝트를 가져옴
        CircleCollider2D childCollider = childTransform.GetComponent<CircleCollider2D>(); // CircleCollider2D 가져오기   

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();                      // 자식오브젝트의 스프라이트를 초기화

        if (damage == Critical_Damage)                                                  // 데미지 == 크리티컬 데미지라는 뜻은 돌진모드 라는 뜻
        {
            childCollider.offset = new Vector2(0f, 0.47f);                              // 포지션과, 사이즈를 창에 맞게 변경함
            childCollider.radius = 1.36f;
            childTransform.transform.localPosition = LanceHoldPosition(childTransform);   // 손잡이 끝이 몸통에 오도록

            LanceRotate();
        }
        else if (lancecharge == false)                                                  // 돌진모드가 끝나면
        {
            spriteRenderer.sprite = data.weaponimage[1];                                // 이미지를 방패로 바꾸고

            transform.rotation = Quaternion.Euler(0f, 0f, 0f);                          // 방패에 맞게 사이즈, 포지션, 피격범위 등을 수정함

            childCollider.offset = new Vector2(0f, 0f);
            childCollider.radius = 1.0f;

            childTransform.transform.localPosition = new Vector3(0f, 0f, 0f);
        }
    }
}