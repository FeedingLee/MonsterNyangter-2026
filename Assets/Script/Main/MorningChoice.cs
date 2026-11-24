using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 아침 선택지: 성장/도전/지옥에서 1장씩 뽑아 3장 중 1장을 고른다.
// 가치 기준: 레벨업 1회 = 1점. 성장 약 1점, 도전 2~3점, 지옥 4~6점.
// 도전 리스크는 다음 아침까지 (보스 등장 시 즉시 해제), 지옥 리스크는 대부분 게임 끝까지.
// 같은 지옥 선택지는 한 판에 한 번만 나온다. 조건을 만족하지 못하는 선택지는 후보에서 뺀다.
public static class MorningChoice
{
    public enum Tier { Growth, Challenge, Hell }

    private class Choice
    {
        public string id;
        public Tier tier;
        public string name;
        public string reward;
        public string risk;
        public System.Func<bool> available;
        public System.Action apply;
        public bool retired;                                 // 풀에서 제외 (데이터는 남김)
        public bool lastDayBan;                              // 5일 차(마지막 날) 아침에는 나오지 않음
    }

    public static bool IsOpen { get; private set; }

    private static List<Choice> choices;
    private static readonly HashSet<string> usedHell = new HashSet<string>();

    private static Canvas canvas;
    private static RectTransform content;                    // 제목·카드·다시 뽑기 묶음 (화면 높이에 맞춰 축소)
    private static Text title;
    private static Button rerollButton;
    private static readonly List<GameObject> cards = new List<GameObject>();
    private static int picksLeft;

    public static void ResetRun()
    {
        usedHell.Clear();
        IsOpen = false;
    }

    // ---------------------------------------------------------------
    // 선택지 목록
    // ---------------------------------------------------------------
    private static List<Choice> All
    {
        get
        {
            if (choices == null)
            {
                choices = BuildChoices();
            }
            return choices;
        }
    }

    private static List<Choice> BuildChoices()
    {
        const ModifierLife UntilMorning = ModifierLife.UntilMorning;
        int questGoal = RunConfig.Instance.questKillGoal;

        List<Choice> list = new List<Choice>
        {
            // 성장: 리스크 없음, 작은 보상
            G("stretch", "기지개 켜기", "즉시 레벨 +1", () => Levels(1)),
            G("claws", "발톱 다듬기", "공격력 +8%\n(영구)", () => RunStats.Multiply(RunStat.Damage, 1.08f)),
            G("grooming", "그루밍", "체력 전부 회복\n최대 체력 +10%\n(영구)", () =>
            {
                RunStats.Multiply(RunStat.MaxHealth, 1.1f);
                GameManager.instance.Heal(GameManager.instance.maxHealth * 2f);
            }),
            G("light_step", "사뿐한 발걸음", "회피 쿨타임 -10%\n(영구)", () => RunStats.Multiply(RunStat.DodgeCooldown, 0.9f)),
            G("weapon_care", "무기 손질", "가장 높은 무기\n레벨 즉시 +1\n(5레벨이 되면 메인무기 해금)",
                () => LevelUpBestWeapon(), () => LevelableWeapons().Count > 0),
            G("pocket_money", "쌈짓돈", "즉시 코인 +30", () => CoinWallet.Add(30)),
            G("sniff", "코끝 킁킁", "경험치 획득 범위 +25%\n(영구)", () => RunStats.Multiply(RunStat.ExpRange, 1.25f)),
            G("cat_sense", "고양이 직감", "레벨업 리롤 +2\n(이번 판 동안)", () => LevelUpReroll.Instance.AddUses(2),
                () => LevelUpReroll.Instance != null),

            // 도전: 다음 아침까지 리스크, 중간 보상
            C("drive_hunt", "몰이사냥", "경험치 획득량 1.5배\n(다음 아침까지)", "몬스터 스폰 속도 2배\n(다음 아침까지)", () =>
            {
                RunStats.Multiply(RunStat.ExpGain, 1.5f, UntilMorning);
                RunStats.Multiply(RunStat.SpawnRate, 2f, UntilMorning);
            }),
            C("elite_hunt", "엘리트 토벌", "엘리트 처치 시 코인 +3\n(다음 아침까지)", "엘리트 등장 3배\n엘리트 체력 2배\n(다음 아침까지)", () =>
            {
                RunStats.Add(RunStat.EliteBonusCoins, 3f, UntilMorning);
                RunStats.Multiply(RunStat.EliteChance, 3f, UntilMorning);
                RunStats.Multiply(RunStat.EliteHealth, 2f, UntilMorning);
            }),
            C("heavy_bag", "무거운 배낭", "즉시 코인 +80", "이동속도 -15%\n(다음 아침까지)", () =>
            {
                CoinWallet.Add(80);
                RunStats.Multiply(RunStat.MoveSpeed, 0.85f, UntilMorning);
            }),
            C("dream_training", "꿈속 수련", "즉시 레벨 +2", "회피 쿨타임 +50%\n(다음 아침까지)", () =>
            {
                Levels(2);
                RunStats.Multiply(RunStat.DodgeCooldown, 1.5f, UntilMorning);
            }),
            C("dark_hunt", "어둠 사냥", "다음 밤 코인 드롭 2배\n(다음 밤 동안)", "다음 밤 시야가\n칠흑같이 어두워짐\n(다음 밤 동안)", () =>
            {
                RunStats.Multiply(RunStat.NightCoinDrop, 2f, UntilMorning);
                RunStats.Multiply(RunStat.NightVision, 0.6f, UntilMorning);   // 시야가 좁아지고
                RunStats.Add(RunStat.NightDarkness, 0.6f, UntilMorning);      // 시야 밖은 완전히, 시야 안도 꽤 어둡게
            }),
            C("kill_quest", "토벌 의뢰", "다음 아침 성공 시\n레벨 +2, 무기 레벨 +1",
                "다음 아침까지 " + questGoal + "마리 처치\n실패 시 코인 -30",
                () => DayNightCycle.Instance.StartKillQuest(),
                () => DayNightCycle.Instance != null && !DayNightCycle.Instance.QuestActive),
            C("fasting", "단식 수행", "공격 속도 +15%\n(영구)", "최대 체력 -20%\n(다음 아침까지)", () =>
            {
                RunStats.Multiply(RunStat.AttackSpeed, 1.15f);
                RunStats.Multiply(RunStat.MaxHealth, 0.8f, UntilMorning);
            }),
            C("seal_ultimate", "필살기 봉인", "필살기 쿨타임 -20% (영구)\n횟수형 필살기는 사용 횟수 +1", "모든 적 체력 2배\n필살기 사용 불가\n(다음 아침까지)", () =>
            {
                RunStats.Multiply(RunStat.UltCooldown, 0.8f);
                RunStats.Multiply(RunStat.EnemyHealth, 2f, UntilMorning);
                RunStats.Multiply(RunStat.UltEnabled, 0f, UntilMorning);
                UltimateManager.Instance.AddUses(1);
            }, () => UltimateManager.Instance != null),

            // 지옥: 큰 리스크, 큰 보상 (한 판에 한 번)
            H("blood_pact", "피의 계약", "즉시 레벨 +3\n코인 +100", "매 밤 몬스터 강화량 +50%\n(영구, 보스 제외)", () =>
            {
                Levels(3);
                CoinWallet.Add(100);
                RunStats.Multiply(RunStat.NightMonsterBuff, 1.5f);
            }),
            H("glass_claws", "유리 발톱", "공격력 +35%\n(영구)", "최대 체력 -35%\n(영구, 보스전 포함)", () =>
            {
                RunStats.Multiply(RunStat.Damage, 1.35f);
                RunStats.Multiply(RunStat.MaxHealth, 0.65f);
            }),
            H("frenzy", "폭주", "이동속도 +20%\n공격 속도 +20%\n(영구)", "회피 쿨타임 2배\n(영구, 보스전 포함)", () =>
            {
                RunStats.Multiply(RunStat.MoveSpeed, 1.2f);
                RunStats.Multiply(RunStat.AttackSpeed, 1.2f);
                RunStats.Multiply(RunStat.DodgeCooldown, 2f);
            }),
            H("endless_night", "끝없는 밤", "이어지는 밤 코인 드롭 3배\n다음 아침 선택지 2장 선택", "다음 낮이 사라지고 밤이 이어짐\n그 밤 몬스터 강화 2배\n(이어지는 밤 동안)", () =>
            {
                RunStats.Multiply(RunStat.NightCoinDrop, 3f, UntilMorning);
                RunStats.Multiply(RunStat.NightMonsterBuff, 2f, UntilMorning);
                DayNightCycle.Instance.SetNextMorningPicks(2);
                DayNightCycle.Instance.StartEndlessNight();
            }, () => DayNightCycle.Instance != null &&
                     GameManager.instance.maxGameTime - GameManager.instance.gameTime > RunConfig.Instance.nightDuration),
            H("elite_march", "엘리트의 행진", "보유 무기 전부\n레벨 즉시 +1", "엘리트 등장 2배\n엘리트 공격·체력 2배\n(영구, 보스 제외)", () =>
            {
                foreach (Item item in LevelableWeapons())
                {
                    item.OnClick();
                }
                RunStats.Multiply(RunStat.EliteChance, 2f);
                RunStats.Multiply(RunStat.EliteHealth, 2f);
                RunStats.Multiply(RunStat.EliteDamage, 2f);
            }, () => LevelableWeapons().Count > 0),
            H("cursed_gold", "저주받은 금화", "코인 +180", "레벨업 선택지 3장 → 2장\n(영구, 리롤 가능)", () =>
            {
                CoinWallet.Add(180);
                RunStats.LevelUpChoices = 2;
            }, () => RunStats.LevelUpChoices > 2),
            H("beast_rage", "맹수의 분노", "즉시 레벨 +4", "보스 체력 +25%\n보스 패턴 간격 -20%\n(보스전)", () =>
            {
                Levels(4);
                RunStats.Multiply(RunStat.BossHp, 1.25f);
                RunStats.Multiply(RunStat.BossInterval, 0.8f);
            }),
            H("last_stand", "배수진", "경험치 획득량 2배\n(영구)", "모든 회복 효과 -50%\n(영구, 필살기 회복 포함)", () =>
            {
                RunStats.Multiply(RunStat.ExpGain, 2f);
                RunStats.Multiply(RunStat.HealScale, 0.5f);
            }),
        };
        Rework(list);
        return list;
    }

    // ---------------------------------------------------------------
    // 1119 아침 선택지 재구성: 18장 (성장 6 / 도전 6 / 지옥 6), 수치는 RunConfig.morningCards
    // 제거 13장은 데이터를 남기고 풀에서만 제외
    // ---------------------------------------------------------------
    private static readonly string[] RetiredIds =
    {
        "claws", "light_step", "sniff", "cat_sense", "drive_hunt", "fasting", "seal_ultimate",
        "dark_hunt", "kill_quest", "dream_training", "glass_claws", "frenzy", "endless_night",
    };

    private static void Rework(List<Choice> list)
    {
        MorningCardValues v = RunConfig.Instance.morningCards;
        const ModifierLife Today = ModifierLife.UntilMorning;
        foreach (Choice c in list)
            if (System.Array.IndexOf(RetiredIds, c.id) >= 0) c.retired = true;

        void Put(Choice c, bool lastDayBan = false)
        {
            c.lastDayBan = lastDayBan;
            int i = list.FindIndex(x => x.id == c.id);
            if (i >= 0) list[i] = c;
            else list.Add(c);
        }

        // 성장: 즉시 또는 오늘 하루
        Put(G("stretch", "기지개 켜기", "즉시 레벨업 " + v.stretchLevels + "회", () => Levels(v.stretchLevels)));
        Put(G("pocket_money", "쌈짓돈", "냥냥코인 +" + v.pocketMoneyCoins, () => CoinWallet.Add(v.pocketMoneyCoins)));
        Put(G("grooming", "그루밍", "체력 전부 회복", () => GameManager.instance.Heal(GameManager.instance.maxHealth * 2f)));
        Put(G("base_repair", "기지 수리", "기지 체력 +" + v.baseRepair + "\n(최대치까지)", () => NightDefense.Instance.Repair(v.baseRepair),
            () => NightDefense.Instance != null && NightDefense.Instance.BaseHp < NightDefense.Instance.BaseMaxHp), true);
        Put(G("engineer_cat", "공병 고양이", "오늘 밤 포탑 건설 비용\n-" + Mathf.RoundToInt((1f - v.engineerCostMul) * 100f) + "%",
            () => RunStats.Multiply(RunStat.TurretCost, v.engineerCostMul, Today),
            () => TurretSystem.Instance != null && TurretSystem.Instance.HasUnbuiltPad), true);

        // 도전: 다음 아침 또는 보스 등장 시 해제
        Put(C("capture_quest", "생포 의뢰", "경험치 획득량 +" + Mathf.RoundToInt((v.captureExpMul - 1f) * 100f) + "%\n(오늘)",
            "플레이어 공격력 -" + Mathf.RoundToInt((1f - v.captureDamageMul) * 100f) + "%\n(오늘, 포탑 제외)", () =>
            {
                RunStats.Multiply(RunStat.ExpGain, v.captureExpMul, Today);
                RunStats.Multiply(RunStat.Damage, v.captureDamageMul, Today);   // 손해: 따로 곱
            }));
        Put(C("elite_hunt", "엘리트 토벌", "오늘 낮 엘리트 처치 시\n냥냥코인 +" + v.eliteHuntCoins,
            "오늘 낮 엘리트 " + v.eliteHuntChanceMul + "배 등장\n체력 " + v.eliteHuntHealthMul + "배", () =>
            {
                RunStats.Add(RunStat.EliteBonusCoins, v.eliteHuntCoins, Today);
                RunStats.Multiply(RunStat.EliteChance, v.eliteHuntChanceMul, Today);
                RunStats.Multiply(RunStat.EliteHealth, v.eliteHuntHealthMul, Today);
            }));
        Put(C("heavy_bag", "무거운 배낭", "즉시 냥냥코인 +" + v.heavyBagCoins,
            "이동속도 -" + Mathf.RoundToInt((1f - v.heavyBagMoveMul) * 100f) + "%\n(오늘)", () =>
            {
                CoinWallet.Add(v.heavyBagCoins);
                RunStats.Multiply(RunStat.MoveSpeed, v.heavyBagMoveMul, Today);
            }));
        Put(C("nest_raid", "둥지 습격", "오늘 군락 파괴 보상\n" + v.nestRewardMul + "배",
            "오늘 군락 " + v.nestCount + "곳 생성,\n못 깬 곳마다 밤 체력\n+" + Mathf.RoundToInt(RunConfig.Instance.colonyPenaltyRate * 100f) + "%", () => RunStats.Add(RunStat.ColonyRaid, 1f, Today),   // 오늘 군락 nestCount곳·보상 x nestRewardMul
            () => ColonySystem.Instance != null && DayNightCycle.Instance != null
                  && DayNightCycle.Instance.DayCount >= RunConfig.Instance.colonyFirstDay && DayNightCycle.Instance.DayCount <= RunConfig.Instance.colonyLastDay), true);   // 군락이 생기는 2~4일 차 아침
        Put(C("boss_bounty", "우두머리 현상금", "오늘 낮 중간보스\n보상 상자 코인 " + v.bountyBoxCoinMul + "배,\n무리 처치 시 레벨업",
            "오늘 낮 중간보스\n체력 " + v.bountyHealthMul + "배", () =>
            {
                RunStats.Multiply(RunStat.MiniBossBounty, v.bountyBoxCoinMul, Today);
                RunStats.Multiply(RunStat.MiniBossHealthToday, v.bountyHealthMul, Today);
            }), true);
        Put(C("rush_night", "질주하는 밤", "오늘 밤 냥냥코인 " + v.rushCoinMul + "배",
            "오늘 밤 몬스터가\n더 빨리 돌진\n(대장 포함)", () =>
            {
                RunStats.Multiply(RunStat.NightCoinDrop, v.rushCoinMul, Today);
                RunStats.Multiply(RunStat.NightTravelTime, v.rushTravelTime / Mathf.Max(1f, RunConfig.Instance.nightTravelTime), Today);
            }), true);

        // 지옥: 영구, 한 판 1회
        Put(H("blood_pact", "피의 계약", "레벨업 " + v.bloodLevels + "회,\n냥냥코인 +" + v.bloodCoins,
            "밤 몬스터 체력 +" + Mathf.RoundToInt(v.bloodPactHpAdd * 100f) + "%,\n밤마다 대장 몬스터 +" + v.bloodPactExtraUnit + "\n(영구)", () =>
            {
                Levels(v.bloodLevels);
                CoinWallet.Add(v.bloodCoins);
                RunStats.Add(RunStat.NightHpAdd, v.bloodPactHpAdd);              // 밤 웨이브 체력 증가 합
                RunStats.Add(RunStat.NightBossExtra, v.bloodPactExtraUnit);      // 밤마다 대장 +1
            }, () => DayNightCycle.Instance != null && DayNightCycle.Instance.DayCount >= 2 && DayNightCycle.Instance.DayCount <= 3), true);   // 2~3일 차 아침에만
        Put(H("elite_march", "엘리트의 행진", "보유 무기 전부\n레벨 +1",
            "낮 엘리트 " + v.marchEliteChanceMul + "배 등장,\n공격·체력 " + v.marchEliteHealthMul + "배,\n크기 " + v.elitesMarchScale + "배 (영구)", () =>
            {
                RunStats.Multiply(RunStat.EliteScale, v.elitesMarchScale);       // 낮 엘리트 크기
                foreach (Item item in LevelableWeapons()) item.OnClick();
                RunStats.Multiply(RunStat.EliteChance, v.marchEliteChanceMul);
                RunStats.Multiply(RunStat.EliteHealth, v.marchEliteHealthMul);
                RunStats.Multiply(RunStat.EliteDamage, v.marchEliteDamageMul);
            }, () => LevelableWeapons().Count > 0));
        Put(H("beast_rage", "맹수의 분노", "즉시 레벨업 " + v.rageLevels + "회", "보스 체력 x" + v.rageBossHpMul + "\n(영구)", () =>
            {
                Levels(v.rageLevels);
                RunStats.Multiply(RunStat.BossHp, v.rageBossHpMul);
            }));
        Put(H("cursed_gold", "저주받은 금화", "냥냥코인 +" + v.cursedCoins, "레벨업 선택지 " + v.cursedChoices + "장\n(영구, 보너스 선택 포함)", () =>
            {
                CoinWallet.Add(v.cursedCoins);
                RunStats.LevelUpChoices = v.cursedChoices;
            }, () => RunStats.LevelUpChoices > v.cursedChoices));
        Put(H("last_stand", "배수진", "즉시 레벨업 " + v.lastStandLevels + "회\n냥냥코인 +" + v.lastStandCoins,
            "기지 최대 체력 x" + v.lastStandBaseMul + "\n(영구)", () =>
            {
                Levels(v.lastStandLevels);
                CoinWallet.Add(v.lastStandCoins);
                RunStats.Multiply(RunStat.BaseMaxHp, v.lastStandBaseMul);
                if (NightDefense.Instance != null) NightDefense.Instance.ClampBaseHp();   // 넘으면 새 최대치로
            }), true);
        Put(H("open_camp", "무방비 캠프", "즉시 레벨업 " + v.openCampLevels + "회\n냥냥코인 +" + v.openCampCoins,
            "포탑 발판 영구 봉인\n지은 포탑 철거\n(영구)", () =>
            {
                Levels(v.openCampLevels);
                CoinWallet.Add(v.openCampCoins);
                if (TurretSystem.Instance != null) TurretSystem.Instance.Seal();
            }, () => TurretSystem.Instance != null && !TurretSystem.Instance.Sealed), true);
    }

    // 5일 차(마지막 날) 아침인지
    private static bool LastMorning => DayNightCycle.Instance != null && DayNightCycle.Instance.DayCount >= DayNightCycle.Instance.LastDay;

    // ---------------- 테스트 모드 ----------------
    public static string ForcedId { get; set; }              // 다음 제시에 이 카드를 그 등급 자리에 강제로

    public static List<KeyValuePair<string, string>> CardList()
    {
        List<KeyValuePair<string, string>> r = new List<KeyValuePair<string, string>>();
        foreach (Choice c in All)
            if (!c.retired) r.Add(new KeyValuePair<string, string>(c.id, (c.tier == Tier.Growth ? "[성장] " : c.tier == Tier.Challenge ? "[도전] " : "[지옥] ") + c.name));
        return r;
    }

    public static bool IsRetired(string id) => All.Exists(c => c.id == id && c.retired);

    private static Choice G(string id, string name, string reward, System.Action apply, System.Func<bool> available = null)
    {
        return new Choice { id = id, tier = Tier.Growth, name = name, reward = reward, risk = "", apply = apply, available = available };
    }

    private static Choice C(string id, string name, string reward, string risk, System.Action apply, System.Func<bool> available = null)
    {
        return new Choice { id = id, tier = Tier.Challenge, name = name, reward = reward, risk = risk, apply = apply, available = available };
    }

    private static Choice H(string id, string name, string reward, string risk, System.Action apply, System.Func<bool> available = null)
    {
        return new Choice { id = id, tier = Tier.Hell, name = name, reward = reward, risk = risk, apply = apply, available = available };
    }

    private static void Levels(int count)
    {
        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.QueueLevelUps(count);
        }
    }

    // 레벨을 올릴 수 있는 보조무기 (보유 중, 최대 레벨 아님, 메인무기 아님)
    public static List<Item> LevelableWeapons()
    {
        List<Item> result = new List<Item>();
        if (LevelUp.instance == null)
            return result;

        foreach (Item item in LevelUp.instance.Items)
        {
            if (item.data.itemType <= ItemData.ItemType.Lance && item.level > 0 &&
                item.level < item.data.damages.Length && (item.weapon == null || !item.weapon.IsMain))
            {
                result.Add(item);
            }
        }
        return result;
    }

    // 레벨이 가장 높은 무기 레벨 +1 (같으면 무작위)
    public static bool LevelUpBestWeapon()
    {
        List<Item> weapons = LevelableWeapons();
        if (weapons.Count == 0)
            return false;

        int best = 0;
        foreach (Item item in weapons)
        {
            best = Mathf.Max(best, item.level);
        }
        weapons.RemoveAll(item => item.level < best);
        weapons[Random.Range(0, weapons.Count)].OnClick();
        return true;
    }

    // ---------------------------------------------------------------
    // 화면
    // ---------------------------------------------------------------
    public static void Open(int picks)
    {
        if (IsOpen)
            return;

        IsOpen = true;
        picksLeft = Mathf.Max(1, picks);
        GameManager.instance.Stop();
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Player_LevelUp);

        BuildScreen();
        Roll();

        // 등장 연출: 위에서 금빛 빛줄기 + 카드 주변 반짝임
        RectTransform root = canvas != null ? (RectTransform)canvas.transform : null;
        // 카드 묶음 테두리 둘레에서 작은 별이 하나씩 은은하게 반짝임 (sub_effect_1, 글자는 가리지 않음)
        // (반짝임 이펙트 없음)
    }

    private static void Close()
    {
        IsOpen = false;
        if (canvas != null)
        {
            Object.Destroy(canvas.gameObject);
        }
        canvas = null;
        cards.Clear();
        GameManager.instance.Resume();
    }

    private static void BuildScreen()
    {
        canvas = RunUI.CreateCanvas("Morning Choice", 30, true);
        Transform root = canvas.transform;

        Image dim = UltimateManager.CreateRect("Dim", root).gameObject.AddComponent<Image>();
        UltimateManager.Stretch(dim.rectTransform);
        dim.color = new Color(0.05f, 0.04f, 0.02f, 0.7f);

        // 가로 화면에서는 캔버스 높이가 720 정도라 위아래가 잘림 → 내용 묶음을 화면 높이에 맞춰 축소
        content = RunUI.Rect("Content", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, RunConfig.Instance.morningContentHeight));
        content.gameObject.AddComponent<UiFitHeight>().needHeight = RunConfig.Instance.morningContentHeight;
        root = content;

        title = RunUI.Label("Title", root, "", 38, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(700f, 110f), 3f);

        // 지속 시간 규칙 안내
        RunUI.Label("Rule", root, "도전 리스크 : 다음 아침까지 (보스 등장 시 해제)\n지옥 리스크 : 게임 끝까지", 18,
            new Color(1f, 0.95f, 0.85f, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 262f), new Vector2(700f, 30f), 2f);

        // 다시 뽑기: 무늬 양피지 바 + 진한 갈색 글자 (없으면 기존 갈색 버튼)
        RunConfig rc = RunConfig.Instance;
        bool parchment = rc.rerollPanel != null;
        rerollButton = RunUI.AssetButton("Reroll", root, parchment ? rc.rerollPanel : rc.buttonBrown, "", 26,
            parchment ? rc.parchmentTextColor : new Color(0.3f, 0.35f, 0.5f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), parchment ? new Vector2(380f, 74f) : new Vector2(320f, 68f), Reroll);
    }

    private static void Roll()
    {
        foreach (GameObject card in cards)
        {
            Object.Destroy(card);
        }
        cards.Clear();

        Tier[] tiers = { Tier.Growth, Tier.Challenge, Tier.Hell };
        List<Choice> picked = new List<Choice>();
        foreach (Tier tier in tiers)
        {
            List<Choice> pool = All.FindAll(c => c.tier == tier && !c.retired &&
                !(c.lastDayBan && LastMorning) &&                 // 5일 차 아침 제외 카드
                (c.tier != Tier.Hell || !usedHell.Contains(c.id)) &&
                (c.available == null || c.available()));
            Choice forced = ForcedId != null ? All.Find(c => c.id == ForcedId && c.tier == tier && !c.retired) : null;
            if (forced != null)
            {
                picked.Add(forced);                              // 테스트 모드 강제 제시
            }
            else if (pool.Count > 0)
            {
                picked.Add(pool[Random.Range(0, pool.Count)]);
            }
        }
        ForcedId = null;

        float spacing = RunConfig.Instance.morningCardSpacing;
        for (int i = 0; i < picked.Count; i++)
        {
            float x = (i - (picked.Count - 1) * 0.5f) * spacing;
            cards.Add(CreateCard(picked[i], new Vector2(x, 0f)));
        }

        RefreshHeader();
    }

    private static void RefreshHeader()
    {
        title.text = picksLeft > 1
            ? "아침이 밝았다냥!\n" + picksLeft + "장을 고르라냥"
            : "아침이 밝았다냥!\n하나를 고르라냥";

        LevelUpReroll reroll = LevelUpReroll.Instance;
        bool canReroll = RunConfig.Instance.allowMorningReroll && reroll != null && (reroll.Remaining > 0 || TestMode.InfiniteReroll);
        rerollButton.gameObject.SetActive(RunConfig.Instance.allowMorningReroll && reroll != null);
        rerollButton.interactable = canReroll;
        RunUI.SetButtonText(rerollButton, "다시 뽑기 (리롤 " + (TestMode.InfiniteReroll ? "∞" : reroll != null ? reroll.Remaining.ToString() : "0") + ")");
    }

    private static void Reroll()
    {
        LevelUpReroll reroll = LevelUpReroll.Instance;
        if (reroll == null || !reroll.TryUse())
            return;

        RunUI.ClickSound();
        if (canvas != null)
        {
            // 다시 뽑기: 버튼과 카드 모서리에 작은 반짝임만 (카드를 가리지 않게)
            UiFx.Play(UiFx.Kind.Sparkle, (RectTransform)rerollButton.transform, new Vector2(-150f, 10f), 70f, null, 18f);
            UiFx.Play(UiFx.Kind.Sparkle, (RectTransform)rerollButton.transform, new Vector2(150f, -5f), 60f, null, 18f);
            UiFx.Play(UiFx.Kind.Stars, (RectTransform)canvas.transform, new Vector2(-250f, 230f), 80f, null, 14f);
            UiFx.Play(UiFx.Kind.Stars, (RectTransform)canvas.transform, new Vector2(255f, -170f), 80f, null, 14f);
        }
        Roll();
    }

    private static GameObject CreateCard(Choice choice, Vector2 position)
    {
        RunConfig config = RunConfig.Instance;
        Color tierColor;
        string tierName;
        Sprite tierIcon;
        Sprite tierBar;
        switch (choice.tier)
        {
            case Tier.Growth: tierColor = config.growthColor; tierName = "성장"; tierIcon = config.growthIcon; tierBar = config.morningBarGreen != null ? config.morningBarGreen : config.buttonGreen; break;
            case Tier.Challenge: tierColor = config.challengeColor; tierName = "도전"; tierIcon = config.challengeIcon; tierBar = config.morningBarYellow != null ? config.morningBarYellow : config.buttonYellow; break;
            default: tierColor = config.hellColor; tierName = "지옥"; tierIcon = config.hellIcon; tierBar = config.morningBarRed != null ? config.morningBarRed : config.buttonRed; break;
        }

        // 레벨업 카드와 같은 그림(lv_card 틀 + lv_button 이름표 + lv_divider)으로 통일
        GimmickCatalog cat = GimmickCatalog.Instance;
        Vector2 size = config.morningCardSize;
        RectTransform holder = RunUI.Rect("Card " + choice.id, content, new Vector2(0.5f, 0.5f), position, size);
        GameObject cardObject = holder.gameObject;

        Image back = CardImage("Frame", holder, cat != null ? cat.card : null, Vector2.zero, size, new Color(0.96f, 0.9f, 0.8f, 1f));
        back.raycastTarget = true;
        Transform card = back.transform;
        Color dark = cat != null ? cat.textDark : config.parchmentTextColor;
        float h = size.y, w = size.x;

        // 위 이름표: 등급 색 + 카드 이름 (레벨업 이름표와 같은 위치·글꼴)
        float nameY = h * 0.5f - (cat != null ? cat.cardNameFromTop : 62f);
        Vector2 tagSize = cat != null ? cat.cardTagSize : new Vector2(214f, 66f);
        Image tag = CardImage("Tag", card, cat != null ? cat.buttonGrey : null, new Vector2(0f, nameY), tagSize, tierColor);
        tag.color = tierColor;
        Text nameLabel = RunUI.Label("Name", tag.transform, choice.name, cat != null ? cat.cardNameFontSize : 26, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, tagSize - new Vector2(24f, 6f), 2f);
        nameLabel.resizeTextForBestFit = true;
        nameLabel.resizeTextMinSize = 14;
        nameLabel.resizeTextMaxSize = cat != null ? cat.cardNameFontSize : 26;

        // 큰 효과 아이콘 + 왼쪽 위 등급 표시
        float iconSize = cat != null ? cat.cardIconSize : 84f;
        float iconTop = nameY - tagSize.y * 0.5f - 10f;
        float iconY = iconTop - iconSize * 0.5f;
        Sprite cardIcon = CardIcon(choice.id);
        RunUI.Icon("Icon", card, cardIcon != null ? cardIcon : tierIcon, new Vector2(0.5f, 0.5f), new Vector2(0f, iconY), Vector2.one * iconSize);
        RunUI.Icon("Tier Icon", tag.transform, tierIcon, new Vector2(0f, 0.5f), new Vector2(4f, 4f), new Vector2(34f, 34f));

        // 아래 단계판 자리: 등급 + 지속 시간
        float stepY = -h * 0.5f + (cat != null ? cat.cardStepFromBottom : 66f);
        string life = choice.tier == Tier.Growth ? "즉시" : choice.tier == Tier.Challenge ? "아침까지" : "영구";
        Text tierLabel = RunUI.Label("Tier", card, tierName + " · " + life, 18, new Color(tierColor.r * 0.6f, tierColor.g * 0.6f, tierColor.b * 0.6f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, stepY), new Vector2(w * 0.6f, 30f), 0f);
        RunUI.ApplyPanelFont(tierLabel);

        // 가운데: 보상(위) / 구분선 / 위험(아래), 각 구역 안에서 세로 가운데
        float areaTop = iconTop - iconSize - 8f;
        float areaBottom = stepY + 36f;                         // 아래 단계판과 겹치지 않게
        bool hasRisk = !string.IsNullOrEmpty(choice.risk);
        Color good = new Color(0.16f, 0.45f, 0.12f), bad = new Color(0.72f, 0.1f, 0.06f);
        if (hasRisk)
        {
            float mid = (areaTop + areaBottom) * 0.5f;
            EffectBlock(card, "Reward", "▲ 보상", choice.reward, good, areaTop, mid + 5f, w);
            Image line = CardImage("Divider", card, cat != null ? cat.divider : null, new Vector2(0f, mid), new Vector2(w * 0.7f, 8f), new Color(dark.r, dark.g, dark.b, 0.3f));
            line.raycastTarget = false;
            EffectBlock(card, "Risk", "▼ 위험", choice.risk, bad, mid - 5f, areaBottom, w);
        }
        else
        {
            EffectBlock(card, "Reward", "▲ 보상", choice.reward, good, areaTop, areaBottom, w);
        }

        Button button = back.gameObject.AddComponent<Button>();
        button.targetGraphic = back;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);            // 마우스를 올리면 살짝 밝게
        colors.pressedColor = new Color(0.9f, 0.82f, 0.7f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => Pick(choice, cardObject));

        // 나타날 때 살짝 커지는 효과
        cardObject.AddComponent<CardPop>();
        return cardObject;
    }

    // 레벨업 카드와 같은 방식의 그림 (테두리 있으면 9-slice)
    private static Image CardImage(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color fallback)
    {
        Image image = RunUI.Rect(name, parent, new Vector2(0.5f, 0.5f), pos, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = GimmickCatalog.Instance != null ? GimmickCatalog.Instance.pixelScale : 1f;
        }
        else
        {
            image.color = fallback;
        }
        return image;
    }

    // 보상·위험 한 구역: 작은 머리글 + 내용, 구역(top~bottom) 안에서 세로 가운데
    private static void EffectBlock(Transform card, string name, string header, string body, Color color, float top, float bottom, float width)
    {
        Text t = RunUI.Label(name, card, "<size=15><b>" + header + "</b></size>\n" + body, 19, color, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, (top + bottom) * 0.5f), new Vector2(width * 0.8f, Mathf.Max(20f, top - bottom)), 0f);
        t.supportRichText = true;
        t.lineSpacing = 1.05f;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = 19;
        RunUI.ApplyPanelFont(t);
    }

    // 테두리 양피지 상자 안에 글자 (보상/리스크 구분, 예전 카드)
    private static void CreateTextBox(Transform card, string name, string content, Color textColor, Color tint, float top, float height,
        string headerText = null, Sprite headerIcon = null)
    {
        RunConfig config = RunConfig.Instance;
        // Morning_card 보상(초록)·리스크(붉은) 상자, 없으면 기존 양피지 줄
        Sprite boxSprite = name == "Risk" ? config.morningBoxRed : config.morningBoxGreen;
        bool mc = boxSprite != null;
        Image box = RunUI.AssetPanel(name + " Box", card, mc ? boxSprite : config.rowSprite, mc ? boxSprite.border : config.rowBorder,
            new Color(tint.r, tint.g, tint.b, 0.25f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(196f, height));
        box.rectTransform.pivot = new Vector2(0.5f, 1f);
        box.rectTransform.anchoredPosition = new Vector2(0f, top);
        if (mc)
        {
            box.color = Color.white;
        }
        else if (config.rowSprite != null)
        {
            box.color = tint;
        }
        box.raycastTarget = false;

        // 머리표: 작은 아이콘 + "보상"/"위험" (상자 위 테두리에 걸침)
        float headerH = 0f;
        if (!string.IsNullOrEmpty(headerText))
        {
            headerH = 18f;
            // 진한 등급 색 바탕 + 흰 글자 (밝은 상자 위에서도 또렷하게)
            Image tag = RunUI.Panel(name + " Tag", box.transform, new Color(textColor.r, textColor.g, textColor.b, 0.95f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(96f, 28f));
            tag.raycastTarget = false;
            Outline tagLine = tag.gameObject.AddComponent<Outline>();
            tagLine.effectColor = new Color(1f, 0.95f, 0.85f, 0.9f);
            tagLine.effectDistance = new Vector2(1.5f, -1.5f);
            RunUI.Icon("Icon", tag.transform, headerIcon, new Vector2(0f, 0.5f), new Vector2(17f, 0f), new Vector2(20f, 20f));
            Text tagText = RunUI.Label("Text", tag.transform, headerText, 18, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(64f, 26f), 0f);
            RunUI.ApplyPanelFont(tagText);
        }

        Text label = RunUI.Label(name, box.transform, content, 20, textColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -headerH * 0.5f), new Vector2(164f, height - 26f - headerH), 0f);   // 테두리 안쪽 여백 (글자가 꽉 차 터져 보이지 않게)
        label.raycastTarget = false;
        RunUI.ApplyPanelFont(label);                             // 작은 설명 글자는 또렷한 글꼴

        // 글이 길면 상자 안에 들어가도록 글자를 자동으로 줄임
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 11;
        label.resizeTextMaxSize = 19;
        label.lineSpacing = 0.95f;
    }

    // 카드 효과를 나타내는 아이콘 (상점 능력치 아이콘·게임 아이콘), 없으면 null → 등급 아이콘
    private static Sprite CardIcon(string id)
    {
        RunConfig rc = RunConfig.Instance;
        TurretConfig tc = TurretConfig.Instance;
        switch (id)
        {
            case "stretch":
            case "capture_quest": return StatIcon(RunStat.ExpGain);
            case "pocket_money":
            case "cursed_gold": return StatIcon(RunStat.CoinDrop);
            case "grooming": return StatIcon(RunStat.MaxHealthFlat);
            case "base_repair":
            case "last_stand": return rc.tentSprite;
            case "engineer_cat":
            case "open_camp": return tc != null && tc.crossbow.attack != null && tc.crossbow.attack.Length > 0 ? tc.crossbow.attack[0] : null;   // 석궁 포탑
            case "elite_hunt":
            case "beast_rage": return StatIcon(RunStat.Damage);
            case "heavy_bag":
            case "boss_bounty": return rc.coinPouchIcon;
            case "nest_raid": return rc.bombIcon;
            case "rush_night": return StatIcon(RunStat.MoveSpeed);
            case "blood_pact": return StatIcon(RunStat.SkillPower);
            case "elite_march": return StatIcon(RunStat.WeaponSize);
        }
        return null;
    }

    private static Sprite StatIcon(RunStat stat)
    {
        StatCatalog cat = StatCatalog.Instance;
        StatCatalog.StatDef def = cat != null ? cat.Def(stat) : null;
        return def != null ? def.icon : null;
    }

    private static void Pick(Choice choice, GameObject cardObject)
    {
        if (!IsOpen)
            return;

        RunUI.ClickSound();
        if (choice.tier == Tier.Hell)
        {
            usedHell.Add(choice.id);
            RunStats.AddLabel(choice.name, ModifierLife.Permanent);       // 능력치 창: 지옥 효과 (끝까지)
        }
        else if (choice.tier == Tier.Challenge)
        {
            RunStats.AddLabel(choice.name, ModifierLife.UntilMorning);    // 능력치 창: 도전 효과 (아침까지)
        }
        choice.apply();

        picksLeft--;
        cards.Remove(cardObject);
        Object.Destroy(cardObject);

        if (picksLeft <= 0 || cards.Count == 0)
        {
            Close();
        }
        else
        {
            RefreshHeader();
        }
    }
}
