using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 캐릭터 강화 저장 (JSON 파일: Application.persistentDataPath/monnyang_save.json)
// 일반 젬 보유 개수, 슬롯 장착 상태, 클리어 횟수, 특수 젬(옵션·수치·잠금), 특수 젬 슬롯, 조각
// 장착·해제·합성·재설정·분해·잠금·결과 보상 때마다 바로 저장한다.
public static class GemSave
{
    // 특수 젬 하나: 증가 옵션 1 + 감소 옵션 1 (종류는 생성 후 바뀌지 않음, 수치는 정수)
    [Serializable]
    public class SpecialGem
    {
        public int id;
        public RunStat up;
        public int upValue;
        public RunStat down;
        public int downValue;
        public bool locked;                          // 잠금: 분해 불가
    }

    [Serializable]
    public class Data
    {
        public int version = 3;
        public int[] gemCounts = new int[0];         // 일반 젬 종류별 보유 개수 (GemCatalog.gems 순서)
        public int[] slots = new int[0];             // 슬롯 9칸: 공격 0~2, 생존 3~5, 유틸 6~8. 값 = 젬 번호, -1 = 비어 있음
        public int clearCount;
        public List<SpecialGem> specials = new List<SpecialGem>();
        public int nextSpecialId = 1;
        public int[] specialSlots = { -1, -1, -1 };  // 특수 젬 슬롯. 값 = 특수 젬 id, -1 = 비어 있음
        public int shards;                           // 조각 (특수 젬 분해 재화)
    }

    private static Data data;
    private static string FilePath => Path.Combine(Application.persistentDataPath, "monnyang_save.json");

    public static Data Current
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    private static GemCatalog Cat => GemCatalog.Instance;
    private static int SlotCount => Cat.slotsPerCategory * 3;

    public static void Load()
    {
        data = null;
        try
        {
            if (File.Exists(FilePath)) data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[젬 저장] 읽기 실패, 새로 시작합니다: " + e.Message);
        }
        if (data == null) data = new Data();
        Normalize();
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[젬 저장] 쓰기 실패: " + e.Message);
        }
    }

    // 테스트 모드: 저장 초기화
    public static void ResetAll()
    {
        data = new Data();
        Normalize();
        Save();
    }

    private static void Normalize()
    {
        int n = Cat.gems.Count;
        if (data.gemCounts == null || data.gemCounts.Length != n) Array.Resize(ref data.gemCounts, n);
        if (data.slots == null || data.slots.Length != SlotCount)
        {
            int old = data.slots == null ? 0 : data.slots.Length;
            Array.Resize(ref data.slots, SlotCount);
            for (int i = old; i < data.slots.Length; i++) data.slots[i] = -1;
        }
        for (int i = 0; i < data.slots.Length; i++)
        {
            int g = data.slots[i];
            if (g < 0 || g >= n || Cat.gems[g].category != SlotCategory(i)) data.slots[i] = -1;
        }
        if (data.specials == null) data.specials = new List<SpecialGem>();
        int specialSlotCount = Mathf.Max(1, Cat.specialSlotCount);
        if (data.specialSlots == null || data.specialSlots.Length != specialSlotCount)
        {
            int old = data.specialSlots == null ? 0 : data.specialSlots.Length;
            Array.Resize(ref data.specialSlots, specialSlotCount);
            for (int i = old; i < data.specialSlots.Length; i++) data.specialSlots[i] = -1;
        }
        for (int i = 0; i < data.specialSlots.Length; i++)
            if (data.specialSlots[i] >= 0 && FindSpecial(data.specialSlots[i]) == null) data.specialSlots[i] = -1;
        foreach (SpecialGem g in data.specials) data.nextSpecialId = Mathf.Max(data.nextSpecialId, g.id + 1);
        if (data.shards < 0) data.shards = 0;
        if (data.version < 3) MigrateUltPower();
    }

    // 버전 3: 필살기 위력 제거
    // - 일반 젬: 같은 개수의 보너스 선택 젬으로 (장착 칸도 그대로 바꿔 끼움)
    // - 특수 젬: 증가 옵션만 무기·총알 크기로, 수치는 새 범위에서 다시 뽑음 (감소 옵션·잠금 유지)
    private static void MigrateUltPower()
    {
        int ult = Cat.gems.FindIndex(x => x.stat == RunStat.UltPower);
        int bonus = Cat.gems.FindIndex(x => x.stat == RunStat.BonusPicks);
        GemCatalog.SpecialOption size = Cat.UpOption(RunStat.WeaponSize);
        if (bonus < 0 || size == null) return;                   // 카탈로그에 새 젬·옵션이 아직 없으면 다음에 다시 시도
        if (ult >= 0)
        {
            data.gemCounts[bonus] += data.gemCounts[ult];
            data.gemCounts[ult] = 0;
            for (int i = 0; i < data.slots.Length; i++)
                if (data.slots[i] == ult) data.slots[i] = bonus;
        }
        foreach (SpecialGem g in data.specials)
        {
            if (g.up != RunStat.UltPower || g.down == RunStat.WeaponSize) continue;
            g.up = RunStat.WeaponSize;
            g.upValue = RollValue(size);
        }
        data.version = 3;
        Save();
        Debug.Log("[젬 저장] 필살기 위력 젬을 보너스 선택 / 무기·총알 크기로 교체했습니다");
    }

    // ---------------- 슬롯 ----------------
    public static StatCategory SlotCategory(int slot) => (StatCategory)(slot / Mathf.Max(1, Cat.slotsPerCategory));
    public static int SlotIndexInCategory(int slot) => slot % Mathf.Max(1, Cat.slotsPerCategory);

    // 분야별 열린 칸 수 (처음 1칸, 클리어마다 1칸)
    public static int OpenSlots => Mathf.Min(Cat.slotsPerCategory, Cat.slotsOpenAtStart + Current.clearCount * Cat.slotsPerClear);
    public static bool SlotOpen(int slot) => SlotIndexInCategory(slot) < OpenSlots;

    // 이 칸을 열려면 필요한 클리어 횟수
    public static int ClearsNeeded(int slot) =>
        Cat.slotsPerClear <= 0 ? 99 : Mathf.CeilToInt((SlotIndexInCategory(slot) + 1 - Cat.slotsOpenAtStart) / (float)Cat.slotsPerClear);

    public static int Owned(int gem) => gem >= 0 && gem < Current.gemCounts.Length ? Current.gemCounts[gem] : 0;

    public static int EquippedCount(int gem)
    {
        int n = 0;
        foreach (int g in Current.slots) if (g == gem) n++;
        return n;
    }

    // 장착하지 않은 개수 (합성 재료로 쓸 수 있는 개수)
    public static int Free(int gem) => Mathf.Max(0, Owned(gem) - EquippedCount(gem));

    // 젬을 그 분야의 빈 열린 칸에 장착 (보유 개수만큼만). preferSlot이 맞으면 그 칸
    public static bool Equip(int gem, int preferSlot = -1)
    {
        if (gem < 0 || gem >= Cat.gems.Count || Free(gem) <= 0)
            return false;
        StatCategory c = Cat.gems[gem].category;
        int target = -1;
        if (preferSlot >= 0 && preferSlot < Current.slots.Length && SlotCategory(preferSlot) == c && SlotOpen(preferSlot))
        {
            target = preferSlot;
        }
        else
        {
            for (int i = 0; i < Current.slots.Length; i++)
            {
                if (SlotCategory(i) == c && SlotOpen(i) && Current.slots[i] < 0) { target = i; break; }
            }
        }
        if (target < 0)
            return false;
        Current.slots[target] = gem;
        Save();
        return true;
    }

    public static void UnequipSlot(int slot)
    {
        if (slot < 0 || slot >= Current.slots.Length || Current.slots[slot] < 0)
            return;
        Current.slots[slot] = -1;
        Save();
    }

    // 장착한 젬 전부 해제 (일반 젬·특수 젬)
    public static void UnequipAll()
    {
        for (int i = 0; i < Current.slots.Length; i++) Current.slots[i] = -1;
        for (int i = 0; i < Current.specialSlots.Length; i++) Current.specialSlots[i] = -1;
        Save();
    }

    // 이 젬을 장착한 칸 하나 해제 (마지막 칸부터)
    public static bool Unequip(int gem)
    {
        for (int i = Current.slots.Length - 1; i >= 0; i--)
        {
            if (Current.slots[i] == gem)
            {
                UnequipSlot(i);
                return true;
            }
        }
        return false;
    }

    // ---------------- 합성·보상 ----------------
    public static int RandomGem(StatCategory? category)
    {
        List<int> pool = new List<int>();
        for (int i = 0; i < Cat.gems.Count; i++)
            if (!Cat.gems[i].retired && (category == null || Cat.gems[i].category == category.Value)) pool.Add(i);
        return pool.Count == 0 ? -1 : pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // 1+1 합성: 장착하지 않은 재료 2개 → 선택한 분야 안에서 무작위 1개. 실패하면 -1
    public static int Combine(int a, int b, StatCategory category)
    {
        if (a < 0 || b < 0) return -1;
        if (a == b ? Free(a) < 2 : (Free(a) < 1 || Free(b) < 1)) return -1;
        int result = RandomGem(category);
        if (result < 0) return -1;
        Current.gemCounts[a]--;
        Current.gemCounts[b]--;
        Current.gemCounts[result]++;
        Save();
        return result;
    }

    // 실패(사망·그만두기) 보상 개수 = 버틴 밤 / failNightsPerGem (버림), 최대 failGemMax
    public static int FailRewardCount(int nights) => Mathf.Min(Cat.failGemMax, Mathf.Max(0, nights) / Mathf.Max(1, Cat.failNightsPerGem));

    // 실패 시 특수 젬 드롭 조건 (특수 젬은 다음 작업, 조건만)
    public static bool SpecialDropAllowed(bool cleared, int nights) => cleared || nights >= Cat.specialDropMinNights;

    // 게임 결과 보상 (클리어 / 버틴 밤 수). 받은 젬 번호 목록, 특수 젬 드롭 (없으면 null)
    public static List<int> GrantRunReward(bool cleared, int nights, out SpecialGem special)
    {
        special = null;
        float chance = cleared ? Cat.specialDropClear : SpecialDropAllowed(false, nights) ? Cat.specialDropFail : 0f;
        if (chance > 0f && UnityEngine.Random.value < chance) special = CreateSpecial(false);
        int count = cleared ? Cat.gemRewardClear : FailRewardCount(nights);
        List<int> got = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int g = RandomGem(null);
            if (g < 0) break;
            Current.gemCounts[g]++;
            got.Add(g);
        }
        if (cleared) Current.clearCount++;
        Save();
        return got;
    }

    public static void AddGems(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int g = RandomGem(null);
            if (g >= 0) Current.gemCounts[g]++;
        }
        Save();
    }

    // ---------------- 특수 젬 ----------------
    public static SpecialGem FindSpecial(int id) => id < 0 ? null : Current.specials.Find(g => g.id == id);

    public static string SpecialName(SpecialGem g) => StatCatalog.Instance.Name(g.up) + " 특수 젬";
    public static StatCategory SpecialCategory(SpecialGem g)
    {
        GemCatalog.SpecialOption o = Cat.UpOption(g.up);
        return o != null ? o.category : StatCategory.Attack;
    }

    private static int RollValue(GemCatalog.SpecialOption o) => UnityEngine.Random.Range(o.Min, o.Max + 1);

    // 특수 젬 생성: 증가 11종 균등 → 감소는 같은 능력치를 뺀 나머지 중 균등, 수치는 범위 안 정수
    public static SpecialGem CreateSpecial(bool save = true)
    {
        List<GemCatalog.SpecialOption> ups = Cat.specialUpOptions.FindAll(o => !o.retired);   // 필살기 위력 등 제외한 옵션
        if (ups.Count == 0) return null;
        GemCatalog.SpecialOption up = ups[UnityEngine.Random.Range(0, ups.Count)];
        List<GemCatalog.SpecialOption> downs = Cat.specialDownOptions.FindAll(o => o.stat != up.stat);
        if (downs.Count == 0) return null;
        GemCatalog.SpecialOption down = downs[UnityEngine.Random.Range(0, downs.Count)];
        SpecialGem g = new SpecialGem
        {
            id = Current.nextSpecialId++,
            up = up.stat,
            upValue = RollValue(up),
            down = down.stat,
            downValue = RollValue(down),
        };
        Current.specials.Add(g);
        if (save) Save();
        return g;
    }

    // 특수 젬 합성: 장착하지 않은 일반 젬 specialCombineCount개 → 특수 젬 1개
    public static SpecialGem CombineSpecial(IList<int> materials)
    {
        if (materials == null || materials.Count != Cat.specialCombineCount) return null;
        Dictionary<int, int> need = new Dictionary<int, int>();
        foreach (int m in materials)
        {
            if (m < 0) return null;
            need.TryGetValue(m, out int n);
            need[m] = n + 1;
        }
        foreach (KeyValuePair<int, int> pair in need)
            if (Free(pair.Key) < pair.Value) return null;
        foreach (int m in materials) Current.gemCounts[m]--;
        return CreateSpecial(true);
    }

    // 특수 젬 슬롯 (처음 1칸, 클리어마다 1칸)
    public static int SpecialOpenSlots => Mathf.Min(Current.specialSlots.Length, Cat.specialSlotsOpenAtStart + Current.clearCount * Cat.specialSlotsPerClear);
    public static bool SpecialSlotOpen(int slot) => slot < SpecialOpenSlots;
    public static int SpecialClearsNeeded(int slot) =>
        Cat.specialSlotsPerClear <= 0 ? 99 : Mathf.CeilToInt((slot + 1 - Cat.specialSlotsOpenAtStart) / (float)Cat.specialSlotsPerClear);

    public static int SpecialSlotOf(int id) => id < 0 ? -1 : Array.IndexOf(Current.specialSlots, id);
    public static bool IsSpecialEquipped(int id) => SpecialSlotOf(id) >= 0;

    // 같은 증가 옵션이 다른 칸에 장착돼 있는지 (exceptSlot 칸은 빼고)
    public static bool SameUpEquipped(SpecialGem g, int exceptSlot = -1)
    {
        for (int i = 0; i < Current.specialSlots.Length; i++)
        {
            if (i == exceptSlot) continue;
            SpecialGem e = FindSpecial(Current.specialSlots[i]);
            if (e != null && e.id != g.id && e.up == g.up) return true;
        }
        return false;
    }

    // 장착 가능 여부와 안 되는 이유 (null = 가능)
    public static string CanEquipSpecial(SpecialGem g, int preferSlot = -1)
    {
        if (g == null) return "젬을 선택하세요";
        if (IsSpecialEquipped(g.id)) return "이미 장착한 젬입니다";
        int target = SpecialTargetSlot(preferSlot);
        if (target < 0) return "빈 특수 젬 칸이 없습니다";
        if (SameUpEquipped(g, target)) return "같은 증가 옵션 특수 젬은 함께 장착할 수 없습니다";
        return null;
    }

    private static int SpecialTargetSlot(int preferSlot)
    {
        if (preferSlot >= 0 && preferSlot < Current.specialSlots.Length && SpecialSlotOpen(preferSlot)) return preferSlot;
        for (int i = 0; i < Current.specialSlots.Length; i++)
            if (SpecialSlotOpen(i) && Current.specialSlots[i] < 0) return i;
        return -1;
    }

    public static bool EquipSpecial(int id, int preferSlot = -1)
    {
        SpecialGem g = FindSpecial(id);
        if (CanEquipSpecial(g, preferSlot) != null) return false;
        Current.specialSlots[SpecialTargetSlot(preferSlot)] = id;
        Save();
        return true;
    }

    public static void UnequipSpecial(int id)
    {
        int slot = SpecialSlotOf(id);
        if (slot < 0) return;
        Current.specialSlots[slot] = -1;
        Save();
    }

    public static void SetSpecialLocked(int id, bool locked)
    {
        SpecialGem g = FindSpecial(id);
        if (g == null) return;
        g.locked = locked;
        Save();
    }

    // 재설정 비용: 기본 rerollCost, 한 줄 잠금 x lockCostMul
    public static int RerollCost(int lockLine) => Cat.rerollCost * (lockLine >= 0 ? Cat.lockCostMul : 1);

    // 수치 재설정: 옵션 종류는 그대로, 수치만 다시 뽑음. lockLine 0 = 증가 줄 유지, 1 = 감소 줄 유지, -1 = 둘 다 다시
    public static bool RerollSpecial(int id, int lockLine)
    {
        SpecialGem g = FindSpecial(id);
        int cost = RerollCost(lockLine);
        if (g == null || Current.shards < cost) return false;
        GemCatalog.SpecialOption up = Cat.UpOption(g.up), down = Cat.DownOption(g.down);
        if (up == null || down == null) return false;
        Current.shards -= cost;
        if (lockLine != 0) g.upValue = RollValue(up);
        if (lockLine != 1) g.downValue = RollValue(down);
        Save();
        return true;
    }

    // 분해할 수 있는지 (잠금·장착 중이면 불가)
    public static bool CanDecompose(SpecialGem g) => g != null && !g.locked && !IsSpecialEquipped(g.id);

    // 여러 개 분해 → 받은 조각 수
    public static int Decompose(IEnumerable<int> ids)
    {
        int got = 0;
        foreach (int id in new List<int>(ids))
        {
            SpecialGem g = FindSpecial(id);
            if (!CanDecompose(g)) continue;
            Current.specials.Remove(g);
            got += Cat.shardPerGem;
        }
        Current.shards += got;
        Save();
        return got;
    }

    public static void AddShards(int count)
    {
        Current.shards = Mathf.Max(0, Current.shards + count);
        Save();
    }

    // ---------------- 게임에 적용 ----------------
    // 게임 시작 때 장착한 젬을 '젬' 출처로, 특수 젬을 '특수 젬' 출처로 적용 (시작 리롤·시작 냥냥코인은 바로 지급)
    // 특수 젬의 감소 옵션은 RunStats 규칙대로 항목마다 따로 곱해진다.
    public static void ApplyToRun()
    {
        foreach (int id in Current.specialSlots)
        {
            SpecialGem s = FindSpecial(id);
            if (s == null) continue;
            GemCatalog.SpecialOption up = Cat.UpOption(s.up), down = Cat.DownOption(s.down);
            if (up != null) RunStats.AddValue(s.up, s.upValue * up.scale, StatSource.SpecialGem);
            if (down != null) RunStats.AddValue(s.down, s.downValue * down.scale, StatSource.SpecialGem);
        }
        foreach (int g in Current.slots)
        {
            if (g < 0 || g >= Cat.gems.Count) continue;
            GemCatalog.GemDef d = Cat.gems[g];
            RunStats.AddValue(d.stat, d.value, StatSource.Gem);
            if (d.stat == RunStat.Rerolls && LevelUpReroll.Instance != null) LevelUpReroll.Instance.AddUses(Mathf.RoundToInt(d.value));
            if (d.stat == RunStat.StartCoins) CoinWallet.Add(Mathf.RoundToInt(d.value));
        }
    }

    // 장착 효과 합계 (분야별 능력치 → 합)
    public static Dictionary<RunStat, float> EquippedTotals()
    {
        Dictionary<RunStat, float> totals = new Dictionary<RunStat, float>();
        foreach (int g in Current.slots)
        {
            if (g < 0 || g >= Cat.gems.Count) continue;
            GemCatalog.GemDef d = Cat.gems[g];
            totals.TryGetValue(d.stat, out float v);
            totals[d.stat] = v + d.value;
        }
        return totals;
    }
}
