using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 밤 디펜스 (DayNightCycle이 붙임)
// - 밤 동안 숲 스폰 지점(웨이브 경로 시작)에서 그날의 몬스터를 계속 소환, 경로를 따라 입구로만 이동
// - 입구에 닿으면 기지 체력 -1 (보상 없이 사라짐), 기지 체력 0이면 게임 오버
// - 밤이 끝나면 길 위 밤 몬스터는 보상 없이 사라짐
// - 귀환 준비 중: 화면 위 경고 문구, 기지가 화면 밖이면 가장자리 방향 표시, 기지 쪽으로 움직이면 이동속도 증가
public class NightDefense : MonoBehaviour
{
    public static NightDefense Instance { get; private set; }

    public int BaseHp { get; private set; }
    // 기지 최대 체력 (아침 선택지 배수진: 영구 x0.5)
    public int BaseMaxHp => Mathf.Max(1, Mathf.RoundToInt(RunConfig.Instance.baseMaxHp * RunStats.Get(RunStat.BaseMaxHp)));

    // 아침 선택지 기지 수리: 최대치를 넘지 않게 회복
    public void Repair(int amount)
    {
        BaseHp = Mathf.Min(BaseMaxHp, BaseHp + Mathf.Max(0, amount));
    }

    // 최대 체력이 줄면 현재 체력을 새 최대치로 맞춤
    public void ClampBaseHp()
    {
        BaseHp = Mathf.Min(BaseHp, BaseMaxHp);
    }
    public static int NightCount => Instance != null ? Instance.alive.Count : 0;
    // 지금 길 위의 밤 웨이브 몬스터 (포탑 조준용, 읽기만)
    public static IEnumerable<Enemy> NightEnemies => Instance != null ? (IEnumerable<Enemy>)Instance.alive : System.Array.Empty<Enemy>();

    private readonly HashSet<Enemy> alive = new HashSet<Enemy>();
    private RunConfig config;
    private GameManager gm;
    private float spawnTimer;
    private bool warnedNoPath;

    // UI
    private GameObject barRoot;
    private Image panelHitFlash;
    private float panelHitAt = -10f;
    private Image barFill;
    private RectTransform barMask;
    private float barWidth;                                  // 기지 체력 게이지 너비 (판 너비에서 계산)                           // 채움 너비만큼 잘라 보이는 마스크 (바 그림은 늘 전체 크기)
    private Text barLabel;
    private Text prepText;
    private RectTransform baseArrow;
    private RectTransform canvasRect;

    private void Awake()
    {
        Instance = this;
        config = RunConfig.Instance;
        gm = GetComponent<GameManager>();
        BaseHp = config.baseMaxHp;
        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 새 판 시작 (DayNightCycle.StartRun)
    public void ResetRun()
    {
        BaseHp = config.baseMaxHp;
        spawnTimer = 0f;
        ClearNight();
    }

    private void LateUpdate()
    {
        UpdateReturnBuffIcon();
    }

    // ---------------------------------------------------------------
    // 귀환 버프 표시: 귀환 준비 중 기지 쪽으로 달려 이동속도 버프가 붙는 동안 체력바(탄창 게이지 있으면 그 아래)에 달리기 아이콘
    // ---------------------------------------------------------------
    private RectTransform hpBarRect;
    private Image returnBuffIcon;

    private void UpdateReturnBuffIcon()
    {
        Player p = gm != null ? gm.player : null;
        DayNightCycle dn = DayNightCycle.Instance;
        if (hpBarRect == null && p != null)
        {
            foreach (Slider slider in FindObjectsOfType<Slider>())
                if (slider.name == "Health Slider") { hpBarRect = (RectTransform)slider.transform; break; }
        }
        if (hpBarRect == null || hpBarRect.parent == null)
            return;
        if (returnBuffIcon == null)
        {
            returnBuffIcon = RunUI.Icon("Return Buff Icon", hpBarRect.parent, config.returnBuffIcon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * config.returnBuffIconSize);
            returnBuffIcon.preserveAspect = true;
            returnBuffIcon.raycastTarget = false;
        }
        bool on = config.returnBuffIcon != null && p != null && dn != null && dn.InReturnPrep && gm.isLive && hpBarRect.gameObject.activeInHierarchy
            && ReturnSpeedScale(p.inputVec, p.transform.position) > 1f;
        if (returnBuffIcon.gameObject.activeSelf != on) returnBuffIcon.gameObject.SetActive(on);
        if (!on) return;

        // 체력바는 플레이어 자식 월드 캔버스(Follow)에 있음 → 체력바 아래 끝과 탄창 게이지 아래 끝(월드) 중 더 아래, 부모(Health) 좌표로
        Transform holder = hpBarRect.parent;
        float bottom = hpBarRect.localPosition.y + hpBarRect.rect.yMin;
        Transform gauge = p.transform.Find("Main Weapon Gauge");
        if (gauge != null && gauge.gameObject.activeInHierarchy)
        {
            float minY = float.MaxValue;
            foreach (SpriteRenderer r in gauge.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled && r.sprite != null) minY = Mathf.Min(minY, r.bounds.min.y);
            if (minY < float.MaxValue)
                bottom = Mathf.Min(bottom, holder.InverseTransformPoint(new Vector3(holder.position.x, minY, holder.position.z)).y);
        }
        // 크기·간격은 1080p 화면 픽셀 기준 → 월드 → 부모 좌표
        Camera cam = Camera.main;
        float worldPerPx = cam != null && cam.orthographic ? 2f * cam.orthographicSize / 1080f : 0.02f;
        float k = worldPerPx / Mathf.Max(0.0001f, holder.lossyScale.y);
        float size = config.returnBuffIconSize * k;
        returnBuffIcon.rectTransform.sizeDelta = new Vector2(size, size);
        returnBuffIcon.rectTransform.localPosition = new Vector3(0f, bottom - config.returnBuffIconGap * k - size * 0.5f, 0f);
    }

    private void Update()
    {
        UpdateUi();
        UpdateGateShake();
        // 밤 시작 판정은 낮/밤 전환만 봄 (예전에는 '게임 진행 중'도 함께 봐서 레벨업 등 일시정지가 끝날 때마다 밤이 새로 시작된 것으로 처리
        // → 대장 일정이 처음부터 다시 잡혀 추가로 나오고 늦게 나와 새벽까지 못 오던 원인)
        bool nightPhase = DayNightCycle.IsNight;
        if (nightPhase && !wasNight) BeginNightWaves();
        wasNight = nightPhase;
        if (!nightPhase || gm == null || !gm.isLive)
            return;

        int day = DayNightCycle.Instance != null ? DayNightCycle.Instance.DayCount : 1;
        UpdateBossUnits(day);
        UpdateSpawnPhase(day);
    }

    // ---------------------------------------------------------------
    // 스폰 단계 변주 (잔잔·보통·러시): 간격 = 밤별 기존 간격 x 배율, 밤별 총 수·스폰 시간 범위는 예전과 같게
    // ---------------------------------------------------------------
    public enum SpawnPhase { Calm, Normal, Rush }
    public SpawnPhase Phase { get; private set; }
    private float phaseLeft, noRushUntil;
    private int spawnTotal, spawnedCount;
    public int SpawnTotal => spawnTotal;
    public int SpawnedCount => spawnedCount;

    private void BeginNightWaves()
    {
        PlanBossUnits();
        int day = DayNightCycle.Instance != null ? DayNightCycle.Instance.DayCount : 1;
        float interval = Mathf.Max(0.05f, Pick(config.nightSpawnInterval, day, 1f));
        spawnTotal = Mathf.FloorToInt(config.nightDuration / interval + 0.0001f);   // 예전과 같은 총 수 (밤 내내 interval마다)
        spawnedCount = 0;
        spawnTimer = 0f;
        noRushUntil = 0f;
        SetPhase(SpawnPhase.Normal);                          // 첫 단계는 보통
    }

    private void SetPhase(SpawnPhase p)
    {
        Phase = p;
        Vector2 range = p == SpawnPhase.Calm ? config.phaseCalmTime : p == SpawnPhase.Rush ? config.phaseRushTime : config.phaseNormalTime;
        phaseLeft = Random.Range(range.x, Mathf.Max(range.x, range.y));
    }

    private float PhaseMul(SpawnPhase p) => p == SpawnPhase.Calm ? config.phaseIntervalMul.x : p == SpawnPhase.Rush ? config.phaseIntervalMul.z : config.phaseIntervalMul.y;

    private void UpdateSpawnPhase(int day)
    {
        if (spawnedCount >= spawnTotal)
            return;                                          // 총 수를 다 내보냄
        float interval = Mathf.Max(0.05f, Pick(config.nightSpawnInterval, day, 1f));
        int remaining = spawnTotal - spawnedCount;
        float timeLeft = Mathf.Max(0f, config.nightDuration - nightElapsed);
        bool mustRush = remaining > timeLeft / Mathf.Max(0.01f, interval * config.phaseIntervalMul.z);   // 남은 수를 러시로만 겨우 낼 수 있음 → 러시 고정

        phaseLeft -= Time.deltaTime;
        if (mustRush && Phase != SpawnPhase.Rush) SetPhase(SpawnPhase.Rush);
        else if (!mustRush && phaseLeft <= 0f) SetPhase(NextPhase(interval));

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= interval * PhaseMul(Phase))
        {
            spawnTimer = 0f;
            spawnedCount++;
            SpawnNightMonster(day);
        }
    }

    private SpawnPhase NextPhase(float interval)
    {
        bool rushOk = Phase != SpawnPhase.Rush && nightElapsed >= noRushUntil;   // 러시 연속 금지, 대장 직후 금지
        float expected = nightElapsed / interval;             // 예전 방식이라면 지금까지 낸 수
        if (spawnedCount - expected > config.phasePaceSlack) return SpawnPhase.Calm;   // 많이 앞섬 → 잔잔 (밤 끝까지 이어지게)
        float wc = config.phaseWeight.x, wn = config.phaseWeight.y, wr = rushOk ? config.phaseWeight.z : 0f;
        float r = Random.value * Mathf.Max(0.0001f, wc + wn + wr);
        if (r < wc) return SpawnPhase.Calm;
        if (r < wc + wn) return SpawnPhase.Normal;
        return SpawnPhase.Rush;
    }

    // 기본 속도: 세 종류 비율로 가중 평균한 이동 시간이 travel이 되도록
    // 일반 L/(b(1+u)) 평균, 돌격 L/(w b) - 돌진시간 x (돌진배율/w - 1), 굼벵이 L/(s b)
    private float BaseNightSpeed(float pathLength, float travel)
    {
        Vector3 r = config.speedTypeRate;
        float sum = Mathf.Max(0.0001f, r.x + r.y + r.z);
        float pn = r.x / sum, pc = r.y / sum, ps = r.z / sum;
        float j = Mathf.Clamp(config.normalSpeedJitter, 0f, 0.9f);
        float jitterAvg = j > 0.0001f ? Mathf.Log((1f + j) / (1f - j)) / (2f * j) : 1f;
        float w = Mathf.Max(0.05f, config.chargerWalkMul);
        float a = pn * jitterAvg + pc / w + ps / Mathf.Max(0.05f, config.slowpokeMul);
        float dashGain = pc * config.chargerDashTime * (config.chargerDashMul / w - 1f);
        return pathLength * a / Mathf.Max(0.1f, travel + dashGain);
    }

    private static float Pick(float[] values, int day, float fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(day - 1, 0, values.Length - 1)];
    }

    // ---------------------------------------------------------------
    // 대장 몬스터: 밤마다 정해진 수를 구간 안 무작위 시점에 (밤 시작부터, 타이머 정지 구간 제외)
    // ---------------------------------------------------------------
    private bool wasNight;
    private float nightElapsed;
    private readonly List<float> bossTimes = new List<float>();
    private float bossNoticeUntil;
    private Text bossNotice;
    private RectTransform bossNoticeRoot;
    private Image bossNoticeIcon;

    public string BossScheduleText
    {
        get
        {
            if (!DayNightCycle.IsNight) return "대장: 밤 아님";
            string s = "단계 " + (Phase == SpawnPhase.Calm ? "잔잔" : Phase == SpawnPhase.Rush ? "러시" : "보통") + " 스폰 " + spawnedCount + "/" + spawnTotal
                + " | 대장 남은 " + bossTimes.Count + " (" + nightElapsed.ToString("0") + "초, 마감 " + BossDeadline.ToString("0") + ")";
            foreach (float t in bossTimes) s += " " + t.ToString("0.0");
            return s;
        }
    }

    // 대장 스폰 마감 = 밤 길이 - 대장 이동 시간 x bossArrivalMargin (질주하는 밤 등 이동 시간이 바뀌면 그때 값으로 다시 계산)
    public float BossDeadline => Mathf.Max(0f, config.nightDuration
        - config.nightTravelTime * RunStats.Get(RunStat.NightTravelTime) * config.bossUnitTravelMul * config.bossArrivalMargin);

    private float BossTime(Vector2 w)
    {
        float deadline = BossDeadline;
        float end = Mathf.Min(Mathf.Max(w.x, w.y), deadline);   // 구간 끝은 마감을 넘지 않음
        float start = Mathf.Min(w.x, end);
        return Random.Range(start, end);
    }

    private void PlanBossUnits()
    {
        bossTimes.Clear();
        nightElapsed = 0f;
        int day = DayNightCycle.Instance != null ? DayNightCycle.Instance.DayCount : 1;
        NightBossPlan[] plans = config.bossUnitPlans;
        if (plans != null && plans.Length > 0)
        {
            NightBossPlan plan = plans[Mathf.Clamp(day - 1, 0, plans.Length - 1)];
            if (plan != null && plan.windows != null)
                foreach (Vector2 w in plan.windows) bossTimes.Add(BossTime(w));
        }
        int extra = Mathf.RoundToInt(RunStats.Get(RunStat.NightBossExtra));   // 피의 계약: 밤마다 +1
        for (int i = 0; i < extra; i++) bossTimes.Add(BossTime(config.bossUnitExtraWindow));
        bossTimes.Sort();
    }

    private void UpdateBossUnits(int day)
    {
        nightElapsed += Time.deltaTime;
        float deadline = BossDeadline;
        while (bossTimes.Count > 0 && (nightElapsed >= bossTimes[0] || nightElapsed >= deadline))   // 마감이 지나면 남은 대장 즉시
        {
            bossTimes.RemoveAt(0);
            SpawnBossUnit(day);
        }
    }

    // 테스트 모드 '대장 몬스터 즉시 소환'
    public void SpawnBossUnitNow()
    {
        if (!DayNightCycle.IsNight) return;
        SpawnBossUnit(DayNightCycle.Instance != null ? DayNightCycle.Instance.DayCount : 1);
    }

    private void SpawnBossUnit(int day)
    {
        Enemy e = SpawnNightMonster(day, true);
        if (e == null) return;
        if (config.bossUnitSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(config.bossUnitSound);
        bossNoticeUntil = Time.unscaledTime + config.bossUnitTextTime;
        noRushUntil = nightElapsed + config.noRushAfterBoss;  // 대장 직후 러시 금지, 러시 중이면 보통으로
        if (Phase == SpawnPhase.Rush) SetPhase(SpawnPhase.Normal);
    }

    // 밤 웨이브 일반 몬스터 최종 체력 = nightHp x (1 + 증가 합: 피의 계약, 군락 미처치 페널티)
    public float NightMonsterHealth(int day)
    {
        float hp = config.nightHp != null && config.nightHp.Length > 0 ? config.nightHp[Mathf.Clamp(day - 1, 0, config.nightHp.Length - 1)] : 10f;
        return hp * (1f + RunStats.Get(RunStat.NightHpAdd));
    }

    // 그날의 몬스터 하나를 경로 시작점에 소환 (boss = 대장 몬스터)
    private Enemy SpawnNightMonster(int day, bool boss = false)
    {
        WavePath path = WavePath.Instance;
        if (path == null || path.Length <= 0.01f)
        {
            if (!warnedNoPath) Debug.LogWarning("[밤 디펜스] 씬에 Wave Path가 없어 밤 웨이브를 소환하지 않습니다");
            warnedNoPath = true;
            return null;
        }
        Spawner spawner = gm.player.GetComponentInChildren<Spawner>(true);
        if (spawner == null || spawner.spawnData == null || spawner.spawnData.Length == 0)
            return null;

        int index = Mathf.Clamp(day - 1, 0, spawner.spawnData.Length - 1);
        SpawnData table = spawner.spawnData[index];
        int species = config.nightSpecies != null && config.nightSpecies.Length > 0
            ? config.nightSpecies[Mathf.Clamp(day - 1, 0, config.nightSpecies.Length - 1)] : table.spriteType;
        SpawnData data = new SpawnData
        {
            spriteType = species,
            health = table.health,
            speed = table.speed,
            spawnTime = table.spawnTime,
            chance = 0,
            stronger = 1f,
            expIndex = 1,
        };

        Enemy enemy = gm.pool.Get(0).GetComponent<Enemy>();
        enemy.Init(data, 1);                                   // 경험치 구슬 1개, 엘리트 판정 없음
        float lane = Random.Range(-config.nightLaneWidth, config.nightLaneWidth);
        float travel = config.nightTravelTime * RunStats.Get(RunStat.NightTravelTime);   // 아침 선택지 질주하는 밤: 오늘 밤 도착 시간 단축
        float hp = NightMonsterHealth(day);
        if (boss)
        {
            travel *= config.bossUnitTravelMul;                // 대장: 이동 시간 x2 (질주하는 밤 반영)
            hp *= config.bossUnitHpMul;                        // 대장: 체력 x20
        }
        enemy.BeginNightWave(path, path.Length / Mathf.Max(0.1f, travel), hp, lane);
        if (boss) enemy.MakeBossUnit(config.bossUnitScale, config.bossUnitTint, config.bossUnitAuraColor);
        else
        {
            // 이동 속도 3종: 일반 / 돌격(걷다가 경로 중간에서 돌진) / 굼벵이. 기본 속도는 가중 평균 이동 시간이 travel이 되게
            float b = BaseNightSpeed(path.Length, travel);
            Vector3 r = config.speedTypeRate;
            float roll = Random.value * Mathf.Max(0.0001f, r.x + r.y + r.z);
            if (roll < r.x)
                enemy.SetNightSpeedType(0, b * (1f + Random.Range(-config.normalSpeedJitter, config.normalSpeedJitter)), 0f, 0f, 0f);
            else if (roll < r.x + r.y)
                enemy.SetNightSpeedType(1, b * config.chargerWalkMul, path.Length * Random.Range(config.chargerDashRange.x, Mathf.Max(config.chargerDashRange.x, config.chargerDashRange.y)),
                    b * config.chargerDashMul, config.chargerDashTime);
            else
                enemy.SetNightSpeedType(2, b * config.slowpokeMul, 0f, 0f, 0f);
        }
        alive.Add(enemy);
        return enemy;
    }

    public static void Remove(Enemy enemy)
    {
        if (Instance != null) Instance.alive.Remove(enemy);
    }

    // 밤 몬스터가 입구에 닿음: 기지 체력 -1, 보상 없이 사라짐
    public static void ReachGate(Enemy enemy)
    {
        RunConfig c = RunConfig.Instance;
        Vector3 at = enemy.transform.position;
        int damage = enemy.BossUnit ? c.bossUnitGateDamage : 1;   // 대장: 기지 체력 -10
        enemy.FadeVanish(c.gateFadeTime);                    // 스르르 사라짐 (그동안 움직임·피격·조준 없음, 보상 없음)
        if (Instance == null) return;
        Instance.GateHit(at);
        Instance.Damage(damage);
    }

    // 문에 부딪힘: 문 자리에 '쿵' 효과 + 흙먼지, 기지 문 흔들림
    private readonly List<Transform> gates = new List<Transform>();
    private readonly List<Vector3> gateHome = new List<Vector3>();
    private float gateShakeUntil;

    private void GateHit(Vector3 at)
    {
        RunConfig c = RunConfig.Instance;
        SpriteFx.StarBurst(at, c.gateHitFxSize);
        SpriteFx.BossDeathDust(at, c.gateHitFxSize);
        if (gates.Count == 0) FindGates();
        gateShakeUntil = Time.time + c.gateShakeTime;
    }

    private void FindGates()
    {
        foreach (string n in RunConfig.Instance.gateObjectNames)
        {
            GameObject g = GameObject.Find(n);
            if (g == null) continue;
            gates.Add(g.transform);
            gateHome.Add(g.transform.position);
        }
    }

    private void UpdateGateShake()
    {
        if (gates.Count == 0) return;
        RunConfig c = RunConfig.Instance;
        float left = gateShakeUntil - Time.time;
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i] == null) continue;
            if (left <= 0f) { gates[i].position = gateHome[i]; continue; }
            float k = left / Mathf.Max(0.01f, c.gateShakeTime);              // 점점 약해짐
            gates[i].position = gateHome[i] + new Vector3(Mathf.Sin(Time.time * 70f) * c.gateShakeAmount * k, 0f, 0f);
        }
    }

    public void Damage(int amount)
    {
        if (gm == null || !gm.isLive || BaseHp <= 0)
            return;
        BaseHp = Mathf.Max(0, BaseHp - amount);
        panelHitAt = Time.unscaledTime;                      // 기지 체력 판: 붉게 번쩍 + 좌우 흔들림
        CameraShake.Play(config.baseHitShakePower, config.baseHitShakeTime);   // 기지 피해: 화면 살짝 흔들림
        if (BaseHp <= 0)
        {
            // 기지 함락: 플레이어 사망과 같은 실패 처리 (Player가 체력 0을 보고 쓰러짐 → 게임 오버, 결과 보상 계산)
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ShowBanner("기지가 무너졌다냥...");
            gm.health = 0f;
        }
    }

    // 밤 종료: 길 위 밤 몬스터 즉시 소멸 (기지 피해·보상 없음)
    public void ClearNight()
    {
        foreach (Enemy e in new List<Enemy>(alive))
        {
            if (e != null && e.IsAlive && e.NightWave) e.Vanish();
        }
        alive.Clear();
    }

    // 밤 몬스터 처치 보상: 냥냥코인 100% (코인 획득량 배율만큼 추가)
    public static void DropCoins(Vector3 position)
    {
        float chance = RunStats.Get(RunStat.CoinDrop) * RunStats.Get(RunStat.NightCoinDrop);   // 질주하는 밤: 오늘 밤 코인 배율
        int count = Mathf.Max(1, Mathf.FloorToInt(chance));
        if (Random.value < chance - Mathf.FloorToInt(chance)) count++;
        Coin.Drop(position, count);
    }

    // 귀환 준비 중 기지 쪽으로 움직이면 이동속도 배율 (Player 이동에 곱함)
    public static float ReturnSpeedScale(Vector2 moveDir, Vector2 playerPos)
    {
        DayNightCycle dn = DayNightCycle.Instance;
        if (dn == null || !dn.InReturnPrep || moveDir.sqrMagnitude < 0.0001f)
            return 1f;
        Vector2 toBase = (Vector2)dn.MapCenter - playerPos;
        if (toBase.sqrMagnitude < 0.25f)
            return 1f;
        RunConfig c = RunConfig.Instance;
        return Vector2.Angle(moveDir, toBase) <= c.returnAngle ? 1f + c.returnSpeedBonus : 1f;
    }

    // ---------------------------------------------------------------
    // 화면: 기지 체력 판 (오른쪽 위, 천막 아이콘 + 게이지), 귀환 준비 문구, 기지 방향 표시
    // ---------------------------------------------------------------
    private void BuildUi()
    {
        Canvas canvas = RunUI.CreateCanvas("Night Defense HUD", -4, false);   // 시계 HUD(-5) 바로 위, 레벨업 창 아래
        canvasRect = (RectTransform)canvas.transform;
        Vector2 top = new Vector2(0.5f, 1f);
        MiniBossConfig mb = MiniBossConfig.Instance;
        Sprite bar = config.morningBarGreen != null ? config.morningBarGreen : config.morningBarRed;

        // 양피지 판 (능력치 판과 같은 그림·투명도), 화면 오른쪽 위
        Image panel = RunUI.AssetPanel("Base Hp", canvas.transform, config.cardSprite, config.cardBorder,
            new Color(0.1f, 0.08f, 0.06f, 0.8f), new Vector2(1f, 1f), config.baseHpPanelOffset, config.baseHpPanelSize);
        panel.rectTransform.pivot = new Vector2(1f, 1f);
        panel.rectTransform.anchoredPosition = config.baseHpPanelOffset;
        if (config.cardSprite != null) panel.color = new Color(1f, 1f, 1f, config.baseHpPanelAlpha);   // 배경 반투명
        panel.raycastTarget = false;
        barRoot = panel.gameObject;

        panel.rectTransform.localScale = Vector3.one * config.baseHpPanelScale;   // 화면을 덜 가리게 판 전체 축소
        // 피격 번쩍임: 판과 같은 양피지 그림을 붉게 (판 바탕 위, 아이콘·글자 아래)
        panelHitFlash = RunUI.Icon("Hit Flash", barRoot.transform, config.cardSprite, new Vector2(0.5f, 0.5f), Vector2.zero, config.baseHpPanelSize);
        panelHitFlash.type = panel.type;
        panelHitFlash.pixelsPerUnitMultiplier = panel.pixelsPerUnitMultiplier;
        panelHitFlash.raycastTarget = false;
        panelHitFlash.color = new Color(0f, 0f, 0f, 0f);
        panelHitFlash.rectTransform.SetSiblingIndex(0);

        // 레퍼런스 배치: 위쪽 [아이콘][제목 / 현재 체력], 아래쪽 판 너비 게이지
        float pad = 14f;
        Vector2 size = config.baseHpPanelSize;
        float barH = config.baseHpBarSize.y;
        barWidth = size.x - pad * 2f;
        float topH = size.y - pad * 2f - barH - 6f;              // 아이콘·글자 영역 높이
        Vector2 topLeft = new Vector2(0f, 1f);

        Image icon = RunUI.Icon("Tent", barRoot.transform, config.tentSprite, topLeft,
            new Vector2(pad + config.baseHpIconSize * 0.5f, -pad - topH * 0.5f), Vector2.one * config.baseHpIconSize);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;

        float left = pad * 2f + config.baseHpIconSize;
        Text title = RunUI.Label("Title", barRoot.transform, "기지 체력", config.baseHpFontSize, config.parchmentTextColor, TextAnchor.UpperCenter,
            topLeft, Vector2.zero, new Vector2(size.x - left - pad, topH * 0.5f), 0f);
        title.rectTransform.pivot = topLeft;
        title.rectTransform.anchoredPosition = new Vector2(left, -pad);
        title.raycastTarget = false;
        RunUI.ApplyPanelFont(title);

        // 현재 체력 글자: 게이지 밖(제목 아래 오른쪽)에 외곽선 없이 → 게이지 위 외곽선 깨짐 없음
        barLabel = RunUI.Label("Label", barRoot.transform, "", config.baseHpFontSize, config.parchmentTextColor, TextAnchor.LowerRight,
            new Vector2(1f, 1f), Vector2.zero, new Vector2(size.x - left - pad, topH * 0.5f), 0f);
        barLabel.rectTransform.pivot = new Vector2(1f, 1f);
        barLabel.rectTransform.anchoredPosition = new Vector2(-pad, -pad - topH * 0.5f);
        barLabel.raycastTarget = false;
        RunUI.ApplyPanelFont(barLabel);

        RectTransform barArea = RunUI.Rect("Bar", barRoot.transform, new Vector2(0.5f, 0f), new Vector2(0f, pad + barH * 0.5f), new Vector2(barWidth, barH));
        Vector2 barSize = new Vector2(barWidth, barH);
        // 게이지 그림: 체력 바 전용 그림(Object_Panel 바)을 그대로 크기만 맞춰 씀. 테두리가 있는 그림이면 슬라이스 + 높이에 맞춰 테두리 비례 축소
        if (config.baseHpFillSprite != null) bar = config.baseHpFillSprite;
        Sprite backSprite = config.baseHpBackSprite != null ? config.baseHpBackSprite : bar;
        float ppu = bar != null ? bar.rect.height / Mathf.Max(1f, barH) : 1f;
        Image back = RunUI.Panel("Back", barArea, new Color(0.08f, 0.05f, 0.04f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, barSize);
        back.raycastTarget = false;
        if (bar != null)
        {
            back.sprite = backSprite;
            back.type = backSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            back.pixelsPerUnitMultiplier = ppu;
            back.color = config.baseHpBackColor;
        }
        // 채움: 왼쪽부터 잘라 보이는 마스크 안에 전체 크기 바
        barMask = RunUI.Rect("Fill Mask", barArea, new Vector2(0f, 0.5f), Vector2.zero, barSize);
        barMask.pivot = new Vector2(0f, 0.5f);
        barMask.gameObject.AddComponent<RectMask2D>();
        barFill = RunUI.Panel("Fill", barMask, config.baseHpColor, new Vector2(0f, 0.5f), Vector2.zero, barSize);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.anchoredPosition = Vector2.zero;
        barFill.raycastTarget = false;
        if (bar != null)
        {
            barFill.sprite = bar;
            barFill.type = bar.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            barFill.pixelsPerUnitMultiplier = ppu;
            barFill.color = Color.white;
        }

        prepText = RunUI.Label("Return Prep", canvas.transform, config.returnPrepText, 40, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, top, new Vector2(0f, config.returnTextY), new Vector2(900f, 60f), 3f);
        prepText.gameObject.SetActive(false);
        // '대장 몬스터 출현!!' + 아이콘: 'N일차 밤이 찾아왔다냥!' 알림과 같은 자리 (문구만 깜빡임)
        Vector2 mid = new Vector2(0.5f, 0.5f);
        bossNoticeRoot = RunUI.Rect("Boss Unit Notice", canvas.transform, mid, new Vector2(0f, config.bossUnitTextY), new Vector2(900f, 80f));
        bossNotice = RunUI.Label("Text", bossNoticeRoot, config.bossUnitText, config.bossUnitFontSize, config.bossUnitTextColor, TextAnchor.MiddleCenter, mid, Vector2.zero, new Vector2(800f, 80f), 3f);
        bossNoticeIcon = RunUI.Icon("Icon", bossNoticeRoot, config.bossUnitIcon, mid, Vector2.zero, Vector2.one * config.bossUnitIconSize);
        bossNoticeIcon.preserveAspect = true;
        bossNoticeIcon.raycastTarget = false;
        bossNoticeIcon.enabled = config.bossUnitIcon != null;
        float iconW = bossNoticeIcon.enabled ? config.bossUnitIconSize + 10f : 0f;   // 문구와 아이콘을 한 줄 가운데로
        float textW = bossNotice.preferredWidth;
        bossNotice.rectTransform.anchoredPosition = new Vector2(-iconW * 0.5f, 0f);
        bossNoticeIcon.rectTransform.anchoredPosition = new Vector2(textW * 0.5f + 10f + config.bossUnitIconSize * 0.5f - iconW * 0.5f, 0f);
        bossNoticeRoot.gameObject.SetActive(false);

        // 기지 방향 표시: 나침반 고리 + 바깥쪽 화살표 (중간보스 위치 표시와 같은 그림)
        Image ring = RunUI.Icon("Base Arrow", canvas.transform, mb != null ? mb.indicatorFrame : null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 64f);
        ring.enabled = ring.sprite != null;
        RunUI.Label("Text", ring.transform, "기지", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 30f), 2f);
        Image arrow = RunUI.Icon("Arrow", ring.transform, mb != null ? mb.indicatorArrow : null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 28f);
        arrow.preserveAspect = true;
        arrow.color = new Color(1f, 0.85f, 0.4f, 1f);
        arrow.enabled = arrow.sprite != null;
        baseArrow = ring.rectTransform;
        baseArrow.gameObject.SetActive(false);
    }

    private void UpdateUi()
    {
        DayNightCycle dn = DayNightCycle.Instance;
        bool show = dn != null && dn.HudVisible && gm != null && !gm.isBossSpawn;
        if (barRoot.activeSelf != show) barRoot.SetActive(show);
        if (show)
        {
            float k = BaseMaxHp > 0 ? BaseHp / (float)BaseMaxHp : 0f;
            barMask.sizeDelta = new Vector2(barWidth * k, config.baseHpBarSize.y);
            barLabel.text = BaseHp + " / " + BaseMaxHp;
            float ht = Time.unscaledTime - panelHitAt;
            float hk = ht < config.baseHitPanelTime ? 1f - ht / config.baseHitPanelTime : 0f;
            Color hc = config.baseHitPanelColor;
            panelHitFlash.color = new Color(hc.r, hc.g, hc.b, hc.a * hk);
            ((RectTransform)barRoot.transform).anchoredPosition = config.baseHpPanelOffset
                + new Vector2(Mathf.Sin(ht * config.baseHitPanelShakeSpeed) * config.baseHitPanelShake * hk, 0f);
        }

        bool prep = show && dn.InReturnPrep && gm.isLive;
        if (prepText.gameObject.activeSelf != prep) prepText.gameObject.SetActive(prep);
        bool notice = show && Time.unscaledTime < bossNoticeUntil;   // '대장 몬스터 출현!!' 알림
        if (bossNoticeRoot.gameObject.activeSelf != notice) bossNoticeRoot.gameObject.SetActive(notice);
        if (notice)
        {
            float fade = Mathf.Clamp01((bossNoticeUntil - Time.unscaledTime) / 0.4f);   // 끝날 때 함께 사라짐
            float blink = 0.25f + 0.75f * Mathf.Abs(Mathf.Cos((config.bossUnitTextTime - (bossNoticeUntil - Time.unscaledTime)) * config.bossUnitBlinkSpeed));
            Color tc = config.bossUnitTextColor;
            bossNotice.color = new Color(tc.r, tc.g, tc.b, tc.a * blink * fade);    // 문구만 깜빡임
            bossNoticeIcon.color = new Color(1f, 1f, 1f, fade);                     // 아이콘은 그대로
        }
        if (prep)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            prepText.color = new Color(1f, 0.85f, 0.4f, pulse);
        }
        UpdateBaseArrow(prep ? dn : null);
    }

    // 기지가 화면 밖이면 화면 가장자리에 방향 표시 (화살표는 기지 쪽을 가리킴)
    private void UpdateBaseArrow(DayNightCycle dn)
    {
        Camera cam = Camera.main;
        bool show = false;
        if (dn != null && cam != null && Time.timeScale > 0f)
        {
            Vector3 vp = cam.WorldToViewportPoint(dn.MapCenter);
            show = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
            if (show)
            {
                Vector2 half = canvasRect.rect.size * 0.5f - new Vector2(60f, 60f);
                Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                float scale = Mathf.Min(half.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x * canvasRect.rect.width)), half.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y * canvasRect.rect.height)));
                Vector2 pos = new Vector2(dir.x * canvasRect.rect.width, dir.y * canvasRect.rect.height) * scale;
                baseArrow.anchoredPosition = pos;
                Transform arrow = baseArrow.Find("Arrow");
                if (arrow != null)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);   // 그림은 위쪽 화살표
                    ((RectTransform)arrow).anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * 44f;
                }
            }
        }
        if (baseArrow.gameObject.activeSelf != show) baseArrow.gameObject.SetActive(show);
    }
}
