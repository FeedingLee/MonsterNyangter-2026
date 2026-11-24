using System.Collections.Generic;
using UnityEngine;

// 효과가 유지되는 기간
public enum ModifierLife
{
    Permanent,      // 게임이 끝날 때까지 (상점, 성장 보상, 지옥 선택지, 도전의 영구 보상)
    UntilMorning,   // 다음 아침까지 (도전 선택지의 리스크와 보상). 보스가 나오면 즉시 해제
}

// 상점에 놓인 카드 한 장 (특수 선택지면 specialIndex >= 0)
public class ShopCard
{
    public ShopGrade grade;
    public int statIndex = -1;      // StatCatalog.shopStats 번호
    public int specialIndex = -1;   // StatCatalog.specials 번호
    public bool IsSpecial => specialIndex >= 0;
}

// 한 판 동안의 능력치 배율 모음 (상점 / 젬 / 특수 젬 / 아침 선택지)
// 각 스크립트는 RunStats.Get(RunStat.X)로 최종 배율을 읽는다. 값은 바뀔 때만 다시 계산한다.
// StatCatalog에 정의된 16종은 계산 규칙을 따른다.
//  - 증가형(%): 모든 출처 합산 후 한 번 곱함 (코인 획득량은 합산 +coinGainCap까지)
//  - 감소형(쿨타임·받는 피해): 출처 안에서 합산, 출처끼리는 곱함 (공격 쿨타임·전용 스킬 쿨타임은 하한)
//  - 더하기(최대 체력 고정값·리롤·시작 코인): 합산
// 그 외 능력치(스폰·엘리트·보스·밤 등)는 예전처럼 배율을 곱한다.
public static class RunStats
{
    private class Entry
    {
        public RunStat stat;
        public StatSource source;
        public float value;          // 규칙 능력치: 변화량 (+0.04 = +4%) / 그 외: 곱할 배율 또는 더할 값
        public ModifierLife life;
    }

    private static readonly List<Entry> entries = new List<Entry>();
    private static float[] cache;
    private static bool dirty = true;

    // 레벨업 선택지 장수 (저주받은 금화로 2장)
    public static int LevelUpChoices { get; set; } = 3;

    // 상점 상태 (구매 횟수로 가격 결정, 놓인 카드는 사거나 새 판이 될 때까지 유지)
    public static int ShopPurchases { get; set; }
    public static readonly List<ShopCard> ShopOffer = new List<ShopCard>();

    // 능력치 창에 표시할 진행 중인 효과 이름 (도전: 아침까지, 지옥: 끝까지)
    public struct EffectLabel
    {
        public string name;
        public ModifierLife life;
    }

    private static readonly List<EffectLabel> labels = new List<EffectLabel>();
    public static IReadOnlyList<EffectLabel> Labels => labels;

    public static void AddLabel(string name, ModifierLife life)
    {
        labels.Add(new EffectLabel { name = name, life = life });
    }

    // 규칙 능력치가 아닌 것 중 더하기로 계산하는 능력치 (나머지는 곱하기)
    public static bool IsAdditive(RunStat stat)
    {
        StatCatalog.StatDef def = StatCatalog.Instance.Def(stat);
        if (def != null)
            return def.direction == StatDirection.Additive;
        return stat == RunStat.Regen || stat == RunStat.EliteBonusCoins || stat == RunStat.NightDarkness
            || stat == RunStat.NightHpAdd || stat == RunStat.NightBossExtra || stat == RunStat.ColonyRaid;   // 밤 웨이브 체력 증가 합, 추가 대장 수
    }

    // 새 게임 시작 시 초기화
    public static void Reset()
    {
        entries.Clear();
        labels.Clear();
        ShopOffer.Clear();
        ShopPurchases = 0;
        LevelUpChoices = 3;
        dirty = true;
    }

    public static float Get(RunStat stat)
    {
        if (dirty || cache == null)
        {
            Rebuild();
        }
        return cache[(int)stat];
    }

    // 규칙 능력치 값 추가 (상점·젬·특수 젬). 변화량: +0.04 = +4%, 감소형은 -0.02 = -2%, 고정값·횟수는 그대로
    public static void AddValue(RunStat stat, float delta, StatSource source, ModifierLife life = ModifierLife.Permanent)
    {
        entries.Add(new Entry { stat = stat, source = source, value = delta, life = life });
        dirty = true;
    }

    // 아침 선택지: 배율 곱하기 (예: 1.08 = +8%, 0 = 사용 불가)
    // 규칙 능력치는 아침 선택지 출처의 변화량(1.08 → +8%)으로 바꿔 규칙대로 계산한다.
    public static void Multiply(RunStat stat, float value, ModifierLife life = ModifierLife.Permanent)
    {
        bool rule = StatCatalog.Instance.Def(stat) != null && !IsAdditive(stat);
        entries.Add(new Entry { stat = stat, source = StatSource.Morning, value = rule ? value - 1f : value, life = life });
        dirty = true;
    }

    // 아침 선택지: 값 더하기 (Regen, EliteBonusCoins 등)
    public static void Add(RunStat stat, float value, ModifierLife life = ModifierLife.Permanent)
    {
        entries.Add(new Entry { stat = stat, source = StatSource.Morning, value = value, life = life });
        dirty = true;
    }

    // 기간이 끝난 효과 제거
    public static void Expire(ModifierLife life)
    {
        labels.RemoveAll(l => l.life == life);
        if (entries.RemoveAll(m => m.life == life) > 0)
        {
            dirty = true;
        }
    }

    // 한 출처의 누적 변화량 (테스트 모드 확인용)
    public static float SourceTotal(RunStat stat, StatSource source)
    {
        float sum = 0f;
        foreach (Entry e in entries)
        {
            if (e.stat == stat && e.source == source) sum += e.value;
        }
        return sum;
    }

    // 이 값을 더 얻었을 때의 최종값 (상점 카드의 누적 효과 표시용)
    public static float Preview(RunStat stat, float extraDelta, StatSource source = StatSource.Shop)
    {
        return Compute(stat, true, extraDelta, source);
    }

    // 최종값을 변화량으로 표시할 값 (증가·감소형: 배율 - 1, 더하기: 그대로)
    public static float AsDelta(RunStat stat, float final)
    {
        return IsAdditive(stat) ? final : final - 1f;
    }

    private static void Rebuild()
    {
        int count = System.Enum.GetValues(typeof(RunStat)).Length;
        if (cache == null || cache.Length != count)
        {
            cache = new float[count];
        }
        for (int i = 0; i < count; i++)
        {
            cache[i] = Compute((RunStat)i, false, 0f, StatSource.Shop);
        }
        dirty = false;
    }

    private static float Compute(RunStat stat, bool withExtra, float extra, StatSource extraSource)
    {
        StatCatalog cat = StatCatalog.Instance;
        StatCatalog.StatDef def = cat.Def(stat);
        float floor = cat.minimumScale;

        // 규칙이 없는 능력치: 예전처럼 곱하기 / 더하기
        if (def == null)
        {
            bool additive = IsAdditive(stat);
            float v = additive ? 0f : 1f;
            foreach (Entry e in entries)
            {
                if (e.stat != stat) continue;
                v = additive ? v + e.value : v * e.value;
            }
            return v;
        }

        switch (def.direction)
        {
            case StatDirection.Additive:
            {
                float sum = withExtra ? extra : 0f;
                foreach (Entry e in entries)
                {
                    if (e.stat == stat) sum += e.value;
                }
                return sum;
            }
            case StatDirection.Decrease:
            {
                // 이득(감소, 음수): 출처 안에서 합산, 출처끼리 곱함 → 쿨타임 하한
                // 손해(증가, 양수: 상점 특수 선택지 대가 등): 하나씩 따로 곱함
                float result = 1f;
                float penalty = withExtra && extra > 0f ? 1f + extra : 1f;
                foreach (StatSource source in (StatSource[])System.Enum.GetValues(typeof(StatSource)))
                {
                    float sum = withExtra && extra <= 0f && source == extraSource ? extra : 0f;
                    foreach (Entry e in entries)
                    {
                        if (e.stat != stat || e.source != source) continue;
                        if (e.value > 0f) penalty *= 1f + e.value;
                        else sum += e.value;
                    }
                    result *= Mathf.Max(floor, 1f + sum);
                }
                if (stat == RunStat.Cooldown) result = Mathf.Max(cat.attackCdFloor, result);
                else if (stat == RunStat.SkillCooldown) result = Mathf.Max(cat.skillCdFloor, result);
                return Mathf.Max(floor, result * penalty);
            }
            default:
            {
                // 이득(양수): 모든 출처 합산 후 한 번 곱함
                // 손해(음수: 상점 특수 선택지 대가, 특수 젬 감소 옵션 등): 하나씩 따로 곱함 (예: +18%, -30% → 1.18 x 0.7)
                float sum = withExtra && extra > 0f ? extra : 0f;
                float penalty = withExtra && extra < 0f ? Mathf.Max(floor, 1f + extra) : 1f;
                foreach (Entry e in entries)
                {
                    if (e.stat != stat) continue;
                    if (e.value < 0f) penalty *= Mathf.Max(floor, 1f + e.value);
                    else sum += e.value;
                }
                if (stat == RunStat.CoinDrop) sum = Mathf.Min(sum, cat.coinGainCap);
                return Mathf.Max(floor, (1f + sum) * penalty);
            }
        }
    }
}
