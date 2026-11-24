using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage;
    public int per;
    public int Weapon_num; // 무기의 번호 받기 (활, 헤보건 구별)

    // true인 동안은 닿아도 피해를 주지 않음 (콜라이더를 끄면 Area 이탈로 처리되어 오브젝트가 꺼지므로 별도 사용)
    public bool IsHarmless { get; set; }

    // 필살기 배율까지 적용된 실제 피해량 (보조무기 공격이면 보조무기 피해 배율도)
    public float HitDamage => damage * PlayerBuff.Damage * (SubWeapon ? RunStats.Get(RunStat.SubWeaponDamage) : 1f);

    // 보조무기(자동 공격)가 쏜 공격인지. Init 때 꺼지고, 보조무기가 Init 뒤에 켬
    public bool SubWeapon { get; set; }

    // 이 공격의 넉백 확률 (음수 = 지정 안 함 → 메인무기 좌클릭 확률). Init 때 지워지고, 공격을 만든 쪽이 지정
    public float KnockChance { get; set; } = -1f;

    // 쌍둥이 무기: 같은 발사(1회)로 나간 투사체 묶음 번호 (0 = 묶음 없음)
    public int VolleyId { get; set; }
    private static int volleyCounter;
    public static int NewVolley() => ++volleyCounter;
    private static readonly HashSet<long> volleyHits = new HashSet<long>();

    // 같은 묶음의 다른 투사체가 이미 이 적을 맞혔으면 피해 배율 (첫 적중은 1)
    public float VolleyScale(GameObject target)
    {
        if (VolleyId == 0)
            return 1f;
        if (volleyHits.Count > 4000) volleyHits.Clear();
        long key = ((long)VolleyId << 32) ^ (uint)target.GetInstanceID();
        return volleyHits.Add(key) ? 1f : GimmickCatalog.Instance.twinSameTargetMul;
    }

    // 쌍둥이 무기 Lv2 보조 쌍검 칼 크기 (Weapon.MarkSubWeapon에서 쌍검 칼만 켬)
    public bool TwinBlade { get; set; }
    // 캐릭터 주위를 도는 보조 근접 무기(쌍검·대검): 크기가 바뀌면 캐릭터와의 거리도 같은 비율로
    public bool OrbitScaled { get; set; }
    private float TwinSize => TwinBlade && ExtraCards.Level(ItemData.ItemType.TwinWeapon) >= 2 ? GimmickCatalog.Instance.twinBladeSizeMul : 1f;

    public float ResolveKnockChance()
    {
        if (KnockChance >= 0f) return KnockChance;
        MainWeaponController main = MainWeaponController.Current;
        return main != null && main.MainWeapon != null ? GimmickCatalog.Instance.KnockLeft(main.MainWeapon.id) : 0f;
    }

    // 능력치 「무기·총알 크기」: 기본 공격(보조무기, 메인무기 기본 공격)만 ApplyWeaponSize로 켬
    private bool sizeScaled;
    private float appliedSize = 1f;
    private float orbitSize = 1f;

    public void ApplyWeaponSize()
    {
        sizeScaled = true;
        orbitSize = RunStats.Get(RunStat.WeaponSize);                 // 보조 쌍검: 배치 때 이 크기만큼 바깥에 놓임 (Weapon.DualBlades)
        Resize(RunStats.Get(RunStat.WeaponSize) * TwinSize);
    }

    private void Resize(float size)
    {
        if (Mathf.Approximately(size, appliedSize))
            return;
        transform.localScale *= size / appliedSize;
        appliedSize = size;
    }

    // 크기 배율을 걷어 내고 원래 크기로 (풀에 돌아가거나 다른 용도로 다시 쓸 때)
    private void ClearSize()
    {
        sizeScaled = false;
        Resize(1f);
    }

    private void LateUpdate()
    {
        if (sizeScaled)
        {
            Resize(RunStats.Get(RunStat.WeaponSize) * TwinSize);   // 계속 붙어 있는 근접 무기는 능력치·쌍둥이 무기가 바뀌면 바로 반영
            // 보조 쌍검은 크기가 바뀌면 캐릭터와의 거리도 같은 비율로 (손잡이가 캐릭터 밖에 있도록)
            float ws = RunStats.Get(RunStat.WeaponSize);
            if (OrbitScaled && !Mathf.Approximately(ws, orbitSize) && orbitSize > 0f)
            {
                transform.localPosition *= ws / orbitSize;
                orbitSize = ws;
            }
        }
    }

    private void OnDisable()
    {
        ClearSize();
    }

    Rigidbody2D rigid;

    // 이미 관통 횟수를 소모한 대상 (같은 적에게 두 번 소모하지 않도록)
    private readonly HashSet<int> hitTargets = new HashSet<int>();

    // 같은 적 재타격 간격 (초, 0이면 제한 없음). 보조무기 랜스 돌진 모드에서 좌우 연타로 피해가 쌓이는 것을 막음
    public float RehitInterval { get; set; }
    private readonly Dictionary<int, float> lastHitTime = new Dictionary<int, float>();

    // 이 적에게 지금 피해를 줄 수 있는지 (가능하면 시각을 기록)
    public bool TryHitTarget(GameObject target)
    {
        if (RehitInterval <= 0f)
            return true;
        int id = target.GetInstanceID();
        if (lastHitTime.TryGetValue(id, out float last) && Time.time - last < RehitInterval)
            return false;
        lastHitTime[id] = Time.time;
        return true;
    }

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    // 발사마다 새 번호 (풀에서 다시 꺼낸 발사체를 이전 발사와 구분: 생명의 일격 "판정 1개")
    private static int shotCounter;
    public int ShotId { get; private set; }

    public void Init(float damage, int per, Vector3 dir)
    {
        ShotId = ++shotCounter;
        this.damage = damage;
        this.per = per;
        hitTargets.Clear();
        SubWeapon = false;
        KnockChance = -1f;
        VolleyId = 0;
        TwinBlade = false;
        OrbitScaled = false;
        ClearSize();

        if (per > -1 && Weapon_num == 1)        // 화살 속도
        {
            rigid.velocity = dir * 15f; 
        }
        else if (per > -1 && Weapon_num == 2)   // 헤보건 탄환 속도
        {
            rigid.velocity = dir * 25f; 
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 보스에게 맞은 발사체도 잡몹과 동일하게 관통 횟수를 소모하여, 맞았는데 그대로 지나가 보이지 않도록 함
        if (!collision.CompareTag("Enemy") && !collision.CompareTag("Boss"))
            return;

        RegisterHit(collision.gameObject);
    }

    // 적에게 맞았을 때 관통 횟수 소모 (적 쪽에서도 호출)
    // 한 방에 죽은 적은 그 자리에서 콜라이더가 꺼져 발사체 쪽 충돌 이벤트가 오지 않을 수 있어,
    // 피해를 준 적 쪽에서도 알려 준다. 같은 적은 한 번만 소모한다.
    // 적에게 피해를 준 순간 (시너지 효과 등에서 사용)
    public event System.Action<GameObject> Hit;

    public void NotifyHit(GameObject target)
    {
        Hit?.Invoke(target);
    }

    public void RegisterHit(GameObject target)
    {
        if (per < 0 || !gameObject.activeSelf)        // -1: 무한 관통, -100: 근접 무기
            return;
        if (!hitTargets.Add(target.GetInstanceID()))
            return;

        per--;

        if (per == -1)
        {
            if (rigid != null)
            {
                rigid.velocity = Vector2.zero;
            }
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Area") || per == -100)
            return;

        gameObject.SetActive(false);
    }
}
