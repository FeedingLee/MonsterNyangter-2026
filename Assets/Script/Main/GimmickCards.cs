using System.Collections.Generic;
using UnityEngine;

// 레벨업 전투 기믹 카드 효과 (회피 반격 ~ 생명의 일격)
// 카드 레벨은 ExtraCards가 만든 카드(Item.level)에서 읽고, 수치는 GimmickCatalog
// 피해 쪽 효과는 발사체·범위 피해가 적에게 들어가기 직전에 ModifyHit, 들어간 뒤 AfterHit로 처리한다.
// 보스에게 들어가는 피해도 기존 TakeDamage(1회 상한 적용)를 그대로 거친다.
public static class GimmickCards
{
    public enum TargetKind { Mob, Elite, MiniBoss, Boss }

    private static GimmickCatalog Cat => GimmickCatalog.Instance;

    public static int Level(ItemData.ItemType type) => ExtraCards.Level(type);

    private static float Table(float[] table, int level) =>
        level <= 0 || table == null || table.Length == 0 ? 0f : table[Mathf.Min(level, table.Length) - 1];

    private static int Table(int[] table, int level) =>
        level <= 0 || table == null || table.Length == 0 ? 0 : table[Mathf.Min(level, table.Length) - 1];

    // 기믹 카드의 최대 단계
    public static int MaxLevel(ItemData.ItemType type)
    {
        GimmickCatalog c = Cat;
        switch (type)
        {
            case ItemData.ItemType.DodgeCounter: return c.dodgeCounterBonus.Length;
            case ItemData.ItemType.CounterInstinct: return c.counterInstinctSpeed.Length;
            case ItemData.ItemType.Focus: return c.focusPerStack.Length;
            case ItemData.ItemType.ChainLightning: return Mathf.Min(c.chainHitsPer.Length, c.chainTargets.Length);
            case ItemData.ItemType.KillExplosion: return c.explodeChance.Length;
            case ItemData.ItemType.HuntInstinct: return c.huntBonus.Length;
            case ItemData.ItemType.TwinWeapon: return Mathf.Min(c.twinProjectiles.Length, c.twinBlades.Length);
            case ItemData.ItemType.Shield: return c.shieldInterval.Length;
            case ItemData.ItemType.FrostStep: return c.frostSlow.Length;
            case ItemData.ItemType.Escape: return Mathf.Max(1, c.escapeMaxLevel);
            case ItemData.ItemType.LifeStrike: return Mathf.Min(c.lifeKillsPer.Length, c.lifeStrikeRatio.Length);
            default: return 1;
        }
    }

    public static bool IsGimmick(ItemData.ItemType type) => System.Array.IndexOf(GimmickCatalog.Order, type) >= 0;
    public static bool IsNewGimmick(ItemData.ItemType type) => type >= ItemData.ItemType.DodgeCounter;

    public static Sprite Icon(ItemData.ItemType type)
    {
        int i = System.Array.IndexOf(GimmickCatalog.Order, type);
        Sprite[] icons = Cat.icons;
        return i >= 0 && icons != null && i < icons.Length ? icons[i] : null;
    }

    // ---------------------------------------------------------------
    // 상태 (한 판 동안). ExtraCards.CreateCards에서 초기화
    // ---------------------------------------------------------------
    private static float dodgeTime = -99f;
    private static readonly NextStrike dodgeStrike = new NextStrike();   // 회피 반격을 쓴 판정
    private static readonly NextStrike lifeStrike = new NextStrike();    // 생명의 일격을 쓴 판정

    // "다음 공격 1회 강화" 계열 공통 판정: 준비된 뒤 처음 적중한 판정 1개
    // - 관통 투사체면 그 1발(같은 ShotId)이 맞힌 적 모두, 근접 무기·범위 공격이면 같은 순간 맞은 적 모두
    // - 한 번에 여러 발을 쏘면 처음 맞은 1발만
    private class NextStrike
    {
        private object source;
        private int shotId;
        private int frame = -1;

        public void Reset() { source = null; shotId = 0; frame = -1; }

        // 준비된 상태에서 첫 적중: 이 판정을 기억
        public void Take(object hitSource)
        {
            source = hitSource;
            shotId = hitSource is Bullet b ? b.ShotId : 0;
            frame = Time.frameCount;
        }

        // 이미 쓴 그 판정이 아직 이어지는지
        public bool Continues(object hitSource)
        {
            if (frame < 0)
                return false;
            if (hitSource != null && ReferenceEquals(hitSource, source) && hitSource is Bullet b)
            {
                bool melee = b.GetComponentInParent<Weapon>() != null || b.GetComponentInParent<MainWeaponController>() != null;
                if (melee) return frame == Time.frameCount;
                return b.ShotId == shotId && b.gameObject.activeInHierarchy;
            }
            return frame == Time.frameCount && (hitSource == null || ReferenceEquals(hitSource, source));
        }
    }
    private static float counterInstinctEnd;
    private static float lastHitTakenTime;
    private static int chainHits;
    private static bool inGimmickDamage;          // 번개·폭발 피해 중 (다시 번개·폭발을 부르지 않게)
    private static bool lifeReady;
    private static float lifeNextReady;
    private static int lifeKills;
    private static float shieldReadyTime;
    private static bool shieldUp;
    private static float escapeEnd;
    private static bool escapeUsed;
    private static readonly HashSet<Enemy> slowed = new HashSet<Enemy>();

    public static void ResetRun()
    {
        dodgeTime = -99f;
        dodgeStrike.Reset();
        lifeStrike.Reset();
        counterInstinctEnd = 0f;
        lastHitTakenTime = Time.time;
        chainHits = 0;
        inGimmickDamage = false;
        lifeReady = false;
        lifeNextReady = 0f;
        lifeKills = 0;
        shieldReadyTime = 0f;
        shieldUp = false;
        escapeEnd = 0f;
        escapeUsed = false;
        slowed.Clear();
        GimmickRunner.Ensure();
    }

    // ---------------------------------------------------------------
    // 플레이어 쪽 배율 (PlayerBuff, DayNightCycle에서 읽음)
    // ---------------------------------------------------------------
    // 반격 본능: 우클릭 기술 후 공격속도
    public static float AttackSpeedScale =>
        Time.time < counterInstinctEnd ? 1f + Table(Cat.counterInstinctSpeed, Level(ItemData.ItemType.CounterInstinct)) : 1f;

    // 집중: 피격 없이 버틴 시간만큼 중첩
    public static int FocusStacks
    {
        get
        {
            if (Level(ItemData.ItemType.Focus) <= 0) return 0;
            return Mathf.Min(Cat.focusMaxStacks, Mathf.FloorToInt((Time.time - lastHitTakenTime) / Mathf.Max(0.1f, Cat.focusInterval)));
        }
    }

    public static float FocusBonus => FocusStacks * Table(Cat.focusPerStack, Level(ItemData.ItemType.Focus));

    // 항상 붙는 피해 증가의 합 (공격력 능력치 + 한계 돌파 + 음식 + 집중). 최종 = 기본 x (1 + 합)
    public static float IncreaseSum =>
        (RunStats.Get(RunStat.Damage) - 1f) +
        (ExtraCards.LastStandActive ? ExtraCards.LastStandBonus : 0f) +
        (ExtraCards.FoodActive ? RunConfig.Instance.foodDamage : 0f) +
        FocusBonus;

    // 이미 (1 + 합)이 곱해진 피해에 증가분을 더 합산 (곱하지 않고 더함): 기본 x (1 + 합 + extra)
    public static float AddIncrease(float damage, float extra)
    {
        if (extra == 0f) return damage;
        float sum = 1f + IncreaseSum;
        return sum <= 0.0001f ? damage : damage * (sum + extra) / sum;
    }

    private static float HuntBonus(TargetKind kind) =>
        kind == TargetKind.Elite || kind == TargetKind.MiniBoss ? Table(Cat.huntBonus, Level(ItemData.ItemType.HuntInstinct)) : 0f;

    // 각성 시너지 피해: 피해 증가만 (회피 반격·생명의 일격 소모 없음, 번개·폭발 없음)
    public static float SynergyDamage(float damage, TargetKind kind) => damage * (1f + IncreaseSum + HuntBonus(kind));

    // 처치 폭발 기준에서 뺄 생명의 일격 추가분 (바로 직전 ModifyHit에서 더한 값)
    private static float lastLifeBonus;

    // 생명의 일격: 처치 수로 오른 최대 체력 배율 (다른 체력 효과 적용 후 곱함)
    public static float MaxHealthScale
    {
        get
        {
            int per = Table(Cat.lifeKillsPer, Level(ItemData.ItemType.LifeStrike));
            if (per <= 0) return 1f;
            return 1f + Mathf.Min(Cat.lifeMaxHpCap, (lifeKills / per) * Cat.lifeMaxHpStep);
        }
    }

    public static bool LifeStrikeReady => lifeReady && Level(ItemData.ItemType.LifeStrike) > 0;
    public static bool EscapeInvincible => Time.time < escapeEnd;
    public static bool EscapeAvailable => !escapeUsed;            // 이번 낮·밤에 아직 안 씀 (표시용)
    public static bool CounterInstinctActive => Time.time < counterInstinctEnd;
    public static bool ShieldUp => shieldUp && Level(ItemData.ItemType.Shield) > 0;

    // ---------------------------------------------------------------
    // 알림 (Player, MainWeaponController에서 호출)
    // ---------------------------------------------------------------
    public static void OnDodge()
    {
        dodgeTime = Time.time;
        dodgeReady = true;
    }

    private static bool dodgeReady;

    // 회피 반격 준비 중 (구르기 직후 창 안, 아직 안 씀) - 표시용
    public static bool DodgeCounterReady => dodgeReady && Time.time - dodgeTime <= Cat.dodgeCounterWindow && Level(ItemData.ItemType.DodgeCounter) > 0;

    // 무기 5종의 우클릭 기술 (쌍검 백대시, 활 부채꼴, 헤비보우건 탄 변경, 대검 저스트 입력, 랜스 가드)
    public static void OnRightClickSkill()
    {
        if (Level(ItemData.ItemType.CounterInstinct) > 0)
        {
            counterInstinctEnd = Time.time + Cat.counterInstinctDuration;
            Transform p = GameManager.instance != null ? GameManager.instance.player.transform : null;
            if (p != null) SpriteFx.BoostSpark(p.position + Vector3.up * 0.4f, 1.6f);   // 발동 순간 금빛 반짝임 (지속 중에는 GimmickRunner가 오라 표시)
        }
    }

    // 보호막: 피해 1회를 막으면 true (Player.TryBlockHit, 말벌 접촉)
    public static bool TryShield()
    {
        if (!ShieldUp)
            return false;
        shieldUp = false;
        shieldReadyTime = Time.time + Table(Cat.shieldInterval, Level(ItemData.ItemType.Shield));
        Transform p = GameManager.instance != null ? GameManager.instance.player.transform : null;
        if (p != null) SpriteFx.StarBurst(p.position, 1.2f);       // 막아 낸 순간 깨지는 빛
        return true;
    }

    // ---------------------------------------------------------------
    // 피해 (Enemy, MiniBoss, BossPattern, CombatUtil에서 호출)
    // ---------------------------------------------------------------
    // 메인무기 확정 전 = 모든 공격, 확정 후 = 메인무기 공격(좌클릭·우클릭·무기 스킬)만
    private static bool Qualifies(bool mainAttack) => MainWeaponController.Current == null || mainAttack;

    // source: 이 피해를 낸 판정 (발사체 Bullet, 범위 공격 1회의 표식). 생명의 일격을 판정 1개에만 적용하는 데 씀
    public static float ModifyHit(float damage, bool mainAttack, TargetKind kind, object source = null)
    {
        if (inGimmickDamage || damage <= 0f)
            return damage;

        float extra = 0f;                                // 피해 증가 (합산)
        float bonus = 0f;                                // 생명의 일격 추가 피해 (증가 미적용)

        // 회피 반격: 구르기 직후 처음 적중한 판정 1개 (생명의 일격과 같은 규칙)
        int dc = Level(ItemData.ItemType.DodgeCounter);
        if (dc > 0 && Qualifies(mainAttack))
        {
            bool apply = false;
            if (DodgeCounterReady)
            {
                dodgeReady = false;
                dodgeStrike.Take(source);
                apply = true;
            }
            else
            {
                apply = dodgeStrike.Continues(source);
            }
            if (apply) extra += Table(Cat.dodgeCounterBonus, dc);
        }

        // 사냥 본능: 엘리트·중간보스 (메인 보스 제외)
        extra += HuntBonus(kind);

        // 생명의 일격: 준비된 뒤 처음 적중한 판정 1개
        // - 관통 투사체면 그 1발이 관통하며 맞힌 적 모두, 광역이면 그 1회에 맞은 적 모두
        // - 한 번에 여러 발을 쏘면 처음 맞은 1발만
        int ls = Level(ItemData.ItemType.LifeStrike);
        if (ls > 0 && Qualifies(mainAttack))
        {
            bool apply = false;
            if (lifeReady)
            {
                lifeReady = false;
                lifeStrike.Take(source);
                lifeNextReady = Time.time + Cat.lifeStrikeInterval;
                apply = true;
            }
            else
            {
                apply = lifeStrike.Continues(source);
            }
            if (apply)
            {
                bonus += GameManager.instance.maxHealth * Table(Cat.lifeStrikeRatio, ls);
            }
        }

        lastLifeBonus = bonus;
        return AddIncrease(damage, extra) + bonus;
    }

    // 피해가 들어간 뒤: 연쇄 번개 적중 수, 처치 폭발
    public static void AfterHit(Vector3 position, float damage, bool killed)
    {
        float lifeBonus = lastLifeBonus;                 // 처치 폭발 기준에서 생명의 일격 추가분은 뺌
        lastLifeBonus = 0f;
        if (inGimmickDamage)
            return;

        int cl = Level(ItemData.ItemType.ChainLightning);
        if (cl > 0)
        {
            chainHits++;
            int need = Mathf.Max(1, Table(Cat.chainHitsPer, cl));
            if (chainHits >= need)
            {
                chainHits = 0;
                ChainLightning(position, Table(Cat.chainTargets, cl));
            }
        }

        int ke = Level(ItemData.ItemType.KillExplosion);
        if (killed && ke > 0 && Random.value < Table(Cat.explodeChance, ke))
        {
            SpriteFx.Explosion(position, Cat.explodeRadius * 1.4f);
            inGimmickDamage = true;
            CombatUtil.DamageInCircle(position, Cat.explodeRadius, Mathf.Max(0f, damage - lifeBonus) * Cat.explodeDamageMul, false, false, Cat.knockExplosion);
            inGimmickDamage = false;
        }
    }

    public static void OnEnemyKilled()
    {
        if (Level(ItemData.ItemType.LifeStrike) > 0)
        {
            lifeKills++;
        }
    }

    // 메인무기 기준 피해 (메인무기가 없으면 가장 강한 보유 무기)
    private static float MainBaseDamage()
    {
        MainWeaponController main = MainWeaponController.Current;
        if (main != null && main.MainWeapon != null)
            return main.MainWeapon.BaseDamage;
        float best = 0f;
        foreach (Weapon w in GameManager.instance.player.GetComponentsInChildren<Weapon>())
        {
            best = Mathf.Max(best, w.BaseDamage);
        }
        return best;
    }

    private static void ChainLightning(Vector3 from, int targets)
    {
        float damage = MainBaseDamage() * Cat.lightningDamageMul * PlayerBuff.Damage;
        if (damage <= 0f || targets <= 0)
            return;

        HashSet<Object> done = new HashSet<Object>();
        Vector3 current = from;
        inGimmickDamage = true;
        for (int i = 0; i < targets; i++)
        {
            Component next = Nearest(current, Cat.chainRadius, done);
            if (next == null)
                break;
            done.Add(next);
            Vector3 to = next.transform.position;
            LightningFx(current, to);
            // 사냥 본능은 연쇄 번개에도 (합산)
            if (next is Enemy e) e.TakeDamage(AddIncrease(damage, HuntBonus(e.HuntKind)));
            else if (next is MiniBoss m) m.TakeDamage(AddIncrease(damage, HuntBonus(TargetKind.MiniBoss)));
            else if (next is ColonyNest n) n.TakeDamage(damage);
            else if (next is BossPattern b) b.TakeDamage(damage);
            current = to;
        }
        inGimmickDamage = false;
    }

    // 번개 줄기 (lightning_effect를 두 점 사이에 늘려 재생하고 lightningLifetime 뒤 삭제)
    private static void LightningFx(Vector3 from, Vector3 to)
    {
        GimmickCatalog c = Cat;
        Sprite[] frames = c.lightningFrames;
        if (frames == null || frames.Length == 0)
            return;
        Vector3 d = to - from;
        float length = Mathf.Max(0.3f, d.magnitude);
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float life = Mathf.Max(0.05f, c.lightningLifetime);
        GameObject fx = SpriteFx.Stretched(frames, (from + to) * 0.5f, length, c.lightningWidth, angle, frames.Length / life, 0f, 6, false);
        if (fx != null)
        {
            Object.Destroy(fx.transform.parent != null ? fx.transform.parent.gameObject : fx, life);
        }
    }

    private static Component Nearest(Vector3 center, float radius, HashSet<Object> exclude)
    {
        Component best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            Component c = col.GetComponentInParent<Enemy>();
            if (c != null && !((Enemy)c).IsAlive) c = null;
            if (c == null)
            {
                MiniBoss m = col.GetComponentInParent<MiniBoss>();
                if (m != null && m.IsLive) c = m;
            }
            if (c == null)
            {
                ColonyNest n = col.GetComponentInParent<ColonyNest>();   // 군락 둥지도 연쇄 번개 대상
                if (n != null && n.IsLive) c = n;
            }
            if (c == null) c = col.GetComponentInParent<BossPattern>();
            if (c == null || exclude.Contains(c)) continue;
            float d = (c.transform.position - center).sqrMagnitude;
            if (d > 0.0001f && d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------
    // 냉기 발자국 (Enemy 이동에서 읽음)
    // ---------------------------------------------------------------
    public static float EnemySpeedScale(Enemy enemy)
    {
        return slowed.Contains(enemy) ? 1f - Table(Cat.frostSlow, Level(ItemData.ItemType.FrostStep)) : 1f;
    }

    // ---------------------------------------------------------------
    // 매 프레임 (GimmickRunner)
    // ---------------------------------------------------------------
    private static float lastHealth = -1f;
    private static float lastMaxHealth = -1f;
    private static bool lastNight;

    internal static void Tick(GimmickRunner runner)
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null)
            return;
        GimmickCatalog c = Cat;

        // 피격 감지 (최대 체력 변화가 아닌 체력 감소) → 집중 초기화
        if (lastHealth >= 0f && gm.health < lastHealth - 0.0001f && Mathf.Approximately(gm.maxHealth, lastMaxHealth))
        {
            lastHitTakenTime = Time.time;
        }

        // 위기 탈출: 체력이 기준 이하로 떨어진 순간 (낮·밤마다 1회)
        bool night = DayNightCycle.IsNight;
        if (night != lastNight)
        {
            escapeUsed = false;
            lastNight = night;
        }
        if (Level(ItemData.ItemType.Escape) > 0 && !escapeUsed && gm.isLive && gm.health > 0f &&
            gm.health <= gm.maxHealth * c.escapeHealthRatio && (lastHealth < 0f || lastHealth > gm.maxHealth * c.escapeHealthRatio))
        {
            escapeUsed = true;
            escapeEnd = Time.time + c.escapeInvincibleTime;
            runner.EscapePush(gm.player.transform.position);
        }
        lastHealth = gm.health;
        lastMaxHealth = gm.maxHealth;

        // 보호막 충전
        int sh = Level(ItemData.ItemType.Shield);
        if (sh > 0 && !shieldUp && Time.time >= shieldReadyTime)
        {
            shieldUp = true;
        }

        // 생명의 일격 준비
        if (Level(ItemData.ItemType.LifeStrike) > 0 && !lifeReady && Time.time >= lifeNextReady)
        {
            lifeReady = true;
        }

        // 냉기 발자국 대상 (일반·엘리트 몬스터만)
        slowed.Clear();
        if (Level(ItemData.ItemType.FrostStep) > 0 && gm.isLive)
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(gm.player.transform.position, c.frostRadius))
            {
                Enemy e = col.GetComponentInParent<Enemy>();
                if (e != null && e.IsAlive) slowed.Add(e);
            }
        }
        runner.UpdateVisuals(slowed);
    }

    internal static void OnPlayerHealthReset()
    {
        lastHealth = -1f;
    }
}
