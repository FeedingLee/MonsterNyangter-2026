using System.Collections.Generic;
using UnityEngine;

// 전투 기믹 카드의 매 프레임 처리와 표시 (씬 배치 없이 GimmickCards.ResetRun에서 자동 생성)
// - 보호막: 캐릭터 둘레 푸른 막 (랜스 패링과 다른 모양)
// - 냉기 발자국: 느려진 몬스터 발밑 서리 고리
// - 위기 탈출: 밀쳐내기 충격파 + 무적 동안 캐릭터 깜빡임
// - 생명의 일격: 준비되면 메인무기가 붉게 빛남
public class GimmickRunner : MonoBehaviour
{
    private static GimmickRunner instance;

    public static void Ensure()
    {
        if (instance == null)
        {
            instance = new GameObject("Gimmick Runner").AddComponent<GimmickRunner>();
        }
    }

    private SpriteFxAnimator shield;
    private readonly Dictionary<Enemy, SpriteFxAnimator> frost = new Dictionary<Enemy, SpriteFxAnimator>();
    private readonly List<Enemy> removeList = new List<Enemy>();
    private readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
    private bool weaponTinted;
    private GameObject counterAura;
    private GimmickBuffBar buffBar;

    // 냉기 발자국에 걸린 몬스터 몸 색 (원래 색을 기억했다가 풀리면 되돌림)
    private class FrostTint { public SpriteRenderer renderer; public Color baseColor; public Color applied; }
    private readonly Dictionary<Enemy, FrostTint> frostTints = new Dictionary<Enemy, FrostTint>();
    private readonly HashSet<Enemy> slowedNow = new HashSet<Enemy>();

    private int lastTwinLevel;

    private void Update()
    {
        int twin = ExtraCards.Level(ItemData.ItemType.TwinWeapon);
        if (twin != lastTwinLevel && GameManager.instance != null && GameManager.instance.player != null)
        {
            lastTwinLevel = twin;
            foreach (Weapon w in GameManager.instance.player.GetComponentsInChildren<Weapon>()) w.RefreshTwinBlades();
        }
        GimmickCards.Tick(this);
        UpdateShield();
        UpdateEscapeBlink();
        UpdateLifeGlow();
        UpdateCounterAura();
        if (buffBar == null) buffBar = GimmickBuffBar.Create();
    }

    private void LateUpdate()
    {
        UpdateFrostTint();
    }

    // 반격 본능 발동 중: 캐릭터 둘레 주황빛 오라
    private void UpdateCounterAura()
    {
        GameManager gm = GameManager.instance;
        bool on = GimmickCards.CounterInstinctActive && gm != null && gm.player != null;
        if (on && counterAura == null)
        {
            counterAura = SpriteFx.ChargeAura(gm.player.transform, GimmickCatalog.Instance.counterInstinctAuraSize);
            if (counterAura != null) SpriteFx.TintAura(counterAura, GimmickCatalog.Instance.counterInstinctColor);
        }
        else if (!on && counterAura != null)
        {
            SpriteFx.StopAura(counterAura);
            counterAura = null;
        }
    }

    private void UpdateFrostTint()
    {
        Color tint = GimmickCatalog.Instance.frostBodyTint;
        foreach (Enemy e in slowedNow)
        {
            if (e == null) continue;
            if (!frostTints.TryGetValue(e, out FrostTint t))
            {
                SpriteRenderer r = e.GetComponent<SpriteRenderer>();
                if (r == null) continue;
                t = new FrostTint { renderer = r, baseColor = r.color };
                frostTints[e] = t;
            }
            else if (t.renderer.color != t.applied)
            {
                t.baseColor = t.renderer.color;                       // 다른 효과(엘리트·처벌 등)가 색을 바꿨으면 그 색을 기준으로
            }
            t.applied = t.baseColor * tint;
            t.applied.a = t.baseColor.a;
            t.renderer.color = t.applied;
        }

        removeList.Clear();
        foreach (KeyValuePair<Enemy, FrostTint> pair in frostTints)
        {
            if (pair.Key != null && slowedNow.Contains(pair.Key) && pair.Key.IsAlive) continue;
            FrostTint t = pair.Value;
            if (t.renderer != null && t.renderer.color == t.applied) t.renderer.color = t.baseColor;   // 풀리면 원래 색
            removeList.Add(pair.Key);
        }
        foreach (Enemy e in removeList) frostTints.Remove(e);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static SpriteRenderer PlayerSprite => GameManager.instance != null && GameManager.instance.player != null
        ? GameManager.instance.player.GetComponent<SpriteRenderer>() : null;

    private SpriteRenderer MakeCircle(string name, Transform parent, Color color, float size, int orderOffset)
    {
        SpriteRenderer ps = PlayerSprite;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = CombatUtil.Circle;
        r.color = color;
        if (ps != null)
        {
            r.sortingLayerID = ps.sortingLayerID;
            r.sortingOrder = ps.sortingOrder + orderOffset;
        }
        return r;
    }

    // 보호막: 준비되면 캐릭터 둘레에 은은하게 숨 쉬는 푸른 막
    private void UpdateShield()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null)
            return;
        bool show = GimmickCards.ShieldUp;
        GimmickCatalog c = GimmickCatalog.Instance;
        if (show && shield == null)
        {
            Sprite[] frames = c.shieldFrames;
            if (frames != null && frames.Length > 0)
            {
                shield = SpriteFx.Loop(frames, gm.player.transform, new Vector3(0f, 0.35f, 0f), c.shieldSize, c.shieldFps, -1f, 2, c.shieldColor);
            }
            else
            {
                SpriteRenderer circle = MakeCircle("Gimmick Shield", gm.player.transform, c.shieldColor, c.shieldSize, 2);
                shield = circle.gameObject.AddComponent<SpriteFxAnimator>();
                shield.InitLoop(circle, new[] { circle.sprite }, c.shieldSize, 1f, -1f);
            }
        }
        else if (!show && shield != null)
        {
            SpriteFx.Stop(shield);
            shield = null;
        }
        if (shield != null)
        {
            SpriteRenderer body = PlayerSprite;
            if (body != null) shield.transform.position = body.bounds.center;   // 캐릭터 몸 정가운데에서 감싸도록
            SpriteRenderer r = shield.GetComponent<SpriteRenderer>();
            if (r != null)
            {
                Color col = c.shieldColor;
                col.a *= 1f - c.shieldPulse * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f));   // 크기는 그대로, 투명도로만 숨쉬기
                r.color = col;
            }
        }
    }

    // 냉기 발자국: 느려진 몬스터 발밑에만 서리 고리
    internal void UpdateVisuals(HashSet<Enemy> slowed)
    {
        GimmickCatalog c = GimmickCatalog.Instance;
        slowedNow.Clear();
        foreach (Enemy e in slowed) slowedNow.Add(e);
        foreach (Enemy e in slowed)
        {
            if ((!frost.TryGetValue(e, out SpriteFxAnimator a) || a == null) && c.frostFrames != null && c.frostFrames.Length > 0)
            {
                SpriteFxAnimator a2 = SpriteFx.Loop(c.frostFrames, e.transform, new Vector3(0f, c.frostOffsetY, 0f), c.frostSize, c.frostFps, -1f, -1, c.frostColor);
                SpriteRenderer es = e.GetComponent<SpriteRenderer>();
                SpriteRenderer fr = a2 != null ? a2.GetComponent<SpriteRenderer>() : null;
                if (fr != null && es != null)
                {
                    fr.sortingLayerID = es.sortingLayerID;             // 몬스터와 같은 층, 바로 뒤
                    fr.sortingOrder = es.sortingOrder - 1;
                }
                frost[e] = a2;
            }
        }

        removeList.Clear();
        foreach (KeyValuePair<Enemy, SpriteFxAnimator> pair in frost)
        {
            if (pair.Key == null || pair.Value == null || !slowed.Contains(pair.Key) || !pair.Key.IsAlive)
            {
                if (pair.Value != null) SpriteFx.Stop(pair.Value);
                removeList.Add(pair.Key);
            }
        }
        foreach (Enemy e in removeList) frost.Remove(e);
    }

    // 위기 탈출: 주변 몬스터를 밀쳐냄 + 충격파
    internal void EscapePush(Vector3 center)
    {
        GimmickCatalog c = GimmickCatalog.Instance;
        float radius = c.escapePushRadius * c.crisisPushRangeMul;
        float force = c.escapePushForce * c.crisisPushForceMul;
        SpriteFx.BossDustBurst(center + Vector3.down * 0.3f, radius * 1.2f);
        SpriteFx.StarBurst(center, radius * 0.8f);
        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            Enemy e = col.GetComponentInParent<Enemy>();
            if (e != null && e.IsAlive)
            {
                e.Push(((Vector2)(e.transform.position - center)).normalized, force);
            }
        }
    }

    // 무적 동안 캐릭터 깜빡임
    private bool blinking;

    private void UpdateEscapeBlink()
    {
        SpriteRenderer ps = PlayerSprite;
        if (ps == null)
            return;
        if (GimmickCards.EscapeInvincible)
        {
            blinking = true;
            Color col = ps.color;
            col.a = Mathf.Sin(Time.time * GimmickCatalog.Instance.escapeBlinkSpeed) > 0f ? 1f : 0.35f;
            ps.color = col;
        }
        else if (blinking)
        {
            blinking = false;
            Color col = ps.color;
            col.a = 1f;
            ps.color = col;
        }
    }

    // 생명의 일격 준비: 메인무기(없으면 보유 무기) 그림이 붉게 맥동
    private void UpdateLifeGlow()
    {
        bool ready = GimmickCards.LifeStrikeReady && GameManager.instance != null && GameManager.instance.isLive;
        if (!ready)
        {
            if (weaponTinted)
            {
                foreach (SpriteRenderer r in tinted) if (r != null) r.color = new Color(1f, 1f, 1f, r.color.a);
                tinted.Clear();
                weaponTinted = false;
            }
            return;
        }

        tinted.Clear();
        MainWeaponController main = MainWeaponController.Current;
        if (main != null)
        {
            tinted.AddRange(main.GetComponentsInChildren<SpriteRenderer>());
            if (main.HeldRenderer != null && !tinted.Contains(main.HeldRenderer)) tinted.Add(main.HeldRenderer);
        }
        else
        {
            foreach (Weapon w in GameManager.instance.player.GetComponentsInChildren<Weapon>())
                tinted.AddRange(w.GetComponentsInChildren<SpriteRenderer>());
        }
        Color glow = GimmickCatalog.Instance.lifeStrikeGlow;
        float k = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
        foreach (SpriteRenderer r in tinted)
        {
            if (r == null) continue;
            Color col = Color.Lerp(Color.white, glow, k);
            col.a = r.color.a;
            r.color = col;
        }
        weaponTinted = true;
    }
}
