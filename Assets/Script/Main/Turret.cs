using UnityEngine;

// 포탑 1기: 대상 고르기·회전·공격 (TurretSystem이 만듦). 물리 충돌 없음
// 대상: 석궁·끈적이·화룡포 = 사거리 안에서 경로 진행도가 가장 큰(입구에 가장 가까운) 밤 몬스터
//       발리스타 = 체력이 가장 높은 밤 몬스터 (엘리트 우선)
public class Turret : MonoBehaviour
{
    public TurretConfig.Kind Kind { get; private set; }

    private TurretConfig cfg;
    private TurretConfig.TurretType type;
    private SpriteRenderer baseRenderer, headRenderer, sleepRenderer;
    private Transform head;
    private bool awake;
    private float nextFire;
    private float attackStart = -1f;
    private float popStart;
    private LineRenderer rangeLine;

    public static Turret Create(TurretConfig.Kind kind, Vector2 center, TurretConfig cfg)
    {
        TurretConfig.TurretType t = cfg.Get(kind);
        GameObject go = new GameObject("Turret " + t.name);
        go.transform.position = center;
        Turret turret = go.AddComponent<Turret>();
        turret.Kind = kind;
        turret.cfg = cfg;
        turret.type = t;
        turret.Build();
        return turret;
    }

    private void Build()
    {
        string layer = SortingLayer.NameToID("Player") != 0 ? "Player" : "Default";
        // 받침대 (회전 없음), 회전 머리 또는 본체, 휴면 천막
        float footY = -cfg.padHalfSize * 0.55f + type.footOffset;   // 발판 가운데보다 조금 아래가 바닥 (포탑별 보정)
        if (type.baseSprite != null)
        {
            baseRenderer = NewRenderer("Base", transform, type.baseSprite, layer, 0);
            baseRenderer.transform.localPosition = new Vector3(0f, footY, 0f);
            baseRenderer.transform.localScale = Vector3.one * type.scale;
        }
        head = new GameObject("Head").transform;
        head.SetParent(transform, false);
        head.localPosition = new Vector3(0f, footY, 0f) + (Vector3)(type.headOffset * type.scale);
        headRenderer = NewRenderer("Sprite", head, First(type.idle), layer, 1);
        headRenderer.transform.localScale = Vector3.one * type.scale;
        sleepRenderer = NewRenderer("Sleep", transform, type.sleep, layer, 1);
        sleepRenderer.transform.localPosition = new Vector3(0f, footY, 0f);
        sleepRenderer.transform.localScale = Vector3.one * type.scale;
        popStart = Time.time;
        transform.localScale = Vector3.zero;                 // 등장 연출: 0 → 1
        nextFire = Time.time + 0.5f;
    }

    private static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, string layer, int order)
    {
        SpriteRenderer r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(parent, false);
        r.sprite = sprite;
        r.sortingLayerName = layer;
        r.sortingOrder = order;
        return r;
    }

    private static Sprite First(Sprite[] s) => s != null && s.Length > 0 ? s[0] : null;

    // 활성(귀환 준비 ~ 밤) / 휴면(낮, 천막)
    public void SetAwake(bool on)
    {
        awake = on;
        if (baseRenderer != null) baseRenderer.enabled = on;
        headRenderer.enabled = on;
        sleepRenderer.enabled = !on;
        attackStart = -1f;
        nextFire = Time.time + 0.5f;
    }

    private void Update()
    {
        float pop = Mathf.Clamp01((Time.time - popStart) / Mathf.Max(0.01f, cfg.buildPopTime));
        transform.localScale = Vector3.one * pop;
        UpdateRangeLine();

        if (!awake || GameManager.instance == null || !GameManager.instance.isLive)
            return;

        Enemy target = FindTarget();
        if (target != null && type.rotates)
            Aim((Vector2)target.transform.position - (Vector2)head.position);
        else if (target != null)
        {
            // 회전 없는 본체(끈적이): 대상 쪽을 바라보도록 좌우 반전
            bool left = target.transform.position.x < transform.position.x;
            headRenderer.flipX = type.spriteFacesRight ? left : !left;
        }
        Animate();

        if (target != null && Time.time >= nextFire)
        {
            nextFire = Time.time + type.interval;
            Fire(target);
        }
    }

    private Enemy FindTarget()
    {
        Enemy best = null;
        float bestScore = float.MinValue;
        Vector2 pos = transform.position;
        float range2 = type.range * type.range;
        bool hasMap = OffscreenSpawn.TryGetMapRect(out Rect mapRect);
        foreach (Enemy e in NightDefense.NightEnemies)
        {
            if (e == null || !e.IsAlive || !e.NightWave || e.Hiding || !e.gameObject.activeInHierarchy)
                continue;                                    // 꺼진 풀 오브젝트·사라지는 중인 몬스터 제외
            if (hasMap && !mapRect.Contains(e.transform.position))
                continue;                                    // 맵 벽 밖(숲 속 소환 지점 등)은 조준하지 않음
            if (((Vector2)e.transform.position - pos).sqrMagnitude > range2)
                continue;
            float score = Kind == TurretConfig.Kind.Ballista
                ? (e.BossUnit ? 1000000f : 0f) + e.health      // 대장 몬스터 우선, 그다음 체력
                : e.PathProgress;                               // 입구에 가장 가까운 적
            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    // 머리가 대상 쪽을 봄 (그림은 오른쪽을 봄, 왼쪽을 볼 때는 뒤집히지 않게 세로 반전)
    private void Aim(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        head.localRotation = Quaternion.Euler(0f, 0f, angle);
        headRenderer.flipY = dir.x < 0f;
    }

    private void Animate()
    {
        Sprite[] frames;
        int i;
        if (attackStart >= 0f && type.attack != null && type.attack.Length > 0)
        {
            i = Mathf.FloorToInt((Time.time - attackStart) * cfg.attackFps);
            if (i >= type.attack.Length) { attackStart = -1f; Animate(); return; }
            frames = type.attack;
        }
        else
        {
            frames = type.idle;
            // 대기: 기본은 첫 장에 멈춤 (두 장을 번갈아 보이면 생물처럼 꾸물거림)
            i = cfg.idleAnimate && frames != null && frames.Length > 0 ? Mathf.FloorToInt(Time.time * cfg.idleFps) % frames.Length : 0;
        }
        if (frames != null && frames.Length > 0 && frames[i] != null) headRenderer.sprite = frames[i];
    }

    private float Damage(float mul) => cfg.turretBaseDamage * Character.Damage * mul;   // 상점·젬·버프·아침 선택지 미적용

    private void Fire(Enemy target)
    {
        attackStart = Time.time;
        if (type.fireSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(type.fireSound);
        Vector2 dir = ((Vector2)target.transform.position - (Vector2)head.position).normalized;
        Vector2 muzzle = (Vector2)head.position + (type.rotates ? dir * type.muzzleDistance * type.scale / 0.6f : Vector2.up * 0.4f);
        switch (Kind)
        {
            case TurretConfig.Kind.Crossbow:
                TurretProjectile.Arrow(type, cfg, muzzle, target, Damage(type.damageMul));
                break;
            case TurretConfig.Kind.Sticky:
                TurretProjectile.Blob(type, cfg, muzzle, target.transform.position, Damage(type.damageMul));
                break;
            case TurretConfig.Kind.Ballista:
                TurretProjectile.Bolt(type, cfg, muzzle, dir, type.range, Damage(type.damageMul));
                break;
            case TurretConfig.Kind.Dragon:
                TurretProjectile.Fireball(type, cfg, muzzle, target.transform.position, Damage(type.damageMul), Damage(cfg.burnDps));
                break;
        }
    }

    // [임시] 테스트 모드 사거리 원
    private void UpdateRangeLine()
    {
        bool show = TurretSystem.ShowRange && awake;
        if (show && rangeLine == null)
        {
            rangeLine = new GameObject("Range").AddComponent<LineRenderer>();
            rangeLine.transform.SetParent(transform, false);
            rangeLine.useWorldSpace = false;
            rangeLine.loop = true;
            rangeLine.positionCount = 48;
            rangeLine.widthMultiplier = 0.06f;
            rangeLine.material = new Material(Shader.Find("Sprites/Default"));
            rangeLine.startColor = rangeLine.endColor = new Color(1f, 0.9f, 0.3f, 0.7f);
            rangeLine.sortingLayerName = cfg.sortingLayer;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                rangeLine.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * type.range);
            }
        }
        if (rangeLine != null && rangeLine.enabled != show) rangeLine.enabled = show;
    }
}
