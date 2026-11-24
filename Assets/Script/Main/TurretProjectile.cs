using System.Collections.Generic;
using UnityEngine;

// 포탑 투사체·효과 (물리 충돌 없이 위치로 판정, 밤 웨이브 몬스터만, Enemy.TakeTurretDamage로 피해)
// 석궁 화살(단일 명중) / 끈적이 덩어리(포물선 → 튀김 → 감속 장판) / 발리스타 대형 화살(일직선 무한 관통) / 화룡포 불덩이(포물선 → 폭발·화상 → 바닥 불꽃)
public class TurretProjectile : MonoBehaviour
{
    private enum Mode { Arrow, Blob, Bolt, Fireball }

    private Mode mode;
    private TurretConfig cfg;
    private TurretConfig.TurretType type;
    private SpriteRenderer sr;
    private Enemy target;
    private int targetSerial;                                // 쏠 때 대상의 소환 번호 (죽어서 풀에서 다시 나오면 다른 몬스터)
    private Vector2 from, to, dir;
    private float damage, burnDps;
    private float start, duration, travel;
    private readonly HashSet<Enemy> pierced = new HashSet<Enemy>();

    private const float HitRadius = 0.55f;                   // 화살·대형 화살 명중 판정 반경

    private static TurretProjectile Spawn(Mode mode, TurretConfig.TurretType t, TurretConfig cfg, Vector2 from)
    {
        GameObject go = new GameObject("Turret " + mode);
        go.transform.position = from;
        TurretProjectile p = go.AddComponent<TurretProjectile>();
        p.mode = mode;
        p.type = t;
        p.cfg = cfg;
        p.from = from;
        p.start = Time.time;
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sortingLayerName = cfg.sortingLayer;
        p.sr.sortingOrder = 10;
        Sprite first = t.projectile != null && t.projectile.Length > 0 ? t.projectile[0] : null;
        p.sr.sprite = first;
        if (first != null) go.transform.localScale = Vector3.one * (t.projectileSize / Mathf.Max(0.01f, first.bounds.size.x));
        return p;
    }

    public static void Arrow(TurretConfig.TurretType t, TurretConfig cfg, Vector2 from, Enemy target, float damage)
    {
        TurretProjectile p = Spawn(Mode.Arrow, t, cfg, from);
        p.target = target;
        p.targetSerial = target.SpawnSerial;
        p.to = target.transform.position;
        p.damage = damage;
    }

    public static void Blob(TurretConfig.TurretType t, TurretConfig cfg, Vector2 from, Vector2 to, float damage)
    {
        TurretProjectile p = Spawn(Mode.Blob, t, cfg, from);
        p.to = to;
        p.damage = damage;
        p.duration = t.flightTime;
    }

    public static void Bolt(TurretConfig.TurretType t, TurretConfig cfg, Vector2 from, Vector2 dir, float range, float damage)
    {
        TurretProjectile p = Spawn(Mode.Bolt, t, cfg, from);
        p.dir = dir;
        p.travel = range;
        p.damage = damage;
        p.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    public static void Fireball(TurretConfig.TurretType t, TurretConfig cfg, Vector2 from, Vector2 to, float damage, float burnDps)
    {
        TurretProjectile p = Spawn(Mode.Fireball, t, cfg, from);
        p.to = to;
        p.damage = damage;
        p.burnDps = burnDps;
        p.duration = t.flightTime;
    }

    private void Update()
    {
        AnimateSelf();
        switch (mode)
        {
            case Mode.Arrow: UpdateArrow(); break;
            case Mode.Bolt: UpdateBolt(); break;
            default: UpdateArc(); break;
        }
    }

    private void AnimateSelf()
    {
        Sprite[] f = type.projectile;
        if (f == null || f.Length < 2) return;
        int i = Mathf.FloorToInt((Time.time - start) * cfg.fxFps) % f.Length;
        if (f[i] != null) sr.sprite = f[i];
    }

    // 석궁: 대상을 따라가 한 마리만 맞힘 (대상이 사라지면 마지막 위치까지)
    private void UpdateArrow()
    {
        if (target != null && (!target.IsAlive || !target.NightWave || target.SpawnSerial != targetSerial)) target = null;   // 대상이 죽거나 다시 소환됐으면 마지막 위치까지만 (길 시작점으로 휘어 날아가지 않게)
        if (target != null) to = target.transform.position;
        Vector2 pos = transform.position;
        Vector2 d = to - pos;
        float step = type.projectileSpeed * Time.deltaTime;
        if (d.magnitude <= Mathf.Max(step, HitRadius * 0.5f))
        {
            if (target != null)
            {
                target.TakeTurretDamage(damage);
                Fx(type.hitFx, to, type.fxSize, 0f);
            }
            Destroy(gameObject);
            return;
        }
        transform.position = pos + d.normalized * step;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // 발리스타: 사거리 끝까지 일직선, 맞은 적마다 한 번씩 (무한 관통)
    private void UpdateBolt()
    {
        float step = type.projectileSpeed * Time.deltaTime;
        transform.position += (Vector3)(dir * step);
        travel -= step;
        Vector2 pos = transform.position;
        foreach (Enemy e in new List<Enemy>(NightDefense.NightEnemies))
        {
            if (e == null || !e.IsAlive || !e.NightWave || pierced.Contains(e)) continue;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > HitRadius * HitRadius) continue;
            pierced.Add(e);
            Fx(type.hitFx, e.transform.position, type.fxSize, 0f);   // 관통 효과
            e.TakeTurretDamage(damage);
        }
        if (travel <= 0f) Destroy(gameObject);
    }

    // 끈적이·화룡포: 포물선으로 날아가 착지
    private void UpdateArc()
    {
        float k = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, duration));
        float height = Mathf.Max(1f, Vector2.Distance(from, to) * 0.35f);
        Vector2 p = Vector2.Lerp(from, to, k) + Vector2.up * height * 4f * k * (1f - k);
        Vector2 vel = (to - from) + Vector2.up * height * 4f * (1f - 2f * k);
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
        if (k < 1f) return;

        if (mode == Mode.Blob)
        {
            // 착지 튀김 + 반경 안 피해 + 감속 장판
            Fx(type.hitFx, to, type.fxSize, 0f);
            foreach (Enemy e in InRadius(to, cfg.slowRadius)) e.TakeTurretDamage(damage);
            SlowField.Create(type, cfg, to);
        }
        else
        {
            // 착탄 폭발 + 반경 피해 + 화상, 바닥 불꽃 (연출만)
            Fx(type.hitFx, to, type.fxSize, 0f);
            if (cfg.explodeSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(cfg.explodeSound);
            foreach (Enemy e in InRadius(to, cfg.explodeRadius))
            {
                e.TakeTurretDamage(damage);
                if (e.IsAlive) e.ApplyTurretBurn(burnDps, cfg.burnTime);
            }
            Fx(type.groundFx, to, cfg.explodeRadius * 1.1f, cfg.flameTime);
        }
        Destroy(gameObject);
    }

    public static List<Enemy> InRadius(Vector2 center, float radius)
    {
        List<Enemy> list = new List<Enemy>();
        foreach (Enemy e in NightDefense.NightEnemies)
        {
            if (e == null || !e.IsAlive || !e.NightWave) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude <= radius * radius) list.Add(e);
        }
        return list;
    }

    // 효과: frames를 한 번 재생 (loopTime > 0 이면 그 시간 동안 반복)
    public static void Fx(Sprite[] frames, Vector2 pos, float width, float loopTime)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return;
        TurretConfig cfg = TurretConfig.Instance;
        GameObject go = new GameObject("Turret Fx");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, frames[0].bounds.size.x));
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sortingLayerName = cfg.sortingLayer;
        r.sortingOrder = loopTime > 0f ? 2 : 11;
        TurretFxPlayer fx = go.AddComponent<TurretFxPlayer>();
        fx.Play(r, frames, cfg.fxFps, loopTime);
    }
}

// 효과 프레임 재생 (반복 시간이 있으면 그동안 반복 후 서서히 사라짐)
public class TurretFxPlayer : MonoBehaviour
{
    private SpriteRenderer r;
    private Sprite[] frames;
    private float fps, loop, start;

    public void Play(SpriteRenderer renderer, Sprite[] f, float framesPerSecond, float loopTime)
    {
        r = renderer; frames = f; fps = framesPerSecond; loop = loopTime; start = Time.time;
        r.sprite = f[0];
    }

    private void Update()
    {
        float t = Time.time - start;
        int i = Mathf.FloorToInt(t * fps);
        if (loop <= 0f)
        {
            if (i >= frames.Length) { Destroy(gameObject); return; }
            r.sprite = frames[i];
            return;
        }
        r.sprite = frames[i % frames.Length];
        float fade = Mathf.Clamp01((t - loop + 0.4f) / 0.4f);
        r.color = new Color(1f, 1f, 1f, 1f - fade);
        if (t >= loop) Destroy(gameObject);
    }
}

// 끈적이 감속 장판: 퍼짐 → 유지 → 사라짐, 안에 있는 밤 몬스터 경로 이동 감속 (중첩 없이 가장 강한 감속)
public class SlowField : MonoBehaviour
{
    private TurretConfig cfg;
    private TurretConfig.TurretType type;
    private SpriteRenderer r;
    private float start;

    public static void Create(TurretConfig.TurretType t, TurretConfig cfg, Vector2 pos)
    {
        GameObject go = new GameObject("Slow Field");
        go.transform.position = pos;
        SlowField f = go.AddComponent<SlowField>();
        f.cfg = cfg;
        f.type = t;
        f.start = Time.time;
        f.r = go.AddComponent<SpriteRenderer>();
        f.r.sortingLayerName = "ForeGround";
        f.r.sortingOrder = 4;                                // 땅 타일 위, 캐릭터 아래
        Sprite s = t.groundFx != null && t.groundFx.Length > 0 ? t.groundFx[0] : null;
        f.r.sprite = s;
        if (s != null) go.transform.localScale = Vector3.one * (cfg.slowRadius * 2f / Mathf.Max(0.01f, s.bounds.size.x));
    }

    private void Update()
    {
        float t = Time.time - start;
        Sprite[] g = type.groundFx;
        if (g != null && g.Length >= 3)
            r.sprite = t < 0.25f ? g[0] : t < cfg.slowTime - 0.4f ? g[1] : g[2];   // 퍼짐 / 유지 / 사라짐
        foreach (Enemy e in TurretProjectile.InRadius(transform.position, cfg.slowRadius))
            e.ApplyTurretSlow(cfg.slowRate, 0.15f);              // 안에 있는 동안만 (나오면 곧 풀림)
        if (t >= cfg.slowTime) Destroy(gameObject);
    }
}
